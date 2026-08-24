using UnityEngine;
using System.Collections;

public abstract class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackCooldown = 2.0f; // 공격 쿨타임
    public int attackDamage = 10; // 공격력

    protected bool isAttacking = false; // 공격 중인지 확인
    protected bool isCooldown = false; // 쿨타임 중인지 확인
    public bool IsAttacking => isAttacking;

    [Header("Critical Settings")]
    [SerializeField] protected float criticalRate = 20f; // 치명타 확률

    protected EnemyClass enemyClass;
    protected PlayerController playerController;
    protected Transform playerTarget;
    protected Animator ani;
    protected Coroutine coolCoroutine;
    internal Transform targetT;
    public void Awake()
    {
        Initialize();
    }
    protected void Initialize()
    {
        enemyClass = GetComponent<EnemyClass>();
        ani = GetComponent<Animator>();
        playerController = FindFirstObjectByType<PlayerController>();
        playerTarget = GameObject.FindWithTag("Player").transform;
    }
    public abstract void StartAttack(Transform target);

    protected void ResetBaseAttackState()
    {
        isAttacking = false;
        isCooldown = false;
        coolCoroutine = null;

        if (ani != null)
            ani.SetBool("Attacking", false);
    }

    protected void RestoreAgentState(bool restoreRotation = true)
    {
        if (enemyClass == null || enemyClass.enemyController == null || enemyClass.enemyController.agent == null)
            return;

        if (!gameObject.activeInHierarchy ||
            (enemyClass.enemyDead != null && enemyClass.enemyDead.isDead))
            return;

        var agent = enemyClass.enemyController.agent;
        if (!agent.enabled)
            agent.enabled = true;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (restoreRotation)
            agent.updateRotation = true;
    }

    public void StopRot()
    {
        isCooldown = true;
        if (targetT != null)
            transform.LookAt(targetT);
        else if (playerTarget != null)
            transform.LookAt(playerTarget);
    }
    public abstract void AttackReset();
    public virtual void CameraShake()
    {
        FindObjectOfType<PlayerCamera>().Shake(0.24f, 0.25f);
    }
}

