using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Components;
using MediumClass.Utilities;
using MediumClass.Utils;
using System;
using System.Linq;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 让通灵者英灵之力里的“职业等级”档位对绝境巡行者生效。
    ///
    /// 手法与 TTT Base 的 NatureFang（德鲁伊变体拿杀手天赋）一致：把血怒者追加进 ClassLevel 档位的
    /// 职业列表，并把本变体追加进 m_AdditionalArchetypes。引擎在
    /// ContextRankConfig.CheckAndSetMaxValue / CheckClassForArchetype 里的语义是：
    ///   该职业蓝图“定义了”所配置的变体 → 只有角色真的选了该变体，这个职业的等级才计数；
    ///   该职业蓝图没有定义该变体 → 该职业无条件计数。
    /// 因此：
    ///   纯通灵者：数值与今天完全一致；
    ///   未选本变体的血怒者（纯血怒者、原怒者等）：血怒者等级不计数；
    ///   选了本变体的血怒者：按血怒者等级计数。
    /// </summary>
    internal static class ProwlerSpiritScaling
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ProwlerSpiritScaling));
        private static bool applied;

        internal static void Configure()
        {
            if (applied) { return; }
            applied = true;

            var bloodrager = BlueprintTool.GetRef<BlueprintCharacterClassReference>(ProwlerContent.Bloodrager);
            var archetype = BlueprintTool.GetRef<BlueprintArchetypeReference>(Guids.ProwlerArchetype);

            // 守护者中等英灵之力“吸收冲击”：DR/抗力 = 职业等级的一半。
            AppendClassLevel(BlueprintTool.Get<BlueprintFeature>(Guids.GuardianAbsorbBlow), AbilityRankType.Default, bloodrager, archetype);
            // 守护者高等英灵之力“守护者惩击”：伤害档位 = 职业等级。
            AppendClassLevel(BlueprintTool.Get<BlueprintAbility>(Guids.GuardianGreaterAbility), AbilityRankType.DamageBonus, bloodrager, archetype);

            // 同一个“吸收冲击”还会把“恩典”作为2环法术加入通灵者已知法术。绝境巡行者没有通灵者法术书，
            // 因此为拥有该能力的角色补一份指向血怒者法术书的同类组件（纯通灵者不受影响：他们同样没有血怒者法术书）。
            FeatureConfigurator.For(Guids.GuardianAbsorbBlow)
                .AddSpellKnownTemporary(
                    characterClass: BlueprintTool.GetRef<BlueprintCharacterClassReference>(ProwlerContent.Bloodrager),
                    spell: AbilityRefs.BestowGrace.Reference.Get(),
                    level: 2,
                    onlySpontaneous: true)
                .Configure();

            Logger.Log("Prowler spirit class-level scaling applied.");
        }

        private static void AppendClassLevel(BlueprintScriptableObject owner, AbilityRankType type,
            BlueprintCharacterClassReference bloodrager, BlueprintArchetypeReference archetype)
        {
            var configs = owner.ComponentsArray.OfType<ContextRankConfig>()
                .Where(c => c.m_BaseValueType == ContextRankBaseValueType.ClassLevel && c.m_Type == type)
                .ToArray();
            if (configs.Length == 0)
            {
                Logger.Log($"No class-level rank config found on {owner.name} ({type}).");
                return;
            }
            foreach (var config in configs)
            {
                config.m_Class = (config.m_Class ?? Array.Empty<BlueprintCharacterClassReference>())
                    .Concat(new[] { bloodrager }).ToArray();
                config.m_AdditionalArchetypes = (config.m_AdditionalArchetypes ?? Array.Empty<BlueprintArchetypeReference>())
                    .Concat(new[] { archetype }).ToArray();
            }
        }
    }
}
