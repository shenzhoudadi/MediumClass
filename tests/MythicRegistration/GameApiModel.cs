// Explicit API doubles. Automatic group population and its GUID de-duplication
// are verified against the shipped BlueprintCore DLL by the test program.
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Prerequisites;

namespace Kingmaker.Blueprints
{
    public readonly record struct BlueprintGuid(Guid Value) { public static BlueprintGuid Parse(string id) => new(Guid.Parse(id)); }
    public class BlueprintScriptableObject { public BlueprintGuid AssetGuid; }
    public class BlueprintCharacterClassReference
    {
        public BlueprintGuid deserializedGuid;
        public BlueprintCharacterClass Get() => BlueprintCore.Utils.BlueprintTool.Get<BlueprintCharacterClass>(deserializedGuid.Value.ToString());
    }
}
namespace Kingmaker.Blueprints.Classes
{
    public enum FeatureGroup { MythicAbility, Feat }
    public class BlueprintCharacterClass : BlueprintScriptableObject { }
    public class BlueprintFeature : BlueprintScriptableObject
    {
        public string DisplayName, Description, Icon;
        public int Ranks = 1;
        public List<FeatureGroup> Groups = new();
        public List<PrerequisiteFeature> Prerequisites = new();
    }
}
namespace Kingmaker.Blueprints.Classes.Selection
{
    public class BlueprintFeatureSelection
    {
        public string Name;
        public FeatureGroup Group;
        public List<BlueprintFeature> AllFeatures = new();
    }
}
namespace Kingmaker.Blueprints.Classes.Prerequisites
{
    public class Prerequisite { public enum GroupType { All, Any } }
    public class PrerequisiteFeature { public string Feature; public Prerequisite.GroupType Group; }
}
namespace BlueprintCore.Utils
{
    public static class BlueprintTool
    {
        public static Dictionary<BlueprintGuid, BlueprintScriptableObject> Assets = new();
        public static T Get<T>(string id) where T : BlueprintScriptableObject =>
            Assets.GetValueOrDefault(BlueprintGuid.Parse(id)) as T;
        public static T GetRef<T>(string id) where T : new()
        {
            var value = new T();
            typeof(T).GetField("deserializedGuid").SetValue(value, BlueprintGuid.Parse(id));
            return value;
        }
    }
}
namespace BlueprintCore.Blueprints.CustomConfigurators.Classes
{
    public class FeatureConfigurator
    {
        public static List<BlueprintFeatureSelection> Selections = new();
        private readonly BlueprintFeature feature;
        private FeatureConfigurator(BlueprintFeature blueprint) { feature = blueprint; }
        public static FeatureConfigurator New(string name, string guid) => new(new BlueprintFeature { AssetGuid = BlueprintGuid.Parse(guid) });
        public FeatureConfigurator SetDisplayName(string value) { feature.DisplayName = value; return this; }
        public FeatureConfigurator SetDescription(string value) { feature.Description = value; return this; }
        public FeatureConfigurator SetIcon(string value) { feature.Icon = value; return this; }
        public FeatureConfigurator AddToGroups(FeatureGroup group) { feature.Groups.Add(group); return this; }
        public FeatureConfigurator AddPrerequisiteFeature(string value, Prerequisite.GroupType group)
        { feature.Prerequisites.Add(new() { Feature = value, Group = group }); return this; }
        public BlueprintFeature Configure()
        {
            BlueprintCore.Utils.BlueprintTool.Assets[feature.AssetGuid] = feature;
            PopulateSelections(feature);
            return feature;
        }
        public static void PopulateSelections(BlueprintFeature feature)
        {
            foreach (var selection in Selections)
                if (feature.Groups.Contains(selection.Group) && selection.AllFeatures.All(f => f.AssetGuid != feature.AssetGuid))
                    selection.AllFeatures.Add(feature);
        }
    }
}
namespace BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection
{
    public class FeatureSelectionConfigurator
    {
        private readonly BlueprintFeatureSelection selection;
        private FeatureSelectionConfigurator(BlueprintFeatureSelection value) { selection = value; }
        public static FeatureSelectionConfigurator For(BlueprintFeatureSelection value) => new(value);
        public FeatureSelectionConfigurator AddToAllFeatures(BlueprintFeature feature)
        { selection.AllFeatures.Add(feature); return this; }
        public void Configure() { }
    }
}
// Minimal channel-state surface also exercises the orphan-buff guard in
// production MultiSpiritRules after the 0.2.3 save-restoration fix.
namespace Kingmaker.UnitLogic { }
namespace Kingmaker.UnitLogic.Buffs.Blueprints { public class BlueprintBuff : BlueprintScriptableObject { } }
namespace Kingmaker.UnitLogic.Abilities
{
    public class AbilityData
    {
        public BlueprintScriptableObject Blueprint;
        public CasterData Caster;
    }
    public class CasterData { public Kingmaker.EntitySystem.Entities.UnitEntityData Unit; }
}
namespace Kingmaker.EntitySystem.Entities
{
    public class UnitEntityData
    {
        public MediumClass.Medium.NewUnitParts.UnitPartMedium State;
        public bool ChannelPresent, MultipleSpirits;
        public T Get<T>() where T : class => State as T;
        public bool HasFact(BlueprintFeature feature) => MultipleSpirits;
        public bool HasFact(Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff buff) => ChannelPresent;
        public Buffs Buffs = new();
    }
    public class Buffs { public void RemoveFact(Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff buff) { } }
}
namespace MediumClass.Medium.NewUnitParts
{
    public class UnitPartMedium
    {
        public Dictionary<BlueprintCharacterClassReference, object> Spirits = new();
        public List<BlueprintCharacterClassReference> ActiveSpiritClasses = new();
        public bool IsActiveSpirit(BlueprintCharacterClassReference spirit) => ActiveSpiritClasses.Contains(spirit);
    }
}
namespace MediumClass.Utils
{
    public static class Logging { public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger GetLogger(string value) => new(); }
}
namespace UnityModManagerNet { public class UnityModManager { public class ModEntry { public class ModLogger { } } } }
namespace MediumClass.Medium.Spirits.Archmage { public static class Archmage { public const string ArchmageName = "Archmage"; } }
