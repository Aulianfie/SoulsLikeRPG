public sealed class PlayerAttackState : PlayerState
{
    private float _elapsedTime;

    /// <summary>
    /// 第一段攻击是否启动失败（AttackData 缺失 / Animator State 不存在）。
    /// 启动失败时不允许停留在 AttackState，下一帧立即退出到 Locomotion。
    /// </summary>
    private bool _startFailed;

    public PlayerAttackState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.Motor.StopHorizontalMovement();
        _elapsedTime = 0f;
        _startFailed = !StateMachine.Combat.StartLightAttack();
    }

    public override void Tick(float deltaTime)
    {
        // 攻击启动失败（配置错误）：立即安全退出，避免卡死在 AttackState。
        // Combo 与命中窗口已由 PlayerCombat 重置。
        if (_startFailed)
        {
            StateMachine.InputReader.ClearLightAttackBuffer();
            StateMachine.ChangeState(StateMachine.LocomotionState);
            return;
        }

        // Jump 暂不缓存；Dodge 在当前攻击的取消窗口中优先于连击。
        StateMachine.InputReader.ConsumeJump();

        if (
            StateMachine.InputReader.HasBufferedDodge &&
            StateMachine.Combat.IsInDodgeCancelWindow &&
            StateMachine.Motor.IsGrounded &&
            StateMachine.Stamina.Consume(
                StateMachine.Stamina.DodgeCost
            )
        )
        {
            StateMachine.InputReader.ConsumeBufferedDodge();
            StateMachine.ChangeState(StateMachine.DodgeState);
            return;
        }

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
        // 这里只做体力预检查（不扣体力），真正扣除在衔接点进行。
        if (StateMachine.InputReader.HasBufferedLightAttack &&
            !StateMachine.InputReader.HasBufferedDodge)
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
                StateMachine.InputReader.ConsumeBufferedLightAttack();
                StateMachine.Combat.QueueNextAttack();
            }
        }

        StateMachine.Combat.TickLightAttack();

        // 情况 A：已缓存下一段且到达 ComboTransitionPoint：
        // 再次确认体力后直接进入下一段，不等待完成点与后摇。
        if (
            StateMachine.Combat.AttackQueued &&
            StateMachine.Combat.HasNextAttack &&
            StateMachine.Combat.IsComboTransitionReached
        )
        {
            if (
                StateMachine.Stamina.Consume(
                    StateMachine.Combat.NextAttackStaminaCost
                )
            )
            {
                if (StateMachine.Combat.TryStartNextComboHit())
                {
                    StateMachine.InputReader.ClearLightAttackBuffer();
                    _elapsedTime = 0f;
                    return;
                }

                // 下一段动画播放失败（配置错误）：
                // Combo 与命中窗口已被重置，立即退出攻击流程，避免卡死。
                StateMachine.ChangeState(StateMachine.LocomotionState);
                return;
            }

            // 体力不足：放弃衔接，继续播放当前攻击，
            // 之后按 Completion -> Recovery -> Locomotion 正常收尾。
        }

        // 情况 B：没有缓存的下一段（或衔接失败/体力不足）：
        // 挥砍未完成或后摇未结束时保持攻击状态。
        if (
            !StateMachine.Combat.IsLightAttackFinished() ||
            !StateMachine.Combat.IsRecoveryDone()
        )
        {
            return;
        }

        // 完成点 + 后摇都已结束：回 Locomotion。
        StateMachine.ChangeState(StateMachine.LocomotionState);
    }

    public override void Exit()
    {
        // FinishLightAttack 会关闭命中窗口并重置连击进度，
        // 因此被 Hurt / Dodge / Dead 打断时连击也会正确重置。
        StateMachine.Combat.FinishLightAttack();
    }
}
