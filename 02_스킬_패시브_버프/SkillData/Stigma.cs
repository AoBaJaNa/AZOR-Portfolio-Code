using System;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;

public class Stigma : MonoBehaviour
{
    private const int OverlapBufferSize = 32;

    [HideInInspector] public StigmaConfigSO so;
    public int stigma_Stack = 0;
    private bool Active_Contagious_Sin = false;
    private bool Active_Abyssal_Elegy = false;
    private bool Active_Lord_of_Penance = false;

    private Coroutine durationCoroutine;
    private Coroutine maxStackLoopCoroutine;

    public EnemyClass enemyClass;
    private LayerMask targetMask;
    private Collider[] searchStigmas;
    private float Contagious_Sin_Ratio = 0;
    private float Lord_of_Penance_Ratio = 0;

    public Action TriggerOnMaxStack;
    private float maxStack_DamageMultiplier;
    private float Stack20Upper_DamageMultiplier;
    private float Stack10Upper_DamageMultiplier;
    private float Stack1Upper_DamageMultiplier;
    private PlayerPassiveController passiveController;
    public bool IsMaxStack => so != null && stigma_Stack >= so.maxStigma_Stack;
    private bool _CloseGateAfterMaxStack = true; // 문을 닫아야 하는 스킬이 있는지 체크용
    private bool Active_MaxStack = true; // 기본값을 true로 시작하는 게 안전합니다.

    private HashSet<PassiveSkillType> registeredMaxStackTriggers = new();
    private void Awake()
    {
        targetMask = LayerMask.GetMask("Enemy");
        enemyClass = GetComponent<EnemyClass>();
        passiveController = FindFirstObjectByType<PlayerPassiveController>();
        searchStigmas = new Collider[OverlapBufferSize];
    }

    private void OnDisable()
    {
        ResetStigma();
    }

    public void SetSOFile(StigmaConfigSO data)
    {
        if (so == null) so = data;
        SyncStigmaStatusUI();
    }

    public void AddStigmaStack(int count)
    {
        //  [방어막] 데이터 파일이 아직 안 들어왔다면 에러 방지를 위해 실행하지 않습니다.
        if (so == null) return;

        stigma_Stack = Mathf.Min(stigma_Stack + count, so.maxStigma_Stack);
        Debug.Log($"[PassiveDebug][Stigma] {gameObject.name} stack +{count} => {stigma_Stack}/{so.maxStigma_Stack}");

        if (durationCoroutine != null)
        {
            StopCoroutine(durationCoroutine);
        }

        PassiveUpdate();
        durationCoroutine = StartCoroutine(StackDurationLoop());
    }

    public void AddTriggerOnMaxStack(PassiveSkillType type, Action action, bool CloseMaxStackLoop = true)
    {
        if (action == null) return;

        if (registeredMaxStackTriggers.Contains(type))
        {
            if (!CloseMaxStackLoop)
                _CloseGateAfterMaxStack = false;
            return;
        }

        registeredMaxStackTriggers.Add(type);
        Debug.Log($"[PassiveDebug][Stigma] {gameObject.name} max stack trigger registered: {type}");
        
        TriggerOnMaxStack -= action;
        TriggerOnMaxStack += action;

        if (!CloseMaxStackLoop)
            _CloseGateAfterMaxStack = false;
    }

