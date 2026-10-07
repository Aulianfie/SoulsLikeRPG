using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyAnimator), typeof(EnemyMotor), typeof(BossDamageArea))]
public sealed class BossSkillRunner : MonoBehaviour
{
    // Serialized fields
    [SerializeField]
    private WeaponHitbox _leftHand;
    [SerializeField]
    private WeaponHitbox _rightHand;
    [SerializeField]
    private Transform _leftFoot;
    [SerializeField]
    private Transform _rightFoot;
    [SerializeField]
    private Transform _throwSocket;
    [SerializeField]
    private BossRockProjectile _rockPrefab;
    [SerializeField]
    private LayerMask _obstacleLayers = 1;

    // Dependencies
    private EnemyAnimator _animationDriver;
    private EnemyMotor _motor;
    private BossDamageArea _damageArea;
    private BossBlackboard _blackboard;

    // Runtime state
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private readonly List<BossRockProjectile> _projectiles = new List<BossRockProjectile>();
    private readonly List<GameObject> _effects = new List<GameObject>();
    private BossSkillData _currentSkill;
    private int _currentStateHash;
    private bool _isDirectionLocked;
    private bool _hasReleased;
    private bool _areHandsActive;
    private bool _hasStartedSpecialMovement;
    private bool _isMovementBlocked;
    private Vector3 _lockedDirection;
    private Vector3 _aimPosition;
    private float _startedAt;
    private float _traveledDistance;
    private float _travelDistanceLimit;
    private int _lastHitWindowIndex;
    private BossRockProjectile _heldRock;

    // Public properties
    public bool IsRunning => _currentSkill != null;
    public bool DirectionLocked => _isDirectionLocked;
    public bool Released => _hasReleased;
    public float NormalizedTime { get; private set; }

    public string Phase
    {
        get
        {
            if (_currentSkill == null)
            {
                return "无技能";
            }
            else if (NormalizedTime < _currentSkill.DirectionLock)
            {
                return "前摇追踪";
            }
            else if (NormalizedTime < _currentSkill.HitStart)
            {
                return "方向锁定";
            }
            else if (NormalizedTime <= _currentSkill.HitEnd)
            {
                return "出手";
            }
            else
            {
                return "收招";
            }
        }
    }

    public Vector3 ThrowPosition
    {
        get
        {
            if (_throwSocket != null)
            {
                return _throwSocket.position;
            }
            else
            {
                return transform.position + Vector3.up * 3;
            }
        }
    }

