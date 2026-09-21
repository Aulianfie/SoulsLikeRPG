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
        StateMachine.InputReader.ConsumeDodge();
        StateMachine.Motor.StopHorizontalMovement();
        StateMachine.Combat.StartLightAttack();
    }

    public override void Tick(float deltaTime)
    {
        // Jump / Dodge 输入在攻击期间丢弃（Day4 不做取消窗口）。
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeDodge();

        // Day4 简单连击：整段攻击动画期间都允许缓存一次下一段输入。
        // 体力不足时不缓存，保证"不足时不能进入下一击"。
        if (StateMachine.InputReader.ConsumeLightAttack())
        {
            if (
                !StateMachine.Combat.AttackQueued &&
                StateMachine.Stamina.CanConsume(
                    StateMachine.Stamina.AttackCost
                )
            )
            {
                StateMachine.Combat.QueueNextAttack();
            }
        }

        StateMachine.Combat.TickLightAttack();

        if (!StateMachine.Combat.IsLightAttackFinished())
            return;

        // 当前段结束：若已缓存、还有下一段、且体力够，进入下一段（继续留在攻击状态）。
        if (
            StateMachine.Combat.AttackQueued &&
            StateMachine.Combat.ComboIndex + 1 <
                StateMachine.Combat.MaxComboCount &&
            StateMachine.Stamina.Consume(
                StateMachine.Stamina.AttackCost
            ) &&
            StateMachine.Combat.TryStartNextComboHit()
        )
        {
            return;
        }

        // 没有下一段：回 Locomotion（Combat 已在 TryStartNextComboHit 中重置连击）。
        StateMachine.ChangeState(StateMachine.LocomotionState);
    }

    public override void Exit()
    {
        // FinishLightAttack 会关闭命中窗口并重置连击进度，
        // 因此被 Hurt / Dodge / Dead 打断时连击也会正确重置。
        StateMachine.Combat.FinishLightAttack();
    }
}
