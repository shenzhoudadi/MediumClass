// Narrow, explicit doubles for stat modification and the target/caster relationship.
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic;
namespace Kingmaker.Blueprints.JsonSystem { public class TypeIdAttribute:Attribute {public TypeIdAttribute(string s){}} }
namespace Kingmaker.Blueprints {
    public class BlueprintScriptableObject { public object[] Components=Array.Empty<object>();public IEnumerable<T> GetComponents<T>()=>Components.OfType<T>(); }
    public class BlueprintCharacterClassReference {public BlueprintCharacterClass Value;public BlueprintCharacterClass Get()=>Value;}
    public class BlueprintFeatureReference {public BlueprintFeature Value;public BlueprintFeature Get()=>Value;}
}
namespace Kingmaker.Blueprints.Classes {public class BlueprintFeature:BlueprintScriptableObject {}public class BlueprintCharacterClass:BlueprintScriptableObject{}}
namespace Kingmaker.UnitLogic.Abilities.Blueprints {public class BlueprintAbility:BlueprintScriptableObject{}}
namespace BlueprintCore.Utils {
    public static class BlueprintTool {
        public static readonly Dictionary<string,object> Assets=new();
        public static T Get<T>(string id)=>(T)Assets[id];
        public static T GetRef<T>(string id)=>(T)Assets[id+".ref"];
    }
}
namespace Kingmaker.EntitySystem {public class EntityFactComponent {public IDisposable RequestEventContext()=>null;} }
namespace Kingmaker.EntitySystem.Stats {
    public enum StatType {AdditionalAttackBonus,AdditionalDamage,Initiative,SaveWill,SkillPersuasion}
    public class Stat {
        public readonly Dictionary<object,int> Modifiers=new();
        public int Total=>Modifiers.Values.Sum();
        public void AddModifier(int amount,object source,Kingmaker.Enums.ModifierDescriptor descriptor)=>Modifiers[source]=amount;
        public void RemoveModifiersFrom(object source)=>Modifiers.Remove(source);
    }
    public class Stats {private readonly Dictionary<StatType,Stat> values=new();public Stat GetStat(StatType type){if(!values.TryGetValue(type,out var stat))values[type]=stat=new();return stat;} }
}
namespace Kingmaker.Enums {public enum ModifierDescriptor{UntypedStackable}}
namespace Kingmaker.UnitLogic {
    public class UnitDescriptor {public UnitEntityData Unit;public bool Prowler,Hardened;}
    public class UnitPart {public UnitEntityData Owner;}
    public class FeatureCollection {public readonly Dictionary<BlueprintFeature,int> Ranks=new();public int GetRank(BlueprintFeature feature)=>Ranks.TryGetValue(feature,out int r)?r:0;}
    public class Progression {public FeatureCollection Features=new();}
    public class Context {public UnitEntityData MaybeCaster;public Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility SourceAbility;}
    public class UnitFactComponentDelegate {
        public UnitEntityData Owner;public Context Context;public object Runtime=new();
        public virtual void OnTurnOn(){}public virtual void OnTurnOff(){}
    }
}
namespace Kingmaker.EntitySystem.Entities {
    public class UnitEntityData {
        readonly Dictionary<Type,UnitPart> parts=new();public Kingmaker.EntitySystem.Stats.Stats Stats=new();public Progression Progression=new();public int Level=20;
        public UnitDescriptor Descriptor;
        public UnitEntityData(){Descriptor=new(){Unit=this};}
        public T Get<T>() where T:UnitPart=>parts.TryGetValue(typeof(T),out var v)?(T)v:null;
        public T Ensure<T>()where T:UnitPart,new(){if(Get<T>()is T p)return p;var value=new T{Owner=this};parts[typeof(T)]=value;return value;}
    }
}
namespace Kingmaker.Designers.Mechanics.Facts {public interface IConcentrationBonusProvider{int GetStaticConcentrationBonus(Kingmaker.EntitySystem.EntityFactComponent runtime);}}
namespace MediumClass.Medium.NewUnitParts {
    public class UnitPartMedium:UnitPart {
        public readonly Dictionary<BlueprintCharacterClassReference,SpiritEntry> Spirits=new();
        public readonly List<BlueprintCharacterClassReference> ActiveSpiritClasses=new();
        public bool IsActiveSpirit(BlueprintCharacterClassReference r)=>ActiveSpiritClasses.Contains(r);
        public class SpiritEntry {public SpiritStatEntry SpiritBonus=new();public int SpiritFocus;}
        public class SpiritStatEntry {public StatType[] Stats;public bool Concentration;public BlueprintFeatureReference SpiritBonusFeature;}
    }
}
namespace MediumClass.Medium.NewComponents.AbilitySpecific {public class MediumSpiritComponent{public StatType[] Stats;}}
namespace MediumClass.Medium {public static class HardenedSoul{public static bool HasFeature(UnitDescriptor unit)=>unit?.Hardened==true;}}
namespace MediumClass.Prowler {
    public static class ProwlerSpiritRules {
        public static bool IsProwler(UnitDescriptor unit)=>unit?.Prowler==true;
        public static int SpiritClassLevel(UnitEntityData unit)=>unit?.Level??0;
    }
}
namespace UnityModManagerNet {public class UnityModManager {public class ModEntry {public class ModLogger {}}}}
namespace MediumClass.Utils {public static class Logging {public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger GetLogger(string s)=>new();}}
namespace MediumClass.Medium.Spirits.Archmage {public static class Archmage {public const string ArchmageName="Archmage";}}
