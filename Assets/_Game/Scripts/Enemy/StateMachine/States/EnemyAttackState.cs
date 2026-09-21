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
            // Day4：前摇段（抬手）慢放，让抬手动作用动画表现出来，
            // 而不是在冷却期站着发呆。伤害窗口按 normalizedTime 对齐，
            // 因此伤害时机随慢放自动后移，视觉与判定保持一致。
            float playbackSpeed =
                normalizedTime <
                    StateMachine.Combat.WindupEndNormalizedTime
                    ? StateMachine.Combat.WindupSpeedMultiplier
                    : 1f;

            StateMachine.EnemyAnimator.SetSpeed(playbackSpeed);
            StateMachine.Combat.TickAttack(normalizedTime);
        }

        if (!StateMachine.EnemyAnimator.IsAttackFinished())
            return;

        StateMachine.Combat.CompleteAttack();
        StateMachine.EvaluateTargetState();
    }

    public override void Exit()
    {
        StateMachine.EnemyAnimator.SetSpeed(1f);
        StateMachine.Combat.CancelAttack();
    }
}
