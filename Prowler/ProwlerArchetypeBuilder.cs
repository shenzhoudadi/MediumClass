using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using BlueprintCore.Utils.Types;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using MediumClass.Utilities;
using MediumClass.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 绝境巡行者变体本体的创建与发布。
    ///
    /// 第一阶段（随通灵者本体一起，在蓝图初始化管线内）只创建蓝图；
    /// 第二阶段（依赖 TTT Base 的命运血脉）由 <see cref="ProwlerBuild.ProwlerLateInit"/> 发布。
    /// </summary>
    internal static class ProwlerArchetypeBuilder
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ProwlerArchetypeBuilder));

        internal static BlueprintArchetype Archetype { get; private set; }
        internal static BlueprintFeature SpiritFeature { get; private set; }
        internal static BlueprintFeature Intermediate { get; private set; }
        internal static BlueprintFeature Greater { get; private set; }
        internal static BlueprintFeature Supreme { get; private set; }

        private static readonly HashSet<string> AddedEntries = new HashSet<string>();

        internal static void Configure()
        {
            SpiritFeature = ProwlerSpiritChannel.Configure();
            ProwlerSpiritTiers.Configure(out var intermediate, out var greater, out var supreme);
            Intermediate = intermediate;
            Greater = greater;
            Supreme = supreme;

            // Supplying the class to New() publishes the archetype immediately.
            // Keep the parent class but register only after the TTT bloodline is ready.
            Archetype = ArchetypeConfigurator.New("ProwlerArchetype", Guids.ProwlerArchetype)
                .SetParentClass(BlueprintTool.Get<BlueprintCharacterClass>(ProwlerContent.Bloodrager))
                .SetLocalizedName("ProwlerArchetype.Name")
                .SetLocalizedDescription("ProwlerArchetype.Description")
                // 固定命运血脉：移除原版的血脉选择，改为在第二阶段补入命运血脉。
                .SetRemoveFeatures(LevelEntryBuilder.New()
                    .AddEntry(1, ProwlerContent.BloodlineSelection))
                .SetAddFeatures(LevelEntryBuilder.New()
                    .AddEntry(1, Guids.ProwlerSpirit, Guids.MediumInfluence, Guids.MediumSpiritBonus, Guids.SpiritPower, Guids.SpiritSurge)
                    .AddEntry(4, Guids.MediumSpiritBonus)
                    .AddEntry(8, Guids.ProwlerIntermediate, Guids.MediumSpiritBonus)
                    .AddEntry(10, Guids.SpiritSurge)
                    .AddEntry(12, Guids.MediumSpiritBonus)
                    .AddEntry(16, Guids.ProwlerGreater, Guids.MediumSpiritBonus)
                    .AddEntry(20, Guids.ProwlerSupreme, Guids.MediumSpiritBonus, Guids.SpiritSurge))
                .Configure();
            Logger.Log("Prowler archetype blueprints created (not published yet).");
        }

        /// <summary>第二阶段为变体追加升级条目（化形等）。</summary>
        internal static void AddFeatureEntry(int level, params BlueprintFeatureBase[] features)
        {
            if (Archetype == null || features == null || features.Length == 0) { return; }
            var entries = Archetype.AddFeatures ?? Array.Empty<LevelEntry>();
            var entry = entries.FirstOrDefault(e => e.Level == level);
            if (entry == null)
            {
                entry = new LevelEntry { Level = level, m_Features = new List<BlueprintFeatureBaseReference>() };
                Archetype.AddFeatures = entries.Concat(new[] { entry }).ToArray();
            }
            if (entry.m_Features == null) { entry.m_Features = new List<BlueprintFeatureBaseReference>(); }
            foreach (var feature in features)
            {
                if (!AddedEntries.Add(level + ":" + feature.AssetGuid)) { continue; }
                entry.m_Features.Add(feature.ToReference<BlueprintFeatureBaseReference>());
            }
            Logger.Log($"Added {features.Length} feature(s) to Prowler level {level}.");
        }

        /// <summary>第二阶段为变体追加升级条目（化形等）。</summary>
        internal static void AddFeatureEntry(int level, params string[] guids)
        {
            if (Archetype == null || guids == null || guids.Length == 0) { return; }
            var entries = Archetype.AddFeatures ?? Array.Empty<LevelEntry>();
            var entry = entries.FirstOrDefault(e => e.Level == level);
            if (entry == null)
            {
                entry = new LevelEntry { Level = level, m_Features = new List<BlueprintFeatureBaseReference>() };
                Archetype.AddFeatures = entries.Concat(new[] { entry }).ToArray();
            }
            if (entry.m_Features == null) { entry.m_Features = new List<BlueprintFeatureBaseReference>(); }
            foreach (var guid in guids)
            {
                if (!AddedEntries.Add(level + ":" + guid)) { continue; }
                entry.m_Features.Add(BlueprintTool.GetRef<BlueprintFeatureBaseReference>(guid));
            }
            Logger.Log($"Added {guids.Length} feature(s) to Prowler level {level}.");
        }

        /// <summary>固定命运血脉：由第二阶段在 1 级补入。</summary>
        internal static void AddBloodline(BlueprintProgression bloodline)
        {
            if (bloodline == null) { return; }
            AddFeatureEntry(1, bloodline);
        }

        /// <summary>把变体挂到血怒者职业上。必须在其他 mod 的蓝图都就绪之后调用。</summary>
        internal static void Publish()
        {
            if (Archetype == null)
            {
                Logger.Log("Prowler archetype was not created; publication skipped.");
                return;
            }

            var bloodlines = ProwlerBlueprints.Get<BlueprintFeatureSelection>(ProwlerContent.BloodlineSelection);
            var destined = bloodlines.AllFeatures.OfType<BlueprintProgression>()
                .SingleOrDefault(p => p.name == ProwlerContent.DestinedBloodline);
            if (destined == null)
            {
                throw new InvalidOperationException("请启用 TTT Base 的 DestinedBloodline（命运血脉）后重启游戏。");
            }

            ProwlerForms.Build();
            ProwlerProgressions.Build(destined);

            AddBloodline(destined);
            AddFeatureEntry(11, ProwlerForms.ShapeFeature);
            AddFeatureEntry(12, ProwlerForms.CastingFeature);

            ProwlerBlueprints.CompleteCreation();

            var bloodrager = BlueprintTool.Get<BlueprintCharacterClass>(ProwlerContent.Bloodrager);
            var existing = bloodrager.m_Archetypes ?? Array.Empty<BlueprintArchetypeReference>();
            if (!existing.Any(reference => reference != null && reference.Get() == Archetype))
                bloodrager.m_Archetypes = existing.Concat(new[] { Archetype.ToReference<BlueprintArchetypeReference>() }).ToArray();
            Logger.Log("Prowler archetype published to the bloodrager class.");
        }
    }
}
