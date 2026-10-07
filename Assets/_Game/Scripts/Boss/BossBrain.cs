using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMotor), typeof(EnemyAnimator))]
[RequireComponent(typeof(EnemyTerritory), typeof(BossSkillRunner))]
public sealed class BossBrain : MonoBehaviour, ICheckpointResettable
{
    private const float SenseInterval = .12f;
    private const float RepathInterval = .2f;
    private const float ChaseDistanceBuffer = .7f;
    private const float ChasePlanTimeout = 10;
    private const int ThrowTrajectorySampleCount = 8;

    // Serialized fields
    [SerializeField]
    private Transform _targetOverride;
    [SerializeField]
    private BossSkillData[] _skills;
    [SerializeField]
    private BossSelectionSettings _selection = new BossSelectionSettings();
    [SerializeField]
    private LayerMask _obstacleLayers = 1; // LayerMask = 1 对应的就是 Unity 中的 Default 层
    [SerializeField]
    private int _randomSeed = 1781;
    [SerializeField]
    private bool _automatic = true;
    [SerializeField]
    private bool _logDecisions;

    // Dependencies
    private EnemyHealth _health;
    private PlayerHealth _targetHealth;
    private EnemyMotor _motor;
    private EnemyAnimator _animationDriver;
    private EnemyTerritory _territory;
    private BossSkillRunner _skillRunner;

    // Runtime state
    private readonly BossBlackboard _blackboard = new BossBlackboard();
    private readonly BossActionSelector _actionSelector = new BossActionSelector();
    private NavMeshPath _navigationPath;
    private readonly RaycastHit[] _throwObstacleHits = new RaycastHit[16];
    private BTNode _behaviorTreeRoot;
    private System.Random _decisionRandom;
    private Quaternion _homeRotation;
    private bool _hasStarted;
    private bool _hasEngagedTarget;
    private bool _isReturningHome;
    private bool _isChasing;
    private bool _hasEnteredDeath;
    private float _nextSenseTime;
    private float _nextRepathTime;
    private float _chaseDeadline;
    private int _previousTargetSide;

    // Public properties
    public BossBlackboard Blackboard => _blackboard;
    public BossActionSelector Selector => _actionSelector;
    public BossSkillRunner Runner => _skillRunner;
    public BossSelectionSettings Selection => _selection;
    public BossSkillData[] Skills => _skills;
    public bool Automatic => _automatic;

    /// <summary>
    /// 获取最后一次决策
    /// </summary>
    public string LastDecision { get; private set; } = "等待目标";
    public int DecisionCount { get; private set; }
    public bool HasValidTarget => _blackboard.Target != null &&
        _blackboard.Target.gameObject.activeInHierarchy &&
        (_targetHealth == null || !_targetHealth.IsDead);

    private void Awake()
    {
        _navigationPath = new NavMeshPath();
        _health = GetComponent<EnemyHealth>();
        _motor = GetComponent<EnemyMotor>();
        _animationDriver = GetComponent<EnemyAnimator>();
        _territory = GetComponent<EnemyTerritory>();
        _skillRunner = GetComponent<BossSkillRunner>();
        _homeRotation = transform.rotation;
        _skillRunner.Bind(_blackboard);
        // 高优先级分支会打断正在运行的行动，顺序也决定死亡、回位与技能的优先级。
        _behaviorTreeRoot = new BTSelector(
            "BossRoot",
            Branch(
                "死亡",
                () => _health.CurrentHealth <= 0,
                dt =>
                {
                    EnterDeath();
                    return BTStatus.Running;
                }
            ),
            Branch(
                "回位",
                () => _isReturningHome ||
                    _hasEngagedTarget &&
                    (!HasValidTarget ||
                        !_territory.IsInsideLeashArea(transform.position) ||
                        !_territory.IsInsideLeashArea(_blackboard.Target.position)), 
                        // 玩家 / Boss跑出 Leash 区域，Boss 都要回位
                ReturnHome
            ),
            Branch("技能执行", () => _skillRunner.IsRunning, dt => _skillRunner.Tick(dt)),
            Branch("接近计划", () => _isChasing, Chase),
            Branch(
                "暂停",
                () => !_automatic,
                dt =>
                {
                    _motor.Stop();
                    return BTStatus.Running;
                }
            ),
            Branch(
                "未索敌",
                () => !HasValidTarget ||
                    !_hasEngagedTarget &&
                    !_territory.IsInsideDetectionArea(_blackboard.Target.position),
                dt =>
                {
                    _motor.Stop();
                    return BTStatus.Running;
                }
            ),
            Branch("后摇间隔", () => Time.time < _blackboard.NextDecisionTime, dt => BTStatus.Running),
            new BTAction("动态选招", Decide)
        );
    }

