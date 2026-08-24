using UnityEngine;
using UnityEngine.SceneManagement;
using System.Security.Cryptography;
using System.Text;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.VisualScripting;
using System.Linq;
public enum ScreenMode
{
    Window,
    FullScreen
}
public enum Resolution
{
    FHD,   // 1920 x 1080
    QHD,   // 2560 x 1440
    UHD    // 3840 x 2160
}
[Serializable]
public class UserSetting
{
    public ScreenMode screenMode = ScreenMode.FullScreen;
    public Resolution resolution = Resolution.FHD;
    public float BGM = 0.5f;
    public float NormalSFX = 0.5f;
    public float SkillSFX = 0.5f;
}
public enum CheckPointSection 
{
    Start,Section1, Section2, Section3, Section4, Section5, Section6, Section7, Section8, Section9, Section10, Section11, Section12 }
public enum SFXType
{
    BGM,
    NormalSFX,
    SkillSFX,
}
[Serializable]
public class LevelUpStats
{
    public int maxhp = 320;
    public int damage = 20;
    public int maxstamina = 100;
    public int staminaGainOnAttack = 6;
    public int defense = 5;
    public int criticalChance = 10;
}
[Serializable]
public class SummonTypeCount
{
    public SummonType summonType = SummonType.None;
    public int count;
}
[Serializable]
public class AbilityStoneCount
{
    public SummonAbilityStoneType abilityType;
    public SummonAbilityStoneValues abilityValue;
    public int count;
}

[Serializable]
public class SummonData
{
    public SummonType eqiutSummonType = SummonType.None;
    public List<AbilityStoneCount> equitabilityStoneCounts = new List<AbilityStoneCount>();
    public int summonStone = 0;
    public List<SummonTypeCount> summonTypeCounts = new List<SummonTypeCount>();

    public List<AbilityStoneCount> abilityStoneCounts = new List<AbilityStoneCount>();
}
[Serializable]
public enum Stage
{
    Jimin_Stage,
    Stage1,
    Stage2,
    Stage3,
    StageTest,
    Preview,
}

[System.Serializable]
public class StageBool
{
    public Stage stage;
    public bool isFirst = true;
}

[System.Serializable]
public class StageCheck
{
    public List<StageBool> stageBools = new List<StageBool>();

    public bool IsFirst(Stage stage)
    {
        var found = stageBools.Find(s => s.stage == stage);
        return found == null || found.isFirst;
    }

    public void SetVisited(Stage stage)
    {
        var found = stageBools.Find(s => s.stage == stage);
        if (found != null)
            found.isFirst = false;
        else
            stageBools.Add(new StageBool { stage = stage, isFirst = false });
    }
}

[Serializable]
public class WeaponStatMultiplierConfig
{
    public float damageMultiplierBase = 1.25f;
    public float defenseMultiplierBase = 1.15f;
    public float hpMultiplierBase = 1.2f;
    public float criticalMultiplierBase = 1.1f;
    public float staminaRecoveryMultiplierBase = 1.05f;
    public float drainMultiplierBase = 1.05f;

    public float GetMultiplier(float baseValue, int grade)
    {
        return Mathf.Pow(baseValue, grade);
    }
}

[Serializable]
public struct DefenseMitigationProfile
{
    public float reductionCurveBase;
    public float percentReductionWeight;
    public float flatReductionWeight;
}

[Serializable]
public class SaveInfo

{
    public UserSetting setting = new UserSetting();

    [Header("UseItem")]
    public int enhancementStone = 0;
    public int skillGem = 0;
    public int hpStone = 0;

    [Header("SummonInventory")]
    public SummonData summonInven = new SummonData();
    public int summonStamina;

    [Header("Saveplayerstatus")]
    public int level = 1;
    public int hp = 300;
    public int maxhp = 300;
    public int damage = 15;
    public int stamina = 0;
    public int maxstamina = 100;
    public int shield = 0;
    public int defense = 5;
    public int criticalChance = 10;
    public int exp = 0;

    internal int staminaGainOnAttack = 5;
    public string[] equipSkill = new string[8];
    public string[] equipPassiveSkill = new string[5];
    public List<string> learnedSkills = new();
    public List<string> viewedTutorialIds = new();
    public List<string> playedDialogueEventIds = new();
    public List<string> dialogueStateFlags = new();

    [Header("PlayerLocation")]
    public string sceneName = "Jimin_Stage";
    public int nextBattleLevel = 1;
    public CheckPointSection checkPoint;
    public StageCheck stageCheck;
    public bool sectionClear = false;
    public int dialogueIndex = 0;

    [Header("PlayerInventory")]
    public List<WeaponData> WeaponInventory = new List<WeaponData>(new WeaponData[14]);
    public List<WeaponAbility> weapon_Ability = new(new WeaponAbility[16]);
    public int equipWeapon;
    public bool BGM = true;
    public bool SFX = true;
}
[Serializable]
public class PlayerInfo : MonoBehaviour
{
    public static PlayerInfo Instance { get; private set; }
    public const string EncryptionKey = "REDACTED_FOR_PUBLIC_PORTFOLIO";

    public static event Action OnChangeStatus;
    public static event Action OnProfileLoaded;

    public SaveInfo saveInfo = new();

    public UserSetting setting = new();
    private bool shouldRefillHpOnSessionInitialize;

    private PlayerStatProfileSO statProfile;
    private readonly Dictionary<int, PlayerLevelStatSO> levelStatTable = new Dictionary<int, PlayerLevelStatSO>();
    public bool isProfileLoaded { get; private set; }

    [Header("UseItem")]
    public int enhancementStone = 0;
    public int skillGem = 0;
    public int hpStone = 0;

    public StageCheck stageCheck;
    public string sceneName = GameSession.DefaultGameplaySceneName;
    public int nextBattleLevel = 1;
    public CheckPointSection checkPoint;
    public bool sectionClear = false;

    [Header("playerNeedExp")]
    public int[] needExp;
    public LevelUpStats[] levelUpStats;

    [Header("playerStatus")]
    public int level = 1;
    public int currentHP = 320;
    public int maxHP = 320;
    public int attackDamage = 20;
    public int stamina = 0;
    public int maxstamina = 100;
    public int defense = 5;
    public int criticalChance = 10;
    public int exp = 0;
    internal int staminaGainOnAttack = 5;
    private float lastLevelUpPresentationEndTime;
    private float lastAttackTime = 0;
    internal int maxHealStoneCount = 5;
    [SerializeField] private float staminaDecayDelay = 5f;
    [SerializeField] private float staminaDecayAmount = 4f;

    public int weapon_Attack_Ability;
    public int weapon_Defense_Ability;
    public int weapon_Critical_Ability;
    public int weapon_HP_Ability;

    public int shield = 0;

    private Dictionary<string, float> damagedModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> defenceModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> HPModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> attackModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> criticalModifiers = new Dictionary<string, float>();

/*  
    public int normal_MaxHP_Passive = 0;
    public int normal_Defence_Passive = 0;
    public int normal_Critical_Passive = 0;*/

    public bool autoHealUse;
    public float healStone_HealAmount = 0.2f;

    public string[] equipSkill = new string[8];
    public string[] equipPassiveSkill = new string[5];

