using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using System.Linq;
using Unity.VisualScripting;

public enum PassiveCombinationTestPreset
{
    None,
    Berserker_State_Scent_Stubborn,
    Berserker_State_Scent_Stubborn_Boiling,
    Berserker_State_Scent_Wounded_Crimson,
    Berserker_Attack_Crimson_Risk,
    Berserker_Attack_Crimson_Pact,
    Berserker_Attack_Risk_Pact,
    Berserker_Attack_Crimson_Risk_Pact_Heart,
    Berserker_Kill_Boiling_Feast,
    Berserker_Kill_Boiling_Heart,
    Berserker_Kill_Feast_Heart,
    Berserker_Kill_Boiling_Feast_Heart_Threshold,
    MoveSpeed_Feast_Hound,
    MoveSpeed_Feast_Hound_AttackBuff,
    Survival_Threshold_Pact,
    Survival_Threshold_Crimson,
    Survival_Threshold_Heart,
    Survival_Threshold_Crimson_Risk_Heart,
    Stigma_Burst_Contagious,
    Stigma_Burst_Penance,
    Stigma_Burst_Elegy,
    Stigma_Burst_Contagious_Penance_Elegy,
    Stigma_Full_Set,
    Stigma_Compact_Set,
    Recommended_Berserker_5Slot,
    Recommended_Stigma_5Slot,
}

public class PassiveSkillManager : MonoBehaviour
{
    private const int MaxMysticPassiveCount = 2;
    private const int FirstMysticGuaranteeSelection = 3;
    private const int FinalMysticGuaranteeSelection = 5;

    public static event Action OnChangedPassive;
    public Sprite blank_image;
    public GameObject passive_List;
    public GameObject passiveSelection;
    public GameObject passiveResult;
    private Dictionary<PassiveSkillType, Action> activePassiveActions
       = new Dictionary<PassiveSkillType, Action>();

    // 현재 활성화된 패시브 인스턴스들
    public List<PassiveSkillData> activePassives = new List<PassiveSkillData>();
    UIManager uIManager;
    PlayerSkill playerSkill;
    PlayerController playerController;
    UIParticleManager uIParticleManager;
    PassiveButton[] passiveButtons;
    Image[] passiveIcon;
    Image[] passive_images;
    private List<PassiveSkillData> offeredPassives = new List<PassiveSkillData>();
    private bool[] rerollUsed = Array.Empty<bool>();

    private Dictionary<PassiveSkillType, PassiveSkillData> passiveSkillData = new();

    [Header("Passive Combination Test")]
    [SerializeField] private PassiveCombinationTestPreset selectedTestPreset = PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn;
    [SerializeField] private bool logPassiveTestChecklistOnApply = true;

