using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;


[System.Serializable]
public enum BuffDisplayMode
{
    Timed,
    Persistent
}

[System.Serializable]
public class BuffSlot
{
    public SkillData data;
    public Image icon;
    public Coroutine routine;
    public float remainingTime;
    public float duration;
    public BuffDisplayMode displayMode;
}


public class InGameUI : MonoBehaviour
{
    public const float LevelUpPresentationDuration = 2.8333333f;

    public static InGameUI Instance { get; private set; }

    [Header("Notice Settings")]
    public GameObject notice;
    public GameObject warning;
    private CanvasGroup noticeCanvasGroup;
    private CanvasGroup warningCanvasGroup;
    private TMP_Text noticeText;
    private TMP_Text warningText;
    private Coroutine noticeCoroutine;
    private Coroutine warningCoroutine;

    [Header("Dialogue Settings")]
    public GameObject MainBar;
    public GameObject dialogue;
    public TMP_Text diaName;
    public TMP_Text diacontent;
    public Image portrait;
    private CanvasGroup mainBarCanvasGroup;

    [Header("TargetEmeny Settings")]
    public GameObject targetHPUI;
    public Image targetHP;
    public TMP_Text targetName;

    [Header("Boss Settings")]
    public GameObject targetHPUI_Boss;
    public Image targetHP_Boss;
    public TMP_Text targetName_Boss;

    [Header("HP&Stamina Settings")]
    public Image hpBar;
    public Image staminaBar;
    public Image shieldBar;
    float maxHP;
    float maxStamina;
    float shield;
    private float currentHP;
    private float currentStamina;
    private RectTransform HPRectTransform;
    private RectTransform MPRectTransform;

    private Vector2 HPoriginalPos;
    private Vector2 MPoriginalPos;
    public float shakeAmount = 3f;  // 흔들림 강도
    public float shakeSpeed = 2f;  // 흔들리는 속도
    UIParticleManager uIParticleManager;

    [Header("Level Settings")]
    [SerializeField] private Text levelText;  // 첫 번째 레벨 텍스트 UI
    public GameObject levelUPUI;
    TMP_Text levelUPText;
    private Animator levelUpAnimator;
    private CanvasGroup levelUpCanvasGroup;

    [Header("Experience Settings")]
    public Image EXPBar;
    float maxEXP;
    private float currentEXP;

    [Header("Dash & HPStone Settings")]
    public Text Dash;
    public Image dashIcon;
    public Text HPStone;
    public Image hpStoneIcon;

