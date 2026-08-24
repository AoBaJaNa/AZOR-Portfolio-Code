using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerSkill : MonoBehaviour
{

    [Header("몬스터 소환 설정")]
    //public int needStamina = 100;
    public Transform[] summonPoints; // 소환 위치
    public GameObject ghoulPrefab;
    public GameObject ghostDogPrefab;
    public GameObject ghostSkullPrefab;
    public GameObject nunPrefab;
    public GameObject skullWarriorPrefab;
    public GameObject paganPrefab;
    [SerializeField] private SummonAbilityConfigSO summonAbilityConfig;
    public SummonAbilityConfigSO SummonAbilityConfig => summonAbilityConfig;


    [Header("이펙트 프리팹")]
    public GameObject SummonSpawnPrefab;

    [Header("더블 스킬 충돌 영역")]
    public SphereCollider double_swordCrashCollider;
    public Collider double_dashSlashCollider;
    public Collider double_backstepSpinCollider;
    public Collider double_counterDashCollider;
    public SphereCollider double_jumpLandingCollider;
    public Collider double_crossSlash1;
    public Collider double_crossSlash2;
    public Collider double_PierceSlash;
    public SphereCollider double_roundSlashCollider;

    [Header("이펙트 생성 위치")]
    public Transform slashComboSpawnPoint;
    public Transform slashBurstSpawnPoint;
    public Transform attackbuffSpawnPoint;
    public Transform shieldbuffSpawnPoint;
    public Transform swordCrashSpawnPoint;
    public Transform swordRainSpawnPoint;
    public Transform dashSlashSpawnPoint;
    public Transform pierceSlashSpawnPoint;
    public Transform roundSlashSpawnPoint;
    public Transform strikeAssaultSpawnPoint;
    public Transform backstepSpinSpawnPoint;
    public Transform blinkStrikeSpawnPoint;
    public Transform counterDashSpawnPoint;
    public Transform counterReflectionSpawnPoint;
    public Transform counterReflectionAttackSpawnPoint;
    public Transform jumpLandingSpawnPoint;
    public Transform crossSlashSpawnPoint;
    public Transform impulseStunSpawnPoint;
    public Transform waveStunSpawnPoint;

    [Header("스킬 충돌 영역")]
    public SphereCollider slashComboCollider;
    public SphereCollider slashBurstCollider;
    public SphereCollider swordCrashCollider;
    public SphereCollider swordRainCollider;
    public Collider dashSlashCollider;
    public Collider backstepSpinCollider;
    public Collider counterDashCollider;
    public SphereCollider jumpLandingCollider;
    public Collider crossSlash1;
    public Collider crossSlash2;
    public Collider PierceSlash;
    public SphereCollider roundSlashCollider;

    [HideInInspector] public GameObject shieldobj;
    [HideInInspector] public GameObject activeSwordRainEffect;

    public event Action OnSkillUsed;
    internal Rigidbody rigidBody;

    PlayerController playerController;
    PlayerCombat playerCombat;
    PlayerTargetSystem playerTargetSystem;
    PlayerMovement playerMovement;
    PlayerPortal playerPortal;
    PassiveSkillManager passiveSkillManager;
    Animator animator;
    private int pendingActionEndRoutines;
    private readonly HashSet<int> combatCollisionIgnoreTokens = new();
    private int nextCombatCollisionIgnoreToken = 1;
    private int legacyCombatCollisionIgnoreToken;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        passiveSkillManager = FindFirstObjectByType<PassiveSkillManager>();
        playerCombat = GetComponent<PlayerCombat>();
        playerTargetSystem = GetComponent<PlayerTargetSystem>();
        playerPortal = GetComponent<PlayerPortal>();
        playerMovement = GetComponent<PlayerMovement>();
    }
    
    public void OnSummonSpawn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (playerController.LockPlayer || playerCombat.IsAttacking || playerCombat.IsSkill)
            {
                InGameUI.ShowWarning("지금은 소환할 수 없습니다.");
                return;
            }

            var type = PlayerInfo.Instance.summonInven.eqiutSummonType;
            if (type == SummonType.None)
            {
                InGameUI.ShowWarning("소환 영혼이 장착되어 있지 않습니다.");
                return;
            }
            int cost = GetSummonStoneCost(type);

            if (PlayerInfo.Instance.summonInven.summonStone < cost)
            {
                InGameUI.ShowWarning("소환석이 부족합니다.");
            }
            else if (summoning)
            {
                InGameUI.ShowWarning("소환 중입니다..");
            }
            else
            {
                StartCoroutine(SummonAllPoints(type, cost));
            }
        }
    }
    public bool CanUseSkill(SkillData skill)
    {
        if (skill == null) return false;
        if (PlayerInfo.Instance == null) return false;
        if (playerController == null) return false;
        if (playerController.dash == null) return false;
        if (Time.time < GetLastUsedTime(skill.skillType) + skill.cooldown) return false;
        if (PlayerInfo.Instance.stamina < skill.staminaCost) return false;
        return true;
    }
    public bool TryUseSkill(SkillData skill)
    {
        if (!CanUseSkill(skill))
            return false;

        if (skill.skillType == SkillType.SwordRain)
            EndSwordRain();

/*        if (skill.skillType == SkillType.ShadowRecall &&
            positionHistory != null &&
            positionHistory.Count != 0)
            ShadowRecallEnd();*/

        playerController.RegisterInput();
        ExecuteSkillByType(skill);
        PlayerInfo.Instance.UsePlayerStamina(skill.staminaCost);
        PlayerInfo.Instance.GainStaminaFromSkill(skill.staminaCost);
        return true;
    }
    public float GetLastUsedTime(SkillType type)
    {
        var skillData = SkillManager.instance.GetSkillData(type);
        return skillData != null ? skillData.lastUsedTime : 0f;
    }

    void SetLastUsedTime(SkillType type, float time)
    {
        var skillData = SkillManager.instance.GetSkillData(type);
        if (skillData != null)
        {
            skillData.lastUsedTime = Time.time;
        }
    }
    public void ExecuteSkillByType(SkillData data)
    {
        playerCombat.ClearDamagedEnemyList();

        SetLastUsedTime(data.skillType, Time.time);
        StartCoroutine(ExecuteSkillRoutine(data));
        OnSkillUsed?.Invoke(); // 공통으로 한 번만

        playerController.playerPassiveController.HandleOnAttack(data);
    }

    private IEnumerator ExecuteSkillRoutine(SkillData data)
    {
        yield return StartCoroutine(data.SkillLogic(playerController));

        if (pendingActionEndRoutines == 0 &&
            playerController != null &&
            playerController.StateMachine != null &&
            playerController.StateMachine.CurrentState == playerController.AttackState)
        {
            data.isActive = false;
            playerCombat.SetIsSkill(false);
            playerController.StateMachine.CurrentState.OnActionEnd();
        }
    }
    public void SkillCoolDown(SkillType type, float reduceTime)
    {
        SkillManager.instance.GetSkillData(type).lastUsedTime -= reduceTime;
    }
    #region Summon
    // 소환된 오브젝트 목록 관리
    private List<GameObject> activeSummons = new List<GameObject>();
    bool summoning = false;
    public IEnumerator SummonAllPoints(SummonType type, int cost)
    {
        summoning = true;
        GameObject prefabToSummon = GetPrefabByType(type);
        if (prefabToSummon == null || summonAbilityConfig == null)
        {
            Debug.LogError($"Summon setup is missing. Type: {type}, Prefab: {prefabToSummon}, Config: {summonAbilityConfig}");
            summoning = false;
            yield break;
        }

        ClearSummons();

        int summonCount = GetSummonCount(type);
        float spawnDelay = GetSpawnDelay(type);
        float activationDelay = GetActivationDelay(type);
        float spawnLockDuration = GetSpawnLockDuration(type);
        Vector3 basePosition = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
            forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        BeginSummonCast();
        playerController.animator.SetTrigger("Summon");
        Inventory.Instance.ChangeSummonStone(-cost);
        PlayerSoundManager.PlaySound("Summon");

        for (int i = 0; i < summonCount; i++)
        {
            GetSummonSpawnTransform(type, i, summonCount, basePosition, forward, right, out Vector3 spawnPosition, out Quaternion spawnRotation);
            EffectPoolManager.Instance.GetFromPool(
                SummonSpawnPrefab,
                spawnPosition,
                Quaternion.Euler(-90, spawnRotation.eulerAngles.y, 0)
            );
        }

        if (spawnDelay > 0f)
            yield return YieldInstructionCache.GetWait(spawnDelay);

        List<SummonRuntime> pendingActivations = new List<SummonRuntime>(summonCount);
        for (int i = 0; i < summonCount; i++)
        {
            GetSummonSpawnTransform(type, i, summonCount, basePosition, forward, right, out Vector3 spawnPosition, out Quaternion spawnRotation);
            GameObject obj = EffectPoolManager.Instance.GetFromPoolInactive(
                prefabToSummon,
                spawnPosition,
                spawnRotation);

            if (obj == null)
                continue;

            SummonRuntime runtime = obj.GetComponent<SummonRuntime>();
            if (runtime == null)
                runtime = obj.AddComponent<SummonRuntime>();

            runtime.Initialize(playerController, type, summonAbilityConfig, prefabToSummon, i);
            obj.transform.position = spawnPosition;
            obj.transform.rotation = spawnRotation;
            obj.SetActive(true);
            pendingActivations.Add(runtime);
            activeSummons.Add(obj);
        }

        if (activationDelay > 0f)
            yield return YieldInstructionCache.GetWait(activationDelay);

        for (int i = 0; i < pendingActivations.Count; i++)
        {
            if (pendingActivations[i] != null)
                pendingActivations[i].Activate();
        }

        float releaseDelay = Mathf.Max(0f, spawnLockDuration - spawnDelay - activationDelay);
        if (releaseDelay > 0f)
            yield return new WaitForSeconds(releaseDelay);

        EndSummonCast();
        summoning = false;
    }

    // 기존 소환 제거 함수
    private void ClearSummons()
    {
        for (int i = 0; i < activeSummons.Count; i++)
        {
            if (activeSummons[i] != null)
            {
                SummonRuntime runtime = activeSummons[i].GetComponent<SummonRuntime>();
                if (runtime != null)
                    runtime.Despawn("ManualClear");
                else
                    activeSummons[i].SetActive(false);
            }
        }
        activeSummons.Clear();
    }

    private int GetSummonCount(SummonType type)
    {
        return type switch
        {
            SummonType.Ghoul => Mathf.Max(1, summonAbilityConfig.ghoul.summonCount),
            SummonType.Nun => Mathf.Max(1, summonAbilityConfig.nun.summonCount),
            SummonType.GhostSkull => Mathf.Max(1, summonAbilityConfig.ghostSkull.summonCount),
            _ => 1
        };
    }

    private int GetSummonStoneCost(SummonType type)
    {
        return type switch
        {
            SummonType.Ghoul => Mathf.Max(0, summonAbilityConfig.ghoul.summonStoneCost),
            SummonType.GhostDog => Mathf.Max(0, summonAbilityConfig.ghostDog.summonStoneCost),
            SummonType.GhostSkull => Mathf.Max(0, summonAbilityConfig.ghostSkull.summonStoneCost),
            SummonType.SkullWarrior => Mathf.Max(0, summonAbilityConfig.skeletonWarrior.summonStoneCost),
            SummonType.Nun => Mathf.Max(0, summonAbilityConfig.nun.summonStoneCost),
            SummonType.Pagan => Mathf.Max(0, summonAbilityConfig.pagan.summonStoneCost),
            _ => 0
        };
    }

    private float GetSpawnDelay(SummonType type)
    {
        return GetTiming(type).spawnDelay;
    }

    private float GetActivationDelay(SummonType type)
    {
        return GetTiming(type).activationDelay;
    }

    private float GetSpawnLockDuration(SummonType type)
    {
        return GetTiming(type).spawnLockDuration;
    }

    private SummonAbilityConfigSO.SummonTimingSettings GetTiming(SummonType type)
    {
        switch (type)
        {
            case SummonType.Ghoul:
                return summonAbilityConfig.ghoul.timing;
            case SummonType.GhostDog:
                return summonAbilityConfig.ghostDog.timing;
            case SummonType.Nun:
                return summonAbilityConfig.nun.timing;
            case SummonType.SkullWarrior:
                return summonAbilityConfig.skeletonWarrior.timing;
            case SummonType.GhostSkull:
                return summonAbilityConfig.ghostSkull.timing;
            case SummonType.Pagan:
                return summonAbilityConfig.pagan.timing;
            default:
                return new SummonAbilityConfigSO.SummonTimingSettings();
        }
    }

    private void BeginSummonCast()
    {
        playerCombat.SetIsSkill(true);
        playerMovement.SetStopMovement(true, true);
        playerController.SetLockPlayer(true);
    }

    private void EndSummonCast()
    {
        playerController.SetLockPlayer(false);
        playerCombat.SetIsSkill(false);
        playerMovement.Initialize();
    }

    private void OnDisable()
    {
        ForceClearCombatCollisionIgnore();

        if (!summoning)
            return;

        summoning = false;
        EndSummonCast();
    }

    private Transform GetSummonPoint(int index)
    {
        if (summonPoints == null || summonPoints.Length == 0)
            return transform;

        Transform point = summonPoints[index % summonPoints.Length];
        return point != null ? point : transform;
    }

    private void GetSummonSpawnTransform(
        SummonType type,
        int index,
        int totalCount,
        Vector3 basePosition,
        Vector3 forward,
        Vector3 right,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation)
    {
        float forwardDistance;
        float formationRadius;
        float fanAngle;

        switch (type)
        {
            case SummonType.Ghoul:
                forwardDistance = summonAbilityConfig.ghoul.spawnForwardDistance;
                formationRadius = summonAbilityConfig.ghoul.formationRadius;
                fanAngle = summonAbilityConfig.ghoul.fanAngle;
                break;
            case SummonType.GhostDog:
                forwardDistance = summonAbilityConfig.ghostDog.spawnForwardDistance;
                formationRadius = summonAbilityConfig.ghostDog.formationRadius;
                fanAngle = summonAbilityConfig.ghostDog.fanAngle;
                break;
            case SummonType.GhostSkull:
                forwardDistance = summonAbilityConfig.ghostSkull.spawnForwardDistance;
                formationRadius = summonAbilityConfig.ghostSkull.formationRadius;
                fanAngle = summonAbilityConfig.ghostSkull.fanAngle;
                break;
            case SummonType.SkullWarrior:
                forwardDistance = summonAbilityConfig.skeletonWarrior.spawnForwardDistance;
                formationRadius = summonAbilityConfig.skeletonWarrior.formationRadius;
                fanAngle = summonAbilityConfig.skeletonWarrior.fanAngle;
                break;
            case SummonType.Nun:
                forwardDistance = summonAbilityConfig.nun.spawnForwardDistance;
                formationRadius = summonAbilityConfig.nun.formationRadius;
                fanAngle = summonAbilityConfig.nun.fanAngle;
                break;
            case SummonType.Pagan:
                forwardDistance = summonAbilityConfig.pagan.spawnForwardDistance;
                formationRadius = summonAbilityConfig.pagan.formationRadius;
                fanAngle = summonAbilityConfig.pagan.fanAngle;
                break;
            default:
                forwardDistance = 2f;
                formationRadius = 0f;
                fanAngle = 0f;
                break;
        }

        Vector3 center = basePosition + forward * forwardDistance;
        Vector3 flatForward = forward;
        Vector3 offset = Vector3.zero;

        if (type == SummonType.Ghoul && totalCount > 1)
        {
            float angle = index * (360f / totalCount);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * flatForward;
            spawnPosition = basePosition + dir * Mathf.Max(0.1f, formationRadius);
            spawnRotation = Quaternion.LookRotation(dir);
            return;
        }

        if (type == SummonType.GhostSkull && totalCount > 1)
        {
            float angle = index * (360f / totalCount);
            float clusterRadius = Mathf.Max(0.1f, summonAbilityConfig.ghostSkull.spawnClusterRadius);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * flatForward;
            spawnPosition = basePosition + dir * clusterRadius;
            spawnRotation = Quaternion.LookRotation(dir);
            return;
        }

        if (formationRadius > 0.001f)
            offset = right * ((index - (totalCount - 1) * 0.5f) * formationRadius);

        spawnPosition = center + offset;
        spawnRotation = Quaternion.LookRotation(flatForward);

        if (summonPoints != null && summonPoints.Length > 0 && type != SummonType.Ghoul && type != SummonType.GhostSkull)
        {
            Transform fallbackPoint = GetSummonPoint(index);
            if (fallbackPoint != null && (spawnPosition - basePosition).sqrMagnitude < 0.0001f)
            {
                spawnPosition = fallbackPoint.position;
                spawnRotation = fallbackPoint.rotation;
            }
        }
    }

    private GameObject GetPrefabByType(SummonType type)
    {
        switch (type)
        {
            case SummonType.Ghoul: return ghoulPrefab;
            case SummonType.GhostDog: return ghostDogPrefab;
            case SummonType.Nun: return nunPrefab;
            case SummonType.SkullWarrior: return skullWarriorPrefab;
            case SummonType.GhostSkull: return ghostSkullPrefab;
            case SummonType.Pagan: return paganPrefab;
            default: return null;
        }
    }
    #endregion
    #region SpecialSkill 
   
    private void EndSwordRain()
    {
        if (activeSwordRainEffect != null)
        {
            Destroy(activeSwordRainEffect);
            activeSwordRainEffect = null;
        }
        swordRainCollider.radius = 0f;
        swordRainCollider.enabled = false;
        SkillManager.instance.GetSkillData(SkillType.SwordRain).isActive = false;
        playerCombat.SetIsAttacking(false);
        playerController.animator.Play("Idle", 0, 0f);
    }
    #endregion
    public void ShieldBreak()
    {
        if(shieldobj != null)
        EffectPoolManager.Instance.ReturnToPoolDirect(SkillManager.instance.GetSkillData(SkillType.ShieldBuff).skilIPrefab, shieldobj);
        InGameUI.Instance.ForceRemoveBuff(SkillType.ShieldBuff);
    }
    public void CounterDashAttack(Transform enemy)
    {
        StartCoroutine(CounterDashAttackCor(enemy));
    }
    private IEnumerator CounterDashAttackCor(Transform enemy)
    {
        CounterDash skill = SkillManager.instance.GetSkillData(SkillType.CounterDash) as CounterDash;
        
        skill.isActive = false;
        
        if (PlayerInfo.Instance.CheckLearnedPassive(PassiveSkillType.DoubleHit))
            StartCoroutine(DoubleCounterDash(enemy));

        playerController.animator.SetTrigger("CounterDash");
        PlayerSoundManager.PlaySound("CounterDash");
        StartCoroutine(ISSkill(YieldInstructionCache.GetWait(skill.counterDashDuration)));

        // 1) 올바른 방향 계산
        Vector3 dir = enemy.position - transform.position;
        dir.y = 0;

        // 2) 올바르게 회전
        transform.rotation = Quaternion.LookRotation(dir);

        // 3) 대쉬
        playerCombat.DashSlash(skill.counterDashLenght,
                                   skill.counterDashDuration,
                                   dir.normalized);

        // 4) 이펙트 생성 (이제 방향 안 틀림)
        GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab,
                                        counterDashSpawnPoint.position,
                                        counterDashSpawnPoint.rotation);


        // 5) 공격 판정
        counterDashCollider.enabled = true;
        yield return YieldInstructionCache.GetWait(skill.counterDashDuration);
        counterDashCollider.enabled = false;
    }
    public void CounterReflectionAttack(GameObject enemy)
    {
        SkillData skill = SkillManager.instance.GetSkillData(SkillType.CounterReflection);

        EnemyClass ie = enemy.GetComponent<EnemyClass>();
        Vector3 dir = (enemy.transform.position + Vector3.up * 2.5f
                      - counterReflectionAttackSpawnPoint.position).normalized;

        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -90f, 0);

        GameObject effect = EffectPoolManager.Instance.GetFromPool(
            skill.skilIPrefab,
            counterReflectionAttackSpawnPoint.position,
            rot
        );
        PlayerSoundManager.PlaySound("CounterReflection_Attack");
        effect.GetComponent<CounterReflection_Projectile>().target = enemy.transform;
        ie.SkillDamaged(SkillType.CounterReflection);
    }
  /*  #region ShadowRecall
    private bool isRecording = false;
    private bool isRewinding = false;
    private float recordTimer = 0f;
    private List<Vector3> positionHistory = new List<Vector3>();
    private Coroutine recordCoroutine;

    public void ShadowRecallEnd()
    {
        if(recordCoroutine !=null)
        StopCoroutine(recordCoroutine);
        StartCoroutine(Rewind());
    }
    private void ShadowRecall()
    {
        positionHistory.Clear();
        isRecording = true;
        recordTimer = 0f;
        recordCoroutine = StartCoroutine(RecordPositions());
    }

    private IEnumerator RecordPositions()
    {
        SkillData skill = skillManager.GetSkillData(SkillType.ShadowRecall);

        while (recordTimer < skill.shadowRecallrecordDuration)
        {
            positionHistory.Add(transform.position);
            recordTimer += skill.shadowRecallrecordInterval;
            yield return new WaitForSeconds(skill.shadowRecallrecordInterval);
        }

        // 자동 종료 후 되감기
        StartCoroutine(Rewind());
    }

    private IEnumerator Rewind()
    {
        SkillData skill = skillManager.GetSkillData(SkillType.ShadowRecall);

        if (positionHistory == null || positionHistory.Count == 0)
            yield break; // 기록이 없으면 바로 종료

        isRecording = false;
        isRewinding = true;
        playerCombat.SetIsSkill(true);
        IgnoreColision(true);

        // Rigidbody 이동을 위한 kinematic 설정
        rigidBody.isKinematic = true;
        rigidBody.velocity = Vector3.zero;
        playerMovement.Initialize();

        for (int i = positionHistory.Count - 1; i >= 0; i--)
        {
            Vector3 targetPos = positionHistory[i];
            while (Vector3.Distance(transform.position, targetPos) > 0.05f)
            {
                Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPos, skill.shadowRecallrewindSpeed * Time.fixedDeltaTime);
                rigidBody.MovePosition(nextPos);  // transform.position 대신 사용
                yield return new WaitForFixedUpdate();
            }
        }

        rigidBody.isKinematic = false;
        positionHistory.Clear();
        playerCombat.SetIsSkill(false);
        recordCoroutine = null;
        IgnoreColision(false);
        isRewinding = false;
    }
    #endregion*/
    public HashSet<EnemyClass> ImpulseStunnedEnemies = new HashSet<EnemyClass>();
    public HashSet<EnemyClass> waveStunnedEnemies = new HashSet<EnemyClass>();
    public HashSet<EnemyClass> astunnedEnemies = new HashSet<EnemyClass>();
    public void StunAndDamage(EnemyClass enemy, SkillType skillType, float duration, int damage)
    {
        enemy.SkillDamaged(skillType);
        enemy.Stun(duration);
        // 필요 시 스턴 종료 후 추가 로직
    }

    #region DoubleHit

    public void ExecuteDoubleHit(SkillData data)
    {
        if (this == null || !isActiveAndEnabled)
        {
            Debug.LogWarning("[ExecuteDoubleHit] PlayerSkill is invalid or inactive");
            return;
        }

        if (data == null)
        {
            Debug.LogWarning("[ExecuteDoubleHit] SkillData is null");
            return;
        }

        StartCoroutine(DoubleSkillExcute(data));
    }
    // 자식 타입(T)으로 안전하게 변환해서 코루틴을 실행해주는 도우미
    private void RunDoubleSkill<T>(SkillType type, System.Func<T, IEnumerator> routine) where T : SkillData
    {
        T data = SkillManager.instance.GetSkillData(type) as T;
        if (data != null)
        {
            Debug.Log(data.skillType);
            StartCoroutine(routine(data));
        }
    }
    IEnumerator DoubleSkillExcute(SkillData data)
    {
        switch (data.skillType)
        {
            case SkillType.RoundSlash:
                yield return YieldInstructionCache.GetWait(0.25f);
                // RunDoubleSkill<자식타입>(데이터타입, 실행할코루틴)
                RunDoubleSkill<RoundSlash_Double>(SkillType.RoundSlash_Double, DoubleRoundSlash);
                break;

            case SkillType.BackstepSpin:
                yield return YieldInstructionCache.GetWait(0.12f);
                RunDoubleSkill<BackstepSpin>(SkillType.BackstepSpin, DoubleBackStepSpin);
                break;

            case SkillType.BlinkStrike:
                yield return YieldInstructionCache.GetWait(0.35f);
                RunDoubleSkill<BlinkStrike>(SkillType.BlinkStrike, DoubleBlinkStrike);
                break;

            case SkillType.CrossSlash:
                yield return YieldInstructionCache.GetWait(0.45f);
                RunDoubleSkill<CrossSlash>(SkillType.CrossSlash, DoubleCrossSlash);
                break;
            case SkillType.DashSlash:
                RunDoubleSkill<DashSlash>(SkillType.DashSlash, DoubleDashSlash);
                break;

            case SkillType.ImpulseStun:
                yield return YieldInstructionCache.GetWait(0.35f);
                RunDoubleSkill<ImpulseStun>(SkillType.ImpulseStun, DoubleImpulseStun);
                break;

            case SkillType.JumpLanding:
                yield return YieldInstructionCache.GetWait(0.62f);
                RunDoubleSkill<JumpLanding>(SkillType.JumpLanding, DoubleJumpLanding);
                break;

            case SkillType.Slash:
                yield return new WaitForSeconds(SkillManager.instance.GetSkillData(SkillType.Slash).cooldown - 0.3f);
                playerCombat.Attack();
                break;

            case SkillType.SwordCrash:
                yield return YieldInstructionCache.GetWait(0.65f);
                RunDoubleSkill<SwordCrash>(SkillType.SwordCrash, DoubleSwordCrash);
                break;

            case SkillType.PierceSlash:
                yield return YieldInstructionCache.GetWait(0.64f);
                RunDoubleSkill<PierceSlash>(SkillType.PierceSlash, DoublePierceSlash);
                break;
        }

        // 공통으로 실행되는 이벤트는 switch 밖으로 빼면 중복 코드가 사라집니다!
        OnSkillUsed?.Invoke();
        yield return null;
    }

    private IEnumerator DoubleRoundSlash(RoundSlash_Double skill)
    {

        PlayerSoundManager.PlaySound("RoundSlash");

        // 2. 이펙트 생성 및 파괴 로직
        GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, roundSlashSpawnPoint.position, roundSlashSpawnPoint.rotation);
        // 파티클이면 재생 시간 기반으로 삭제


        // 3. 스킬 상태 제어
        StartCoroutine(ISSkill(YieldInstructionCache.GetWait(0.33f)));
        StartTimedCombatCollisionIgnore(0.22f);

        // 4. 콜라이더 설정 (자식인 RoundSlash에만 있는 roundSlashRadius 접근)
        double_roundSlashCollider.radius = skill.roundSlashRadius;
        double_roundSlashCollider.enabled = true;

        yield return YieldInstructionCache.GetWait(0.1f);

        double_roundSlashCollider.enabled = false;
    }
    private IEnumerator DoubleBackStepSpin(BackstepSpin skill)
    {
        if (SkillManager.instance.GetSkillData(SkillType.JumpLanding).isActive)
            yield break;
        skill.isActive = true;
        PlayerSoundManager.PlaySound("BackstepSpin");


        Transform spawnPos = backstepSpinSpawnPoint != null ? backstepSpinSpawnPoint : transform;

        GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, spawnPos.position, spawnPos.rotation);
            effect.transform.SetParent(spawnPos); // 따라다니도록 부모 설정

        SkillData_Colider hitCollider = double_backstepSpinCollider.GetComponent<SkillData_Colider>();
        hitCollider?.BeginHitWindow();
        double_backstepSpinCollider.enabled = true;
        yield return YieldInstructionCache.GetWait(skill.backstepSpinColliderDuration);
        double_backstepSpinCollider.enabled = false;
        hitCollider?.EndHitWindow();
        skill.isActive = false;
    }
    private IEnumerator DoubleImpulseStun(ImpulseStun skill)
    {

        PlayerSoundManager.PlaySound("ImpulseStun");

        StartCoroutine(ISSkill(YieldInstructionCache.GetWait(0.7f)));
        StartTimedCombatCollisionIgnore(0.7f);

            Transform spawnPos = impulseStunSpawnPoint != null ? impulseStunSpawnPoint : transform;
            GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, spawnPos.position, spawnPos.rotation);
            effect .transform.SetParent(spawnPos);

        Collider[] hits = Physics.OverlapSphere(transform.position, skill.impulseStunRange / 2);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy") && !hit.CompareTag("BloodDrop"))
                continue;

            EnemyClass enemy = hit.GetComponentInParent<EnemyClass>();
            if (enemy != null && !ImpulseStunnedEnemies.Contains(enemy))
            {
                ImpulseStunnedEnemies.Add(enemy); // 중복 방지
                StunAndDamage(enemy, SkillType.ImpulseStun_Double, skill.impulseStunDuration, skill.GetSkillDamaged());
            }
        }
        ImpulseStunnedEnemies.Clear();
        yield return null;
    }
    private IEnumerator DoublePierceSlash(PierceSlash skill)
    {
        if (SkillManager.instance.GetSkillData(SkillType.JumpLanding).isActive) yield break;

        // 시각적 효과 예시 (필요 시)

        GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, pierceSlashSpawnPoint.position, pierceSlashSpawnPoint.rotation);
        effect.transform.SetParent(null);
        // 파티클이면 재생 시간 기반으로 삭제

        StartCoroutine(ISSkill(YieldInstructionCache.GetWait(0.35f)));
        yield return YieldInstructionCache.GetWait(0.1f);
        double_PierceSlash.enabled = true;
        yield return YieldInstructionCache.GetWait(0.1f);
        double_PierceSlash.enabled = false;

    }
    private IEnumerator DoubleCounterDash(Transform enemy)
    {
        CounterDash skill = SkillManager.instance.GetSkillData(SkillType.CounterDash) as CounterDash;

        yield return YieldInstructionCache.GetWait(0.05f);

        PlayerSoundManager.PlaySound("CounterDash");

            // 1) 올바른 방향 계산
            Vector3 dir = enemy.position - transform.position;
            dir.y = 0;

            // 2) 올바르게 회전
            transform.rotation = Quaternion.LookRotation(dir);


            // 3) 이펙트 생성 (이제 방향 안 틀림)
            GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab,
                                            counterDashSpawnPoint.position,
                                            counterDashSpawnPoint.rotation);


            // 5) 공격 판정
            double_counterDashCollider.enabled = true;
        yield return YieldInstructionCache.GetWait(skill.counterDashDuration);
            double_counterDashCollider.enabled = false;
    }
    private IEnumerator DoubleJumpLanding(SkillData skill)
    {
        skill.isActive = true;
        PlayerSoundManager.PlaySound("JumpLanding");

        GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, jumpLandingSpawnPoint.position, jumpLandingSpawnPoint.rotation);

        double_jumpLandingCollider.enabled = true;
        yield return YieldInstructionCache.GetWait(0.15f);
        double_jumpLandingCollider.enabled = false;
        skill.isActive = false;
    }
    private IEnumerator DoubleBlinkStrike(BlinkStrike skill)
    {
        yield return null;
        GameObject target = playerTargetSystem.FindClosestEnemy(skill.blinkStrikeRange);
        // ---- 데미지 처리 ----
        EnemyClass enemy = target.gameObject.GetComponentInParent<EnemyClass>();
        if (enemy != null)
        {
            // ---- 이펙트 생성 ----
            GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, blinkStrikeSpawnPoint.position, blinkStrikeSpawnPoint.rotation);
            effect.transform.SetParent(blinkStrikeSpawnPoint);


            int finalDamage = Mathf.RoundToInt(PlayerInfo.Instance.FinalAttackDamage * skill.AttackDamage *0.5f);
            enemy.Damaged(finalDamage, false);
        }
    }
    private IEnumerator DoubleCrossSlash(CrossSlash skill)
    {
        Debug.Log(skill.skillType);
        if (SkillManager.instance.GetSkillData(SkillType.JumpLanding).isActive) yield break;

        // 시각적 효과 예시 (필요 시)
        PlayerSoundManager.PlaySound("CrossSlash");

        StartCoroutine(ISSkill(YieldInstructionCache.GetWait(0.5f)));


        Transform spawnPos = crossSlashSpawnPoint != null ? crossSlashSpawnPoint : transform;
        GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, spawnPos.position, spawnPos.rotation);
        

        double_crossSlash1.enabled = true;
        yield return YieldInstructionCache.GetWait(0.1f);
        double_crossSlash1.enabled = false;

        yield return YieldInstructionCache.GetWait(0.2f);

        double_crossSlash2.enabled = true;
        yield return YieldInstructionCache.GetWait(0.1f);
        double_crossSlash2.enabled = false;
    }
    private IEnumerator DoubleDashSlash(DashSlash skill)
    {

        if (SkillManager.instance.GetSkillData(SkillType.JumpLanding).isActive)
            yield break;

        skill.isActive = true;
        PlayerSoundManager.PlaySound("DashSlash");


        if (dashSlashCollider == null)
        {
            Debug.LogWarning("SkillTest: Q용 SphereCollider가 설정되지 않음.");
            yield break;
        }

        Transform spawnPos = dashSlashSpawnPoint != null ? dashSlashSpawnPoint : transform;

        GameObject effect = null;

            effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, spawnPos.position, spawnPos.rotation);
            effect.transform.SetParent(spawnPos); // 따라다니도록 부모 설정

        

        double_dashSlashCollider.enabled = true;
        ////Debug.Log("켜짐");
        yield return YieldInstructionCache.GetWait(skill.dashSlashColliderDuration);
        double_dashSlashCollider.enabled = false;
        ////Debug.Log("꺼짐");
        skill.isActive = false;
    }
    private IEnumerator DoubleSwordCrash(SwordCrash skill)
    {

        if (swordCrashCollider == null)
        {
            Debug.LogWarning("SkillTest: E용 SphereCollider가 설정되지 않음.");
            yield break;
        }

        PlayerSoundManager.PlaySound("SwordCrash");


            Transform spawnPos = swordCrashSpawnPoint != null ? swordCrashSpawnPoint : transform;
            GameObject effect = EffectPoolManager.Instance.GetFromPool(skill.skilIPrefab, spawnPos.position, spawnPos.rotation);

        

        double_swordCrashCollider.enabled = true;
        double_swordCrashCollider.radius = skill.burstRadius;

        yield return YieldInstructionCache.GetWait(skill.burstColliderDuration);

        double_swordCrashCollider.enabled = false;
        double_swordCrashCollider.radius = 0f;

    }
    #endregion
    public IEnumerator ISSkill(float time)
    {
        return ISSkillInternal(new WaitForSeconds(time));
    }

    public IEnumerator ISSkill(WaitForSeconds wait)
    {
        return ISSkillInternal(wait);
    }

    private IEnumerator ISSkillInternal(WaitForSeconds wait)
    {
        pendingActionEndRoutines++;
        playerCombat.SetIsSkill(true);
        playerMovement.Initialize();
        yield return wait;
        pendingActionEndRoutines = Mathf.Max(0, pendingActionEndRoutines - 1);
        playerCombat.SetIsSkill(false);
        playerController.StateMachine.CurrentState.OnActionEnd();
    }

    public Coroutine StartTimedCombatCollisionIgnore(float duration)
    {
        return StartCoroutine(TimedCombatCollisionIgnore(duration));
    }

    public int AcquireCombatCollisionIgnore()
    {
        int token = nextCombatCollisionIgnoreToken++;
        if (nextCombatCollisionIgnoreToken <= 0)
            nextCombatCollisionIgnoreToken = 1;

        if (combatCollisionIgnoreTokens.Add(token) && combatCollisionIgnoreTokens.Count == 1)
            SetCombatCollisionIgnored(true);

        return token;
    }

    public void ReleaseCombatCollisionIgnore(int token)
    {
        if (token == 0 || !combatCollisionIgnoreTokens.Remove(token))
            return;

        if (combatCollisionIgnoreTokens.Count == 0)
            SetCombatCollisionIgnored(false);
    }

    public void ForceClearCombatCollisionIgnore()
    {
        combatCollisionIgnoreTokens.Clear();
        legacyCombatCollisionIgnoreToken = 0;
        SetCombatCollisionIgnored(false);
    }

    private IEnumerator TimedCombatCollisionIgnore(float duration)
    {
        int token = AcquireCombatCollisionIgnore();
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        yield return new WaitForFixedUpdate();
        ReleaseCombatCollisionIgnore(token);
    }

    public void ResetRuntimeSkillState()
    {
        StopAllCoroutines();
        pendingActionEndRoutines = 0;
        summoning = false;

        DisableSkillCollider(slashComboCollider);
        DisableSkillCollider(slashBurstCollider);
        DisableSkillCollider(swordCrashCollider);
        DisableSkillCollider(swordRainCollider);
        DisableSkillCollider(dashSlashCollider);
        DisableSkillCollider(backstepSpinCollider);
        DisableSkillCollider(counterDashCollider);
        DisableSkillCollider(jumpLandingCollider);
        DisableSkillCollider(crossSlash1);
        DisableSkillCollider(crossSlash2);
        DisableSkillCollider(PierceSlash);
        DisableSkillCollider(roundSlashCollider);
        DisableSkillCollider(double_swordCrashCollider);
        DisableSkillCollider(double_dashSlashCollider);
        DisableSkillCollider(double_backstepSpinCollider);
        DisableSkillCollider(double_counterDashCollider);
        DisableSkillCollider(double_jumpLandingCollider);
        DisableSkillCollider(double_crossSlash1);
        DisableSkillCollider(double_crossSlash2);
        DisableSkillCollider(double_PierceSlash);
        DisableSkillCollider(double_roundSlashCollider);

        if (swordRainCollider != null)
            swordRainCollider.radius = 0f;

        if (double_swordCrashCollider != null)
            double_swordCrashCollider.radius = 0f;

        if (activeSwordRainEffect != null)
        {
            Destroy(activeSwordRainEffect);
            activeSwordRainEffect = null;
        }

        if (PlayerInfo.Instance != null)
        {
            PlayerInfo.Instance.SetAttackModifier("AttackBuff", 0f);
            if (PlayerInfo.Instance.shield > 0)
                PlayerInfo.Instance.ShieldChange(-PlayerInfo.Instance.shield);
        }

        if (shieldobj != null)
        {
            SkillData shieldData = SkillManager.instance?.GetSkillData(SkillType.ShieldBuff);
            if (EffectPoolManager.Instance != null && shieldData != null && shieldData.skilIPrefab != null)
                EffectPoolManager.Instance.ReturnToPoolDirect(shieldData.skilIPrefab, shieldobj);
            else
                shieldobj.SetActive(false);

            shieldobj = null;
        }

        if (InGameUI.Instance != null)
        {
            InGameUI.Instance.ForceRemoveBuff(SkillType.AttackBuff);
            InGameUI.Instance.ForceRemoveBuff(SkillType.ShieldBuff);
            InGameUI.Instance.ForceRemoveBuff(SkillType.AdditionalHitBuff);
        }

        if (rigidBody != null)
        {
            rigidBody.isKinematic = false;
            rigidBody.velocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
        }

        ForceClearCombatCollisionIgnore();
        SkillManager.instance?.ResetRuntimeSkillStates();
    }

    private static void DisableSkillCollider(Collider skillCollider)
    {
        if (skillCollider == null)
            return;

        skillCollider.enabled = false;
        skillCollider.GetComponent<SkillData_Colider>()?.EndHitWindow();
    }

    public IEnumerator MoveBlinkStrike(GameObject target, float speed)
    {
        if (target == null || rigidBody == null)
            yield break;

        Vector3 startPos = rigidBody.position;
        Vector3 endPos = GetBlinkStrikeDestination(target, startPos);
        Vector3 lookDir = endPos - startPos;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        Vector3 velocity = rigidBody.velocity;
        velocity.y = 0f;
        rigidBody.velocity = velocity;

        float distance = Vector3.Distance(startPos, endPos);
        float travelTime = Mathf.Clamp(distance / Mathf.Max(0.01f, speed), 0.05f, 0.3f);
        float elapsed = 0f;

        int collisionIgnoreToken = AcquireCombatCollisionIgnore();
        try
        {
            while (elapsed < travelTime)
            {
                float t = Mathf.Clamp01(elapsed / travelTime);
                Vector3 nextPos = Vector3.Lerp(startPos, endPos, t);
                nextPos.y = startPos.y;
                rigidBody.MovePosition(nextPos);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            endPos.y = startPos.y;
            rigidBody.MovePosition(endPos);
            yield return new WaitForFixedUpdate();

            velocity = rigidBody.velocity;
            velocity.y = 0f;
            rigidBody.velocity = velocity;
        }
        finally
        {
            ReleaseCombatCollisionIgnore(collisionIgnoreToken);
        }
    }

    private Vector3 GetBlinkStrikeDestination(GameObject target, Vector3 startPos)
    {
        EnemyClass targetEnemy = target.GetComponentInParent<EnemyClass>();
        GameObject targetRoot = targetEnemy != null ? targetEnemy.gameObject : target;
        Vector3 targetCenter = targetRoot.transform.position;
        Vector3 approachDir = targetCenter - startPos;
        approachDir.y = 0f;

        if (approachDir.sqrMagnitude <= 0.0001f)
            return startPos;

        approachDir.Normalize();
        Vector3 closestPoint = targetCenter;
        float closestDistance = float.MaxValue;
        Collider[] targetColliders = targetRoot.GetComponentsInChildren<Collider>();

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider targetCollider = targetColliders[i];
            if (targetCollider == null || !targetCollider.enabled || targetCollider.isTrigger)
                continue;

            Vector3 point = targetCollider.ClosestPoint(startPos);
            float distance = (point - startPos).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        float playerClearance = 0.2f;
        Collider[] playerColliders = GetComponents<Collider>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider playerCollider = playerColliders[i];
            if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                continue;

            playerClearance = Mathf.Max(
                playerClearance,
                Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.z) + 0.2f);
        }

        Vector3 destination = closestPoint - approachDir * playerClearance;
        destination.y = startPos.y;
        return Vector3.Dot(destination - startPos, approachDir) > 0f ? destination : startPos;
    }

    public void IgnoreColision(bool value)
    {
        if (value)
        {
            if (legacyCombatCollisionIgnoreToken == 0)
                legacyCombatCollisionIgnoreToken = AcquireCombatCollisionIgnore();
            return;
        }

        ReleaseCombatCollisionIgnore(legacyCombatCollisionIgnoreToken);
        legacyCombatCollisionIgnoreToken = 0;
    }

    private static void SetCombatCollisionIgnored(bool value)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int summonLayer = LayerMask.NameToLayer("Summon");

        if (playerLayer < 0)
            return;

        if (enemyLayer >= 0)
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, value);

        if (summonLayer >= 0)
            Physics.IgnoreLayerCollision(playerLayer, summonLayer, value);
    }
}

