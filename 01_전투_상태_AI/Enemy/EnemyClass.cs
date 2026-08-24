using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public enum EnemyType
{
    Ghoul,
    GhostDog,
    SkullWarrior,
    Nun,
    GhostSkull,
    Pagan,
    Ghoul_Elite,
    GhostDog_Elite,
    SkullWarrior_Elite,
    Nun_Elite,
    GhostSkull_Elite,
    Pagan_Elite,
    Boss
}
public struct EnemyStats
{
    public int HP;
    public int Damage;
    public int Defence;
    public float DetectRange;

    public EnemyStats(int hp, int damage, int defence, float detectRange = 10)
    {
        HP = hp;
        Damage = damage;
        Defence = defence;
        DetectRange = detectRange;
    }
}
public class EnemyClass : MonoBehaviour
{
    public StateMachine<EnemyBaseState> StateMachine { get; protected set; }
    public EnemyIdleState IdleState { get; protected set; }
    public EnemyStunState StunState { get; protected set; }
    public EnemyDeathState DeathState { get; protected set; }
    public EnemyAttackState AttackState { get; protected set; }

    [Header("이름 설정")]
    public EnemyType enemytype;
    public string enemyName;

    [Header("체력 설정")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int Defense { get; protected set; } = 10;

    [Header("UI 설정")]
    public GameObject hpBarPrefab;
    protected Transform uiParent;
    public float yOffset = 2f;

    [Header("경험치 설정")]
    public int expReward = 50;

    [Header("데미지 팝업")]
    public GameObject damagePopupPrefab;
    public Transform damagePopupSpawnPoint;
    protected int activeDamagePopupCount = 0;
    public virtual bool IsBoss => false;
    protected virtual bool UsesGenericEnemyInitialization => true;
    public bool IsPaganBuff{get; private set;} = false;
    public TMP_Text nameTxt;
    protected Coroutine showNameCoroutine;
    protected Canvas hpBarCanvas;
    protected RectTransform hpBarRectTransform;
    protected RectTransform hpBarCanvasRect;
    protected PassiveSkillManager passiveSkillManager;
    protected EnemyStats enemyStats;
    internal GameObject hpBarInstance;
    internal Slider hpSlider;
    internal PlayerCamera playerCamera;
    internal PlayerController playerController;
    internal EnemyAttack enemyAttack;
    internal EnemyController enemyController;
    internal EnemyDead enemyDead;
    internal EnemyDropItem enemyDropItem;
    internal EnemyVFX enemyVFX;
    internal EnemySoundManager enemySoundManager;
    internal EnemyPassiveController enemyPassiveController;
    internal EnemyStatusController enemyStatusController;
    internal EnemyStatusBarView enemyStatusBarView;
    SkillData buffData;
    AdditionalHitBuff additionalHitBuffData;
    private Dictionary<string, float> damagedModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> defenseModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> HPModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> attackModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> speedModifiers = new Dictionary<string, float>();
    private bool isPooling = false;
    private bool pendingCameraImpulse = false;
    private float pendingCameraImpulseTime = 0f;
    private float pendingCameraImpulseForce = 0f;
    private int hpBarUpdateFrameOffset = 0;

    private void Awake()
    {
        playerController = FindFirstObjectByType<PlayerController>();
        enemyController = GetComponent<EnemyController>();
        passiveSkillManager = FindFirstObjectByType<PassiveSkillManager>();
        enemyPassiveController = GetComponent<EnemyPassiveController>();
        playerCamera = FindFirstObjectByType<PlayerCamera>();
        enemyDead = GetComponent<EnemyDead>();
        enemyVFX = GetComponent<EnemyVFX>();
        enemyAttack = GetComponent<EnemyAttack>();
        enemyStatusController = GetComponent<EnemyStatusController>();
        if (enemyStatusController == null)
            enemyStatusController = gameObject.AddComponent<EnemyStatusController>();
        hpBarUpdateFrameOffset = Mathf.Abs(GetInstanceID()) % 2;

        
        StateMachine = new StateMachine<EnemyBaseState>();
        DeathState = new EnemyDeathState(this, StateMachine);
        IdleState = new EnemyIdleState(this, StateMachine);
        StunState = new EnemyStunState(this, StateMachine);
        AttackState = new EnemyAttackState(this, StateMachine);
        StateMachine.Initialize(IdleState);
    }
    private void Start()
    {
        if (uiParent == null)
        {
            GameObject container = GameObject.Find("EnemyHPBar");
            if (container != null)
                uiParent = container.transform;
            else
                Debug.LogError("EnemyHPBar 오브젝트를 찾을 수 없습니다.");
        }

        CreateHPBar();
        buffData = SkillManager.instance.GetSkillData(SkillType.AdditionalHitBuff);
        additionalHitBuffData = buffData as AdditionalHitBuff;
    }
    public virtual void StatSetting(int hp, int damage, int defence, float range)
    {
        enemyStats.HP = hp;
        enemyStats.Damage = damage;
        enemyStats.Defence = defence;
        enemyStats.DetectRange = range;
        if (gameObject.activeInHierarchy)
            Initialize();
    }
    protected void Initialize()
    {
        maxHP = enemyStats.HP;
        currentHP = enemyStats.HP;
        if (enemyAttack != null)
            enemyAttack.attackDamage = enemyStats.Damage;
        else
            Debug.LogError($"EnemyClass: {name} is missing EnemyAttack required for generic enemy initialization.", this);
        Defense = enemyStats.Defence;

        if (isPooling)
            enemyDead.HandleSetup();

        if (enemyAttack != null)
            enemyAttack.AttackReset();

        if (enemyController != null)
        {
            enemyController.ResetControllerStateForReuse();
            enemyController.ChangeDetectRange(enemyStats.DetectRange);
            enemyController.StickToNavMesh();
            enemyController.StopMovement();
        }

        StateMachine.Initialize(IdleState);
        UpdateHPBar();
    }
    protected void OnEnable()
    {
        if (!UsesGenericEnemyInitialization)
            return;

        CreateHPBar();
        Initialize();

    }
    protected void OnDisable()
    {
        DestroyHPBar();
    }
    private void OnDestroy()
    {
        DestroyHPBar();
    }
    private void Update()
    {
        if (!UsesGenericEnemyInitialization)
            return;

        StateMachine.CurrentState.Update();

        if (pendingCameraImpulse && Time.time >= pendingCameraImpulseTime)
        {
            pendingCameraImpulse = false;
            playerCamera.PlayHitFeedback(new CombatFeedbackRequest
            {
                cameraMode = CameraFeedbackMode.Impulse,
                feedbackLevel = pendingCameraImpulseForce >= 0.2f ? HitFeedbackLevel.Medium : HitFeedbackLevel.Light,
                cameraDuration = 0.2f,
                cameraStrength = pendingCameraImpulseForce
            });
        }
    }
    private void LateUpdate()
    {
        if (((Time.frameCount + hpBarUpdateFrameOffset) & 1) != 0)
            return;

        UpdateHPBarPosition();
    }

    #region Damage Logic
    public virtual void Damaged(int damage, bool critical)
    {
        PlayerInfo.Instance.EnterCombat();
        PlayerInfo.Instance.GainStaminaFromAttack();
        enemyVFX.HitParticle(EnemyHitEffectType.Slash);

        TakeDamage(damage, critical, SkillManager.instance.GetSkillData(SkillType.Slash).color);
        ApplyImpactFeedback(BuildBasicAttackFeedback(critical));
    }
    public void SkillDamaged(SkillType type)
    {
        SkillData data = SkillManager.instance.GetSkillData(type);

        switch (type)
        {
            case SkillType.CounterReflection:
                {
                    bool critical = PlayerInfo.Instance.GetFinalCriticalChance();
                    StartCoroutine(DelayDamaged(data, critical, 0.4f));
                    enemyVFX.HitParticle(EnemyHitEffectType.Magic_Skill);
                    PlayerInfo.Instance.GainStaminaFromAttack();
                    break;
                }

            case SkillType.AdditionalHitBuff:
                {
                    bool critical = PlayerInfo.Instance.GetFinalCriticalChance();
                    StartCoroutine(AdditionalHit(data.GetSkillDamaged(), critical));
                    break;
                }

            default:
                DamagedLogic(data);
                break;
        }
    }
    IEnumerator DelayDamaged(SkillData data, bool critical, float DelayTime, bool addHit = false)
    {
        yield return new WaitForSeconds(DelayTime);
        if (data == null)
            yield break;

        TakeDamage(data.GetSkillDamaged(), critical, data.color, addHit);
        ApplyImpactFeedback(BuildSkillFeedback(data, critical, addHit));
    }
    public virtual void DamagedLogic(SkillData data)
    {
        bool critical = PlayerInfo.Instance.GetFinalCriticalChance();
        TakeDamage(data.GetSkillDamaged(), critical, data.color);
        enemyVFX.HitParticle(data.hitEffectType);
        PlayerInfo.Instance.GainStaminaFromAttack();
        ApplyImpactFeedback(BuildSkillFeedback(data, critical));

        playerController.playerCombat.AddDamagedEnemyList(this);
        if (playerController.playerPassiveController.HasPassiveBuildTypes.Contains(PassiveSkillBuildType.Stigma))
            playerController.playerPassiveController.HandleStigmaAttack(this);
    }
    public virtual void TakeDamage(int damage, bool isCritical, SkillColor color = SkillColor.Red, bool addHit = false)
    {
        StateMachine.CurrentState.OnDamage(damage,isCritical,color,addHit);
    }
    public void TakeDamageLogic(int damage, bool isCritical, SkillColor color = SkillColor.Red, bool addHit = false)
    {
        if (buffData != null && buffData.isActive && addHit == false)
        {
            SkillDamaged(SkillType.AdditionalHitBuff);
        }

        int finaldamage = Mathf.RoundToInt(damage - (Defense * Random.Range(0.94f, 1f)));

        if (enemyController.isStunned)
        {
            finaldamage = Mathf.RoundToInt(finaldamage * (1f + enemyPassiveController.TotalStunAddDealMultiplier));
        }

        int appliedDamage = GetFinalDamaged(finaldamage);
        currentHP -= appliedDamage;
        ShowDamagePopup(appliedDamage, isCritical);
        GlobalEnemySoundManager.Instance.PlaySound(enemytype, EnemySoundType.Hit);
        enemyDead.HandleHit(color);
        UpdateHPBar();

        if (currentHP <= 0)
        {
                StateMachine.ChangeState(DeathState);
        }
    }
    private IEnumerator AdditionalHit(int finalDamage, bool critical)
    {
        if (additionalHitBuffData == null)
            yield break;

        yield return YieldInstructionCache.GetWait(0.5f);
        for (int i = 0; i < additionalHitBuffData.additionalHitBuffCount; i++)
        {
            TakeDamage(finalDamage, critical, additionalHitBuffData.color, true);
            ApplyImpactFeedback(BuildSkillFeedback(additionalHitBuffData, critical, true));
            enemyVFX.HitParticle(EnemyHitEffectType.AdditionalHit);

            yield return YieldInstructionCache.GetWait(0.1f);
        }
    }

    #endregion
    #region Crowd Control
    public virtual void Stun(float duration)
    {
        StateMachine.CurrentState.OnStunInput(duration);
    }
    public virtual void Aggro(Transform target, float duration)
    {
        enemyController.SetAggro(target, duration);
    }

    public virtual void ClearAggro(Transform target)
    {
        enemyController.ClearAggro(target);
    }

    #endregion
    #region UIControl
    protected void CreateHPBar()
    {
        if (hpBarInstance != null)
            DestroyHPBar();

        if (hpBarPrefab != null && uiParent != null)
        {
            hpBarInstance = Instantiate(hpBarPrefab, uiParent);
            hpSlider = hpBarInstance.GetComponentInChildren<Slider>();
            nameTxt = hpBarInstance.GetComponentInChildren<TMP_Text>();
            hpBarRectTransform = hpBarInstance.transform as RectTransform;
            hpBarCanvas = hpBarInstance.GetComponentInParent<Canvas>();
            hpBarCanvasRect = hpBarCanvas != null ? hpBarCanvas.transform as RectTransform : null;
            if (hpSlider != null)
            {
                hpSlider.maxValue = 1;
                hpSlider.value = (float)currentHP / maxHP;
                nameTxt.text = enemyName;
                nameTxt.gameObject.SetActive(false);
            }

            enemyStatusBarView = hpBarInstance.GetComponent<EnemyStatusBarView>();
            if (enemyStatusBarView == null)
                enemyStatusBarView = hpBarInstance.AddComponent<EnemyStatusBarView>();

            enemyStatusBarView.Initialize();
            enemyStatusController?.BindView(enemyStatusBarView);
            UpdateHPBarPosition();
        }
    }
    protected void DestroyHPBar()
    {

        if (hpBarInstance != null)
        {
            Destroy(hpBarInstance);
        }

        enemyStatusBarView = null;
        enemyStatusController?.UnbindView();
    }
    protected void UpdateHPBarPosition()
    {
        if (hpBarRectTransform != null && Camera.main != null)
        {
            Vector3 worldPosition = transform.position + Vector3.up * yOffset;
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

            if (hpBarCanvas == null)
                return;

            if (hpBarCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                hpBarInstance.transform.position = screenPosition;
            }
            else if (hpBarCanvas.renderMode == RenderMode.ScreenSpaceCamera && hpBarCanvasRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    hpBarCanvasRect,
                    screenPosition,
                    hpBarCanvas.worldCamera,
                    out Vector2 localPoint
                );
                hpBarRectTransform.anchoredPosition = localPoint;

            }
        }
    }
    protected void UpdateHPBar()
    {
        if (hpSlider != null)
        {
            hpSlider.value = (float)currentHP / maxHP;
        }
    }
    public virtual void ShowName()
    {
        if (hpBarInstance == null) return;

        nameTxt.gameObject.SetActive(true);

        if (showNameCoroutine != null)
            StopCoroutine(showNameCoroutine);

        showNameCoroutine = StartCoroutine(HideNameAfterDelay());
    }
    private IEnumerator HideNameAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        if (nameTxt != null && nameTxt.gameObject != null)
        {
            nameTxt.gameObject.SetActive(false);
        }
        showNameCoroutine = null;
    }
    protected void QueueCameraImpulse(bool isCritical)
    {
        float force = isCritical ? 0.25f : 0.09f;

        if (!pendingCameraImpulse)
        {
            pendingCameraImpulse = true;
            pendingCameraImpulseTime = Time.time + 0.1f;
            pendingCameraImpulseForce = force;
            return;
        }

        pendingCameraImpulseForce = Mathf.Max(pendingCameraImpulseForce, force);
        pendingCameraImpulseTime = Mathf.Min(pendingCameraImpulseTime, Time.time + 0.1f);
    }
    public virtual void OnDamagePopupEnd()
    {
        activeDamagePopupCount = Mathf.Max(0, activeDamagePopupCount - 1);
    }

    protected CombatFeedbackRequest BuildBasicAttackFeedback(bool isCritical)
    {
        return CombatFeedbackRequest.CreateBasicHit(isCritical);
    }

    protected CombatFeedbackRequest BuildSkillFeedback(SkillData data, bool isCritical, bool addHit = false)
    {
        if (data == null)
            return BuildBasicAttackFeedback(isCritical);

        return data.CreateFeedbackRequest(isCritical, addHit);
    }

    protected void ApplyImpactFeedback(CombatFeedbackRequest request)
    {
        if (request == null)
            return;

        if (enemyController != null && request.reactionTier != HitReactionTier.None && playerController != null)
            enemyController.ApplyHitReaction(playerController.transform.position, request);

        if (playerCamera != null)
            playerCamera.PlayHitFeedback(request);

        if (InGameUI.Instance != null)
            InGameUI.Instance.TryHitStop(request);
    }

    protected void ShowDamagePopup(int damage, bool critical)
    {
        if (hpBarInstance == null) return;

        Canvas canvas = hpBarInstance.GetComponentInParent<Canvas>();
        if (damagePopupPrefab == null || canvas == null)
            return;

        GameObject popupInstance = Instantiate(damagePopupPrefab, uiParent);
        float yOffset = activeDamagePopupCount * 35f;
        Vector2 offset = new Vector2(Random.Range(-10f, 10f), yOffset);

        DamagePopup3D popup = popupInstance.GetComponent<DamagePopup3D>();
        if (popup != null)
        {
            popup.target = damagePopupSpawnPoint != null ? damagePopupSpawnPoint : transform;
            popup.owner = this;
            popup.SetDamage(damage, critical);
            popup.SetStackOffset(offset);
        }

        activeDamagePopupCount++;
    }
    #endregion
    #region Status Change
    public void PaganBuff(bool value, float multiplier)
    {
        if (value == IsPaganBuff) return;
        IsPaganBuff = value;

        if (IsPaganBuff)
        {
            maxHP = Mathf.RoundToInt(enemyStats.HP * multiplier);
            currentHP = Mathf.RoundToInt(currentHP * multiplier);
            enemyAttack.attackDamage = Mathf.CeilToInt(enemyStats.Damage * multiplier);

        }
        else
        {
            maxHP = enemyStats.HP;
            enemyAttack.attackDamage = enemyStats.Damage;

            currentHP = Mathf.RoundToInt(currentHP / multiplier);

            if (currentHP > maxHP) currentHP = maxHP;
        }
    }
    public void HealHP(float amount)
    {
        //enemyVFX.HitParticle(EnemyVFX.EnemyHitEffectType.Heal);
        currentHP += Mathf.RoundToInt(maxHP * amount);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateHPBar();
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
    protected int GetFinalDamaged(float damage)
    {
        float finalMultiplier = 1f;

        foreach (var kvp in damagedModifiers)
        {
            string key = kvp.Key;
            float amount = kvp.Value;

            if (key.Contains("Buff"))
            {
                finalMultiplier *= (1f - amount);
            }
            else
            {
                finalMultiplier *= (1f + amount);
            }
        }

        return Mathf.Max(Mathf.RoundToInt(finalMultiplier * damage),1);

    }
    public void SetDefenceModifier(string key, float amount)
    {
        if (amount <= 0f)
        {
            if (defenseModifiers.ContainsKey(key))
                defenseModifiers.Remove(key);
        }
        else
        {
            defenseModifiers[key] = amount;
        }
        GetDefenceModifier();
    }
    private void GetDefenceModifier()
    {
        float finalMultiplier = 1f;

        foreach (var kvp in defenseModifiers)
        {
            string key = kvp.Key;
            float amount = kvp.Value;

            if (key.Contains("Buff"))
            {
                finalMultiplier *= (1f + amount);
            }
            else
            {
                finalMultiplier *= (1f - amount);
            }
        }

        int originalDefence = enemyStats.Defence;
        Defense = Mathf.Max(0, Mathf.RoundToInt(originalDefence * finalMultiplier));

    }
    public void SetSpeedModifier(string key, float amount)
    {
        if (amount <= 0f)
        {
            if (speedModifiers.ContainsKey(key))
                speedModifiers.Remove(key);
        }
        else
        {
            speedModifiers[key] = amount;
        }
        GetSpeedModifier();
    }
    private void GetSpeedModifier()
    {
        float finalMultiplier = 1f;

        foreach (var kvp in speedModifiers)
        {
            string key = kvp.Key;
            float amount = kvp.Value;

            if (key.Contains("Buff"))
            {
                finalMultiplier *= (1f + amount);
            }
            else
            {
                finalMultiplier *= (1f - amount);
            }
        }

        enemyController.SetMoveSpeedMultiplier(finalMultiplier);
    }

    #endregion
    public void Die()
    {
        currentHP = 0;
        enemyDead.HandleDeath();
        isPooling = true;
        DestroyHPBar();
    }
    public int GetExpReward()
    {
        return expReward;
    }

    public void SetExpReward(int value)
    {
        expReward = Mathf.Max(0, value);
    }
    
    public void CheckCurrentState() {
       //Debug.Log(StateMachine.CurrentState.ToString());
    }
    public virtual void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out SkillData_Colider skillCol))
        {
            SkillData data = skillCol.GetCachedSkillData();
            if (data != null && skillCol.TryRegisterHit(this))
            {
                DamagedLogic(data);
            }

            return;
        }

        /*        if (other.CompareTag("SummonHit"))
                {
                    var summon = other.GetComponentInParent<Summon>();
                    var col = other.GetComponentInParent<SummonAttack>();
                    if (summon != null)
                    {
                        bool critical = summon.GetFinalCriticalChance();
                        int finalDamage = critical ? Mathf.RoundToInt(summon.attackDamage * 1.5f) : summon.attackDamage;
                        finalDamage = Mathf.Max(1, finalDamage);
                        col.AttackColliderOFF();
                        TakeDamage(finalDamage, critical);
                    }
                    enemyVFX.HitParticle(EnemyVFX.EnemyHitEffectType.Slash);
                    PlayerInfo.Instance.GainStaminaFromAttack();

                }*/

        if (other.CompareTag("BlockWall") && enemyController != null)
        {
            enemyController.ResetPos();
        }
    }

}