    /// <summary>
    /// 创建一个分支节点，用于在行为树中根据条件执行不同的操作。
    /// </summary>
    /// <param name="name"> 分支节点的名称 </param>
    /// <param name="condition"> 判断条件 </param>
    /// <param name="action"> 执行的操作 </param>
    /// <returns> 返回一个分支节点 </returns>
    private BTNode Branch(string name, Func<bool> condition, Func<float, BTStatus> action)
    {
        return new BTSequence(
            name,
            new BTCondition(name + "条件", condition),
            new BTAction(
                name,
                dt =>
                {
                    _blackboard.ActiveNode = name;
                    return action(dt);
                }
            )
        );
    }

    private void OnEnable()
    {
        _health.Died += EnterDeath;
        if (_hasStarted)
        {
            Initialize();
        }
    }

    private void Start()
    {
        _hasStarted = true;
        Initialize();
    }

    private void Initialize()
    {
        _blackboard.Reset();
        _decisionRandom = new System.Random(_randomSeed);
        _hasEnteredDeath = false;
        _isChasing = false;
        _isReturningHome = false;
        _hasEngagedTarget = false;
        ResolveTarget();
        _nextSenseTime = 0;
        _nextRepathTime = 0;
        _animationDriver.PlayIdle();
    }
    /// <summary>
    /// 解析目标。
    /// </summary>
    /// <returns></returns>
    private void ResolveTarget()
    {
        if (_targetOverride == null)
        {
            foreach (var player in FindObjectsOfType<PlayerHealth>())
            {
                if (player.gameObject.scene == gameObject.scene)
                {
                    _targetOverride = player.transform;
                    break;
                }
            }
        }

        _blackboard.Target = _targetOverride;
        if (_blackboard.Target != null)
        {
            _targetHealth = _blackboard.Target.GetComponentInParent<PlayerHealth>();
        }
        else
        {
            _targetHealth = null;
        }
    }

