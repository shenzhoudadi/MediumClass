// Explicit game API doubles. This suite is a mechanics/patch-contract test, not Unity gameplay.
using System.Runtime.CompilerServices;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
namespace Newtonsoft.Json { public sealed class JsonPropertyAttribute:Attribute{} }
namespace Kingmaker.Blueprints.JsonSystem { public sealed class TypeIdAttribute:Attribute {public TypeIdAttribute(string value){}} }
namespace Kingmaker.Blueprints {
    public readonly struct BlueprintGuid { public readonly Guid Value; public BlueprintGuid(Guid value){Value=value;} public static BlueprintGuid Parse(string text)=>new(Guid.Parse(text)); public static bool operator==(BlueprintGuid a, BlueprintGuid b)=>a.Value==b.Value; public static bool operator!=(BlueprintGuid a, BlueprintGuid b)=>a.Value!=b.Value; public override bool Equals(object o)=>o is BlueprintGuid b && b==this; public override int GetHashCode()=>Value.GetHashCode(); }
    public class BlueprintScriptableObject {
        public BlueprintGuid AssetGuid;
        public Dictionary<Type,object> Components=new();
        public T GetComponent<T>() where T:class=>Components.TryGetValue(typeof(T),out var c)?c as T:null;
    }
    public class BlueprintAbilityResource:BlueprintScriptableObject {
        [MethodImpl(MethodImplOptions.NoInlining)] public int GetMaxAmount(UnitDescriptor unit)=>5;
    }
}
namespace Kingmaker.Blueprints.Classes {public class BlueprintFeature:BlueprintScriptableObject{} }
namespace Kingmaker.UnitLogic.Buffs.Blueprints {public class BlueprintBuff:BlueprintScriptableObject{} }
namespace BlueprintCore.Utils {
    public static class BlueprintTool {
        public static Dictionary<string,BlueprintScriptableObject> Assets=new();
        public static T Get<T>(string key) where T:BlueprintScriptableObject => (T)Assets[key];
        public static T Register<T>(string key,T b) where T:BlueprintScriptableObject {b.AssetGuid=BlueprintGuid.Parse(key);Assets[key]=b;return b;}
    }
}
namespace Kingmaker.EntitySystem.Entities {
    public class UnitEntityData {
        public UnitDescriptor Descriptor;
        readonly Dictionary<Type,UnitPart> parts=new();
        public T Get<T>() where T:UnitPart=>parts.TryGetValue(typeof(T),out var p)?(T)p:null;
        public T Ensure<T>() where T:UnitPart,new(){if(Get<T>() is T p)return p;var n=new T{Owner=this};parts[typeof(T)]=n;return n;}
    }
}
namespace Kingmaker.UnitLogic {
    public class UnitPart {public Kingmaker.EntitySystem.Entities.UnitEntityData Owner;}
    public class Progression {public int MythicLevel;}
    public class UnitDescriptor {
        public Kingmaker.EntitySystem.Entities.UnitEntityData Unit;
        public UnitAbilityResourceCollection Resources;
        public Progression Progression=new(); public BuffCollection Buffs=new();
        public HashSet<BlueprintScriptableObject> Facts=new();
        public bool HasFact(BlueprintScriptableObject fact)=>Facts.Contains(fact)||Buffs.Active.Contains(fact);
        public UnitDescriptor(){Unit=new(){Descriptor=this};Resources=new(this);}
        [MethodImpl(MethodImplOptions.NoInlining)] public void ApplyPostLoadFixes(){}
    }
    public class BuffCollection {
        public HashSet<BlueprintScriptableObject> Active=new();
        public void RemoveFact(BlueprintScriptableObject b)=>Active.Remove(b);
        public void AddBuff(BlueprintScriptableObject b,Kingmaker.EntitySystem.Entities.UnitEntityData u,TimeSpan duration)=>Active.Add(b);
    }
    public class UnitAbilityResource {public int Amount;public BlueprintScriptableObject Blueprint;}
    public class UnitAbilityResourceCollection {
        private UnitDescriptor m_Owner;
        private Dictionary<BlueprintScriptableObject,UnitAbilityResource> resources=new();
        public IEnumerable<BlueprintScriptableObject> Enumerable=>resources.Keys;
        public UnitAbilityResourceCollection(UnitDescriptor owner){m_Owner=owner;}
        public UnitAbilityResource GetResource(BlueprintScriptableObject b)=>resources.TryGetValue(b,out var r)?r:null;
        [MethodImpl(MethodImplOptions.NoInlining)] public void Add(BlueprintScriptableObject blueprint,bool restore){
            if(!resources.ContainsKey(blueprint))resources[blueprint]=new(){Blueprint=blueprint};
            if(restore)resources[blueprint].Amount=((BlueprintAbilityResource)blueprint).GetMaxAmount(m_Owner);
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool HasEnoughResource(BlueprintScriptableObject blueprint,int amount)=>GetResource(blueprint)?.Amount>=amount;
        [MethodImpl(MethodImplOptions.NoInlining)] public bool HasMaxAmount(BlueprintScriptableObject blueprint)=>GetResource(blueprint)?.Amount>=((BlueprintAbilityResource)blueprint).GetMaxAmount(m_Owner);
        [MethodImpl(MethodImplOptions.NoInlining)] public void Spend(BlueprintScriptableObject blueprint,int amount){GetResource(blueprint).Amount-=amount;}
        [MethodImpl(MethodImplOptions.NoInlining)] public void Restore(BlueprintScriptableObject blueprint,int amount,bool full){GetResource(blueprint).Amount=full?((BlueprintAbilityResource)blueprint).GetMaxAmount(m_Owner):GetResource(blueprint).Amount+amount;}
    }
}
namespace Kingmaker.UnitLogic.Abilities {
    public class AbilityData {
        public BlueprintScriptableObject Blueprint;public UnitDescriptor Caster;public AbilityResourceLogic ResourceLogic;
        public BlueprintScriptableObject RequiredResource=>ResourceLogic?.RequiredResource;
    }
    public static class AbilityCastRateUtils {
        [MethodImpl(MethodImplOptions.NoInlining)] public static int GetAvailableCastsCountFromResources(AbilityData ability)=>ability.Caster.Resources.GetResource(ability.RequiredResource).Amount / Math.Max(1,ability.ResourceLogic.CalculateCost(ability));
    }
}
namespace Kingmaker.UnitLogic.Abilities.Components {
    public class AbilityEffectRunAction {public ActionList Actions=new();}
    public class ActionList {public object[] Actions=Array.Empty<object>();}
    public class AbilityResourceLogic {
        public BlueprintAbilityResource RequiredResource;public bool IsSpendResource=true;public int Amount=1;
        [MethodImpl(MethodImplOptions.NoInlining)] public int CalculateCost(AbilityData ability)=>Amount;
        [MethodImpl(MethodImplOptions.NoInlining)] public void Spend(AbilityData ability){ability.Caster.Resources.Spend(RequiredResource,CalculateCost(ability));}
    }
}
namespace Kingmaker.UI.UnitSettings {
    public class MechanicActionBarSlot {
        [MethodImpl(MethodImplOptions.NoInlining)] public virtual string GetCountText(int count)=>count.ToString();
    }
    public class MechanicActionBarSlotSpell:MechanicActionBarSlot {
        public AbilityData Spell; public int GetResource()=>AbilityCastRateUtils.GetAvailableCastsCountFromResources(Spell);
    }
    public class MechanicActionBarSlotAbility:MechanicActionBarSlot {
        public AbilityData Ability;
        [MethodImpl(MethodImplOptions.NoInlining)] public int GetResource()=>AbilityCastRateUtils.GetAvailableCastsCountFromResources(Ability);
    }
}
namespace Kingmaker.PubSubSystem {
    public interface IUnitAbilityResourceHandler {void HandleAbilityResourceChange(Kingmaker.EntitySystem.Entities.UnitEntityData unit,UnitAbilityResource resource,int old);}
    public static class EventBus {public static void RaiseEvent<T>(Action<T> action){} }
}
namespace MediumClass.Medium.NewActions { public class ContextActionApplySpirit{} }
namespace MediumClass.Medium.NewUnitParts { public class UnitPartMedium:UnitPart {
    public List<string> ActiveSpiritClasses=new();public int FreeSurgeAmount;public int ForgonePowers;
    public void ResetDailySurges()=>FreeSurgeAmount=InfluenceMath.DailyFreeSurges(ForgonePowers,
        Owner.Descriptor.HasFact(BlueprintCore.Utils.BlueprintTool.Get<Kingmaker.Blueprints.Classes.BlueprintFeature>(MediumClass.Utilities.Guids.MediumSpiritMastery)));
} }
namespace MediumClass.Medium {
    internal static class HardenedSoul { internal const string FeatureGuid="28224e93-7791-4385-bc98-5bd6e1c4f56d"; internal static bool HasFeature(UnitDescriptor owner)=>owner?.HasFact(BlueprintCore.Utils.BlueprintTool.Get<Kingmaker.Blueprints.Classes.BlueprintFeature>(FeatureGuid))==true; }
    internal static class MythicInfluence {internal const string FeatureGuid="ae859d6d-ffb3-4ba4-93c7-f6458f02db1e";}
    internal static class MultiSpiritRules {internal static int NextChannelCost(Kingmaker.EntitySystem.Entities.UnitEntityData owner)=>Math.Max(1,owner.Get<NewUnitParts.UnitPartMedium>()?.ActiveSpiritClasses.Count ?? 0);}
}




