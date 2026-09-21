public sealed class PlayerHurtState : PlayerState
{
    private const float ExitTransitionDuration = 0.08f;
    private const float CompletionNormalizedTime = 0.95f;

    public PlayerHurtState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        ConsumeBufferedInput();
        StateMachine.Health.DisableIFrame();
        StateMachine.Motor.StopHorizontalMovement();
        StateMachine.PlayerAnimator.PlayHurt();
    }

    public override void Tick(float deltaTime)
    {
        ConsumeBufferedInput();
        StateMachine.Motor.TickAirborne(deltaTime);

        if (!StateMachine.PlayerAnimator.IsHurtFinished(
                CompletionNormalizedTime
            ))
        {
            return;
        }

        StateMachine.ChangeState(StateMachine.Motor.IsGrounded
            ? StateMachine.LocomotionState
            : StateMachine.AirborneState);
    }

    public override void Exit()
    {
        StateMachine.PlayerAnimator.PlayLocomotion(
            ExitTransitionDuration
        );
    }

    private void ConsumeBufferedInput()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeLightAttack();
        StateMachine.InputReader.ConsumeDodge();
    }
}
