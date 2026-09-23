using UnityEngine;

public sealed class EnemyReturnHomeState : EnemyState
{
    private const float RetryInterval = 0.5f;
    private float _nextRetryTime;

    public EnemyReturnHomeState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.BeginReturnHome();
        StateMachine.Motor.Stop();
        StateMachine.Combat.CancelAttack();
        StateMachine.EnemyAnimator.PlayChase();
        MoveHome();
    }

    public override void Tick(float deltaTime)
    {
        // 返回期间不重新索敌；完成返回后由 Patrol 决定是否再次追击。
        if (StateMachine.Territory.IsInsidePatrolArea(StateMachine.transform.position) ||
            StateMachine.Motor.HasReachedDestination())
        {
            StateMachine.CompleteReturnHome();
            return;
        }

        if (Time.time >= _nextRetryTime &&
            !StateMachine.Motor.IsPathPending &&
            !StateMachine.Motor.HasValidPath())
        {
            MoveHome();
        }
    }

    public override void Exit()
    {
        StateMachine.Motor.Stop();
    }

    private void MoveHome()
    {
        _nextRetryTime = Time.time + RetryInterval;
        StateMachine.Motor.MoveTo(StateMachine.Territory.HomePosition);
    }
}
