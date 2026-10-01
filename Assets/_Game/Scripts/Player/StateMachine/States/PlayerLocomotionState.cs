public sealed class PlayerLocomotionState : PlayerState
{
    public PlayerLocomotionState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Tick(float deltaTime)
    {
        int switchDirection = StateMachine.InputReader.ConsumeSwitchWeapon();
        bool useItemRequested = StateMachine.InputReader.ConsumeUseItem();
        bool skillRequested = StateMachine.InputReader.ConsumeWeaponSkill();
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
            StateMachine.TryBeginAttack(PlayerAttackType.Light)
        )
        {
            StateMachine.InputReader.ConsumeBufferedLightAttack();
            return;
        }

        if (skillRequested && StateMachine.TryBeginAttack(PlayerAttackType.WeaponSkill)) return;

        if (useItemRequested && StateMachine.Items != null && StateMachine.Items.TryUseItem())
            return;

        if (switchDirection != 0)
        {
            StateMachine.Equipment?.CycleWeapon(switchDirection);
            if (StateMachine.CurrentState != this)
            {
                StateMachine.Motor.TickLocomotion(StateMachine.InputReader.MoveInput,
                    StateMachine.InputReader.SprintInput, deltaTime);
                return;
            }
        }

        // Day5 Task3（按反馈调整）：锁定与否共用同一套相机相对移动，
        // 角色朝移动方向转身、可奔跑；
        // 锁定的差异只体现在相机（看向目标）与攻击朝向辅助上。
        StateMachine.Motor.TickLocomotion(
            StateMachine.InputReader.MoveInput,
            StateMachine.InputReader.SprintInput,
            deltaTime
        );

        if (StateMachine.Motor.ShouldEnterAirborne)
        {
            StateMachine.ChangeState(StateMachine.AirborneState);
        }
    }

}
