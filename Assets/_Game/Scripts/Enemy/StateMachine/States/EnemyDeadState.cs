public sealed class EnemyDeadState : EnemyState
{
    public EnemyDeadState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        StateMachine.Motor.Stop();
        StateMachine.Combat.CancelAttack();
        StateMachine.EnemyAnimator.PlayDeath();
    }
}
