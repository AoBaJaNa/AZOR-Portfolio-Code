using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.VisualScripting;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
public enum PlayerHealthState
{
    Normal,       // 정상 (70% 초과)
    Injured,      // [부상] (70% 이하)
    Frenzy,       // [광분] (40% 이하)
    NearDeath     // [사선] (25% 이하)
}
public class PlayerPassiveController : MonoBehaviour
{
    //stigma Config
    public StigmaConfigSO stigmaConfigSO;
    private bool stigmaUpdate = false;
    //Berserker Config
    private readonly float injuredThreshold = 0.70f;
    private readonly float frenzyThreshold = 0.40f;
    private readonly float nearDeathThreshold = 0.25f;
    public PlayerHealthState CurrentHealthState { get; private set; } = PlayerHealthState.Normal;
    public event Action OnChangeHPState;
    private Dictionary<PassiveSkillData, Action> registeredBerserkerPassives = new();
    
    
    private Dictionary<PassiveSkillData, Action> registeredOnEnemyDeadPassives = new();
    public event Action OnEnemyDead;
    public event Action<EnemyClass> OnEnemyDeadWithContext;
    private Dictionary<PassiveSkillData, Action> registeredOnSkillPassives = new();
    private Dictionary<PassiveSkillData, Action> registeredOnPlayerDamagedPassives = new();
    public event Action OnPlayerDamaged;
    private Dictionary<PassiveSkillData, Action> registeredOnStageResetPassives = new();
    public event Action OnStageReset;

    PlayerController playerController;
    List<PassiveSkillData> activePassives;
    public HashSet<PassiveSkillBuildType> HasPassiveBuildTypes { get; private set; } = new();
    private Dictionary<PassiveSkillData, int> registeredStigmaStackPassives = new Dictionary<PassiveSkillData, int>();
    private readonly HashSet<EnemyClass> stigmaAppliedEnemiesThisAttack = new HashSet<EnemyClass>();
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

