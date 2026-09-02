using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStunState : EnemyBaseState
{
    public float duration;
    public EnemyStunState(EnemyClass enemyClass, StateMachine<EnemyBaseState> state) : base(enemyClass,state){}
    public override void Enter()
    {
        enemyClass.enemyController.StunEnemy(duration);
        enemyClass.enemyDead.StunEffect(duration);
        enemyClass.enemyVFX.HitParticle(EnemyHitEffectType.Stun);
    }
    public override void OnStunInput(float duration)
    {
        enemyClass.StunState.duration = duration;
        enemyClass.enemyController.StunEnemy(duration);
        enemyClass.enemyDead.StunEffect(duration);
    }
    public override void OnDamage(int damage, bool isCritical, SkillColor color = SkillColor.Red, bool addHit = false)
    {
        enemyClass.TakeDamageLogic(damage, isCritical,color,addHit);
    }
    public override void OnActionEnd()
    {
        if (!enemyClass.enemyController.CanExitStunState)
            return;

        stateMachine.ChangeState(enemyClass.IdleState);
    }
}

