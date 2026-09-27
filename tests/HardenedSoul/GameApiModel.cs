// Minimal API model for executing the optional-integration production code.
// No Unity or real BlueprintCore initialization is performed by these doubles.
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
namespace Kingmaker.Blueprints
{
 public readonly record struct BlueprintGuid(Guid Value) {public static BlueprintGuid Parse(string s)=>new(Guid.Parse(s));}
 public class BlueprintScriptableObject {public BlueprintGuid AssetGuid;public List<object> Components=new();}
 public class BlueprintFeatureBaseReference {public BlueprintGuid deserializedGuid;}
 public class BlueprintFeatureReference:BlueprintFeatureBaseReference {}
 public class BlueprintCharacterClassReference {public BlueprintGuid deserializedGuid;}
 public static class Extensions { public static T ToReference<T>(this BlueprintScriptableObject b) where T:BlueprintFeatureBaseReference,new()=>new(){deserializedGuid=b.AssetGuid}; }
 public static class ResourcesLibrary {public static Dictionary<BlueprintGuid,BlueprintScriptableObject> Assets=new();public static T TryGetBlueprint<T>(BlueprintGuid id)where T:BlueprintScriptableObject=>Assets.TryGetValue(id,out var b)?b as T:null;}
}
namespace Kingmaker.Blueprints.Classes
{
 public class BlueprintFeature:BlueprintScriptableObject {public string Name,Description; public bool Reapply,ClassFeature;public int Ranks;}
 public class BlueprintCharacterClass:BlueprintScriptableObject {public BlueprintProgression Progression;public BlueprintArchetype[] Archetypes=Array.Empty<BlueprintArchetype>();}
 public class BlueprintArchetype {public LevelEntry[] RemoveFeatures;}
 public class BlueprintProgression:BlueprintFeature {public LevelEntry[] LevelEntries;public UIGroup[] UIGroups;public BlueprintFeatureBaseReference[] m_UIDeterminatorsGroup;}
 public class LevelEntry {public int Level;public List<BlueprintFeatureBaseReference> m_Features=new();}
 public class UIGroup {public List<BlueprintFeatureBaseReference> m_Features=new();}
}
namespace Kingmaker.Blueprints.Classes.Selection {public class BlueprintFeatureSelection:BlueprintFeature {public BlueprintFeatureReference[] m_Features,m_AllFeatures;public bool IgnorePrerequisites;}}
namespace Kingmaker.Blueprints.Classes.Prerequisites
{
 public class Prerequisite {public bool CheckInProgression,HideInUI;}
 public class PrerequisiteClassLevel:Prerequisite {public BlueprintCharacterClassReference m_CharacterClass;public int Level;}
 public class PrerequisiteNoFeature:Prerequisite {public BlueprintFeatureReference m_Feature;}
}
namespace Kingmaker.UnitLogic {public class UnitDescriptor {public HashSet<BlueprintGuid> Facts=new();public bool HasFact(BlueprintFeature f)=>Facts.Contains(f.AssetGuid);}}
namespace BlueprintCore.Utils
{
 public static class BlueprintTool
 {
  public static T GetRef<T>(string s)where T:new()
  {
   var v=new T();typeof(T).GetField("deserializedGuid").SetValue(v,BlueprintGuid.Parse(s));return v;
  }
 }
}
namespace BlueprintCore.Blueprints.CustomConfigurators.Classes
{
 public class FeatureConfigurator
 {
  protected BlueprintFeature bp;
  protected FeatureConfigurator(BlueprintFeature feature){bp=feature;}
  public static FeatureConfigurator New(string name,string guid)=>new(new BlueprintFeature{AssetGuid=BlueprintGuid.Parse(guid)});
  public FeatureConfigurator SetDisplayName(string s){bp.Name=s;return this;}
  public FeatureConfigurator SetDescription(string s){bp.Description=s;return this;}
  public FeatureConfigurator SetIcon(string s)=>this;
  public FeatureConfigurator SetIsClassFeature(bool b){bp.ClassFeature=b;return this;}
  public FeatureConfigurator SetRanks(int n){bp.Ranks=n;return this;}
  public FeatureConfigurator SetIgnorePrerequisites(bool b){((BlueprintFeatureSelection)bp).IgnorePrerequisites=b;return this;}
  public FeatureConfigurator SetReapplyOnLevelUp(bool b){bp.Reapply=b;return this;}
  public FeatureConfigurator AddComponent<T>(Action<T> init)where T:new(){var c=new T();init(c);bp.Components.Add(c);return this;}
  public FeatureConfigurator AddToAllFeatures(params string[] ids){var s=(BlueprintFeatureSelection)bp;s.m_AllFeatures=s.m_Features=ids.Select(id=>new BlueprintFeatureReference{deserializedGuid=BlueprintGuid.Parse(id)}).ToArray();return this;}
  public BlueprintFeature Configure(){ResourcesLibrary.Assets[bp.AssetGuid]=bp;return bp;}
 }
}
namespace BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection
{
 public class FeatureSelectionConfigurator:BlueprintCore.Blueprints.CustomConfigurators.Classes.FeatureConfigurator
 {
  private FeatureSelectionConfigurator(BlueprintFeatureSelection b):base(b){}
  public new static FeatureSelectionConfigurator New(string name,string guid)=>new(new BlueprintFeatureSelection{AssetGuid=BlueprintGuid.Parse(guid)});
 }
}
namespace MediumClass.Utilities {public static class Guids {public const string Medium="b11b2e0b-3076-4b9c-bbf3-ca7f851b5bb4";public const string AstralBeacon="5835c65f-1774-41d9-a20b-e012f9fbb83d";}}
namespace MediumClass.Utils
{
 public static class Settings{public static bool Enabled;public static bool IsTTTBaseEnabled()=>Enabled;}
 public static class Logging{public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger GetLogger(string s)=>new();}
}
namespace UnityModManagerNet {public class UnityModManager {public class ModEntry {public class ModLogger {public void Log(string s) {}}}}}
