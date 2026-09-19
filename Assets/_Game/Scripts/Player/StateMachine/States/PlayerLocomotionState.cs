public sealed class PlayerLocomotionState : PlayerState
{
    public PlayerLocomotionState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Tick(float deltaTime)
    {
        bool jumpRequested = StateMachine.InputReader.ConsumeJump();

        if (
            StateMachine.Motor.IsGrounded &&
            jumpRequested
        )
        {
            StateMachine.Motor.Jump();
            StateMachine.ChangeState(StateMachine.AirborneState);
            return;
        }

        StateMachine.Motor.TickLocomotion(
            StateMachine.InputReader.MoveInput,
            StateMachine.InputReader.SprintInput,
            deltaTime
        );

        if (!StateMachine.Motor.IsGrounded)
        {
            StateMachine.ChangeState(StateMachine.AirborneState);
        }
    }
}