    private void Update()
    {
        if (!_hasStarted)
        {
            return;
        }
        // 大概0.12秒感知一次玩家状态，避免每帧都计算距离、角度、侧向位置、停留时间等信息。
        if (Time.time >= _nextSenseTime)
        {
            Sense(SenseInterval);
            _nextSenseTime = Time.time + SenseInterval;
        }

        _behaviorTreeRoot.Tick(Time.deltaTime);
    }
    /// <summary>
    /// 感知玩家的状态，包括距离、角度、侧向位置、停留时间等信息，并更新BossBlackboard数据。
    /// </summary>
    /// <param name="dt"></param>
    private void Sense(float dt)
    {
        if (!HasValidTarget)
        {
            return;
        }

        // 选招使用水平距离，避免玩家跳跃改变近距、远距行动池。
        Vector3 delta = _blackboard.Target.position - transform.position;
        delta.y = 0;
        _blackboard.Distance = delta.magnitude;
        _blackboard.Angle = Vector3.Angle(transform.forward, delta);
        _blackboard.Side = Vector3.Dot(transform.right, delta);
        int side;
        if (Mathf.Abs(_blackboard.Side) > _selection.sideDeadZone)
        {
            if (_blackboard.Side < 0)
            {
                side = -1;
            }
            else
            {
                side = 1;
            }
        }
        else
        {
            side = 0;
        }

        // 记录玩家在 Boss 同一侧停留了多久
        bool isStayingOnSameSide = side != 0 &&
            side == _previousTargetSide &&
            _blackboard.Distance <= _selection.nearRange;
        _blackboard.SideDwell = isStayingOnSameSide ? _blackboard.SideDwell + dt : 0;
        _previousTargetSide = side;
        
        // 记录玩家在 Boss 远距离区域停留了多久
        _blackboard.FarDwell = _blackboard.Distance >= _selection.farRange ? _blackboard.FarDwell + dt : 0;

        // 检查 Boss 如果执行 Dash / Whirlwind 这种特殊移动，会不会撞墙
        _blackboard.MovementClear = _motor.CanMoveSpecial(delta, Mathf.Min(2, _blackboard.Distance), _obstacleLayers);
        
        // 检查 Boss 能不能正常走到玩家附近
        _blackboard.PathReachable = _motor.IsOnNavMesh &&
            NavMesh.SamplePosition(_blackboard.Target.position, out NavMeshHit sample, 3, _motor.AreaMask) &&
            NavMesh.CalculatePath(transform.position, sample.position, _motor.AreaMask, _navigationPath) &&
            _navigationPath.status == NavMeshPathStatus.PathComplete;
        
        // 检查 Boss 投掷技能的轨迹是否会被障碍物挡住
        _blackboard.ThrowClear = ThrowLineClear(_skillRunner.ThrowPosition, _blackboard.Target.position + Vector3.up);
    }

    /// <summary>
    /// 投石技能专用检查，即从起点到目标点的抛物线轨迹上没有障碍物阻挡。
    /// 它先计算一条抛物线，然后把轨迹分成若干段（ThrowTrajectorySampleCount = 8）
    /// 使用 SphereCast 检查每一段是否有障碍物阻挡。
    /// </summary>
    /// <param name="start"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    private bool ThrowLineClear(Vector3 start, Vector3 target)
    {
        float duration = Mathf.Clamp(Vector3.Distance(start, target) / 12, .65f, 2);
        Vector3 velocity = (target - start - .5f * Physics.gravity * duration * duration) / duration;
        Vector3 previous = start;
        for (int i = 1; i <= ThrowTrajectorySampleCount; i++)
        {
            float t = duration * i / ThrowTrajectorySampleCount;
            Vector3 next = start + velocity * t + .5f * Physics.gravity * t * t;
            int count = Physics.SphereCastNonAlloc(
                previous,
                .45f,
                (next - previous).normalized,
                _throwObstacleHits,
                Vector3.Distance(next, previous),
                _obstacleLayers,
                QueryTriggerInteraction.Ignore
            );
            for (int h = 0; h < count; h++)
            {
                if (!_throwObstacleHits[h].collider.transform.IsChildOf(transform) &&
                    _throwObstacleHits[h].collider.GetComponentInParent<PlayerHealth>() == null)
                {
                    return false;
                }
            }

            previous = next;
        }

        return true;
    }

    private BTStatus Decide(float dt)
    {
        _hasEngagedTarget = true;
        _blackboard.ActiveNode = "动态选招";
        BossSkillData skill = _actionSelector.Select(
            _skills,
            _blackboard,
            _selection,
            Time.time,
            (float)_decisionRandom.NextDouble(),
            (float)_decisionRandom.NextDouble(),
            out bool run
        );
        DecisionCount++;
        if (skill != null)
        {
            LastDecision = skill.Id;
        }
        else if (run)
        {
            LastDecision = "快跑接近";
        }
        else
        {
            LastDecision = "无可用技能";
        }

        if (_logDecisions)
        {
            Debug.Log(
                "[Golem] " + LastDecision +
                " distance=" + _blackboard.Distance.ToString("F1") +
                " last=" + (_blackboard.LastSkill != null ? _blackboard.LastSkill.Id : "None"),
                this
            );
        }

        // case 1: selector 选中了一个技能，尝试执行它，下一帧行为树会进入技能执行
        if (skill != null && _skillRunner.Begin(skill))
        {
            return BTStatus.Success;
        }

        // case 2: selector 返回了 run = true，Boss 决定接近玩家
        if ((run || _blackboard.Distance > _selection.nearRange - ChaseDistanceBuffer) &&
            _blackboard.PathReachable)
        {
            BeginChase();
            return BTStatus.Success;
        }

        // case 3: selector 没有选中技能，也没有决定接近玩家，Boss 进入等待状态
        _motor.Stop();
        _motor.FaceTarget(_blackboard.Target.position, dt);
        _blackboard.NextDecisionTime = Time.time + .2f;
        return BTStatus.Success;
    }

