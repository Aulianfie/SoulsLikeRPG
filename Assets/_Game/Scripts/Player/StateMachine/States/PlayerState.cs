public abstract class PlayerState
{
    protected readonly PlayerStateMachine StateMachine;

    protected PlayerState(PlayerStateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }

    public virtual void Enter() { }

    public virtual void Tick(float deltaTime) { }

    public virtual void Exit() { }
}
