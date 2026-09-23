public sealed class EnemyIdleState : EnemyState
{
    public EnemyIdleState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.Motor.Stop();
        StateMachine.EnemyAnimator.PlayIdle();
    }

    public override void Tick(float deltaTime)
    {
        if (StateMachine.ShouldReturnHome())
        {
            StateMachine.ChangeState(StateMachine.ReturnHomeState);
            return;
        }

        if (StateMachine.HasTargetInAttackRange())
        {
            StateMachine.Motor.FaceTarget(
                StateMachine.Target.position,
                deltaTime
            );

            if (StateMachine.Combat.CanStartAttack)
                StateMachine.ChangeState(StateMachine.AttackState);

            return;
        }

        StateMachine.ChangeState(StateMachine.ChaseState);
    }
}
