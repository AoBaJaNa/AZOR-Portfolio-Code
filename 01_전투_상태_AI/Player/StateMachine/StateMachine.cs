public interface IState
{
    void Enter();
    void Update();
    void FixedUpdate();
    void Exit();
}
[System.Serializable]
public class StateMachine<T> where T : IState
{
    public T CurrentState { get; private set; }

    public void Initialize(T initialState)
    {
        CurrentState = initialState;
        CurrentState.Enter();
    }

    public void ChangeState(T newState)
    {
        if (CurrentState.Equals(newState)) return;
        CurrentState.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update() => CurrentState?.Update();
    public void FixedUpdate() => CurrentState?.FixedUpdate();
}
