using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Parts;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

namespace MediumClass.Medium
{
    internal static class MediumSpiritSpellbookRules
    {
        private static readonly BlueprintGuid MediumBookId = BlueprintGuid.Parse(Guids.MediumSpellbook);
        private static readonly BlueprintGuid ArchmageBookId = BlueprintGuid.Parse(Guids.ArchmageSpellbook);
        private static readonly BlueprintGuid HierophantBookId = BlueprintGuid.Parse(Guids.HierophantSpellbook);
        private static readonly BlueprintGuid ArchmageId = BlueprintGuid.Parse(Guids.Archmage);
        private static readonly BlueprintGuid HierophantId = BlueprintGuid.Parse(Guids.Hierophant);
        private static readonly AccessTools.FieldRef<Spellbook, int[]> SpontaneousSlots =
            AccessTools.FieldRefAccess<Spellbook, int[]>("m_SpontaneousSlots");
        // The game's Harmony can predate the four-argument MethodDelegate overload.
        // Use the framework reflection API so even ordinary spellbook queries can
        // initialize these rules without depending on that newer Harmony method.
        private static readonly MethodInfo UpdatePreparationSlots = typeof(Spellbook).GetMethod(
            "UpdateSlotsSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(int), typeof(bool) }, null);

        internal static bool IsMediumBook(Spellbook book) => book?.Blueprint?.AssetGuid == MediumBookId;

        internal static bool IsPreparationBook(Spellbook book) => book != null
            && (book.Blueprint?.AssetGuid == ArchmageBookId || book.Blueprint?.AssetGuid == HierophantBookId);

        internal static bool IsRevokedTemporarySpell(Spellbook book, AbilityData spell)
        {
            if (!IsMediumBook(book) || spell == null) return false;
            // Conversion/variant buttons can outlive their source AbilityData too.
            // Inspect its source while leaving unrelated books and permanent spells alone.
            var seen = new List<AbilityData>();
            for (var candidate = spell; candidate != null && !seen.Any(s => ReferenceEquals(s, candidate)); candidate = candidate.ConvertedFrom)
            {
                seen.Add(candidate);
                if (!candidate.IsTemporary || candidate.Spellbook != book) continue;
                int level = candidate.SpellLevelInSpellbook ?? candidate.SpellLevel;
                int castLevel = candidate.SpellLevel;
                if (level < 0 || level > 10 || castLevel < 0 || castLevel > 10) return true;
                // A known copy at the metamagic's resulting circle is not an
                // equivalent source: it may cost more when metamagic is applied.
                bool stillKnown = book.GetKnownSpells(level).Concat(book.GetSpecialSpells(level))
                    .Concat(book.GetCustomSpells(castLevel).Where(custom =>
                        (custom.SpellLevelInSpellbook ?? custom.SpellLevel) == level))
                    .Any(known => known.Blueprint == candidate.Blueprint
                        || candidate.Blueprint.Parent != null && known.Blueprint == candidate.Blueprint.Parent);
                if (!stillKnown) return true;
            }
            return false;
        }

        internal static int MediumLevel(UnitDescriptor owner) => owner?.Progression?.GetClassLevel(
            BlueprintTool.Get<BlueprintCharacterClass>(Guids.Medium)) ?? 0;

        internal static BlueprintSpellbook ActivePreparationBlueprint(UnitDescriptor owner)
            => ActivePreparationBlueprints(owner).FirstOrDefault();

        internal static IEnumerable<BlueprintSpellbook> ActivePreparationBlueprints(UnitDescriptor owner)
        {
            var state = owner?.Unit?.Get<UnitPartMedium>();
            if (state == null || MediumLevel(owner) <= 0) yield break;
            if (owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower))
                <= state.ForgonePowers) yield break;
            var seen = new HashSet<BlueprintGuid>();
            foreach (var reference in state.ActiveSpiritClasses)
            {
                var spirit = reference?.Get();
                if (spirit == null || !seen.Add(spirit.AssetGuid)) continue;
                if (spirit.AssetGuid == ArchmageId)
                    yield return BlueprintTool.Get<BlueprintSpellbook>(Guids.ArchmageSpellbook);
                else if (spirit.AssetGuid == HierophantId)
                    yield return BlueprintTool.Get<BlueprintSpellbook>(Guids.HierophantSpellbook);
            }
        }

