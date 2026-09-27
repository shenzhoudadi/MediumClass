using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.FactLogic;
using MediumClass.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 绝境巡行者的血脉延迟进阶表（固定命运血脉主血脉、第二血脉、原怒者子进阶）。
    /// 血脉之力档位按规则延迟到 4/9/12/15/20 级。
    /// </summary>
    internal static class ProwlerProgressions
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ProwlerProgressions));
        internal static readonly Dictionary<BlueprintGuid, LevelEntry[]> Tables = new Dictionary<BlueprintGuid, LevelEntry[]>();

        internal static void Build(BlueprintProgression primary)
        {
            var secondary = ProwlerBlueprints.Get<BlueprintFeatureSelection>(ProwlerContent.SecondBloodlineSelection);
            foreach (var bloodline in secondary.AllFeatures.OfType<BlueprintProgression>().Concat(new[] { primary }).Distinct())
            {
                Tables[bloodline.AssetGuid] = ConvertBloodline(bloodline);
            }

            var primalist = ProwlerBlueprints.Get<BlueprintProgression>(ProwlerContent.Primalist);
            Tables[primalist.AssetGuid] = Shift(primalist.LevelEntries);

            var choices = primalist.LevelEntries.SelectMany(e => e.Features).OfType<BlueprintFeatureSelection>()
                .Concat(secondary.AllFeatures.OfType<BlueprintProgression>().SelectMany(p => p.LevelEntries)
                    .SelectMany(e => e.Features).OfType<BlueprintFeatureSelection>().Where(f => f.name.Contains("Primalist"))).Distinct();

            foreach (var choice in choices)
            {
                foreach (var child in choice.AllFeatures.OfType<BlueprintProgression>())
                {
                    Tables[child.AssetGuid] = Shift(child.LevelEntries);
                }
            }

            Logger.Log($"Prepared per-character progression plans: {Tables.Count}");
        }

        private static LevelEntry[] Shift(IEnumerable<LevelEntry> entries) => entries
            .Where(e => e.Level >= 4 && ProwlerProgressionPolicy.Delay(e.Level) > 0)
            .Select(e => ProwlerBlueprints.Level(ProwlerProgressionPolicy.Delay(e.Level), e.Features.ToArray())).ToArray();

        private static LevelEntry[] ConvertBloodline(BlueprintProgression bp)
        {
            var result = new SortedDictionary<int, List<BlueprintFeatureBase>>();
            var firstLevel = new Dictionary<BlueprintGuid, int>();

            foreach (var entry in bp.LevelEntries.OrderBy(e => e.Level))
            {
                foreach (var original in entry.Features)
                {
                    var f = original;
                    // 原版酸素第二血脉在 12 级的选项误指向了 16 级选项，此处针对该表修正。
                    if (bp.name == "BloodragerElementalAcidSecondBloodline" && entry.Level == 12
                        && f.AssetGuid.ToString() == "d43b1581b1f64f928e55d7ad6d36cb13")
                    {
                        f = ProwlerBlueprints.Get<BlueprintFeatureSelection>("c63fcfd424a64d1aa195e7721b33863e");
                    }

                    int level = entry.Level;
                    bool seen = firstLevel.TryGetValue(f.AssetGuid, out int first);
                    if (!seen) firstLevel[f.AssetGuid] = level;

                    bool choice = f.name.Contains("Primalist");
                    bool identity = level == 1 && f.name.Contains("Requisite");
                    bool spell = f.ComponentsArray.Any(c => c is AddKnownSpell);
                    bool feat = f is BlueprintFeatureSelection && !choice && (level == 6 || level == 9 || level == 12 || level == 15 || level == 18);
                    bool upgrade = (f.name.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) >= 0 && level > 1)
                        || f.name.Contains("ResourceIncrease");
                    bool repeatedRank = seen && ProwlerProgressionPolicy.CoreTier(first) && !feat && !choice && !spell;

                    level = ProwlerProgressionPolicy.FeatureLevel(entry.Level, identity, spell, feat, upgrade, repeatedRank);
                    if (level < 0) continue;

                    BlueprintFeatureBase grant = f;
                    if (repeatedRank)
                    {
                        // 重复抗性等级属于较早的核心能力，若原怒者替换了它则不应获得，包装一层条件判断。
                        var wrapper = ProwlerBlueprints.New<BlueprintFeature>("Upgrade_" + bp.AssetGuid + "_" + f.AssetGuid + "_" + level);
                        wrapper.m_DisplayName = f.m_DisplayName;
                        wrapper.m_Description = f.m_Description;
                        wrapper.HideInUI = true;
                        wrapper.HideInCharacterSheetAndLevelUp = true;
                        wrapper.IsClassFeature = true;
                        ProwlerBlueprints.Add(wrapper, new AddFeatureIfHasFact
                        {
                            m_CheckedFact = ((BlueprintFeature)f).ToReference<BlueprintUnitFactReference>(),
                            m_Feature = ((BlueprintFeature)f).ToReference<BlueprintUnitFactReference>()
                        });
                        grant = wrapper;
                    }

                    if (!result.TryGetValue(level, out var features))
                    {
                        result[level] = features = new List<BlueprintFeatureBase>();
                    }
                    features.Add(grant);
                }
            }

            return result.Select(p => ProwlerBlueprints.Level(p.Key, p.Value.ToArray())).ToArray();
        }
    }

    [HarmonyPatch(typeof(LevelUpHelper), nameof(LevelUpHelper.UpdateProgression))]
    internal static class DelayedProgressionPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(LevelUpState state, UnitDescriptor unit, BlueprintProgression progression)
        {
            if (!ProwlerBlueprints.IsProwler(unit) || !ProwlerProgressions.Tables.TryGetValue(progression.AssetGuid, out var entries))
            {
                return true;
            }
            var data = unit.Progression.SureProgressionData(progression);
            data.LevelEntries = entries;
            int previous = data.Level;
            int current = progression.CalcLevel(unit);
            data.Level = current;
            if (previous >= current || (progression.ExclusiveProgression != null && state.SelectedClass != progression.ExclusiveProgression))
            {
                return false;
            }
            foreach (int level in ProwlerProgressionPolicy.NewlyReached(previous, current))
            {
                var entry = data.GetLevelEntry(level);
                LevelUpHelper.AddFeaturesFromProgression(state, unit, entry.Features, progression, level);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitProgressionData), nameof(UnitProgressionData.PostLoad))]
    internal static class RestoreProgressionTables
    {
        private static void Postfix(UnitProgressionData __instance)
        {
            if (!ProwlerBlueprints.IsProwler(__instance.Owner)) return;
            foreach (var entry in __instance.GetAllProgressions)
            {
                if (ProwlerProgressions.Tables.TryGetValue(entry.Key.AssetGuid, out var plan))
                {
                    entry.Value.LevelEntries = plan;
                }
            }
        }
    }
}