    [Header("BuffSkill Settings")]
    public GameObject buffSkillUI;
    [SerializeField] private BuffSlot[] buffSlots = new BuffSlot[5];
    PlayerTargetSystem playerTargetSystem;
    PlayerController playerController;
    PlayerMovement playerMovement;
    UIManager uiManager;
    PlayerInput playerInput;
    Dash dash;
    Image[] buffSkillSlot;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); // 중복 방지
    }

    void Start()
    {
        Transform textChild = notice.transform.GetChild(0);
        noticeText = textChild.GetComponent<TMP_Text>();
        noticeCanvasGroup = notice.GetComponent<CanvasGroup>();
        if (noticeCanvasGroup == null)
        {
            noticeCanvasGroup = notice.AddComponent<CanvasGroup>();
        }
        notice.SetActive(false);

        Transform textChild2 = warning.transform.GetChild(0);
        warningText = textChild2.GetComponent<TMP_Text>();
        warningCanvasGroup = warning.GetComponent<CanvasGroup>();
        if (warningCanvasGroup == null)
        {
            warningCanvasGroup = warning.AddComponent<CanvasGroup>();
        }
        warning.SetActive(false);

        uIParticleManager =FindFirstObjectByType<UIParticleManager>();
        playerTargetSystem = FindFirstObjectByType<PlayerTargetSystem>();
        playerController = FindFirstObjectByType<PlayerController>();
        playerMovement = FindFirstObjectByType<PlayerMovement>();
        levelUPText = levelUPUI.GetComponentInChildren<TMP_Text>();
        levelUpAnimator = levelUPUI != null ? levelUPUI.GetComponent<Animator>() : null;
        levelUpCanvasGroup = levelUPUI != null ? levelUPUI.GetComponent<CanvasGroup>() : null;
        if (levelUPUI != null && levelUpCanvasGroup == null)
            levelUpCanvasGroup = levelUPUI.AddComponent<CanvasGroup>();
        maxEXP = GetSafeMaxExp();
        maxHP = PlayerInfo.Instance.maxHP;
        maxStamina = PlayerInfo.Instance.maxstamina;
        shield = PlayerInfo.Instance.shield;
        currentEXP = PlayerInfo.Instance.exp;
        currentHP = PlayerInfo.Instance.currentHP;
        currentStamina = PlayerInfo.Instance.stamina;
        HPRectTransform = hpBar.GetComponent<RectTransform>();
        MPRectTransform = staminaBar.GetComponent<RectTransform>();
        HPoriginalPos = HPRectTransform.localPosition;
        MPoriginalPos = MPRectTransform.localPosition;
        StatusChange();
        PlayerInfo.OnChangeStatus += StatusChange;
        PlayerInfo.OnProfileLoaded += StatusChange;
       Inventory.OnChangeInventory += StatusChange;
        targetHPUI.gameObject.SetActive(false); 
        buffSkillSlot = buffSkillUI.GetComponentsInChildren<Image>();
        for (int i = 0; i < buffSlots.Length; i++)
        {
            buffSkillSlot[i].sprite = null;
            buffSkillSlot[i].gameObject.SetActive(false);
        }

        if (MainBar != null)
        {
            mainBarCanvasGroup = MainBar.GetComponent<CanvasGroup>();
            if (mainBarCanvasGroup == null)
                mainBarCanvasGroup = MainBar.AddComponent<CanvasGroup>();
        }
    }

    void Update()
    {
        ShakeBar();
        if (playerTargetSystem.lockOnTarget != null)
        {
            TargetEnemyInfo();
        }
        else
        {
            targetHPUI.gameObject.SetActive(false);
            targetHPUI_Boss.gameObject.SetActive(false);
        }
    }

    void TargetEnemyInfo()
    {
        if (playerTargetSystem.lockOnTarget.GetComponent<EnemyClass>() != null)
        {
            EnemyClass enemy = playerTargetSystem.lockOnTarget.GetComponent<EnemyClass>();

            if (enemy.IsBoss)
            {
                targetHPUI_Boss.gameObject.SetActive(true);
                targetName_Boss.text = TextSanitizer.Sanitize(enemy.enemyName);
                targetHP_Boss.fillAmount = (float)enemy.currentHP / enemy.maxHP;
                targetHPUI.gameObject.SetActive(false);
            }
            else
            {
                targetHPUI.gameObject.SetActive(true);
                targetName.text = TextSanitizer.Sanitize(enemy.enemyName);
                targetHP.fillAmount = (float)enemy.currentHP / enemy.maxHP;
                targetHPUI_Boss.gameObject.SetActive(false);
            }
        }
    }
    void ClearAllEnemyHPBars()
    {
        GameObject container = GameObject.Find("EnemyHPBar");
        if (container != null)
        {
            foreach (Transform child in container.transform)
            {
                Destroy(child.gameObject);
            }
        }
    }
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        // 이벤트 구독 해제
        PlayerInfo.OnChangeStatus -= StatusChange;
        PlayerInfo.OnProfileLoaded -= StatusChange;
       Inventory.OnChangeInventory -= StatusChange;
    }

    void OnDestroy()
    {
        // 혹시라도 파괴될 때 이벤트 구독 제거
        PlayerInfo.OnChangeStatus -= StatusChange;
        PlayerInfo.OnProfileLoaded -= StatusChange;
       Inventory.OnChangeInventory -= StatusChange;

    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAllEnemyHPBars();
    }
    Coroutine hitEffect = null;
    bool hit = false;
    public void HitEffect()
    {
        if(hitEffect == null)
        hitEffect = StartCoroutine(HitEffectCor());
    }
    IEnumerator HitEffectCor()
    {
        hit = true;
        uIParticleManager.HPHit();
        yield return new WaitForSeconds(0.25f);
        hit = false;
        hitEffect = null;
        HPWarning();
    }
    void HPWarning()
    {
        if(hit)
            return;
        if (currentHP / maxHP <= 0.3)
        {
            if (uIParticleManager != null)
                uIParticleManager.HPVignetting(true);
        }
        else
        {
            if (uIParticleManager != null)
                uIParticleManager.HPVignetting(false);
        }
    }
    public void BuffSkillON(SkillData data, float duration)
    {
        BuffSkillON(data, duration, BuffDisplayMode.Timed);
    }
    public void BuffSkillONPersistent(SkillData data)
    {
        BuffSkillON(data, 0f, BuffDisplayMode.Persistent);
    }
    public void BuffSkillON(SkillData data, float duration, BuffDisplayMode displayMode)
    {
        if (data == null)
            return;

        int existingIndex = FindBuffIndex(data.skillType);
        if (existingIndex >= 0)
        {
            StartBuff(existingIndex, data, duration, displayMode);
            return;
        }

        // 빈 슬롯 찾기
        for (int i = 0; i < buffSlots.Length; i++)
        {
            if (buffSlots[i].data == null)
            {
                StartBuff(i, data, duration, displayMode);
                return;
            }
        }

        // 슬롯 다 찼을 때 처리 (선택)
        //Debug.Log("Buff slots full");
    }
    void StartBuff(int index, SkillData data, float duration, BuffDisplayMode displayMode)
    {
        BuffSlot slot = buffSlots[index];

        if (slot.routine != null)
        {
            StopCoroutine(slot.routine);
            slot.routine = null;
        }

        slot.data = data;
        data?.PreloadIconData();
        slot.icon.sprite = data.skillIcon;
        slot.icon.fillAmount = 1f;
        slot.icon.gameObject.SetActive(true);
        slot.displayMode = displayMode;

        if (displayMode == BuffDisplayMode.Persistent)
        {
            slot.duration = 0f;
            slot.remainingTime = 0f;
            return;
        }

        slot.duration = Mathf.Max(duration, 0.01f);
        slot.remainingTime = slot.duration;
        slot.routine = StartCoroutine(BuffDurationRoutine(slot));
    }

    IEnumerator BuffDurationRoutine(BuffSlot slot)
    {
        while (slot.displayMode == BuffDisplayMode.Timed && slot.remainingTime > 0f)
        {
            slot.remainingTime -= Time.deltaTime;
            slot.icon.fillAmount = slot.remainingTime / slot.duration;
            yield return null;
        }

        if (slot.displayMode == BuffDisplayMode.Timed)
        {
            RemoveBuff(slot);
        }
    }

    void RemoveBuff(BuffSlot target)
    {
        int index = System.Array.IndexOf(buffSlots, target);

        if (index < 0)
            return;

        RemoveBuffAt(index);
    }
    public bool ForceRemoveBuff(SkillType skillType)
    {
        int index = FindBuffIndex(skillType);
        if (index >= 0)
        {
            RemoveBuffAt(index);
            return true;
        }

        return false;
    }
    void RemoveBuffAt(int index)
    {
        if (index < 0 || index >= buffSlots.Length)
            return;

        // 삭제 위치부터 끝까지 기존 코루틴 전부 정지
        for (int i = index; i < buffSlots.Length; i++)
        {
            if (buffSlots[i].routine != null)
            {
                StopCoroutine(buffSlots[i].routine);
                buffSlots[i].routine = null;
            }
        }

        // 한 칸씩 당기기
        for (int i = index; i < buffSlots.Length - 1; i++)
        {
            CopySlotDataOnly(buffSlots[i + 1], buffSlots[i]);
        }

        // 마지막 슬롯 비우기
        ClearSlot(buffSlots[buffSlots.Length - 1]);

        // 삭제 위치부터 끝까지 살아있는 버프 코루틴 재시작
        for (int i = index; i < buffSlots.Length; i++)
        {
            if (buffSlots[i].data != null && buffSlots[i].displayMode == BuffDisplayMode.Timed && buffSlots[i].remainingTime > 0f)
            {
                buffSlots[i].routine = StartCoroutine(BuffDurationRoutine(buffSlots[i]));
            }
        }
    }
    void CopySlotDataOnly(BuffSlot from, BuffSlot to)
    {
        to.data = from.data;

        to.icon.sprite = from.icon.sprite;
        to.icon.fillAmount = from.icon.fillAmount;
        to.icon.gameObject.SetActive(to.data != null);

        to.remainingTime = from.remainingTime;
        to.duration = from.duration;
        to.displayMode = from.displayMode;

        // 코루틴은 여기서 복사하지 않음
        to.routine = null;
    }
    void ClearSlot(BuffSlot slot)
    {
        slot.data = null;

        slot.icon.sprite = null;
        slot.icon.fillAmount = 0f;
        slot.icon.gameObject.SetActive(false);   // ★ 핵심

        slot.routine = null;
        slot.remainingTime = 0f;
        slot.duration = 0f;
        slot.displayMode = BuffDisplayMode.Timed;
    }


    void CopySlot(BuffSlot from, BuffSlot to)
    {
        if (to.routine != null)
        {
            StopCoroutine(to.routine);
            to.routine = null;
        }

        to.data = from.data;

        to.icon.sprite = from.icon.sprite;
        to.icon.fillAmount = from.icon.fillAmount;
        to.icon.gameObject.SetActive(to.icon.sprite != null);

        to.remainingTime = from.remainingTime;
        to.duration = from.duration;
        to.displayMode = from.displayMode;

        if (from.data != null && to.displayMode == BuffDisplayMode.Timed && to.remainingTime > 0f)
        {
            to.routine = StartCoroutine(BuffDurationRoutine(to));
        }
    }
    int FindBuffIndex(SkillType skillType)
    {
        for (int i = 0; i < buffSlots.Length; i++)
        {
            if (buffSlots[i].data != null && buffSlots[i].data.skillType == skillType)
            {
                return i;
            }
        }

        return -1;
    }
    public void StatusChange()
    {
        if (PlayerInfo.Instance == null) return;
        currentHP = PlayerInfo.Instance.currentHP;
        maxHP = PlayerInfo.Instance.FinalMaxHP;
        if(hpBar !=null)
        hpBar.fillAmount = currentHP / maxHP;
        HPWarning();
        currentStamina = PlayerInfo.Instance.stamina;
        maxStamina = PlayerInfo.Instance.maxstamina;

        staminaBar.fillAmount = currentStamina / maxStamina;

        shield = PlayerInfo.Instance.shield;
        shieldBar.fillAmount = shield / maxHP;

        currentEXP = PlayerInfo.Instance.exp;
        maxEXP = GetSafeMaxExp();
        EXPBar.fillAmount = currentEXP / maxEXP;

        levelText.text = PlayerInfo.Instance.level.ToString();
        HPStone.text = PlayerInfo.Instance.hpStone.ToString() + "/ " + PlayerInfo.Instance.maxHealStoneCount.ToString();

        dash = FindFirstObjectByType<Dash>();

        Dash.text = dash.currentDashCount.ToString() + "/" + dash.MaxDashCount.ToString();

        if (dash.currentDashCount == 0)
        {
            Color color = dashIcon.color;
            color.a = 37f / 255f;  // 또는 0.145f;
            dashIcon.color = color;
        }
        else
        {
            Color color = dashIcon.color;
            color.a = 255f / 255f;  // 또는 0.145f;
            dashIcon.color = color;
        }
        if (PlayerInfo.Instance.hpStone == 0)
        {
            Color color = hpStoneIcon.color;
            color.a = 37f / 255f;  // 또는 0.145f;
            hpStoneIcon.color = color;
        }
        else
        {
            Color color = hpStoneIcon.color;
            color.a = 255f / 255f;  // 또는 0.145f;
            hpStoneIcon.color = color;
        }

    }

    private float GetSafeMaxExp()
    {
        if (PlayerInfo.Instance == null || PlayerInfo.Instance.needExp == null || PlayerInfo.Instance.needExp.Length == 0)
            return 1f;

        int levelIndex = Mathf.Clamp(PlayerInfo.Instance.level - 1, 0, PlayerInfo.Instance.needExp.Length - 1);
        return Mathf.Max(1, PlayerInfo.Instance.needExp[levelIndex]);
    }

    void ShakeBar()
    {
        if (HPRectTransform == null || MPRectTransform == null)
            return; // 안전하게 빠져나가기

        // 경계선 부분만 흔들리는 애니메이션
        float shakeX = Mathf.Cos(Time.time * shakeSpeed) * shakeAmount;
        float shakeY = Mathf.Cos(Time.time * shakeSpeed) * shakeAmount;

        HPRectTransform.localPosition = new Vector3(HPoriginalPos.x + shakeX, HPoriginalPos.y + shakeY, 0f);
        MPRectTransform.localPosition = new Vector3(MPoriginalPos.x + shakeX, MPoriginalPos.y + shakeY, 0f);
    }
    public static void ShowNotice(string message, float time = 2f)
    {
        if (Instance == null)
        {
            Debug.LogWarning("InGameUI Instance가 존재하지 않습니다.");
            return;
        }

        if (Instance.noticeCoroutine != null)
            Instance.StopCoroutine(Instance.noticeCoroutine);
        if (Instance.notice != null && Instance.noticeText != null)
        {
            Instance.noticeCoroutine = Instance.StartCoroutine(Instance.NoticeRoutine(message, time));
             GameAudioManager.PlayCommon("Show_Notice");
        }
    }
    public static IEnumerator ShowNoticeRoutine(string message, float time = 2f)
    {
        if (Instance == null)
        {
            Debug.LogWarning("InGameUI Instance가 존재하지 않습니다.");
            yield break;
        }

        if (Instance.noticeCoroutine != null)
            Instance.StopCoroutine(Instance.noticeCoroutine);

        if (Instance.notice == null || Instance.noticeText == null)
            yield break;

        GameAudioManager.PlayCommon("Show_Notice");
        yield return Instance.StartCoroutine(Instance.NoticeRoutine(message, time));
        Instance.noticeCoroutine = null;
    }
    public static void ShowWarning(string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning("InGameUI Instance가 존재하지 않습니다.");
            return;
        }

        if (Instance.warningCoroutine != null)
            Instance.StopCoroutine(Instance.warningCoroutine);

        Instance.warningCoroutine = Instance.StartCoroutine(Instance.WarningRoutine(message));
        GameAudioManager.PlayCommon("Show_Warning");
    }
    public static IEnumerator ShowDialogue(DialogueTxt[] txt)
    {
        if (Instance == null || txt == null || txt.Length == 0) yield break;

        // 실제 UI가 대사를 한 글자씩 출력하고 '클릭 대기'하는 코루틴이 끝날 때까지 기다림
        yield return Instance.StartCoroutine(Instance.PlayDialogue(txt));

        // 대화가 다 끝난 직후 실행될 로직
        if (PlayerInfo.Instance != null)
        {
            PlayerInfo.Instance.dialogueIndex++;
            PlayerInfo.Instance.Save();
        }
    }
    public void LevelUP()
    {
        levelUPText.text = "레벨 " +PlayerInfo.Instance.level;
        levelUPText.text = "레벨 " + PlayerInfo.Instance.level;
        if (levelUPUI == null)
            return;

        levelUPUI.SetActive(true);

        if (levelUpCanvasGroup != null)
            levelUpCanvasGroup.alpha = 1f;

        if (levelUpAnimator != null)
        {
            levelUpAnimator.Rebind();
            levelUpAnimator.Update(0f);
            levelUpAnimator.Play(0, 0, 0f);
            return;
        }

        levelUPUI.SetActive(false);
        levelUPUI.SetActive(true);
    }
    private void ResolveDialogueRuntimeReferences()
    {
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (uiManager == null)
            uiManager = FindFirstObjectByType<UIManager>();

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();
    }

    private IEnumerator PlayDialogue(DialogueTxt[] txt)
    {
        bool restoreMainBar = MainBar != null && MainBar.activeSelf;

        if (MainBar != null)
            MainBar.SetActive(false);

        ResolveDialogueRuntimeReferences();
        uiManager?.OpenDialogueUI();
        dialogue.SetActive(true);

        if (playerMovement != null)
            playerMovement.Initialize();

        if (playerController != null)
        {
            if (playerController.animator != null)
                playerController.animator.SetBool("Move", false);

            playerController.SetLockPlayer(true);
        }

        if (playerMovement != null)
        {
            playerMovement.SetMoveInput(Vector2.zero);
            playerMovement.SetStopMovement(true, true);
        }

        if (playerInput != null)
            playerInput.enabled = false;

        uiManager?.BeginModalPlayerLock(true);

        for (int i = 0; i < txt.Length; i++)
        {
            DialogueLinePresentation presentation = txt[i].ResolvePresentation();
            presentation.speakerName = TextSanitizer.Sanitize(presentation.speakerName);
            presentation.contents = TextSanitizer.Sanitize(presentation.contents);
            portrait.sprite = presentation.portrait;
            portrait.gameObject.SetActive(presentation.showPortrait);
            diaName.text = presentation.speakerName;
            diaName.gameObject.SetActive(presentation.showName);

            // 1. 타이핑 효과 시작 (Skip 기능을 위해 Coroutine 변수에 담음)
            Coroutine typingRoutine = StartCoroutine(TypeSentence(presentation.contents));

            bool skipTyping = false;
            float autoWait = Mathf.Clamp(presentation.contents.Length * 0.15f, 0.8f, 4.8f);
            float timer = 0f;
            bool next = false;

            // 2. 타이핑 도중 클릭하면 즉시 전체 출력
            while (diacontent.text != presentation.contents)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                {
                    StopCoroutine(typingRoutine);
                    diacontent.text = presentation.contents; // 즉시 전체 대사 표시
                    skipTyping = true;
                    break;
                }
                yield return null;
            }

            // 대사 출력 후 잠깐 대기 (실수 방지)
            if (!skipTyping) yield return YieldInstructionCache.GetWait(0.1f);
            else yield return YieldInstructionCache.GetWait(0.2f); // 스킵했을 땐 조금 더 대기

            // 3. 대사 완료 후 다음으로 넘어가기 대기
            while (!next)
            {
                timer += Time.deltaTime;
                if (timer >= autoWait) next = true;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                    next = true;

                yield return null;
            }
        }

        if (playerController != null)
            playerController.SetLockPlayer(false);

        if (playerInput != null)
            playerInput.enabled = true;

        if (playerMovement != null)
            playerMovement.SetStopMovement(false);

        dialogue.SetActive(false);
        uiManager?.CloseDialogueUI();
        uiManager?.EndModalPlayerLock(false);

        if (MainBar != null)
            MainBar.SetActive(restoreMainBar);
    }
    IEnumerator TypeSentence(string sentence)
    {
        sentence = TextSanitizer.Sanitize(sentence);
        diacontent.text = "";
        foreach (char letter in sentence.ToCharArray())
        {
            diacontent.text += letter;
            yield return YieldInstructionCache.GetWait(0.02f); // 타이핑 속도
        }
    }
    private IEnumerator NoticeRoutine(string message, float time)
    {

        noticeText.text = TextSanitizer.Sanitize(message);
        notice.SetActive(true);
        noticeCanvasGroup.alpha = 0f;

        // Fade In
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            noticeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        noticeCanvasGroup.alpha = 1f;

        // 유지 시간
        yield return new WaitForSecondsRealtime(time);

        // Fade Out
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            noticeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        noticeCanvasGroup.alpha = 0f;
        notice.SetActive(false);
    }
    private IEnumerator WarningRoutine(string message)
    {
        warningText.text = TextSanitizer.Sanitize(message);
        warning.SetActive(true);
        warningCanvasGroup.alpha = 0f;

        // Fade In
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            warningCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        warningCanvasGroup.alpha = 1f;

        // 유지 시간
        yield return new WaitForSecondsRealtime(1.2f);

        // Fade Out
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            warningCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        warningCanvasGroup.alpha = 0f;
        warning.SetActive(false);
    }

    Coroutine hitstop;
    [Header("Hit Stop Settings")]
    [SerializeField] private bool enableHitStopFeedback = true;
    [SerializeField] private float hitStopMinInterval = 0.12f;
    [SerializeField] private int maxHitStopRequestsPerFrame = 1;
    [SerializeField] private float minimumHitStopCameraStrength = 0.45f;
    private float lastHitStopTime = -10f;
    private int hitStopFrame = -1;
    private int hitStopRequestsThisFrame = 0;

    public bool TryHitStop(CombatFeedbackRequest request)
    {
        if (!enableHitStopFeedback || request == null || !request.enableHitStop)
            return false;

        if (request.isMultiHit && !request.allowMultiHitHitStop)
            return false;

        if (request.cameraStrength < minimumHitStopCameraStrength)
            return false;

        if (Time.frameCount != hitStopFrame)
        {
            hitStopFrame = Time.frameCount;
            hitStopRequestsThisFrame = 0;
        }

        hitStopRequestsThisFrame++;
        if (hitStopRequestsThisFrame > maxHitStopRequestsPerFrame)
            return false;

        if (Time.unscaledTime - lastHitStopTime < hitStopMinInterval)
            return false;

        if (hitstop != null)
            return false;

        lastHitStopTime = Time.unscaledTime;
        hitstop = StartCoroutine(HitStopCor(request.hitStopDuration, 0.1f, request.hitStopSlowScale));
        return true;
    }

    public void HitStop()
    {
        TryHitStop(new CombatFeedbackRequest
        {
            enableHitStop = true,
            feedbackLevel = HitFeedbackLevel.Critical,
            cameraStrength = 0.55f,
            hitStopDuration = 0.012f,
            hitStopSlowScale = 0.2f
        });
    }
    private IEnumerator HitStopCor(float stopTime, float slowTime, float slowScale = 0.2f)
    {
        Time.timeScale = 0.05f; // 아주 느리게 시작
        yield return new WaitForSecondsRealtime(stopTime);

        // 서서히 1f로 복구 (0.1초 동안)
        float t = 0;
        float recoveryTime = 0.1f;
        while (t < recoveryTime)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(slowScale, 1f, t / recoveryTime);
            yield return null;
        }

        Time.timeScale = 1f;
        hitstop = null;
    }
}
