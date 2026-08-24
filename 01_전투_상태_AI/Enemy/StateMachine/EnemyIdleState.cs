using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyIdleState : EnemyBaseState
{
    public EnemyIdleState(EnemyClass enemyClass, StateMachine<EnemyBaseState> state) : base(enemyClass,state){}

    public override void OnStunInput(float duration)
    {
        enemyClass.StunState.duration = duration;
        enemyClass.StateMachine.ChangeState(enemyClass.StunState);
    }
    public override void OnDamage(int damage, bool isCritical, SkillColor color = SkillColor.Red, bool addHit = false)
    {
        enemyClass.TakeDamageLogic(damage, isCritical, color, addHit);
    }
    public override void Update()
    {
        enemyClass.enemyController.DetectUpdate();
    }
    public override void OnAttack(Transform target)
    {
        enemyClass.AttackState.target = target;
        stateMachine.ChangeState(enemyClass.AttackState);
    }
}

