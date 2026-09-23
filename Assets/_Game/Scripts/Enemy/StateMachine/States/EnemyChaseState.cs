public sealed class EnemyChaseState : EnemyState
{
    private const float RepathInterval = 0.2f;
    private float _nextRepathTime;

    public EnemyChaseState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        _nextRepathTime = 0f;
        StateMachine.EnemyAnimator.PlayChase();
    }

    public override void Tick(float deltaTime)
    {
        if (!StateMachine.HasValidTarget() || StateMachine.ShouldReturnHome())
        {
            StateMachine.ChangeState(StateMachine.ReturnHomeState);
            return;
        }

        if (StateMachine.HasTargetInAttackRange())
        {
            StateMachine.EvaluateTargetState();
            return;
        }

        if (UnityEngine.Time.time >= _nextRepathTime)
        {
            _nextRepathTime = UnityEngine.Time.time + RepathInterval;
            StateMachine.Motor.MoveTo(StateMachine.Target.position);
        }
    }

    public override void Exit()
    {
        StateMachine.Motor.Stop();
    }
}
