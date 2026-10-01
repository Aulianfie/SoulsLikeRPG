using UnityEngine;

public sealed class PlayerAttackState : PlayerState
{
    private float _elapsedTime;
    private PlayerAttackType _attackType;
    private float _motionDistance;
    private float _lastMotionProgress;
    private Vector3 _motionDirection;
    private bool _motionDirectionLocked;
    private bool _jumpStrikeCommitted;
    private bool _jumpHolding;
    private bool _jumpWindupReleased;
    public void SetAttackType(PlayerAttackType type) => _attackType = type;

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
        StateMachine.InputReader.ClearActionRequests();
        if (_attackType != PlayerAttackType.Jump) StateMachine.Motor.StopHorizontalMovement();
        _elapsedTime = 0f;
        StateMachine.PlayerAnimator.SetJumpAttackPaused(false);
        _lastMotionProgress = 0f;
        _motionDirectionLocked = false;
        _jumpStrikeCommitted = _jumpHolding = _jumpWindupReleased = false;
        _startFailed = !StateMachine.Combat.TryStartAttack(_attackType);
        _motionDistance = !_startFailed && _attackType == PlayerAttackType.WeaponSkill
            ? StateMachine.Combat.CurrentAttack.MoveDistance : 0f;
    }

    public override void Tick(float deltaTime)
    {
        // 攻击启动失败（配置错误）：立即安全退出，避免卡死在 AttackState。
        // Combo 与命中窗口已由 PlayerCombat 重置。
        if (_startFailed)
        {
            StateMachine.InputReader.ClearLightAttackBuffer();
            StateMachine.ChangeState(StateMachine.Motor.IsGrounded ? StateMachine.LocomotionState : StateMachine.AirborneState);
            return;
        }

        // Jump 暂不缓存；Dodge 在当前攻击的取消窗口中优先于连击。
        StateMachine.InputReader.ConsumeJump();
        if (_attackType == PlayerAttackType.Jump || (_attackType == PlayerAttackType.Light && !StateMachine.Motor.IsGrounded))
            StateMachine.Motor.TickAirborne(deltaTime);
        if (_attackType != PlayerAttackType.Light)
            StateMachine.InputReader.ClearAllBuffers();

        if (
            _attackType == PlayerAttackType.Light &&
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

        AttackData data = StateMachine.Combat.CurrentAttack;
        bool hasTime = StateMachine.PlayerAnimator.TryGetAttackNormalizedTime(out float time);
        if (_attackType == PlayerAttackType.Jump && data.AlignJumpStrikeToLanding && !_jumpWindupReleased &&
            hasTime && time >= data.JumpWindupHoldPoint)
        {
            if (StateMachine.Motor.IsNearGroundForJumpStrike(data.JumpStrikeGroundDistance))
            {
                _jumpWindupReleased = true;
                _jumpHolding = false;
                StateMachine.PlayerAnimator.SetJumpAttackPaused(false);
            }
            else if (!_jumpHolding)
            {
                _jumpHolding = true;
                StateMachine.PlayerAnimator.HoldJumpAttackAt(data.JumpWindupHoldPoint);
            }
        }
        if (_attackType == PlayerAttackType.Jump && !_jumpHolding && hasTime && time >= data.HitWindowStart)
            _jumpStrikeCommitted = true;

        bool assist = _attackType == PlayerAttackType.Jump ? !_jumpStrikeCommitted :
            _attackType == PlayerAttackType.WeaponSkill ? !_motionDirectionLocked && (!hasTime || time < data.MotionStart) :
            _elapsedTime < StateMachine.Combat.CurrentRotateAssistTime;
        if (assist)
        {
            TryRotateTowardsTarget(deltaTime);
        }
        if (_attackType == PlayerAttackType.WeaponSkill) TickSkillMotion(data, hasTime ? time : 0f, deltaTime);

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

        bool landed = StateMachine.Motor.IsGrounded && StateMachine.Motor.VerticalVelocity <= 0f;
        StateMachine.Combat.TickAttack(deltaTime, _attackType != PlayerAttackType.Jump || landed, !_jumpHolding);

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
            !StateMachine.Combat.IsAttackFinished() ||
            !StateMachine.Combat.IsRecoveryDone() ||
            (_attackType == PlayerAttackType.Jump && !landed)
        )
        {
            return;
        }

        // 完成点 + 后摇都已结束：回 Locomotion。
        StateMachine.ChangeState(landed ? StateMachine.LocomotionState : StateMachine.AirborneState);
    }

    public override void Exit()
    {
        // FinishLightAttack 会关闭命中窗口并重置连击进度，
        // 因此被 Hurt / Dodge / Dead 打断时连击也会正确重置。
        StateMachine.Combat.FinishAttack();
        StateMachine.PlayerAnimator.SetJumpAttackPaused(false);
        if (_attackType == PlayerAttackType.WeaponSkill) StateMachine.Motor.StopHorizontalMovement();
        _attackType = PlayerAttackType.Light;
    }

    private void TickSkillMotion(AttackData data, float normalizedTime, float deltaTime)
    {
        float progress = data.MotionEnd > data.MotionStart
            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(data.MotionStart, data.MotionEnd, normalizedTime)) : 0f;
        // Charge attempted travel once; collision never creates stored displacement for a later burst.
        float step = Mathf.Max(0f, progress - _lastMotionProgress) * _motionDistance;
        _lastMotionProgress = Mathf.Max(_lastMotionProgress, progress);
        if (step > 0f && !_motionDirectionLocked)
        {
            _motionDirection = StateMachine.transform.forward;
            _motionDirectionLocked = true;
        }
        StateMachine.Motor.TickAttackMotion(_motionDirection, step, deltaTime);
    }

    /// <summary>
    /// 锁定攻击辅助：目标在 AttackAssistRange 内、且夹角不超过
    /// AttackAssistAngle 时平滑面向目标（修正朝向）；
    /// 不满足条件（无目标 / 太远 / 背后）时回退为朝移动输入方向转向。
    /// 只修正朝向，不产生位移，也不直接结算伤害。
    /// </summary>
    private void TryRotateTowardsTarget(float deltaTime)
    {
        Targetable target = StateMachine.Targeting != null
            ? StateMachine.Targeting.CurrentTarget
            : null;

        bool assisted =
            target != null &&
            target.IsAvailable &&
            target.LockPoint != null &&
            StateMachine.Motor.RotateTowardsTarget(
                target.LockPoint,
                StateMachine.Combat.AttackAssistRange,
                _attackType == PlayerAttackType.Jump ? Mathf.Max(120f, StateMachine.Combat.AttackAssistAngle) : StateMachine.Combat.AttackAssistAngle,
                deltaTime
            );

        if (assisted)
            return;

        StateMachine.Motor.RotateTowardsInput(
            StateMachine.InputReader.MoveInput,
            deltaTime
        );
    }
}
