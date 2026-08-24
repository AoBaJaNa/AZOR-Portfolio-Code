using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaganFleeState : EnemyBaseState
{
    public PaganFleeState(EnemyClass enemyClass, StateMachine<EnemyBaseState> state) : base(enemyClass, state) { }

    public override void Enter()
    {
        // 1. 기존 모든 행동 즉시 중단
        enemyClass.enemyController.StopAllMovementCoroutines();
        enemyClass.enemyAttack.AttackReset();
        enemyClass.enemyController.FleeFromTarget();
        enemyClass.CheckCurrentState();
    }
    public override void OnStunInput(float duration)
    {
        enemyClass.StunState.duration = duration;
        enemyClass.StateMachine.ChangeState(enemyClass.StunState);
    }
    public override void OnDamage(int damage, bool isCritical, SkillColor color = SkillColor.Red, bool addHit = false)
    {
        enemyClass.TakeDamageLogic(damage, isCritical, color, addHit);
    }
    public override void OnActionEnd()
    {
        enemyClass.StateMachine.ChangeState(enemyClass.IdleState);
    }
    public override void Exit()
    {
        // 도망 중이던 이동 멈춤
        enemyClass.enemyController.IsFleeing = false; // 플래그 초기화
    }
}
