using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.AddressableAssets;
using System.Linq;
using System;

[System.Serializable]
public class SkillUnlock
{
    public SkillType skillType;
    internal Image img;
    public GameObject locked;
    public GameObject skillObject;
    internal bool activated = false;  // 활성화 여부 (실행 중 내부 체크용)
    public VideoClip videoClip;
}

public class SkillManager : MonoBehaviour
{
    public static SkillManager instance;
    public static event Action OnChangeSkill;

    [Header("Skill Data Settings")]
    public List<SkillData> SkillData;
    public Dictionary<SkillType, SkillData> skillDictionary = new Dictionary<SkillType, SkillData>();

    [Header("Skill UI Settings")]
    public Button skill_BT;
    public Button rune_BT;
    public GameObject button_Frame;
    public GameObject skill_Window;
    public GameObject rune_Window;
    [Header("Skill Unlock Settings")]
    public SkillUnlock[] skillUnlocks;
    public GameObject newSkill;
    public VideoPlayer videoPlayer;
    UIParticleManager uIParticleManager;
    PlayerController playerController;
    private AsyncOperationHandle<IList<SkillData>> skillLoadHandel;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        playerController = FindFirstObjectByType<PlayerController>();
        uIParticleManager = FindFirstObjectByType<UIParticleManager>();

        skill_Window.SetActive(true);
        rune_Window.SetActive(false);
        skill_BT.interactable = false;
        rune_BT.interactable = true;

        skill_BT.onClick.AddListener(() =>
        {
            rune_Window.SetActive(false);
            skill_Window.SetActive(true);
            CheckUnlockableSkills();
            newSkill.SetActive(false);
            UnlockEffect();

            Image image = rune_BT.GetComponent<Image>();
            Color fixedColor;
            ColorUtility.TryParseHtmlString("#FFFFFF", out fixedColor);
            image.color = fixedColor;

            Image image2 = skill_BT.GetComponent<Image>();
            Color fixedColor2;
            ColorUtility.TryParseHtmlString("#787878", out fixedColor2);
            image2.color = fixedColor2;

            skill_BT.interactable = false;
            rune_BT.interactable = true;

            button_Frame.SetActive(false);
        });

        rune_BT.onClick.AddListener(() =>
        {
            skill_Window.SetActive(false);
            rune_Window.SetActive(true);
            uIParticleManager.SkillUnlockDel();

            Image image = skill_BT.GetComponent<Image>();
            Color fixedColor;
            ColorUtility.TryParseHtmlString("#FFFFFF", out fixedColor);
            image.color = fixedColor;

            Image image2 = rune_BT.GetComponent<Image>();
            Color fixedColor2;
            ColorUtility.TryParseHtmlString("#787878", out fixedColor2);
            image2.color = fixedColor2;

            skill_BT.interactable = true;
            rune_BT.interactable = false;

            button_Frame.SetActive(false);

        });

        // 모든 스킬 오브젝트 비활성화
        foreach (SkillUnlock skill in skillUnlocks)
        {
            if (skill.locked != null && skill.skillObject != null)
            {
                skill.locked.SetActive(true);
                skill.skillObject.SetActive(true);
                skill.img = skill.skillObject.GetComponent<Image>();

                Color fixedColor;
                ColorUtility.TryParseHtmlString("#222222", out fixedColor);
                fixedColor.a = 255f / 255f;
                skill.img.raycastTarget = false;

                skill.img.color = fixedColor;
                skill.activated = false;
            }
        }
        foreach (var data in SkillData)
        {
            if (data == null) continue;

            if (!skillDictionary.ContainsKey(data.skillType))
            {
                skillDictionary.Add(data.skillType, data);
            }
            else
            {
                Debug.LogWarning($"SkillManager: duplicate skill type '{data.skillType}' in asset '{data.name}'.");
            }
        }

        RefreshSkillIcons();

    }
    public void Start()
    {
        RefreshFromPlayerState();
        newSkill.SetActive(false);
        button_Frame.SetActive(false);

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
    }
    private void OnEnable()
    {
        GameSession.OnGameplayCacheReady += RefreshSkillIcons;
    }

    private void OnDisable()
    {
        GameSession.OnGameplayCacheReady -= RefreshSkillIcons;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        GameSession.OnGameplayCacheReady -= RefreshSkillIcons;
        foreach(SkillData skill in skillDictionary.Values)
        {
            if (skill != null)
            {
                skill.ReleasePrefabData();
            }
        }

        OnChangeSkill = null;
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
            UnlockAllSkillsForEditorTesting();
    }

    private void UnlockAllSkillsForEditorTesting()
    {
        if (PlayerInfo.Instance == null)
        {
            Debug.LogWarning("[SkillManager] PlayerInfo is not ready. Cannot unlock skills yet.");
            return;
        }

        int unlockedCount = 0;
        foreach (SkillUnlock skill in skillUnlocks)
        {
            if (skill == null)
                continue;

            SkillData skillData = GetSkillData(skill.skillType);
            if (skillData == null)
            {
                Debug.LogWarning($"[SkillManager] Missing SkillData for {skill.skillType}. Skipped editor unlock.");
                continue;
            }

            string skillName = skillData.skillType.ToString();
            if (PlayerInfo.Instance.learnedSkills.Contains(skillName))
                continue;

            PlayerInfo.Instance.learnedSkills.Add(skillName);
            unlockedCount++;
        }

        RefreshFromPlayerState();
        Debug.Log($"[SkillManager] Editor test: unlocked {unlockedCount} skills for this play session.");
    }