    private PlayerEffectManager playerEffectManager;
    private Collider[] searchStigmas;
    private Stigma[] detectedStigmas;
    public LayerMask targetMask;
    private readonly float checkInterval = 0.2f; // 0.1초에 한 번만 물리/컴포넌트 탐지 진행
    private float currentTimer = 0f;
    private int cachedTotalStigmaStackCount = 0;
    public float StaticAttackMultiplier { get; private set; } = 0;
    public float StaticMaxHPMultiplier { get; private set; } = 0;
    public float StaticDefenceMultiplier { get; private set; } = 0;
    public float StaticCriticalMultiplier { get; private set; } = 0;
    private AsyncOperationHandle<GameObject> stigmaTransferTrailHandle;
    private bool hasStigmaTransferTrailHandle;
    private Coroutine stigmaEffectLoadCoroutine;
    private GameObject loadedTransferTrailPrefab;
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerEffectManager = GetComponent<PlayerEffectManager>();
    }
    private void Start()
    {
        targetMask = LayerMask.GetMask("Enemy");
        searchStigmas = new Collider[30];
        detectedStigmas = new Stigma[30];
        activePassives = playerController.passiveSkillManager.activePassives;

        PassiveSkillManager.OnChangedPassive += GetStaticAttackMultiplier;
        PassiveSkillManager.OnChangedPassive += GetStaticMaxHPMultiplier;
        PassiveSkillManager.OnChangedPassive += GetStaticDefenceMultiplier;
        PassiveSkillManager.OnChangedPassive += UpdateStigmaStack;
        PlayerInfo.OnChangeStatus += UpdateHealthState;
    }
    private void OnDestroy()
    {
        PassiveSkillManager.OnChangedPassive -= GetStaticAttackMultiplier;
        PassiveSkillManager.OnChangedPassive -= GetStaticMaxHPMultiplier;
        PassiveSkillManager.OnChangedPassive -= GetStaticDefenceMultiplier;
        PassiveSkillManager.OnChangedPassive -= UpdateStigmaStack;
        PlayerInfo.OnChangeStatus -= UpdateHealthState;
        ReleaseStigmaEffectAssets();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            Inventory.Instance.AddSummonStone(9999);
            Inventory.Instance.SkillGemChange(9999);
            Inventory.Instance.AddWeaponAbilityStone(WeaponAbilityType.Hp, WeaponAbilityValues.Value5);
            Inventory.Instance.AddWeaponAbilityStone(WeaponAbilityType.Hp, WeaponAbilityValues.Value10);
            Inventory.Instance.AddWeaponAbilityStone(WeaponAbilityType.Hp, WeaponAbilityValues.Value15);
            Inventory.Instance.AddWeaponAbilityStone(WeaponAbilityType.Hp, WeaponAbilityValues.Value20);
            Inventory.Instance.AddSummonType(SummonType.GhostDog);
            Inventory.Instance.AddSummonType(SummonType.Pagan);
            Inventory.Instance.AddSummonType(SummonType.Nun);
            Inventory.Instance.AddSummonType(SummonType.GhostSkull);
            Inventory.Instance.AddSummonType(SummonType.Ghoul);
            Inventory.Instance.AddSummonType(SummonType.SkullWarrior);
            //playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Contagious_Sin));
            playerController.passiveSkillManager.PassiveSelect();
            /* playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Corrupted_Shroud));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Echoes_Of_Agony));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Hound_Pursuit));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Cruel_Engraving));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Infected_Burst));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Abyssal_Hook));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Lord_of_Penance));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Soul_Devourer));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Stigma_Abyssal_Elegy));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Scent_of_Blood));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Stubborn_Survival));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Boiling_Veins));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Crimson_Recoil));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Overload_Eruption));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Wounded_Lion));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Blood_Feast));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Risk_Awakening));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Blood_Pact));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Deaths_Threshold));
             playerController.passiveSkillManager.LearnSkill(playerController.passiveSkillManager.GetPassiveData(PassiveSkillType.Berserker_Heart_of_Slaughter));*/

        }

        if (activePassives.Count > 0)
            foreach (var pair in activePassives)
                pair.OnUpdate(playerController);

        if (stigmaUpdate)
        {
            // 타이머 누적
            currentTimer += Time.deltaTime;

            // 설정한 checkInterval(0.25초)이 지나지 않았다면 리턴
            if (currentTimer < checkInterval) return;

            // 타이머 초기화
            currentTimer = 0f;

            int hit = Physics.OverlapSphereNonAlloc(playerController.transform.position, 7f, searchStigmas, targetMask);

            int stigmaTarget = 0;

            for (int i = 0; i < hit; i++)
            {
                if (searchStigmas[i] == null)
                {
                    continue;
                }


                if (searchStigmas[i].TryGetComponent<Stigma>(out var stigma))
                {
                    if (stigma.stigma_Stack > 0)
                    {
                        detectedStigmas[stigmaTarget] = stigma;
                        stigmaTarget++;
                    }
                }
                // 사용 완료 후 방 비워주기
                searchStigmas[i] = null;
            }

            foreach (var pair in activePassives)
                pair.StigmaSearch(stigmaTarget, detectedStigmas, playerController);
            
            for (int i = 0; i < stigmaTarget; i++)
            {
                detectedStigmas[i] = null;
            }
        }
    }
    public void UpdateHealthState()
    {
        if(!HasPassiveBuildTypes.Contains(PassiveSkillBuildType.Berserker))
            return;

        PlayerHealthState previousState = CurrentHealthState;

        float hpRatio = PlayerInfo.Instance.currentHP / (float)PlayerInfo.Instance.FinalMaxHP;

        if (hpRatio <= nearDeathThreshold)
        {
            CurrentHealthState = PlayerHealthState.NearDeath;
        }
        else if (hpRatio <= frenzyThreshold)
        {
            CurrentHealthState = PlayerHealthState.Frenzy;
        }
        else if (hpRatio <= injuredThreshold)
        {
            CurrentHealthState = PlayerHealthState.Injured;
        }
        else
        {
            CurrentHealthState = PlayerHealthState.Normal;
        }
        if (previousState != CurrentHealthState)
            OnChangeHPState?.Invoke();
    }
    public void GetStaticAttackMultiplier()
    {
        StaticAttackMultiplier = 0f;

        foreach (var passive in activePassives)
        {
            // 각 패시브 데이터에 적힌 증가 수치(0.05, 0.1 등)를 다 더함
            StaticAttackMultiplier += passive.GetAttackMultiplier();
        }
    }
    public void GetStaticMaxHPMultiplier()
    {
        StaticMaxHPMultiplier = 0f;

        foreach (var passive in activePassives)
        {
            // 각 패시브 데이터에 적힌 증가 수치(0.05, 0.1 등)를 다 더함
            StaticMaxHPMultiplier += passive.GetHPMultiplier();
        }
    }
    public void GetStaticDefenceMultiplier()
    {
        StaticDefenceMultiplier = 0f;

        foreach (var passive in activePassives)
        {
            // 각 패시브 데이터에 적힌 증가 수치(0.05, 0.1 등)를 다 더함
            StaticDefenceMultiplier += passive.GetDefenceMultiplier();
        }
    }
    public void GetStaticCriticalMultiplier()
    {
        StaticCriticalMultiplier = 0f;

        foreach (var passive in activePassives)
        {
            // 각 패시브 데이터에 적힌 증가 수치(0.05, 0.1 등)를 다 더함
            StaticCriticalMultiplier += passive.GetCriticalMultiplier();
        }
    }
    public void RegisterOnEnemyDeathAction(PassiveSkillData passive, Action action)
    {
        UnRegisterOnEnemyDeathAction(passive);

        registeredOnEnemyDeadPassives[passive] = action;
        OnEnemyDead += action;
    }
    public void UnRegisterOnEnemyDeathAction(PassiveSkillData passive)
    {
        if (registeredOnEnemyDeadPassives.TryGetValue(passive, out var action))
        {
            OnEnemyDead -= action;
            registeredOnEnemyDeadPassives.Remove(passive);
        }
    }
    public void RegisterOnPlayerDamagedAction(PassiveSkillData passive, Action action)
    {
        UnRegisterOnPlayerDamagedAction(passive);

        registeredOnPlayerDamagedPassives[passive] = action;
        OnPlayerDamaged += action;
    }
    public void UnRegisterOnPlayerDamagedAction(PassiveSkillData passive)
    {
        if (registeredOnPlayerDamagedPassives.TryGetValue(passive, out var action))
        {
            OnPlayerDamaged -= action;
            registeredOnPlayerDamagedPassives.Remove(passive);
        }
    }
    public void RegisterOnStageResetAction(PassiveSkillData passive, Action action)
    {
        UnRegisterOnStageResetAction(passive);

        registeredOnStageResetPassives[passive] = action;
        OnStageReset += action;
    }
    public void UnRegisterOnStageResetAction(PassiveSkillData passive)
    {
        if (registeredOnStageResetPassives.TryGetValue(passive, out var action))
        {
            OnStageReset -= action;
            registeredOnStageResetPassives.Remove(passive);
        }
    }
    public void InvokeOnenemyDeath()
    {
        OnEnemyDead?.Invoke();
    }
    public void InvokeOnEnemyDeath(EnemyClass enemy)
    {
        OnEnemyDead?.Invoke();
        OnEnemyDeadWithContext?.Invoke(enemy);
    }
    public void InvokeOnStageReset()
    {
        OnStageReset?.Invoke();
    }
    public void InvokeOnPlayerDamaged()
    {
        OnPlayerDamaged?.Invoke();
    }
    public void HandleOnAttack(SkillData usedSkill)
    {
        ResetStigmaAttackWindow();
        foreach (var passive in activePassives)
        {
            passive.OnAttack(playerController, usedSkill);
        }
    }
    public void HandleEndAttack()
    {
        HashSet<EnemyClass> list = playerController.playerCombat.GetDamagedEnemyList();
        foreach (var passive in activePassives)
        {
            passive.EndAttack(playerController,list);
        }
        ResetStigmaAttackWindow();
    }

    public void ResetPassiveBuildState()
    {
        HasPassiveBuildTypes.Clear();
        registeredStigmaStackPassives.Clear();
        cachedTotalStigmaStackCount = 0;
        stigmaUpdate = false;
    }


    #region stigma관련
    public void SetStigmaSearch()
    {
        if(!stigmaUpdate)
        stigmaUpdate = true;
    }

    public void SyncStigmaEffectPreload()
    {
        bool shouldLoad = HasPassiveBuildTypes.Contains(PassiveSkillBuildType.Stigma);
        if (shouldLoad)
        {
            if (stigmaEffectLoadCoroutine == null &&
                loadedTransferTrailPrefab == null)
            {
                stigmaEffectLoadCoroutine = StartCoroutine(EnsureStigmaEffectAssetsLoaded());
            }
            return;
        }

        ReleaseStigmaEffectAssets();
    }

    private IEnumerator EnsureStigmaEffectAssetsLoaded()
    {
        if (stigmaConfigSO == null)
        {
            stigmaEffectLoadCoroutine = null;
            yield break;
        }

        if (stigmaConfigSO.transferTrailEffectRef != null &&
            stigmaConfigSO.transferTrailEffectRef.RuntimeKeyIsValid() &&
            loadedTransferTrailPrefab == null)
        {
            var handle = stigmaConfigSO.transferTrailEffectRef.LoadAssetAsync<GameObject>();
            stigmaTransferTrailHandle = handle;
            hasStigmaTransferTrailHandle = true;
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
                loadedTransferTrailPrefab = handle.Result;
        }

        stigmaEffectLoadCoroutine = null;
    }

    public void ReleaseStigmaEffectAssets()
    {
        if (stigmaEffectLoadCoroutine != null)
        {
            StopCoroutine(stigmaEffectLoadCoroutine);
            stigmaEffectLoadCoroutine = null;
        }

        if (hasStigmaTransferTrailHandle)
        {
            Addressables.Release(stigmaTransferTrailHandle);
            hasStigmaTransferTrailHandle = false;
        }

        loadedTransferTrailPrefab = null;
    }

    public bool TryGetStigmaTransferTrailPrefab(out GameObject prefab)
    {
        prefab = loadedTransferTrailPrefab;
        return prefab != null;
    }

    public Sprite GetStigmaStatusIcon()
    {
        if (stigmaConfigSO != null && stigmaConfigSO.stigmaStatusIcon != null)
            return stigmaConfigSO.stigmaStatusIcon;

        if (activePassives == null)
            return null;

        foreach (PassiveSkillData passive in activePassives)
        {
            if (passive != null &&
                passive.buildType == PassiveSkillBuildType.Stigma &&
                passive.skillIcon != null)
            {
                return passive.skillIcon;
            }
        }

        return null;
    }



    public void HandleStigmaAttack(EnemyClass enemy)
    {
        if (cachedTotalStigmaStackCount <= 0) return;
        if (enemy == null) return;
        if (stigmaAppliedEnemiesThisAttack.Contains(enemy)) return;

        Stigma st = enemy.gameObject.GetOrAddComponent<Stigma>();
        
        st.SetSOFile(stigmaConfigSO);
            
        foreach (var script in registeredStigmaStackPassives.Keys)
                script.StigmaLogicBeforeStackCount(st);

            st.AddStigmaStack(cachedTotalStigmaStackCount);
        stigmaAppliedEnemiesThisAttack.Add(enemy);
    }

    public void ResetStigmaAttackWindow()
    {
        stigmaAppliedEnemiesThisAttack.Clear();
    }

    public void RegisterStigmaStack(PassiveSkillData script, int value, PassiveSkillBuildType type)
    {
        if (script == null) return;
        if (value <= 0) return;

        registeredStigmaStackPassives[script] = value;
        if(!HasPassiveBuildTypes.Contains(type))
            HasPassiveBuildTypes.Add(type);
    }
    public void UnRegisterStigmaStack(PassiveSkillData script)
    {
        if (script == null) return;

        registeredStigmaStackPassives.Remove(script);
    }
    public void UpdateStigmaStack()
    {
        cachedTotalStigmaStackCount = 0;
        foreach (var count in registeredStigmaStackPassives.Values)
        {
            cachedTotalStigmaStackCount += count;
        }

        if (cachedTotalStigmaStackCount <= 0)
        {
            registeredStigmaStackPassives.Clear();
        }
    }
    private IEnumerator AddStigmaStack()
    {
        yield return  _waitForEndOfFrame;
        var enemyList = playerController.playerCombat.GetDamagedEnemyList();

            foreach (var enemy in enemyList)
            {
            if (enemy == null) continue;
                Stigma st = enemy.gameObject.GetOrAddComponent<Stigma>();
                st.SetSOFile(stigmaConfigSO);
                foreach (var script in registeredStigmaStackPassives.Keys)
                    script.StigmaLogicBeforeStackCount(st);
                st.AddStigmaStack(cachedTotalStigmaStackCount);

        }
    }
    #endregion
    #region Berserker 관련

    public void RegisterBerserkerAction(PassiveSkillData passive, Action action)
    {
        UnRegisterBerserkerAction(passive);

        registeredBerserkerPassives[passive] = action;
        OnChangeHPState += action;
    }

    public void UnRegisterBerserkerAction(PassiveSkillData passive)
    {
        if (registeredBerserkerPassives.TryGetValue(passive, out var action))
        {
            OnChangeHPState -= action;
            registeredBerserkerPassives.Remove(passive);
        }
    }
    private bool passiveInvincible;
    private bool heartOfSlaughterEmpoweredSkillActive;

    public bool IsPassiveInvincible => passiveInvincible;
    public bool IsHeartOfSlaughterEmpoweredSkillActive => heartOfSlaughterEmpoweredSkillActive;

    public void SetInvincibleByPassive(bool value)
    {
        passiveInvincible = value;
    }
    public void SetHeartOfSlaughterEmpoweredSkill(bool value)
    {
        heartOfSlaughterEmpoweredSkillActive = value;
    }
    #endregion
}



