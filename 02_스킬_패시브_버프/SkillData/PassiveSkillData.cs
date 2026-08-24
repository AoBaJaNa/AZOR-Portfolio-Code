using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
public enum PassiveSkillType
{
    DoubleHit,
    Auto_Recovery,
    Increase_Attack,
    Increase_HP,
    Stun_Add_Deal,
    Moveset_Cooldown,
    Dangerous_Awakening,
    Auto_Heal_Use,
    HealStone_Boost,
    Increase_Defence,
    Increase_Critical,
    Increase_HealStone_MaxCount,
    Stigma_Echoes_Of_Agony,
    Stigma_Corrupted_Shroud,
    Stigma_Hound_Pursuit,
    Stigma_Cruel_Engraving,
    Stigma_Contagious_Sin,
    Stigma_Infected_Burst,
    Stigma_Abyssal_Hook,
    Stigma_Lord_of_Penance,
    Stigma_Soul_Devourer,
    Stigma_Abyssal_Elegy,
    Berserker_Scent_of_Blood,
    Berserker_Stubborn_Survival,
    Berserker_Boiling_Veins,
    Berserker_Crimson_Recoil,
    Berserker_Overload_Eruption,
    Berserker_Wounded_Lion,
    Berserker_Blood_Feast,
    Berserker_Risk_Awakening,
    Berserker_Blood_Pact,
    Berserker_Deaths_Threshold,
    Berserker_Heart_of_Slaughter
}
public enum PassiveSkillRank
{
    Normal,
    Epic,
    Mystic
}
public enum PassiveSkillBuildType
{
    None,
    Stigma,
    Berserker,
}
[CreateAssetMenu(
    fileName = "NewPassiveSkillData",
    menuName = "Skill/Passive Skill Data",
    order = 1)]
public class PassiveSkillData : ScriptableObject
{
    [Header("Debug")]
    public bool enableDebugLog = true;

    public PassiveSkillType skillType;
    public PassiveSkillRank rank;
    public PassiveSkillBuildType buildType;
    public string skillName;
    public int unlockLevel;
    public Sprite skillIcon;
    public Sprite skillIcon_Frame;


    // 패시브가 장착될 때 (능력치 영구 증가 등)
    public virtual void OnEquip(PlayerController player) { }

    // 패시브가 해제될 때
    public virtual void OnUnEquip(PlayerController player)
    {
        if (buildType == PassiveSkillBuildType.Stigma &&
            player != null &&
            player.playerPassiveController != null)
        {
            player.playerPassiveController.UnRegisterStigmaStack(this);
        }
    }

    // 공격 시점에 호출
    public virtual void OnAttack(PlayerController player, SkillData skill) { }
    public virtual void EndAttack(PlayerController player, HashSet<EnemyClass> enemies) { }
    public virtual bool OnBeforeDamaged(PlayerController player, int incomingDamage)
    {
        return false;
    }
    public virtual void OnDamaged(PlayerController player) { }
    // 매 초마다 호출 (회복 등)
    public virtual void OnUpdate(PlayerController player) { }
    public virtual float GetAttackMultiplier() => 0f;
    public virtual float GetHPMultiplier() => 0f;
    public virtual float GetDefenceMultiplier() => 0f;
    public virtual float GetCriticalMultiplier() => 0f;
    public virtual float OnEnemyStunAddDeal() => 0f;
    public virtual void StigmaLogicBeforeStackCount(Stigma stigma) {}
    public virtual void StigmaSearch(int count, Stigma[] stigmaArray, PlayerController player) {}

    protected void DebugPassiveLog(string message)
    {
        if (!enableDebugLog) return;
        Debug.Log($"[PassiveDebug][{skillType}] {message}");
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        string targetName = skillType.ToString();

        if (name != targetName)
        {
            AssetDatabase.RenameAsset(
                AssetDatabase.GetAssetPath(this),
                targetName
            );
            AssetDatabase.SaveAssets();
        }
    }
#endif
}