        // Read-only: asking for slots must never create a zero-level spirit spellbook.
        internal static bool TryGetProgression(Spellbook book, out BlueprintSpellsTable table, out int level)
        {
            table = null;
            level = 0;
            if (!IsMediumBook(book)) return false;
            var spiritBlueprint = ActivePreparationBlueprint(book.Owner);
            if (spiritBlueprint == null) return false;
            table = spiritBlueprint.SpellsPerDay;
            level = Math.Min(MediumLevel(book.Owner), table.Levels.Length - 1);
            return level > 0;
        }

        internal static Spellbook EnsurePreparationBook(UnitDescriptor owner)
            => EnsurePreparationBooks(owner).FirstOrDefault();

        internal static IReadOnlyList<Spellbook> EnsurePreparationBooks(UnitDescriptor owner)
        {
            var books = new List<Spellbook>();
            foreach (var blueprint in ActivePreparationBlueprints(owner))
                books.Add(InitializePreparationBook(owner, blueprint));
            return books;
        }

        private static Spellbook InitializePreparationBook(UnitDescriptor owner, BlueprintSpellbook blueprint)
        {
            var book = owner.DemandSpellbook(blueprint);
            int level = Math.Min(MediumLevel(owner), blueprint.SpellsPerDay.Levels.Length - 1);
            // Explicit lifecycle initialization, including old saves with a missing/stale book.
            // The native AddSpellbook feature normally does this on level-up.
            while (book.RawBaseLevel < level) book.AddBaseLevel();
            // Native level-up learning starts above the previous maximum (already zero),
            // and UpdateAllSlotsSize starts at level one. Initialize cantrips explicitly
            // so the existing UI can show their list and the one preparation slot.
            foreach (var spell in blueprint.SpellList.GetSpells(0))
                if (!book.IsKnownOnLevel(spell, 0)) book.AddKnown(0, spell);
            UpdatePreparationSlots.Invoke(book, new object[] { 0, false });
            book.UpdateAllSlotsSize(false);
            return book;
        }

        internal static Spellbook MediumBook(UnitDescriptor owner)
        {
            var book = owner.DemandSpellbook(BlueprintTool.Get<BlueprintSpellbook>(Guids.MediumSpellbook));
            // The ordinary Medium table does not open level-one spells before class level 4.
            // A book recovered from an early-level save still needs its actual caster levels.
            int level = Math.Min(20, MediumLevel(owner));
            while (book.RawBaseLevel < level) book.AddBaseLevel();
            return book;
        }

        internal static void RefreshForChannel(UnitDescriptor owner)
        {
            if (ActivePreparationBlueprint(owner) == null) return;
            owner.Unit.Ensure<UnitPartMediumPreparedSpells>().Sync(false);
            MediumBook(owner).Rest();
            NotifyActionBar(owner);
        }

        // Capture before adding an additional spirit. This is an explicit channel action,
        // so repairing a missing/stale main book here is safe; ordinary slot queries stay pure.
        internal static int[] CaptureChannelSlots(UnitDescriptor owner)
        {
            // Null means no capacity expansion is permitted: the character either
            // is not a Medium or already uses the one shared six-circle progression.
            if (MediumLevel(owner) <= 0 || ActivePreparationBlueprint(owner) != null) return null;
            var book = MediumBook(owner);
            var capacity = new int[SpontaneousSlots(book).Length];
            for (int level = 1; level < capacity.Length; level++)
                capacity[level] = book.GetSpellsPerDay(level);
            return capacity;
        }

