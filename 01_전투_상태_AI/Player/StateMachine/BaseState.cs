using UnityEngine;

public abstract class BaseState : IState
{
    protected PlayerController player;
    protected StateMachine<BaseState> stateMachine;
    
    public BaseState(PlayerController player, StateMachine<BaseState> stateMachine)
    {
        this.player = player;
        this.stateMachine = stateMachine;
    }
    public virtual void Enter() {
    }
    public virtual void Exit() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void OnMoveInput(Vector2 input) {}
    public virtual void OnDashInput() {}
    public virtual void OnHealInput() {}
    public virtual void OnPortalInput() {}
    public virtual void OnSkillInput(SkillData skill) {}
    public virtual void OnActionEnd() {}
    public virtual void OnDamage(int rawDamage, bool isCritical, GameObject enemy) {}

    public virtual void OnStunInput(float duration) {}
    public virtual void Revive() 
    {
        player.playerMovement.SetMoveInput(Vector2.zero);
        stateMachine.ChangeState(player.MoveState);
    }
    public virtual void LockPlayer(bool value)
    {
        stateMachine.ChangeState(value? player.LockState : player.MoveState);
    }

}

