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
    }

    public override void Tick(float deltaTime)
    {
        // Day2 暂不实现 Jump Buffer，空中输入直接丢弃。
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeLightAttack();

        StateMachine.Motor.TickAirborne(deltaTime);

        if (
            StateMachine.Motor.IsGrounded &&
            StateMachine.Motor.VerticalVelocity <= 0f
        )
        {
            StateMachine.ChangeState(StateMachine.LocomotionState);
        }
    }
}
