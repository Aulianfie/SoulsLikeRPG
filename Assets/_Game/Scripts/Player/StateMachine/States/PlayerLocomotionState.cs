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

        // Day4：攻击体力消耗来自连招第一段的 AttackData；不足时忽略攻击输入。
        if (
            StateMachine.Motor.IsGrounded &&
            lightAttackRequested &&
            StateMachine.Stamina.Consume(
                GetLightAttackStaminaCost()
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
