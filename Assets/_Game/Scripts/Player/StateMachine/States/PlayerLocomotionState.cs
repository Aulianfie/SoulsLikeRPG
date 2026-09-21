public sealed class PlayerLocomotionState : PlayerState
{
    public PlayerLocomotionState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Tick(float deltaTime)
    {
        bool jumpRequested = StateMachine.InputReader.ConsumeJump();
        bool lightAttackRequested =
            StateMachine.InputReader.ConsumeLightAttack();
        bool dodgeRequested =
            StateMachine.InputReader.ConsumeDodge();

        if (
            StateMachine.Motor.IsGrounded &&
            jumpRequested
        )
        {
            StateMachine.Motor.Jump();
            StateMachine.ChangeState(StateMachine.AirborneState);
            return;
        }

        // Day4：检查并扣除 Stamina；只有扣除成功才进入攻击状态。
        if (
            StateMachine.Motor.IsGrounded &&
            lightAttackRequested &&
            StateMachine.Stamina.Consume(
                StateMachine.Stamina.AttackCost
            )
        )
        {
            StateMachine.ChangeState(StateMachine.AttackState);
            return;
        }

        // Day4：检查并扣除 Stamina；只有扣除成功才进入 Dodge 状态。
        if (
            StateMachine.Motor.IsGrounded &&
            dodgeRequested &&
            StateMachine.Stamina.Consume(
                StateMachine.Stamina.DodgeCost
            )
        )
        {
            StateMachine.ChangeState(StateMachine.DodgeState);
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
