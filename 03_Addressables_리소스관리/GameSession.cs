using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

public sealed class GameSession : MonoBehaviour
{
    private const int GameplayCacheLoadStepCount = 16;
    private const string AssetLoadLogPrefix = "[GameSession][AssetLoad]";
    public const string DefaultGameplaySceneName = "Jimin_Stage";
    private const string EnemyDataLabel = "EnemyDataLabel";
    private const string StageRewardDataLabel = StageRewardDataSO.AddressableLabel;
    private const string StageDropBalanceLabel = StageDropBalanceSO.AddressableLabel;
    private const string DialogueDataLabel = "DialogueDataLabel";
    private const string DialogueSpeakerLabel = DialogueSpeakerProfileSO.AddressableLabel;
    private const string PassiveSkillLabel = "PassiveSkillLabel";
    private const string InventorySpriteLabel = "InventoryLabel";
    private const string TutorialSpriteLabel = "TutorialSpriteLabel";
    private const string SoundLabel = "SoundLabel";
    private const string SkillDataLabel = "SkillDataLabel";
    private const string StageLevelConfigLabel = StageLevelConfigSO.AddressableLabel;

    private static readonly HashSet<string> NonGameplayScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "GameTitle",
        "LoadingScene",
        "Prologue",
    };

    private static GameSession instance;

    public static event Action OnGameplayCacheReady;
    public static event Action<string, float> OnGameplayCachePreloadStep;

    public static GameSession Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameSession>();
                if (instance == null)
                {
                    GameObject gameObject = new GameObject(nameof(GameSession));
                    instance = gameObject.AddComponent<GameSession>();
                }
            }

            return instance;
        }
    }

    public static bool Exists => instance != null;

    public UserSetting Settings { get; private set; } = new UserSetting();
    public SaveInfo CurrentSave { get; private set; } = new SaveInfo();
    public bool HasSaveFile { get; private set; }
    public bool IsGameplayCacheReady { get; private set; }
    public bool IsNewGamePending { get; private set; }
    public bool HasBattleAttemptSnapshot => battleAttemptSnapshot != null;
    public bool IsBossTestSession { get; private set; }

    private bool isCacheLoading;
    private bool isAddressablesInitialized;
    private bool isAddressablesInitializationRequested;
    private AsyncOperationHandle addressablesInitializeHandle;

    private AsyncOperationHandle<PlayerStatProfileSO> playerProfileHandle;
    private AsyncOperationHandle<WeaponEnhanceBalanceSO> weaponEnhanceBalanceHandle;
    private AsyncOperationHandle<IList<PlayerLevelStatSO>> levelStatHandle;
    private AsyncOperationHandle<IList<EnemySpawnData>> enemyDataHandle;
    private AsyncOperationHandle<IList<StageRewardDataSO>> stageRewardDataHandle;
    private AsyncOperationHandle<IList<StageDropBalanceSO>> stageDropBalanceHandle;
    private AsyncOperationHandle<IList<DialogueData>> dialogueDataHandle;
    private AsyncOperationHandle<IList<DialogueSpeakerProfileSO>> dialogueSpeakerHandle;
    private AsyncOperationHandle<IList<PassiveSkillData>> passiveSkillHandle;
    private AsyncOperationHandle<IList<Sprite>> inventorySpriteHandle;
    private AsyncOperationHandle<IList<SoundAsset>> soundAssetHandle;
    private AsyncOperationHandle<IList<SkillData>> skillDataHandle;
    private AsyncOperationHandle<IList<StageLevelConfigSO>> stageLevelConfigHandle;
    private AsyncOperationHandle<IList<IResourceLocation>> tutorialSpriteLocationHandle;

    private readonly List<AsyncOperationHandle<Sprite>> skillIconHandles = new List<AsyncOperationHandle<Sprite>>();
    private readonly List<AsyncOperationHandle<Sprite>> dialoguePortraitHandles = new List<AsyncOperationHandle<Sprite>>();
    private readonly List<AsyncOperationHandle<Sprite>> tutorialSpriteAssetHandles = new List<AsyncOperationHandle<Sprite>>();
    private readonly List<AsyncOperationHandle<Sprite>> tutorialSpriteReferenceHandles = new List<AsyncOperationHandle<Sprite>>();
    private readonly Dictionary<int, PlayerLevelStatSO> levelStatTable = new Dictionary<int, PlayerLevelStatSO>();
    private readonly Dictionary<int, EnemySpawnData> enemySpawnTable = new Dictionary<int, EnemySpawnData>();
    private readonly Dictionary<int, StageRewardDataSO> stageRewardTable = new Dictionary<int, StageRewardDataSO>();
    private readonly Dictionary<int, StageDropBalanceSO> stageDropBalanceTable = new Dictionary<int, StageDropBalanceSO>();
    private readonly Dictionary<int, DialogueData> dialogueTable = new Dictionary<int, DialogueData>();
    private readonly Dictionary<DialogueSpeakerType, DialogueSpeakerProfileSO> dialogueSpeakerTable = new Dictionary<DialogueSpeakerType, DialogueSpeakerProfileSO>();
    private readonly Dictionary<string, Sprite> dialoguePortraitTable = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<PassiveSkillType, PassiveSkillData> passiveSkillTable = new Dictionary<PassiveSkillType, PassiveSkillData>();
    private readonly Dictionary<string, Sprite> inventorySpriteTable = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sprite> tutorialSpriteTable = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundAsset> soundTable = new Dictionary<string, SoundAsset>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sprite> skillIconTable = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly List<SkillData> cachedSkillDataList = new List<SkillData>();
    private readonly Dictionary<SkillType, SkillData> cachedSkillData = new Dictionary<SkillType, SkillData>();
    private readonly Dictionary<int, StageLevelConfigSO> stageLevelConfigTable = new Dictionary<int, StageLevelConfigSO>();
    private readonly List<StageLevelConfigSO> orderedStageLevelConfigs = new List<StageLevelConfigSO>();

    private PlayerStatProfileSO playerProfile;
    private WeaponEnhanceBalanceSO weaponEnhanceBalance;
    private int[] needExpTable = Array.Empty<int>();
    private LevelUpStats[] levelUpStatsTable = Array.Empty<LevelUpStats>();
    private WeaponStatMultiplierConfig statMultipliers = new WeaponStatMultiplierConfig();
    private SaveInfo battleAttemptSnapshot;
    private SaveInfo committedSave = new SaveInfo();
    private readonly HashSet<int> shownLevelStartDialogueLevels = new HashSet<int>();
    private readonly HashSet<int> shownAutoTutorialLevels = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadPersistentState();
        ApplyCurrentSettings();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (instance == this)
        {
            ClearGameplayCache();
            ReleaseHandle(ref addressablesInitializeHandle);
            instance = null;
        }
    }

    public static bool IsGameplayScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && !NonGameplayScenes.Contains(sceneName);
    }

    public void StartNewGame()
    {
        EnsurePersistentStateInitialized();
        GameSaveFileService.Delete();
        IsNewGamePending = true;
        HasSaveFile = false;
        CurrentSave = new SaveInfo();
        CurrentSave.setting = CloneSettings(Settings);
        CurrentSave.sceneName = DefaultGameplaySceneName;
        CurrentSave.nextBattleLevel = 1;
        committedSave = CloneSave(CurrentSave);
        battleAttemptSnapshot = null;
        ResetBattlePresentationState();
    }

    public void BeginBossTest(BossTestProfileSO profile)
    {
        if (profile == null)
        {
            Debug.LogError("GameSession: cannot begin boss test without a profile.");
            return;
        }

        EnsurePersistentStateInitialized();

        CurrentSave = BuildBossTestSave(profile);
        committedSave = CloneSave(CurrentSave);
        HasSaveFile = GameSaveFileService.Exists();
        IsNewGamePending = false;
        IsBossTestSession = true;
        battleAttemptSnapshot = null;
        ResetBattlePresentationState();
    }

    public void UpdateSettings(UserSetting source)
    {
        EnsurePersistentStateInitialized();
        Settings = CloneSettings(source);

        if (CurrentSave == null)
            CurrentSave = new SaveInfo();

        CurrentSave.setting = CloneSettings(Settings);
    }

    public void ApplyCurrentSettings()
    {
        EnsurePersistentStateInitialized();
        ApplyGraphicsSettings(Settings);
    }

    public void SaveSettings()
    {
        EnsurePersistentStateInitialized();
        GameSettingsService.Save(Settings);
    }

    public void SaveAll()
    {
        EnsurePersistentStateInitialized();
        SaveCurrentProgress(CurrentSave);
        SaveSettings();
    }

    public IEnumerator PreloadGameplayCacheAsync()
    {
        EnsurePersistentStateInitialized();

        if (IsGameplayCacheReady)
        {
            LogAssetLoad("SKIP | Gameplay cache is already ready.");
            yield break;
        }

        if (isCacheLoading)
        {
            while (isCacheLoading)
                yield return null;

            yield break;
        }

        isCacheLoading = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        System.Diagnostics.Stopwatch totalLoadStopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif

        ReportGameplayCachePreloadStep("Initializing Addressables...", 0.02f);
        yield return EnsureAddressablesInitializedCoroutine();

        if (!isAddressablesInitialized)
        {
            ReportGameplayCachePreloadStep("Addressables initialization failed.", 0f);
            LogAssetLoad("ERROR | Addressables initialization failed.");
            isCacheLoading = false;
            yield break;
        }

        int stepIndex = 0;
        foreach (var step in CreateGameplayCacheLoadList())
        {
            stepIndex++;
            ReportGameplayCachePreloadStep(step.message, step.progress);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stepStopwatch = System.Diagnostics.Stopwatch.StartNew();
            LogAssetLoad($"START {stepIndex:00}/{GameplayCacheLoadStepCount:00} | {step.message} | {step.progress:P0}");
#endif

            yield return step.load();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            stepStopwatch.Stop();
            LogAssetLoad($"COMPLETE {stepIndex:00}/{GameplayCacheLoadStepCount:00} | {step.message} | {stepStopwatch.Elapsed.TotalSeconds:F2}s");
#endif
        }

        isCacheLoading = false;
        IsGameplayCacheReady = playerProfile != null &&
        weaponEnhanceBalance != null &&
        levelStatTable.Count > 0 &&
        enemySpawnTable.Count > 0 &&
        dialogueTable.Count > 0 &&
        stageLevelConfigTable.Count > 0 &&
        passiveSkillTable.Count > 0;

        if (!IsGameplayCacheReady)
        {
            ReportGameplayCachePreloadStep("Gameplay cache build incomplete.", 0f);
            Debug.LogError("GameSession: gameplay cache preload completed, but one or more required caches are empty.");
        }
        else
        {
            ReportGameplayCachePreloadStep("Gameplay cache ready.", 1f);
            OnGameplayCacheReady?.Invoke();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        totalLoadStopwatch.Stop();
        LogAssetLoad(
            $"READY | total={totalLoadStopwatch.Elapsed.TotalSeconds:F2}s | ready={IsGameplayCacheReady} | " +
            $"levelStats={levelStatTable.Count} | enemySpawns={enemySpawnTable.Count} | rewards={stageRewardTable.Count} | " +
            $"dropBalances={stageDropBalanceTable.Count} | dialogues={dialogueTable.Count} | passives={passiveSkillTable.Count} | " +
            $"inventorySprites={inventorySpriteTable.Count} | sounds={soundTable.Count} | skills={cachedSkillData.Count} | " +
            $"stageConfigs={stageLevelConfigTable.Count}");
#endif
    }

    private struct GamePlayCacheLoadStep
    {
        public float progress;
        public string message;
        public Func<IEnumerator> load;
        public GamePlayCacheLoadStep(string Message, float Progress, Func<IEnumerator> Load)
        {
            progress = Progress;
            message = Message;
            load = Load;
        }
    }
    private IEnumerable<GamePlayCacheLoadStep> CreateGameplayCacheLoadList()
    {
        yield return new GamePlayCacheLoadStep("Loading player profile...", 0.10f, LoadPlayerProfileCache);
        yield return new GamePlayCacheLoadStep("Loading weapon enhance balance...", 0.16f, LoadWeaponEnhanceBalanceCache);
        yield return new GamePlayCacheLoadStep("Loading level stat tables...", 0.24f, ()=> LoadLabelCache<PlayerLevelStatSO>(PlayerLevelStatSO.AddressableLabel, handle => levelStatHandle = handle, BuildLevelStatCache, "PlayerLevelStat"));
        yield return new GamePlayCacheLoadStep("Loading enemy stage data...", 0.34f, ()=> LoadLabelCache<EnemySpawnData>(EnemyDataLabel, handle => enemyDataHandle = handle, BuildEnemyCache, "EnemySpawnData"));
        yield return new GamePlayCacheLoadStep("Loading stage reward data...", 0.42f, ()=> LoadOptionalLabelCache<StageRewardDataSO>(StageRewardDataLabel, handle => stageRewardDataHandle = handle, BuildStageRewardCache, "StageRewardData"));
        yield return new GamePlayCacheLoadStep("Loading stage drop balance...", 0.46f, ()=> LoadOptionalLabelCache<StageDropBalanceSO>(StageDropBalanceLabel, handle => stageDropBalanceHandle = handle, BuildStageDropBalanceCache, "StageDropBalance"));
        yield return new GamePlayCacheLoadStep("Loading dialogue data...", 0.50f, ()=> LoadLabelCache<DialogueData>(DialogueDataLabel, handle => dialogueDataHandle = handle, BuildDialogueCache, "DialogueData"));
        yield return new GamePlayCacheLoadStep("Loading speaker profiles...", 0.58f, ()=> LoadOptionalLabelCache<DialogueSpeakerProfileSO>(DialogueSpeakerLabel, handle => dialogueSpeakerHandle = handle, BuildDialogueSpeakerCache, "DialogueSpeakerProfile"));
        yield return new GamePlayCacheLoadStep("Loading dialogue portraits...", 0.64f, LoadDialoguePortraits);
        yield return new GamePlayCacheLoadStep("Loading passive data...", 0.70f, ()=> LoadLabelCache<PassiveSkillData>(PassiveSkillLabel, handle => passiveSkillHandle = handle, BuildPassiveCache, "PassiveSkillData"));
        yield return new GamePlayCacheLoadStep("Loading inventory sprites...", 0.76f, ()=> LoadLabelCache<Sprite>(InventorySpriteLabel, handle => inventorySpriteHandle = handle, BuildInventorySpriteCache, "InventorySprite"));
        yield return new GamePlayCacheLoadStep("Loading tutorial sprites...", 0.82f, LoadTutorialSpriteCache);
        yield return new GamePlayCacheLoadStep("Loading sound assets...", 0.88f, ()=> LoadLabelCache<SoundAsset>(SoundLabel, handle => soundAssetHandle = handle, BuildSoundCache, "SoundAsset"));
        yield return new GamePlayCacheLoadStep("Loading skill data...", 0.93f, ()=> LoadLabelCache<SkillData>(SkillDataLabel, handle => skillDataHandle = handle, BuildSkillCache, "SkillData"));
        yield return new GamePlayCacheLoadStep("Loading stage level configs...", 0.96f, ()=> LoadLabelCache<StageLevelConfigSO>(StageLevelConfigLabel, handle => stageLevelConfigHandle = handle, BuildStageLevelConfigCache, "StageLevelConfig"));
        yield return new GamePlayCacheLoadStep("Loading skill icons...", 0.97f, LoadSkillIcons);
    }

    private static void ReportGameplayCachePreloadStep(string message, float progress)
    {
        OnGameplayCachePreloadStep?.Invoke(message, Mathf.Clamp01(progress));
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogAssetLoad(string message)
    {
        Debug.Log($"{AssetLoadLogPrefix} {message}");
    }

    public void ClearGameplayCache()
    {
        ReleaseHandle(ref playerProfileHandle);
        ReleaseHandle(ref weaponEnhanceBalanceHandle);
        ReleaseHandle(ref levelStatHandle);
        ReleaseHandle(ref enemyDataHandle);
        ReleaseHandle(ref stageRewardDataHandle);
        ReleaseHandle(ref stageDropBalanceHandle);
        ReleaseHandle(ref dialogueDataHandle);
        ReleaseHandle(ref dialogueSpeakerHandle);
        ReleaseHandle(ref passiveSkillHandle);
        ReleaseHandle(ref inventorySpriteHandle);
        ReleaseHandle(ref soundAssetHandle);
        ReleaseHandle(ref skillDataHandle);
        ReleaseHandle(ref stageLevelConfigHandle);
        ReleaseHandle(ref tutorialSpriteLocationHandle);

        for (int i = 0; i < skillIconHandles.Count; i++)
        {
            AsyncOperationHandle<Sprite> handle = skillIconHandles[i];
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        skillIconHandles.Clear();

        for (int i = 0; i < dialoguePortraitHandles.Count; i++)
        {
            AsyncOperationHandle<Sprite> handle = dialoguePortraitHandles[i];
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        dialoguePortraitHandles.Clear();

        for (int i = 0; i < tutorialSpriteAssetHandles.Count; i++)
        {
            AsyncOperationHandle<Sprite> handle = tutorialSpriteAssetHandles[i];
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        tutorialSpriteAssetHandles.Clear();

        for (int i = 0; i < tutorialSpriteReferenceHandles.Count; i++)
        {
            AsyncOperationHandle<Sprite> handle = tutorialSpriteReferenceHandles[i];
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        tutorialSpriteReferenceHandles.Clear();

        playerProfile = null;
        weaponEnhanceBalance = null;
        statMultipliers = new WeaponStatMultiplierConfig();
        needExpTable = Array.Empty<int>();
        levelUpStatsTable = Array.Empty<LevelUpStats>();

        levelStatTable.Clear();
        enemySpawnTable.Clear();
        stageRewardTable.Clear();
        stageDropBalanceTable.Clear();
        dialogueTable.Clear();
        dialogueSpeakerTable.Clear();
        dialoguePortraitTable.Clear();
        passiveSkillTable.Clear();
        inventorySpriteTable.Clear();
        tutorialSpriteTable.Clear();
        soundTable.Clear();
        skillIconTable.Clear();
        cachedSkillDataList.Clear();
        cachedSkillData.Clear();
        stageLevelConfigTable.Clear();
        orderedStageLevelConfigs.Clear();

        IsGameplayCacheReady = false;
        isCacheLoading = false;
    }

    public SaveInfo CreateGameplaySaveSnapshot()
    {
        EnsurePersistentStateInitialized();

        SaveInfo snapshot = IsNewGamePending
            ? BuildNewGameSaveFromProfile()
            : CloneSave(committedSave ?? CurrentSave);

        snapshot.setting = CloneSettings(Settings);
        CurrentSave = CloneSave(snapshot);
        HasSaveFile = GameSaveFileService.Exists();
        IsNewGamePending = false;
        return CloneSave(snapshot);
    }

    public void UpdateRuntimeProgress(SaveInfo runtimeSave)
    {
        EnsurePersistentStateInitialized();

        SaveInfo safeSave = runtimeSave != null ? CloneSave(runtimeSave) : new SaveInfo();
        safeSave.setting = CloneSettings(Settings);

        if (string.IsNullOrWhiteSpace(safeSave.sceneName))
            safeSave.sceneName = DefaultGameplaySceneName;

        if (safeSave.nextBattleLevel <= 0)
            safeSave.nextBattleLevel = 1;

        CurrentSave = safeSave;
    }

    public void CaptureBattleAttemptSnapshot(SaveInfo runtimeSave)
    {
        UpdateRuntimeProgress(runtimeSave);
        battleAttemptSnapshot = CloneSave(CurrentSave);
    }

    public SaveInfo RestoreBattleAttemptSnapshot()
    {
        EnsurePersistentStateInitialized();

        if (battleAttemptSnapshot == null)
            return null;

        int currentEnhancementStone = CurrentSave != null ? CurrentSave.enhancementStone : 0;
        int currentSkillGem = CurrentSave != null ? CurrentSave.skillGem : 0;
        int currentHpStone = CurrentSave != null ? CurrentSave.hpStone : 0;

        CurrentSave = CloneSave(battleAttemptSnapshot);
        CurrentSave.enhancementStone = currentEnhancementStone;
        CurrentSave.skillGem = currentSkillGem;
        CurrentSave.hpStone = currentHpStone;
        return CloneSave(CurrentSave);
    }

    public void ClearBattleAttemptSnapshot()
    {
        battleAttemptSnapshot = null;
    }

    public bool HasShownLevelStartDialogue(int level)
    {
        return shownLevelStartDialogueLevels.Contains(Mathf.Max(1, level));
    }

    public void MarkLevelStartDialogueShown(int level)
    {
        shownLevelStartDialogueLevels.Add(Mathf.Max(1, level));
    }

    public bool HasShownAutoTutorial(int level)
    {
        return shownAutoTutorialLevels.Contains(Mathf.Max(1, level));
    }

    public void MarkAutoTutorialShown(int level)
    {
        shownAutoTutorialLevels.Add(Mathf.Max(1, level));
    }

    public void ResetBattlePresentationState()
    {
        shownLevelStartDialogueLevels.Clear();
        shownAutoTutorialLevels.Clear();
    }

    public void ReloadPersistentProgress()
    {
        UserSetting previousSettings = CloneSettings(Settings);
        LoadPersistentState();
        Settings = previousSettings ?? Settings;

        if (CurrentSave != null)
            CurrentSave.setting = CloneSettings(Settings);

        battleAttemptSnapshot = null;
        ResetBattlePresentationState();
    }

    public void SaveCurrentProgress(SaveInfo runtimeSave)
    {
        EnsurePersistentStateInitialized();

        SaveInfo safeSave = runtimeSave != null ? CloneSave(runtimeSave) : new SaveInfo();
        safeSave.setting = CloneSettings(Settings);
        CurrentSave = safeSave;
        committedSave = CloneSave(safeSave);
        battleAttemptSnapshot = null;
        ResetBattlePresentationState();

        if (IsBossTestSession)
            return;

        string json = JsonUtility.ToJson(CurrentSave);
        GameSaveFileService.SaveEncryptedJson(GameSaveFileService.DefaultSaveFileName, json, PlayerInfo.EncryptionKey);
        HasSaveFile = true;
    }

    public void SavePersistentMetaCurrencies(SaveInfo runtimeSave)
    {
        EnsurePersistentStateInitialized();

        SaveInfo currencySource = runtimeSave != null ? CloneSave(runtimeSave) : CloneSave(CurrentSave);
        SaveInfo baseSave = committedSave != null ? CloneSave(committedSave) : new SaveInfo();

        if (string.IsNullOrWhiteSpace(baseSave.sceneName))
            baseSave.sceneName = DefaultGameplaySceneName;

        if (baseSave.nextBattleLevel <= 0)
            baseSave.nextBattleLevel = 1;

        baseSave.setting = CloneSettings(Settings);
        baseSave.enhancementStone = currencySource.enhancementStone;
        baseSave.skillGem = currencySource.skillGem;
        baseSave.hpStone = currencySource.hpStone;

        committedSave = CloneSave(baseSave);

        if (CurrentSave != null)
        {
            CurrentSave.enhancementStone = currencySource.enhancementStone;
            CurrentSave.skillGem = currencySource.skillGem;
            CurrentSave.hpStone = currencySource.hpStone;
            CurrentSave.setting = CloneSettings(Settings);
        }

        if (IsBossTestSession)
            return;

        string json = JsonUtility.ToJson(committedSave);
        GameSaveFileService.SaveEncryptedJson(GameSaveFileService.DefaultSaveFileName, json, PlayerInfo.EncryptionKey);
        HasSaveFile = true;
    }

    public int GetNextBattleLevel()
    {
        EnsurePersistentStateInitialized();
        return CurrentSave != null && CurrentSave.nextBattleLevel > 0
            ? CurrentSave.nextBattleLevel
            : 1;
    }

    public void SetRuntimeNextBattleLevel(int nextBattleLevel)
    {
        EnsurePersistentStateInitialized();

        if (CurrentSave == null)
            CurrentSave = new SaveInfo();

        CurrentSave.nextBattleLevel = Mathf.Max(1, nextBattleLevel);
        if (string.IsNullOrWhiteSpace(CurrentSave.sceneName))
            CurrentSave.sceneName = DefaultGameplaySceneName;
    }

    public PlayerStatProfileSO GetPlayerProfileData() => playerProfile;
    public WeaponEnhanceBalanceSO GetWeaponEnhanceBalance() => weaponEnhanceBalance;
    public IReadOnlyDictionary<int, PlayerLevelStatSO> GetLevelStatTable() => levelStatTable;
    public int[] GetNeedExpTable() => needExpTable;
    public LevelUpStats[] GetLevelUpStatsTable() => levelUpStatsTable;
    public WeaponStatMultiplierConfig GetWeaponStatMultipliers() => statMultipliers ?? new WeaponStatMultiplierConfig();

    public EnemySpawnData GetEnemySpawnData(int level)
    {
        enemySpawnTable.TryGetValue(level, out EnemySpawnData data);
        return data;
    }

    public IReadOnlyDictionary<int, EnemySpawnData> GetEnemySpawnDataMap() => enemySpawnTable;

    public StageRewardDataSO GetStageRewardData(int level)
    {
        stageRewardTable.TryGetValue(level, out StageRewardDataSO data);
        return data;
    }

    public IReadOnlyDictionary<int, StageRewardDataSO> GetStageRewardDataMap() => stageRewardTable;

    public StageDropBalanceSO GetStageDropBalance(int level)
    {
        stageDropBalanceTable.TryGetValue(level, out StageDropBalanceSO data);
        return data;
    }

    public IReadOnlyDictionary<int, StageDropBalanceSO> GetStageDropBalanceMap() => stageDropBalanceTable;

    public DialogueData GetDialogueData(int level)
    {
        dialogueTable.TryGetValue(level, out DialogueData data);
        return data;
    }

    public IReadOnlyDictionary<int, DialogueData> GetDialogueDataMap() => dialogueTable;

    public StageLevelConfigSO GetStageLevelConfig(int level)
    {
        stageLevelConfigTable.TryGetValue(level, out StageLevelConfigSO config);
        return config;
    }

    public bool TryGetStageLevelConfig(int level, out StageLevelConfigSO config)
    {
        return stageLevelConfigTable.TryGetValue(level, out config) && config != null;
    }

    public IReadOnlyList<StageLevelConfigSO> GetAllStageLevelConfigs() => orderedStageLevelConfigs;

    public DialogueSpeakerProfileSO GetDialogueSpeakerProfile(DialogueSpeakerType speakerType)
    {
        dialogueSpeakerTable.TryGetValue(speakerType, out DialogueSpeakerProfileSO profile);
        return profile;
    }

    public bool TryGetDialoguePortrait(DialogueSpeakerType speakerType, DialoguePortraitVariant portraitVariant, out Sprite sprite)
    {
        sprite = null;

        string variantKey = BuildDialoguePortraitKey(speakerType, portraitVariant);
        if (dialoguePortraitTable.TryGetValue(variantKey, out sprite) && sprite != null)
            return true;

        string defaultKey = BuildDialoguePortraitKey(speakerType, DialoguePortraitVariant.Default);
        return dialoguePortraitTable.TryGetValue(defaultKey, out sprite) && sprite != null;
    }

    public PassiveSkillData GetPassiveSkill(PassiveSkillType type)
    {
        passiveSkillTable.TryGetValue(type, out PassiveSkillData data);
        return data;
    }

    public IReadOnlyDictionary<PassiveSkillType, PassiveSkillData> GetPassiveSkillMap() => passiveSkillTable;

    public Sprite GetInventorySprite(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        inventorySpriteTable.TryGetValue(key, out Sprite sprite);
        return sprite;
    }

    public IReadOnlyDictionary<string, Sprite> GetInventorySpriteMap() => inventorySpriteTable;

    public bool TryGetTutorialSprite(AssetReferenceSprite assetReference, out Sprite sprite)
    {
        sprite = null;

        if (assetReference == null || !assetReference.RuntimeKeyIsValid())
            return false;

        string runtimeKey = assetReference.RuntimeKey.ToString();
        string guidKey = assetReference.AssetGUID;

        if (!string.IsNullOrWhiteSpace(runtimeKey) && tutorialSpriteTable.TryGetValue(runtimeKey, out sprite))
            return true;

        if (!string.IsNullOrWhiteSpace(guidKey) && tutorialSpriteTable.TryGetValue(guidKey, out sprite))
            return true;

        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(assetReference);
        Sprite loadedSprite = handle.WaitForCompletion();
        if (handle.Status != AsyncOperationStatus.Succeeded || loadedSprite == null)
        {
            if (handle.IsValid())
                Addressables.Release(handle);

            return false;
        }

        tutorialSpriteReferenceHandles.Add(handle);
        CacheTutorialSprite(runtimeKey, guidKey, loadedSprite);
        sprite = loadedSprite;
        return true;
    }

    public IReadOnlyDictionary<string, Sprite> GetTutorialSpriteMap() => tutorialSpriteTable;

    public SoundAsset GetSoundAsset(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        soundTable.TryGetValue(key, out SoundAsset soundAsset);
        return soundAsset;
    }

    public IReadOnlyDictionary<string, SoundAsset> GetSoundAssetMap() => soundTable;

    public bool TryGetSkillIcon(AssetReferenceSprite assetReference, out Sprite sprite)
    {
        sprite = null;

        if (assetReference == null)
            return false;

        string guid = assetReference.AssetGUID;
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        return skillIconTable.TryGetValue(guid, out sprite);
    }

    private void EnsurePersistentStateInitialized()
    {
        if (Settings == null || CurrentSave == null)
        {
            LoadPersistentState();
        }
    }

    private void LoadPersistentState()
    {
        Settings = GameSettingsService.Load();

        if (GameSaveFileService.Exists())
        {
            try
            {
                string loadedJson = GameSaveFileService.LoadDecryptedJson(GameSaveFileService.DefaultSaveFileName, PlayerInfo.EncryptionKey);
                SaveInfo save = string.IsNullOrWhiteSpace(loadedJson) ? null : JsonUtility.FromJson<SaveInfo>(loadedJson);
                CurrentSave = save ?? new SaveInfo();
                HasSaveFile = save != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"GameSession: save load failed - {exception.Message}");
                CurrentSave = new SaveInfo();
                HasSaveFile = false;
            }
        }
        else
        {
            CurrentSave = new SaveInfo();
            HasSaveFile = false;
        }

        if (CurrentSave.setting == null)
            CurrentSave.setting = CloneSettings(Settings);

        if (string.IsNullOrWhiteSpace(CurrentSave.sceneName))
            CurrentSave.sceneName = DefaultGameplaySceneName;

        if (CurrentSave.nextBattleLevel <= 0)
            CurrentSave.nextBattleLevel = 1;

        if (Settings == null)
            Settings = CurrentSave.setting ?? new UserSetting();

        committedSave = CloneSave(CurrentSave);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsBossTestSession || !string.Equals(scene.name, "GameTitle", StringComparison.OrdinalIgnoreCase))
            return;

        IsBossTestSession = false;
        ReloadPersistentProgress();
        ClearGameplayCache();
    }

    private SaveInfo BuildBossTestSave(BossTestProfileSO profile)
    {
        int maxHp = Mathf.Max(1, profile.startMaxHp);
        int maxStamina = Mathf.Max(1, profile.startMaxStamina);
        int equippedWeaponIndex = Mathf.Clamp(profile.equippedWeaponIndex, 0, 13);
        SaveInfo save = new SaveInfo
        {
            setting = CloneSettings(Settings),
            enhancementStone = Mathf.Max(0, profile.startEnhancementStone),
            skillGem = Mathf.Max(0, profile.startSkillGem),
            hpStone = Mathf.Max(0, profile.startHpStone),
            level = Mathf.Max(1, profile.startLevel),
            exp = Mathf.Max(0, profile.startExp),
            hp = Mathf.Clamp(profile.startHp, 0, maxHp),
            maxhp = maxHp,
            damage = Mathf.Max(0, profile.startAttackDamage),
            stamina = Mathf.Clamp(profile.startStamina, 0, maxStamina),
            maxstamina = maxStamina,
            defense = Mathf.Max(0, profile.startDefense),
            criticalChance = Mathf.Max(0, profile.startCriticalChance),
            staminaGainOnAttack = Mathf.Max(0, profile.startStaminaGainOnAttack),
            equipSkill = CopyStringArray(profile.equippedSkills, 8),
            equipPassiveSkill = CopyStringArray(profile.equippedPassiveSkills, 5),
            learnedSkills = profile.learnedSkills != null
                ? profile.learnedSkills.Where(skill => !string.IsNullOrWhiteSpace(skill)).ToList()
                : new List<string>(),
            stageCheck = new StageCheck(),
            checkPoint = CheckPointSection.Start,
            sectionClear = false,
            sceneName = BossSceneFlow.BossSceneName,
            nextBattleLevel = 1,
            dialogueIndex = 0,
            WeaponInventory = CreateDefaultWeaponInventory(14),
            weapon_Ability = CreateDefaultWeaponAbilityInventory(16),
            equipWeapon = equippedWeaponIndex,
            summonInven = CreateDefaultSummonData(),
            summonStamina = 0,
        };

        save.WeaponInventory[equippedWeaponIndex] = CloneWeaponData(profile.equippedWeapon);
        return save;
    }

    private IEnumerator EnsureAddressablesInitializedCoroutine()
    {
        if (isAddressablesInitialized)
            yield break;

        if (!isAddressablesInitializationRequested)
        {
            PrepareEditorAddressablesRuntimePath();
            addressablesInitializeHandle = Addressables.InitializeAsync(false);
            isAddressablesInitializationRequested = true;
        }

        yield return addressablesInitializeHandle;

        bool isHandleValid = addressablesInitializeHandle.IsValid();
        AsyncOperationStatus status = isHandleValid
            ? addressablesInitializeHandle.Status
            : AsyncOperationStatus.Failed;

        isAddressablesInitialized = isHandleValid &&
            status == AsyncOperationStatus.Succeeded;

        if (!isAddressablesInitialized)
        {
            string exceptionMessage = "Addressables initialization handle is invalid.";
            if (isHandleValid)
            {
                exceptionMessage = addressablesInitializeHandle.OperationException != null
                    ? addressablesInitializeHandle.OperationException.ToString()
                    : "No exception message was provided by Addressables.";
            }

            isAddressablesInitializationRequested = false;

            Debug.LogError(
                "GameSession: Addressables initialization failed.\n" +
                $"HandleValid: {isHandleValid}\n" +
                $"Status: {status}\n" +
                $"Exception: {exceptionMessage}\n" +
                "Check whether Addressables content has been built and included for the current target."
            );
        }
    }

    private IEnumerator LoadPlayerProfileCache()
    {
        if (playerProfile != null)
            yield break;

        playerProfileHandle = Addressables.LoadAssetAsync<PlayerStatProfileSO>(PlayerStatProfileSO.DefaultAddress);
        yield return playerProfileHandle;

        if (playerProfileHandle.Status != AsyncOperationStatus.Succeeded || playerProfileHandle.Result == null)
        {
            Debug.LogError($"{AssetLoadLogPrefix} ERROR | failed to load PlayerStatProfileSO. address={PlayerStatProfileSO.DefaultAddress}");
            yield break;
        }

        playerProfile = playerProfileHandle.Result;
        statMultipliers = playerProfile.statMultipliers ?? new WeaponStatMultiplierConfig();
        LogAssetLoad($"LOADED PlayerStatProfileSO | key={PlayerStatProfileSO.DefaultAddress} | assets=1 | success=true");
    }

    private IEnumerator LoadWeaponEnhanceBalanceCache()
    {
        if (weaponEnhanceBalance != null)
            yield break;

        weaponEnhanceBalanceHandle = Addressables.LoadAssetAsync<WeaponEnhanceBalanceSO>(WeaponEnhanceBalanceSO.DefaultAddress);
        yield return weaponEnhanceBalanceHandle;

        if (weaponEnhanceBalanceHandle.Status != AsyncOperationStatus.Succeeded || weaponEnhanceBalanceHandle.Result == null)
        {
            Debug.LogError($"{AssetLoadLogPrefix} ERROR | failed to load WeaponEnhanceBalanceSO. address={WeaponEnhanceBalanceSO.DefaultAddress}");
            yield break;
        }

        weaponEnhanceBalance = weaponEnhanceBalanceHandle.Result;
        LogAssetLoad($"LOADED WeaponEnhanceBalanceSO | key={WeaponEnhanceBalanceSO.DefaultAddress} | assets=1 | success=true");
    }

    private IEnumerator LoadLabelCache<T>(
        object key,
        Action<AsyncOperationHandle<IList<T>>> cacheHandle,
        Action<IList<T>> buildAction,
        string debugName)
    {
        AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(key, null);
        cacheHandle?.Invoke(handle);
        yield return handle;

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogError($"{AssetLoadLogPrefix} ERROR | failed to load {debugName}. key={key}");
            yield break;
        }

        buildAction?.Invoke(handle.Result);
        LogAssetLoad($"LOADED {debugName} | key={key} | assets={handle.Result.Count} | success=true");
    }

    private IEnumerator LoadOptionalLabelCache<T>(
        object key,
        Action<AsyncOperationHandle<IList<T>>> cacheHandle,
        Action<IList<T>> buildAction,
        string debugName)
    {
        AsyncOperationHandle<IList<IResourceLocation>> locationHandle =
            Addressables.LoadResourceLocationsAsync(key, typeof(T));
        yield return locationHandle;

        bool hasLocations = locationHandle.Status == AsyncOperationStatus.Succeeded &&
            locationHandle.Result != null &&
            locationHandle.Result.Count > 0;

        if (!hasLocations)
        {
            if (locationHandle.IsValid())
                Addressables.Release(locationHandle);

            Debug.LogWarning($"{AssetLoadLogPrefix} WARNING | optional cache skipped because no Addressables were found. key={key}, type={debugName}");
            yield break;
        }

        if (locationHandle.IsValid())
            Addressables.Release(locationHandle);

        yield return LoadLabelCache(key, cacheHandle, buildAction, debugName);
    }

    private void BuildLevelStatCache(IList<PlayerLevelStatSO> loadedAssets)
    {
        levelStatTable.Clear();

        foreach (PlayerLevelStatSO levelStat in loadedAssets)
        {
            if (levelStat == null)
                continue;

            levelStatTable[levelStat.level] = levelStat;
        }

        int maxLevel = levelStatTable.Count > 0 ? levelStatTable.Keys.Max() : 0;
        needExpTable = new int[maxLevel];
        levelUpStatsTable = new LevelUpStats[maxLevel];

        foreach (KeyValuePair<int, PlayerLevelStatSO> pair in levelStatTable)
        {
            int index = pair.Key - 1;
            if (index < 0 || index >= maxLevel || pair.Value == null || pair.Value.levelStats == null)
                continue;

            needExpTable[index] = pair.Value.needExpToNextLevel;
            levelUpStatsTable[index] = new LevelUpStats
            {
                maxhp = pair.Value.levelStats.maxhp,
                damage = pair.Value.levelStats.damage,
                maxstamina = pair.Value.levelStats.maxstamina,
                defense = pair.Value.levelStats.defense,
                criticalChance = pair.Value.levelStats.criticalChance
            };
        }
    }

    private void BuildEnemyCache(IList<EnemySpawnData> loadedAssets)
    {
        enemySpawnTable.Clear();
        foreach (EnemySpawnData data in loadedAssets)
        {
            if (data != null)
                enemySpawnTable[data.SpawnLevel] = data;
        }
    }

    private void BuildStageRewardCache(IList<StageRewardDataSO> loadedAssets)
    {
        stageRewardTable.Clear();
        foreach (StageRewardDataSO data in loadedAssets)
        {
            if (data == null)
                continue;

            stageRewardTable[data.rewardLevel] = data;
        }
    }

    private void BuildStageDropBalanceCache(IList<StageDropBalanceSO> loadedAssets)
    {
        stageDropBalanceTable.Clear();
        foreach (StageDropBalanceSO data in loadedAssets)
        {
            if (data == null)
                continue;

            stageDropBalanceTable[data.battleLevel] = data;
        }
    }

    private void BuildDialogueCache(IList<DialogueData> loadedAssets)
    {
        dialogueTable.Clear();
        foreach (DialogueData data in loadedAssets)
        {
            if (data != null)
                dialogueTable[data.SpawnLevel] = data;
        }
    }

    private void BuildDialogueSpeakerCache(IList<DialogueSpeakerProfileSO> loadedAssets)
    {
        dialogueSpeakerTable.Clear();
        dialoguePortraitTable.Clear();

        foreach (DialogueSpeakerProfileSO profile in loadedAssets)
        {
            if (profile == null)
                continue;

            dialogueSpeakerTable[profile.speakerType] = profile;
        }
    }

    private void BuildPassiveCache(IList<PassiveSkillData> loadedAssets)
    {
        passiveSkillTable.Clear();
        foreach (PassiveSkillData data in loadedAssets)
        {
            if (data != null)
                passiveSkillTable[data.skillType] = data;
        }
    }

    private void BuildInventorySpriteCache(IList<Sprite> loadedAssets)
    {
        inventorySpriteTable.Clear();
        foreach (Sprite sprite in loadedAssets)
        {
            if (sprite != null && !string.IsNullOrWhiteSpace(sprite.name))
                inventorySpriteTable[sprite.name] = sprite;
        }
    }

    private IEnumerator LoadDialoguePortraits()
    {
        dialoguePortraitTable.Clear();

        if (dialogueSpeakerTable.Count == 0)
        {
            LogAssetLoad("LOADED DialoguePortrait | cached=0 | success=true");
            yield break;
        }

        HashSet<string> loadedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DialogueSpeakerProfileSO profile in dialogueSpeakerTable.Values)
        {
            if (profile == null)
                continue;

            yield return LoadDialoguePortraitReference(profile.speakerType, DialoguePortraitVariant.Default, profile.defaultPortrait, loadedKeys);

            if (profile.variantPortraits == null)
                continue;

            for (int i = 0; i < profile.variantPortraits.Length; i++)
            {
                DialoguePortraitEntry entry = profile.variantPortraits[i];
                if (entry == null)
                    continue;

                yield return LoadDialoguePortraitReference(profile.speakerType, entry.portraitVariant, entry.portraitReference, loadedKeys);
            }
        }

        LogAssetLoad($"LOADED DialoguePortrait | cached={dialoguePortraitTable.Count} | success=true");
    }

    private IEnumerator LoadDialoguePortraitReference(
        DialogueSpeakerType speakerType,
        DialoguePortraitVariant portraitVariant,
        AssetReferenceSprite portraitReference,
        HashSet<string> loadedKeys)
    {
        if (portraitReference == null || !portraitReference.RuntimeKeyIsValid())
            yield break;

        string key = BuildDialoguePortraitKey(speakerType, portraitVariant);
        if (!loadedKeys.Add(key))
            yield break;

        AsyncOperationHandle<Sprite> portraitHandle = Addressables.LoadAssetAsync<Sprite>(portraitReference);
        dialoguePortraitHandles.Add(portraitHandle);
        yield return portraitHandle;

        if (portraitHandle.Status != AsyncOperationStatus.Succeeded || portraitHandle.Result == null)
        {
            Debug.LogWarning($"{AssetLoadLogPrefix} WARNING | failed to load dialogue portrait. speakerType={speakerType}, variant={portraitVariant}");
            yield break;
        }

        dialoguePortraitTable[key] = portraitHandle.Result;
    }

    private IEnumerator LoadTutorialSpriteCache()
    {
        tutorialSpriteTable.Clear();

        tutorialSpriteLocationHandle = Addressables.LoadResourceLocationsAsync(TutorialSpriteLabel, typeof(Sprite));
        yield return tutorialSpriteLocationHandle;

        if (tutorialSpriteLocationHandle.Status != AsyncOperationStatus.Succeeded || tutorialSpriteLocationHandle.Result == null)
        {
            Debug.LogError($"{AssetLoadLogPrefix} ERROR | failed to load tutorial sprite locations. key={TutorialSpriteLabel}");
            yield break;
        }

        IList<IResourceLocation> locations = tutorialSpriteLocationHandle.Result;
        for (int i = 0; i < locations.Count; i++)
        {
            IResourceLocation location = locations[i];
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey))
                continue;

            AsyncOperationHandle<Sprite> spriteHandle = Addressables.LoadAssetAsync<Sprite>(location);
            tutorialSpriteAssetHandles.Add(spriteHandle);
            yield return spriteHandle;

            if (spriteHandle.Status != AsyncOperationStatus.Succeeded || spriteHandle.Result == null)
            {
                Debug.LogWarning($"{AssetLoadLogPrefix} WARNING | failed to load tutorial sprite. key={location.PrimaryKey}");
                continue;
            }

            CacheTutorialSprite(location.PrimaryKey, null, spriteHandle.Result);
        }

        LogAssetLoad($"LOADED TutorialSprite | cached={tutorialSpriteTable.Count} | success=true");
    }

    private void CacheTutorialSprite(string runtimeKey, string guidKey, Sprite sprite)
    {
        if (sprite == null)
            return;

        if (!string.IsNullOrWhiteSpace(runtimeKey))
            tutorialSpriteTable[runtimeKey] = sprite;

        if (!string.IsNullOrWhiteSpace(guidKey))
            tutorialSpriteTable[guidKey] = sprite;

        if (!string.IsNullOrWhiteSpace(sprite.name))
            tutorialSpriteTable[sprite.name] = sprite;
    }

    private void BuildSoundCache(IList<SoundAsset> loadedAssets)
    {
        soundTable.Clear();
        foreach (SoundAsset soundAsset in loadedAssets)
        {
            if (soundAsset == null)
                continue;

            string key = soundAsset.KeyName;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            soundTable[key] = soundAsset;
        }
    }

    private void BuildSkillCache(IList<SkillData> loadedAssets)
    {
        cachedSkillDataList.Clear();
        cachedSkillData.Clear();
        foreach (SkillData skillData in loadedAssets)
        {
            if (skillData == null)
                continue;

            cachedSkillDataList.Add(skillData);
            if (!cachedSkillData.ContainsKey(skillData.skillType))
                cachedSkillData[skillData.skillType] = skillData;
        }
    }

    private void BuildStageLevelConfigCache(IList<StageLevelConfigSO> loadedAssets)
    {
        stageLevelConfigTable.Clear();
        orderedStageLevelConfigs.Clear();

        foreach (StageLevelConfigSO config in loadedAssets)
        {
            if (config == null)
                continue;

            int level = Mathf.Max(1, config.level);
            if (stageLevelConfigTable.ContainsKey(level))
            {
                Debug.LogWarning($"GameSession: duplicate stage level config detected. level={level}, keeping first asset '{stageLevelConfigTable[level].name}' and ignoring '{config.name}'.");
                continue;
            }

            stageLevelConfigTable[level] = config;
            orderedStageLevelConfigs.Add(config);
        }

        orderedStageLevelConfigs.Sort((left, right) =>
        {
            int leftLevel = left != null ? left.level : int.MaxValue;
            int rightLevel = right != null ? right.level : int.MaxValue;
            return leftLevel.CompareTo(rightLevel);
        });
    }

    private IEnumerator LoadSkillIcons()
    {
        skillIconTable.Clear();
        HashSet<string> requestedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SkillData skillData in cachedSkillDataList)
        {
            if (skillData == null ||
                skillData.skillIcon_address == null ||
                string.IsNullOrWhiteSpace(skillData.skillIcon_address.AssetGUID) ||
                !skillData.skillIcon_address.RuntimeKeyIsValid())
            {
                continue;
            }

            string iconGuid = skillData.skillIcon_address.AssetGUID;
            if (!requestedGuids.Add(iconGuid))
            {
                if (skillIconTable.TryGetValue(iconGuid, out Sprite cachedSprite))
                    skillData.skillIcon = cachedSprite;

                continue;
            }

            AsyncOperationHandle<Sprite> iconHandle = Addressables.LoadAssetAsync<Sprite>(skillData.skillIcon_address);
            skillIconHandles.Add(iconHandle);
            yield return iconHandle;

            if (iconHandle.Status != AsyncOperationStatus.Succeeded || iconHandle.Result == null)
            {
                Debug.LogWarning($"{AssetLoadLogPrefix} WARNING | failed to load skill icon for {skillData.skillType}");
                continue;
            }

            skillData.skillIcon = iconHandle.Result;
            skillIconTable[iconGuid] = iconHandle.Result;
        }

        LogAssetLoad($"LOADED SkillIcon | cached={skillIconTable.Count} | success=true");
    }

    private SaveInfo BuildNewGameSaveFromProfile()
    {
        if (playerProfile == null)
        {
            Debug.LogError("GameSession: cannot build new game save before player profile cache is loaded.");
            return CloneSave(CurrentSave);
        }

        SaveInfo save = new SaveInfo
        {
            setting = CloneSettings(Settings),
            enhancementStone = playerProfile.startEnhancementStone,
            skillGem = playerProfile.startSkillGem,
            hpStone = playerProfile.startHpStone,
            level = playerProfile.startLevel,
            exp = playerProfile.startExp,
            hp = playerProfile.startMaxHP,
            maxhp = playerProfile.startMaxHP,
            damage = playerProfile.startAttackDamage,
            stamina = playerProfile.startStamina,
            maxstamina = playerProfile.startMaxStamina,
            defense = playerProfile.startDefense,
            criticalChance = playerProfile.startCriticalChance,
            staminaGainOnAttack = playerProfile.startStaminaGainOnAttack,
            equipSkill = CopyStringArray(playerProfile.startEquipSkills, 8),
            equipPassiveSkill = CopyStringArray(playerProfile.startEquipPassiveSkills, 5),
            learnedSkills = CreateLearnedSkillList(playerProfile.startLearnedSkills),
            stageCheck = new StageCheck(),
            checkPoint = CheckPointSection.Start,
            sectionClear = false,
            sceneName = DefaultGameplaySceneName,
            nextBattleLevel = 1,
            dialogueIndex = 0,
            WeaponInventory = CreateDefaultWeaponInventory(14),
            weapon_Ability = CreateDefaultWeaponAbilityInventory(16),
            equipWeapon = Mathf.Clamp(playerProfile.startEquipWeaponIndex, 0, 13),
            summonInven = CreateDefaultSummonData(),
            summonStamina = 0,
        };

        save.WeaponInventory[save.equipWeapon] = CloneWeaponData(playerProfile.startWeapon);
        return save;
    }

    private static string[] CopyStringArray(string[] source, int length)
    {
        string[] result = new string[length];
        if (source == null)
            return result;

        Array.Copy(source, result, Mathf.Min(source.Length, length));
        return result;
    }

    private static List<string> CreateLearnedSkillList(string[] source)
    {
        List<string> result = new();
        if (source == null)
            return result;

        for (int i = 0; i < source.Length; i++)
        {
            string value = source[i];
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        return result;
    }

    private static List<WeaponData> CreateDefaultWeaponInventory(int size)
    {
        List<WeaponData> result = new List<WeaponData>(size);
        for (int i = 0; i < size; i++)
        {
            result.Add(new WeaponData());
        }

        return result;
    }

    private static List<WeaponAbility> CreateDefaultWeaponAbilityInventory(int size)
    {
        List<WeaponAbility> result = new List<WeaponAbility>(size);
        for (int i = 0; i < size; i++)
        {
            result.Add(new WeaponAbility());
        }

        return result;
    }

    private static SummonData CreateDefaultSummonData()
    {
        SummonData data = new SummonData();
        for (int i = 0; i < 4; i++)
        {
            data.equitabilityStoneCounts.Add(new AbilityStoneCount
            {
                abilityType = SummonAbilityStoneType.None,
                abilityValue = SummonAbilityStoneValues.Value0,
                count = 0
            });
        }

        return data;
    }

    private static WeaponData CloneWeaponData(WeaponData source)
    {
        if (source == null)
            return new WeaponData();

        WeaponData clone = new WeaponData
        {
            weaponName = source.weaponName,
            rank = source.rank,
            grade = source.grade,
            damage = source.damage,
            hp = source.hp,
            defense = source.defense,
            staminaRecovery = source.staminaRecovery,
            criticalChance = source.criticalChance,
            drain = source.drain,
            used = source.used
        };

        int abilityLength = source.weaponAbilityData != null && source.weaponAbilityData.Length > 0
            ? source.weaponAbilityData.Length
            : 3;

        clone.weaponAbilityData = new WeaponAbility[abilityLength];
        for (int i = 0; i < abilityLength; i++)
        {
            WeaponAbility ability = source.weaponAbilityData != null && i < source.weaponAbilityData.Length
                ? source.weaponAbilityData[i]
                : null;

            clone.weaponAbilityData[i] = ability == null
                ? new WeaponAbility()
                : new WeaponAbility
                {
                    weaponAbilityType = ability.weaponAbilityType,
                    weaponAbilityValues = ability.weaponAbilityValues,
                    count = ability.count
                };
        }

        return clone;
    }

    private static SaveInfo CloneSave(SaveInfo source)
    {
        if (source == null)
            return new SaveInfo();

        return JsonUtility.FromJson<SaveInfo>(JsonUtility.ToJson(source));
    }

    private static UserSetting CloneSettings(UserSetting source)
    {
        if (source == null)
            return new UserSetting();

        return new UserSetting
        {
            screenMode = source.screenMode,
            resolution = source.resolution,
            BGM = source.BGM,
            NormalSFX = source.NormalSFX,
            SkillSFX = source.SkillSFX
        };
    }

    private static void ApplyGraphicsSettings(UserSetting source)
    {
        source ??= new UserSetting();

        int width = 1920;
        int height = 1080;

        switch (source.resolution)
        {
            case Resolution.QHD:
                width = 2560;
                height = 1440;
                break;
            case Resolution.UHD:
                width = 3840;
                height = 2160;
                break;
        }

        bool isFullScreen = source.screenMode == ScreenMode.FullScreen;
        Screen.SetResolution(width, height, isFullScreen);
    }

    private static string BuildDialoguePortraitKey(DialogueSpeakerType speakerType, DialoguePortraitVariant portraitVariant)
    {
        return $"{speakerType}:{portraitVariant}";
    }

    private static void ReleaseHandle<T>(ref AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
            Addressables.Release(handle);

        handle = default;
    }

    private static void ReleaseHandle(ref AsyncOperationHandle handle)
    {
        if (handle.IsValid())
            Addressables.Release(handle);

        handle = default;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void PrepareEditorAddressablesRuntimePath()
    {
#if UNITY_EDITOR
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return;

        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(settings, out string guid, out long _))
        {
            string runtimeDataPath = $"GUID:{guid}";
            if (PlayerPrefs.GetString(Addressables.kAddressablesRuntimeDataPath, string.Empty) != runtimeDataPath)
            {
                PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, runtimeDataPath);
                PlayerPrefs.Save();
            }
        }
#endif
    }
}

