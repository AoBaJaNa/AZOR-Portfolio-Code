using System.Collections;
using UnityEngine.AddressableAssets;
using System.Threading.Tasks;


#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public enum SkillType
{
    Slash,
    SlashBurst,
    SwordCrash,
    AttackBuff,
    ShieldBuff,
    Stardust,
    SwordRain,
    SlashCombo,
    DoubleAttack,
    DashSlash,
    BackstepSpin,
    BlinkStrike,
    CounterDash,
    JumpLanding,
    CrossSlash,
    PierceSlash,
    RoundSlash,
    CounterReflection,
    StrikeAssault,
    AdditionalHitBuff,
    ShadowRecall,
    WaveStun,
    ImpulseStun,
    DashSlash_Double,
    BackstepSpin_Double,
    BlinkStrike_Double,
    CounterDash_Double,
    JumpLanding_Double,
    CrossSlash_Double,
    PierceSlash_Double,
    RoundSlash_Double,
    ImpulseStun_Double,
    SwordCrash_Double
}
public enum HitType
{
    Single_Hit,
    Multi_Hit
}
public enum SkillCategory
{
    Moveset,
    Short_Dealing,
    Buff,
    Stun
}
public enum SkillColor
{
    Yellow,
    Blue,
    Red
}

//[CreateAssetMenu(fileName = "NewSkillData", menuName = "Skill/Skill Data", order = 0)]

public abstract class SkillData : ScriptableObject
{
    [Header("Basic Info")]
    public SkillType skillType;
    public SkillCategory skillCategory;
    public HitType hitType;
    public SkillColor color;
    public EnemyHitEffectType hitEffectType = EnemyHitEffectType.Tech_Skill;
    public int learnCost;
    public int unlockLevel;
    public AssetReferenceGameObject skillPrefab_address;
    public AssetReferenceGameObject activeSkilIPrefab_address;
    public AssetReferenceSprite skillIcon_address;
    public SoundAsset SoundAsset;
    public GameObject skilIPrefab;
    internal GameObject activeSkilIPrefab;
    internal Sprite skillIcon;

    internal float lastUsedTime = -999f;
    internal bool isActive = false;
    internal bool isKnockback = false;
    internal float knokbackForce = 0f;
    internal bool cameraShakeMode = false;
    internal float cameraShakeDuration = 0f;
    internal float cameraShakeForce = 0f;
    [Header("Hit Feedback")]
    public SkillHitFeedbackSettings hitFeedback = new SkillHitFeedbackSettings();

    [Header("General Settings")]
    public int staminaCost = 10;
    [Tooltip("퍼센트 데미지 (예: 20% = 0.2f)")]
    [Range(0, 10)]
    public float AttackDamage = 1f;
    public float cooldown = 5f;

    public abstract IEnumerator SkillLogic(PlayerController playerController);
    public virtual int GetSkillDamaged()
    {
        int damage = Mathf.RoundToInt(PlayerInfo.Instance.FinalAttackDamage * AttackDamage);
        return Mathf.Max(1,damage);
    }
    public virtual CombatFeedbackRequest CreateFeedbackRequest(bool isCritical, bool addHit = false)
    {
        if (hitFeedback != null && hitFeedback.overrideLegacyValues)
        {
            CombatFeedbackRequest overrideRequest = new CombatFeedbackRequest
            {
                cameraMode = hitFeedback.cameraMode,
                reactionTier = hitFeedback.reactionTier,
                feedbackLevel = isCritical ? HitFeedbackLevel.Critical : hitFeedback.feedbackLevel,
                cameraDuration = hitFeedback.cameraDuration,
                cameraStrength = hitFeedback.cameraStrength,
                reactionForce = hitFeedback.reactionForce,
                enableHitStop = hitFeedback.enableHitStop,
                hitStopDuration = hitFeedback.hitStopDuration,
                hitStopSlowScale = hitFeedback.hitStopSlowScale,
                allowMultiHitHitStop = hitFeedback.allowMultiHitHitStop,
                enableScalePunch = hitFeedback.enableScalePunch,
                scalePunchStrength = hitFeedback.scalePunchStrength,
                scalePunchDuration = hitFeedback.scalePunchDuration,
                isCritical = isCritical,
                isMultiHit = hitType == HitType.Multi_Hit
            };

            if (addHit)
            {
                overrideRequest.enableHitStop = false;
                overrideRequest.allowMultiHitHitStop = false;
                overrideRequest.scalePunchStrength *= 0.7f;
                overrideRequest.cameraStrength *= 0.7f;
            }

            return overrideRequest;
        }

        CombatFeedbackRequest request = new CombatFeedbackRequest
        {
            cameraMode = cameraShakeForce <= 0f && cameraShakeDuration <= 0f
                ? (isCritical ? CameraFeedbackMode.Impulse : CameraFeedbackMode.None)
                : (cameraShakeMode ? CameraFeedbackMode.Shake : CameraFeedbackMode.Impulse),
            reactionTier = GetLegacyReactionTier(isCritical),
            feedbackLevel = GetLegacyFeedbackLevel(isCritical),
            cameraDuration = Mathf.Max(cameraShakeDuration, isCritical ? 0.12f : 0.08f),
            cameraStrength = Mathf.Max(cameraShakeForce, isCritical ? 0.22f : 0.1f),
            reactionForce = GetLegacyReactionForce(isCritical),
            enableHitStop = ShouldEnableLegacyHitStop(isCritical, addHit),
            hitStopDuration = GetLegacyHitStopDuration(isCritical),
            hitStopSlowScale = 0.2f,
            allowMultiHitHitStop = false,
            enableScalePunch = true,
            scalePunchStrength = GetLegacyScalePunchStrength(isCritical, addHit),
            scalePunchDuration = 0.08f,
            isCritical = isCritical,
            isMultiHit = hitType == HitType.Multi_Hit
        };

        if (addHit)
        {
            request.cameraStrength *= 0.75f;
            request.enableHitStop = false;
        }

        return request;
    }
    public virtual void OnEnable()
    {
        lastUsedTime = -999f;
        isActive = false;
        skilIPrefab = null;
        activeSkilIPrefab = null;
        skillIcon = null;
        PreloadIconData();
    }