    // Start is called before the first frame update
    void Awake()
    {
        passive_images = passive_List.GetComponentsInChildren<Image>();
        passiveButtons = passiveSelection.GetComponentsInChildren<PassiveButton>(true);
        offeredPassives = Enumerable.Repeat<PassiveSkillData>(null, passiveButtons.Length).ToList();
        rerollUsed = new bool[passiveButtons.Length];
        passiveIcon = passiveResult.GetComponentsInChildren<Image>(true);
        uIParticleManager = FindFirstObjectByType<UIParticleManager>();
        playerSkill = FindFirstObjectByType<PlayerSkill>();
        playerController = FindFirstObjectByType<PlayerController>();
        uIManager = FindFirstObjectByType<UIManager>();
        passiveSkillData = GameSession.Instance.GetPassiveSkillMap().ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    void Start()
    {
        UpdatePassiveEvents();
        passiveSelection.SetActive(false);
    }

    public void LearnSkill(PassiveSkillData skillData)
    {
        PassiveSkillData targetSkill = GetPassiveData(skillData.skillType);


        if (targetSkill == null)
            return;

        if (targetSkill.rank == PassiveSkillRank.Mystic && GetOwnedMysticCount() >= MaxMysticPassiveCount)
        {
            InGameUI.ShowNotice($"미스틱 패시브는 최대 {MaxMysticPassiveCount}개까지 장착할 수 있습니다.");
            return;
        }

        for (int i = 0; i < PlayerInfo.Instance.equipPassiveSkill.Length; i++)
        {
            if (PlayerInfo.Instance.equipPassiveSkill[i] == skillData.skillType.ToString())
                return;
        }

        for (int i = 0; i < PlayerInfo.Instance.equipPassiveSkill.Length; i++)
        {
            if (string.IsNullOrEmpty(PlayerInfo.Instance.equipPassiveSkill[i]))
            {
                PlayerInfo.Instance.equipPassiveSkill[i] = skillData.skillType.ToString();
                RegisterPassive(skillData.skillType);
                InGameUI.ShowNotice(skillData.skillName + "을(를) 획득하였습니다");
                PlayerInfo.Instance.Save();
                UpdatePassiveEvents();
                return;
            }
        }
    }
    void RegisterPassive(PassiveSkillType type)
    {
        if (activePassives.Contains(GetPassiveData(type)))
            return;
        activePassives.Add(GetPassiveData(type));
    }

    public Sprite GetSprite(PassiveSkillType type, bool icon)
    {
        PassiveSkillData targetSkill = null;

        targetSkill = GetPassiveData(type);

        if(targetSkill == null)
            return null;
        else if(icon)
            return targetSkill.skillIcon;
        else return targetSkill.skillIcon_Frame;
    }

    public void RemovePassive(PassiveSkillType type)
    {
        foreach (var passive in activePassives)
            if(passive.skillType == type)
                activePassives.Remove(passive);
        UpdatePassiveEvents();
    }

    public void UpdatePassiveEvents()
    {
        if (PlayerInfo.Instance == null || passive_List == null || playerController == null)
            return;

        // 기존 등록된 패시브 전부 해제
        foreach (var passive in activePassives)
            passive.OnUnEquip(playerController);

        playerController.playerPassiveController.ResetPassiveBuildState();

        activePassives.Clear();

        // 현재 장착된 패시브 기준으로 재등록
        for (int i = 0; i < PlayerInfo.Instance.equipPassiveSkill.Length; i++)
        {
            string skillName = PlayerInfo.Instance.equipPassiveSkill[i];

            if (passive_images == null) return;

            passive_images[i].gameObject.SetActive(true);
            passive_images[i].sprite = blank_image;

            passiveIcon[i].sprite = null;
            passiveIcon[i].gameObject.SetActive(false);

            if (string.IsNullOrEmpty(skillName))
                continue;

            if (Enum.TryParse(skillName, true, out PassiveSkillType type))
            {
                PassiveSkillData skillData = GetPassiveData(type);
                if (skillData != null)
                {
                    RegisterPassive(skillData.skillType);
                }

                Sprite sprite = GetSprite(type, false);
                Sprite icon = GetSprite(type, true);

                if (sprite != null && icon != null)
                {
                    passiveIcon[i].sprite = icon;
                    passiveIcon[i].gameObject.SetActive(true);
                    passive_images[i].sprite = sprite;
                }
            }
            else
            {
                Debug.LogWarning($"PassiveSkillManager: unknown passive skill name '{skillName}'.");
            }
        }

        // 새 패시브 장착 효과 적용
        foreach (var passive in activePassives)
            passive.OnEquip(playerController);

        if (playerController != null && playerController.playerPassiveController != null)
            playerController.playerPassiveController.SyncStigmaEffectPreload();

        InvokeOnChangedPassive();
        playerController.playerPassiveController.UpdateHealthState();

    }

    public void PassiveSelect()
    {
        if (passiveSelection == null || passiveButtons == null || passiveButtons.Length == 0)
            return;

        if (uIManager != null)
            uIManager.CloseAllUI();

        if (playerController != null)
            playerController.SetLockPlayer(true);

        passiveSelection.SetActive(true);
        if (uIManager != null)
            uIManager.UpdateMouseState(true);

        PlayerSoundManager.PlaySound("LevelUP");

        offeredPassives = Enumerable.Repeat<PassiveSkillData>(null, passiveButtons.Length).ToList();
        rerollUsed = new bool[passiveButtons.Length];

        for (int i = 0; i < passiveButtons.Length; i++)
            passiveButtons[i].gameObject.SetActive(false);

        if (ShouldGuaranteeMystic())
        {
            int guaranteedSlot = UnityEngine.Random.Range(0, passiveButtons.Length);
            PassiveSkillData mysticOffer = RollMysticPassive(BuildOfferExclusions(guaranteedSlot, null));
            if (mysticOffer != null)
                offeredPassives[guaranteedSlot] = mysticOffer;
        }

        for (int i = 0; i < passiveButtons.Length; i++)
        {
            if (offeredPassives[i] != null)
                continue;

            PassiveSkillData offer = RollPassive(BuildOfferExclusions(i, null));
            if (offer != null)
                offeredPassives[i] = offer;
        }

        for (int i = 0; i < passiveButtons.Length; i++)
            RefreshOfferCard(i, true);
    }

    private void RefreshOfferCard(int slotIndex, bool playAppearEffect)
    {
        if (slotIndex < 0 || slotIndex >= passiveButtons.Length)
            return;

        PassiveButton card = passiveButtons[slotIndex];
        PassiveSkillData data = offeredPassives[slotIndex];
        if (card == null || data == null)
        {
            if (card != null)
                card.gameObject.SetActive(false);
            return;
        }

        card.gameObject.SetActive(true);
        if (card.IconImage != null)
            card.IconImage.sprite = data.skillIcon_Frame;

        card.mystic = data.rank == PassiveSkillRank.Mystic;
        bool canReroll = !rerollUsed[slotIndex] && HasRerollCandidate(slotIndex);
        PassiveSkillData selectedData = data;
        card.Configure(
            () => SelectPassive(selectedData),
            () => RerollSlot(slotIndex),
            canReroll);

        if (playAppearEffect)
            card.Appear();
    }

    private void SelectPassive(PassiveSkillData selectedData)
    {
        LearnSkill(selectedData);
        PlayerSoundManager.PlaySound("SummonAbilityStone_Drop");
        uIParticleManager?.RuneHover_Exit();
        StartCoroutine(PassiveSelected(0.6f));
    }

    private void RerollSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= offeredPassives.Count || rerollUsed[slotIndex])
            return;

        PassiveSkillData previous = offeredPassives[slotIndex];
        PassiveSkillData replacement = RollPassive(BuildOfferExclusions(slotIndex, previous));
        if (replacement == null)
        {
            passiveButtons[slotIndex].SetRerollAvailable(false);
            return;
        }

