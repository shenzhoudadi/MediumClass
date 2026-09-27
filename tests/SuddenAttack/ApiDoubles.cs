// Only the API surface needed by the two linked production files is modeled.
// The pool merger itself is imported from installed game IL in NativeAttackPool.cs.
using Kingmaker.Blueprints.Facts;
using static Kingmaker.UnitLogic.FactLogic.AddMechanicsFeature;

namespace Kingmaker.Blueprints
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class AllowedOnAttribute : Attribute
    { public AllowedOnAttribute(Type type, bool value) { } }
}
namespace Kingmaker.Blueprints.JsonSystem
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TypeIdAttribute : Attribute
    { public TypeIdAttribute(string id) { } }
}
namespace Kingmaker.Blueprints.Facts
{
    public class BlueprintUnitFact { public string Id; }
}
namespace Kingmaker.PubSubSystem
{
    public interface IInitiatorRulebookHandler<T> { }
    public interface IRulebookHandler<T> { }
    public interface ISubscriber { }
    public interface IInitiatorRulebookSubscriber { }
}
namespace Kingmaker.UnitLogic
{
    public class UnitFactComponentDelegate { }
    public class UnitState { public bool IsCharging; }
    public class TestUnit
    {
        public readonly UnitState State = new();
        public readonly HashSet<string> Facts = new();
        public bool HasFact(BlueprintUnitFact fact) => Facts.Contains(fact.Id);
    }
}
namespace Kingmaker.Utility { public class NamespaceAnchor { } }
namespace Kingmaker.UnitLogic.FactLogic
{
    public class AddMechanicsFeature { public enum MechanicsFeatureType { SuppressedManyshot } }
}
namespace Kingmaker.RuleSystem.Rules
{
    public class AttackPool
    {
        public int HasteAttacks;
        public int PenalizedAttacks;
        public int AdditionalAttacks;
        public int Total => HasteAttacks + PenalizedAttacks + AdditionalAttacks;
    }
    public class RuleCalculateAttacksCount
    {
        public class RuleReason { public Kingmaker.UnitLogic.TestUnit Caster; }
        public readonly Kingmaker.UnitLogic.TestUnit Initiator = new();
        public readonly RuleReason Reason;
        public readonly AttackPool PrimaryHand = new();
        public readonly List<(int number, bool haste, bool penalized, object weapon)> Calls = new();
        public RuleCalculateAttacksCount() { Reason = new() { Caster = Initiator }; }
        public void AddExtraAttacks(int number, bool haste, bool penalized, object weapon)
        {
            if (weapon != null) throw new NotSupportedException("This harness only models the ordinary primary-hand, weapon=null route.");
            Calls.Add((number, haste, penalized, weapon));
            NativeAttackPool.Merge(PrimaryHand, number, haste, penalized);
        }
    }
}
namespace BlueprintCore.Blueprints.References
{
    public static class FeatureRefs
    {
        public const string FlurryOfBlows = "FlurryOfBlows";
        public const string FlurryOfBlowsLevel11 = "FlurryOfBlowsLevel11";
    }
}
namespace BlueprintCore.Utils
{
    public static class BlueprintTool
    { public static T Get<T>(string id) where T : BlueprintUnitFact, new() => new() { Id = id }; }
}
namespace UnityModManagerNet
{
    public static class UnityModManager
    { public class ModEntry { public class ModLogger { public void Log(string text) { } } } }
}
namespace MediumClass.Utils
{
    public static class Logging
    { public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger GetLogger(string name) => new(); }
}
namespace MediumClass.Utilities
{
    public static class Guids { public const string ChampionSuddenAttack = "ChampionSuddenAttack"; }
}
namespace BlueprintCore.Blueprints.CustomConfigurators.Classes
{
    public sealed class FeatureConfigurator
    {
        public static FeatureConfigurator Last;
        public string Name, Guid, DisplayName, Description;
        public readonly List<object> Components = new();
        public readonly List<MechanicsFeatureType> Mechanics = new();
        public bool Configured;
        public static FeatureConfigurator New(string name, string guid) => Last = new() { Name = name, Guid = guid };
        public FeatureConfigurator SetDisplayName(string text) { DisplayName = text; return this; }
        public FeatureConfigurator SetDescription(string text) { Description = text; return this; }
        public FeatureConfigurator AddComponent<T>(Action<T> configure) where T : new()
        { var component = new T(); configure(component); Components.Add(component); return this; }
        public FeatureConfigurator AddMechanicsFeature(MechanicsFeatureType feature) { Mechanics.Add(feature); return this; }
        public void Configure() { Configured = true; }
    }
}
