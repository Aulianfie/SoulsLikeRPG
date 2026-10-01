using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMotor), typeof(EnemyAnimator))]
[RequireComponent(typeof(EnemyTerritory), typeof(BossSkillRunner))]
public sealed class BossBrain : MonoBehaviour, ICheckpointResettable
{
    [SerializeField] Transform _targetOverride;
    [SerializeField] BossSkillData[] _skills;
    [SerializeField] BossSelectionSettings _selection = new BossSelectionSettings();
    [SerializeField] LayerMask _obstacleLayers = 1;
    [SerializeField] int _randomSeed = 1781;
    [SerializeField] bool _automatic = true;
    [SerializeField] bool _logDecisions;
    readonly BossBlackboard board = new BossBlackboard();
    readonly BossActionSelector selector = new BossActionSelector();
    NavMeshPath path;
    readonly RaycastHit[] throwHits = new RaycastHit[16];
    EnemyHealth health;
    PlayerHealth targetHealth;
    EnemyMotor motor;
    EnemyAnimator animationDriver;
    EnemyTerritory territory;
    BossSkillRunner runner;
    BTNode root;
    System.Random random;
    Quaternion homeRotation;
    bool started, engaged, returning, chasing, dead;
    float nextSense, nextRepath, chaseDeadline;
    int previousSide;
    public BossBlackboard Blackboard => board;
    public BossActionSelector Selector => selector;
    public BossSkillRunner Runner => runner;
    public BossSelectionSettings Selection => _selection;
    public BossSkillData[] Skills => _skills;
    public bool Automatic => _automatic;
    public string LastDecision { get; private set; } = "等待目标";
    public int DecisionCount { get; private set; }
    public bool HasValidTarget => board.Target != null && board.Target.gameObject.activeInHierarchy && (targetHealth == null || !targetHealth.IsDead);

    void Awake()
    {
        path = new NavMeshPath();
        health = GetComponent<EnemyHealth>(); motor = GetComponent<EnemyMotor>(); animationDriver = GetComponent<EnemyAnimator>();
        territory = GetComponent<EnemyTerritory>(); runner = GetComponent<BossSkillRunner>();
        homeRotation = transform.rotation; runner.Bind(board);
        root = new BTSelector("BossRoot",
            Branch("死亡", () => health.CurrentHealth <= 0, dt => { EnterDeath(); return BTStatus.Running; }),
            Branch("回位", () => returning || engaged && (!HasValidTarget || !territory.IsInsideLeashArea(transform.position) || !territory.IsInsideLeashArea(board.Target.position)), ReturnHome),
            Branch("技能执行", () => runner.IsRunning, dt => runner.Tick(dt)),
            Branch("接近计划", () => chasing, Chase),
            Branch("暂停", () => !_automatic, dt => { motor.Stop(); return BTStatus.Running; }),
            Branch("未索敌", () => !HasValidTarget || !engaged && !territory.IsInsideDetectionArea(board.Target.position), dt => { motor.Stop(); return BTStatus.Running; }),
            Branch("后摇间隔", () => Time.time < board.NextDecisionTime, dt => BTStatus.Running),
            new BTAction("动态选招", Decide));
    }

    BTNode Branch(string name, Func<bool> condition, Func<float, BTStatus> action)
        => new BTSequence(name, new BTCondition(name + "条件", condition), new BTAction(name, dt => { board.ActiveNode = name; return action(dt); }));