    private HitReactionTier GetLegacyReactionTier(bool isCritical)
    {
        if (isKnockback)
        {
            if (knokbackForce >= 2.7f)
                return HitReactionTier.HeavyKnockback;

            return HitReactionTier.PushBack;
        }

        return HitReactionTier.LightStagger;
    }

    private HitFeedbackLevel GetLegacyFeedbackLevel(bool isCritical)
    {
        if (isCritical)
            return HitFeedbackLevel.Critical;
        if (cameraShakeForce >= 0.55f || knokbackForce >= 2.7f)
            return HitFeedbackLevel.Heavy;
        if (cameraShakeForce >= 0.25f || isKnockback)
            return HitFeedbackLevel.Medium;

        return HitFeedbackLevel.Light;
    }

    private float GetLegacyReactionForce(bool isCritical)
    {
        if (isKnockback)
            return Mathf.Max(0.8f, knokbackForce);

        return isCritical ? 0.5f : 0.35f;
    }

    private bool ShouldEnableLegacyHitStop(bool isCritical, bool addHit)
    {
        if (addHit || hitType == HitType.Multi_Hit)
            return false;

        return isCritical || cameraShakeForce >= 0.65f || knokbackForce >= 2.7f;
    }

    private float GetLegacyHitStopDuration(bool isCritical)
    {
        if (cameraShakeForce >= 0.8f)
            return 0.018f;
        if (isCritical || cameraShakeForce >= 0.5f)
            return 0.014f;

        return 0.012f;
    }

    private float GetLegacyScalePunchStrength(bool isCritical, bool addHit)
    {
        if (addHit)
            return 0.025f;
        if (knokbackForce >= 2.7f || cameraShakeForce >= 0.8f)
            return 0.075f;
        if (isKnockback || isCritical || cameraShakeForce >= 0.3f)
            return 0.05f;

        return 0.035f;
    }

    public async Task PreloadPrefabData()
    {
        if (skillPrefab_address == null || string.IsNullOrEmpty(skillPrefab_address.AssetGUID) || !skillPrefab_address.RuntimeKeyIsValid())
            return;

        if (skilIPrefab != null)
            return;

        try
        {
            var prefabHandle = Addressables.LoadAssetAsync<GameObject>(skillPrefab_address);
            skilIPrefab = await prefabHandle.Task;

            if(SoundAsset !=null)
            SoundAsset.PreloadSound();

            if (activeSkilIPrefab_address != null &&
                !string.IsNullOrEmpty(activeSkilIPrefab_address.AssetGUID) &&
                activeSkilIPrefab_address.RuntimeKeyIsValid())
            {
                var activeHandle = Addressables.LoadAssetAsync<GameObject>(activeSkilIPrefab_address);
                activeSkilIPrefab = await activeHandle.Task;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"SkillData: failed to preload prefab for '{name}'. {e.Message}");
        }
    }
    public void PreloadIconData()
    {
        if (skillIcon_address == null || skillIcon != null || string.IsNullOrEmpty(skillIcon_address.AssetGUID) || !skillIcon_address.RuntimeKeyIsValid())
            return;

        if (GameSession.Exists && GameSession.Instance.TryGetSkillIcon(skillIcon_address, out Sprite cachedIcon))
        {
            skillIcon = cachedIcon;
        }
    }
    public void ReleasePrefabData()
    {
        if (SoundAsset != null)
        {
            SoundAsset.ReleaseSound();
        }

        if (skillPrefab_address != null && !string.IsNullOrEmpty(skillPrefab_address.AssetGUID))
        {
            skillPrefab_address.ReleaseAsset();
        }

        if (activeSkilIPrefab_address != null && !string.IsNullOrEmpty(activeSkilIPrefab_address.AssetGUID))
        {
            activeSkilIPrefab_address.ReleaseAsset();
        }

        skilIPrefab = null;
        activeSkilIPrefab = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        string targetName = skillType.ToString();

        if (name != targetName)
        {
            // OnValidate 중 직접 이름을 변경하지 않고 에디터 대기열에서 처리한다.
            UnityEditor.EditorApplication.delayCall -= SafeRenameAsset;
            UnityEditor.EditorApplication.delayCall += SafeRenameAsset;
        }
    }

    private void SafeRenameAsset()
    {
        UnityEditor.EditorApplication.delayCall -= SafeRenameAsset;

        if (this == null) return;

        string targetName = skillType.ToString();
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);

        if (!string.IsNullOrEmpty(assetPath) && name != targetName)
        {
            UnityEditor.AssetDatabase.RenameAsset(assetPath, targetName);
            UnityEditor.AssetDatabase.SaveAssets();

            UnityEditor.AssetDatabase.Refresh();
        }
    }
#endif
}



