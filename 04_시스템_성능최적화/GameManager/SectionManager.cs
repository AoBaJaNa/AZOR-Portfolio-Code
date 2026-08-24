using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System;
using UnityEngine.Serialization;

public enum StageType
{
    Static = 0,
    Wave = 1,
    Lobby = 2,
    None = 3,
    Reward = 4
}

[System.Serializable]
public class StaticEnemySpawnPoint
{
    public Transform transform;
    [Tooltip("Spawn radius for this static enemy point.")]
    public float range = 10f;
    [Tooltip("Spawn weight for this point. Keep the total across points at 100.")]
    [Range(0, 100)]
    public int weight = 1;

    [HideInInspector] public int currentSpawnCount = 0;
}

[System.Serializable]
public struct CombatStageStatAdjustment
{
    [Range(1, 300)] public int hpPercent;
    [Range(1, 300)] public int damagePercent;
    [Range(1, 300)] public int defencePercent;
}

public class SectionManager : MonoBehaviour
{
    private const float StageClearNoticeDuration = 3f;
    private const float EliteHealthMultiplier = 1.65f;
    private const float EliteDamageMultiplier = 1.4f;
    private const float EliteDefenseMultiplier = 1.25f;

    public static event Action Reset;

    [Header("Common")]
    public StageType stageType;
    public GameObject blockObject;
    [Header("Lobby")]
    public GameObject enhancementStationObject;
    [Header("Wave Exit")]
    public GameObject wavePortalPrefab;
    [Header("Wave Combat Balance")]
    [FormerlySerializedAs("waveStageMultiplier")]
    [SerializeField] private CombatStageStatAdjustment waveStageReduction = new CombatStageStatAdjustment
    {
        hpPercent = 60,
        damagePercent = 80,
        defencePercent = 85
    };
    [Header("Debug")]
    [SerializeField] private bool logSpawnStatResolution;

    internal Transform spawnPos;
    internal EnemySpawnData enemySpawnData;
    internal StageTypeSO roomData;
    private StageLevelConfigSO runtimeLevelConfig;
    private StageRewardDataSO runtimeRewardData;
    private StageDropBalanceSO runtimeDropBalanceData;
    private int runtimeBattleLevel = -1;
    private int runtimeStaticEnemyCount;
    private StaticEnemySetting[] runtimeStaticEnemySettings = Array.Empty<StaticEnemySetting>();
    private WaveEnemySetting[] runtimeWaveEnemySettings = Array.Empty<WaveEnemySetting>();
    private GameObject spawnedWavePortal;
    private bool wavePortalSpawned;
    [Header("Static Stage")]
    public int SpawnPointCount;
    public List<StaticEnemySpawnPoint> spawnPoints = new List<StaticEnemySpawnPoint>();
    private int remainNumber = 0;
    private int staticStageStartLevel = 1;
    private int staticStageTargetExp = 0;
    private int staticStageAwardedExp = 0;
    private int staticStageExpEnemyCount = 0;
    private int staticStageExpBaseReward = 0;
    private int staticStageExpRemainder = 0;
    private int staticStageExpGrantCount = 0;
    private float safetyRadius = 1f;       

    private List<int> previousWeights = new List<int>();

    [Header("Wave Stage")]
    public float restDuration = 5f;
    public int totalSpawn = 0;
    public int kill = 0;
    public CheckPointSection Section => checkPoint != null ? checkPoint.section : CheckPointSection.Start;

    private bool waveRunning = false;
    private bool IsRewardRoom => stageType == StageType.Reward || (stageType != StageType.Lobby && roomData != null && roomData.roomType == RoomType.Reward);
    private bool isEliteCombat;

    StageManager stageManager;
    PassiveSkillManager passiveSkillManager;
    Transform player;
    PlayerPassiveController playerPassiveController;
    public CheckPoint checkPoint;
    int enemyLayerMask;
    private bool isStageClearProcessing;
    private void Start()
    {
        stageManager = FindFirstObjectByType<StageManager>();
        passiveSkillManager = FindFirstObjectByType<PassiveSkillManager>();
        enemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");
        player = GameObject.FindWithTag("Player").transform;
        playerPassiveController = GameObject.FindFirstObjectByType<PlayerPassiveController>();
    }
    public void StageStart()
    {
        if (stageType == StageType.Lobby)
        {
            RefreshLobbyEnhancementStationState();
            PrepareStageEntryCheckpoint(true);
            return;
        }

        if (IsRewardRoom)
        {
            StartRewardRoom();
            return;
        }

        switch (stageType)
        {
            case StageType.Wave:
                ResetMap();
                break;

            case StageType.Static:
                SetStageBlockActive(true);
                ConfigureStaticChests();
                ResetMap();
                break;
            default:
                break;

        }
    }

    public void ApplyRoomData(StageTypeSO stageRoomData, bool eliteCombat)
    {
        runtimeLevelConfig = null;
        roomData = stageRoomData;
        runtimeRewardData = null;
        runtimeDropBalanceData = null;
        runtimeBattleLevel = enemySpawnData != null ? enemySpawnData.SpawnLevel : -1;
        runtimeStaticEnemyCount = 0;
        runtimeStaticEnemySettings = Array.Empty<StaticEnemySetting>();
        runtimeWaveEnemySettings = Array.Empty<WaveEnemySetting>();
        isEliteCombat = eliteCombat && !IsRewardRoom;
    }

