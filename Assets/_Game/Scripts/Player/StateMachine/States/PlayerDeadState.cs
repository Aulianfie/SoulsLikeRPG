public sealed class PlayerDeadState : PlayerState
{
    private const float TransitionDuration = 0.08f;

    public PlayerDeadState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        ConsumeBufferedInput();
        StateMachine.Health.DisableIFrame();
        StateMachine.Motor.StopHorizontalMovement();
        StateMachine.PlayerAnimator.PlayDeath(
            TransitionDuration
        );
    }

    public override void Tick(float deltaTime)
    {
        ConsumeBufferedInput();
        StateMachine.Motor.TickAirborne(deltaTime);
    }

    private void ConsumeBufferedInput()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ClearAllBuffers();
    }
}
