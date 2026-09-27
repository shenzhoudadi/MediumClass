using System.Linq;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints.Classes;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using MediumClass.Medium.NewUnitParts;
using MediumClass.NewComponents;
using MediumClass.Utilities;

namespace MediumClass.Medium
{
    internal static class MediumChannelRestore
    {
        internal static void Restore(UnitDescriptor owner)
        {
            if (owner?.Unit == null) return;
            // The catalogue is derived from owned class facts, never serialized.
            // Rebuild it even when native fact TurnOn has not happened yet.
            bool found = false;
            foreach (var id in new[] { Guids.MediumChannelSpirit, Guids.ProwlerSpirit })
            {
                var fact = owner.Unit.Facts.Get(BlueprintTool.Get<BlueprintFeature>(id));
                if (fact == null || !fact.IsActive) continue;
                found = true;
                fact.CallComponents<MediumSpiritComponent>(c => c.Register());
            }
            if (!found) return;
            var state = owner.Unit.Get<UnitPartMedium>();
            if (state == null) return;

            // Focus can turn on before the catalogue on older saves. Reconcile
            // its derived entries only after every spirit has been registered.
            foreach (var fact in owner.Unit.Facts.List.Where(f => f.IsActive).ToArray())
                fact.CallComponents<MediumSpiritFocusComponent>(c => c.OnTurnOn());

            var channel = owner.Buffs.Enumerable.FirstOrDefault(b => b.IsActive && b.Blueprint ==
                BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff));
            if (channel == null)
            {
                state.ClearChannelSelection();
                return;
            }
            state.RestoreSavedSelection();
            // Restore only derived powers. Reapply/OnDeactivate would end the
            // session, reset influence, and remove its prepared spell grants.
            channel.CallComponents<ApplySpirits>(c => c.RefreshActiveSpirits());
            foreach (var buff in owner.Buffs.Enumerable.Where(b => b.IsActive && b.IsTurnedOn
                && b.Blueprint == BlueprintTool.Get<BlueprintBuff>(Guids.MediumSharedSeanceBuff)).ToArray())
                buff.CallComponents<MediumContextSharedSeanceComponent>(c => c.OnTurnOn());
        }
    }

    [HarmonyPatch(typeof(UnitDescriptor), nameof(UnitDescriptor.ApplyPostLoadFixes))]
    internal static class MediumChannelRestorePatch
    {
        // Before native spellbook fixes and both of our spell/influence postfixes:
        // they must see the recovered six-circle progression and channel choice.
        private static void Prefix(UnitDescriptor __instance) => MediumChannelRestore.Restore(__instance);
    }
}
