using UnityEngine;

public sealed class PlayerWeaponSwitchState : PlayerState
{
    private bool _started;
    private bool _committed;
    private float _elapsed;

    public PlayerWeaponSwitchState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        StateMachine.InputReader.ClearPendingActions();
        StateMachine.Combat.ResetForRespawn();
        StateMachine.Motor.StopHorizontalMovement();
        _elapsed = 0f;
        _committed = false;
        _started = StateMachine.PlayerAnimator.PlayWeaponSwitch();
    }

    public override void Tick(float deltaTime)
    {
        ClearOtherActions();
        PlayerEquipment equipment = StateMachine.Equipment;
        if (!_started || equipment == null || !equipment.isActiveAndEnabled)
        {
            ReturnToMovement();
            return;
        }

        if (StateMachine.InputReader.HasBufferedDodge && StateMachine.Motor.IsGrounded &&
            StateMachine.Stamina.Consume(StateMachine.Stamina.DodgeCost))
        {
            StateMachine.InputReader.ConsumeBufferedDodge();
            StateMachine.ChangeState(StateMachine.DodgeState);
            return;
        }
        StateMachine.InputReader.ClearDodgeBuffer();
        StateMachine.Motor.TickLocomotion(Vector2.zero, false, deltaTime, 0f);
        if (StateMachine.Motor.ShouldEnterAirborne)
        {
            StateMachine.ChangeState(StateMachine.AirborneState);
            return;
        }

        _elapsed += deltaTime;
        if (StateMachine.PlayerAnimator.TryGetWeaponSwitchNormalizedTime(out float time))
        {
            if (time >= equipment.SwitchHidePoint && time < equipment.SwitchShowPoint)
                StateMachine.WeaponVisibility?.HideWeapon();
            if (!_committed && time >= equipment.SwitchEquipPoint)
            {
                if (!equipment.CommitWeaponSwitch())
                {
                    ReturnToMovement();
                    return;
                }
                _committed = true;
            }
            if (time >= equipment.SwitchShowPoint) StateMachine.WeaponVisibility?.ShowWeapon();
            if (time >= equipment.SwitchCompletionPoint) ReturnToMovement();
        }
        // 动画或控制器被外部替换也必须恢复武器显示并退出。
        if (_elapsed >= 3f) ReturnToMovement();
    }

    public override void Exit()
    {
        StateMachine.WeaponVisibility?.ShowWeapon();
        StateMachine.Equipment?.CancelWeaponSwitch();
        StateMachine.PlayerAnimator.StopWeaponSwitch();
        StateMachine.InputReader.ClearPendingActions();
    }

    private void ClearOtherActions()
    {
        StateMachine.InputReader.ClearLightAttackBuffer();
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeUseItem();
        StateMachine.InputReader.ConsumeInteract();
        StateMachine.InputReader.ConsumeSwitchWeapon();
    }

    private void ReturnToMovement()
    {
        if (StateMachine.CurrentState == this)
            StateMachine.ChangeState(StateMachine.Motor.IsGrounded
                ? StateMachine.LocomotionState : StateMachine.AirborneState);
    }
}
