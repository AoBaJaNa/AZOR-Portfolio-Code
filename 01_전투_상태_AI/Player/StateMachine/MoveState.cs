using UnityEngine;

public class MoveState : BaseState
{
    public Vector2 Input { get; set; }
    public MoveState(PlayerController player, StateMachine<BaseState> stateMachine) : base(player,stateMachine) {}
    public override void OnMoveInput(Vector2 input)
    {
        player.playerMovement.SetMoveInput(input);
    }
    public override void OnSkillInput(SkillData skill)
    {
        if (player.dash.IsDashing)
            return;

        player.AttackState.SkillData = skill;
        stateMachine.ChangeState(player.AttackState);
    }
    public override void OnDashInput()
    {
        stateMachine.ChangeState(player.ActionState);
        player.UseDash();
    }
    public override void OnPortalInput()
    {
        stateMachine.ChangeState(player.ActionState);
        player.OpenPortal();
    }
    public override void OnDamage(int rawDamage, bool isCritical, GameObject enemy)
    {
        PlayerInfo.Instance.ApplyDamage(rawDamage, isCritical, enemy);
    }
    public override void OnStunInput(float duration)
    {
        player.StunState.duration = duration;
        stateMachine.ChangeState(player.StunState);
    }
    public override void OnHealInput()
    {
        player.UseHeal();
    }
    public override void Update()
    {
        if (player.LockPlayer)
        {
            player.animator.SetBool("Move", false);
            return;
        }

        if (player.playerMovement.Direction.magnitude > 0)
        {
            if (player.playerMovement.MoveInput.magnitude <= 0)
                player.playerMovement.Initialize();
            player.animator.SetBool("Move", true);
        }
        else
        {
            player.animator.SetBool("Move", false);
        }
    }
    public override void FixedUpdate()
    {
        if (!player.dash.IsDashing) //대시 중이 아닐때만 방향 전환
            player.playerMovement.MoveUpdate();
    }
}

