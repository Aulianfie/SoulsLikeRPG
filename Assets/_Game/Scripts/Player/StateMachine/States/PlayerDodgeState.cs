using UnityEngine;

public sealed class PlayerDodgeState : PlayerState
{
    private const float TransitionDuration = 0.08f;
    private const float CompletionNormalizedTime = 0.95f;
    private const float IFrameStartNormalizedTime = 0.2f;
    private const float IFrameEndNormalizedTime = 0.65f;

    private Vector3 _dodgeDirection;
    private float _elapsedTime;

    public PlayerDodgeState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeBufferedDodge();
        StateMachine.InputReader.ClearLightAttackBuffer();
        StateMachine.Health.DisableIFrame();

        _elapsedTime = 0f;
        _dodgeDirection = StateMachine.Motor.GetDodgeDirection(
            StateMachine.InputReader.MoveInput
        );

        StateMachine.Motor.BeginDodge(_dodgeDirection);
        StateMachine.PlayerAnimator.PlayDodge(TransitionDuration);
    }

    public override void Tick(float deltaTime)
    {
        // 翻滚期间不允许攻击或再次翻滚。
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ClearAllBuffers();

        _elapsedTime += deltaTime;

        if (
            StateMachine.PlayerAnimator.TryGetDodgeNormalizedTime(
                out float normalizedTime
            )
        )
        {
            bool isInsideIFrame =
                normalizedTime >= IFrameStartNormalizedTime &&
                normalizedTime < IFrameEndNormalizedTime;

            if (isInsideIFrame)
                StateMachine.Health.EnableIFrame();
            else
                StateMachine.Health.DisableIFrame();
        }

        StateMachine.Motor.TickDodge(
            _elapsedTime < StateMachine.Motor.DodgeDuration,
            deltaTime
        );

        if (
            _elapsedTime >= StateMachine.Motor.DodgeDuration &&
            StateMachine.PlayerAnimator.IsDodgeFinished(
                CompletionNormalizedTime
            )
        )
        {
            StateMachine.ChangeState(StateMachine.LocomotionState);
        }
    }

    public override void Exit()
    {
        StateMachine.InputReader.ClearAllBuffers();
        StateMachine.Health.DisableIFrame();
        StateMachine.Motor.EndDodge();
        StateMachine.PlayerAnimator.PlayLocomotion(
            TransitionDuration
        );
    }
}
