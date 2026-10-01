public sealed class PlayerAirborneState : PlayerState
{
    public PlayerAirborneState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeLightAttack();
        StateMachine.InputReader.ConsumeDodge();
    }

    public override void Tick(float deltaTime)
    {
        // No jump/dodge buffering in the air; LightAttack may start one JumpAttack.
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeDodge();

        StateMachine.Motor.TickAirborne(deltaTime);

        if (
            StateMachine.Motor.IsGrounded &&
            StateMachine.Motor.VerticalVelocity <= 0f
        )
        {
            StateMachine.ChangeState(StateMachine.LocomotionState);
            return;
        }
        if (StateMachine.InputReader.ConsumeLightAttack())
            StateMachine.TryBeginAttack(PlayerAttackType.Jump);
    }
}