    private void BeginChase()
    {
        _isChasing = true;
        _nextRepathTime = 0;
        _chaseDeadline = Time.time + ChasePlanTimeout;
        _animationDriver.PlayChase();
    }

    private BTStatus Chase(float dt)
    {
        bool shouldStopChasing = _blackboard.Distance <= _selection.nearRange - ChaseDistanceBuffer ||
            Time.time >= _chaseDeadline ||
            !_blackboard.PathReachable;
        if (shouldStopChasing)
        {
            _isChasing = false;
            _motor.Stop();
            _animationDriver.PlayIdle();
            _blackboard.NextDecisionTime = Time.time + .15f;
            return BTStatus.Success;
        }

        if (Time.time >= _nextRepathTime)
        {
            _motor.MoveTo(_blackboard.Target.position, _selection.nearRange - .9f);
            _nextRepathTime = Time.time + RepathInterval;
        }

        return BTStatus.Running;
    }

    private BTStatus ReturnHome(float dt)
    {
        // 一旦进入回位就保持该行动，直到原有重置入口完成回位。
        if (!_isReturningHome)
        {
            _isReturningHome = true;
            _isChasing = false;
            _skillRunner.Abort();
            _skillRunner.ClearProjectiles();
            _animationDriver.PlayChase();
            _nextRepathTime = 0;
        }

        if (Vector3.Distance(transform.position, _territory.HomePosition) <= .5f)
        {
            ResetForCheckpoint();
            return BTStatus.Success;
        }

        if (Time.time >= _nextRepathTime)
        {
            _motor.MoveTo(_territory.HomePosition, .2f);
            _nextRepathTime = Time.time + RepathInterval;
        }

        return BTStatus.Running;
    }

    private void EnterDeath()
    {
        if (_hasEnteredDeath)
        {
            return;
        }

        _hasEnteredDeath = true;
        _isReturningHome = false;
        _isChasing = false;
        _skillRunner.Abort();
        _skillRunner.ClearProjectiles();
        _motor.Stop();
        _animationDriver.PlayDeath();
        _blackboard.ActiveNode = "死亡";
    }

    public void ResetForCheckpoint()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _behaviorTreeRoot.Abort();
        _skillRunner.Abort(false);
        _skillRunner.ClearProjectiles();
        _blackboard.Reset();
        _motor.Teleport(_territory.HomePosition, _homeRotation);
        _health.RestoreFull();
        Initialize();
    }

    public void SetAutomatic(bool automatic)
    {
        _automatic = automatic;
        _isChasing = false;
        _skillRunner.Abort();
        _motor.Stop();
        _animationDriver.PlayIdle();
    }

    public bool TryForceSkill(string id)
    {
        if (_health.CurrentHealth <= 0 ||
            !HasValidTarget ||
            _skillRunner.IsRunning)
        {
            return false;
        }

        Sense(SenseInterval);
        foreach (var skill in _skills)
        {
            if (skill != null && skill.Id == id)
            {
                _isChasing = false;
                _hasEngagedTarget = true;
                return _skillRunner.Begin(skill);
            }
        }

        return false;
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.Died -= EnterDeath;
        }

        _behaviorTreeRoot?.Abort();
        _skillRunner?.Abort(false);
        _skillRunner?.ClearProjectiles();
        _motor?.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _selection.nearRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _selection.farRange);
    }
}