        internal static void RefreshForAdditionalSpirit(UnitDescriptor owner, int[] previousCapacity)
        {
            if (ActivePreparationBlueprint(owner) == null) return;
            owner.Unit.Ensure<UnitPartMediumPreparedSpells>().Sync(false);
            if (previousCapacity == null)
            {
                ClampRemainingSlots(owner);
                return;
            }
            var book = MediumBook(owner);
            var slots = SpontaneousSlots(book);
            for (int level = 1; level < slots.Length; level++)
            {
                int capacity = Math.Max(0, book.GetSpellsPerDay(level));
                int oldCapacity = level < previousCapacity.Length ? previousCapacity[level] : 0;
                int addedCapacity = Math.Max(0, capacity - oldCapacity);
                // Preserve already spent casts when opening the six-circle progression.
                // A second preparation book shares it and therefore grants no extra pool.
                slots[level] = Math.Max(0, Math.Min(capacity, slots[level] + addedCapacity));
            }
            NotifyActionBar(owner);
        }

        internal static void ClampRemainingSlots(UnitDescriptor owner)
        {
            if (MediumLevel(owner) <= 0) return;
            var book = owner.GetSpellbook(BlueprintTool.Get<BlueprintSpellbook>(Guids.MediumSpellbook));
            if (book == null) return;
            var slots = SpontaneousSlots(book);
            for (int level = 1; level < slots.Length; level++)
                slots[level] = Math.Max(0, Math.Min(slots[level], book.GetSpellsPerDay(level)));
            NotifyActionBar(owner);
        }

        internal static void NotifyActionBar(UnitDescriptor owner)
        {
            // Native AddKnownTemporary only raises ILearnSpellHandler, which the
            // modern ActionBarVM does not listen to. Spellbook.Rest is silent too.
            // Dirty triggers its complete CollectSpells/OnUnitUpdated path, even
            // when the character previously had no visible spell group at all.
            var settings = owner?.Unit?.UISettings;
            if (settings != null && !settings.Dirty) settings.SetDirty();
        }

        internal static void SyncForActionBar(UnitDescriptor owner)
        {
            // A saved preparation selection can finish restoring after an early
            // sync. Reconcile at the actual collection boundary, before Fetch
            // enumerates any books. Never refill slots or invalidate this fetch.
            // Inactive/partially loaded units are read-only here: their earlier
            // grants must not be cleared before the post-load lifecycle finishes.
            if (ActivePreparationBlueprint(owner) != null)
                owner.Unit.Ensure<UnitPartMediumPreparedSpells>().Sync(false);
        }

