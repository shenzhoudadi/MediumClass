using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;

namespace MediumClass.Medium
{
    [HarmonyPatch(typeof(ActionBarSpellbookHelper), nameof(ActionBarSpellbookHelper.Fetch))]
    internal static class MediumSpiritPreparationActionBarRefreshPatch
    {
        private static void Prefix(UnitEntityData __0) =>
            MediumSpiritSpellbookRules.SyncForActionBar(__0?.Descriptor);
    }

    // The native action bar has separate paths for cantrips/spontaneous abilities
    // and memorized slots. Both need filtering before deduplication occurs.
    [HarmonyPatch(typeof(ActionBarSpellbookHelper), "TryAddAbility")]
    internal static class MediumSpiritPreparationActionBarPatch
    {
        private static bool Prefix(AbilityData __1) =>
            !MediumSpiritSpellbookRules.IsPreparationBook(__1?.Spellbook);
    }

    [HarmonyPatch(typeof(ActionBarSpellbookHelper), "TryAddSpell")]
    internal static class MediumSpiritPreparationMemorizedActionBarPatch
    {
        private static bool Prefix(SpellSlot __1) =>
            !MediumSpiritSpellbookRules.IsPreparationBook(__1?.SpellShell?.Spellbook);
    }

}