    private void ExecuteOnMaxStack()
    {
        //PrintTriggerNames();
        Debug.Log($"[PassiveDebug][Stigma] {gameObject.name} reached max stack. Trigger count={registeredMaxStackTriggers.Count}");
        TriggerOnMaxStack?.Invoke();
        if (maxStackLoopCoroutine != null) StopCoroutine(maxStackLoopCoroutine);
        maxStackLoopCoroutine = StartCoroutine(MaxStackLoop());
    }
    public void PrintTriggerNames()
    {
        // 1. 만약 Action에 등록된 함수가 아예 없다면(null) 안내문 출력
        if (TriggerOnMaxStack == null)
        {
            Debug.LogWarning($"[디버그] {gameObject.name}의 TriggerOnMaxStack에 등록된 함수가 아무것도 없습니다 (null)");
            return;
        }

        // 2. Action 안에 조립된 함수 전표(Invocation List)를 통째로 가져옵니다.
        Delegate[] invocationList = TriggerOnMaxStack.GetInvocationList();

        Debug.LogWarning($"====== 🎯 [디버그: {gameObject.name}] 현재 등록된 함수 목록 (총 {invocationList.Length}개) ======");

        for (int i = 0; i < invocationList.Length; i++)
        {
            // 각 대리자가 가리키고 있는 실제 함수(Method) 정보 추출
            var methodInfo = invocationList[i].Method;

            // 함수가 소속된 클래스(스크립트) 이름과 실제 함수명을 가독성 있게 조립
            string className = methodInfo.DeclaringType != null ? methodInfo.DeclaringType.Name : "UnknownClass";
            string methodName = methodInfo.Name;

            Debug.Log($"   [{i}] 소속 클래스: {className} | ➡️ 함수명: {methodName}");
        }

        Debug.LogWarning("=============================================================");
    }
    private IEnumerator StackDurationLoop()
    {
        yield return new WaitForSeconds(so.stigma_Duration);
        stigma_Stack--;
        PassiveUpdate();

        while (stigma_Stack > 0)
        {
            yield return YieldInstructionCache.GetWait(0.5f);
            stigma_Stack--;
            PassiveUpdate();
        }

        durationCoroutine = null;
        ResetStigma(); //  스크립트를 파괴하지 않고 데이터만 리셋합니다!
    }
    public void UseStack(int count)
    {
        stigma_Stack = Mathf.Max(stigma_Stack - count, 0);
        SyncStigmaStatusUI();
    }
    private IEnumerator MaxStackLoop()
    {
        yield return YieldInstructionCache.GetWait(6f);
        Active_MaxStack = true; // 6초가 지나면 정직하게 오직 여기서만 문을 다시 열어줍니다.
        maxStackLoopCoroutine = null;

        if (IsMaxStack)
        {
            if (_CloseGateAfterMaxStack)
            {
                Active_MaxStack = false;
            }
            ExecuteOnMaxStack();
        }
    }
    public void PassiveUpdate()
    {
        
        if (so == null) return;


        // 최대 스택에 정확히 도달했을 때
        if (IsMaxStack)
        {
            stigma_Stack = so.maxStigma_Stack; // 오버플로우 방지 고정

            Debug.Log($"[PassiveDebug][Stigma] {gameObject.name} is at max stack {stigma_Stack}");

            if(Active_Lord_of_Penance)
                Lord_of_Penance();

            if (Active_MaxStack)
            {
                Active_MaxStack = false; //문 닫기
                ExecuteOnMaxStack();
            }
        }
        else
        {
            if (maxStackLoopCoroutine == null)
            {
                Active_MaxStack = true;
            }
        }

        if (enemyClass == null) return;

        float reductionAmount = Echoes_Of_Agony_Multiplier * stigma_Stack;
        enemyClass.SetDefenceModifier("EchoesOfAgony", reductionAmount);
        SyncStigmaStatusUI();
    }

    private float Echoes_Of_Agony_Multiplier;
    public void Echoes_Of_Agony(float multiplier)
    {
        Echoes_Of_Agony_Multiplier = multiplier;
    }

    // [개조] 스크립트를 파괴(Destroy)하는 대신 값만 0으로 비워두고 재사용합니다.
    public void ResetStigma()
    {
        Debug.Log($"[PassiveDebug][Stigma] ResetStigma {gameObject.name}");
        if (durationCoroutine != null) { StopCoroutine(durationCoroutine); durationCoroutine = null; }
        if (maxStackLoopCoroutine != null) { StopCoroutine(maxStackLoopCoroutine); maxStackLoopCoroutine = null; }
       
        registeredMaxStackTriggers.Clear();
        TriggerOnMaxStack = null; // 람다식 이벤트 누수 방지

        if (enemyClass != null)
        {
            enemyClass.SetDefenceModifier("EchoesOfAgony", 0f);
        }
        stigma_Stack = 0;
        _CloseGateAfterMaxStack = true;
        Active_MaxStack = true;
        Active_Lord_of_Penance = false;
        Active_Abyssal_Elegy = false;
        Active_Contagious_Sin = false;
        SyncStigmaStatusUI();
    }

    // 완전히 꺼질 때(오브젝트가 죽거나 바뀔 때) 완벽한 청소용 함수


    public void Cruel_Engraving(int threshold, int stack)
    {
        if (stigma_Stack >= threshold)
        {
            AddStigmaStack(stack);
        }
    }

    public void ActiveContagious_Sin(float rate)
    {
        if (!Active_Contagious_Sin)
        {
            Active_Contagious_Sin = true;
            Contagious_Sin_Ratio = (rate / 100f);
        }
    }