    public List<string> learnedSkills = new List<string>();
    public List<string> viewedTutorialIds = new List<string>();
    public List<string> playedDialogueEventIds = new List<string>();
    public List<string> dialogueStateFlags = new List<string>();

    private float staminaDecayBuffer = 0f;

    public List<WeaponData> WeaponInventory = new List<WeaponData>(new WeaponData[14]);
    public List<WeaponAbility> weapon_Ability = new List<WeaponAbility>(new WeaponAbility[16]);

    public SummonData summonInven = new SummonData();
    public int summonStamina;
    public int dialogueIndex = 0;

    public int equipWeapon;

    public WeaponStatMultiplierConfig statMultipliers = new WeaponStatMultiplierConfig();
    [Header("Combat Balance")]
    [SerializeField] private DefenseMitigationProfile defenseMitigationProfile = new DefenseMitigationProfile
    {
        reductionCurveBase = 40f,
        percentReductionWeight = 0.65f,
        flatReductionWeight = 0.2f
    };

    Animator animator;
    PlayerController playerController;
    PlayerCamera playerCamera;
    InGameUI inGameUI;
    PlayerEffectManager effectManager;
    PlayerSkill playerSkill;
    PassiveSkillManager passiveSkillManager;
    PlayerMovement playerMovement;
    internal bool isCombat = false;
    public event Action<bool> OnCombatStateChanged;