    public int LiveProjectileCount
    {
        get
        {
            int count = 0;
            foreach (var stone in _projectiles)
            {
                if (stone != null &&
                    stone.IsLaunched &&
                    !stone.Resolved)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private void Awake()
    {
        _animationDriver = GetComponent<EnemyAnimator>();
        _motor = GetComponent<EnemyMotor>();
        _damageArea = GetComponent<BossDamageArea>();
    }

    public void Bind(BossBlackboard blackboard)
    {
        _blackboard = blackboard;
    }

    public bool Begin(BossSkillData skill)
    {
        if (IsRunning ||
            skill == null ||
            _blackboard == null ||
            _blackboard.Target == null ||
            GetComponent<EnemyHealth>().CurrentHealth <= 0)
        {
            return false;
        }

        if (!_animationDriver.PlayState(skill.StateHash))
        {
            Debug.LogError("Boss 找不到动画状态：" + skill.StateName, this);
            return false;
        }

        Animator animator = _animationDriver.Animator;
        if (skill.DamageKind == BossDamageKind.Projectile &&
            animator.GetCurrentAnimatorStateInfo(0).fullPathHash == skill.StateHash)
        {
            // 同名投石立即重播时，先提交 CrossFade 请求，否则首个 Tick 仍可能读到旧片段。
            animator.Update(0);
        }

        _motor.Stop();
        // 双手共用同一次出手的命中集合，多段技能只在下一段出手时重新允许命中。
        _hitTargets.Clear();
        _damageArea.ResetSweep();
        _currentSkill = skill;
        _currentStateHash = skill.StateHash;
        _blackboard.Commit(skill, Time.time);
        _isMovementBlocked = false;
        _hasStartedSpecialMovement = false;
        _areHandsActive = false;
        _hasReleased = false;
        _isDirectionLocked = false;
        NormalizedTime = 0;
        _startedAt = Time.time;
        _traveledDistance = 0;
        _lastHitWindowIndex = -1;
        if (skill.Family == BossSkillFamily.Dash)
        {
            _travelDistanceLimit = Mathf.Max(.2f, _blackboard.Distance - 1.8f);
        }
        else
        {
            _travelDistanceLimit = _blackboard.Distance + 2;
        }

        _lockedDirection = transform.forward;
        _aimPosition = _blackboard.Target.position + Vector3.up;
        _animationDriver.SetSpeed(skill.PlaybackSpeed);
        return true;
    }

    /// <summary>
    /// 更新技能状态。
    /// </summary>
    /// <param name="dt"> 时间增量 </param>
    /// <returns> 返回技能执行状态 </returns>
    public BTStatus Tick(float dt)
    {
        if (!IsRunning)
        {
            return BTStatus.Success;
        }

        if (!_animationDriver.TryGetStateNormalizedTime(_currentStateHash, out float t))
        {
            if (Time.time - _startedAt > 15)
            {
                Debug.LogError("Boss 技能动画未完成：" + _currentSkill.Id, this);
                Abort();
            }

            return BTStatus.Running;
        }

        Animator animator = _animationDriver.Animator;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo incoming = animator.GetNextAnimatorStateInfo(0);
            if (incoming.fullPathHash == _currentStateHash)
            {
                // 重置后立即重播同名技能时，旧片段仍在淡出，不能沿用它的释放进度。
                t = incoming.normalizedTime;
            }
        }

        NormalizedTime = t;
        // Begin()调用时已经设置了_isDirectionLocked = false，t < DirectionLock 时仍然允许追踪目标。
        if (!_isDirectionLocked)
        {
            if (_blackboard.Target != null)
            {
                if (!_currentSkill.IsSidedGroundSlam)
                {
                    _motor.FaceTarget(_blackboard.Target.position, dt);
                }

                _aimPosition = _blackboard.Target.position + Vector3.up;
            }
            // 方向锁定后，后续的移动和投掷都将沿着锁定方向进行。
            if (t >= _currentSkill.DirectionLock)
            {
                _isDirectionLocked = true;
                _lockedDirection = transform.forward;
            }
        }
        // 处理移动，包括 DashAtk whirlwind 等技能的位移。
        if (_currentSkill.MoveSpeed > 0 &&
            t >= _currentSkill.MoveStart &&
            t <= _currentSkill.MoveEnd &&
            !_isMovementBlocked)
        {
            if (!_hasStartedSpecialMovement)
            {
                _motor.BeginSpecialMovement();
                _hasStartedSpecialMovement = true;
            }

            float step = Mathf.Min(_currentSkill.MoveSpeed * dt, Mathf.Max(0, _travelDistanceLimit - _traveledDistance));
            if (step > 0 &&
                _motor.MoveSpecial(_lockedDirection, step, _obstacleLayers))
            {
                _traveledDistance += step;
            }
            else
            {
                _isMovementBlocked = true;
                _motor.EndSpecialMovement();
            }
        }
        // 处理特殊移动结束的时机，避免在 MoveEnd 之后仍然保持特殊移动状态。
        if (_hasStartedSpecialMovement &&
            t > _currentSkill.MoveEnd)
        {
            _motor.EndSpecialMovement();
        }

        // 处理命中窗口和伤害逻辑。
        int hitWindowIndex = _currentSkill.GetHitWindowIndex(t);
        bool active = hitWindowIndex >= 0;
        if (active && hitWindowIndex != _lastHitWindowIndex)
        {
            // 窗口间的空档不能连成一次扫掠；即使一帧跨过空档，也要重新开启检测。
            CloseHands();
            _hitTargets.Clear();
            _damageArea.ResetSweep();
            _lastHitWindowIndex = hitWindowIndex;
        }
        // 处理双手攻击的伤害逻辑
        if (_currentSkill.DamageKind == BossDamageKind.Hands)
        {
            if (active &&
                !_areHandsActive)
            {
                if ((_currentSkill.Hands & 1) != 0 && // 左手
                    _leftHand != null)
                {
                    _leftHand.BeginAttack(_currentSkill.Damage, _hitTargets);
                }

                if ((_currentSkill.Hands & 2) != 0 && // 右手
                    _rightHand != null)
                {
                    _rightHand.BeginAttack(_currentSkill.Damage, _hitTargets);
                }

                _areHandsActive = true;
            }
            else if (!active &&
                _areHandsActive)
            {
                CloseHands();
            }
        }
        // 处理旋转攻击的伤害逻辑
        else if (_currentSkill.DamageKind == BossDamageKind.BodySweep && 
            active)
        {
            _damageArea.Sweep(transform.position + Vector3.up * 1.3f, _currentSkill.Radius, _currentSkill.Damage, _hitTargets);
        }
        // 处理投射物的逻辑
        if (_currentSkill.DamageKind == BossDamageKind.Projectile &&
            !_hasReleased &&
            _heldRock == null &&
            _rockPrefab != null &&
            t >= _currentSkill.ProjectilePickup)
        {
            PrepareHeldRock();
        }

        if (!_hasReleased &&
            t >= _currentSkill.Release)
        {
            _hasReleased = true;
            // 对于 GroundPulse 类技能，Release 时产生冲击波
            if (_currentSkill.DamageKind == BossDamageKind.GroundPulse)
            {
                Transform foot;
                if (_currentSkill.Side == BossSkillSide.Left)
                {
                    foot = _leftFoot;
                }
                else if (_currentSkill.Side == BossSkillSide.Right)
                {
                    foot = _rightFoot;
                }
                else
                {
                    foot = null;
                }

                Vector3 center;
                if (foot != null)
                {
                    center = foot.position;
                }
                else
                {
                    center = transform.position;
                }

                center.y = transform.position.y;
                // Stomp 技能的伤害区域是矩形，Pulse 技能的伤害区域是圆形。
                // 对于左/右脚踩地，伤害区域的中心点在 Boss 中线的左/右侧，对于跳跃落地，伤害区域的中心点在 Boss 中线的正下方。
                if (_currentSkill.IsSidedGroundSlam)
                {
                    _damageArea.Stomp(center, _currentSkill.Radius, _currentSkill.Side, _currentSkill.Damage, _hitTargets);
                }
                else
                {
                    _damageArea.Pulse(center, _currentSkill.Radius, _currentSkill.Damage, _hitTargets);
                }
            }
            // 对于Projectile类技能，Release 时生成石头
            else if (_currentSkill.DamageKind == BossDamageKind.Projectile &&
                _heldRock != null)
            {
                _heldRock.Launch(gameObject, _aimPosition, _currentSkill.Damage);
                _heldRock = null;
            }
        }
        // 技能未完成前，都返回Running状态
        if (t < _currentSkill.Completion)
        {
            return BTStatus.Running;
        }

        Finish(true);
        return BTStatus.Success;
    }

    private void CloseHands()
    {
        _leftHand?.EndAttack();
        _rightHand?.EndAttack();
        _areHandsActive = false;
    }

    private void PrepareHeldRock()
    {
        _projectiles.RemoveAll(p => p == null);
        _heldRock = Instantiate(_rockPrefab, ThrowPosition, Quaternion.identity);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_heldRock.gameObject, gameObject.scene);
        _heldRock.PrepareHeld(gameObject);
        _projectiles.Add(_heldRock);
        Transform parent = _throwSocket != null ? _throwSocket : transform;
        // 保留预制体的世界大小，避免 Boss 模型缩放同时放大或缩小石头。
        _heldRock.transform.SetParent(parent, true);
        if (_throwSocket != null)
        {
            _heldRock.transform.localPosition = Vector3.zero;
            _heldRock.transform.localRotation = Quaternion.identity;
        }
    }

    private void ClearHeldRock()
    {
        if (_heldRock != null)
        {
            _heldRock.Dissolve();
            _heldRock = null;
        }
    }

    private void Finish(bool completed)
    {
        ClearHeldRock();
        CloseHands();
        _motor.EndSpecialMovement();
        _motor.Stop();
        _animationDriver.SetSpeed(1);
        _blackboard?.Finish(Time.time, completed);
        _currentSkill = null;
        if (completed)
        {
            _animationDriver.PlayIdle();
        }
    }

    public void Abort(bool recordHistory = true)
    {
        if (_currentSkill != null &&
            recordHistory)
        {
            Finish(false);
        }
        else
        {
            ClearHeldRock();
            CloseHands();
            _motor?.EndSpecialMovement();
            _motor?.Stop();
            if (_animationDriver != null)
            {
                _animationDriver.SetSpeed(1);
            }

            _currentSkill = null;
            if (_blackboard != null)
            {
                _blackboard.CurrentSkill = null;
            }
        }
    }

    public void TrackEffect(GameObject effect)
    {
        _effects.RemoveAll(e => e == null);
        _effects.Add(effect);
    }

    public void ClearProjectiles()
    {
        foreach (var stone in _projectiles)
        {
            if (stone != null)
            {
                stone.Dissolve();
            }
        }

        _projectiles.Clear();
        _heldRock = null;
        foreach (var effect in _effects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        _effects.Clear();
    }

    private void OnDisable()
    {
        Abort(false);
        ClearProjectiles();
    }
}
