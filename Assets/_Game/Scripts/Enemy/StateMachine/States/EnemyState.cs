public abstract class EnemyState
{
    protected readonly EnemyStateMachine StateMachine;

    protected EnemyState(EnemyStateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }

    public virtual void Enter() { }

    public virtual void Tick(float deltaTime) { }

    public virtual void Exit() { }
}