#endif

    private void RefreshSkillIcons()
    {
        if (SkillData != null)
        {
            foreach (SkillData data in SkillData)
            {
                data?.PreloadIconData();
            }
        }

        foreach (SkillData data in skillDictionary.Values)
        {
            data?.PreloadIconData();
        }

        OnChangeSkill?.Invoke();
    }

    public void RefreshFromPlayerState()
    {
        ResetSkillVisualState();
        CheckUnlockableSkills();
        CheckLearnedSkills();
        OnChangeSkill?.Invoke();
    }

    private void ResetSkillVisualState()
    {
        foreach (SkillUnlock skill in skillUnlocks)
        {
            if (skill == null)
                continue;

            if (skill.locked != null)
                skill.locked.SetActive(true);

            if (skill.skillObject != null)
                skill.skillObject.SetActive(true);

            if (skill.img == null && skill.skillObject != null)
                skill.img = skill.skillObject.GetComponent<Image>();

            if (skill.img != null)
            {
                Color fixedColor;
                ColorUtility.TryParseHtmlString("#222222", out fixedColor);
                fixedColor.a = 255f / 255f;
                skill.img.color = fixedColor;
                skill.img.raycastTarget = false;
            }

            skill.activated = false;
        }
    }
    public async Task DoubleSkillPreload(bool value = true)
    {
        var locationHandle = Addressables.LoadResourceLocationsAsync("DoubleSkill");
        IList<IResourceLocation> locations = await locationHandle.Task;

        HashSet<string> doubleSkillKeys = new HashSet<string>();
        foreach (var loc in locations)
        {
            string pureName = System.IO.Path.GetFileNameWithoutExtension(loc.PrimaryKey);
            doubleSkillKeys.Add(pureName);
            doubleSkillKeys.Add(loc.PrimaryKey);
        }

        if (skillDictionary == null || skillDictionary.Count == 0)
        {
            Debug.LogWarning("SkillManager: double-skill preload skipped because the skill cache is empty.");
            Addressables.Release(locationHandle);
            return;
        }

        foreach (SkillData skill in skillDictionary.Values)
        {
            if (skill == null) continue;

            string skillKey = skill.name;

            if (doubleSkillKeys.Contains(skillKey))
            {
                if (value)
                {
                    await skill.PreloadPrefabData();
                }
                else
                {
                    skill.ReleasePrefabData();
                }
            }
        }

        Addressables.Release(locationHandle);
    }
    public SkillData GetSkillData(SkillType type)
    {
        if (skillDictionary.TryGetValue(type, out SkillData data))
        {
            return data;
        }
        return null;
    }

    public void ResetRuntimeSkillStates()
    {
        foreach (SkillData data in skillDictionary.Values)
        {
            if (data != null)
                data.isActive = false;
        }
    }

    public void Play_Video(SkillType type)
    {
        VideoClip clip = null;
        foreach (SkillUnlock skill in skillUnlocks) { 
        if(skill.skillType == type)
            {
                clip = skill.videoClip;
                break;
            }
        }

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.time = 0;
        videoPlayer.Prepare();

        videoPlayer.prepareCompleted += OnPrepared;
    }

    void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();
    }

    public void ButtonIn(Button target)
    {
        if (target.interactable)
        {
            button_Frame.transform.localPosition = target.transform.localPosition;
            button_Frame.SetActive(true);
        }
    }
    public void ButtonOut()
    {
        button_Frame.SetActive(false);
    }

    public void UnlockEffect()
    {
        uIParticleManager.SkillUnlockDel();
        List<SkillUnlock> availableSkills = GetUnlockedButUnlearnedSkills();
        foreach (SkillUnlock skill in availableSkills)
        {
            uIParticleManager.SkillUnlock(skill.skillObject.transform as RectTransform);
        }
    }
    /// <summary>
    /// 현재 레벨에서 해금 가능한 스킬을 반영한다.
    /// </summary>
    public void CheckUnlockableSkills()
    {
        List<SkillUnlock> activeSkill = new List<SkillUnlock>();
        int currentLevel = (PlayerInfo.Instance != null) ? PlayerInfo.Instance.level : 0;
        foreach (SkillUnlock skill in skillUnlocks)
        {
            SkillData skillData = GetSkillData(skill.skillType);

            if (skill == null)
            {
                Debug.LogError("리스트의 어떤 요소가 Null입니다!");
            }
            else if (skillData == null)
            {
                Debug.LogError($"{skill.skillType.ToString()} 스크립트 안에 SkillData SO가 비어있습니다!");
                continue;
            }
            if (!skill.activated && currentLevel >= skillData.unlockLevel)
            {
                if (skill.locked != null)
                {
                    activeSkill.Add(skill);
                }
            }
        }

        foreach(var skill in activeSkill)
        {
            skill.locked.SetActive(false);
            skill.skillObject.SetActive(true);

            Color fixedColor;
            ColorUtility.TryParseHtmlString("#545454", out fixedColor);
            fixedColor.a = 255f / 255f;

            skill.img.color = fixedColor;
            skill.img.raycastTarget = true;
            skill.activated = true;
        }
    }
    public void CheckUnlockableNewSkills()
    {
        int currentLevel = (PlayerInfo.Instance != null) ? PlayerInfo.Instance.level : 0;
        foreach (SkillUnlock skill in skillUnlocks)
        {
                if (GetSkillData(skill.skillType).unlockLevel == currentLevel)
                {
                    newSkill.SetActive(true);
                }
        }
    }
    public void EqiupSkill(int index, string name)
    {
        PlayerInfo.Instance.equipSkill[index] = name;
        InvokeOnChangeSkill();
        PlayerInfo.Instance.Save();
    }
    public void LearnSkill(string skillName)
    {

        // SkillUnlock 중 해당 이름과 일치하는 것 찾기
        foreach (SkillUnlock skill in skillUnlocks)
        {
            SkillData skillData = GetSkillData(skill.skillType);
            if (skillData.skillType.ToString() == skillName)
            {
                if (!PlayerInfo.Instance.learnedSkills.Contains(skillName))
                {
                    PlayerInfo.Instance.learnedSkills.Add(skillName);
                }
                // 스킬 활성화
                if (skill.skillObject != null)
                {

                    skill.skillObject.SetActive(true);
                    Color fixedColor;
                    ColorUtility.TryParseHtmlString("#FFFFFF", out fixedColor);
                    fixedColor.a = 255f / 255f;
                    skill.img.color = fixedColor;
                    skill.img.raycastTarget = true;
                    skill.activated = true;
                }
            }
        }
        UnlockEffect();
        InvokeOnChangeSkill();
    }
    public void CheckLearnedSkills()
    {
        if (PlayerInfo.Instance == null) return;

        List<SkillUnlock> activeSkill = new List<SkillUnlock>();

        foreach (string learnedSkillName in PlayerInfo.Instance.learnedSkills)
        {
            foreach (SkillUnlock skill in skillUnlocks)
            {
                SkillData skillData = GetSkillData(skill.skillType);

                if (skillData.skillType.ToString() == learnedSkillName)
                {
                    if (skill.locked != null)
                    {
                        activeSkill.Add(skill);
                    }
                }
            }
        }

        foreach (SkillUnlock skill in activeSkill)
        {
            skill.skillObject.SetActive(true);
            skill.locked.SetActive(false);
            Color fixedColor;
            ColorUtility.TryParseHtmlString("#FFFFFF", out fixedColor);
            fixedColor.a = 255f / 255f;
            skill.img.color = fixedColor;
            skill.img.raycastTarget = true;
            skill.activated = true;
        }
    }
    public List<SkillUnlock> GetUnlockedButUnlearnedSkills()
    {
        List<SkillUnlock> result = new List<SkillUnlock>();

        foreach (SkillUnlock skill in skillUnlocks)
        {
            if (skill.activated &&
                !PlayerInfo.Instance.learnedSkills.Contains(GetSkillData(skill.skillType).skillType.ToString()))
            {
                result.Add(skill);
            }
        }

        return result;
    }
    public static void InvokeOnChangeSkill()
    {
        OnChangeSkill?.Invoke();
    }
    public static void ResetOnChangeSkill()
    {
        OnChangeSkill = null;
    }
#if UNITY_EDITOR
    public void RefreshAllSkillDataInEditor()
    {
        string assetFolderPath = "Assets/AddressableAsset/SkillData";

        if (!System.IO.Directory.Exists(assetFolderPath))
        {
            Debug.LogError($"SkillManager: asset folder not found. path={assetFolderPath}");
            return;
        }

        // 되돌리기(Ctrl+Z) 기능 지원
        UnityEditor.Undo.RecordObject(this, "Auto Assign Skill Data");

        // 폴더 내 모든 SkillData 타입 에셋 검색
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SkillData", new[] { assetFolderPath });
        List<SkillData> foundSkills = new List<SkillData>();

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            SkillData skill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(path);

            if (skill != null && !foundSkills.Contains(skill))
            {
                foundSkills.Add(skill);
            }
        }

        // 리스트 갱신 및 저장
        SkillData = foundSkills;
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"SkillManager: registered {foundSkills.Count} skill assets in the inspector.");
    }
#endif
}

