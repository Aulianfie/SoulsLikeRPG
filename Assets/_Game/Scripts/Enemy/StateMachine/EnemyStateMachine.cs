using UnityEngine;


/// <summary>
/// 敌人状态机，管理敌人的不同状态（Idle、Hurt、Dead）以及状态之间的转换。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyAnimator))]
public sealed class EnemyStateMachine : MonoBehaviour
{
    [SerializeField]
    private bool _logStateChanges = true;

    public EnemyHealth Health { get; private set; }
    public EnemyAnimator EnemyAnimator { get; private set; }
    
    public string CurrentStateName =>
        CurrentState == null ? "None" : CurrentState.GetType().Name;
    /// <summary>
    /// 维护一个当前状态和不同的状态实例（Idle、Hurt、Dead），并提供方法来切换状态和处理敌人受到伤害的逻辑。
    /// </summary>
    public EnemyState CurrentState { get; private set; }
    public EnemyIdleState IdleState { get; private set; }
    public EnemyHurtState HurtState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    private void Awake()
    {
        Health = GetComponent<EnemyHealth>();
        EnemyAnimator = GetComponent<EnemyAnimator>();

        IdleState = new EnemyIdleState(this);
        HurtState = new EnemyHurtState(this);
        DeadState = new EnemyDeadState(this);
    }

    private void OnEnable()
    {
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

    public void ChangeState(EnemyState newState)
    {
        if (newState == null || CurrentState == DeadState)
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
}
