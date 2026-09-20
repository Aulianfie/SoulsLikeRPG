public sealed class EnemyDeadState : EnemyState
{
    public EnemyDeadState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.EnemyAnimator.PlayDeath();
    }
}