    private const int DefaultWeaponInventorySize = 14;
    private const int DefaultWeaponAbilitySlotCount = 3;
    private const int DefaultSkillSlotCount = 8;
    private const int DefaultPassiveSlotCount = 5;
    private const int DefaultWeaponAbilityInventorySize = 16;
    private const int DefaultSummonAbilityEquipSlotCount = 4;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        playerMovement = GetComponent<PlayerMovement>();
        inGameUI = FindFirstObjectByType<InGameUI>();
        playerCamera = FindFirstObjectByType<PlayerCamera>();
        playerSkill = GetComponent<PlayerSkill>();
        passiveSkillManager = FindFirstObjectByType<PassiveSkillManager>();
        effectManager = GetComponent<PlayerEffectManager>();
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Inventory.OnChangeInventory -= Save;
        Inventory.OnChangeSummonInventory -= Save;

    }
    private void Start()
    {
       Inventory.OnChangeInventory += Save;
        Inventory.OnChangeSummonInventory += Save;
        if (GameSession.Exists)
        {
            setting = GameSession.Instance.Settings;
            ApplyGraphicsSettings();
        }
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void InitializeFromSession(GameSession session)
    {
        if (session == null)
        {
            Debug.LogError("PlayerInfo: InitializeFromSession failed because GameSession is null.");
            return;
        }

        statProfile = session.GetPlayerProfileData();
        if (statProfile == null)
        {
            Debug.LogError("PlayerInfo: cached PlayerStatProfileSO is missing.");
            return;
        }

        levelStatTable.Clear();
        foreach (KeyValuePair<int, PlayerLevelStatSO> pair in session.GetLevelStatTable())
            levelStatTable[pair.Key] = pair.Value;

        needExp = session.GetNeedExpTable();
        levelUpStats = session.GetLevelUpStatsTable();
        statMultipliers = session.GetWeaponStatMultipliers();
        setting = session.Settings;
        isProfileLoaded = true;
        shouldRefillHpOnSessionInitialize = session.IsNewGamePending;

        saveInfo = session.CreateGameplaySaveSnapshot();
        NormalizeSaveInfo();
        ApplyLoadedData();
        SkillManager.instance?.RefreshFromPlayerState();
        OnProfileLoaded?.Invoke();
    }
    public void WeaponAbilityStone()
    {
        weapon_Attack_Ability = 0;
        weapon_Critical_Ability = 0;
        weapon_Defense_Ability = 0;
        weapon_HP_Ability = 0;

        WeaponAbility[] weaponAbility = WeaponInventory[equipWeapon].weaponAbilityData;

        for (int i = 0; i < weaponAbility.Length; i++)
        {
            switch (weaponAbility[i].weaponAbilityType)
            {
                case WeaponAbilityType.Attack:
                    weapon_Attack_Ability += Mathf.RoundToInt(WeaponInventory[equipWeapon].damage * (float)weaponAbility[i].weaponAbilityValues / 100f);
                    break;
                case WeaponAbilityType.Defence:
                    weapon_Defense_Ability += Mathf.RoundToInt(WeaponInventory[equipWeapon].defense * (float)weaponAbility[i].weaponAbilityValues / 100f);
                    break;
                case WeaponAbilityType.Critical:
                    weapon_Critical_Ability += Mathf.RoundToInt(WeaponInventory[equipWeapon].criticalChance * (float)weaponAbility[i].weaponAbilityValues / 100f);
                    break;
                case WeaponAbilityType.Hp:
                    weapon_HP_Ability += Mathf.RoundToInt(WeaponInventory[equipWeapon].hp * (float)weaponAbility[i].weaponAbilityValues / 100f);
                    break;
            }
        }
        Inventory.Instance.InvokeOnChangeInventory();
        InvokeOnChangeStatus();
    }

    public int FinalAttackDamage
    {
        get
        {
            if (equipWeapon == null) return attackDamage ;
            float m = statMultipliers.GetMultiplier(statMultipliers.damageMultiplierBase, WeaponInventory[equipWeapon].grade);
            return GetFinalAttackDMG(attackDamage + Mathf.RoundToInt(WeaponInventory[equipWeapon].damage * m)+ weapon_Attack_Ability);
        }
    }

    public int FinalDefense
    {
        get
        {

            float m = statMultipliers.GetMultiplier(statMultipliers.defenseMultiplierBase, WeaponInventory[equipWeapon].grade);
            return GetFinalDefence(defense + Mathf.RoundToInt(WeaponInventory[equipWeapon].defense * m) + weapon_Defense_Ability);

        }
    }

    public int FinalMaxHP
    {
        get
        {

            float m = statMultipliers.GetMultiplier(statMultipliers.hpMultiplierBase, WeaponInventory[equipWeapon].grade);
                return GetFinalHP(maxHP + Mathf.RoundToInt(WeaponInventory[equipWeapon].hp * m) + weapon_HP_Ability);
        }
    }

    public int FinalCritical
    {
        get
        {

            float m = statMultipliers.GetMultiplier(statMultipliers.criticalMultiplierBase, WeaponInventory[equipWeapon].grade);
            return criticalChance + Mathf.RoundToInt(WeaponInventory[equipWeapon].criticalChance * m) + weapon_Critical_Ability;
        }
    }

    public int FinalStaminaGain
    {
        get
        {

            float m = statMultipliers.GetMultiplier(statMultipliers.staminaRecoveryMultiplierBase, WeaponInventory[equipWeapon].grade);
            return staminaGainOnAttack + Mathf.RoundToInt(WeaponInventory[equipWeapon].staminaRecovery * m);
        }
    }

    public int FinalDrain
    {
        get
        {

            float m = statMultipliers.GetMultiplier(statMultipliers.drainMultiplierBase, WeaponInventory[equipWeapon].grade);
            return Mathf.RoundToInt(WeaponInventory[equipWeapon].drain * m);
        }
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyLoadedData();
    }

    public void UpdateRuntimeSettings(UserSetting source, bool applyGraphics = true)
    {
        if (source == null)
            return;

        setting = new UserSetting
        {
            screenMode = source.screenMode,
            resolution = source.resolution,
            BGM = source.BGM,
            NormalSFX = source.NormalSFX,
            SkillSFX = source.SkillSFX
        };

        if (saveInfo != null)
            saveInfo.setting = setting;

        if (applyGraphics)
            ApplyGraphicsSettings();
    }

    public void ApplyGraphicsSettings()
    {
        int width = 1920;
        int height = 1080;

        switch (setting.resolution)
        {
            case Resolution.FHD:
                width = 1920;
                height = 1080;
                break;
            case Resolution.QHD:
                width = 2560;
                height = 1440;
                break;
            case Resolution.UHD:
                width = 3840;
                height = 2160;
                break;
        }

        bool isFullScreen = (setting.screenMode == ScreenMode.FullScreen);

        Screen.SetResolution(width, height, isFullScreen);

    }

    public void ApplyDamage(int rawDamage, bool isCritical, GameObject enemy)
    {
        // if (playerController.isDead) return;
        EnterCombat();

        if (SkillManager.instance.GetSkillData(SkillType.CounterDash).isActive)
        {
            rawDamage = Mathf.RoundToInt(rawDamage * 0.2f);
            playerSkill.CounterDashAttack(enemy.transform);
        }

        if (SkillManager.instance.GetSkillData(SkillType.CounterReflection).isActive)
        {
            playerSkill.CounterReflectionAttack(enemy);
            return;
        }

        GetComponent<PlayerEffectManager>().PlayEffect(PlayerEffectType.Hit);
        int currentDamage = rawDamage;

        if (shield > 0)
        {
            int absorbed = Mathf.Min(shield, currentDamage);
            ShieldChange(-absorbed);
            currentDamage -= absorbed;
        }

        if (currentDamage > 0)
        {
            int finalHpDamage = CalculateDamageAfterDefense(currentDamage);

            int totalDamageToHp = GetFinalDamaged(finalHpDamage);

            PlayerSoundManager.PlaySound("Hit");
            HpChange(-totalDamageToHp);
            inGameUI.HitEffect();

        }
    }

    private int CalculateDamageAfterDefense(int incomingDamage)
    {
        if (incomingDamage <= 0)
            return 0;

        float currentDefense = Mathf.Max(0f, FinalDefense);
        float reductionBase = Mathf.Max(1f, defenseMitigationProfile.reductionCurveBase);
        float percentWeight = Mathf.Clamp01(defenseMitigationProfile.percentReductionWeight);
        float flatWeight = Mathf.Max(0f, defenseMitigationProfile.flatReductionWeight);

        float percentReduction = currentDefense / (currentDefense + reductionBase);
        float reducedDamage = incomingDamage * (1f - (percentReduction * percentWeight));
        reducedDamage -= currentDefense * flatWeight;

        return Mathf.Max(1, Mathf.RoundToInt(reducedDamage));
    }
    public void UseHPStone()
    {
        if (hpStone > 0)
        {
            if (Instance.currentHP == Instance.FinalMaxHP)
            {
                InGameUI.ShowWarning("피가 가득 차 있습니다");
                return;
            }
            PlayerSoundManager.PlaySound("HealStone");
            effectManager.PlayEffect(PlayerEffectType.Heal_Stone_Use);
            hpStone--;
            SavePersistentMetaCurrencies();
            HpChange(Mathf.RoundToInt(FinalMaxHP * healStone_HealAmount));
        }
        else
        {
            InGameUI.ShowWarning("회복석이 부족합니다");
        }
    }
    private Coroutine deadCor;
    void Dead()
    {
        playerController.StateMachine.ChangeState(playerController.DeathState);
        if (deadCor == null)
        {
           deadCor = StartCoroutine(DeadRoutine());
        }
        ShieldChange(-shield);
        SetAttackModifier("AttackBuff", 0);
    }
    IEnumerator DeadRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        Time.timeScale = 0;
        FindFirstObjectByType<UIManager>().ShowDeathUIOnly();
    }
    public void Revive()
    {
        Time.timeScale = 1;
        UIManager uIManager = FindFirstObjectByType<UIManager>();
        uIManager.deathUI.SetActive(false);
        uIManager.UpdateMouseState(false);
        deadCor = null;

        if (BossSceneFlow.IsBossScene(SceneManager.GetActiveScene().name))
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(BossSceneFlow.BossSceneName);
            return;
        }

        bool restoredSnapshot = RestoreBattleAttemptSnapshot();
        StageManager stageManager = FindFirstObjectByType<StageManager>();
        if (restoredSnapshot && stageManager != null && !stageManager.IsLobbyStageActive)
        {
            playerController.StateMachine.CurrentState.Revive();
            stageManager.RestartCurrentBattleLevel();
            return;
        }

        currentHP = FinalMaxHP;
        exp = 0;

        SectionManager sections = FindFirstObjectByType<SectionManager>();
        if (sections != null && sections.spawnPos != null)
        {
            Vector3 pos = sections.spawnPos.position;
            sections.ResetMap();
            GetComponent<Rigidbody>().MovePosition(pos);
        }

        InvokeOnChangeStatus();
        playerController.StateMachine.CurrentState.Revive();
    }

    Coroutine combatEndCoroutine;
    public void EnterCombat()
    {
        SetCombatState(true);

        if (combatEndCoroutine != null)
            StopCoroutine(combatEndCoroutine);

        combatEndCoroutine = StartCoroutine(CombatEndTimer());
    }
    IEnumerator CombatEndTimer()
    {
        yield return new WaitForSeconds(7f);
        combatEndCoroutine = null;
        SetCombatState(false);
    }

    public void ExitCombat()
    {
        if (combatEndCoroutine != null)
        {
            StopCoroutine(combatEndCoroutine);
            combatEndCoroutine = null;
        }

        SetCombatState(false);
    }

    private void SetCombatState(bool value)
    {
        if (isCombat == value)
            return;

        isCombat = value;
        OnCombatStateChanged?.Invoke(isCombat);
    }
    public void NewGame()
    {
        if (GameSession.Exists)
        {
            GameSession.Instance.StartNewGame();
        }
    }

    private bool EnsureProfileLoaded()
    {
        if (isProfileLoaded && statProfile != null && levelStatTable.Count > 0)
            return true;

        Debug.LogError("PlayerInfo: PlayerStatProfileSO가 아직 로드되지 않았습니다.");
        return false;
    }

    private string[] CopyStringArray(string[] source, int length)
    {
        string[] result = new string[length];
        if (source == null)
            return result;

        int copyCount = Mathf.Min(source.Length, length);
        Array.Copy(source, result, copyCount);
        return result;
    }

    private List<string> CreateLearnedSkillList(string[] source)
    {
        List<string> result = new List<string>();
        if (source == null)
            return result;

        foreach (string skillName in source)
        {
            if (!string.IsNullOrWhiteSpace(skillName))
            {
                result.Add(skillName);
            }
        }

        return result;
    }

    private WeaponData CloneWeaponData(WeaponData source)
    {
        WeaponData result = new WeaponData();
        if (source == null)
        {
            result.weaponAbilityData = new WeaponAbility[DefaultWeaponAbilitySlotCount];
            return result;
        }

        result.weaponName = source.weaponName;
        result.rank = source.rank;
        result.grade = source.grade;
        result.damage = source.damage;
        result.hp = source.hp;
        result.defense = source.defense;
        result.staminaRecovery = source.staminaRecovery;
        result.criticalChance = source.criticalChance;
        result.drain = source.drain;
        result.used = source.used;

        int abilityLength = source.weaponAbilityData != null && source.weaponAbilityData.Length > 0
            ? source.weaponAbilityData.Length
            : DefaultWeaponAbilitySlotCount;

        result.weaponAbilityData = new WeaponAbility[abilityLength];
        for (int i = 0; i < abilityLength; i++)
        {
            WeaponAbility sourceAbility = source.weaponAbilityData != null && i < source.weaponAbilityData.Length
                ? source.weaponAbilityData[i]
                : null;

            result.weaponAbilityData[i] = sourceAbility == null
                ? new WeaponAbility()
                : new WeaponAbility
                {
                    weaponAbilityType = sourceAbility.weaponAbilityType,
                    weaponAbilityValues = sourceAbility.weaponAbilityValues,
                    count = sourceAbility.count
                };
        }

        return result;
    }

    public void InitializeEquippedAbilitySlots()
    {
        if (summonInven.equitabilityStoneCounts.Count < 4)
        {
            summonInven.equitabilityStoneCounts.Clear();
            for (int i = 0; i < 4; i++)
            {
                summonInven.equitabilityStoneCounts.Add(new AbilityStoneCount
                {
                    abilityType = SummonAbilityStoneType.None,
                    abilityValue = SummonAbilityStoneValues.Value0,
                    count = 0
                });
            }
        }
    }
    public void Save()
    {
        SaveMetaState(ShouldPersistMetaState());
    }

    public void SaveMetaState(bool persistToDisk = false)
    {
        saveInfo.setting = setting;
        saveInfo.enhancementStone = enhancementStone;
        saveInfo.hpStone = hpStone;
        saveInfo.skillGem = skillGem;
        saveInfo.equipSkill = equipSkill;
        saveInfo.equipPassiveSkill = equipPassiveSkill;
        saveInfo.learnedSkills = learnedSkills;
        saveInfo.viewedTutorialIds = viewedTutorialIds != null ? new List<string>(viewedTutorialIds) : new List<string>();
        saveInfo.playedDialogueEventIds = playedDialogueEventIds != null ? new List<string>(playedDialogueEventIds) : new List<string>();
        saveInfo.dialogueStateFlags = dialogueStateFlags != null ? new List<string>(dialogueStateFlags) : new List<string>();
        saveInfo.stageCheck = stageCheck;
        string currentSceneName = SceneManager.GetActiveScene().name;
        saveInfo.sceneName = !BossSceneFlow.IsBossRelatedScene(currentSceneName) && GameSession.IsGameplayScene(currentSceneName)
            ? currentSceneName
            : GameSession.DefaultGameplaySceneName;
        saveInfo.nextBattleLevel = Mathf.Max(1, nextBattleLevel);
        saveInfo.checkPoint = checkPoint;
        saveInfo.sectionClear = sectionClear;
        saveInfo.dialogueIndex = dialogueIndex;
        saveInfo.WeaponInventory = WeaponInventory;
        saveInfo.equipWeapon = equipWeapon;
        saveInfo.weapon_Ability = weapon_Ability;
        saveInfo.summonInven = summonInven;
        saveInfo.summonStamina = summonStamina;

        if (GameSession.Exists)
        {
            GameSession.Instance.UpdateSettings(setting);
            GameSession.Instance.UpdateRuntimeProgress(saveInfo);
            GameSession.Instance.SaveSettings();

            if (persistToDisk)
                GameSession.Instance.SaveCurrentProgress(saveInfo);
        }
    }

    public void CommitLobbyProgress(int nextBattleLevel)
    {
        this.nextBattleLevel = Mathf.Max(1, nextBattleLevel);
        saveInfo.setting = setting;
        saveInfo.enhancementStone = enhancementStone;
        saveInfo.skillGem = skillGem;
        saveInfo.level = level;
        saveInfo.hpStone = hpStone;
        saveInfo.exp = exp;
        saveInfo.defense = defense;
        saveInfo.criticalChance = criticalChance;
        saveInfo.hp = currentHP;
        saveInfo.maxhp = maxHP;
        saveInfo.stamina = stamina;
        saveInfo.maxstamina = maxstamina;
        saveInfo.damage = attackDamage;
        saveInfo.staminaGainOnAttack = staminaGainOnAttack;
        saveInfo.equipSkill = equipSkill;
        saveInfo.equipPassiveSkill = equipPassiveSkill;
        saveInfo.learnedSkills = learnedSkills;
        saveInfo.viewedTutorialIds = viewedTutorialIds != null ? new List<string>(viewedTutorialIds) : new List<string>();
        saveInfo.playedDialogueEventIds = playedDialogueEventIds != null ? new List<string>(playedDialogueEventIds) : new List<string>();
        saveInfo.dialogueStateFlags = dialogueStateFlags != null ? new List<string>(dialogueStateFlags) : new List<string>();
        saveInfo.stageCheck = stageCheck;
        saveInfo.sceneName = GameSession.IsGameplayScene(SceneManager.GetActiveScene().name)
            ? SceneManager.GetActiveScene().name
            : GameSession.DefaultGameplaySceneName;
        saveInfo.nextBattleLevel = this.nextBattleLevel;
        saveInfo.checkPoint = checkPoint;
        saveInfo.sectionClear = false;
        saveInfo.dialogueIndex = dialogueIndex;
        saveInfo.WeaponInventory = WeaponInventory;
        saveInfo.equipWeapon = equipWeapon;
        saveInfo.weapon_Ability = weapon_Ability;
        saveInfo.summonInven = summonInven;
        saveInfo.summonStamina = summonStamina;

        if (GameSession.Exists)
        {
            GameSession.Instance.UpdateSettings(setting);
            GameSession.Instance.SetRuntimeNextBattleLevel(saveInfo.nextBattleLevel);
            GameSession.Instance.SaveCurrentProgress(saveInfo);
            GameSession.Instance.SaveSettings();
        }
    }

    public void SavePersistentMetaCurrencies()
    {
        saveInfo.setting = setting;
        saveInfo.enhancementStone = enhancementStone;
        saveInfo.skillGem = skillGem;
        saveInfo.hpStone = hpStone;

        if (GameSession.Exists)
        {
            GameSession.Instance.UpdateSettings(setting);
            GameSession.Instance.SavePersistentMetaCurrencies(saveInfo);
            GameSession.Instance.SaveSettings();
        }
    }

    private void NormalizeSaveInfo()
    {
        if (saveInfo.setting == null) saveInfo.setting = new UserSetting();
        if (saveInfo.learnedSkills == null) saveInfo.learnedSkills = new List<string>();
        if (saveInfo.viewedTutorialIds == null) saveInfo.viewedTutorialIds = new List<string>();
        if (saveInfo.playedDialogueEventIds == null) saveInfo.playedDialogueEventIds = new List<string>();
        if (saveInfo.dialogueStateFlags == null) saveInfo.dialogueStateFlags = new List<string>();
        if (saveInfo.stageCheck == null) saveInfo.stageCheck = new StageCheck();
        if (string.IsNullOrWhiteSpace(saveInfo.sceneName)) saveInfo.sceneName = GameSession.DefaultGameplaySceneName;
        if (saveInfo.nextBattleLevel <= 0) saveInfo.nextBattleLevel = 1;
        if (saveInfo.summonInven == null) saveInfo.summonInven = new SummonData();
        if (saveInfo.summonInven.summonTypeCounts == null) saveInfo.summonInven.summonTypeCounts = new List<SummonTypeCount>();
        if (saveInfo.summonInven.abilityStoneCounts == null) saveInfo.summonInven.abilityStoneCounts = new List<AbilityStoneCount>();
        if (saveInfo.summonInven.equitabilityStoneCounts == null) saveInfo.summonInven.equitabilityStoneCounts = new List<AbilityStoneCount>();

        saveInfo.equipSkill = CopyStringArray(saveInfo.equipSkill, DefaultSkillSlotCount);
        saveInfo.equipPassiveSkill = CopyStringArray(saveInfo.equipPassiveSkill, DefaultPassiveSlotCount);

        int maxLevel = needExp != null && needExp.Length > 0 ? needExp.Length : Mathf.Max(1, statProfile != null ? statProfile.startLevel : 1);
        int originalLevel = saveInfo.level;
        saveInfo.level = Mathf.Clamp(saveInfo.level <= 0 ? 1 : saveInfo.level, 1, maxLevel);
        if (originalLevel != saveInfo.level)
        {
            Debug.LogWarning($"PlayerInfo: 저장된 레벨 {originalLevel} 이(가) 현재 레벨 테이블 범위를 벗어나 {saveInfo.level}(으)로 보정되었습니다.");
        }
        else
        {
            Debug.Log($"[PlayerInfo] NormalizeSaveInfo => loaded level:{saveInfo.level}");
        }

        bool isBossTestSession = GameSession.Exists && GameSession.Instance.IsBossTestSession;
        bool hasLevelStats = TryGetLevelUpStats(saveInfo.level, out LevelUpStats savedLevelStats);
        if (hasLevelStats)
        {
            saveInfo.maxhp = saveInfo.maxhp > 0 ? saveInfo.maxhp : savedLevelStats.maxhp;
            saveInfo.damage = saveInfo.damage > 0 ? saveInfo.damage : savedLevelStats.damage;
            saveInfo.maxstamina = saveInfo.maxstamina > 0 ? saveInfo.maxstamina : savedLevelStats.maxstamina;
            saveInfo.defense = saveInfo.defense > 0 ? saveInfo.defense : savedLevelStats.defense;
            saveInfo.criticalChance = saveInfo.criticalChance > 0 ? saveInfo.criticalChance : savedLevelStats.criticalChance;
            if (!isBossTestSession)
                saveInfo.staminaGainOnAttack = Mathf.Max(0, savedLevelStats.staminaGainOnAttack);
        }
        else if (statProfile != null)
        {
            saveInfo.maxhp = saveInfo.maxhp > 0 ? saveInfo.maxhp : statProfile.startMaxHP;
            saveInfo.damage = saveInfo.damage > 0 ? saveInfo.damage : statProfile.startAttackDamage;
            saveInfo.maxstamina = saveInfo.maxstamina > 0 ? saveInfo.maxstamina : statProfile.startMaxStamina;
            saveInfo.defense = saveInfo.defense > 0 ? saveInfo.defense : statProfile.startDefense;
            saveInfo.criticalChance = saveInfo.criticalChance > 0 ? saveInfo.criticalChance : statProfile.startCriticalChance;
        }

        saveInfo.hp = Mathf.Clamp(saveInfo.hp <= 0 ? saveInfo.maxhp : saveInfo.hp, 1, Mathf.Max(1, saveInfo.maxhp));
        saveInfo.stamina = Mathf.Clamp(saveInfo.stamina, 0, Mathf.Max(0, saveInfo.maxstamina));
        if (!hasLevelStats && !isBossTestSession)
        {
            saveInfo.staminaGainOnAttack = statProfile != null
                ? Mathf.Max(0, statProfile.startStaminaGainOnAttack)
                : 5;
        }

        saveInfo.WeaponInventory = NormalizeWeaponInventory(saveInfo.WeaponInventory);
        saveInfo.equipWeapon = Mathf.Clamp(saveInfo.equipWeapon, 0, DefaultWeaponInventorySize - 1);
        if (statProfile != null)
        {
            bool hasAnyWeapon = false;
            for (int i = 0; i < saveInfo.WeaponInventory.Count; i++)
            {
                if (saveInfo.WeaponInventory[i] != null && saveInfo.WeaponInventory[i].used)
                {
                    hasAnyWeapon = true;
                    break;
                }
            }

            if (!hasAnyWeapon)
            {
                saveInfo.WeaponInventory[saveInfo.equipWeapon] = CloneWeaponData(statProfile.startWeapon);
                Debug.LogWarning("PlayerInfo: 저장된 인벤토리가 비어 있어 시작 무기를 자동 복구했습니다.");
            }
        }

        if (saveInfo.weapon_Ability == null) saveInfo.weapon_Ability = new List<WeaponAbility>();
        while (saveInfo.weapon_Ability.Count < DefaultWeaponAbilityInventorySize)
        {
            saveInfo.weapon_Ability.Add(new WeaponAbility());
        }

        while (saveInfo.summonInven.equitabilityStoneCounts.Count < DefaultSummonAbilityEquipSlotCount)
        {
            saveInfo.summonInven.equitabilityStoneCounts.Add(new AbilityStoneCount
            {
                abilityType = SummonAbilityStoneType.None,
                abilityValue = SummonAbilityStoneValues.Value0,
                count = 0
            });
        }
    }

    private List<WeaponData> NormalizeWeaponInventory(List<WeaponData> source)
    {
        List<WeaponData> result = source ?? new List<WeaponData>();

        while (result.Count < DefaultWeaponInventorySize)
        {
            result.Add(new WeaponData());
        }

        for (int i = 0; i < result.Count; i++)
        {
            if (result[i] == null)
            {
                result[i] = new WeaponData();
            }

            if (result[i].weaponAbilityData == null || result[i].weaponAbilityData.Length == 0)
            {
                result[i].weaponAbilityData = new WeaponAbility[DefaultWeaponAbilitySlotCount];
            }

            for (int j = 0; j < result[i].weaponAbilityData.Length; j++)
            {
                if (result[i].weaponAbilityData[j] == null)
                {
                    result[i].weaponAbilityData[j] = new WeaponAbility();
                }
            }
        }

        if (result.Count > DefaultWeaponInventorySize)
        {
            result.RemoveRange(DefaultWeaponInventorySize, result.Count - DefaultWeaponInventorySize);
        }

        return result;
    }

    public bool TryGetStageFromScene(string sceneName, out Stage stage)
    {
        return System.Enum.TryParse(sceneName, out stage);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            LevelUp();
        }

        HandleStaminaDecay();
    }

    public void DebugSetCurrentHpPercent(float percent)
    {
        float clampedPercent = Mathf.Clamp(percent, 0f, 100f);

        int targetHp;
        if (clampedPercent <= 0f)
        {
            targetHp = 0;
        }
        else
        {
            targetHp = Mathf.Max(1, Mathf.RoundToInt(FinalMaxHP * (clampedPercent / 100f)));
        }

        currentHP = Mathf.Clamp(targetHp, 0, FinalMaxHP);
        Debug.Log($"[PassiveTest] currentHP forced => {currentHP}/{FinalMaxHP} ({clampedPercent:0.#}%)");
        InvokeOnChangeStatus();
    }

    [ContextMenu("Passive Debug/HP 100%")]
    private void DebugSetHp100Percent() => DebugSetCurrentHpPercent(100f);

    [ContextMenu("Passive Debug/HP 71%")]
    private void DebugSetHp71Percent() => DebugSetCurrentHpPercent(71f);

    [ContextMenu("Passive Debug/HP 70%")]
    private void DebugSetHp70Percent() => DebugSetCurrentHpPercent(70f);

    [ContextMenu("Passive Debug/HP 41%")]
    private void DebugSetHp41Percent() => DebugSetCurrentHpPercent(41f);

    [ContextMenu("Passive Debug/HP 40%")]
    private void DebugSetHp40Percent() => DebugSetCurrentHpPercent(40f);

    [ContextMenu("Passive Debug/HP 26%")]
    private void DebugSetHp26Percent() => DebugSetCurrentHpPercent(26f);

    [ContextMenu("Passive Debug/HP 25%")]
    private void DebugSetHp25Percent() => DebugSetCurrentHpPercent(25f);

    [ContextMenu("Passive Debug/HP 10%")]
    private void DebugSetHp10Percent() => DebugSetCurrentHpPercent(10f);

    private void HandleStaminaDecay()
    {
        if (Time.time - lastAttackTime > staminaDecayDelay && stamina > 0)
        {
            staminaDecayBuffer += staminaDecayAmount * Time.deltaTime;

            if (staminaDecayBuffer >= 1f)
            {
                int decreaseAmount = Mathf.RoundToInt(staminaDecayBuffer);
                StaminaChange(-decreaseAmount);
                staminaDecayBuffer -= decreaseAmount;

                InvokeOnChangeStatus();
            }
        }
    }

    private bool TryGetRequiredExp(int currentLevel, out int requiredExp)
    {
        requiredExp = 0;

        if (!levelStatTable.TryGetValue(currentLevel, out PlayerLevelStatSO levelProfile) || levelProfile == null)
            return false;

        requiredExp = levelProfile.needExpToNextLevel;
        return true;
    }

    private bool TryGetLevelUpStats(int targetLevel, out LevelUpStats stats)
    {
        stats = null;

        if (!levelStatTable.TryGetValue(targetLevel, out PlayerLevelStatSO levelProfile) || levelProfile == null)
            return false;

        stats = levelProfile.levelStats;
        return stats != null;
    }

    private bool TryCanLevelUp(int currentLevel, out int requiredExp)
    {
        requiredExp = 0;

        if (!TryGetRequiredExp(currentLevel, out requiredExp))
            return false;

        return levelStatTable.ContainsKey(currentLevel + 1);
    }

    public void ExpChange(int value)
    {
        if (!EnsureProfileLoaded())
            return;

        Instance.exp += value;
        while (TryCanLevelUp(level, out int requiredExp) && Instance.exp >= requiredExp)
        {
            Instance.exp -= requiredExp;
            LevelUp();
        }
        InvokeOnChangeStatus();
    }

    public void LevelUp()
    {
        if (!EnsureProfileLoaded())
            return;

        int nextLevel = Instance.level + 1;
        if (!TryGetLevelUpStats(nextLevel, out LevelUpStats stats))
        {
            Debug.LogWarning($"PlayerInfo: LevelUp 데이터가 없습니다. targetLevel={nextLevel}");
            return;
        }

        ApplyLevelUpStats(stats, nextLevel);
        PlayLevelUpPresentation();
        SkillManager.instance.CheckUnlockableNewSkills();
        InvokeOnChangeStatus();
    }

    private void ApplyLevelUpStats(LevelUpStats stats, int nextLevel)
    {
        Instance.level = nextLevel;
        Instance.exp = Mathf.Max(0, Instance.exp);
        Instance.currentHP = stats.maxhp;
        Instance.maxHP = stats.maxhp;
        Instance.attackDamage = stats.damage;
        Instance.defense = stats.defense;
        Instance.maxstamina = stats.maxstamina;
        Instance.stamina = stats.maxstamina;
        Instance.staminaGainOnAttack = Mathf.Max(0, stats.staminaGainOnAttack);
    }

    private void PlayLevelUpPresentation()
    {
        lastLevelUpPresentationEndTime = Time.unscaledTime + InGameUI.LevelUpPresentationDuration;
        GetComponent<PlayerEffectManager>().PlayEffect(PlayerEffectType.LevelUP);
        PlayerSoundManager.PlaySound("LevelUP");
        inGameUI.LevelUP();
    }

    public float GetRemainingLevelUpPresentationTime()
    {
        return Mathf.Max(0f, lastLevelUpPresentationEndTime - Time.unscaledTime);
    }
    public void LevelUpTarget(int level)
    {
        if (!EnsureProfileLoaded())
            return;

        if (!TryGetLevelUpStats(level, out LevelUpStats stats))
        {
            Debug.LogWarning($"PlayerInfo: LevelUpTarget 데이터가 없습니다. targetLevel={level}");
            return;
        }

        Instance.level = level;
        Instance.exp = Mathf.Max(0, Instance.exp);
        Instance.currentHP = stats.maxhp;
        Instance.maxHP = stats.maxhp;
        Instance.attackDamage = stats.damage;
        Instance.defense = stats.defense;
        Instance.maxstamina = stats.maxstamina;
        Instance.stamina = stats.maxstamina;
        Instance.staminaGainOnAttack = Mathf.Max(0, stats.staminaGainOnAttack);
        Instance.nextBattleLevel = Mathf.Max(1, level);
        if (Instance.saveInfo != null)
            Instance.saveInfo.nextBattleLevel = Instance.nextBattleLevel;

        if (GameSession.Exists)
            GameSession.Instance.SetRuntimeNextBattleLevel(Instance.nextBattleLevel);

        InvokeOnChangeStatus();
    }

    public void SyncTestBattleLevelFromEditor(int level)
    {
        int syncedLevel = Mathf.Max(1, level);
        nextBattleLevel = syncedLevel;

        if (saveInfo != null)
            saveInfo.nextBattleLevel = syncedLevel;

        if (GameSession.Exists)
            GameSession.Instance.SetRuntimeNextBattleLevel(syncedLevel);
    }

    public void InvokeOnChangeStatus()
    {
        OnChangeStatus?.Invoke();
    }
    public bool GetFinalCriticalChance()
    {
        return UnityEngine.Random.value <= (FinalCritical / 100f);
    }

    public void SetDamagedModifier(string key, float amount)
    {
        if (amount <= 0f)
        {
            if (damagedModifiers.ContainsKey(key))
                damagedModifiers.Remove(key);
        }
        else
        {
            damagedModifiers[key] = amount;
        }
    }


    private int GetFinalDamaged(float baseDamage)
    {

        float finalMultiplier = 1f;

        foreach (var kvp in damagedModifiers)
        {
            string key = kvp.Key;
            float amount = kvp.Value;

            if (key.Contains("Buff",StringComparison.OrdinalIgnoreCase))
            {
                finalMultiplier *= (1f - amount);
            }
            else
            {
                finalMultiplier *= (1f + amount);
            }
        }
        return Mathf.RoundToInt(baseDamage * finalMultiplier);
    }
    public void SetAttackModifier(string key, float amount)
    {
        if (amount <= 0f)
        {
            if (attackModifiers.ContainsKey(key))
                attackModifiers.Remove(key);
        }
        else
        {
            attackModifiers[key] = amount;
        }
        InvokeOnChangeStatus();
    }
    private int GetFinalAttackDMG(int baseDamage)
    {
        float totalPercent = playerController.playerPassiveController.StaticAttackMultiplier;

        int staticPlayerAttack = Mathf.RoundToInt(baseDamage * (1f + totalPercent));
        
        float finalMultiplier = 1f;

        foreach (var kvp in attackModifiers)
        {
            string key = kvp.Key;
            float amount = kvp.Value;

            if (key.Contains("Buff", StringComparison.OrdinalIgnoreCase))
            {
                finalMultiplier *= (1f + amount);
            }
            else
            {
                finalMultiplier *= (1f - amount);
            }
        }
        return Mathf.RoundToInt(staticPlayerAttack * finalMultiplier);
    }
    public void SetHPModifier(string key, float amount)
    {
        if (amount <= 0f)
        {
            if (HPModifiers.ContainsKey(key))
                HPModifiers.Remove(key);
        }
        else
        {
            HPModifiers[key] = amount;
        }
        InvokeOnChangeStatus();
    }
    private int GetFinalHP(int baseHP)
    {
        float totalPercent = playerController.playerPassiveController.StaticMaxHPMultiplier;
        int staticHP = Mathf.RoundToInt(baseHP * (1f + totalPercent));

        float externalMuliplier = 1f;

        foreach (var kvp in HPModifiers)
        {
            string key = kvp.Key;
            float amount = kvp.Value;

            if (key.Contains("Buff", StringComparison.OrdinalIgnoreCase))
            {
                externalMuliplier *= (1f + amount);
            }
            else
            {
                externalMuliplier *= (1f - amount);
            }
        }
        int result = Mathf.RoundToInt(staticHP * externalMuliplier);

        if (result < currentHP)
            currentHP = result;
        return result;
    }
    public void SetDefenceModifier(string key, float amount)
    {
        if (amount <= 0f)
        {
            if (defenceModifiers.ContainsKey(key))
                defenceModifiers.Remove(key);
        }
        else
        {
            defenceModifiers[key] = amount;
        }
        InvokeOnChangeStatus();
    }
    private int GetFinalDefence(int baseDefence)
    {
        float totalPercent = playerController.playerPassiveController.StaticDefenceMultiplier;

        float playerStaticDefence = baseDefence * (1f + totalPercent);

        float externalMuliplier = 1f;

        foreach (var kvp in defenceModifiers)
        {
            string key = kvp.Key;
            float amount = kvp.Value;

            if (key.Contains("Buff", StringComparison.OrdinalIgnoreCase))
            {
                externalMuliplier *= (1f + amount);
            }
            else
            {
                externalMuliplier *= (1f - amount);
            }
        }

        
        return Mathf.RoundToInt(playerStaticDefence * externalMuliplier);
    }
    public bool CheckLearnedPassive(PassiveSkillType type)
    {
        foreach (var data in Instance.equipPassiveSkill)
        {
            if (data.ToString() == type.ToString())
                return true;
        }
        return false;
    }

    public void HpChange(int value)
    {
        if (value < 0)
        {
            int incomingDamage = -value;

            if (playerController.playerPassiveController.IsPassiveInvincible)
            {
                InvokeOnChangeStatus();
                return;
            }

            foreach (var passive in passiveSkillManager.activePassives)
            {
                if (passive.OnBeforeDamaged(playerController, incomingDamage))
                {
                    InvokeOnChangeStatus();
                    return;
                }
            }
        }

        currentHP += value;

        if (currentHP > FinalMaxHP)
            currentHP = FinalMaxHP;

        if (currentHP <= 0)
        {
            currentHP = 0;
            Dead();
        }

        foreach (var passive in passiveSkillManager.activePassives)
        {
            passive.OnDamaged(playerController);
        }

        if (value < 0)
        {
            playerController.playerPassiveController.InvokeOnPlayerDamaged();
        }

        InvokeOnChangeStatus();
    }

    public void ShieldChange(int value)
    {
        shield += value;
        if (shield <= 0)
        {
            playerSkill.ShieldBreak();
            shield = 0;
        }
        InvokeOnChangeStatus();
    }
    private float ShieldRemainTime;
    private Coroutine ShieldCor;
    public void ShieldActive(float duration)
    {
        if(ShieldCor != null)
        {
            StopCoroutine(ShieldCor);
            float durationTime = ShieldRemainTime + duration;
            ShieldCor = StartCoroutine(ShieldActiveTime(durationTime));
        }
        else
        {
            ShieldCor = StartCoroutine(ShieldActiveTime(duration));
        }
    }
    IEnumerator ShieldActiveTime(float duration)
    {
        ShieldRemainTime = duration;

        while(ShieldRemainTime >= 0)
        {
            ShieldRemainTime -= Time.deltaTime;
            yield return null;
        }
        ShieldCor = null;
        ShieldRemainTime = 0;
    }
    public void UsePlayerStamina(int amount)
    {
        stamina -= amount;
        stamina = Mathf.Max(0, stamina);

        InvokeOnChangeStatus();
    }

    public void GainStaminaFromAttack()
    {
        StaminaChange(FinalStaminaGain);
        lastAttackTime = Time.time;
    }
    public void GainStaminaFromSkill(int value)
    {
        summonStamina += value;
        InvokeOnChangeStatus();
    }

    public void StaminaChange(int value)
    {
        Instance.stamina = Mathf.Min(Instance.maxstamina, Instance.stamina + value);
        InvokeOnChangeStatus();
    }

    public void ApplyLoadedData()
    {

        if (saveInfo != null)
        {
            setting = saveInfo.setting;
            skillGem = saveInfo.skillGem;
            enhancementStone = saveInfo.enhancementStone;
            hpStone = saveInfo.hpStone;
            int maxLevel = needExp != null && needExp.Length > 0 ? needExp.Length : Mathf.Max(1, statProfile != null ? statProfile.startLevel : 1);
            level = Mathf.Clamp(saveInfo.level <= 0 ? 1 : saveInfo.level, 1, maxLevel);
            exp = saveInfo.exp;
            defense = saveInfo.defense;
            criticalChance = saveInfo.criticalChance;
            currentHP = saveInfo.hp;
            maxHP = saveInfo.maxhp;
            stamina = saveInfo.stamina;
            maxstamina = saveInfo.maxstamina;
            attackDamage = saveInfo.damage;
            staminaGainOnAttack = saveInfo.staminaGainOnAttack;
            equipSkill = CopyStringArray(saveInfo.equipSkill, DefaultSkillSlotCount);
            equipPassiveSkill = CopyStringArray(saveInfo.equipPassiveSkill, DefaultPassiveSlotCount);
            learnedSkills = saveInfo.learnedSkills ?? new List<string>();
            viewedTutorialIds = saveInfo.viewedTutorialIds ?? new List<string>();
            playedDialogueEventIds = saveInfo.playedDialogueEventIds ?? new List<string>();
            dialogueStateFlags = saveInfo.dialogueStateFlags ?? new List<string>();
            WeaponInventory = NormalizeWeaponInventory(saveInfo.WeaponInventory);
            equipWeapon = saveInfo.equipWeapon;
            weapon_Ability = saveInfo.weapon_Ability ?? new List<WeaponAbility>(new WeaponAbility[DefaultWeaponAbilityInventorySize]);
            summonInven = saveInfo.summonInven ?? new SummonData();
            summonStamina = saveInfo.summonStamina;
            stageCheck = saveInfo.stageCheck ?? new StageCheck();
            sceneName = saveInfo.sceneName;
            nextBattleLevel = saveInfo.nextBattleLevel;
            checkPoint = saveInfo.checkPoint;
            sectionClear = saveInfo.sectionClear;
            dialogueIndex = saveInfo.dialogueIndex;

            if (shouldRefillHpOnSessionInitialize)
            {
                currentHP = FinalMaxHP;
                saveInfo.hp = currentHP;
                shouldRefillHpOnSessionInitialize = false;
            }

            Debug.Log($"[PlayerInfo] ApplyLoadedData => level:{level}, exp:{exp}, hp:{currentHP}/{maxHP}, equipWeapon:{equipWeapon}");

        InvokeOnChangeStatus();
        ApplyGraphicsSettings();
        SkillManager.instance?.RefreshFromPlayerState();
        //Save();
    }
}
    public void PlayerLocate()
    {
        if (!GameSession.IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            return;
        }
/*            string currentScene = SceneManager.GetActiveScene().name;*/
            //print(currentScene);

        {
            SectionManager sections = FindFirstObjectByType<SectionManager>();
            Vector3 pos = sections.spawnPos.position;
            //sections.ResetMap();
            GetComponent<Rigidbody>().MovePosition(pos);
        }
        /*if (TryGetStageFromScene(currentScene, out Stage stage))
        {
            if (stageCheck.IsFirst(stage))
            {
                GameObject spawnObj = GameObject.Find("PlayerSpawnPos");
                if (spawnObj != null)
                {
                    Vector3 spawnPos = spawnObj.transform.position;
                    GetComponent<Rigidbody>().MovePosition(spawnPos);
                }
                stageCheck.SetVisited(stage);
            }
            else
            {
                SectionManager[] sections = FindObjectsOfType<SectionManager>();

                foreach (SectionManager sec in sections)
                {
                    if (sec.Section == 
                        saveInfo.checkPoint)
                    {
                        //////Debug.Log(saveInfo.checkPoint);
                        //////Debug.Log(sec.spawnPos.position);
                        Vector3 pos = sec.spawnPos.position;
                        sec.ResetMap();
                        GetComponent<Rigidbody>().MovePosition(pos);
                        break;
                    }
                }
            }
        }
        else
        {
            GameObject spawnObj = GameObject.Find("PlayerSpawnPos");
            if (spawnObj != null)
            {
                Vector3 spawnPos = spawnObj.transform.position;
                GetComponent<Rigidbody>().MovePosition(spawnPos);
            }
            stageCheck.SetVisited(stage);
        }*/
    }

    public void CaptureBattleAttemptSnapshot()
    {
        SaveMetaState(false);
        if (GameSession.Exists)
            GameSession.Instance.CaptureBattleAttemptSnapshot(saveInfo);
    }

    public bool RestoreBattleAttemptSnapshot()
    {
        if (!GameSession.Exists)
            return false;

        SaveInfo restoredSnapshot = GameSession.Instance.RestoreBattleAttemptSnapshot();
        if (restoredSnapshot == null)
            return false;

        saveInfo = restoredSnapshot;
        NormalizeSaveInfo();
        ApplyLoadedData();
        return true;
    }

    private bool ShouldPersistMetaState()
    {
        if (!GameSession.IsGameplayScene(SceneManager.GetActiveScene().name))
            return true;

        SectionManager activeSection = FindFirstObjectByType<SectionManager>();
        return activeSection != null && activeSection.stageType == StageType.Lobby;
    }

    public bool HasPlayedDialogueEvent(string eventId)
    {
        return !string.IsNullOrWhiteSpace(eventId) &&
               playedDialogueEventIds != null &&
               playedDialogueEventIds.Contains(eventId);
    }

    public void MarkDialogueEventPlayed(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        playedDialogueEventIds ??= new List<string>();
        if (!playedDialogueEventIds.Contains(eventId))
            playedDialogueEventIds.Add(eventId);
    }

    public bool HasDialogueStateFlag(string stateFlag)
    {
        return !string.IsNullOrWhiteSpace(stateFlag) &&
               dialogueStateFlags != null &&
               dialogueStateFlags.Contains(stateFlag);
    }

    public void GrantDialogueStateFlag(string stateFlag)
    {
        if (string.IsNullOrWhiteSpace(stateFlag))
            return;

        dialogueStateFlags ??= new List<string>();
        if (!dialogueStateFlags.Contains(stateFlag))
            dialogueStateFlags.Add(stateFlag);
    }

}
public static class SecurePlayerPrefs
{
    public static string Encrypt(string plainText, string key)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetAesKey(key);
            aes.IV = new byte[16];
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encryptedBytes);
        }
    }
    public static string Decrypt(string encryptedText, string key)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetAesKey(key);
            aes.IV = new byte[16];
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
    private static byte[] GetAesKey(string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        Array.Resize(ref keyBytes, 32);
        return keyBytes;
    }
    public static void SetSecureString(string key, string value, string encryptionKey)
    {
        string encryptedValue = Encrypt(value, encryptionKey);
        PlayerPrefs.SetString(key, encryptedValue);
    }
    public static string GetSecureString(string key, string encryptionKey, string defaultValue = "")
    {
        if (PlayerPrefs.HasKey(key))
        {
            string encryptedValue = PlayerPrefs.GetString(key);
            return Decrypt(encryptedValue, encryptionKey);
        }
        return defaultValue;
    }
    public static void SetSecureInt(string key, int value, string encryptionKey)
    {
        SetSecureString(key, value.ToString(), encryptionKey);
    }
    public static int GetSecureInt(string key, string encryptionKey, int defaultValue = 0)
    {
        string stringValue = GetSecureString(key, encryptionKey, defaultValue.ToString());
        if (int.TryParse(stringValue, out int result))
        {
            return result;
        }
        return defaultValue;
    }
    public static void SetSecureFloat(string key, float value, string encryptionKey)
    {
        SetSecureString(key, value.ToString(), encryptionKey);
    }
    public static float GetSecureFloat(string key, string encryptionKey, float defaultValue = 0f)
    {
        string stringValue = GetSecureString(key, encryptionKey, defaultValue.ToString());
        if (float.TryParse(stringValue, out float result))
        {
            return result;
        }
        return defaultValue;
    }
}

public static class SecureJson
{
    public static string Encrypt(string plainText, string key)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetAesKey(key);
            aes.IV = new byte[16];
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encryptedBytes);
        }
    }
    public static string Decrypt(string encryptedText, string key)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = GetAesKey(key);
            aes.IV = new byte[16];
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
    private static byte[] GetAesKey(string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        Array.Resize(ref keyBytes, 32);
        return keyBytes;
    }
}

