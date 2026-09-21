public sealed class EnemyChaseState : EnemyState
{
    public EnemyChaseState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.EnemyAnimator.PlayChase();
    }

    public override void Tick(float deltaTime)
    {
        if (!StateMachine.HasTargetInDetectionRange())
        {
            StateMachine.ChangeState(StateMachine.IdleState);
            return;
        }

        if (StateMachine.HasTargetInAttackRange())
        {
            StateMachine.EvaluateTargetState();
            return;
        }

        StateMachine.Motor.TickChase(
            StateMachine.Target.position,
            StateMachine.AttackRange,
            deltaTime
        );
    }
}