    public void ApplyLevelConfig(StageLevelConfigSO levelConfig)
    {
        runtimeLevelConfig = levelConfig;
        roomData = null;
        runtimeRewardData = levelConfig != null ? levelConfig.rewardData : null;
        runtimeDropBalanceData = levelConfig != null ? levelConfig.dropBalanceData : null;
        runtimeBattleLevel = levelConfig != null ? levelConfig.level : -1;
        enemySpawnData = ResolveEnemySpawnData(runtimeBattleLevel);
        runtimeStaticEnemyCount = enemySpawnData != null ? Mathf.Max(0, enemySpawnData.EnemyCount) : 0;
        runtimeStaticEnemySettings = enemySpawnData != null && enemySpawnData.staticEnemy != null
            ? enemySpawnData.staticEnemy
            : Array.Empty<StaticEnemySetting>();
        runtimeWaveEnemySettings = enemySpawnData != null && enemySpawnData.waveEnemy != null
            ? enemySpawnData.waveEnemy
            : Array.Empty<WaveEnemySetting>();
        stageType = ResolveStageType(levelConfig);
        isEliteCombat = levelConfig != null && levelConfig.forceEliteCombat;

        if (levelConfig != null && enemySpawnData == null)
            Debug.LogWarning($"SectionManager: EnemySpawnData is missing for configured level. level={runtimeBattleLevel}");
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            ResetMap();
            PlayerInfo.Instance.Revive();
        }
        if (waveRunning)
            Progress();
    }
    private void OnDisable()
    {
        CleanupWaveExitPortal();
        ClearEnemies();
    }
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (spawnPoints.Count != SpawnPointCount)
        {
            AdjustSpawnPoints();
        }

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i].transform != null)
            {
                spawnPoints[i].transform.name = $"Point_{i} (Range: {spawnPoints[i].range})";
            }
        }

        if (spawnPoints == null || spawnPoints.Count == 0) return;

        if (previousWeights.Count != spawnPoints.Count)
        {
            UpdatePreviousWeights();
            return;
        }

        int changedIndex = -1;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i].weight != previousWeights[i])
            {
                changedIndex = i;
                break;
            }
        }

        if (changedIndex != -1)
        {
            AdjustWeights(changedIndex);
            UpdatePreviousWeights();
        }
    }

    public void ClearEnemies()
    {
        if (spawncor != null)
            StopCoroutine(spawncor);

        List<GameObject> remainingTrackedEnemies = null;

        foreach (var enemy in activeEnemies)
        {
            if (enemy == null)
                continue;

            EnemyClass enemyClass = enemy.GetComponent<EnemyClass>();
            if (enemyClass == null)
                continue;

            if (enemyClass.enemyDead != null && enemyClass.enemyDead.isDead)
            {
                remainingTrackedEnemies ??= new List<GameObject>();
                remainingTrackedEnemies.Add(enemy);
                continue;
            }

            EnemyPoolManager.Instance.ReturnToPool(enemyClass.enemytype, enemy);
        }

        activeEnemies = remainingTrackedEnemies ?? new List<GameObject>();
    }

    public void EnemyKill()
    {
        kill++;
    }
    public void ResetMap()
    {
        Reset?.Invoke();
        CleanupWaveExitPortal();
        ClearEnemies();
        waveRunning = false;
        staticStageStartLevel = PlayerInfo.Instance != null ? Mathf.Max(1, PlayerInfo.Instance.level) : 1;
        staticStageTargetExp = 0;
        staticStageAwardedExp = 0;
        staticStageExpEnemyCount = 0;
        staticStageExpBaseReward = 0;
        staticStageExpRemainder = 0;
        staticStageExpGrantCount = 0;
        if (PlayerInfo.Instance == null || !PlayerInfo.Instance.isProfileLoaded)
        {
            Debug.LogWarning("SectionManager: ResetMap skipped because PlayerInfo profile is not loaded yet.");
            return;
        }

        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        playerController?.ResetTransientCombatState();
        PlayerInfo.Instance.ExpChange(-PlayerInfo.Instance.exp);
        playerPassiveController?.InvokeOnStageReset();

        if (stageType == StageType.Static)
        {
            SpawnAllMonsters();
        }
        else if (stageType == StageType.Wave && !waveRunning)
        {
            totalSpawn = 0;
            kill = 0;
            StartCoroutine(WaveRoutine());
        }
    }
    public void StageClear()
    {
        if (!isStageClearProcessing)
            StartCoroutine(StageClearRoutine());
    }

    private IEnumerator StageClearRoutine()
    {
        isStageClearProcessing = true;

        PlayerInfo playerInfo = PlayerInfo.Instance;
        if (playerInfo != null)
            playerInfo.sectionClear = true;

        if (stageType == StageType.Static)
            EnsureStaticStageLevelUp();

        if (playerInfo != null)
        {
            float remainingLevelUpPresentationTime = playerInfo.GetRemainingLevelUpPresentationTime();
            if (remainingLevelUpPresentationTime > 0f)
                yield return new WaitForSecondsRealtime(remainingLevelUpPresentationTime);
        }

        waveRunning = false;
        if (stageType == StageType.Static)
            SetStageBlockActive(false);
        ClearEnemies();
        yield return StartCoroutine(InGameUI.ShowNoticeRoutine("[Stage] Clear!", StageClearNoticeDuration));

        StageRewardDataSO rewardData = GetActiveRewardData();

        if (stageType == StageType.Wave)
            CheckResult();

        DialogueManager dialogueManager = FindFirstObjectByType<DialogueManager>();
        if (dialogueManager != null)
        {
            if (stageManager != null && stageManager.ActiveLevelConfig != null)
                yield return StartCoroutine(dialogueManager.PlayLevelClearDialogue(stageManager.ActiveLevelConfig));
            else
                yield return StartCoroutine(dialogueManager.OnClearDialogue());
        }

        if (rewardData != null && rewardData.giveRuneOnClear)
            yield return StartCoroutine(RuneGet());

        if (stageType == StageType.Wave)
            SpawnWaveExitPortal();

        isStageClearProcessing = false;
    }

    public void PrepareStageEntryCheckpoint(bool activateVisual)
    {
        if (checkPoint != null)
        {
            checkPoint.MarkRespawnPoint();

            if (activateVisual)
                checkPoint.ActivateLobbyCheckpoint();
        }
    }

    public void RefreshLobbyEnhancementStationState()
    {
        if (stageType != StageType.Lobby || enhancementStationObject == null)
            return;

        if (stageManager == null)
            stageManager = FindFirstObjectByType<StageManager>();

        int currentLobbyChapterIndex = stageManager != null ? stageManager.GetCurrentLobbyChapterIndex() : 1;
        enhancementStationObject.SetActive(currentLobbyChapterIndex >= 3);
    }

    private StageRewardDataSO GetActiveRewardData()
    {
        if (runtimeRewardData != null)
            return runtimeRewardData;

        if (!GameSession.Exists)
            return null;

        return GameSession.Instance.GetStageRewardData(GetBattleLevel());
    }

    private void SetStageBlockActive(bool isActive)
    {
        if (stageType != StageType.Static)
            return;

        blockObject?.SetActive(isActive);
    }

    private void SpawnWaveExitPortal()
    {
        if (stageType != StageType.Wave || wavePortalSpawned)
            return;

        if (wavePortalPrefab == null)
        {
            Debug.LogWarning($"SectionManager: wavePortalPrefab is missing. stage={name}");
            return;
        }

        Transform anchor = checkPoint != null ? checkPoint.GetPortalSpawnPoint() : transform;
        Quaternion anchorRotation = anchor.rotation;
        Quaternion fixedRotation = Quaternion.Euler(-90f, anchorRotation.eulerAngles.y, anchorRotation.eulerAngles.z);
        spawnedWavePortal = Instantiate(wavePortalPrefab, anchor.position + (Vector3.up * 0.8f), fixedRotation);
        spawnedWavePortal.SetActive(true);
        wavePortalSpawned = true;
    }

    private void CleanupWaveExitPortal()
    {
        wavePortalSpawned = false;

        if (spawnedWavePortal != null)
        {
            Destroy(spawnedWavePortal);
            spawnedWavePortal = null;
        }
    }

    private void ConfigureStaticChests()
    {
        Chest[] chests = GetComponentsInChildren<Chest>(true);
        if (chests == null || chests.Length == 0)
            return;

        StageRewardDataSO rewardData = GetActiveRewardData();
        if (rewardData == null)
        {
            Debug.LogWarning($"SectionManager: reward data is missing. Static chests could not be configured. level={GetBattleLevel()}");
            return;
        }

        for (int i = 0; i < chests.Length; i++)
        {
            Chest chest = chests[i];
            if (chest == null || chest.chestType != ChestType.Static)
                continue;

            RewardDropEntry[] materialLootPool = chest.size == ChestSize.Large
                ? rewardData.staticLargeChestMaterialLootPool
                : rewardData.staticSmallChestMaterialLootPool;
            RewardDropEntry[] guaranteedMaterialLoot = chest.size == ChestSize.Large
                ? rewardData.staticLargeChestGuaranteedMaterialLoot
                : rewardData.staticSmallChestGuaranteedMaterialLoot;
            RewardDropEntry[] weaponLootPool = chest.size == ChestSize.Large
                ? rewardData.staticLargeChestWeaponLootPool
                : rewardData.staticSmallChestWeaponLootPool;

            chest.SetRuntimeItems(BuildRuntimeItemsFromLootPools(guaranteedMaterialLoot, materialLootPool, weaponLootPool, $"static {chest.size} chest"));
        }
    }

    private void StartRewardRoom()
    {
        waveRunning = false;
        CleanupWaveExitPortal();
        ClearEnemies();
        ConfigureStaticChests();
        PrepareStageEntryCheckpoint(false);

        if (stageType == StageType.Static)
            SetStageBlockActive(false);
        else if (stageType == StageType.Wave)
            SpawnWaveExitPortal();
    }

    private GameObject[] BuildRuntimeItemsFromLootPools(RewardDropEntry[] guaranteedMaterialLoot, RewardDropEntry[] materialLootPool, RewardDropEntry[] weaponLootPool, string debugContext)
    {
        List<GameObject> selectedItems = new List<GameObject>();

        GameObject[] guaranteedItems = BuildGuaranteedItems(guaranteedMaterialLoot);
        if (guaranteedItems.Length > 0)
            selectedItems.AddRange(guaranteedItems);

        GameObject[] materialItems = BuildMaterialItemsFromLootPool(materialLootPool, debugContext);
        if (materialItems.Length > 0)
            selectedItems.AddRange(materialItems);

        GameObject weaponItem = RollSingleWeaponDrop(weaponLootPool);
        if (weaponItem != null)
            selectedItems.Add(weaponItem);

        if (selectedItems.Count == 0)
        {
            Debug.LogWarning($"SectionManager: all reward pools are empty. context={debugContext}, level={GetBattleLevel()}");
            return Array.Empty<GameObject>();
        }

        return selectedItems.ToArray();
    }

    private GameObject[] BuildGuaranteedItems(RewardDropEntry[] guaranteedMaterialLoot)
    {
        if (guaranteedMaterialLoot == null || guaranteedMaterialLoot.Length == 0)
            return Array.Empty<GameObject>();

        List<GameObject> guaranteedItems = new List<GameObject>();

        for (int i = 0; i < guaranteedMaterialLoot.Length; i++)
        {
            RewardDropEntry entry = guaranteedMaterialLoot[i];
            if (entry == null || entry.prefab == null)
                continue;

            guaranteedItems.Add(entry.prefab);
        }

        return guaranteedItems.ToArray();
    }

    private GameObject[] BuildMaterialItemsFromLootPool(RewardDropEntry[] materialLootPool, string debugContext)
    {
        if (materialLootPool == null || materialLootPool.Length == 0)
            return Array.Empty<GameObject>();

        List<RewardDropEntry> validEntries = new List<RewardDropEntry>();
        List<GameObject> selectedItems = new List<GameObject>();

        for (int i = 0; i < materialLootPool.Length; i++)
        {
            RewardDropEntry entry = materialLootPool[i];
            if (entry == null || entry.prefab == null)
                continue;

            validEntries.Add(entry);

            if (RollByPercent(entry.dropChancePercent))
                selectedItems.Add(entry.prefab);
        }

        if (selectedItems.Count == 0 && validEntries.Count > 0)
        {
            GameObject fallbackItem = ResolveGuaranteedFallbackItem(validEntries);
            if (fallbackItem != null)
                selectedItems.Add(fallbackItem);
        }

        if (selectedItems.Count == 0)
        {
            Debug.LogWarning($"SectionManager: material loot pool has no valid item prefabs. context={debugContext}, level={GetBattleLevel()}");
            return Array.Empty<GameObject>();
        }

        return selectedItems.ToArray();
    }

    private GameObject RollSingleWeaponDrop(RewardDropEntry[] weaponLootPool)
    {
        if (weaponLootPool == null || weaponLootPool.Length == 0)
            return null;

        List<GameObject> successfulWeaponDrops = new List<GameObject>();

        for (int i = 0; i < weaponLootPool.Length; i++)
        {
            RewardDropEntry entry = weaponLootPool[i];
            if (entry == null || entry.prefab == null)
                continue;

            if (RollByPercent(entry.dropChancePercent))
                successfulWeaponDrops.Add(entry.prefab);
        }

        if (successfulWeaponDrops.Count == 0)
            return null;

        int randomIndex = UnityEngine.Random.Range(0, successfulWeaponDrops.Count);
        return successfulWeaponDrops[randomIndex];
    }

    private GameObject ResolveGuaranteedFallbackItem(List<RewardDropEntry> validEntries)
    {
        if (validEntries == null || validEntries.Count == 0)
            return null;

        for (int i = 0; i < validEntries.Count; i++)
        {
            GameObject prefab = validEntries[i].prefab;
            if (prefab == null)
                continue;

            ItemInfo itemInfo = prefab.GetComponent<ItemInfo>();
            if (itemInfo != null && itemInfo.itemData != null &&
                itemInfo.itemData.itemType == ItemType.EnhancementStone)
            {
                return prefab;
            }
        }

        int randomIndex = UnityEngine.Random.Range(0, validEntries.Count);
        return validEntries[randomIndex].prefab;
    }

    #region Static Pattern
    public void EnemyDie()
    {
        remainNumber--;
        if (remainNumber <= 0)
            StageClear();
        else
            SetStageBlockActive(true);
    }
    public void SpawnAllMonsters()
    {
        // ???????밸븶??????밸븶?????????????轅몄뫅??????????? ????썹땟??雍?????쇨덧?????????딅?!
        StartCoroutine(SpawnMonstersCoroutine());
    }

    private IEnumerator SpawnMonstersCoroutine()
    {
        StaticEnemySetting[] staticEnemySettings = GetStaticEnemySettings();
        int configuredEnemyCount = GetStaticEnemyCount();
        if (staticEnemySettings == null || staticEnemySettings.Length == 0 || configuredEnemyCount <= 0)
            yield break;

        foreach (var sp in spawnPoints) sp.currentSpawnCount = 0;
        activeEnemies.Clear();

        int successCount = 0;
        int failSafety = 0;
        int totalWeight = 0;
        foreach (var sp in spawnPoints) totalWeight += sp.weight;

        // --- ?轅붽틓????彛???????? ???????밸븶??????밸븶?????袁⑸즴?????轅붽틓????彛? ?轅붽틓??????---
        int spawnPerFrame = 5;

        while (successCount < configuredEnemyCount && failSafety < configuredEnemyCount * 10)
        {
            failSafety++;

            StaticEnemySetting selectedEnemyData = GetRandomEnemyByRatio(staticEnemySettings);

            StaticEnemySpawnPoint selectedPoint = GetWeightedRandomPoint(totalWeight);
            Vector3 spawnPos = GetRandomNavMeshPosition(selectedPoint.transform.position, selectedPoint.range);

            if (spawnPos != Vector3.zero)
            {
                selectedPoint.currentSpawnCount++;
                spawnPos.y += 2f;

                EnemyType type = RollByPercent(selectedEnemyData.elite_SpawnRatio)
                    ? selectedEnemyData.elite_EnemyType
                    : selectedEnemyData.enemyType;
                if (isEliteCombat && selectedEnemyData.elite_EnemyType != selectedEnemyData.enemyType)
                    type = selectedEnemyData.elite_EnemyType;
                GameObject clone = EnemyPoolManager.Instance.SpawnFromPool(type);
                clone.transform.position = spawnPos;

                EnemyClass enemyScript = clone.GetComponent<EnemyClass>();
                if (enemyScript != null)
                {
                    GetSpawnStats(selectedEnemyData, type, out int hp, out int damage, out int defence, out int detectRange);
                    enemyScript.StatSetting(hp, damage, defence, detectRange);
                }

                EnemyDropItem ED = clone.GetComponent<EnemyDropItem>();
                if (ED != null) ED.sectionManager = this;

                activeEnemies.Add(clone);
                successCount++;

                // --- ????ш끽維??λ궔? ??????黎??筌??믨퀡??---
                // spawnPerFrame ?轅붽틓????????袁⑸즴?????????????????밸븶??????밸븶????????낅쨦??
                if (successCount % spawnPerFrame == 0)
                {
                    yield return null;
                }
            }
        }

        remainNumber = activeEnemies.Count;
        AssignStaticStageExpRewards();
        if (successCount < configuredEnemyCount)
            Debug.LogWarning($"SectionManager: static enemy spawn was short. missing:{configuredEnemyCount - successCount}");
    }

    private void AssignStaticStageExpRewards()
    {
        staticStageTargetExp = 0;
        staticStageAwardedExp = 0;
        staticStageExpEnemyCount = 0;
        staticStageExpBaseReward = 0;
        staticStageExpRemainder = 0;
        staticStageExpGrantCount = 0;

        if (stageType != StageType.Static || PlayerInfo.Instance == null || activeEnemies.Count == 0)
            return;

        if (PlayerInfo.Instance.needExp == null || PlayerInfo.Instance.needExp.Length == 0)
            return;

        int currentLevel = Mathf.Max(1, PlayerInfo.Instance.level);
        int levelIndex = Mathf.Clamp(currentLevel - 1, 0, PlayerInfo.Instance.needExp.Length - 1);
        int requiredExp = Mathf.Max(1, PlayerInfo.Instance.needExp[levelIndex]);
        int remainingExpToLevel = Mathf.Max(0, requiredExp - Mathf.Max(0, PlayerInfo.Instance.exp));

        if (remainingExpToLevel <= 0)
            return;

        staticStageStartLevel = currentLevel;
        staticStageTargetExp = remainingExpToLevel;

        List<EnemyClass> expTargets = new List<EnemyClass>(activeEnemies.Count);

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            GameObject enemy = activeEnemies[i];
            if (enemy == null)
                continue;

            EnemyClass enemyClass = enemy.GetComponent<EnemyClass>();
            if (enemyClass != null)
                expTargets.Add(enemyClass);
        }

        if (expTargets.Count == 0)
        {
            Debug.LogWarning("SectionManager: Static stage EXP assignment skipped because no valid EnemyClass targets were found.");
            return;
        }

        int enemyCount = expTargets.Count;
        staticStageExpEnemyCount = enemyCount;
        staticStageExpBaseReward = remainingExpToLevel / enemyCount;
        staticStageExpRemainder = remainingExpToLevel % enemyCount;

        Debug.Log($"[SectionManager] Assigned static stage EXP => level:{currentLevel}, targetExp:{remainingExpToLevel}, enemyCount:{enemyCount}, base:{staticStageExpBaseReward}, remainder:{staticStageExpRemainder}");
    }

    public void NotifyStaticExpGranted(int amount)
    {
        if (stageType != StageType.Static || amount <= 0)
            return;

        staticStageAwardedExp += amount;
    }

    public bool TryConsumeStaticExpReward(out int reward)
    {
        reward = 0;

        if (stageType != StageType.Static || staticStageExpEnemyCount <= 0)
            return false;

        if (staticStageExpGrantCount >= staticStageExpEnemyCount)
            return false;

        reward = staticStageExpBaseReward;
        if (staticStageExpGrantCount < staticStageExpRemainder)
            reward += 1;

        staticStageExpGrantCount++;
        return reward > 0;
    }

    private void EnsureStaticStageLevelUp()
    {
        if (PlayerInfo.Instance == null || staticStageTargetExp <= 0)
            return;

        if (PlayerInfo.Instance.level > staticStageStartLevel)
            return;

        int missingExp = staticStageTargetExp - staticStageAwardedExp;
        if (missingExp <= 0)
            return;

        Debug.LogWarning($"[SectionManager] Static stage EXP was short. Applying catch-up EXP => missing:{missingExp}, awarded:{staticStageAwardedExp}, target:{staticStageTargetExp}");
        PlayerInfo.Instance.ExpChange(missingExp);
        staticStageAwardedExp += missingExp;
    }
    // ???ル봿???μ떝?띄몭??袁㏉떋????壤굿???????逆??
    private StaticEnemySpawnPoint GetWeightedRandomPoint(int totalWeight)
    {
        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulativeWeight = 0;

        foreach (var sp in spawnPoints)
        {
            cumulativeWeight += sp.weight;
            if (roll < cumulativeWeight)
            {
                return sp;
            }
        }
        return spawnPoints[0];
    }
    // ???ル봿???μ떝?띄몭??袁㏉떋??Ratio) ???????泳?뿀????癲ル슢??節륁춻?????節떷???????????
    #endregion

    private List<GameObject> activeEnemies = new List<GameObject>();

    private static bool IsEliteEnemyType(EnemyType type)
    {
        return type == EnemyType.Ghoul_Elite
            || type == EnemyType.GhostDog_Elite
            || type == EnemyType.SkullWarrior_Elite
            || type == EnemyType.Nun_Elite
            || type == EnemyType.GhostSkull_Elite
            || type == EnemyType.Pagan_Elite;
    }

    private void GetSpawnStats(StaticEnemySetting setting, EnemyType spawnedType, out int hp, out int damage, out int defence, out int detectRange)
    {
        hp = 0;
        damage = 0;
        defence = 0;
        detectRange = 8;

        EnemyType baseEnemyType = spawnedType;
        if (IsEliteEnemyType(spawnedType) && setting != null)
            baseEnemyType = setting.enemyType;

        int battleLevel = GetBattleLevel();
        if (enemySpawnData == null || !enemySpawnData.TryGetEnemyStats(baseEnemyType, out EnemySpawnStatEntry baseStat))
        {
            Debug.LogWarning($"SectionManager: enemy base stats not found in EnemySpawnData. level={battleLevel}, enemyType={baseEnemyType}");
            return;
        }

        int baseHp = baseStat.hp;
        int baseDamage = baseStat.damage;
        int baseDefence = baseStat.defence;
        int baseDetectRange = Mathf.Max(0, baseStat.detectRange);

        hp = baseHp;
        damage = baseDamage;
        defence = baseDefence;
        detectRange = baseDetectRange;

        ApplyWaveStageReduction(ref hp, ref damage, ref defence);

        bool isEliteEnemy = IsEliteEnemyType(spawnedType);
        if (isEliteEnemy)
        {
            hp = Mathf.RoundToInt(hp * EliteHealthMultiplier);
            damage = Mathf.RoundToInt(damage * EliteDamageMultiplier);
            defence = Mathf.RoundToInt(defence * EliteDefenseMultiplier);
        }

        LogResolvedSpawnStats(spawnedType, baseHp, baseDamage, baseDefence, hp, damage, defence, isEliteEnemy, baseDetectRange, detectRange);
    }

    private EnemySpawnData ResolveEnemySpawnData(int battleLevel)
    {
        if (!GameSession.Exists || battleLevel <= 0)
            return null;

        return GameSession.Instance.GetEnemySpawnData(battleLevel);
    }

    private void ApplyWaveStageReduction(ref int hp, ref int damage, ref int defence)
    {
        if (stageType != StageType.Wave)
            return;

        hp = ApplyPercentMultiplier(hp, waveStageReduction.hpPercent);
        damage = ApplyPercentMultiplier(damage, waveStageReduction.damagePercent);
        defence = ApplyPercentMultiplier(defence, waveStageReduction.defencePercent);
    }

    private static int ApplyPercentMultiplier(int value, int percent)
    {
        if (value <= 0)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(value * (percent / 100f)));
    }

    private void LogResolvedSpawnStats(
        EnemyType spawnedType,
        int baseHp,
        int baseDamage,
        int baseDefence,
        int finalHp,
        int finalDamage,
        int finalDefence,
        bool isEliteEnemy,
        int baseDetectRange,
        int finalDetectRange)
    {
        if (!logSpawnStatResolution)
            return;

        string stageAdjustmentLabel = stageType == StageType.Wave
            ? $"wave({waveStageReduction.hpPercent}%/{waveStageReduction.damagePercent}%/{waveStageReduction.defencePercent}%)"
            : "static(100%/100%/100%)";
        string eliteLabel = isEliteEnemy
            ? $"elite({EliteHealthMultiplier:0.##}/{EliteDamageMultiplier:0.##}/{EliteDefenseMultiplier:0.##})"
            : "normal";

        Debug.Log(
            $"[SectionManager] Spawn stat resolved | level:{GetBattleLevel()} | stage:{stageType} | enemy:{spawnedType} | " +
            $"base:{baseHp}/{baseDamage}/{baseDefence}/detect:{baseDetectRange} | adjustment:{stageAdjustmentLabel} | mode:{eliteLabel} | " +
            $"final:{finalHp}/{finalDamage}/{finalDefence}/detect:{finalDetectRange}");
    }

    #region Wave Pattern
    private Queue<GameObject> enemyPool = new Queue<GameObject>();
    public void PreparePoolForPhase(int phase)
    {
        enemyPool.Clear();
        WaveEnemySetting[] waveEnemySettings = GetWaveEnemySettings();
        if (waveEnemySettings == null || phase < 0 || phase >= waveEnemySettings.Length)
            return;

        int needed = waveEnemySettings[phase].phaseSpawnCount;

        for (int i = 0; i < needed; i++)
        {
            StaticEnemySetting selectedEnemyData = GetRandomEnemyByRatio(waveEnemySettings[phase].enemySetting);
            EnemyType type = RollByPercent(selectedEnemyData.elite_SpawnRatio)
                ? selectedEnemyData.elite_EnemyType
                : selectedEnemyData.enemyType;
            if (isEliteCombat && selectedEnemyData.elite_EnemyType != selectedEnemyData.enemyType)
                type = selectedEnemyData.elite_EnemyType;
            GameObject prefab = EnemyPoolManager.Instance.GetFromPoolInactive(type);
            if (prefab == null)
                continue;
            // ????ш낄援ο쭛????濚밸Ŧ???
            EnemyClass ie = prefab.GetComponent<EnemyClass>();
            if (ie != null)
            {
                GetSpawnStats(selectedEnemyData, type, out int hp, out int damage, out int defence, out int detectRange);
                ie.StatSetting(hp, damage, defence, detectRange);
            }
            enemyPool.Enqueue(prefab);
        }
    }
    private GameObject GetEnemy()
    {
        if (enemyPool.Count == 0)
        {
            Debug.LogWarning("[Wave] Prepared enemy pool is empty.");
            return null;
        }

        GameObject obj = enemyPool.Dequeue();
        Vector3 spawnPos = GetRandomNavMeshPosition(player.position, 15f);
        if (spawnPos == Vector3.zero)
        {
            Vector3 fallbackCenter = player != null ? player.position : transform.position;
            if (NavMesh.SamplePosition(fallbackCenter, out NavMeshHit fallbackHit, 8f, NavMesh.AllAreas))
                spawnPos = fallbackHit.position;
            else
            {
                ReturnFailedWaveSpawn(obj, "No valid NavMesh position was found.");
                return null;
            }
        }

        NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
            agent.enabled = false;

        obj.transform.position = spawnPos;

        EnemyClass enemyClass = obj.GetComponent<EnemyClass>();
        if (enemyClass != null && enemyClass.enemyController != null)
            enemyClass.enemyController.SetSpawnAnchor(spawnPos);

        obj.SetActive(true);

        if (enemyClass != null)
        {
            EnemyController controller = enemyClass.enemyController;
            if (controller == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                ReturnFailedWaveSpawn(obj, "Enemy could not be placed on the NavMesh.");
                return null;
            }

            controller.SetSpawnAnchor(obj.transform.position);
        }

        return obj;
    }

    private void ReturnFailedWaveSpawn(GameObject enemy, string reason)
    {
        if (enemy == null)
            return;

        EnemyClass enemyClass = enemy.GetComponent<EnemyClass>();
        if (enemyClass == null || EnemyPoolManager.Instance == null)
        {
            enemy.SetActive(false);
            Debug.LogWarning($"[Wave] Disabled invalid enemy spawn. {reason}");
            return;
        }

        EnemyPoolManager.Instance.ReturnToPool(enemyClass.enemytype, enemy);
        Debug.LogWarning($"[Wave] Returned {enemy.name} to pool. {reason}");
    }
    
    public void SpawnGroup(int phase)
    {
        PreparePoolForPhase(phase);
        spawncor = StartCoroutine(WaveSpawnRoutine(phase));
    }
    private IEnumerator WaveSpawnRoutine(int phase)
    {
        WaveEnemySetting[] waveEnemySettings = GetWaveEnemySettings();
        if (waveEnemySettings == null || phase < 0 || phase >= waveEnemySettings.Length)
            yield break;

        WaveEnemySetting currentPhase = waveEnemySettings[phase];
        int totalMonsters = currentPhase.phaseSpawnCount;
        float duration = currentPhase.phaseDuration;
        if (totalMonsters <= 0)
            yield break;

        SpawnWaveEnemyInstance();

        List<float> spawnTimes = new List<float>();
        float randomWindowMax = Mathf.Max(0f, duration - 5f);
        for (int i = 1; i < totalMonsters; i++)
            spawnTimes.Add(UnityEngine.Random.Range(0f, randomWindowMax));

        spawnTimes.Sort();

        float currentTime = 0f;
        for (int i = 0; i < spawnTimes.Count; i++)
        {
            float waitTime = spawnTimes[i] - currentTime;
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
                currentTime = spawnTimes[i];
            }

            SpawnWaveEnemyInstance();
        }
    }

    private void SpawnWaveEnemyInstance()
    {
        GameObject enemy = GetEnemy();
        if (enemy == null)
            return;

        SpawnCheck();

        EnemyDropItem drop = enemy.GetComponent<EnemyDropItem>();
        if (drop != null)
            drop.sectionManager = this;

        activeEnemies.Add(enemy);
    }
    Coroutine spawncor;
    float waveStartTime;
    private int lastExpSet = 0;
    int nowLevel;
    private float currentPhaseElapsed = 0f; // ?????밸븶??????蹂κ텤?熬곎逾??轅붽틓????筌뤾쑴????????
    private float totalBattleDuration = 0f; // ?????밸븶??????蹂κ텤?熬곎逾??????????
    private float currentWaveTimeStack = 0f; // ?????밸븶??????蹂κ텤?熬곎逾??????쇨덫???띾쿀?꾩룆??? ?????????????????밸븶????뼿?
    public void Progress()
    {
        // 1. ?????????轅붽틓??????⒟?(????裕뼘???μ떝?띄몭??袁㏉떋???????밸븶?????????꾤뙴??沃섃뫗커???????????????쇨덧?筌먦렜逾????숆강????
        WaveEnemySetting[] waveEnemySettings = GetWaveEnemySettings();
        if (!waveRunning || waveEnemySettings == null || waveEnemySettings.Length == 0 || PlayerInfo.Instance == null) return;

        // 2. [????? ????筌??????ш끽維뽳쭩???????????ル늅獄??????????筌???????????????鶯???β뼯援?????⑥쥓堉??? ????용츧?? ?????ㅿ폎??
        // ????裕뼘????癲ル슢??節녿쨨??????? nowLevel???ㅼ뒧?????????밸븶??????筌????????붺몭?⑸쨨?壤??ル∥釉???ш끽維뽳쭩??嚥?????獄???
        if (PlayerInfo.Instance.level != nowLevel) return;

        // 3. ?????????轅붽틓????筌뤾쑴?????壤굿?????
        currentPhaseElapsed += Time.deltaTime;
        float currentTotalElapsed = currentWaveTimeStack + currentPhaseElapsed;

        // ?????밸븶??????袁ｋ쨨?????????????????밸븶???轅붽틓????筌뤾쑴???????(0~1)
        float progress = Mathf.Clamp01(currentTotalElapsed / totalBattleDuration);

        // 4. ?????밸븶??????筌?????轅붽틓????彛? ??β뼯援?????⑥쥓堉??
        if (PlayerInfo.Instance.needExp == null || PlayerInfo.Instance.needExp.Length == 0) return;

        int levelIndex = Mathf.Clamp(nowLevel - 1, 0, PlayerInfo.Instance.needExp.Length - 1);
        int maxExp = Mathf.Max(1, PlayerInfo.Instance.needExp[levelIndex]);

        // 5. ??????????밸븶?????袁ㅻ쇀??ル?????깆궔? ?μ떝?띄몭??觀??????????밸븶????β뼯援?????⑥쥓堉??????
        int targetExp = Mathf.RoundToInt(progress * maxExp);

        // 6. ?????쇨덫???????밸븶??????밸븶?????????????濚밸Ŧ援????????轅붽틓?????
        if (targetExp > lastExpSet)
        {
            int diff = targetExp - lastExpSet;

            // ??????????산뭐??maxExp????? ??????몃뜪?????ㅼ뒧????
            if (lastExpSet + diff > maxExp)
                diff = maxExp - lastExpSet;

            PlayerInfo.Instance.ExpChange(diff);
            lastExpSet += diff;
        }
    }
    private IEnumerator WaveRoutine()
    {
        //????裕뼘???逆???⑸걦???
        lastExpSet = 0;
        waveStartTime = Time.time; // ????裕뼘????癲ル슢??節녿쨨????????????????덉땃?
        totalBattleDuration = 0f;
        nowLevel = PlayerInfo.Instance.level;
        currentWaveTimeStack = 0f; // ?????밸븶?????????逆???⑸걦???
        WaveEnemySetting[] waveEnemySettings = GetWaveEnemySettings();
        if (waveEnemySettings == null || waveEnemySettings.Length == 0)
            yield break;

        foreach (var phase in waveEnemySettings)
        {
            totalBattleDuration += phase.phaseDuration;
        }
        for (int i = 0; i < waveEnemySettings.Length; i++)
        {
            waveRunning = true;

            InGameUI.ShowNotice($"[Wave] Phase {i + 1} start");
            SpawnGroup(i);
            yield return new WaitForSeconds(waveEnemySettings[i].phaseDuration);

            waveRunning = false;


            ClearEnemies();

            if(i == waveEnemySettings.Length - 1)
            {
                InGameUI.ShowNotice("[Wave] All phases cleared", 4f);
                yield return new WaitForSeconds(4f);
            }
            else
            {
                InGameUI.ShowNotice($"[Wave] Phase {i + 1} complete. Rest {restDuration} sec");
                yield return new WaitForSeconds(restDuration);
            }
        }
        yield return StartCoroutine(StageClearRoutine());
    }
    #endregion

    private StaticEnemySetting GetRandomEnemyByRatio(StaticEnemySetting[] enemies)
    {
        float totalRatio = 0;
        foreach (var e in enemies) totalRatio += e.spawnRatio;

        float randomValue = UnityEngine.Random.Range(0, totalRatio);
        float cumulative = 0;

        foreach (var e in enemies)
        {
            cumulative += e.spawnRatio;
            if (randomValue <= cumulative)
                return e;
        }
        return enemies[0];
    }

    private bool RollByPercent(float chancePercent)
    {
        return UnityEngine.Random.Range(0f, 100f) <= chancePercent;
    }

    Vector3 GetRandomNavMeshPosition(Vector3 center, float range)
    {
        int obstacleMask = LayerMask.GetMask("Obstacle", "Wall", "Enemy");

        for (int i = 0; i < 15; i++)
        {
            Vector2 unitCircle = UnityEngine.Random.insideUnitCircle * range;
            Vector3 randomPoint = center + new Vector3(unitCircle.x, 0, unitCircle.y);

            float searchRadius = 2.0f + (i * 0.5f);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                float currentSafety = safetyRadius * (1.0f - (i / 15f * 0.5f));

                bool isBlocked = Physics.CheckSphere(hit.position, currentSafety, obstacleMask);

                if (!isBlocked)
                {
                    return hit.position;
                }
            }
        }

        return Vector3.zero;
    }
    private IEnumerator RuneGet()
    {
        yield return new WaitForSeconds(1f);
        if (passiveSkillManager != null)
            passiveSkillManager.PassiveSelect();
        else
            Debug.LogWarning("SectionManager: PassiveSkillManager not found. Rune reward skipped.");
    }
    public void SpawnCheck()
    {
        totalSpawn++;
    }
    void CheckResult()
    {
        if (totalSpawn == 0)
        {
            Debug.LogWarning("[Wave] No enemies were spawned, so reward grade cannot be calculated. (totalSpawn = 0)");
            return;
        }

        float resultRate = (float)kill / totalSpawn * 100f;
        StageRewardDataSO rewardData = GetActiveRewardData();
        if (rewardData == null)
        {
            Debug.LogWarning($"[Wave] Reward data is missing. level={GetBattleLevel()}");
            return;
        }

        WaveRewardGradeEntry selectedGrade = ResolveWaveRewardGrade(rewardData, resultRate);
        if (selectedGrade == null)
        {
            Debug.LogWarning($"[Wave] No valid wave reward grades configured. level={GetBattleLevel()}");
            return;
        }

        GameObject rewardPrefab = GetWaveChestPrefab(rewardData, selectedGrade.grade);
        if (rewardPrefab == null)
        {
            Debug.LogWarning($"[Wave] Reward chest prefab is missing. grade={selectedGrade.grade}, level={GetBattleLevel()}");
            return;
        }

        GameObject[] runtimeItems = BuildRuntimeItemsFromLootPools(
            selectedGrade.guaranteedMaterialLoot,
            selectedGrade.materialLootPool,
            selectedGrade.weaponLootPool,
            $"wave reward {selectedGrade.grade}");
        Vector3 spawnPosition = player.position + Vector3.back * 3f;
        GameObject rewardChestObject = Instantiate(rewardPrefab, spawnPosition, Quaternion.identity);
        Chest rewardChest = rewardChestObject.GetComponent<Chest>();
        if (rewardChest != null)
        {
            rewardChest.chestType = ChestType.Wave;
            rewardChest.SetRuntimeItems(runtimeItems);
        }

        rewardChestObject.SetActive(true);

        string rewardGradeLabel = GetRewardGradeLabel(selectedGrade);
        InGameUI.ShowNotice($"[Reward] {rewardGradeLabel} grade reward acquired!", 3f);
    }

    private WaveRewardGradeEntry ResolveWaveRewardGrade(StageRewardDataSO rewardData, float resultRate)
    {
        if (rewardData == null || rewardData.waveRewardGrades == null || rewardData.waveRewardGrades.Length == 0)
            return null;

        WaveRewardGradeEntry[] validGrades = Array.FindAll(
            rewardData.waveRewardGrades,
            gradeEntry => gradeEntry != null);

        if (validGrades.Length == 0)
            return null;

        Array.Sort(validGrades, (a, b) => b.minKillRatePercent.CompareTo(a.minKillRatePercent));

        WaveRewardGradeEntry selectedGrade = validGrades[validGrades.Length - 1];
        for (int i = 0; i < validGrades.Length; i++)
        {
            if (resultRate >= validGrades[i].minKillRatePercent)
            {
                selectedGrade = validGrades[i];
                break;
            }
        }

        return selectedGrade;
    }

    private GameObject GetWaveChestPrefab(StageRewardDataSO rewardData, WaveRewardGradeType grade)
    {
        if (rewardData == null)
            return null;

        switch (grade)
        {
            case WaveRewardGradeType.A:
            case WaveRewardGradeType.B:
                return rewardData.waveLargeChestPrefab;
            case WaveRewardGradeType.C:
            case WaveRewardGradeType.D:
                return rewardData.waveSmallChestPrefab;
            default:
                return null;
        }
    }

    private string GetRewardGradeLabel(WaveRewardGradeEntry selectedGrade)
    {
        return selectedGrade == null ? "Reward" : selectedGrade.grade.ToString();
    }

    public StageDropBalanceSO GetDropBalanceData()
    {
        if (runtimeDropBalanceData != null)
            return runtimeDropBalanceData;

        return GameSession.Exists ? GameSession.Instance.GetStageDropBalance(GetBattleLevel()) : null;
    }

    public int GetBattleLevel()
    {
        if (runtimeBattleLevel > 0)
            return runtimeBattleLevel;

        if (enemySpawnData != null)
            return Mathf.Max(1, enemySpawnData.SpawnLevel);

        return stageManager != null ? Mathf.Max(1, stageManager.ActiveBattleLevel) : 1;
    }

    private StaticEnemySetting[] GetStaticEnemySettings()
    {
        if (runtimeLevelConfig != null)
            return runtimeStaticEnemySettings;

        return enemySpawnData != null ? enemySpawnData.staticEnemy : null;
    }

    private int GetStaticEnemyCount()
    {
        if (runtimeLevelConfig != null)
            return runtimeStaticEnemyCount > 0 ? runtimeStaticEnemyCount : (runtimeStaticEnemySettings != null ? runtimeStaticEnemySettings.Length : 0);

        return enemySpawnData != null ? enemySpawnData.EnemyCount : 0;
    }

    private WaveEnemySetting[] GetWaveEnemySettings()
    {
        if (runtimeLevelConfig != null)
            return runtimeWaveEnemySettings;

        return enemySpawnData != null ? enemySpawnData.waveEnemy : null;
    }

    private static StageType ResolveStageType(StageLevelConfigSO levelConfig)
    {
        if (levelConfig == null)
            return StageType.None;

        return levelConfig.stageType switch
        {
            StageLevelType.Static => StageType.Static,
            StageLevelType.Wave => StageType.Wave,
            StageLevelType.Reward => StageType.Reward,
            _ => StageType.None
        };
    }

    private void UpdatePreviousWeights()
    {
        previousWeights.Clear();
        foreach (var sp in spawnPoints) previousWeights.Add(sp.weight);
    }

    private void AdjustWeights(int changedIndex)
    {
        int changedValue = spawnPoints[changedIndex].weight;
        int otherWeightsSum = 0;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (i != changedIndex) otherWeightsSum += spawnPoints[i].weight;
        }

        int targetOtherSum = 100 - changedValue;

        if (otherWeightsSum > 0)
        {
            int currentNewSum = 0;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (i == changedIndex) continue;

                float ratio = (float)spawnPoints[i].weight / otherWeightsSum;
                spawnPoints[i].weight = Mathf.RoundToInt(ratio * targetOtherSum);
                currentNewSum += spawnPoints[i].weight;
            }

            int diff = targetOtherSum - currentNewSum;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (i != changedIndex)
                {
                    spawnPoints[i].weight += diff;
                    break;
                }
            }
        }
        else if (spawnPoints.Count > 1)
        {
            int remainingPoints = spawnPoints.Count - 1;
            int valuePerPoint = targetOtherSum / remainingPoints;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (i != changedIndex) spawnPoints[i].weight = valuePerPoint;
            }
        }
    }
    private void AdjustSpawnPoints()
    {
        while (spawnPoints.Count < SpawnPointCount)
        {
            GameObject newObj = new GameObject($"Point_{spawnPoints.Count}");
            newObj.transform.SetParent(this.transform);

            newObj.transform.SetAsFirstSibling(); 

            newObj.transform.localPosition = Vector3.zero;

            spawnPoints.Add(new StaticEnemySpawnPoint { transform = newObj.transform, range = 5f });
        }

        while (spawnPoints.Count > SpawnPointCount)
        {
            int lastIdx = spawnPoints.Count - 1;
            if (spawnPoints[lastIdx].transform != null)
            {
                GameObject objToDelete = spawnPoints[lastIdx].transform.gameObject;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () => {
                    if (objToDelete != null) DestroyImmediate(objToDelete);
                };
#else
        Destroy(objToDelete);
#endif
            }
            spawnPoints.RemoveAt(lastIdx);
        }
    }
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;

        int maxWeight = 0;
        foreach (var sp in spawnPoints)
        {
            if (sp.weight > maxWeight) maxWeight = sp.weight;
        }

        foreach (var sp in spawnPoints)
        {
            if (sp.transform == null) continue;

            Gizmos.color = Color.cyan;
            Gizmos.DrawCube(sp.transform.position, Vector3.one * 0.2f);

            float relativeRatio = (maxWeight > 0) ? (float)sp.weight / maxWeight : 0f;

            Color lowColor = new Color(1f, 1f, 1f, 0.05f);  
            Color highColor = new Color(0f, 1f, 0.3f, 0.7f); 

            Gizmos.color = Color.Lerp(lowColor, highColor, relativeRatio);

            Gizmos.DrawSphere(sp.transform.position, sp.range);

            UnityEditor.Handles.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.8f);
            UnityEditor.Handles.DrawWireDisc(sp.transform.position, Vector3.up, sp.range);

            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.DrawWireArc(sp.transform.position, Vector3.up, Vector3.forward, 360f, safetyRadius);

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 15;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            UnityEditor.Handles.Label(sp.transform.position + Vector3.up * 0.5f,
                $"[ {sp.transform.name}]\n" +
                $"Weight: {sp.weight}%\n"+
                $"{sp.currentSpawnCount} Spawned", style);
        }

        if (Application.isPlaying && activeEnemies != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                    Gizmos.DrawWireSphere(enemy.transform.position, safetyRadius);
            }
        }
    }
#endif
}




