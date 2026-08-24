using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [Header("Wandering Settings")]
    [SerializeField] private float idleTimeMin = 2f;
    [SerializeField] private float idleTimeMax = 7f;
    [SerializeField] private float moveSpeed = 3f;
    private float current_MoveSpeed = 3f;
    [SerializeField] private float chaseSpeed = 8f;
    private float current_chaseSpeed = 8f;
    [SerializeField] private float wanderRange = 20f;
    [SerializeField] private float knockBack = 2f;
    [SerializeField] private LayerMask knockbackBlockMask = ~0;
    [SerializeField] private float knockbackSafetyPadding = 0.08f;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float expandedDetectionRange = 20f;
    [SerializeField] internal float attackRange = 3.2f;
    [SerializeField] private LayerMask detectionLayer;
    [SerializeField] private LayerMask obstacleBlockMask;
    [SerializeField] private float lineOfSightHeight = 1.1f;
    [SerializeField] private float forwardBlockCheckDistance = 0.45f;

    EnemyClass enemyClass;

    internal NavMeshAgent agent;
    internal Animator ani;

    public Transform CurrentTarget { get; set; }
    public bool isTargetDetected;

    internal Transform aggroT;
    internal bool aggro;
    private int aggroVersion;

    public bool isWander;

    private Coroutine wanderCoroutine;
    private float currentDetectionRange;
    internal bool isStunned;
    Rigidbody rigidbody;
    private Vector3 velocity; // SmoothDamp용
    private float speedMultiplier = 1f;
    private Coroutine hitScaleCoroutine;
    private Coroutine knockbackCoroutine;
    private NavMeshPath cachedNavPath;
    private Collider[] detectionBuffer = new Collider[8];
    private bool isResettingPosition;
    private string lastAIDecision = "Not initialized";

    public string DebugStateName =>
        enemyClass != null && enemyClass.StateMachine != null && enemyClass.StateMachine.CurrentState != null
            ? enemyClass.StateMachine.CurrentState.GetType().Name
            : "None";
    public string DebugLastDecision => lastAIDecision;
    public Transform DebugCurrentTarget => CurrentTarget;
    public bool DebugIsAggro => aggro;
    public bool DebugIsStunned => isStunned;
    public bool DebugIsResettingPosition => isResettingPosition;
    public bool DebugIsChasing => chaseCoroutine != null;
    public bool DebugIsFollowingPath => followPathCoroutine != null;
    public bool DebugIsWandering => wanderCoroutine != null;
    public bool DebugIsKnockedBack => knockbackCoroutine != null;
    public bool DebugIsFleeing => IsFleeing;
    public bool DebugIsAttacking => enemyClass != null && enemyClass.enemyAttack != null && enemyClass.enemyAttack.IsAttacking;
    public string DebugAttackComponentName =>
        enemyClass != null && enemyClass.enemyAttack != null
            ? enemyClass.enemyAttack.GetType().Name
            : "None";
    public bool DebugHasAttackStateMismatch =>
        DebugIsAttacking &&
        enemyClass != null &&
        enemyClass.StateMachine != null &&
        enemyClass.StateMachine.CurrentState == enemyClass.IdleState;
    public string DebugAnimatorStateName => GetCurrentAnimatorStateName();
    public bool DebugHasStunAnimationMismatch =>
        !isStunned &&
        !DebugIsDead &&
        ani != null &&
        ani.GetCurrentAnimatorStateInfo(0).IsName("Stun");
    public bool DebugIsDead => enemyClass != null && enemyClass.enemyDead != null && enemyClass.enemyDead.isDead;
    public NavMeshAgent DebugAgent => agent;
    public Rigidbody DebugRigidbody => rigidbody;
    private void Awake()
    {
        enemyClass = GetComponent<EnemyClass>();
        agent = GetComponent<NavMeshAgent>();
        ani = GetComponent<Animator>();
        currentDetectionRange = detectionRange;
        agent.autoBraking = false;
        rigidbody = GetComponent<Rigidbody>();
        // Detection LayerMask 초기 설정: Player와 Summon 레이어를 모두 감지하도록 설정합니다.
        detectionLayer = LayerMask.GetMask("Player", "Summon");
        if (obstacleBlockMask == 0)
            obstacleBlockMask = LayerMask.GetMask("Wall", "Environment", "Default");
        cachedNavPath = new NavMeshPath();
    }
    private void Start()
    {
        RefreshMoveSpeed();
        returnPos = transform.position;
        StickToNavMesh();
    }

    public void DetectUpdate()
    {
        if (isStunned)
        {
            lastAIDecision = "Stopped: stunned";
            StopMovement();
            return;
        }

        if (enemyClass != null && enemyClass.enemyAttack != null && enemyClass.enemyAttack.IsAttacking)
        {
            lastAIDecision = "Stopped: attack in progress";
            StopMovement();
            return;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            lastAIDecision = agent == null
                ? "Stopped: NavMeshAgent missing"
                : (!agent.enabled ? "Stopped: NavMeshAgent disabled" : "Stopped: agent is off NavMesh");
            return;
        }
        // 강제 어그로 우선 처리
        if (aggro && aggroT != null)
        {
            lastAIDecision = "Following forced aggro target";
            HandleTargetDetection(aggroT); // 어그로 대상도 타겟으로 처리
            return;
        }

        DetectTargets();

        if (isTargetDetected)
            HandleTargetDetection(CurrentTarget);
        else
            HandleWandering();
    }

    public void ChangeDetectRange(float range)
    {
        detectionRange = range;
        expandedDetectionRange = range;
    }
    private void DetectTargets()
    {
        int hitCount;

        hitCount = Physics.OverlapSphereNonAlloc(transform.position,currentDetectionRange,detectionBuffer,detectionLayer);

        float closestDist = Mathf.Infinity;
        Transform closestTarget = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = detectionBuffer[i];
            if (col == null)
                continue;

            Transform target = col.transform;
            if (!IsValidDetectTarget(target))
                continue;

            if (!HasLineOfSight(target))
                continue;

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestTarget = target;
            }
        }

        if (closestTarget != null)
        {
            CurrentTarget = closestTarget;
            isTargetDetected = true;
            isWander = false;
            currentDetectionRange = expandedDetectionRange;
        }
        else
        {
            ResetState();
        }
    }

    private bool IsValidDetectTarget(Transform target)
    {
        if (target == null)
            return false;

        if (target == transform)
            return false;

        if (target.CompareTag("Player"))
            return true;

        if (target.CompareTag("Summon") || target.gameObject.layer == LayerMask.NameToLayer("Summon"))
        {
            SummonRuntime summonRuntime = target.GetComponentInParent<SummonRuntime>();
            return summonRuntime != null && summonRuntime.IsAggroAllowed;
        }

        SummonRuntime runtime = target.GetComponentInParent<SummonRuntime>();
        if (runtime != null)
            return runtime.IsAggroAllowed;

        return true;
    }
    public void SetMoveSpeedMultiplier(float ratio)
    {
        speedMultiplier = Mathf.Max(0f, ratio);
        RefreshMoveSpeed();
    }

    private void RefreshMoveSpeed()
    {
        current_MoveSpeed = moveSpeed * speedMultiplier;
        current_chaseSpeed = chaseSpeed * speedMultiplier;
    }

    public void ResetControllerStateForReuse()
    {
        if (chaseCoroutine != null) { StopCoroutine(chaseCoroutine); chaseCoroutine = null; }
        if (followPathCoroutine != null) { StopCoroutine(followPathCoroutine); followPathCoroutine = null; }
        if (wanderCoroutine != null) { StopCoroutine(wanderCoroutine); wanderCoroutine = null; }
        if (stunCor != null) { StopCoroutine(stunCor); stunCor = null; }
        if (knockbackCoroutine != null) { StopCoroutine(knockbackCoroutine); knockbackCoroutine = null; }
        if (hitScaleCoroutine != null) { StopCoroutine(hitScaleCoroutine); hitScaleCoroutine = null; }

        aggro = false;
        aggroT = null;
        aggroVersion++;
        CurrentTarget = null;
        isTargetDetected = false;
        isWander = false;
        isResettingPosition = false;
        IsFleeing = false;
        isStunned = false;
        remainingStunTime = 0f;
        currentDetectionRange = detectionRange;
        velocity = Vector3.zero;
        lastAIDecision = "Reset for pool reuse";

        if (rigidbody != null && !rigidbody.isKinematic)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        if (agent != null && !agent.enabled)
            agent.enabled = true;

        StopMovement();
    }
    private void HandleTargetDetection(Transform target)
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        LookAtTarget(target);

        if (dist <= attackRange)
        {
            lastAIDecision = "Target in attack range";
            StopMovement();
            if (chaseCoroutine != null)
            {
                StopCoroutine(chaseCoroutine);
                chaseCoroutine = null;
            }
            if (followPathCoroutine != null)
            {
                StopCoroutine(followPathCoroutine);
                followPathCoroutine = null;
            }

            enemyClass.StateMachine.CurrentState.OnAttack(target);
        }
        else
        {
            if (chaseCoroutine == null)
            {
                lastAIDecision = "Starting chase";
                chaseCoroutine = StartCoroutine(ChaseTarget(target, current_chaseSpeed));
            }
            else
            {
                lastAIDecision = "Chasing target";
            }
        }
    }

    private IEnumerator ChaseTarget(Transform target, float speed)
    {
        if (isStunned)
        {
            lastAIDecision = "Chase cancelled: stunned";
            chaseCoroutine = null;
            yield break;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            lastAIDecision = "Chase cancelled: invalid NavMeshAgent";
            chaseCoroutine = null;
            yield break;
        }

        agent.isStopped = false;
        agent.speed = speed;
        ani.SetFloat("Speed", speed);

        while (target != null && agent.isActiveAndEnabled)
        {
            if (isStunned)
            {
                lastAIDecision = "Chase interrupted: stunned";
                chaseCoroutine = null;
                yield break;
            }

            if (!agent.isOnNavMesh)
            {
                lastAIDecision = "Chase interrupted: agent left NavMesh";
                chaseCoroutine = null;
                yield break;
            }

            float dist = Vector3.Distance(transform.position, target.position);

            if (dist <= attackRange)
                break;

            if (!TrySetDestination(target.position))
            {
                lastAIDecision = "Chase stopped: destination invalid";
                ResetState();
                break;
            }

            if (IsMovementBlockedTowards(agent.steeringTarget))
            {
                lastAIDecision = "Chase blocked: returning to spawn anchor";
                StopMovement();
                if (!isResettingPosition)
                    ResetPos();
                break;
            }

            if (agent.velocity.sqrMagnitude > 0.01f)
            {
                Quaternion rot = Quaternion.LookRotation(agent.velocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
            }

            yield return null;
        }

        StopMovement();
        chaseCoroutine = null;
    }
    public bool StickToNavMesh()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            lastAIDecision = "NavMesh placement failed: agent missing";
            return false;
        }

        if (!agent.enabled)
            agent.enabled = true;

        // 현재 위치에서 반경 5.0m 이내의 가장 가까운 NavMesh 바닥을 찾습니다.
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
        {
            // 에이전트를 해당 위치로 즉시 순간이동 (이게 제일 확실함)
            if (!agent.Warp(hit.position))
            {
                lastAIDecision = "NavMesh placement failed: Warp returned false";
                return false;
            }

            agent.ResetPath();
            agent.isStopped = false;
            lastAIDecision = "Placed on NavMesh";
            return agent.isOnNavMesh;
        }

        lastAIDecision = "NavMesh placement failed: no sampled position";
        Debug.LogWarning($"{gameObject.name} 주변에 NavMesh 바닥을 찾을 수 없습니다! Bake 상태를 확인하세요.");
        return false;
    }
    Vector3 returnPos;
    public void SetSpawnAnchor(Vector3 position)
    {
        returnPos = position;
    }
    public void ResetPos()
    {
        if (isResettingPosition)
            return;

        StartCoroutine(ResetPosCor());
    }
    private IEnumerator ResetPosCor()
    {
        isResettingPosition = true;

        if (followPathCoroutine != null)
            StopCoroutine(followPathCoroutine);
        followPathCoroutine = StartCoroutine(FollowPath(returnPos, current_chaseSpeed, attackRange - 0.2f));
        yield return followPathCoroutine;
        followPathCoroutine = null;
        isResettingPosition = false;

    }

    private void HandleWandering()
    {
        isWander = true;
        if (wanderCoroutine == null)
        {
            lastAIDecision = "Starting wander";
            StopMovement();
            wanderCoroutine = StartCoroutine(WanderRoutine());
        }
        else
        {
            lastAIDecision = "Wandering";
        }
    }
    private IEnumerator WanderRoutine()
    {
        if (isStunned)
        {
            wanderCoroutine = null;
            yield break;
        }

        Vector3 randomDir = Random.insideUnitSphere * wanderRange + transform.position;
        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, wanderRange, NavMesh.AllAreas))
        {
            yield return FollowPath(hit.position, current_MoveSpeed);
            yield return new WaitForSeconds(Random.Range(idleTimeMin, idleTimeMax));
        }
        wanderCoroutine = null; // 루프 끝나면 다시 Wander 시작 가능하게
    }

    public void StopMovement()
    {
        if (ani != null)
            ani.SetFloat("Speed", 0);

        if (agent == null)
            return;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.speed = 0;
            agent.ResetPath(); // 현재 경로 제거
            agent.velocity = Vector3.zero;
        }
    }
    #region Aggro
    public void SetAggro(Transform target, float duration = -1f)
    {
        if (target == null)
            return;

        aggroT = target;
        aggro = true;
        aggroVersion++;

        // 기존 상태 초기화
        isTargetDetected = false;

        if (duration > 0f)
            StartCoroutine(AggroDurationRoutine(duration, aggroVersion));
    }
    public void ClearAggro(Transform target)
    {
        if (target != null && aggroT != target)
            return;

        aggro = false;
        aggroT = null;
        aggroVersion++;
        isTargetDetected = false;
    }
    private IEnumerator AggroDurationRoutine(float duration, int version)
    {
        yield return new WaitForSeconds(duration);

        if (version != aggroVersion)
            yield break;

        aggro = false;
        aggroT = null;
        aggroVersion++;
    }
    #endregion
    public void LookAtTarget(Transform target)
    {
        if (isStunned)
            return;
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 120f);
    }

    private void ResetState()
    {
        isTargetDetected = false;
        isWander = true;
        CurrentTarget = null;
        currentDetectionRange = detectionRange;
    }

    public void ApplyHitReaction(Vector3 attackerPos, CombatFeedbackRequest request)
    {
        if (request == null || request.reactionTier == HitReactionTier.None)
            return;

        if (request.enableScalePunch)
            PlayHitScale(request.scalePunchStrength, request.scalePunchDuration);

        if (knockbackCoroutine != null)
            StopCoroutine(knockbackCoroutine);

        knockbackCoroutine = StartCoroutine(KnockbackRoutine(attackerPos, request));
    }
    // 도망 중인지 확인하는 변수 (외부 접근용)

    // 모든 이동/행동을 즉시 멈추는 통합 관리 함수
    public void StopAllMovementCoroutines()
    {
        if (chaseCoroutine != null) { StopCoroutine(chaseCoroutine); chaseCoroutine = null; }
        if (followPathCoroutine != null) { StopCoroutine(followPathCoroutine); followPathCoroutine = null; }
        if (wanderCoroutine != null) { StopCoroutine(wanderCoroutine); wanderCoroutine = null; }

        // 공격 코루틴도 여기서 끊어주면 더 확실함
        if (enemyClass != null && enemyClass.enemyAttack != null)
        {
            enemyClass.enemyAttack.AttackReset();
        }

        StopMovement(); // 물리적 정지
    }
    internal bool IsFleeing;

    public void FleeFromTarget()
    {
        StartCoroutine(FleeFromTargetCor());
    }
    // [핵심] 이 함수 하나만 부르면 도망갑니다.
    private IEnumerator FleeFromTargetCor() // 거리를 조금 더 늘려보세요
    {
        if (IsFleeing)
            yield break;

        if (CurrentTarget == null)
        {
            lastAIDecision = "Flee cancelled: target missing";
            yield break;
        }

        IsFleeing = true;
        lastAIDecision = "Fleeing from target";

        // 1. 플레이어와 반대되는 방향 벡터
        Vector3 fleeDir = (transform.position - CurrentTarget.position).normalized;

        // 2. 일단 아주 멀리(예: 10m) 후보 지점을 잡습니다.
        Vector3 potentialFleePos = transform.position + fleeDir * (attackRange + 0.5f);

        Vector3 finalDest = transform.position; // 기본값은 현재 위치

        // 3. NavMesh 위에서 유효한 위치 찾기
        // 범위를 너무 크게 주면(fleeSafeDistance) 제자리 근처가 잡힐 수 있으니 
        // 적당한 범위(3~4f) 내에서 가장 가까운 NavMesh를 찾습니다.
        if (NavMesh.SamplePosition(potentialFleePos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            finalDest = hit.position;
        }
        else
        {
            // 만약 정반대 방향이 막혔다면? 약간의 랜덤성을 섞어 측면으로라도 튑니다.
            Vector3 randomDir = Quaternion.Euler(0, Random.Range(-45f, 45f), 0) * fleeDir;
            Vector3 fallbackPos = transform.position + randomDir * (attackRange + 0.5f);

            if (NavMesh.SamplePosition(fallbackPos, out NavMeshHit hitFallback, 5f, NavMesh.AllAreas))
            {
                finalDest = hitFallback.position;
            }
        }

        // 4. 이동 실행
        followPathCoroutine = StartCoroutine(FollowPath(finalDest, current_chaseSpeed * 1.5f)); // 도망은 더 빠르게!
        yield return followPathCoroutine;
        followPathCoroutine = null;

        IsFleeing = false;

        // 상태 종료 알림
        enemyClass.StateMachine.CurrentState.OnActionEnd();
    }
    private IEnumerator KnockbackRoutine(Vector3 attackerPos, CombatFeedbackRequest request)
    {
        if (agent == null || rigidbody == null)
            yield break;

        Vector3 dir = (transform.position - attackerPos).normalized;
        float reactionForce = Mathf.Max(0.1f, request.reactionForce);
        float knockbackPower = knockBack * 1.5f * reactionForce;
        float duration = GetReactionDuration(request.reactionTier);
        float stunDuration = GetReactionStunDuration(request.reactionTier);
        float intendedDistance = GetReactionDistance(request.reactionTier, knockbackPower);
        Vector3 startPosition = rigidbody != null ? rigidbody.position : transform.position;
        Vector3 targetPosition = ResolveKnockbackTarget(startPosition, dir, intendedDistance);

        agent.enabled = false;

        if (rigidbody != null)
        {
            RigidbodyConstraints originalConstraints = rigidbody.constraints;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            float timer = 0f;

            while (timer < duration)
            {
                float t = timer / duration;
                float curveValue = AnimationCurve.EaseInOut(0, 0, 1, 1).Evaluate(t);
                Vector3 movePosition = Vector3.Lerp(startPosition, targetPosition, curveValue);
                rigidbody.MovePosition(movePosition);
                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            rigidbody.MovePosition(targetPosition);
            rigidbody.constraints = originalConstraints;
        }

        agent.enabled = true;
        if (agent.isOnNavMesh)
            agent.Warp(transform.position);

        if (ShouldApplyStun(request.reactionTier))
            enemyClass.Stun(stunDuration);
        knockbackCoroutine = null;
    }

    private static bool ShouldApplyStun(HitReactionTier reactionTier)
    {
        return reactionTier switch
        {
            HitReactionTier.PushBack => true,
            HitReactionTier.HeavyKnockback => true,
            _ => false
        };
    }
    private Coroutine chaseCoroutine;
    internal Coroutine followPathCoroutine;
private IEnumerator FollowPath(Vector3 target, float speed, float stopDistance = 0.2f)
{
    if (isStunned)
    {
        lastAIDecision = "Path cancelled: stunned";
        yield break;
    }

    if (agent == null || !agent.enabled || !agent.isOnNavMesh)
    {
        lastAIDecision = "Path cancelled: invalid NavMeshAgent";
        yield break;
    }

    agent.isStopped = false;
    agent.speed = speed;
    if (!TrySetDestination(target))
    {
        lastAIDecision = "Path cancelled: destination invalid";
        yield break;
    }

    lastAIDecision = "Following path";
    ani.SetFloat("Speed", speed);

    while (true)
    {
        if (isStunned)
        {
            lastAIDecision = "Path interrupted: stunned";
            yield break;
        }

        // 1. 방어 코드: 오브젝트가 파괴되었거나, 에이전트가 비활성화/NavMesh이탈 시 즉시 중단
        if (this == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            lastAIDecision = "Path interrupted: agent unavailable";
            yield break;
        }

        // 2. 적이 죽었는지 확인
        if (enemyClass != null && enemyClass.enemyDead != null && enemyClass.enemyDead.isDead)
            break;

        // 3. 목적지 도착 확인 (위의 방어 코드를 통과했으므로 안전하게 호출 가능)
        if (!agent.pathPending && agent.remainingDistance <= stopDistance)
            break;

        if (IsMovementBlockedTowards(agent.steeringTarget))
        {
            lastAIDecision = "Path blocked";
            StopMovement();
            if (!isResettingPosition)
                ResetPos();
            yield break;
        }

        // 이동 중 회전 보정
        if (agent.velocity.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
        }

        yield return null;
    }

    // 종료 시점에서 다시 한번 안전 체크 후 정지
    if (agent != null && agent.enabled && agent.isOnNavMesh)
    {
        agent.isStopped = true;
    }

    lastAIDecision = "Path completed";
    isResettingPosition = false;

    if (ani != null)
    {
        ani.SetFloat("Speed", 0);
    }
}
    private Coroutine stunCor;
    private float remainingStunTime = 0f;
    public bool CanExitStunState => !isStunned && remainingStunTime <= 0f;

    public void StunEnemy(float stunDuration)
    {
        if (stunDuration <= 0f)
            return;

        // 더 긴 스턴만 갱신
        if (stunDuration > remainingStunTime)
        {
            remainingStunTime += stunDuration;

            if (stunCor == null)
                stunCor = StartCoroutine(StunRoutine());
        }
    }

    private IEnumerator StunRoutine()
    {
        isStunned = true;
        if (ani != null)
            ani.SetTrigger("Stun");
        StopAllMovementCoroutines();
        StopMovement();
        enemyClass.enemyAttack.AttackReset();

        if (agent != null && agent.enabled)
            agent.enabled = false;

        while (remainingStunTime > 0f)
        {
            remainingStunTime -= Time.deltaTime;
            yield return null;
        }

        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            agent.Warp(transform.position); // 현재 위치로 에이전트의 위치 정보를 강제 동기화
        }

        // 스턴 해제
        isStunned = false;
        ResetStunAnimatorState();
        stunCor = null;
        remainingStunTime = 0;
        if (enemyClass != null && enemyClass.StateMachine != null && enemyClass.StateMachine.CurrentState == enemyClass.StunState)
            enemyClass.StateMachine.CurrentState.OnActionEnd();
    }

    private void ResetStunAnimatorState()
    {
        if (ani == null || DebugIsDead)
            return;

        ani.ResetTrigger("Stun");
        ani.SetBool("Attacking", false);
        ani.SetFloat("Speed", 0);

        AnimatorStateInfo stateInfo = ani.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Stun"))
        {
            ani.CrossFade("Blend Tree", 0.05f, 0);
        }
    }

    private string GetCurrentAnimatorStateName()
    {
        if (ani == null)
            return "None";

        AnimatorStateInfo stateInfo = ani.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Stun"))
            return "Stun";
        if (stateInfo.IsName("Blend Tree"))
            return "Blend Tree";
        if (stateInfo.IsName("Dead"))
            return "Dead";
        if (stateInfo.IsName("Attack1"))
            return "Attack1";
        if (stateInfo.IsName("Attack2"))
            return "Attack2";
        if (stateInfo.IsName("Attack"))
            return "Attack";

        return stateInfo.shortNameHash.ToString();
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wanderRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, currentDetectionRange);
    }

    private float GetReactionDuration(HitReactionTier reactionTier)
    {
        switch (reactionTier)
        {
            case HitReactionTier.LightStagger:
                return 0.08f;
            case HitReactionTier.HeavyKnockback:
                return 0.22f;
            case HitReactionTier.PushBack:
                return 0.16f;
            default:
                return 0.12f;
        }
    }

    private float GetReactionStunDuration(HitReactionTier reactionTier)
    {
        switch (reactionTier)
        {
            case HitReactionTier.LightStagger:
                return 0.18f;
            case HitReactionTier.HeavyKnockback:
                return 0.65f;
            case HitReactionTier.PushBack:
                return 0.35f;
            default:
                return 0.2f;
        }
    }

    private float GetReactionDistance(HitReactionTier reactionTier, float knockbackPower)
    {
        switch (reactionTier)
        {
            case HitReactionTier.LightStagger:
                return Mathf.Min(0.35f, knockbackPower * 0.08f);
            case HitReactionTier.HeavyKnockback:
                return Mathf.Clamp(knockbackPower * 0.28f, 0.75f, 2.0f);
            case HitReactionTier.PushBack:
                return Mathf.Clamp(knockbackPower * 0.18f, 0.4f, 1.25f);
            default:
                return 0f;
        }
    }

    private Vector3 ResolveKnockbackTarget(Vector3 startPosition, Vector3 direction, float intendedDistance)
    {
        if (intendedDistance <= 0f)
            return startPosition;

        direction.y = 0f;
        direction.Normalize();

        float safeDistance = intendedDistance;
        Vector3 capsuleStart = startPosition + Vector3.up * 0.15f;
        float radius = agent != null ? Mathf.Max(0.1f, agent.radius * 0.9f) : 0.2f;
        float capsuleHeight = agent != null ? Mathf.Max(radius * 2f + 0.1f, agent.height - 0.1f) : 1.5f;
        Vector3 capsuleEnd = capsuleStart + Vector3.up * Mathf.Max(0f, capsuleHeight - radius * 2f);

        if (Physics.CapsuleCast(capsuleStart, capsuleEnd, radius, direction, out RaycastHit hit, intendedDistance, knockbackBlockMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.transform != transform)
                safeDistance = Mathf.Max(0f, hit.distance - knockbackSafetyPadding);
        }

        Vector3 candidate = startPosition + direction * safeDistance;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 0.6f, NavMesh.AllAreas))
            return navHit.position;

        if (NavMesh.SamplePosition(startPosition + direction * (safeDistance * 0.5f), out navHit, 0.6f, NavMesh.AllAreas))
            return navHit.position;

        return startPosition;
    }

    private void PlayHitScale(float strength, float duration)
    {
        if (strength <= 0f || duration <= 0f || enemyClass == null || enemyClass.IsBoss)
            return;

        if (hitScaleCoroutine != null)
            StopCoroutine(hitScaleCoroutine);

        hitScaleCoroutine = StartCoroutine(HitScaleRoutine(strength, duration));
    }

    private IEnumerator HitScaleRoutine(float strength, float duration)
    {
        Vector3 originalScale = transform.localScale;
        Vector3 squashedScale = new Vector3(
            originalScale.x + strength,
            Mathf.Max(0.7f, originalScale.y - strength * 0.9f),
            originalScale.z + strength);

        float halfDuration = duration * 0.5f;
        float timer = 0f;

        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, squashedScale, timer / halfDuration);
            yield return null;
        }

        timer = 0f;
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            transform.localScale = Vector3.Lerp(squashedScale, originalScale, timer / halfDuration);
            yield return null;
        }

        transform.localScale = originalScale;
        hitScaleCoroutine = null;
    }

    private bool TrySetDestination(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(destination, out NavMeshHit navHit, 1.2f, NavMesh.AllAreas))
            return false;

        if (!agent.CalculatePath(navHit.position, cachedNavPath))
            return false;

        if (cachedNavPath.status != NavMeshPathStatus.PathComplete)
            return false;

        return agent.SetDestination(navHit.position);
    }

    private bool HasLineOfSight(Transform target)
    {
        if (target == null)
            return false;

        Vector3 origin = transform.position + Vector3.up * lineOfSightHeight;
        Vector3 targetPoint = target.position + Vector3.up * lineOfSightHeight;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
            return true;

        return !Physics.Raycast(origin, direction.normalized, distance, obstacleBlockMask, QueryTriggerInteraction.Ignore);
    }

    private bool IsMovementBlockedTowards(Vector3 targetPoint)
    {
        Vector3 origin = transform.position + Vector3.up * 0.4f;
        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
            return false;

        direction.Normalize();
        float radius = agent != null ? Mathf.Max(0.1f, agent.radius * 0.9f) : 0.2f;
        Vector3 capsuleEnd = origin + Vector3.up * Mathf.Max(0.4f, (agent != null ? agent.height * 0.5f : 1f));

        return Physics.CapsuleCast(
            origin,
            capsuleEnd,
            radius,
            direction,
            forwardBlockCheckDistance,
            obstacleBlockMask,
            QueryTriggerInteraction.Ignore);
    }
}

