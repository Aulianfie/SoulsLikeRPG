using UnityEngine;


/// <summary>
/// 敌人状态机，管理敌人的不同状态（Idle、Hurt、Dead）以及状态之间的转换。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyAnimator))]
[RequireComponent(typeof(EnemyMotor))]
[RequireComponent(typeof(EnemyCombat))]
public sealed class EnemyStateMachine : MonoBehaviour
{
    private const float RangeTolerance = 0.02f;

    [Header("Target")]
    [SerializeField]
    private Transform _targetOverride;

    [SerializeField, Min(0.1f)]
    private float _detectionRange = 8f;

    [SerializeField, Min(0.1f)]
    private float _attackRange = 1.8f;

    private PlayerHealth _targetHealth;

    [Header("Debug")]
    [SerializeField]
    private bool _logStateChanges = true;

    public EnemyHealth Health { get; private set; }
    public EnemyAnimator EnemyAnimator { get; private set; }
    public EnemyMotor Motor { get; private set; }
    public EnemyCombat Combat { get; private set; }
    public Transform Target { get; private set; }
    public float AttackRange => _attackRange;
    
    public string CurrentStateName =>
        CurrentState == null ? "None" : CurrentState.GetType().Name;
    /// <summary>
    /// 维护一个当前状态和不同的状态实例（Idle、Hurt、Dead），并提供方法来切换状态和处理敌人受到伤害的逻辑。
    /// </summary>
    public EnemyState CurrentState { get; private set; }
    public EnemyIdleState IdleState { get; private set; }
    public EnemyChaseState ChaseState { get; private set; }
    public EnemyAttackState AttackState { get; private set; }
    public EnemyHurtState HurtState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    private void Awake()
    {
        Health = GetComponent<EnemyHealth>();
        EnemyAnimator = GetComponent<EnemyAnimator>();
        Motor = GetComponent<EnemyMotor>();
        Combat = GetComponent<EnemyCombat>();

        IdleState = new EnemyIdleState(this);
        ChaseState = new EnemyChaseState(this);
        AttackState = new EnemyAttackState(this);
        HurtState = new EnemyHurtState(this);
        DeadState = new EnemyDeadState(this);
    }

    private void OnEnable()
    {
        ResolveTarget();
        ChangeState(IdleState);
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
        if (!HasValidTarget() || DistanceToTarget > _detectionRange)
        {
            ChangeState(IdleState);
            return;
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
        return HasValidTarget() && DistanceToTarget <= _detectionRange;
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

    private bool HasValidTarget()
    {
        if (Target == null)
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
                "EnemyStateMachine 未找到 PlayerHealth，敌人将保持待机。",
                this
            );
        }
    }

    private void OnValidate()
    {
        _attackRange = Mathf.Min(_attackRange, _detectionRange);
    }
}
