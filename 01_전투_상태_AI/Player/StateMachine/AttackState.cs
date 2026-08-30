using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : BaseState
{
    public SkillData SkillData { get; set; }
    public AttackState(PlayerController player, StateMachine<BaseState> stateMachine) : base(player,stateMachine) {}
    public override void OnSkillInput(SkillData skill)
    {
        //player.playerSkill.TryUseSkill(skill); //스킬 사용중에 중첩 안 되게 막아놓음
    }
    public override void Enter()
    {
        if (SkillData != null)
        {
            if (!player.playerSkill.TryUseSkill(SkillData))
                stateMachine.ChangeState(player.MoveState);
        }
        else
        {
            stateMachine.ChangeState(player.MoveState);
        }
    }
    public override void Exit()
    {
        SkillData = null;
    }
    // ActionState - 이동 입력은 MoveState에 저장해두기
    public override void OnMoveInput(Vector2 input)
    {
        player.playerMovement.SetMoveInput(input); // 실제 입력도 유지
    }
    public override void OnActionEnd()
    {
        player.playerPassiveController.HandleEndAttack();
        stateMachine.ChangeState(player.MoveState);
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
}

