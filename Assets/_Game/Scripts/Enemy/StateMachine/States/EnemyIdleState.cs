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
        if (!StateMachine.HasTargetInDetectionRange())
        {
            StateMachine.ChangeState(StateMachine.PatrolState);
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

        if (StateMachine.HasTargetInDetectionRange())
            StateMachine.ChangeState(StateMachine.ChaseState);
    }
}
