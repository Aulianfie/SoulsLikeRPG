public sealed class PlayerAttackState : PlayerState
{
    private float _elapsedTime;

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
        _elapsedTime = 0f;
    }

    public override void Tick(float deltaTime)
    {
        // Jump / Dodge 输入在攻击期间丢弃（Day4 不做取消窗口）。
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeDodge();

        // Day4：攻击开头的 rotateAssistTime 内允许向移动输入方向转向。
        if (_elapsedTime < StateMachine.Combat.CurrentRotateAssistTime)
        {
            StateMachine.Motor.RotateTowardsInput(
                StateMachine.InputReader.MoveInput,
                deltaTime
            );
        }

        _elapsedTime += deltaTime;

        // 连击窗口内允许缓存一次下一段输入；
        // 窗口外、没有下一段或体力不足时忽略。
        if (StateMachine.InputReader.ConsumeLightAttack())
        {
            if (
                !StateMachine.Combat.AttackQueued &&
                StateMachine.Combat.HasNextAttack &&
                StateMachine.Combat.IsInComboInputWindow &&
                StateMachine.Stamina.CanConsume(
                    StateMachine.Combat.NextAttackStaminaCost
                )
            )
            {
                StateMachine.Combat.QueueNextAttack();
            }
        }

        StateMachine.Combat.TickLightAttack();

        // 挥砍未完成或后摇未结束时，保持攻击状态。
        if (
            !StateMachine.Combat.IsLightAttackFinished() ||
            !StateMachine.Combat.IsRecoveryDone()
        )
        {
            return;
        }

        // 后摇结束：若已缓存、还有下一段、体力够，衔接下一段。
        if (
            StateMachine.Combat.AttackQueued &&
            StateMachine.Combat.HasNextAttack &&
            StateMachine.Stamina.Consume(
                StateMachine.Combat.NextAttackStaminaCost
            ) &&
            StateMachine.Combat.TryStartNextComboHit()
        )
        {
            _elapsedTime = 0f;
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