        internal static void SyncPreparation(Spellbook book)
        {
            if (IsPreparationBook(book)) book.Owner?.Unit?.Ensure<UnitPartMediumPreparedSpells>()?.Sync();
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.GetSpellsPerDay))]
    internal static class MediumSpiritSpellsPerDayPatch
    {
        private static bool Prefix(Spellbook __instance, int __0, ref int __result)
        {
            if (!MediumSpiritSpellbookRules.TryGetProgression(__instance, out var table, out int level)) return true;
            __result = 0;
            if (__0 < 0 || __0 > 6) return false;
            var attribute = __instance.Owner.Stats.GetStat(__instance.Blueprint.CastingAttribute)
                as ModifiableValueAttributeStat;
            if (attribute == null) return false;
            var extras = __instance.Owner.Get<UnitPartExtraSpellsPerDay>()?.BonusSpells;
            int extraSlots = extras != null && __0 < extras.Length ? extras[__0] : 0;
            __result = SpiritSpellcastingMath.DailySlots(table.GetCount(level, __0), __0,
                attribute.ModifiedValue, attribute.CalculatePermanentValueWithoutTempBuffs(),
                attribute.BaseValue, __instance.Owner.IsPlayerFaction, extraSlots);
            return false;
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.GetMaxSpellLevel))]
    internal static class MediumSpiritMaxSpellLevelPatch
    {
        private static void Postfix(Spellbook __instance, ref int __result)
        {
            if (MediumSpiritSpellbookRules.TryGetProgression(__instance, out var table, out int level))
                __result = SpiritSpellcastingMath.MaxSpellLevel(table.Levels[level].Count);
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.GetLastSpellbookLevel))]
    internal static class MediumSpiritLastSpellbookLevelPatch
    {
        private static void Postfix(Spellbook __instance, ref int __result)
        {
            if (MediumSpiritSpellbookRules.TryGetProgression(__instance, out _, out _)) __result = 6;
        }
    }

    [HarmonyPatch(typeof(UIUtilityUnit), nameof(UIUtilityUnit.GetSpellNumberBaseTable))]
    internal static class MediumSpiritBaseSpellNumberTablePatch
    {
        private static bool Prefix(Spellbook __0, ref List<int> __result)
        {
            if (!MediumSpiritSpellbookRules.TryGetProgression(__0, out var table, out int level)) return true;
            __result = new List<int>(7);
            for (int spellLevel = 0; spellLevel <= 6; spellLevel++)
                __result.Add(Math.Max(0, table.GetCount(level, spellLevel)));
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitDescriptor), nameof(UnitDescriptor.ApplyPostLoadFixes))]
    internal static class MediumSpiritRestorePreparedSpellsPatch
    {
        private static void Postfix(UnitDescriptor __instance)
        {
            if (MediumSpiritSpellbookRules.ActivePreparationBlueprint(__instance) != null)
                __instance.Unit.Ensure<UnitPartMediumPreparedSpells>().Sync();
            else
                __instance.Unit.Get<UnitPartMediumPreparedSpells>()?.Clear();
            // Native loading preserves m_SpontaneousSlots. A live daily-capacity
            // query can temporarily miss attribute/extra-slot bonuses here; clamping
            // to it would permanently spend saved casts (even without a spirit).
            // Capacity changes are handled by the explicit channel/end/level-up
            // paths. Loading must neither clamp nor refill the saved pool.
            if (MediumSpiritSpellbookRules.MediumLevel(__instance) > 0)
                MediumSpiritSpellbookRules.NotifyActionBar(__instance);
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.Memorize))]
    internal static class MediumSpiritMemorizePatch
    {
        private static void Postfix(Spellbook __instance, bool __result)
        {
            if (__result) MediumSpiritSpellbookRules.SyncPreparation(__instance);
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.ForgetMemorized))]
    internal static class MediumSpiritForgetPatch
    {
        private static void Postfix(Spellbook __instance) => MediumSpiritSpellbookRules.SyncPreparation(__instance);
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.CanSpend), new Type[] { typeof(BlueprintAbility) })]
    internal static class MediumSpiritPreparationCannotSpendBlueprintPatch
    {
        private static void Postfix(Spellbook __instance, ref bool __result)
        {
            if (MediumSpiritSpellbookRules.IsPreparationBook(__instance)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.CanSpend), new Type[] { typeof(AbilityData), typeof(bool) })]
    internal static class MediumSpiritPreparationCannotSpendAbilityPatch
    {
        private static void Postfix(Spellbook __instance, AbilityData __0, ref bool __result)
        {
            if (MediumSpiritSpellbookRules.IsPreparationBook(__instance)
                || MediumSpiritSpellbookRules.IsRevokedTemporarySpell(__instance, __0)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.Spend), new Type[] { typeof(AbilityData), typeof(bool) })]
    internal static class MediumSpiritPreparationCannotSpendPatch
    {
        private static bool Prefix(Spellbook __instance, AbilityData __0, ref bool __result)
        {
            if (!MediumSpiritSpellbookRules.IsPreparationBook(__instance)
                && !MediumSpiritSpellbookRules.IsRevokedTemporarySpell(__instance, __0)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Spellbook), "SpendInternal")]
    internal static class MediumSpiritPreparationCannotSpendInternalPatch
    {
        private static bool Prefix(Spellbook __instance, AbilityData __1, ref bool __result)
        {
            if (!MediumSpiritSpellbookRules.IsPreparationBook(__instance)
                && !MediumSpiritSpellbookRules.IsRevokedTemporarySpell(__instance, __1)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Spellbook), nameof(Spellbook.GetAvailableForCastSpellCount))]
    internal static class MediumSpiritPreparationHasNoCastsPatch
    {
        private static void Postfix(Spellbook __instance, AbilityData __0, ref int __result)
        {
            if (MediumSpiritSpellbookRules.IsPreparationBook(__instance)
                || MediumSpiritSpellbookRules.IsRevokedTemporarySpell(__instance, __0)) __result = 0;
        }
    }
}
