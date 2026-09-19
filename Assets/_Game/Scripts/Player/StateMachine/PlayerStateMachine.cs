using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerCombat))]
public sealed class PlayerStateMachine : MonoBehaviour
{
    [SerializeField] private bool _logStateChanges = true;

    public PlayerInputReader InputReader { get; private set; }
    public PlayerMotor Motor { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public PlayerState CurrentState { get; private set; }
    public string CurrentStateName =>
        CurrentState == null ? "None" : CurrentState.GetType().Name;

    public PlayerLocomotionState LocomotionState { get; private set; }
    public PlayerAirborneState AirborneState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }

    private void Awake()
    {
        InputReader = GetComponent<PlayerInputReader>();
        Motor = GetComponent<PlayerMotor>();
        Combat = GetComponent<PlayerCombat>();
        LocomotionState = new PlayerLocomotionState(this);
        AirborneState = new PlayerAirborneState(this);
        AttackState = new PlayerAttackState(this);
    }

    private void OnEnable()
    {
        ChangeState(LocomotionState);
    }

    /// <summary>
    /// 每帧调用当前状态的 Tick 方法，以便状态可以处理输入和更新逻辑。
    /// </summary>
    private void Update()
    {
        CurrentState?.Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        CurrentState?.Exit();
        CurrentState = null;
    }

    public void ChangeState(PlayerState newState)
    {
        if (newState == null || newState == CurrentState)
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
}
