using UnityEngine;

public sealed class PlayerDodgeState : PlayerState
{
    private const float TransitionDuration = 0.08f;
    private const float CompletionNormalizedTime = 0.95f;

    private Vector3 _dodgeDirection;
    private float _elapsedTime;

    public PlayerDodgeState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeLightAttack();
        StateMachine.InputReader.ConsumeDodge();

        _elapsedTime = 0f;
        _dodgeDirection = StateMachine.Motor.GetDodgeDirection(
            StateMachine.InputReader.MoveInput
        );

        StateMachine.Motor.BeginDodge(_dodgeDirection);
        StateMachine.PlayerAnimator.PlayDodge(TransitionDuration);
    }

    public override void Tick(float deltaTime)
    {
        // Day3 暂不实现取消窗口，翻滚期间的新输入直接丢弃。
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeLightAttack();
        StateMachine.InputReader.ConsumeDodge();

        _elapsedTime += deltaTime;

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
        StateMachine.Motor.EndDodge();
        StateMachine.PlayerAnimator.PlayLocomotion(
            TransitionDuration
        );
    }
}
