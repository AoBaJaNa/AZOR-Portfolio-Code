using System;
using UnityEngine;

public enum CameraFeedbackMode
{
    None,
    Impulse,
    Shake
}

public enum HitReactionTier
{
    None,
    LightStagger,
    PushBack,
    HeavyKnockback
}

public enum HitFeedbackLevel
{
    Light = 0,
    Medium = 1,
    Heavy = 2,
    Critical = 3
}

[Serializable]
public class SkillHitFeedbackSettings
{
    public bool overrideLegacyValues = false;
    public CameraFeedbackMode cameraMode = CameraFeedbackMode.Impulse;
    public HitReactionTier reactionTier = HitReactionTier.None;
    public HitFeedbackLevel feedbackLevel = HitFeedbackLevel.Medium;
    public float cameraDuration = 0.12f;
    public float cameraStrength = 0.2f;
    public float reactionForce = 1f;
    public bool enableHitStop = false;
    public float hitStopDuration = 0.012f;
    public float hitStopSlowScale = 0.2f;
    public bool allowMultiHitHitStop = false;
    public bool enableScalePunch = true;
    public float scalePunchStrength = 0.04f;
    public float scalePunchDuration = 0.08f;
}

[Serializable]
public class CombatFeedbackRequest
{
    public CameraFeedbackMode cameraMode = CameraFeedbackMode.None;
    public HitReactionTier reactionTier = HitReactionTier.None;
    public HitFeedbackLevel feedbackLevel = HitFeedbackLevel.Light;
    public float cameraDuration = 0.1f;
    public float cameraStrength = 0.1f;
    public float reactionForce = 1f;
    public bool enableHitStop;
    public float hitStopDuration = 0.012f;
    public float hitStopSlowScale = 0.2f;
    public bool allowMultiHitHitStop;
    public bool enableScalePunch = true;
    public float scalePunchStrength = 0.035f;
    public float scalePunchDuration = 0.08f;
    public bool isCritical;
    public bool isMultiHit;

    public int Priority => (int)feedbackLevel;

    public CombatFeedbackRequest Clone()
    {
        return (CombatFeedbackRequest)MemberwiseClone();
    }

    public static CombatFeedbackRequest CreateBasicHit(bool isCritical, bool isMultiHit = false)
    {
        return new CombatFeedbackRequest
        {
            cameraMode = CameraFeedbackMode.Impulse,
            reactionTier = isCritical ? HitReactionTier.PushBack : HitReactionTier.LightStagger,
            feedbackLevel = isCritical ? HitFeedbackLevel.Critical : HitFeedbackLevel.Light,
            cameraDuration = isCritical ? 0.12f : 0.08f,
            cameraStrength = isCritical ? 0.24f : 0.08f,
            reactionForce = isCritical ? 1.1f : 0.45f,
            enableHitStop = isCritical,
            hitStopDuration = 0.012f,
            hitStopSlowScale = 0.2f,
            allowMultiHitHitStop = false,
            enableScalePunch = true,
            scalePunchStrength = isCritical ? 0.06f : 0.03f,
            scalePunchDuration = 0.075f,
            isCritical = isCritical,
            isMultiHit = isMultiHit
        };
    }
}

