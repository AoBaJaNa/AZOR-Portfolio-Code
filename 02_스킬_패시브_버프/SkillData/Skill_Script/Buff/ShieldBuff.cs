using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "ShieldBuff", menuName = "Skill/ShieldBuff")]
public class ShieldBuff : SkillData
{
    [Header("쉴드 버프 설정")]
    public float shieldBuffDuration = 15f;
    [Range(0f, 1f)] public float shieldBuffMultiplier = 0.7f;


    public override void OnEnable()
    {
        base.OnEnable();
        skillType = SkillType.ShieldBuff;
        skillCategory = SkillCategory.Buff;
        hitType = HitType.Single_Hit;
        color = SkillColor.Yellow;
        hitEffectType = EnemyHitEffectType.Magic_Skill;
        cameraShakeMode = false;
        cameraShakeForce = 0;
        cameraShakeDuration = 0;
    }

    public override IEnumerator SkillLogic(PlayerController player)
    {
        isActive = true;
        player.animator.SetTrigger("ShieldBuff");

        if (PlayerInfo.Instance == null)
        {
            //Debug.LogWarning("PlayerInfo.Instance가 존재하지 않습니다.");
            yield break;
        }
        player.playerSkill.StartCoroutine(player.playerSkill.ISSkill(YieldInstructionCache.GetWait(0.7f)));

        PlayerInfo.Instance.ShieldChange(Mathf.RoundToInt(PlayerInfo.Instance.FinalMaxHP * shieldBuffMultiplier));

        yield return YieldInstructionCache.GetWait(0.5f);

        PlayerSoundManager.PlaySound("ShieldBuff");

        if (skilIPrefab != null)
        {
            Transform spawnPos = player.playerSkill.shieldbuffSpawnPoint != null ? player.playerSkill.shieldbuffSpawnPoint : player.playerSkill.transform;
            player.playerSkill.shieldobj = EffectPoolManager.Instance.GetFromPool(skilIPrefab, spawnPos.position, spawnPos.rotation);
            player.playerSkill.shieldobj.transform.SetParent(spawnPos);
        }

        InGameUI.Instance.BuffSkillON(this,shieldBuffDuration);

        yield return YieldInstructionCache.GetWait(shieldBuffDuration);
        PlayerInfo.Instance.ShieldChange(Mathf.RoundToInt(-PlayerInfo.Instance.shield));
        isActive = false;
    }
}
