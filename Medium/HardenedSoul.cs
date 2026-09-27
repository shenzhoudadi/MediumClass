using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.UnitLogic;
using MediumClass.Utilities;
using MediumClass.Utils;

namespace MediumClass.Medium
{
    // Optional TTT Base integration. No assembly reference to TTT Base is required.
    internal static class HardenedSoul
    {
        internal const string FeatureGuid = "28224e93-7791-4385-bc98-5bd6e1c4f56d";
        internal const string SelectionGuid = "9881d67b-c736-4e81-a3b3-c7281f07db4f";
        internal const string PerfectBodyGuid = "cb61beef-4f35-4992-a09e-821d170a6582";
        internal const string GreatBeastGuid = "fc2f6d6e-eafd-46d8-842e-b3c88a732213";
        private static BlueprintFeature feature;
        private static readonly UnityModManagerNet.UnityModManager.ModEntry.ModLogger Logger = Logging.GetLogger(nameof(HardenedSoul));

        internal static bool HasFeature(UnitDescriptor owner)
        {
            if (owner == null) return false;
            var blueprint = feature ?? ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(BlueprintGuid.Parse(FeatureGuid));
            return blueprint != null && owner.HasFact(blueprint);
        }

        internal static void Configure()
        {
            // Keep our own blueprints resolvable even when TTT is absent/disabled.
            // Only ConfigureOptionalIntegration changes which feature level 20 grants.
            feature = FeatureConfigurator.New("MediumHardenedSoul", FeatureGuid)
                .SetDisplayName("HardenedSoul.Name")
                .SetDescription("HardenedSoul.Description")
                .SetIcon("assets/icons/spiritsurge.png")
                .SetIsClassFeature(true)
                .SetRanks(1)
                .AddComponent<PrerequisiteClassLevel>(c => {
                    c.m_CharacterClass = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Medium);
                    c.Level = 20;
                })
                .AddComponent<PrerequisiteNoFeature>(c => {
                    c.m_Feature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.AstralBeacon);
                })
                .Configure();

            FeatureSelectionConfigurator.New("MediumAlternateCapstone", SelectionGuid)
                .SetDisplayName("MediumAlternateCapstone.Name")
                .SetDescription("MediumAlternateCapstone.Description")
                .SetIcon("assets/icons/spiritsurge.png")
                .SetIsClassFeature(true)
                .SetRanks(1)
                .SetIgnorePrerequisites(false)
                .SetReapplyOnLevelUp(false)
                // The level-20 progression entry controls when this is offered.
                // A NoFeature check on the selection itself would become false
                // as soon as its chosen capstone is added to the preview unit.
                .AddToAllFeatures(Guids.AstralBeacon, FeatureGuid)
                .Configure();
        }

        // Called after the game's blueprint cache patches, including TTT's late
        // capstone patches. Reading these internals is optional and fail-closed.
        internal static void ConfigureOptionalIntegration()
        {
            try
            {
                if (!Settings.IsTTTBaseEnabled()) return;
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TabletopTweaks-Base");
                var main = assembly?.GetType("TabletopTweaks.Base.Main", false);
                var context = main?.GetField("TTTContext", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                if (!AlternateCapstonesEnabled(context)) return;

                var medium = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>(BlueprintGuid.Parse(Guids.Medium));
                var selection = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>(BlueprintGuid.Parse(SelectionGuid));
                if (medium?.Progression == null || selection == null) return;
                var choices = new List<BlueprintFeatureReference> {
                    BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.AstralBeacon),
                    BlueprintTool.GetRef<BlueprintFeatureReference>(FeatureGuid)
                };
                // Great Beast retains its native pet prerequisite. Old Dog, New
                // Tricks is excluded: Medium does not grant four combat feats.
                foreach (var id in new[] { PerfectBodyGuid, GreatBeastGuid })
                {
                    var optional = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(BlueprintGuid.Parse(id));
                    if (optional != null) choices.Add(optional.ToReference<BlueprintFeatureReference>());
                }
                selection.m_Features = choices.ToArray();
                selection.m_AllFeatures = choices.ToArray();
                if (ReplaceCapstone(medium, selection)) Logger.Log("Enabled TTT alternate capstones for Medium (Hardened Soul).");
            }
            catch (Exception e)
            {
                // An unavailable/changed optional API must not block character creation.
                Logger.Log("Skipped optional TTT Medium capstone integration: " + e.GetType().Name + ": " + e.Message);
            }
        }

        internal static bool AlternateCapstonesEnabled(object context)
        {
            try
            {
                var group = ReadMember(ReadMember(context, "Fixes"), "AlternateCapstones");
                return ReadMember(group, "DisableAll") is bool disabled && !disabled;
            }
            catch { return false; }
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();
            var field = type.GetField(name, flags);
            return field != null ? field.GetValue(instance) : type.GetProperty(name, flags)?.GetValue(instance, null);
        }

        internal static bool ReplaceCapstone(BlueprintCharacterClass medium, BlueprintFeatureSelection selection)
        {
            var progression = medium?.Progression;
            if (progression?.LevelEntries == null || selection == null) return false;
            var oldId = BlueprintGuid.Parse(Guids.AstralBeacon);
            var newRef = selection.ToReference<BlueprintFeatureBaseReference>();
            var entries = progression.LevelEntries.Where(e => e != null && e.Level == 20
                && e.m_Features != null && e.m_Features.Any(f => f?.deserializedGuid == oldId)).ToArray();
            if (entries.Length == 0) return false;

            foreach (var entry in entries) ReplaceReferences(entry.m_Features, oldId, newRef);
            foreach (var group in progression.UIGroups ?? Array.Empty<UIGroup>())
                if (group?.m_Features != null) ReplaceReferences(group.m_Features, oldId, newRef);
            if (progression.m_UIDeterminatorsGroup != null)
                progression.m_UIDeterminatorsGroup = progression.m_UIDeterminatorsGroup
                    .Select(f => f?.deserializedGuid == oldId ? newRef : f).ToArray();
            // A Medium archetype which gives away Astral Beacon must also give
            // away the replacement selection, matching TTT's class integration.
            foreach (var archetype in medium.Archetypes)
                foreach (var removed in archetype.RemoveFeatures ?? Array.Empty<LevelEntry>())
                    if (removed?.Level == 20 && removed.m_Features != null
                        && removed.m_Features.Any(f => f?.deserializedGuid == oldId)
                        && !removed.m_Features.Any(f => f?.deserializedGuid == selection.AssetGuid))
                        removed.m_Features.Add(newRef);
            return true;
        }

        private static void ReplaceReferences(List<BlueprintFeatureBaseReference> references,
            BlueprintGuid oldId, BlueprintFeatureBaseReference replacement)
        {
            for (int i = 0; i < references.Count; i++)
                if (references[i]?.deserializedGuid == oldId) references[i] = replacement;
        }
    }
}
