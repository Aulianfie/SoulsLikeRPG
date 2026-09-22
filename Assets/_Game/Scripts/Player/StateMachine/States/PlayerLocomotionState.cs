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

        // 同时缓存闪避和攻击时，优先执行闪避。
        if (
            StateMachine.Motor.IsGrounded &&
            StateMachine.InputReader.HasBufferedDodge &&
            StateMachine.Stamina.Consume(
                StateMachine.Stamina.DodgeCost
            )
        )
        {
            StateMachine.InputReader.ConsumeBufferedDodge();
            StateMachine.ChangeState(StateMachine.DodgeState);
            return;
        }

        // 第一段攻击的体力消耗来自 AttackData，扣除成功才消耗缓存。
        if (
            StateMachine.Motor.IsGrounded &&
            StateMachine.InputReader.HasBufferedLightAttack &&
            StateMachine.Stamina.Consume(
                GetLightAttackStaminaCost()
            )
        )
        {
            StateMachine.InputReader.ConsumeBufferedLightAttack();
            StateMachine.ChangeState(StateMachine.AttackState);
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

    /// <summary>
    /// 第一段攻击的体力消耗：只有连招未配置（AttackData 为 null）时
    /// 才回退到 PlayerStamina 上的默认消耗；
    /// 配置里的 0 是合法值，必须原样使用。
    /// </summary>
    private float GetLightAttackStaminaCost()
    {
        if (StateMachine.Combat.HasFirstAttack)
            return StateMachine.Combat.FirstAttackStaminaCost;

        return StateMachine.Stamina.AttackCost;
    }
}