    void OnEnable() { health.Died += EnterDeath; if (started) Initialize(); }
    void Start() { started = true; Initialize(); }
    void Initialize()
    {
        board.Reset(); random = new System.Random(_randomSeed); engaged = returning = chasing = dead = false;
        ResolveTarget(); nextSense = 0; nextRepath = 0; animationDriver.PlayIdle();
    }
    void ResolveTarget()
    {
        if (_targetOverride == null)
        {
            foreach (var player in FindObjectsOfType<PlayerHealth>()) if (player.gameObject.scene == gameObject.scene) { _targetOverride = player.transform; break; }
        }
        board.Target = _targetOverride;
        targetHealth = board.Target != null ? board.Target.GetComponentInParent<PlayerHealth>() : null;
    }
    void Update()
    {
        if (!started) return;
        if (Time.time >= nextSense) { Sense(.12f); nextSense = Time.time + .12f; }
        root.Tick(Time.deltaTime);
    }
    void Sense(float dt)
    {
        if (!HasValidTarget) return;
        Vector3 delta = board.Target.position - transform.position; delta.y = 0;
        board.Distance = delta.magnitude; board.Angle = Vector3.Angle(transform.forward, delta); board.Side = Vector3.Dot(transform.right, delta);
        int side = Mathf.Abs(board.Side) > _selection.sideDeadZone ? (board.Side < 0 ? -1 : 1) : 0;
        board.SideDwell = side != 0 && side == previousSide && board.Distance <= _selection.nearRange ? board.SideDwell + dt : 0; previousSide = side;
        board.FarDwell = board.Distance >= _selection.farRange ? board.FarDwell + dt : 0;
        board.MovementClear = motor.CanMoveSpecial(delta, Mathf.Min(2, board.Distance), _obstacleLayers);
        board.PathReachable = motor.IsOnNavMesh && NavMesh.SamplePosition(board.Target.position, out NavMeshHit sample, 3, motor.AreaMask)
            && NavMesh.CalculatePath(transform.position, sample.position, motor.AreaMask, path) && path.status == NavMeshPathStatus.PathComplete;
        board.ThrowClear = ThrowLineClear(runner.ThrowPosition, board.Target.position + Vector3.up);
    }
    bool ThrowLineClear(Vector3 start, Vector3 target)
    {
        float duration = Mathf.Clamp(Vector3.Distance(start, target) / 12, .65f, 2);
        Vector3 velocity = (target - start - .5f * Physics.gravity * duration * duration) / duration;
        Vector3 previous = start;
        for (int i = 1; i <= 8; i++)
        {
            float t = duration * i / 8; Vector3 next = start + velocity * t + .5f * Physics.gravity * t * t;
            int count = Physics.SphereCastNonAlloc(previous, .45f, (next - previous).normalized, throwHits, Vector3.Distance(next, previous), _obstacleLayers, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < count; h++) if (!throwHits[h].collider.transform.IsChildOf(transform) && throwHits[h].collider.GetComponentInParent<PlayerHealth>() == null) return false;
            previous = next;
        }
        return true;
    }
    BTStatus Decide(float dt)
    {
        engaged = true; board.ActiveNode = "动态选招";
        BossSkillData skill = selector.Select(_skills, board, _selection, Time.time, (float)random.NextDouble(), (float)random.NextDouble(), out bool run);
        DecisionCount++;
        LastDecision = skill != null ? skill.Id : run ? "快跑接近" : "无可用技能";
        if (_logDecisions) Debug.Log("[Golem] " + LastDecision + " distance=" + board.Distance.ToString("F1") + " last=" + (board.LastSkill != null ? board.LastSkill.Id : "None"), this);
        if (skill != null && runner.Begin(skill)) return BTStatus.Success;
        if ((run || board.Distance > _selection.nearRange - .7f) && board.PathReachable) { BeginChase(); return BTStatus.Success; }
        motor.Stop(); motor.FaceTarget(board.Target.position, dt);
        board.NextDecisionTime = Time.time + .2f; return BTStatus.Success;
    }
    void BeginChase() { chasing = true; nextRepath = 0; chaseDeadline = Time.time + 10; animationDriver.PlayChase(); }
    BTStatus Chase(float dt)
    {
        if (board.Distance <= _selection.nearRange - .7f || Time.time >= chaseDeadline || !board.PathReachable)
        { chasing = false; motor.Stop(); animationDriver.PlayIdle(); board.NextDecisionTime = Time.time + .15f; return BTStatus.Success; }
        if (Time.time >= nextRepath) { motor.MoveTo(board.Target.position, _selection.nearRange - .9f); nextRepath = Time.time + .2f; }
        return BTStatus.Running;
    }
    BTStatus ReturnHome(float dt)
    {
        if (!returning) { returning = true; chasing = false; runner.Abort(); runner.ClearProjectiles(); animationDriver.PlayChase(); nextRepath = 0; }
        if (Vector3.Distance(transform.position, territory.HomePosition) <= .5f) { ResetForCheckpoint(); return BTStatus.Success; }
        if (Time.time >= nextRepath) { motor.MoveTo(territory.HomePosition, .2f); nextRepath = Time.time + .2f; }
        return BTStatus.Running;
    }
    void EnterDeath()
    {
        if (dead) return; dead = true; chasing = returning = false;
        runner.Abort(); runner.ClearProjectiles(); motor.Stop(); animationDriver.PlayDeath(); board.ActiveNode = "死亡";
    }
    public void ResetForCheckpoint()
    {
        if (!isActiveAndEnabled) return;
        root.Abort(); runner.Abort(false); runner.ClearProjectiles(); board.Reset();
        motor.Teleport(territory.HomePosition, homeRotation); health.RestoreFull(); Initialize();
    }
    public void SetAutomatic(bool automatic) { _automatic = automatic; chasing = false; runner.Abort(); motor.Stop(); animationDriver.PlayIdle(); }
    public bool TryForceSkill(string id)
    {
        if (health.CurrentHealth <= 0 || !HasValidTarget || runner.IsRunning) return false;
        Sense(.12f); foreach (var skill in _skills) if (skill != null && skill.Id == id) { chasing = false; engaged = true; return runner.Begin(skill); }
        return false;
    }
    void OnDisable() { if (health != null) health.Died -= EnterDeath; root?.Abort(); runner?.Abort(false); runner?.ClearProjectiles(); motor?.Stop(); }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, _selection.nearRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, _selection.farRange);
    }
}
