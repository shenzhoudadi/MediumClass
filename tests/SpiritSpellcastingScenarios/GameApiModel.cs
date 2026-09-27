// Minimal API model for executing the linked production patches outside Unity.
// These types model game storage and method contracts; they do not implement spirit switching.
using System.Runtime.CompilerServices;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;

namespace Newtonsoft.Json
{
    public class JsonPropertyAttribute : Attribute { }
    public class JsonIgnoreAttribute : Attribute { }
}

namespace Kingmaker.Blueprints
{
    public readonly record struct BlueprintGuid(Guid Value)
    {
        public static BlueprintGuid Parse(string text) => new(Guid.Parse(text));
    }
    public class Blueprint { public BlueprintGuid AssetGuid; }
    public class BlueprintCharacterClassReference
    {
        public BlueprintCharacterClass Blueprint;
        public BlueprintCharacterClass Get() => Blueprint;
    }
}

namespace BlueprintCore.Utils
{
    public static class BlueprintTool
    {
        public static readonly Dictionary<string, Blueprint> Assets = new();
        public static T Get<T>(string id) where T : Blueprint => (T)Assets[id];
        public static T Register<T>(string id, T blueprint) where T : Blueprint
        {
            blueprint.AssetGuid = BlueprintGuid.Parse(id);
            Assets[id] = blueprint;
            return blueprint;
        }
    }
}

namespace Kingmaker.Blueprints.Classes
{
    public class BlueprintCharacterClass : Blueprint { }
    public class BlueprintFeature : Blueprint { }
}

namespace Kingmaker.Blueprints.Classes.Spells
{
    public class SpellsLevelEntry { public int[] Count; }
    public class BlueprintSpellsTable
    {
        public SpellsLevelEntry[] Levels;
        public int GetCount(int level, int circle) => level >= 0 && level < Levels.Length
            && circle >= 0 && circle < Levels[level].Count.Length ? Levels[level].Count[circle] : -1;
    }
    public class BlueprintSpellbook : Blueprint
    {
        public bool Spontaneous;
        public BlueprintSpellsTable SpellsPerDay;
        public BlueprintSpellsTable SpellSlots;
        public int CastingAttribute;
        public BlueprintSpellList SpellList = new();
    }
    public class BlueprintSpellList
    {
        public readonly Dictionary<int, List<BlueprintAbility>> Spells = new();
        public IEnumerable<BlueprintAbility> GetSpells(int circle) => Spells.GetValueOrDefault(circle) ?? new List<BlueprintAbility>();
    }
}

namespace Kingmaker.EntitySystem.Stats
{
    public class ModifiableValueAttributeStat
    {
        public int ModifiedValue = 18, PermanentValue = 18, BaseValue = 18;
        public int CalculatePermanentValueWithoutTempBuffs() => PermanentValue;
    }
    public class StatsContainer
    {
        public ModifiableValueAttributeStat Charisma = new();
        public object GetStat(int type) => Charisma;
    }
}

namespace Kingmaker.UnitLogic.Abilities.Blueprints
{
    public class BlueprintAbility : Blueprint { public string Name; public BlueprintAbility Parent; }
}

namespace Kingmaker.UnitLogic.Abilities
{
    public class AbilityData
    {
        public BlueprintAbility Blueprint;
        public AbilityData ConvertedFrom;
        public int SpellLevel;
        public int? SpellLevelInSpellbook;
        public bool IsTemporary;
        public Kingmaker.UnitLogic.Spellbook Spellbook;
        public int MetamagicMask;
        public object MetamagicData;
    }
}

