// Explicit API doubles. The test also checks these contracts against the installed game DLL.
namespace Kingmaker.Blueprints.JsonSystem {public class TypeIdAttribute:Attribute{public TypeIdAttribute(string s){}}}
namespace Kingmaker.UnitLogic {public class UnitFactComponentDelegate{public object Fact=new();}}
namespace Kingmaker.PubSubSystem {
    public interface IInitiatorRulebookHandler<T>{}
    public interface IRulebookHandler<T>{}
    public interface ISubscriber{}
    public interface IInitiatorRulebookSubscriber{}
}
namespace Kingmaker.RuleSystem {
    public class BlueprintAbility {public bool IsSpell=true;}
    public class Context {public BlueprintAbility SourceAbility;}
    public class Reason {public Context Context;}
    public class RulebookEvent {
        public class CustomDataKey{public CustomDataKey(string name){}}
        public Reason Reason;
        private readonly Dictionary<CustomDataKey,object> data=new();
        public bool TryGetCustomData<T>(CustomDataKey key,out T value){if(data.TryGetValue(key,out var v)&&v is T typed){value=typed;return true;}value=default;return false;}
        public void SetCustomData<T>(CustomDataKey key,T value)=>data[key]=value;
    }
}
namespace Kingmaker.RuleSystem.Rules.Damage {
    public class DiceFormula {public int Rolls;}
    public class DiceFormulaValue {public DiceFormula ModifiedValue=new();}
    public class BaseDamage {
        public bool Precision,IgnoreModifiers;
        public DiceFormulaValue Dice=new();
        public int Bonus,BonusTargetRelated;public int? PreRolledValue;
        public int TotalBonus=>Bonus+BonusTargetRelated;
        public List<(int Amount,object Source)> Modifiers=new();
        public void AddModifier(int bonus,object fact){Modifiers.Add((bonus,fact));Bonus+=bonus;}
    }
    public class DamageBundle:List<BaseDamage> {public object Weapon;}
    public class RuleDealDamage:RulebookEvent {public BlueprintAbility SourceAbility;public DamageBundle DamageBundle=new();}
    public class RuleCalculateDamage:RulebookEvent {
        public RuleDealDamage ParentRule;
        public DamageBundle DamageBundle=>ParentRule.DamageBundle;
    }
}
