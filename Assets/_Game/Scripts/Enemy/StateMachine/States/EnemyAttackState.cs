public sealed class EnemyAttackState : EnemyState
{
    public EnemyAttackState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.Motor.Stop();
        StateMachine.Combat.BeginAttack();
        StateMachine.EnemyAnimator.PlayAttack();
    }

    public override void Tick(float deltaTime)
    {
        if (StateMachine.Target != null)
        {
            StateMachine.Motor.FaceTarget(
                StateMachine.Target.position,
                deltaTime
            );
        }

        if (
            StateMachine.EnemyAnimator.TryGetAttackNormalizedTime(
                out float normalizedTime
            )
        )
        {
            StateMachine.Combat.TickAttack(normalizedTime);
        }

        if (!StateMachine.EnemyAnimator.IsAttackFinished())
            return;

        StateMachine.Combat.CompleteAttack();
        StateMachine.EvaluateTargetState();
    }

    public override void Exit()
    {
        StateMachine.Combat.CancelAttack();
    }
}