        rerollUsed[slotIndex] = true;
        offeredPassives[slotIndex] = replacement;
        RefreshOfferCard(slotIndex, true);
    }

    private bool HasRerollCandidate(int slotIndex)
    {
        return GetEligiblePassives(BuildOfferExclusions(slotIndex, offeredPassives[slotIndex])).Count > 0;
    }

    private HashSet<PassiveSkillType> BuildOfferExclusions(int slotIndex, PassiveSkillData previous)
    {
        HashSet<PassiveSkillType> exclusions = GetOwnedPassiveTypes();
        for (int i = 0; i < offeredPassives.Count; i++)
        {
            if (i != slotIndex && offeredPassives[i] != null)
                exclusions.Add(offeredPassives[i].skillType);
        }

        if (previous != null)
            exclusions.Add(previous.skillType);

        return exclusions;
    }

    private HashSet<PassiveSkillType> GetOwnedPassiveTypes()
    {
        HashSet<PassiveSkillType> owned = new HashSet<PassiveSkillType>();
        if (PlayerInfo.Instance == null || PlayerInfo.Instance.equipPassiveSkill == null)
            return owned;

        foreach (string skillName in PlayerInfo.Instance.equipPassiveSkill)
        {
            if (Enum.TryParse(skillName, true, out PassiveSkillType type))
                owned.Add(type);
        }

        return owned;
    }

    private List<PassiveSkillData> GetEligiblePassives(HashSet<PassiveSkillType> exclusions)
    {
        List<PassiveSkillData> eligible = new List<PassiveSkillData>();
        if (PlayerInfo.Instance == null)
            return eligible;

        bool reachedMysticLimit = GetOwnedMysticCount() >= MaxMysticPassiveCount;
        foreach (PassiveSkillData data in passiveSkillData.Values)
        {
            if (data != null &&
                data.unlockLevel <= PlayerInfo.Instance.level &&
                !exclusions.Contains(data.skillType) &&
                (!reachedMysticLimit || data.rank != PassiveSkillRank.Mystic))
            eligible.Add(data);
        }

        return eligible;
    }

    private PassiveSkillData RollPassive(HashSet<PassiveSkillType> exclusions)
    {
        List<PassiveSkillData> eligible = GetEligiblePassives(exclusions);
        return RollWeightedPassive(eligible);
    }

    private PassiveSkillData RollMysticPassive(HashSet<PassiveSkillType> exclusions)
    {
        if (GetOwnedMysticCount() >= MaxMysticPassiveCount)
            return null;

        List<PassiveSkillData> eligible = GetEligiblePassives(exclusions)
            .Where(data => data.rank == PassiveSkillRank.Mystic)
            .ToList();
        return RollWeightedPassive(eligible);
    }

    private PassiveSkillData RollWeightedPassive(List<PassiveSkillData> eligible)
    {
        if (eligible.Count == 0)
            return null;

        HashSet<PassiveSkillBuildType> ownedBuilds = GetOwnedBuildTypes();
        int totalWeight = 0;
        foreach (PassiveSkillData data in eligible)
            totalWeight += GetOfferWeight(data, ownedBuilds);

        int roll = UnityEngine.Random.Range(0, totalWeight);
        foreach (PassiveSkillData data in eligible)
        {
            roll -= GetOfferWeight(data, ownedBuilds);
            if (roll < 0)
                return data;
        }

        return eligible[eligible.Count - 1];
    }

    private bool ShouldGuaranteeMystic()
    {
        if (GetOwnedMysticCount() >= MaxMysticPassiveCount)
            return false;

        int nextSelectionIndex = GetOwnedPassiveTypes().Count + 1;
        return nextSelectionIndex == FirstMysticGuaranteeSelection ||
               nextSelectionIndex == FinalMysticGuaranteeSelection;
    }

    private int GetOwnedMysticCount()
    {
        int count = 0;
        foreach (PassiveSkillType type in GetOwnedPassiveTypes())
        {
            PassiveSkillData data = GetPassiveData(type);
            if (data != null && data.rank == PassiveSkillRank.Mystic)
                count++;
        }

        return count;
    }

    private HashSet<PassiveSkillBuildType> GetOwnedBuildTypes()
    {
        HashSet<PassiveSkillBuildType> builds = new HashSet<PassiveSkillBuildType>();
        foreach (PassiveSkillType type in GetOwnedPassiveTypes())
        {
            PassiveSkillData data = GetPassiveData(type);
            if (data != null && data.buildType != PassiveSkillBuildType.None)
                builds.Add(data.buildType);
        }

        return builds;
    }

    private int GetOfferWeight(PassiveSkillData data, HashSet<PassiveSkillBuildType> ownedBuilds)
    {
        return data.buildType != PassiveSkillBuildType.None && ownedBuilds.Contains(data.buildType) ? 3 : 1;
    }
    IEnumerator PassiveSelected(float time)
    {
        yield return new WaitForSeconds(time);
        passiveSelection.SetActive(false);

        if (uIParticleManager != null)
            uIParticleManager.RuneHover_Exit();

        if (playerController != null)
            playerController.SetLockPlayer(false);

        if (uIManager != null)
            uIManager.UpdateMouseState(false);
    }
    public PassiveSkillData GetPassiveData(PassiveSkillType type)
    {
        if (passiveSkillData.TryGetValue(type, out PassiveSkillData data))
        {
            return data;
        }
        return null;
    }
    public void InvokeOnChangedPassive()
    {
        OnChangedPassive?.Invoke();
    }
    public static void ResetOnChangedPassive()
    {
        OnChangedPassive = null;
    }

    public void ApplySelectedTestPreset()
    {
        ApplyPassiveTestPreset(selectedTestPreset);
    }

    public static PassiveCombinationTestPreset[] GetAllTestPresets()
    {
        PassiveCombinationTestPreset[] presets = (PassiveCombinationTestPreset[])Enum.GetValues(typeof(PassiveCombinationTestPreset));
        List<PassiveCombinationTestPreset> filtered = new List<PassiveCombinationTestPreset>();

        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] == PassiveCombinationTestPreset.None)
                continue;

            filtered.Add(presets[i]);
        }

        return filtered.ToArray();
    }

    public static string GetTestPresetDisplayName(PassiveCombinationTestPreset preset)
    {
        return GetPresetDisplayName(preset);
    }

    public void ApplyPassiveTestPreset(PassiveCombinationTestPreset preset)
    {
        if (preset == PassiveCombinationTestPreset.None)
        {
            Debug.LogWarning("[PassiveTest] 선택된 프리셋이 없습니다.");
            return;
        }

        PassiveSkillType[] loadout = GetPresetPassives(preset);
        if (loadout == null || loadout.Length == 0)
        {
            Debug.LogWarning($"[PassiveTest] 프리셋 {preset}에 등록된 패시브가 없습니다.");
            return;
        }

        ApplyPassiveLoadout(loadout, GetPresetDisplayName(preset));

        if (logPassiveTestChecklistOnApply)
        {
            LogPassiveTestPresetChecklist(preset);
        }
    }

    public void ClearPassiveLoadoutForTest()
    {
        if (PlayerInfo.Instance == null)
        {
            Debug.LogWarning("[PassiveTest] PlayerInfo.Instance가 없어 패시브 슬롯을 초기화할 수 없습니다.");
            return;
        }

        for (int i = 0; i < PlayerInfo.Instance.equipPassiveSkill.Length; i++)
        {
            PlayerInfo.Instance.equipPassiveSkill[i] = string.Empty;
        }

        UpdatePassiveEvents();
        PlayerInfo.Instance.Save();
        Debug.Log("[PassiveTest] 패시브 테스트 슬롯을 모두 비웠습니다.");
    }

    public void LogSelectedTestPresetChecklist()
    {
        LogPassiveTestPresetChecklist(selectedTestPreset);
    }

    public void LogPassiveTestPresetChecklist(PassiveCombinationTestPreset preset)
    {
        if (preset == PassiveCombinationTestPreset.None)
        {
            Debug.LogWarning("[PassiveTest] 체크리스트를 출력할 프리셋이 없습니다.");
            return;
        }

        string title = GetPresetDisplayName(preset);
        string purpose = GetPresetPurpose(preset);
        string expectedOutcome = GetPresetExpectedOutcome(preset);
        string[] checks = GetPresetChecks(preset);

        Debug.Log($"[PassiveTest] [{title}] 목적: {purpose}");
        Debug.Log($"[PassiveTest] [{title}] 기대 시너지: {expectedOutcome}");

        for (int i = 0; i < checks.Length; i++)
        {
            Debug.Log($"[PassiveTest] [{title}] 체크 {i + 1}. {checks[i]}");
        }
    }

    public void LogCurrentPassiveLoadout(string context = "Current")
    {
        if (PlayerInfo.Instance == null)
        {
            Debug.LogWarning("[PassiveTest] PlayerInfo.Instance가 없어 현재 패시브 장착 상태를 출력할 수 없습니다.");
            return;
        }

        List<string> equipped = new List<string>();
        for (int i = 0; i < PlayerInfo.Instance.equipPassiveSkill.Length; i++)
        {
            string passiveName = PlayerInfo.Instance.equipPassiveSkill[i];
            if (!string.IsNullOrWhiteSpace(passiveName))
            {
                equipped.Add(passiveName);
            }
        }

        string joined = equipped.Count > 0 ? string.Join(", ", equipped) : "없음";
        Debug.Log($"[PassiveTest] [{context}] 현재 장착 패시브: {joined}");
    }

    private void ApplyPassiveLoadout(IReadOnlyList<PassiveSkillType> loadout, string context)
    {
        if (PlayerInfo.Instance == null)
        {
            Debug.LogWarning("[PassiveTest] PlayerInfo.Instance가 없어 패시브 테스트 프리셋을 적용할 수 없습니다.");
            return;
        }

        for (int i = 0; i < PlayerInfo.Instance.equipPassiveSkill.Length; i++)
        {
            PlayerInfo.Instance.equipPassiveSkill[i] = string.Empty;
        }

        int slotCount = Mathf.Min(PlayerInfo.Instance.equipPassiveSkill.Length, loadout.Count);
        for (int i = 0; i < slotCount; i++)
        {
            PlayerInfo.Instance.equipPassiveSkill[i] = loadout[i].ToString();
        }

        UpdatePassiveEvents();
        PlayerInfo.Instance.Save();
        LogCurrentPassiveLoadout(context);
        InGameUI.ShowNotice($"{context} 테스트 프리셋 적용");
    }

    private static PassiveSkillType[] GetPresetPassives(PassiveCombinationTestPreset preset)
    {
        switch (preset)
        {
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn:
                return new[] { PassiveSkillType.Berserker_Scent_of_Blood, PassiveSkillType.Berserker_Stubborn_Survival };
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn_Boiling:
                return new[] { PassiveSkillType.Berserker_Scent_of_Blood, PassiveSkillType.Berserker_Stubborn_Survival, PassiveSkillType.Berserker_Boiling_Veins };
            case PassiveCombinationTestPreset.Berserker_State_Scent_Wounded_Crimson:
                return new[] { PassiveSkillType.Berserker_Scent_of_Blood, PassiveSkillType.Berserker_Wounded_Lion, PassiveSkillType.Berserker_Crimson_Recoil };
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk:
                return new[] { PassiveSkillType.Berserker_Crimson_Recoil, PassiveSkillType.Berserker_Risk_Awakening };
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Pact:
                return new[] { PassiveSkillType.Berserker_Crimson_Recoil, PassiveSkillType.Berserker_Blood_Pact };
            case PassiveCombinationTestPreset.Berserker_Attack_Risk_Pact:
                return new[] { PassiveSkillType.Berserker_Risk_Awakening, PassiveSkillType.Berserker_Blood_Pact };
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk_Pact_Heart:
                return new[] { PassiveSkillType.Berserker_Crimson_Recoil, PassiveSkillType.Berserker_Risk_Awakening, PassiveSkillType.Berserker_Blood_Pact, PassiveSkillType.Berserker_Heart_of_Slaughter };
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast:
                return new[] { PassiveSkillType.Berserker_Boiling_Veins, PassiveSkillType.Berserker_Blood_Feast };
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Heart:
                return new[] { PassiveSkillType.Berserker_Boiling_Veins, PassiveSkillType.Berserker_Heart_of_Slaughter };
            case PassiveCombinationTestPreset.Berserker_Kill_Feast_Heart:
                return new[] { PassiveSkillType.Berserker_Blood_Feast, PassiveSkillType.Berserker_Heart_of_Slaughter };
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast_Heart_Threshold:
                return new[] { PassiveSkillType.Berserker_Boiling_Veins, PassiveSkillType.Berserker_Blood_Feast, PassiveSkillType.Berserker_Heart_of_Slaughter, PassiveSkillType.Berserker_Deaths_Threshold };
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound:
                return new[] { PassiveSkillType.Berserker_Blood_Feast, PassiveSkillType.Stigma_Hound_Pursuit };
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound_AttackBuff:
                return new[] { PassiveSkillType.Berserker_Blood_Feast, PassiveSkillType.Stigma_Hound_Pursuit, PassiveSkillType.Increase_Attack };
            case PassiveCombinationTestPreset.Survival_Threshold_Pact:
                return new[] { PassiveSkillType.Berserker_Deaths_Threshold, PassiveSkillType.Berserker_Blood_Pact };
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson:
                return new[] { PassiveSkillType.Berserker_Deaths_Threshold, PassiveSkillType.Berserker_Crimson_Recoil };
            case PassiveCombinationTestPreset.Survival_Threshold_Heart:
                return new[] { PassiveSkillType.Berserker_Deaths_Threshold, PassiveSkillType.Berserker_Heart_of_Slaughter };
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson_Risk_Heart:
                return new[] { PassiveSkillType.Berserker_Deaths_Threshold, PassiveSkillType.Berserker_Crimson_Recoil, PassiveSkillType.Berserker_Risk_Awakening, PassiveSkillType.Berserker_Heart_of_Slaughter };
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious:
                return new[] { PassiveSkillType.Stigma_Infected_Burst, PassiveSkillType.Stigma_Contagious_Sin };
            case PassiveCombinationTestPreset.Stigma_Burst_Penance:
                return new[] { PassiveSkillType.Stigma_Infected_Burst, PassiveSkillType.Stigma_Lord_of_Penance };
            case PassiveCombinationTestPreset.Stigma_Burst_Elegy:
                return new[] { PassiveSkillType.Stigma_Infected_Burst, PassiveSkillType.Stigma_Abyssal_Elegy };
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious_Penance_Elegy:
                return new[] { PassiveSkillType.Stigma_Infected_Burst, PassiveSkillType.Stigma_Contagious_Sin, PassiveSkillType.Stigma_Lord_of_Penance, PassiveSkillType.Stigma_Abyssal_Elegy };
            case PassiveCombinationTestPreset.Stigma_Full_Set:
                return new[] { PassiveSkillType.Stigma_Infected_Burst, PassiveSkillType.Stigma_Contagious_Sin, PassiveSkillType.Stigma_Lord_of_Penance, PassiveSkillType.Stigma_Abyssal_Elegy, PassiveSkillType.Stigma_Hound_Pursuit };
            case PassiveCombinationTestPreset.Stigma_Compact_Set:
                return new[] { PassiveSkillType.Stigma_Infected_Burst, PassiveSkillType.Stigma_Contagious_Sin, PassiveSkillType.Stigma_Hound_Pursuit };
            case PassiveCombinationTestPreset.Recommended_Berserker_5Slot:
                return new[] { PassiveSkillType.Berserker_Crimson_Recoil, PassiveSkillType.Berserker_Blood_Pact, PassiveSkillType.Berserker_Deaths_Threshold, PassiveSkillType.Berserker_Blood_Feast, PassiveSkillType.Berserker_Heart_of_Slaughter };
            case PassiveCombinationTestPreset.Recommended_Stigma_5Slot:
                return new[] { PassiveSkillType.Stigma_Infected_Burst, PassiveSkillType.Stigma_Contagious_Sin, PassiveSkillType.Stigma_Lord_of_Penance, PassiveSkillType.Stigma_Abyssal_Elegy, PassiveSkillType.Stigma_Hound_Pursuit };
            default:
                return Array.Empty<PassiveSkillType>();
        }
    }

    private static string GetPresetDisplayName(PassiveCombinationTestPreset preset)
    {
        switch (preset)
        {
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn: return "버서커 상태 전이 2종";
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn_Boiling: return "버서커 상태 전이 3종";
            case PassiveCombinationTestPreset.Berserker_State_Scent_Wounded_Crimson: return "버서커 상태 전이 자해 교차";
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk: return "공격 HP 소모 - 붉은 반동 + 위험한 각성";
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Pact: return "공격 HP 소모 - 붉은 반동 + 피의 계약";
            case PassiveCombinationTestPreset.Berserker_Attack_Risk_Pact: return "공격 HP 소모 - 위험한 각성 + 피의 계약";
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk_Pact_Heart: return "공격 HP 소모 4종 스트레스";
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast: return "처치 이벤트 - 끓는 혈맥 + 피의 축제";
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Heart: return "처치 이벤트 - 끓는 혈맥 + 학살의 심장";
            case PassiveCombinationTestPreset.Berserker_Kill_Feast_Heart: return "처치 이벤트 - 피의 축제 + 학살의 심장";
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast_Heart_Threshold: return "처치 이벤트 4종 스트레스";
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound: return "이동속도 중첩 - 피의 축제 + 오염된 추적견";
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound_AttackBuff: return "이동속도 중첩 - 버프 포함";
            case PassiveCombinationTestPreset.Survival_Threshold_Pact: return "생존 조합 - 죽음의 문턱 + 피의 계약";
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson: return "생존 조합 - 죽음의 문턱 + 붉은 반동";
            case PassiveCombinationTestPreset.Survival_Threshold_Heart: return "생존 조합 - 죽음의 문턱 + 학살의 심장";
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson_Risk_Heart: return "생존 조합 4종 스트레스";
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious: return "낙인 최대스택 - 감염 파열 + 전염되는 죄";
            case PassiveCombinationTestPreset.Stigma_Burst_Penance: return "낙인 최대스택 - 감염 파열 + 참회의 군주";
            case PassiveCombinationTestPreset.Stigma_Burst_Elegy: return "낙인 최대스택 - 감염 파열 + 심연의 애가";
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious_Penance_Elegy: return "낙인 최대스택 4종 스트레스";
            case PassiveCombinationTestPreset.Stigma_Full_Set: return "낙인 5슬롯 실전 세트";
            case PassiveCombinationTestPreset.Stigma_Compact_Set: return "낙인 축약 세트";
            case PassiveCombinationTestPreset.Recommended_Berserker_5Slot: return "추천 실전 5슬롯 - 버서커";
            case PassiveCombinationTestPreset.Recommended_Stigma_5Slot: return "추천 실전 5슬롯 - 낙인";
            default: return preset.ToString();
        }
    }

    private static string GetPresetPurpose(PassiveCombinationTestPreset preset)
    {
        switch (preset)
        {
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn:
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn_Boiling:
            case PassiveCombinationTestPreset.Berserker_State_Scent_Wounded_Crimson:
                return "체력 구간 변경 이벤트와 상태별 modifier 반영 확인";
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk:
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Pact:
            case PassiveCombinationTestPreset.Berserker_Attack_Risk_Pact:
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk_Pact_Heart:
                return "한 번 공격에 여러 HP 소모형 패시브가 겹칠 때 계산과 해제 순서 확인";
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast:
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Heart:
            case PassiveCombinationTestPreset.Berserker_Kill_Feast_Heart:
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast_Heart_Threshold:
                return "처치 1회에 회복, 폭발, 스택, 버프가 중복 발동될 때 이벤트 순서 확인";
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound:
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound_AttackBuff:
                return "속도 modifier 키 중첩과 해제 독립성 확인";
            case PassiveCombinationTestPreset.Survival_Threshold_Pact:
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson:
            case PassiveCombinationTestPreset.Survival_Threshold_Heart:
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson_Risk_Heart:
                return "치명 피해 직전 발동, 무적, 자해, 처치 후속 효과가 한 흐름에서 꼬이지 않는지 확인";
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious:
            case PassiveCombinationTestPreset.Stigma_Burst_Penance:
            case PassiveCombinationTestPreset.Stigma_Burst_Elegy:
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious_Penance_Elegy:
            case PassiveCombinationTestPreset.Stigma_Full_Set:
            case PassiveCombinationTestPreset.Stigma_Compact_Set:
            case PassiveCombinationTestPreset.Recommended_Stigma_5Slot:
                return "낙인 최대스택 등록, 발동, 전파, 초기화가 중복 없이 처리되는지 확인";
            case PassiveCombinationTestPreset.Recommended_Berserker_5Slot:
                return "실전형 5슬롯 장시간 플레이 스트레스 테스트";
            default:
                return "패시브 조합 테스트";
        }
    }

    private static string[] GetPresetChecks(PassiveCombinationTestPreset preset)
    {
        switch (preset)
        {
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn:
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn_Boiling:
            case PassiveCombinationTestPreset.Berserker_State_Scent_Wounded_Crimson:
                return new[]
                {
                    "71 / 70 / 41 / 40 / 26 / 25% HP 구간에서 상태 로그가 한 번씩만 찍히는지 확인",
                    "이전 상태 modifier가 남지 않고 새 상태 modifier만 유지되는지 확인",
                    "붉은 반동 자해 직후 상태가 한 단계 더 내려가도 연쇄 이상이 없는지 확인",
                };
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk:
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Pact:
            case PassiveCombinationTestPreset.Berserker_Attack_Risk_Pact:
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk_Pact_Heart:
                return new[]
                {
                    "공격 1회당 HP 감소량이 의도한 합만큼만 빠지는지 확인",
                    "HP 1 보정 패시브가 있으면 진짜 1에서 멈추는지 확인",
                    "OnAttack 증뎀 적용 후 EndAttack에서 modifier가 정상 해제되는지 확인",
                    "피의 계약 modifier가 HP 감소 직후 재계산되는지 확인",
                };
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast:
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Heart:
            case PassiveCombinationTestPreset.Berserker_Kill_Feast_Heart:
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast_Heart_Threshold:
                return new[]
                {
                    "적 1킬당 처치 이벤트 로그가 패시브 수만큼 정확히 한 번씩만 호출되는지 확인",
                    "학살의 심장이 강화된 공격 상태일 때만 폭발하는지 확인",
                    "피의 축제 스택과 이동속도 증가가 처치 수와 일치하는지 확인",
                    "죽음의 문턱 활성 중 다른 회복 패시브와 합쳐져도 과회복이 없는지 확인",
                };
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound:
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound_AttackBuff:
                return new[]
                {
                    "속도 효과가 동시에 붙으면 실제 이동속도가 더 빨라지는지 확인",
                    "한 효과가 끝나도 다른 키의 속도 효과는 유지되는지 확인",
                    "스택 리셋 후 속도가 정상 원복되는지 확인",
                };
            case PassiveCombinationTestPreset.Survival_Threshold_Pact:
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson:
            case PassiveCombinationTestPreset.Survival_Threshold_Heart:
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson_Risk_Heart:
                return new[]
                {
                    "치명 피해를 받을 때 죽음의 문턱이 먼저 받아 HP 1 생존이 되는지 확인",
                    "무적 중 추가 피해나 자해로 다시 죽지 않는지 확인",
                    "무적 종료 후 공격 modifier가 정상 해제되는지 확인",
                    "활성 중 처치 시 회복, 폭발, 강화공격 해제가 순서대로 동작하는지 확인",
                };
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious:
            case PassiveCombinationTestPreset.Stigma_Burst_Penance:
            case PassiveCombinationTestPreset.Stigma_Burst_Elegy:
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious_Penance_Elegy:
            case PassiveCombinationTestPreset.Stigma_Full_Set:
            case PassiveCombinationTestPreset.Stigma_Compact_Set:
            case PassiveCombinationTestPreset.Recommended_Stigma_5Slot:
                return new[]
                {
                    "같은 적에 최대스택 트리거가 중복 등록되어도 같은 패시브가 두 번 안 터지는지 확인",
                    "최대스택 도달 시 등록된 효과 수만큼만 실행되는지 확인",
                    "폭발 후 스택 초기화와 주변 전파가 무한 루프로 이어지지 않는지 확인",
                };
            case PassiveCombinationTestPreset.Recommended_Berserker_5Slot:
                return new[]
                {
                    "장시간 플레이 중 같은 이벤트 로그가 2번 이상 찍히지 않는지 확인",
                    "HP 1 보정, 자해, 처치 회복, 이동속도 스택이 실전 전투에서 동시에 안정적으로 동작하는지 확인",
                    "상태 전이 후 modifier 잔류가 없는지 확인",
                };
            default:
                return new[] { "적용 후 콘솔의 [PassiveDebug] 로그를 기준으로 동작 여부를 확인" };
        }
    }

    private static string GetPresetExpectedOutcome(PassiveCombinationTestPreset preset)
    {
        switch (preset)
        {
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn:
                return "HP가 70% 아래로 내려가면 공격/피격 modifier가 함께 바뀌고, 40%와 25% 구간에서 더 강한 버서커 상태로 자연스럽게 이어져야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_State_Scent_Stubborn_Boiling:
                return "체력이 낮아질수록 공격력과 생존력이 같이 올라가고, 처치 시 즉시 회복이 들어와 버서커 상태를 유지하거나 한 단계 회복하는 흐름이 보여야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_State_Scent_Wounded_Crimson:
                return "붉은 반동 자해로 체력 구간이 빨리 내려가면서 피비린내와 상처 입은 사자의 증뎀이 더 빨리 켜지고, 공격 후 광역 타격까지 붙어 공격적으로 굴러가야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk:
                return "한 번 공격할 때 체력을 소모하는 대신 즉발 공격력 증가가 함께 붙어, 자해 리스크와 순간 화력이 같이 커지는 형태로 동작해야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Pact:
                return "공격할수록 HP가 줄고, 줄어든 HP 비율만큼 피의 계약 증뎀이 누적되어 후속 공격이 점점 더 강해져야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Attack_Risk_Pact:
                return "위험한 각성의 1회성 스킬 증뎀과 피의 계약의 누적 증뎀이 겹쳐, 체력이 낮을수록 같은 스킬 공격이 훨씬 아프게 들어가야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Attack_Crimson_Risk_Pact_Heart:
                return "자해형 공격 패시브들이 서로 체력을 깎아 피의 계약 증뎀을 키우고, 학살의 심장 강화 공격까지 겹쳐서 한 번의 공격과 처치 후 폭발이 연쇄적으로 강해져야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast:
                return "적을 잡을수록 끓는 혈맥 회복과 피의 축제 스택이 동시에 쌓여, 전투가 길어질수록 유지력과 이동속도가 같이 올라가야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Heart:
                return "강화된 공격으로 적을 마무리하면 학살의 심장 폭발과 처치 회복이 함께 터져, 마무리 한 번이 회복과 광역딜로 이어져야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Kill_Feast_Heart:
                return "적 처치가 곧 이동속도 스택과 강화 공격 폭발의 기점이 되어, 한 번 킬을 내면 다음 타겟으로 더 빨리 이어 붙는 템포가 만들어져야 정상입니다.";
            case PassiveCombinationTestPreset.Berserker_Kill_Boiling_Feast_Heart_Threshold:
                return "위험한 저체력 상태에서 생존 후, 처치 한 번으로 회복·이속·폭발이 동시에 터지며 역전하는 버서커 루프가 만들어져야 정상입니다.";
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound:
                return "피의 축제 처치 스택 이속과 오염된 추적견 이속이 동시에 유지되어, 이동이 체감상 확실히 더 빨라지고 하나가 꺼져도 다른 하나는 남아 있어야 정상입니다.";
            case PassiveCombinationTestPreset.MoveSpeed_Feast_Hound_AttackBuff:
                return "이동속도 시너지 위에 일반 공격 버프까지 얹혀, 빠르게 추적하고 붙어서 더 강하게 때리는 전투 흐름이 유지되어야 정상입니다.";
            case PassiveCombinationTestPreset.Survival_Threshold_Pact:
                return "죽음의 문턱으로 살아남은 직후 낮아진 체력 덕분에 피의 계약 증뎀이 크게 올라, 빈사 상태에서 반격 화력이 확 뛰어야 정상입니다.";
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson:
                return "죽음의 문턱으로 버틴 뒤 붉은 반동의 공격-광역 후속타가 살아 있어, 위험한 상태에서도 계속 딜 압박을 넣는 흐름이 보여야 정상입니다.";
            case PassiveCombinationTestPreset.Survival_Threshold_Heart:
                return "죽음의 문턱으로 생존 후 강화 공격으로 적을 잡으면, 학살의 심장 폭발과 회복이 이어져 빈사에서 역전하는 감각이 나와야 정상입니다.";
            case PassiveCombinationTestPreset.Survival_Threshold_Crimson_Risk_Heart:
                return "자해와 빈사 증뎀이 모두 겹쳐 극단적인 하이리스크 하이리턴 조합처럼 동작하고, 살아남기만 하면 폭발적인 반격이 나와야 정상입니다.";
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious:
                return "최대 낙인 적이 터질 때 주변 적에게 낙인이 퍼져, 한 적의 폭발이 다음 적 낙인 누적으로 이어지는 연쇄 감염 구조가 보여야 정상입니다.";
            case PassiveCombinationTestPreset.Stigma_Burst_Penance:
                return "최대 낙인 도달 순간 참회의 군주 추가 피해가 정확히 들어가고, 감염 파열의 폭발 딜과 함께 순간 폭딜처럼 터져야 정상입니다.";
            case PassiveCombinationTestPreset.Stigma_Burst_Elegy:
                return "최대 낙인 적이 터진 뒤 주변 적에게 심연의 애가가 다시 스택을 뿌려서, 폭발 후 바로 다음 연계 준비가 되는 구조여야 정상입니다.";
            case PassiveCombinationTestPreset.Stigma_Burst_Contagious_Penance_Elegy:
                return "한 적의 최대 낙인 폭발이 피해, 전염, 추가 스택 살포를 동시에 일으켜서 지속딜과 폭발딜이 연속적으로 이어져야 정상입니다.";
            case PassiveCombinationTestPreset.Stigma_Full_Set:
                return "여러 적에게 낙인이 빠르게 누적되고, 최대 스택 적이 터질 때 주변 전파와 추가 피해가 이어지며 화면 전체에 낙인 연쇄가 굴러가야 정상입니다.";
            case PassiveCombinationTestPreset.Stigma_Compact_Set:
                return "적은 수의 낙인 패시브만으로도 낙인 누적, 폭발, 이동 추적성이 짧은 사이클 안에서 깔끔하게 보이면 정상입니다.";
            case PassiveCombinationTestPreset.Recommended_Berserker_5Slot:
                return "공격할수록 스스로 체력을 깎아 더 강해지고, 빈사 생존 후 처치로 회복·폭발·이속이 한꺼번에 이어지는 버서커 캐리 루프가 보여야 정상입니다.";
            case PassiveCombinationTestPreset.Recommended_Stigma_5Slot:
                return "한 적에 낙인을 쌓아 터뜨리면 주변 전파와 추가 스택, 추가 피해가 연속해서 이어지며 광역 연쇄 딜 세팅처럼 굴러가야 정상입니다.";
            default:
                return "콘솔에서 로그 순서와 수치를 보며 의도한 시너지 방향으로 굴러가는지 확인합니다.";
        }
    }
}



