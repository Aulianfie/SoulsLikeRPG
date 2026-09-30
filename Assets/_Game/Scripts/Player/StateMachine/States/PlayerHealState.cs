public sealed class PlayerHealState : PlayerState
{
    private const float AnimationFailsafeSeconds = 8f;

    private IPlayerHealingItem _item;
    private bool _applied;
    private bool _startFailed;
    private float _elapsed;

    public PlayerHealState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public void SetItem(IPlayerHealingItem item) => _item = item;

    public override void Enter()
    {
        ClearOtherActions();
        StateMachine.InputReader.ClearDodgeBuffer();
        _applied = false;
        _elapsed = 0f;
        _startFailed = !IsItemAvailable() || !_item.CanUse || !StateMachine.PlayerAnimator.PlayHealing();
        if (!_startFailed)
        {
            StateMachine.WeaponVisibility?.HideWeapon();
            _item.SetUseVisual(true);
        }
    }

    public override void Tick(float deltaTime)
    {
        ClearOtherActions();
        if (_startFailed || !IsItemAvailable())
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
            deltaTime, _item.MovementMultiplier);
        if (StateMachine.Motor.ShouldEnterAirborne)
        {
            StateMachine.ChangeState(StateMachine.AirborneState);
            return;
        }

        _elapsed += deltaTime;
        if (StateMachine.PlayerAnimator.TryGetHealingNormalizedTime(out float time))
        {
            if (!_applied && time >= _item.HealPoint)
            {
                _applied = true;
                if (_item.TryConsume()) StateMachine.Health.Heal(_item.HealAmount);
            }
            if (time >= _item.CompletionPoint) ReturnToMovement();
        }
        // Animator 被意外换掉或停止时也能退出，避免卡在道具状态。
        if (_elapsed >= AnimationFailsafeSeconds) ReturnToMovement();
    }

    public override void Exit()
    {
        if (_item != null && !(_item is UnityEngine.Object obj && obj == null))
            _item.SetUseVisual(false);
        StateMachine.WeaponVisibility?.ShowWeapon();
        StateMachine.PlayerAnimator.StopHealing();
        StateMachine.InputReader.ClearAllBuffers();
        ClearOtherActions();
        _item = null;
    }

    private bool IsItemAvailable()
    {
        return _item != null && !(_item is UnityEngine.Object obj && obj == null) && _item.IsAvailable;
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
