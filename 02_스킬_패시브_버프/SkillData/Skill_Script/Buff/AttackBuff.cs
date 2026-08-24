using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "AttackBuff", menuName = "Skill/AttackBuff")]
public class AttackBuff : SkillData
{
    [Header("���� ���� ����")]
    public float attackBuffDuration = 15f;
    [Range(1f, 100f)] public float attackBuffMultiplier = 30f;

    public override void OnEnable()
    {
        base.OnEnable();
        skillType = SkillType.AttackBuff;
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
        player.animator.SetTrigger("AttackBuff");
        player.playerSkill.StartCoroutine(player.playerSkill.ISSkill(YieldInstructionCache.GetWait(0.7f)));
        if (PlayerInfo.Instance == null)
        {
            Debug.LogWarning("PlayerInfo.Instance�� �������� �ʽ��ϴ�.");
            yield break;
        }

        float multiplier = attackBuffMultiplier/100;
        PlayerInfo.Instance.SetAttackModifier("AttackBuff",multiplier);

        InGameUI.Instance.BuffSkillON(this, attackBuffDuration);

        yield return YieldInstructionCache.GetWait(0.19f);

        if (skilIPrefab != null)
        {
            Transform spawnPos = player.playerSkill.attackbuffSpawnPoint != null ? player.playerSkill.attackbuffSpawnPoint : player.playerSkill.transform;
            GameObject effect = Instantiate(skilIPrefab, spawnPos.position, spawnPos.rotation, spawnPos);
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            Destroy(effect, attackBuffDuration);
        }
        PlayerSoundManager.PlaySound("AttackBuff");

        yield return new WaitForSeconds(attackBuffDuration - 0.19f);

        PlayerInfo.Instance.SetAttackModifier("AttackBuff", 0);

        isActive = false;
    }
}

