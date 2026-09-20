public sealed class EnemyIdleState : EnemyState
{
    public EnemyIdleState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.EnemyAnimator.PlayIdle();
    }
}
