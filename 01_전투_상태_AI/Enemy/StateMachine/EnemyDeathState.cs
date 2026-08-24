using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDeathState : EnemyBaseState
{
    public EnemyDeathState(EnemyClass enemyClass, StateMachine<EnemyBaseState> state) : base(enemyClass,state){}
    public override void Enter()
    {
        enemyClass.CheckCurrentState();
        enemyClass.Die();
    }
}