namespace Kingmaker.UnitLogic
{
    public class SpellSlot
    {
        public AbilityData SpellShell;
        public int SpellLevel;
    }
    public class UnitPart { public Unit Owner; }
    public class UnitUISettings
    {
        public readonly List<AbilityData> Slots = new();
        public bool Dirty { get; private set; }
        public int DirtyNotifications;
        public event Action ActionBarUpdated;
        public void SetDirty() { Dirty = true; DirtyNotifications++; ActionBarUpdated?.Invoke(); }
        public void TryToInitialize() { Dirty = false; }
        public void RemoveSlot(AbilityData spell) => Slots.RemoveAll(slot => ReferenceEquals(slot, spell));
    }
    public class Unit
    {
        public UnitDescriptor Descriptor;
        public UnitUISettings UISettings = new();
        public event Action MemorizedSpellsChanged;
        public void NotifyMemorizedSpellsChanged() => MemorizedSpellsChanged?.Invoke();
        public event Action<AbilityData> CustomSpellRemoved;
        public void NotifyCustomSpellRemoved(AbilityData spell) => CustomSpellRemoved?.Invoke(spell);
        private readonly Dictionary<Type, UnitPart> parts = new();
        public T Get<T>() where T : UnitPart => parts.TryGetValue(typeof(T), out var part) ? (T)part : null;
        public T Ensure<T>() where T : UnitPart, new()
        {
            if (Get<T>() is T existing) return existing;
            var created = new T { Owner = this };
            parts[typeof(T)] = created;
            return created;
        }
    }
    public class Features
    {
        public int SpiritPowerRank = 1;
        public int GetRank(BlueprintFeature feature) => SpiritPowerRank;
    }
    public class Progression
    {
        public int MediumLevel;
        public Features Features = new();
        public int GetClassLevel(BlueprintCharacterClass blueprint) => MediumLevel;
    }
    public class UnitDescriptor
    {
        public Kingmaker.EntitySystem.Entities.UnitEntityData Unit;
        public Progression Progression = new();
        public Kingmaker.EntitySystem.Stats.StatsContainer Stats = new();
        public bool IsPlayerFaction = true;
        public readonly Dictionary<BlueprintSpellbook, Spellbook> Books = new();
        public int DemandCalls;
        public UnitDescriptor() { Unit = new Kingmaker.EntitySystem.Entities.UnitEntityData { Descriptor = this }; }
        public T Get<T>() where T : UnitPart => Unit.Get<T>();
        public Spellbook GetSpellbook(BlueprintSpellbook blueprint) => Books.GetValueOrDefault(blueprint);
        public Spellbook DemandSpellbook(BlueprintSpellbook blueprint)
        {
            DemandCalls++;
            if (!Books.TryGetValue(blueprint, out var book))
                Books[blueprint] = book = new Spellbook(this, blueprint);
            return book;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ApplyPostLoadFixes() { }
    }
    public class Spellbook
    {
        public readonly BlueprintSpellbook Blueprint;
        public readonly UnitDescriptor Owner;
        public int RawBaseLevel;
        private readonly int[] m_SpontaneousSlots = new int[11];
        private readonly List<AbilityData> known = new();
        private readonly List<AbilityData> custom = new();
        private readonly List<SpellSlot> prepared = new();
        private readonly int[] preparedSlotCounts = new int[11];
        public int SlotsResizeCalls;
        public Spellbook(UnitDescriptor owner, BlueprintSpellbook blueprint) { Owner = owner; Blueprint = blueprint; }
        public int MaxSpellLevel => GetMaxSpellLevel();
        public void AddBaseLevel()
        {
            int previousMax = GetMaxSpellLevel();
            RawBaseLevel++;
            for (int circle = previousMax + 1; circle <= GetMaxSpellLevel(); circle++)
                foreach (var spell in Blueprint.SpellList.GetSpells(circle)) AddKnown(circle, spell);
            UpdateAllSlotsSize(false);
        }
        public void UpdateAllSlotsSize(bool restore)
        {
            SlotsResizeCalls++;
            // Native Spellbook.UpdateAllSlotsSize starts at ONE, leaving cantrips untouched.
            for (int circle = 1; circle <= 10; circle++) UpdateSlotsSize(circle, restore);
            prepared.RemoveAll(slot => slot.SpellLevel > GetMaxSpellLevel());
        }
        private void UpdateSlotsSize(int circle, bool restore) =>
            preparedSlotCounts[circle] = Math.Max(0, Blueprint.SpellSlots?.GetCount(RawBaseLevel, circle) ?? 0);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int GetSpellsPerDay(int circle)
        {
            int count = Blueprint.SpellsPerDay.GetCount(RawBaseLevel, circle);
            var cha = Owner.Stats.Charisma;
            if (count < 0 || Math.Min(cha.ModifiedValue, cha.PermanentValue) < 10 + circle) return 0;
            int modifier = (cha.PermanentValue - 10) / 2;
            return count + (circle > 0 && modifier >= circle ? (modifier - circle) / 4 + 1 : 0);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int GetMaxSpellLevel()
        {
            for (int circle = 10; circle >= 0; circle--)
                if (Blueprint.SpellsPerDay.GetCount(RawBaseLevel, circle) >= 0) return circle;
            return 0;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int GetLastSpellbookLevel() => Blueprint.SpellsPerDay.Levels[^1].Count.Length - 1;
        public void Rest()
        {
            UpdateAllSlotsSize(true);
            for (int circle = 0; circle < m_SpontaneousSlots.Length; circle++)
                m_SpontaneousSlots[circle] = GetSpellsPerDay(circle);
        }
        public int[] SaveRemaining() => (int[])m_SpontaneousSlots.Clone();
        public void LoadRemaining(int[] values) => values.CopyTo(m_SpontaneousSlots, 0);
        public int Remaining(int circle) => m_SpontaneousSlots[circle];
        public IEnumerable<SpellSlot> GetAllMemorizedSpells() => prepared;
        public int PreparationSlots(int circle) => preparedSlotCounts[circle];
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Memorize(AbilityData ability, SpellSlot slot)
        {
            if (PreparationSlots(slot.SpellLevel) < 1 || ability.SpellLevel != slot.SpellLevel) return false;
            if (prepared.Any(item => item != slot && item.SpellLevel == slot.SpellLevel)) return false;
            slot.SpellShell = ability;
            if (!prepared.Contains(slot)) prepared.Add(slot);
            Owner.Unit.NotifyMemorizedSpellsChanged();
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ForgetMemorized(SpellSlot slot) { prepared.Remove(slot); slot.SpellShell = null; Owner.Unit.NotifyMemorizedSpellsChanged(); }
        // The serializer restores field contents directly, without Memorize's UI event.
        public void RestoreMemorized(SpellSlot slot) { if (!prepared.Contains(slot)) prepared.Add(slot); }
        public bool IsKnownOnLevel(BlueprintAbility blueprint, int circle) => known.Any(s => s.Blueprint == blueprint && s.SpellLevel == circle);
        public AbilityData AddKnownTemporary(int circle, BlueprintAbility blueprint)
        {
            var result = known.FirstOrDefault(s => s.SpellLevel == circle && s.Blueprint == blueprint);
            if (result == null) { result = new() { Blueprint = blueprint, SpellLevel = circle, IsTemporary = true, Spellbook = this }; known.Add(result); }
            return result;
        }
        public AbilityData AddKnownPermanent(int circle, BlueprintAbility blueprint)
        {
            var result = AddKnownTemporary(circle, blueprint); result.IsTemporary = false; return result;
        }
        public AbilityData AddKnown(int circle, BlueprintAbility blueprint) => AddKnownPermanent(circle, blueprint);
        public void RemoveTemporarySpell(AbilityData spell) { if (spell.IsTemporary) known.Remove(spell); }
        public AbilityData Known(BlueprintAbility blueprint) => known.FirstOrDefault(s => s.Blueprint == blueprint);
        public IEnumerable<AbilityData> GetKnownSpells(int circle) => known.Where(s => s.SpellLevel == circle);
        public IEnumerable<AbilityData> GetCustomSpells(int circle) => custom.Where(s => s.SpellLevel == circle);
        public void AddCustomSpell(AbilityData spell) => custom.Add(spell);
        public void RemoveCustomSpell(AbilityData spell) { custom.Remove(spell); Owner.Unit.NotifyCustomSpellRemoved(spell); }
        public IEnumerable<AbilityData> GetSpecialSpells(int circle) => Array.Empty<AbilityData>();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool CanSpend(BlueprintAbility blueprint) => known.FirstOrDefault(s => s.Blueprint == blueprint) is AbilityData spell && CanSpend(spell, true);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool CanSpend(AbilityData spell, bool allowSpontaneousConversion) => spell.SpellLevel == 0
            || (spell.SpellLevel <= MaxSpellLevel && GetSpellsPerDay(spell.SpellLevel) > 0 && Remaining(spell.SpellLevel) > 0);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Spend(AbilityData spell, bool allowSpontaneousConversion) => SpendInternal(spell.Blueprint, spell, true, allowSpontaneousConversion);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool SpendInternal(BlueprintAbility blueprint, AbilityData spell, bool spend, bool excludeSpecial)
        {
            // Native SpendInternal checks the pool, not whether cached AbilityData is
            // still known. Do not call patched CanSpend, which would mask a missing guard.
            if (spell.SpellLevel != 0 && (spell.SpellLevel > MaxSpellLevel
                || GetSpellsPerDay(spell.SpellLevel) <= 0 || Remaining(spell.SpellLevel) <= 0)) return false;
            if (spend && spell.SpellLevel > 0) m_SpontaneousSlots[spell.SpellLevel]--;
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int GetAvailableForCastSpellCount(AbilityData spell) => Remaining(spell.SpellLevel);
    }
}

namespace Kingmaker.EntitySystem.Entities
{
    public class UnitEntityData : Kingmaker.UnitLogic.Unit { }
}

namespace Kingmaker.UnitLogic.Parts
{
    public class UnitPartExtraSpellsPerDay : Kingmaker.UnitLogic.UnitPart { public int[] BonusSpells = new int[11]; }
}
namespace Kingmaker.UI.Common
{
    public static class UIUtilityUnit
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<int> GetSpellNumberBaseTable(Kingmaker.UnitLogic.Spellbook book) =>
            Enumerable.Range(0, 7).Select(circle => Math.Max(0, book.Blueprint.SpellsPerDay.GetCount(book.RawBaseLevel, circle))).ToList();
    }
}
namespace MediumClass.Medium.NewUnitParts
{
    public class UnitPartMedium : Kingmaker.UnitLogic.UnitPart
    {
        public BlueprintCharacterClassReference PrimarySpirit;
        public readonly List<BlueprintCharacterClassReference> AdditionalSpirits = new();
        public IEnumerable<BlueprintCharacterClassReference> ActiveSpiritClasses =>
            (PrimarySpirit == null ? Enumerable.Empty<BlueprintCharacterClassReference>() : new[] { PrimarySpirit })
                .Concat(AdditionalSpirits);
        public int ForgonePowers;
    }
}
namespace Kingmaker.UI.MVVM._VM.ActionBar
{
    public static class ActionBarSpellbookHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<AbilityData> Fetch(Kingmaker.EntitySystem.Entities.UnitEntityData unit)
        {
            var abilities = new List<AbilityData>();
            var memorized = new List<Kingmaker.UnitLogic.SpellSlot>();
            // Native Fetch enumerates all actual books and their runtime MaxSpellLevel.
            // LearnSpell notifications and Rest do not refresh its cached consumer.
            foreach (var book in unit.Descriptor.Books.Values)
                for (int circle = book.MaxSpellLevel; circle >= 0; circle--)
                    if (book.Blueprint.Spontaneous || circle < 1)
                        foreach (var ability in book.GetSpecialSpells(circle).Concat(book.GetKnownSpells(circle)).Concat(book.GetCustomSpells(circle)))
                            TryAddAbility(abilities, ability);
                    else
                        foreach (var slot in book.GetAllMemorizedSpells().Where(slot => slot.SpellLevel == circle && slot.SpellShell != null))
                            TryAddSpell(memorized, slot);
            return abilities.Concat(memorized.Select(slot => slot.SpellShell)).ToList();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TryAddAbility(List<AbilityData> list, AbilityData item)
        {
            if (!list.Any(existing => existing.Blueprint == item.Blueprint
                && existing.SpellLevel == item.SpellLevel && existing.MetamagicMask == item.MetamagicMask))
                list.Add(item);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TryAddSpell(List<Kingmaker.UnitLogic.SpellSlot> list, Kingmaker.UnitLogic.SpellSlot item)
        {
            // Native memorized entries have a separate insertion path. Do not call
            // TryAddAbility here: that would conceal a missing memorized-slot patch.
            var spell = item.SpellShell;
            if (!list.Any(existing => existing.SpellShell.Blueprint == spell.Blueprint
                && existing.SpellLevel == spell.SpellLevel && existing.SpellShell.MetamagicMask == spell.MetamagicMask))
                list.Add(item);
        }
    }
}
namespace MediumClass.Utilities
{
    public static class Guids
    {
        public const string Medium = "b11b2e0b-3076-4b9c-bbf3-ca7f851b5bb4";
        public const string MediumSpellbook = "18218284-8e8e-40c8-b029-adfa64eba37f";
        public const string SpiritPower = "8de7a6b4-a076-4e68-bb45-a397ba4455dc";
        public const string Archmage = "039af4ae-2b4e-44d5-addd-67ba35eae478";
        public const string ArchmageSpellbook = "b2e275c3-acfa-4b47-aeac-8163172474de";
        public const string Hierophant = "c600360a-ed01-4117-a97a-5c52737a846f";
        public const string HierophantSpellbook = "b5aea57f-0587-4a59-a35c-5efa3a730811";
    }
}
