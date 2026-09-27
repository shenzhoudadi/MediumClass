// Explicit API model for the linked production ApplySpirits component. Feature
// rank attach/detach behavior is based on the installed game's inspected IL.
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using MediumClass.Medium.NewUnitParts;

namespace Kingmaker.Blueprints
{
    public class ComponentNameAttribute(string name) : Attribute { public string Name => name; }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class AllowedOnAttribute(Type type, bool flag) : Attribute { public Type Type => type; public bool Flag => flag; }
    public class AllowMultipleComponentsAttribute : Attribute { }
    public class BlueprintCharacterClassReference
    {
        public BlueprintCharacterClass Blueprint;
        public BlueprintCharacterClass Get() => Blueprint;
    }
    public class BlueprintFeatureReference
    {
        public BlueprintFeature Blueprint;
        public BlueprintFeature Get() => Blueprint;
    }
}
namespace Kingmaker.Blueprints.Classes
{
    public class BlueprintCharacterClass { public string Name; }
    public class BlueprintFeature : Kingmaker.Blueprints.Facts.BlueprintFact
    {
        public int Ranks = 1;
        public List<object> Components = new();
        public IEnumerable<T> GetComponents<T>() => Components.OfType<T>();
    }
}
namespace Kingmaker.Blueprints.JsonSystem
{
    public class TypeIdAttribute(string id) : Attribute { public string Id => id; }
}
namespace BlueprintCore.Utils
{
    public static class BlueprintTool
    {
        public static readonly Dictionary<string, object> Assets = new();
        public static T Get<T>(string id) => (T)Assets[id];
        public static T GetRef<T>(string id)
        {
            object value = typeof(T) == typeof(BlueprintFeatureReference)
                ? new BlueprintFeatureReference { Blueprint = Get<BlueprintFeature>(id) }
                : new BlueprintCharacterClassReference { Blueprint = Get<BlueprintCharacterClass>(id) };
            return (T)value;
        }
    }
}
namespace Kingmaker.UnitLogic
{
    public class UnitPart { public UnitEntityData Owner; }
    public class UnitFactComponentDelegate
    {
        public UnitEntityData Owner;
        public UnitFact Fact;
        public readonly object Runtime = new();
        public object Context;
        public virtual void OnInitialize() { }
        public virtual void OnPostLoad() { }
        public virtual void OnApplyPostLoadFixes() { }
        public virtual void OnTurnOn() { }
        public virtual void OnTurnOff() { }
        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }
        public void ClearData() { }
    }
    public class UnitFact : Kingmaker.EntitySystem.EntityFact { }
    public class UnitFactComponentDelegate<TData> : UnitFactComponentDelegate where TData : class, new()
    {
        private TData savedData;
        public TData Data => savedData ??= new();
        public TData MaybeData => savedData;
        public void LoadData(TData data) => savedData = data;
    }
    public class UnitDescriptor { public UnitEntityData Unit; }
    public class FeatureCollection
    {
        public readonly Dictionary<BlueprintFeature, int> Counts = new();
        public int GetRank(BlueprintFeature feature) => Counts.GetValueOrDefault(feature);
        public int GetRank(BlueprintFeatureReference feature) => GetRank(feature.Get());
        public bool HasFact(BlueprintFeature feature) => GetRank(feature) > 0;
        public void AddFact(BlueprintFeature feature) => Counts[feature] = Math.Min(feature.Ranks, GetRank(feature) + 1);
        // Native PrepareFactForDetach removes one rank; it does not erase the feature.
        public void RemoveFact(BlueprintFeature feature)
        {
            int remaining = GetRank(feature) - 1;
            if (remaining > 0) Counts[feature] = remaining;
            else Counts.Remove(feature);
        }
    }
    public class Progression { public FeatureCollection Features = new(); }
}
namespace Kingmaker.UnitLogic.Buffs
{
    public class BuffBlueprint : Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff { }
    public class Buff : Kingmaker.UnitLogic.UnitFact
    {
        public bool IsActive = true;
        public List<object> Components = new();
        public List<Buff> Stored = new();
        public void StoreFact(Buff buff) => Stored.Add(buff);
        public void CallComponents<T>(Action<T> callback)
        {
            foreach (var component in Components.OfType<T>()) callback(component);
        }
    }
    public class BuffCollection
    {
        public List<Buff> Enumerable = new();
        public void RemoveFact(Buff buff) => Enumerable.Remove(buff);
        public Buff AddBuff(Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff blueprint, object context, TimeSpan duration)
        {
            var buff = new Buff { Blueprint = blueprint };
            Enumerable.Add(buff);
            return buff;
        }
    }
}
namespace Kingmaker.EntitySystem.Entities
{
    public class UnitEntityData
    {
        public Kingmaker.UnitLogic.UnitDescriptor Descriptor;
        public Kingmaker.UnitLogic.Progression Progression = new();
        public Kingmaker.EntitySystem.Stats.Stats Stats = new();
        public Kingmaker.UnitLogic.Buffs.BuffCollection Buffs = new();
        public Kingmaker.EntitySystem.FactCollection Facts = new();
        public int SpiritClassLevel = 20;
        public int Influence = 3;
        public int SlotClamps;
        private readonly Dictionary<Type, Kingmaker.UnitLogic.UnitPart> parts = new();
        public UnitEntityData() { Descriptor = new() { Unit = this }; }
        public T Get<T>() where T : Kingmaker.UnitLogic.UnitPart => (T)parts.GetValueOrDefault(typeof(T));
        public T Ensure<T>() where T : Kingmaker.UnitLogic.UnitPart, new()
        {
            if (Get<T>() is T part) return part;
            var created = new T { Owner = this };
            parts[typeof(T)] = created;
            return created;
        }
        public bool HasFact(BlueprintFeature feature) => Progression.Features.HasFact(feature);
        public bool HasFact(Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff buff) => Buffs.Enumerable.Any(b => b.Blueprint == buff);
        public Kingmaker.EntitySystem.EntityFact AddFact(BlueprintFeature feature)
        {
            Progression.Features.AddFact(feature);
            if (Facts.Get(feature) is {} existing) return existing;
            var fact = new Kingmaker.EntitySystem.EntityFact { Blueprint = feature };
            Facts.Facts.Add(feature, fact);
            return fact;
        }
        public void RemoveFact(BlueprintFeature feature)
        {
            Progression.Features.RemoveFact(feature);
            if (!HasFact(feature) && Facts.Get(feature) is {} fact)
            { fact.IsDisposed = true; Facts.Facts.Remove(feature); }
        }
        public void RemoveFact(Kingmaker.EntitySystem.EntityFact fact)
        {
            if (fact?.Blueprint is BlueprintFeature feature && Facts.Get(feature) == fact) RemoveFact(feature);
        }
    }
}
namespace Kingmaker.PubSubSystem
{
    public interface IUnitReapplyFeaturesOnLevelUpHandler { void HandleUnitReapplyFeaturesOnLevelUp(); }
}
namespace Owlcat.Runtime.Core.Utils
{
    public static class TemporaryLists { public static List<T> ToTempList<T>(this IEnumerable<T> items) => items.ToList(); }
}
namespace UnityModManagerNet
{
    public class UnityModManager
    {
        public class ModEntry { public class ModLogger { public void Log(string text) { } } }
    }
}
namespace MediumClass.Utils
{
    public static class Logging
    {
        public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger GetLogger(string name) => new();
    }
}
namespace MediumClass.Prowler
{
    public static class ProwlerSpiritRules { public static int SpiritClassLevel(UnitEntityData owner) => owner.SpiritClassLevel; }
}
namespace MediumClass.Medium
{
    internal static class MythicSpirits { internal const string MultipleSpiritsGuid = "e7cf953e-cc45-42dd-806e-faf29dcf49da"; }
    internal static class MediumSpiritSpellbookRules
    {
        public static void ClampRemainingSlots(Kingmaker.UnitLogic.UnitDescriptor owner) => owner.Unit.SlotClamps++;
    }
    internal static class MediumInfluenceRules
    {
        public static void Reset(Kingmaker.UnitLogic.UnitDescriptor owner) => owner.Unit.Influence = 0;
    }
}
namespace MediumClass.Medium.NewUnitParts
{
    public class UnitPartMediumPreparedSpells : Kingmaker.UnitLogic.UnitPart
    {
        public int Syncs;
        public int Clears;
        public void Sync() => Syncs++;
        public void Clear() => Clears++;
    }
    public class UnitPartMedium : Kingmaker.UnitLogic.UnitPart
    {
        public readonly Dictionary<BlueprintCharacterClassReference, SpiritEntry> Spirits = new();
        public BlueprintCharacterClassReference PrimarySpirit = new();
        public BlueprintCharacterClassReference SecondarySpirit = new();
        public List<BlueprintCharacterClassReference> AdditionalSpirits = new();
        public int ForgonePowers;
        public IEnumerable<BlueprintCharacterClassReference> ActiveSpiritClasses => new[] { PrimarySpirit }
            .Concat(AdditionalSpirits ?? Enumerable.Empty<BlueprintCharacterClassReference>()).Where(s => s?.Get() != null && Spirits.ContainsKey(s)).Distinct();
        public bool IsActiveSpirit(BlueprintCharacterClassReference spirit) => ActiveSpiritClasses.Contains(spirit);
        public void ClearChannelSelection() { PrimarySpirit = new(); AdditionalSpirits?.Clear(); }
        public class SpiritEntry
        {
            public SpiritStatEntry SpiritBonus = new();
            public SpiritStatEntry SpiritPenalty = new();
            public int SpiritFocus;
            public BlueprintFeatureReference SpiritLesserPower;
            public BlueprintFeatureReference SpiritSeanceBoon;
            public BlueprintFeatureReference SpiritIntermediatePower;
            public BlueprintFeatureReference SpiritIntermediatePowerMove;
            public BlueprintFeatureReference SpiritIntermediatePowerSwift;
            public BlueprintFeatureReference OverwriteIntermediatePower;
            public BlueprintFeatureReference SpiritGreaterPower;
            public BlueprintFeatureReference OverwriteGreaterPower;
            public BlueprintFeatureReference SpiritSupremePower;
        }
        public class SpiritStatEntry
        {
            public Kingmaker.EntitySystem.Stats.StatType[] Stats;
            public BlueprintFeatureReference SpiritBonusFeature;
        }
    }
}
namespace MediumClass.Utilities
{
    public static class Guids
    {
        public const string SpiritPower = "SpiritPower", Archmage = "Archmage", Hierophant = "Hierophant", Trickster = "Trickster",
            AstralBeacon = "AstralBeacon", MediumSpellcasterFeatProhibitArchmage = "ForbidArchmage", MediumSpellcasterFeatProhibitHierophant = "ForbidHierophant",
            MediumSharedSeanceBuff = "SharedSeance", MediumSpiritBonus = "SpiritBonus", MediumSpiritBonusBuff = "SpiritBonusBuff",
            MediumInfluenceDebuff = "InfluenceDebuff", MediumChannelSpirit = "ChannelSpirit", Marshal = "Marshal";
    }
}
// Unused production imports are intentional. They do not replace any executed logic.
namespace Kingmaker
{
    public class Game
    {
        public static Game Instance = new();
        public Player Player = new();
    }
    public class Player { public List<UnitEntityData> ActiveCompanions = new(); }
}
namespace Kingmaker.Blueprints.Classes.Selection { }
namespace Kingmaker.Blueprints.Classes.Spells { }
namespace Kingmaker.Blueprints.Facts { public class BlueprintFact { public string Name; } }
namespace Kingmaker.Designers { }
namespace Kingmaker.EntitySystem
{
    public class EntityFact { public Kingmaker.Blueprints.Facts.BlueprintFact Blueprint; public bool IsDisposed; public bool IsTurnedOn = true; }
    public class FactCollection
    {
        public readonly Dictionary<Kingmaker.Blueprints.Facts.BlueprintFact, EntityFact> Facts = new();
        public EntityFact Get(Kingmaker.Blueprints.Facts.BlueprintFact blueprint) => Facts.GetValueOrDefault(blueprint);
    }
}
namespace Kingmaker.EntitySystem.Stats
{
    public enum StatType { AdditionalAttackBonus, AdditionalDamage, AC, SaveFortitude, SaveWill, Initiative, SkillPerception, SkillKnowledgeArcana }
    public class Stats
    {
        public Dictionary<StatType, Stat> Values = new();
        public Stat GetStat(StatType type)
        {
            if (!Values.TryGetValue(type, out var stat)) Values[type] = stat = new();
            return stat;
        }
    }
    public class Stat
    {
        public List<(int Amount, object Source, Kingmaker.Enums.ModifierDescriptor Descriptor)> Modifiers = new();
        public void AddModifier(int amount, object source, Kingmaker.Enums.ModifierDescriptor descriptor) => Modifiers.Add((amount, source, descriptor));
        public void RemoveModifiersFrom(object source) => Modifiers.RemoveAll(modifier => modifier.Source == source);
    }
}
namespace Kingmaker.Enums { public enum ModifierDescriptor { UntypedStackable, Penalty } }
namespace Kingmaker.QA { }
namespace Kingmaker.UnitLogic.Buffs.Blueprints { public class BlueprintBuff : Kingmaker.Blueprints.Facts.BlueprintFact { } }
namespace Kingmaker.UnitLogic.Mechanics { }
namespace Newtonsoft.Json { public class JsonPropertyAttribute : Attribute { } }
namespace Kingmaker.UnitLogic.Abilities { }
namespace Kingmaker.UnitLogic.Abilities.Blueprints { }
namespace Kingmaker.UnitLogic.FactLogic { public class AddContextStatBonus { public class ComponentData { } } }
namespace Kingmaker.UnitLogic.Parts { }
namespace Kingmaker.Utility { }
namespace Owlcat.QA.Validation { }
namespace TabletopTweaks.Core.NewUnitParts { }
namespace UnityEngine { }
namespace MediumClass.Medium.NewComponents.AbilitySpecific
{
    public class MediumSpiritComponent
    {
        public Kingmaker.EntitySystem.Stats.StatType[] Stats;
        public Kingmaker.EntitySystem.Stats.StatType[] Penalties;
    }
}
namespace BlueprintCore.Blueprints.References
{
    public static class BuffRefs
    {
        public static BuffEntry FightDefensivelyBuff = new();
        public class BuffEntry
        {
            public BuffEntry Reference => this;
            public readonly Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff Blueprint = new() { Name = "FightDefensively" };
            public Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff Get() => Blueprint;
        }
    }
}
