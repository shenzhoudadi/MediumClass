using BlueprintCore.Actions.Builder;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using MediumClass.Medium.NewActions;
using MediumClass.Medium.NewComponents;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using MediumClass.Utilities;
using MediumClass.Utils;
using System.Linq;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 绝境巡行者的四个降灵能力与“英灵降临”特性。
    ///
    /// 只新建降灵能力本身（名称按猫科精魂命名，图标复用通灵者现有的四个英灵图标），
    /// 四大英灵的能力树、降灵奖励、英灵同调与共鸣惩罚全部复用通灵者已有内容：
    /// 每条英灵条目都与 ChannelSpirit 中对应英灵是同一份配置、同一个 SpiritClass。
    /// </summary>
    internal static class ProwlerSpiritChannel
    {
        private const string IconPrefix = "assets/icons/";
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ProwlerSpiritChannel));

        internal static BlueprintFeature Configure()
        {
            Logger.Log("Generating Prowler channel spirit abilities.");

            var champion = Channel("ProwlerChannelChampion", Guids.ProwlerChannelChampion, Guids.Champion, IconPrefix + "spiritchampion2.png");
            var guardian = Channel("ProwlerChannelGuardian", Guids.ProwlerChannelGuardian, Guids.Guardian, IconPrefix + "spiritguardian2.png");
            var marshal = Channel("ProwlerChannelMarshal", Guids.ProwlerChannelMarshal, Guids.Marshal, IconPrefix + "spiritmarshal2.png");
            var trickster = Channel("ProwlerChannelTrickster", Guids.ProwlerChannelTrickster, Guids.Trickster, IconPrefix + "spirittrickster2.png");

            return FeatureConfigurator.New("ProwlerSpirit", Guids.ProwlerSpirit)
                .SetDisplayName("ProwlerSpirit.Name")
                .SetDescription("ProwlerSpirit.Description")
                .SetIcon(AbilityRefs.ShamanWanderingHexAbility.Reference.Get().Icon)
                .SetIsClassFeature(true)
                // 猛虎 · 勇士
                .AddComponent<MediumSpiritComponent>(c =>
                {
                    c.SpiritClass = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Champion);
                    c.SpiritInfluencePenalty = BlueprintTool.GetRef<BlueprintBuffReference>(Guids.MediumInfluenceDebuff);
                    c.SpiritInfluence = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(Guids.MediumInfluenceResourceChampion);
                    c.SpiritBonusFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus);
                    c.SpiritSeanceBoon = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.ChampionSeanceBoon);
                    c.SpiritLesserPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.ChampionLesser);
                    c.SpiritIntermediatePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.ChampionSuddenAttack);
                    c.SpiritGreaterPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.ChampionFleetCharge);
                    c.SpiritSupremePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.LegendaryChampion);
                    c.Stats = new StatType[] { StatType.SkillAthletics, StatType.SaveFortitude, StatType.AdditionalDamage, StatType.AdditionalAttackBonus };
                    c.Penalties = new StatType[] { StatType.SkillKnowledgeArcana, StatType.SkillKnowledgeWorld, StatType.BonusCasterLevel };
                })
                // 美洲豹 · 守护者
                .AddComponent<MediumSpiritComponent>(c =>
                {
                    c.SpiritClass = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Guardian);
                    c.SpiritInfluencePenalty = BlueprintTool.GetRef<BlueprintBuffReference>(Guids.MediumInfluenceDebuff);
                    c.SpiritInfluence = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(Guids.MediumInfluenceResourceGuardian);
                    c.SpiritBonusFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus);
                    c.SpiritSeanceBoon = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.GuardianSeanceBoon);
                    c.SpiritLesserPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.GuardianLesser);
                    c.SpiritIntermediatePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.GuardianAbsorbBlow);
                    c.SpiritGreaterPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.GuardianGreater);
                    c.SpiritOverwriteGreater = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.SecondaryGuardianGreater);
                    c.SpiritSupremePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.LegendaryGuardian);
                    c.Stats = new StatType[] { StatType.AC, StatType.SaveReflex, StatType.SaveFortitude };
                    c.Penalties = new StatType[] { StatType.AdditionalDamage };
                })
                // 狮子 · 统帅
                .AddComponent<MediumSpiritComponent>(c =>
                {
                    c.SpiritClass = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Marshal);
                    c.SpiritInfluencePenalty = BlueprintTool.GetRef<BlueprintBuffReference>(Guids.MediumInfluenceDebuff);
                    c.SpiritInfluence = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(Guids.MediumInfluenceResourceMarshal);
                    c.SpiritBonusFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus);
                    c.SpiritSeanceBoon = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MarshalSeanceBoon);
                    c.SpiritLesserPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MarshalMarshalsOrders);
                    c.SpiritIntermediatePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MarshalInspiringCallStandard);
                    c.SpiritIntermediatePowerMove = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MarshalInspiringCallMove);
                    c.SpiritIntermediatePowerSwift = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MarshalInspiringCallSwift);
                    c.SpiritGreaterPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MarshalDecisiveStrikeFeature);
                    c.SpiritOverwriteGreater = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.SecondaryDecisiveStrikeFeature);
                    c.SpiritSupremePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MarshalLegendaryMarshal);
                    c.Stats = new StatType[] { StatType.AdditionalAttackBonus }.Concat(StatTypeHelper.Saves).Concat(StatTypeHelper.Skills).ToArray();
                    c.Penalties = new StatType[] { StatType.SkillPerception, StatType.SkillLoreNature, StatType.SkillLoreReligion, StatType.SaveWill };
                    c.Concentration = true;
                })
                // 猎豹 · 诡术师
                .AddComponent<MediumSpiritComponent>(c =>
                {
                    c.SpiritClass = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Trickster);
                    c.SpiritInfluencePenalty = BlueprintTool.GetRef<BlueprintBuffReference>(Guids.MediumInfluenceDebuff);
                    c.SpiritInfluence = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(Guids.MediumInfluenceResourceTrickster);
                    c.SpiritBonusFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus);
                    c.SpiritSeanceBoon = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.TricksterSeanceBoon);
                    c.SpiritLesserPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.TricksterEdge);
                    c.SpiritIntermediatePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.TricksterSurpriseStrike);
                    c.SpiritGreaterPower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.TricksterTransferMagic);
                    c.SpiritOverwriteGreater = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.SecondaryTricksterTransferMagic);
                    c.SpiritSupremePower = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.TricksterLegendaryTrickster);
                    c.Penalties = new StatType[] { StatType.AC, StatType.AdditionalCMB, StatType.AdditionalCMD, StatType.SaveWill, StatType.SaveFortitude };
                    c.Stats = new StatType[] { StatType.SkillAthletics, StatType.SkillKnowledgeArcana, StatType.SkillKnowledgeWorld, StatType.SkillLoreNature, StatType.SkillLoreReligion,
                        StatType.SkillMobility, StatType.SkillPerception, StatType.SkillPersuasion, StatType.SkillStealth, StatType.SkillThievery, StatType.SkillUseMagicDevice, StatType.SaveReflex, StatType.Initiative };
                })
                .AddFacts(new() { champion, guardian, marshal, trickster })
                .Configure();
        }

        private static BlueprintAbility Channel(string name, string guid, string spirit, string icon)
        {
            return AbilityConfigurator.New(name, guid)
                .SetDisplayName(name + ".Name")
                .SetDescription(name + ".Description")
                .SetIcon(icon)
                .AddComponent<AbilityRequirementHasSpirit>(c =>
                {
                    c.Not = true;
                })
                .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                .AddAbilityEffectRunAction(
                    actions: ActionsBuilder.New()
                        .Add<ContextActionApplySpirit>(c =>
                        {
                            c.Spirit = BlueprintTool.GetRef<BlueprintCharacterClassReference>(spirit);
                        })
                        .Add<ContextActionSpiritInfluence>())
                .AddAbilityResourceLogic(
                    requiredResource: BlueprintTool.GetRef<BlueprintAbilityResourceReference>(Guids.MediumInfluenceResource),
                    amount: 1,
                    isSpendResource: true)
                .Configure();
        }
    }
}
