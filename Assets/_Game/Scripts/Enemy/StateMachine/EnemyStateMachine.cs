using UnityEngine;


/// <summary>
/// 管理敌人的巡逻、战斗、返回领地、受击和死亡状态。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyAnimator))]
[RequireComponent(typeof(EnemyMotor))]
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyTerritory))]
public sealed class EnemyStateMachine : MonoBehaviour
{
    private const float RangeTolerance = 0.02f;

    [Header("Target")]
    [SerializeField]
    private Transform _targetOverride;

    [SerializeField, Min(0.1f)]
    private float _attackRange = 1.8f;

    private PlayerHealth _targetHealth;
    // Detection 只负责首次索敌；进入战斗后由 Leash 决定何时放弃。
    private bool _hasEngagedTarget;
    // 返回途中受击后，Hurt 结束仍应继续返回。
    private bool _isReturningHome;

    [Header("Debug")]
    [SerializeField]
    private bool _logStateChanges = true;

    public EnemyHealth Health { get; private set; }
    public EnemyAnimator EnemyAnimator { get; private set; }
    public EnemyMotor Motor { get; private set; }
    public EnemyCombat Combat { get; private set; }
    public EnemyTerritory Territory { get; private set; }
    public Transform Target { get; private set; }
    public float AttackRange => _attackRange;
    public bool HasEngagedTarget => _hasEngagedTarget;
    
    public string CurrentStateName =>
        CurrentState == null ? "None" : CurrentState.GetType().Name;
    /// <summary>
    /// 当前状态及其状态实例。
    /// </summary>
    public EnemyState CurrentState { get; private set; }
    public EnemyIdleState IdleState { get; private set; }
    public EnemyPatrolState PatrolState { get; private set; }
    public EnemyChaseState ChaseState { get; private set; }
    public EnemyAttackState AttackState { get; private set; }
    public EnemyReturnHomeState ReturnHomeState { get; private set; }
    public EnemyHurtState HurtState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    private void Awake()
    {
        Health = GetComponent<EnemyHealth>();
        EnemyAnimator = GetComponent<EnemyAnimator>();
        Motor = GetComponent<EnemyMotor>();
        Combat = GetComponent<EnemyCombat>();
        Territory = GetComponent<EnemyTerritory>();

        IdleState = new EnemyIdleState(this);
        PatrolState = new EnemyPatrolState(this);
        ChaseState = new EnemyChaseState(this);
        AttackState = new EnemyAttackState(this);
        ReturnHomeState = new EnemyReturnHomeState(this);
        HurtState = new EnemyHurtState(this);
        DeadState = new EnemyDeadState(this);
    }

    private void OnEnable()
    {
        _hasEngagedTarget = false;
        _isReturningHome = false;
        ResolveTarget();
        ChangeState(PatrolState);
    }

    private void Update()
    {
        CurrentState?.Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        CurrentState?.Exit();
        CurrentState = null;
        Combat?.CancelAttack();
    }

    public void HandleDamageTaken()
    {
        if (Health.CurrentHealth <= 0)
        {
            ChangeState(DeadState);
            return;
        }

        ChangeState(HurtState);
    }

    public void EvaluateTargetState()
    {
        if (Health.CurrentHealth <= 0)
        {
            ChangeState(DeadState);
            return;
        }

        if (_isReturningHome)
        {
            ChangeState(ReturnHomeState);
            return;
        }

        if (!HasValidTarget())
        {
            ChangeState(_hasEngagedTarget ? ReturnHomeState : PatrolState);
            return;
        }

        if (!Territory.IsInsideLeashArea(transform.position) ||
            (_hasEngagedTarget && !Territory.IsInsideLeashArea(Target.position)))
        {
            ChangeState(ReturnHomeState);
            return;
        }

        if (!_hasEngagedTarget)
        {
            if (!Territory.IsInsideDetectionArea(Target.position))
            {
                ChangeState(PatrolState);
                return;
            }

            _hasEngagedTarget = true;
        }

        if (HasTargetInAttackRange())
        {
            if (Combat.CanStartAttack)
                ChangeState(AttackState);
            else
                ChangeState(IdleState);

            return;
        }

        ChangeState(ChaseState);
    }

    public bool HasTargetInDetectionRange()
    {
        return HasValidTarget() && Territory.IsInsideDetectionArea(Target.position);
    }

    public bool ShouldReturnHome()
    {
        return !HasValidTarget() ||
            !Territory.IsInsideLeashArea(transform.position) ||
            !Territory.IsInsideLeashArea(Target.position);
    }

    public void BeginReturnHome()
    {
        _isReturningHome = true;
    }

    public void CompleteReturnHome()
    {
        _isReturningHome = false;
        _hasEngagedTarget = false;
        ChangeState(PatrolState);
    }

    public bool HasTargetInAttackRange()
    {
        return
            HasValidTarget() &&
            DistanceToTarget <= _attackRange + RangeTolerance;
    }

    public float DistanceToTarget
    {
        get
        {
            if (Target == null)
                return float.PositiveInfinity;

            Vector3 offset = Target.position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (
            newState == null ||
            CurrentState == newState ||
            CurrentState == DeadState
        )
            return;

        string previousStateName = CurrentStateName;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();

        if (_logStateChanges)
        {
            Debug.Log(
                $"Enemy State: {previousStateName} -> {CurrentStateName}",
                this
            );
        }
    }

    public bool HasValidTarget()
    {
        if (Target == null || !Target.gameObject.activeInHierarchy)
            return false;

        return _targetHealth == null || !_targetHealth.IsDead;
    }

    private void ResolveTarget()
    {
        if (_targetOverride != null)
        {
            Target = _targetOverride;
            _targetHealth = Target.GetComponentInParent<PlayerHealth>();
            return;
        }

        _targetHealth = FindFirstObjectByType<PlayerHealth>();
        Target = _targetHealth == null ? null : _targetHealth.transform;

        if (Target == null)
        {
            Debug.LogWarning(
                "EnemyStateMachine 未找到 PlayerHealth，敌人将继续巡逻。",
                this
            );
        }
    }

    private void OnValidate()
    {
        _attackRange = Mathf.Max(0.1f, _attackRange);
        EnemyTerritory territory = GetComponent<EnemyTerritory>();
        if (territory != null)
            _attackRange = Mathf.Min(_attackRange, territory.DetectionRadius);
    }
}
