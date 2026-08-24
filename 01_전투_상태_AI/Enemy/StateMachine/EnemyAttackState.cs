using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackState : EnemyBaseState
{
    public Transform target;
    public EnemyAttackState(EnemyClass enemyClass, StateMachine<EnemyBaseState> state) : base(enemyClass,state){}
    public override void Enter()
    {
        enemyClass.CheckCurrentState();
        if(target != null)
        enemyClass.enemyAttack.StartAttack(target);
    }
    public override void OnDamage(int damage, bool isCritical, SkillColor color = SkillColor.Red, bool addHit = false)
    {
        enemyClass.TakeDamageLogic(damage, isCritical, color, addHit);
    }
    public override void OnStunInput(float duration)
    {
        enemyClass.StunState.duration = duration;
        enemyClass.StateMachine.ChangeState(enemyClass.StunState);
    }
    public override void OnActionEnd()
    {
        stateMachine.ChangeState(enemyClass.IdleState);
    }
    public override void Exit()
    {
        enemyClass.enemyAttack.AttackReset();
    }
}

