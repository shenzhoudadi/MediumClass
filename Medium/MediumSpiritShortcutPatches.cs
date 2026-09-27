using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using MediumClass.Utilities;

namespace MediumClass.Medium
{
    internal static class MediumSpiritActionBarRules
    {
        private static readonly BlueprintGuid LegacyHierophantId = BlueprintGuid.Parse(Guids.HierophantSupremeAbility4);
        private static readonly BlueprintGuid CurrentHierophantId = BlueprintGuid.Parse(Guids.HierophantSupremeAbility2);

        internal static Ability FindAbility(UnitDescriptor owner, BlueprintGuid id)
        {
            if (owner?.Abilities == null) return null;
            foreach (var ability in owner.Abilities)
                if (ability?.Data?.Blueprint?.AssetGuid == id) return ability;
            return null;
        }

        internal static void HideLegacyAbility(UnitDescriptor owner) => FindAbility(owner, LegacyHierophantId)?.Hide();

        internal static AbilityData SlotAbility(MechanicActionBarSlot slot) =>
            (slot as MechanicActionBarSlotSpell)?.Spell
            ?? (slot as MechanicActionBarSlotSpontaneusConvertedSpell)?.Spell
            ?? (slot as MechanicActionBarSlotAbility)?.Ability;

        // Never use availability/CanSpend here: a depleted real spell belongs on the bar.
        // The two book identities distinguish preparation-only spells from castable spells.
        internal static bool TryRemap(MechanicActionBarSlot slot, UnitDescriptor owner,
            out MechanicActionBarSlot replacement)
        {
            replacement = slot;
            var spell = SlotAbility(slot);
            if (spell == null || owner == null) return false;
            if (spell.Blueprint?.AssetGuid == LegacyHierophantId)
            {
                var current = FindAbility(owner, CurrentHierophantId);
                replacement = current == null
                    ? (MechanicActionBarSlot)new MechanicActionBarSlotEmpty { Unit = owner.Unit }
                    : new MechanicActionBarSlotAbility { Ability = current.Data, Unit = owner.Unit };
                return true;
            }
            if (!MediumSpiritSpellbookRules.IsPreparationBook(spell.Spellbook)) return false;

            var main = owner.Spellbooks.FirstOrDefault(MediumSpiritSpellbookRules.IsMediumBook);
            var level = spell.SpellLevel;
            var equivalent = main == null || level < 0 || level > 10 ? null
                : main.GetKnownSpells(level).Concat(main.GetCustomSpells(level)).Concat(main.GetSpecialSpells(level))
                    .FirstOrDefault(candidate => candidate.Blueprint == spell.Blueprint
                        && Equals(candidate.MetamagicData, spell.MetamagicData));
            replacement = equivalent == null
                ? (MechanicActionBarSlot)new MechanicActionBarSlotEmpty { Unit = owner.Unit }
                : new MechanicActionBarSlotSpontaneousSpell(equivalent) { Unit = owner.Unit };
            return true;
        }
    }

    // Automatic shortcut creation has its own enumeration outside Fetch.
    [HarmonyPatch]
    internal static class MediumSpiritPreparationAutomaticShortcutPatch
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(UnitUISettings)
            .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "CollectSpells"
                && method.GetParameters().Length > 0
                && method.GetParameters()[0].ParameterType == typeof(Spellbook));

        private static bool Prefix(Spellbook __0) => !MediumSpiritSpellbookRules.IsPreparationBook(__0);
    }

    // Covers dragging as well as Add to Action Bar from the preparation window.
    // A prepared spell is redirected only if the same spell is actually known
    // by the main book; trying to add an unprepared choice leaves the bar intact.
    [HarmonyPatch(typeof(UnitUISettings), nameof(UnitUISettings.SetSlot),
        new[] { typeof(MechanicActionBarSlot), typeof(int) })]
    internal static class MediumSpiritPreparationSetSlotPatch
    {
        private static bool Prefix(UnitUISettings __instance, ref MechanicActionBarSlot __0)
        {
            if (!MediumSpiritActionBarRules.TryRemap(__0, __instance.Owner, out var replacement)) return true;
            if (replacement is MechanicActionBarSlotEmpty) return false;
            __0 = replacement;
            return true;
        }
    }

    // Lazy migration occurs only when a saved bar is actually displayed. This
    // avoids touching the bar before post-load spell transfer has finished.
    [HarmonyPatch(typeof(UnitUISettings), nameof(UnitUISettings.GetSlot))]
    internal static class MediumSpiritPreparationSavedSlotPatch
    {
        private static void Prefix(UnitUISettings __instance, int __0, MechanicActionBarSlot[] ___m_Slots)
        {
            // Run before the native IsBad cleanup can discard an old hidden fact
            // or a memorized slot whose preparation book is no longer active.
            if (___m_Slots == null || __0 < 0 || __0 >= ___m_Slots.Length) return;
            if (!MediumSpiritActionBarRules.TryRemap(___m_Slots[__0], __instance.Owner, out var replacement)) return;
            __instance.SetSlot(replacement, __0);
        }
    }

    // UpdateBadSlots examines the raw array before the UI calls GetSlot. In
    // particular, hiding the old fourth legendary entry makes that slot bad.
    // Remap at the shared native replacement point so it cannot be discarded
    // before the lazy GetSlot migration has a chance to run.
    [HarmonyPatch(typeof(UnitUISettings), "GetBadSlotReplacement")]
    internal static class MediumSpiritPreparationBadSlotReplacementPatch
    {
        private static bool Prefix(MechanicActionBarSlot __0, UnitDescriptor __1,
            ref MechanicActionBarSlot __result)
        {
            if (!MediumSpiritActionBarRules.TryRemap(__0, __1, out var replacement)) return true;
            __result = replacement;
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitDescriptor), nameof(UnitDescriptor.ApplyPostLoadFixes))]
    internal static class MediumLegendaryHierophantLegacyAbilityPatch
    {
        private static void Postfix(UnitDescriptor __instance)
        {
            // AddFacts may retain the old fourth fact in an existing save.
            // Keep it valid for saved references, but show only the three new entries.
            MediumSpiritActionBarRules.HideLegacyAbility(__instance);
        }
    }
}
