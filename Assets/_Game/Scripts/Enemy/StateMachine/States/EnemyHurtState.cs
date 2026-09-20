public sealed class EnemyHurtState : EnemyState
{
    public EnemyHurtState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.EnemyAnimator.PlayHurt();
    }

    public override void Tick(float deltaTime)
    {
        if (StateMachine.EnemyAnimator.IsHurtFinished())
        {
            StateMachine.ChangeState(StateMachine.IdleState);
        }
    }
}
