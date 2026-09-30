using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerAnimator))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerStamina))]
[RequireComponent(typeof(PlayerTargeting))]
public sealed class PlayerStateMachine : MonoBehaviour
{
    [SerializeField] private bool _logStateChanges = true;

    public PlayerInputReader InputReader { get; private set; }
    public PlayerMotor Motor { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public PlayerAnimator PlayerAnimator { get; private set; }
    public PlayerHealth Health { get; private set; }
    public PlayerStamina Stamina { get; private set; }
    public PlayerTargeting Targeting { get; private set; }
    public PlayerItemController Items { get; private set; }
    public IPlayerWeaponVisibility WeaponVisibility { get; private set; }
    public PlayerState CurrentState { get; private set; }
    public string CurrentStateName =>
        CurrentState == null ? "None" : CurrentState.GetType().Name;

    public PlayerLocomotionState LocomotionState { get; private set; }
    public PlayerAirborneState AirborneState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerDodgeState DodgeState { get; private set; }
    public PlayerHurtState HurtState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }

    public PlayerInteractState InteractState { get; private set; }
    public PlayerHealState HealState { get; private set; }

    private bool _hasStarted;

    private void Awake()
    {
        InputReader = GetComponent<PlayerInputReader>();
        Motor = GetComponent<PlayerMotor>();
        Combat = GetComponent<PlayerCombat>();
        PlayerAnimator = GetComponent<PlayerAnimator>();
        Health = GetComponent<PlayerHealth>();
        Stamina = GetComponent<PlayerStamina>();
        Targeting = GetComponent<PlayerTargeting>();
        Items = GetComponent<PlayerItemController>();
        WeaponVisibility = GetComponent<IPlayerWeaponVisibility>();
        LocomotionState = new PlayerLocomotionState(this);
        AirborneState = new PlayerAirborneState(this);
        AttackState = new PlayerAttackState(this);
        DodgeState = new PlayerDodgeState(this);
        HurtState = new PlayerHurtState(this);
        DeadState = new PlayerDeadState(this);
        InteractState = new PlayerInteractState(this);
        HealState = new PlayerHealState(this);
    }

    private void OnEnable()
    {
        // 首次启用时，其他组件的 Awake 尚未保证全部执行完毕。
        // 等到 Start 再读取 PlayerHealth，避免把默认生命值 0 误判为死亡。
        if (_hasStarted)
            EnterInitialState();
    }

    private void Start()
    {
        _hasStarted = true;
        EnterInitialState();
    }

    /// <summary>
    /// 每帧调用当前状态的 Tick 方法，以便状态可以处理输入和更新逻辑。
    /// </summary>
    private void Update()
    {
        if (CurrentState != LocomotionState)
            InputReader.ConsumeUseItem();
        CurrentState?.Tick(Time.deltaTime);
    }

    public bool TryBeginHealing(IPlayerHealingItem item)
    {
        MonoBehaviour behaviour = item as MonoBehaviour;
        if (CurrentState != LocomotionState || !Motor.IsGrounded || Health.IsDead ||
            behaviour == null || behaviour.gameObject != gameObject || !item.CanUse)
            return false;
        HealState.SetItem(item);
        ChangeState(HealState);
        return CurrentState == HealState;
    }

    private void OnDisable()
    {
        CurrentState?.Exit();
        CurrentState = null;
    }

    public void ChangeState(PlayerState newState)
    {
        if (
            newState == null ||
            newState == CurrentState ||
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
                $"Player State: {previousStateName} -> {CurrentStateName}",
                this
            );
        }
    }

    public void HandleDamageTaken()
    {
        if (Health == null)
            return;

        ChangeState(Health.IsDead ? DeadState : HurtState);
    }

    public bool TryBeginInteraction(
        IInteractable target,
        float maxDistance
    )
    {
        // 只允许存活、站在地面且处于移动状态的玩家开始交互。
        if (Health.IsDead ||
            CurrentState != LocomotionState ||
            !Motor.IsGrounded)
            return false;

        MonoBehaviour behaviour = target as MonoBehaviour;

        if (behaviour == null ||
            !behaviour.isActiveAndEnabled ||
            !target.CanInteract)
            return false;

        float sqrDistance =
            (behaviour.transform.position -
            transform.position).sqrMagnitude;

        if (sqrDistance > maxDistance * maxDistance)
            return false;

        if (!target.RequiresInteractionAnimation)
        {
            target.Interact();
            return true;
        }

        // 先保存目标，再进入交互状态。
        InteractState.SetTarget(behaviour, maxDistance);
        ChangeState(InteractState);
        return CurrentState == InteractState;
    }

    public void Respawn()
    {
        if (Health == null || Health.IsDead)
            return;

        string previousStateName = CurrentStateName;
        CurrentState?.Exit();
        CurrentState = null;
        InputReader.ClearPendingActions();
        Combat.ResetForRespawn();
        Items?.RefillRestItems();
        Targeting.ClearTarget();
        Motor.StopHorizontalMovement();
        PlayerAnimator.PlayLocomotion(0f);
        CurrentState = LocomotionState;
        CurrentState.Enter();

        if (_logStateChanges)
            Debug.Log($"Player State: {previousStateName} -> {CurrentStateName}", this);
    }

    private void EnterInitialState()
    {
        ChangeState(Health != null && Health.IsDead
            ? DeadState
            : LocomotionState);
    }
}
