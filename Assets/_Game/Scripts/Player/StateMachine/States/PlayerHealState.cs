public sealed class PlayerHealState : PlayerState
{
    private PlayerHealingFlask _flask;
    private bool _applied;
    private bool _startFailed;
    private float _elapsed;

    public PlayerHealState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public void SetFlask(PlayerHealingFlask flask) => _flask = flask;

    public override void Enter()
    {
        ClearOtherActions();
        StateMachine.InputReader.ClearDodgeBuffer();
        _applied = false;
        _elapsed = 0f;
        _startFailed = _flask == null || !_flask.CanUse || !StateMachine.PlayerAnimator.PlayHealing();
        if (!_startFailed) _flask.SetUseVisual(true);
    }

    public override void Tick(float deltaTime)
    {
        ClearOtherActions();
        if (_startFailed || _flask == null || !_flask.isActiveAndEnabled)
        {
            ReturnToMovement();
            return;
        }

        // 闪避和受击优先于本帧的回血点；回血已经生效后不回滚消耗。
        if (StateMachine.Motor.IsGrounded && StateMachine.InputReader.HasBufferedDodge &&
            StateMachine.Stamina.Consume(StateMachine.Stamina.DodgeCost))
        {
            StateMachine.InputReader.ConsumeBufferedDodge();
            StateMachine.ChangeState(StateMachine.DodgeState);
            return;
        }
        StateMachine.InputReader.ClearDodgeBuffer();
        StateMachine.Motor.TickLocomotion(StateMachine.InputReader.MoveInput, false,
            deltaTime, _flask.MovementMultiplier);
        if (StateMachine.Motor.ShouldEnterAirborne)
        {
            StateMachine.ChangeState(StateMachine.AirborneState);
            return;
        }

        _elapsed += deltaTime;
        if (StateMachine.PlayerAnimator.TryGetHealingNormalizedTime(out float time))
        {
            if (!_applied && time >= _flask.HealPoint)
            {
                _applied = true;
                if (_flask.TryConsume()) StateMachine.Health.Heal(_flask.HealAmount);
            }
            if (time >= 0.95f) ReturnToMovement();
        }
        // Animator 被意外换掉或停止时也能退出，避免卡在道具状态。
        if (_elapsed >= 8f) ReturnToMovement();
    }

    public override void Exit()
    {
        _flask?.SetUseVisual(false);
        StateMachine.PlayerAnimator.StopHealing();
        StateMachine.InputReader.ClearAllBuffers();
        ClearOtherActions();
        _flask = null;
    }

    private void ClearOtherActions()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeInteract();
        StateMachine.InputReader.ConsumeUseItem();
        StateMachine.InputReader.ClearLightAttackBuffer();
    }

    private void ReturnToMovement()
    {
        if (StateMachine.CurrentState == this)
            StateMachine.ChangeState(StateMachine.Motor.IsGrounded
                ? StateMachine.LocomotionState : StateMachine.AirborneState);
    }
}