    public void Contagious_Sin()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, 7f, searchStigmas, targetMask);
        Debug.Log($"[PassiveDebug][Stigma] Contagious_Sin from {gameObject.name}, nearby targets={hitCount}, sourceStack={stigma_Stack}");

        for (int i = 0; i < hitCount; i++)
        {
            Collider enemy = searchStigmas[i];
            if (enemy == null) continue;
            Stigma stigma = enemy.gameObject.GetOrAddComponent<Stigma>();
            stigma.SetSOFile(so);
            stigma.ActiveContagious_Sin(Contagious_Sin_Ratio * 100);
            PlayTransferTrail(stigma.transform.position);
            stigma.AddStigmaStack(Mathf.RoundToInt(stigma_Stack * Contagious_Sin_Ratio));
        }
    }
    public void ActiveLord_of_Penance(float ratio)
    {
        if (!Active_Lord_of_Penance)
        {
            Active_Lord_of_Penance = true;
            Lord_of_Penance_Ratio = ratio / 100;
        }
    }
    public void Lord_of_Penance()
    {
        UseStack(stigma_Stack);
        int DamageValue = Mathf.RoundToInt(PlayerInfo.Instance.FinalAttackDamage * Lord_of_Penance_Ratio);
        Debug.Log($"[PassiveDebug][Stigma] Lord_of_Penance triggered on {gameObject.name}, damage={DamageValue}");
        enemyClass.TakeDamage(DamageValue, PlayerInfo.Instance.GetFinalCriticalChance());
    }
    public void ActiveAbyssal_Elegy(float max, float stack20, float stack10, float stack1)
    {
        if (!Active_Abyssal_Elegy)
        {
            maxStack_DamageMultiplier = max/100;
            Stack20Upper_DamageMultiplier = stack20/100;
            Stack10Upper_DamageMultiplier = stack10 / 100;
            Stack1Upper_DamageMultiplier = stack1 / 100;
            Active_Abyssal_Elegy = true;
        }
    }
    public void Abyssal_Elegy()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, 7f, searchStigmas, targetMask);
        Debug.Log($"[PassiveDebug][Stigma] Abyssal_Elegy from {gameObject.name}, nearby targets={hitCount}, currentStack={stigma_Stack}");
        for (int i = 0; i < hitCount; i++)
        {
            Collider enemy = searchStigmas[i];
            if (enemy == null) continue;
            EnemyClass em = enemy.gameObject.GetComponent<EnemyClass>();
            if (em == null) continue;
            if (stigma_Stack >= so.maxStigma_Stack)
            {
                em.TakeDamage(Mathf.RoundToInt(PlayerInfo.Instance.FinalAttackDamage * maxStack_DamageMultiplier), PlayerInfo.Instance.GetFinalCriticalChance());
            }
            else if (stigma_Stack >= 20)
            {
            em.TakeDamage(Mathf.RoundToInt(PlayerInfo.Instance.FinalAttackDamage * Stack20Upper_DamageMultiplier), PlayerInfo.Instance.GetFinalCriticalChance());
            }
            else if(stigma_Stack >= 10)
            {
                em.TakeDamage(Mathf.RoundToInt(PlayerInfo.Instance.FinalAttackDamage * Stack10Upper_DamageMultiplier), PlayerInfo.Instance.GetFinalCriticalChance());
            }
            else if (stigma_Stack >= 1)
            {
                em.TakeDamage(Mathf.RoundToInt(PlayerInfo.Instance.FinalAttackDamage * Stack1Upper_DamageMultiplier), PlayerInfo.Instance.GetFinalCriticalChance());
            }
        }
    }
    public void StigmaHandleDeath()
    {
        Debug.Log($"[PassiveDebug][Stigma] {gameObject.name} died with stack={stigma_Stack}, contagious={Active_Contagious_Sin}, elegy={Active_Abyssal_Elegy}");
        Active_MaxStack = false;
        TriggerOnMaxStack = null; // 대리자 비우기
        if (Active_Contagious_Sin)
            Contagious_Sin();

        if (Active_Abyssal_Elegy)
            Abyssal_Elegy();

        ResetStigma(); // 사망 시 깔끔하게 로그 및 이벤트 정리
    }

    public void ReleaseStackIndicatorVisualOnly()
    {
        enemyClass?.enemyStatusController?.RemoveStatus(EnemyStatusKey.Stigma);
    }

    private void SyncStigmaStatusUI()
    {
        if (so == null || enemyClass == null || enemyClass.enemyStatusController == null)
            return;

        if (passiveController == null)
            passiveController = FindFirstObjectByType<PlayerPassiveController>();

        Sprite stigmaIcon = so.stigmaStatusIcon;
        if (stigmaIcon == null && passiveController != null)
            stigmaIcon = passiveController.GetStigmaStatusIcon();

        enemyClass.enemyStatusController.SetPersistentStatus(
            EnemyStatusKey.Stigma,
            stigma_Stack > 0,
            stigmaIcon,
            true,
            stigma_Stack,
            "낙인");
    }

    public void PlayTransferTrail(Vector3 targetPosition)
    {
        if (so == null)
            return;

        if (passiveController == null)
            passiveController = FindFirstObjectByType<PlayerPassiveController>();

        if (passiveController == null || !passiveController.TryGetStigmaTransferTrailPrefab(out GameObject effectPrefab))
            return;

        if (EffectPoolManager.Instance == null)
            return;

        Vector3 startPosition = transform.position + so.stackIndicatorOffset;
        Vector3 adjustedTargetPosition = targetPosition + so.stackIndicatorOffset;
        bool hasTrailController = effectPrefab.GetComponent<StigmaTransferTrailEffect>() != null;
        GameObject effectInstance = EffectPoolManager.Instance.GetFromPool(effectPrefab, startPosition, Quaternion.identity, !hasTrailController);
        if (effectInstance == null)
            return;

        StigmaTransferTrailEffect trailEffect = effectInstance.GetComponent<StigmaTransferTrailEffect>();
        if (trailEffect != null)
        {
            trailEffect.Play(effectPrefab, startPosition, adjustedTargetPosition, so.transferTrailDuration);
            return;
        }

        Vector3 direction = adjustedTargetPosition - startPosition;
        if (direction.sqrMagnitude > 0.0001f)
            effectInstance.transform.rotation = Quaternion.LookRotation(direction.normalized);
    }
}

