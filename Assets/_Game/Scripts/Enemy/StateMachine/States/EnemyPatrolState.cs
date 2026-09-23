using UnityEngine;

public sealed class EnemyPatrolState : EnemyState
{
    private const float RetryDelay = 1f;

    private float _waitRemaining;
    private bool _hasDestination;

    public EnemyPatrolState(EnemyStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        _waitRemaining = 0f;
        _hasDestination = false;
        ChooseDestination();
    }

    public override void Tick(float deltaTime)
    {
        if (StateMachine.HasTargetInDetectionRange())
        {
            StateMachine.EvaluateTargetState();
            return;
        }

        if (_waitRemaining > 0f)
        {
            _waitRemaining -= deltaTime;
            if (_waitRemaining <= 0f)
                ChooseDestination();
            return;
        }

        if (!_hasDestination)
        {
            ChooseDestination();
            return;
        }

        if (StateMachine.Motor.HasReachedDestination())
        {
            WaitAtDestination(Random.Range(1f, 3f));
        }
        else if (!StateMachine.Motor.IsPathPending && !StateMachine.Motor.HasValidPath())
        {
            WaitAtDestination(RetryDelay);
        }
    }

    public override void Exit()
    {
        StateMachine.Motor.Stop();
        _hasDestination = false;
    }

    private void ChooseDestination()
    {
        EnemyMotor motor = StateMachine.Motor;
        EnemyTerritory territory = StateMachine.Territory;

        if (motor.IsOnNavMesh &&
            territory.TryGetPatrolPoint(StateMachine.transform.position, motor.AreaMask, out Vector3 point) &&
            motor.MoveTo(point))
        {
            _hasDestination = true;
            StateMachine.EnemyAnimator.PlayChase();
            return;
        }

        WaitAtDestination(RetryDelay);
    }

    private void WaitAtDestination(float duration)
    {
        StateMachine.Motor.Stop();
        StateMachine.EnemyAnimator.PlayIdle();
        _hasDestination = false;
        _waitRemaining = duration;
    }
}
