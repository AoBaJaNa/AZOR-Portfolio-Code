using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AdditionalHitBuff", menuName = "Skill/AdditionalHitBuff")]
public class AdditionalHitBuff : SkillData
{
    [Header("추가타 버프 설정")]
    public float additionalHitBuffDuration = 15f;
    public int additionalHitBuffCount = 3;
    public override void OnEnable()
    {
        base.OnEnable();
        skillType = SkillType.AdditionalHitBuff;
        skillCategory = SkillCategory.Buff;
        hitType = HitType.Single_Hit;
        color = SkillColor.Blue;
        hitEffectType = EnemyHitEffectType.Tech_Skill;
        cameraShakeMode = false;
        cameraShakeForce = 0f;
        cameraShakeDuration = 0f;
}

    public override IEnumerator SkillLogic(PlayerController player)
    {
        player.playerSkill.StartCoroutine(player.playerSkill.ISSkill(YieldInstructionCache.GetWait(0f)));

        isActive = true;
        PlayerSoundManager.PlaySound("AdditionalHitBuff");

        InGameUI.Instance.BuffSkillON(this, additionalHitBuffDuration);
        yield return YieldInstructionCache.GetWait(additionalHitBuffDuration);
        isActive = false;
    }
}

