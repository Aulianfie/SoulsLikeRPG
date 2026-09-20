public sealed class PlayerAttackState : PlayerState
{
    public PlayerAttackState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeLightAttack();
        StateMachine.Motor.StopHorizontalMovement();
        StateMachine.Combat.StartLightAttack();
    }

    public override void Tick(float deltaTime)
    {
        // Day2 暂不实现攻击派生与取消窗口，攻击期间的新输入直接丢弃。
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeLightAttack();
        StateMachine.Combat.TickLightAttack();

        if (StateMachine.Combat.IsLightAttackFinished())
        {
            StateMachine.ChangeState(StateMachine.LocomotionState);
        }
    }

    public override void Exit()
    {
        StateMachine.Combat.FinishLightAttack();
    }
}
