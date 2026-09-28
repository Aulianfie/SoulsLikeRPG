using UnityEngine;

public sealed class PlayerInteractState : PlayerState
{
    private MonoBehaviour _target;
    private float _maxDistance;

    public PlayerInteractState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public void SetTarget(MonoBehaviour target, float maxDistance)
    {
        _target = target;
        _maxDistance = maxDistance;
    }

    public override void Enter()
    {
        ClearActionInput();
        StateMachine.Motor.StopHorizontalMovement();
        StateMachine.PlayerAnimator.PlayInteract(0.08f);
    }

    public override void Tick(float deltaTime)
    {
        // 动作期间丢弃攻击、闪避、跳跃和重复交互输入。
        ClearActionInput();

        // 继续处理重力，但不处理移动输入。
        StateMachine.Motor.TickAirborne(deltaTime);

        // 目标失效、距离过远或开始坠落时，取消交互。
        if (!IsTargetValid() ||
            StateMachine.Motor.ShouldEnterAirborne)
        {
            ReturnToMovement();
            return;
        }

        if (!StateMachine.PlayerAnimator.IsInteractFinished(0.95f))
            return;

        // 动画完成后，才执行原有的回血、保存和敌人重置。
        ((IInteractable)_target).Interact();
        ReturnToMovement();
    }

    public override void Exit()
    {
        _target = null;
        ClearActionInput();

        // 若被受击或死亡打断，新状态的 Enter 会接着播放对应动画。
        StateMachine.PlayerAnimator.PlayLocomotion(0.08f);
    }

    private bool IsTargetValid()
    {
        // 保存 MonoBehaviour 引用，才能正确判断 Unity 对象已被销毁。
        if (_target == null || !_target.isActiveAndEnabled)
            return false;

        IInteractable interactable = (IInteractable)_target;
        float sqrDistance =
            (_target.transform.position -
             StateMachine.transform.position).sqrMagnitude;

        return interactable.CanInteract &&
            sqrDistance <= _maxDistance * _maxDistance;
    }

    private void ClearActionInput()
    {
        StateMachine.InputReader.ConsumeJump();
        StateMachine.InputReader.ConsumeInteract();
        StateMachine.InputReader.ClearAllBuffers();
    }

    private void ReturnToMovement()
    {
        // 避免覆盖交互效果可能触发的其他状态。
        if (StateMachine.CurrentState != this)
            return;

        StateMachine.ChangeState(
            StateMachine.Motor.IsGrounded
                ? StateMachine.LocomotionState
                : StateMachine.AirborneState
        );
    }
}