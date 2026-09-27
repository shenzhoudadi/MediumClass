using System.Reflection;
using System.Reflection.Emit;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.UnitLogic;
using MediumClass.Medium;
using MediumClass.Utilities;
using MediumClass.Utils;
int checks=0;
void Check(bool pass,string s){checks++;if(!pass)throw new Exception(s);}
var oldId=BlueprintGuid.Parse(Guids.AstralBeacon);var newId=BlueprintGuid.Parse(HardenedSoul.SelectionGuid);
BlueprintFeatureBaseReference Ref(BlueprintGuid id)=>new(){deserializedGuid=id};
BlueprintCharacterClass NewMedium()
{
 var extra=BlueprintGuid.Parse("02760f93-9e71-4164-985b-b3e02c6094d4");
 var medium=new BlueprintCharacterClass{AssetGuid=BlueprintGuid.Parse(Guids.Medium),Progression=new()
 {
  LevelEntries=new[]{new LevelEntry{Level=19,m_Features=new(){Ref(oldId)}},new LevelEntry{Level=20,m_Features=new(){Ref(oldId),Ref(extra)}}},
  UIGroups=new[]{new UIGroup{m_Features=new(){Ref(oldId),Ref(extra)}}},m_UIDeterminatorsGroup=new[]{Ref(oldId),Ref(extra)}
 },Archetypes=new[]{new BlueprintArchetype{RemoveFeatures=new[]{new LevelEntry{Level=20,m_Features=new(){Ref(oldId)}}}}}};
 ResourcesLibrary.Assets[medium.AssetGuid]=medium;return medium;
}
void Unchanged(BlueprintCharacterClass m,string text)=>Check(m.Progression.LevelEntries[1].m_Features[0].deserializedGuid==oldId,text);
ResourcesLibrary.Assets[oldId]=new BlueprintFeature{AssetGuid=oldId};
HardenedSoul.Configure();
var feat=ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(BlueprintGuid.Parse(HardenedSoul.FeatureGuid));
var selection=ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>(newId);
Check(feat!=null&&selection!=null,"own saved-blueprint refs exist without TTT");
Check(feat.Components.OfType<PrerequisiteClassLevel>().Single().Level==20,"Hardened child needs Medium20");
Check(feat.Components.OfType<PrerequisiteClassLevel>().Single().m_CharacterClass.deserializedGuid==BlueprintGuid.Parse(Guids.Medium),"actual Medium only");
Check(feat.Components.OfType<PrerequisiteNoFeature>().Single().m_Feature.deserializedGuid==oldId,"Hardened child cannot stack with Astral");
Check(selection.Components.Count==0,"selection cannot invalidate itself after its child is selected");
Check(selection.Ranks==1&&!selection.Reapply&&!selection.IgnorePrerequisites,"one stable choice respects generic prerequisites");
Check(selection.m_AllFeatures.Length==2,"own choices present without external blueprints");
var unit=new UnitDescriptor();unit.Facts.Add(oldId);
Check(!HardenedSoul.HasFeature(unit),"old Astral owner is not granted Hardened");
Check(!HardenedSoul.HasFeature(null),"null owner safe");
unit.Facts.Add(BlueprintGuid.Parse(HardenedSoul.FeatureGuid));Check(HardenedSoul.HasFeature(unit),"selected Hardened helper");
Check(!HardenedSoul.AlternateCapstonesEnabled(null),"missing context disabled");
Check(!HardenedSoul.AlternateCapstonesEnabled(new object()),"unknown API disabled");
Check(!HardenedSoul.AlternateCapstonesEnabled(new Throws()),"throwing optional API disabled");
Check(!HardenedSoul.AlternateCapstonesEnabled(new Context{Fixes=new(){AlternateCapstones=new(){DisableAll=true}}}),"disabled setting");
Check(HardenedSoul.AlternateCapstonesEnabled(new Context{Fixes=new(){AlternateCapstones=new(){DisableAll=false}}}),"enabled setting");
var medium=NewMedium();Settings.Enabled=false;HardenedSoul.ConfigureOptionalIntegration();Unchanged(medium,"absent mod no change");
Settings.Enabled=true;HardenedSoul.ConfigureOptionalIntegration();Unchanged(medium,"missing assembly no change");
var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TabletopTweaks-Base"),AssemblyBuilderAccess.Run);
var typeBuilder=assembly.DefineDynamicModule("TTTStub").DefineType("TabletopTweaks.Base.Main",TypeAttributes.Public);
typeBuilder.DefineField("TTTContext",typeof(object),FieldAttributes.Public|FieldAttributes.Static);
var contextField=typeBuilder.CreateType().GetField("TTTContext");
contextField.SetValue(null,new Context{Fixes=new(){AlternateCapstones=new(){DisableAll=true}}});
HardenedSoul.ConfigureOptionalIntegration();Unchanged(medium,"disabled TTT group preserves Astral level20");
contextField.SetValue(null,new Throws());HardenedSoul.ConfigureOptionalIntegration();Unchanged(medium,"malformed optional API preserves progression");
contextField.SetValue(null,new Context{Fixes=new(){AlternateCapstones=new(){DisableAll=false}}});
foreach(var id in new[]{HardenedSoul.PerfectBodyGuid,HardenedSoul.GreatBeastGuid})ResourcesLibrary.Assets[BlueprintGuid.Parse(id)]=new BlueprintFeature{AssetGuid=BlueprintGuid.Parse(id)};
HardenedSoul.ConfigureOptionalIntegration();
Check(medium.Progression.LevelEntries[1].m_Features[0].deserializedGuid==newId,"enabled replaces capstone at20");
Check(medium.Progression.LevelEntries[0].m_Features[0].deserializedGuid==oldId,"other level entries unchanged");
Check(medium.Progression.LevelEntries[1].m_Features.Count==2,"other20features unchanged");
Check(medium.Progression.UIGroups[0].m_Features[0].deserializedGuid==newId,"UIgroup uses selection");
Check(medium.Progression.m_UIDeterminatorsGroup[0].deserializedGuid==newId,"UI determinator uses selection");
var removed=medium.Archetypes[0].RemoveFeatures[0].m_Features;
Check(removed.Count==2&&removed.Any(r=>r.deserializedGuid==oldId)&&removed.Any(r=>r.deserializedGuid==newId),"archetype that trades away capstone cannot gain alternative");
Check(selection.m_AllFeatures.Length==4&&selection.m_Features.Length==4,"TTT generic options added to both lists");
Check(selection.m_AllFeatures.Select(r=>r.deserializedGuid).Distinct().Count()==4,"choices unique");
HardenedSoul.ConfigureOptionalIntegration();Check(removed.Count==2,"repeated integration idempotent");
Check(unit.Facts.Count==2,"blueprint integration never changes existing unit facts");
foreach(var id in new[]{HardenedSoul.PerfectBodyGuid,HardenedSoul.GreatBeastGuid})ResourcesLibrary.Assets.Remove(BlueprintGuid.Parse(id));
medium=NewMedium();HardenedSoul.ConfigureOptionalIntegration();Check(selection.m_AllFeatures.Length==2,"missing optional generics safely omitted");
Check(!HardenedSoul.ReplaceCapstone(medium,selection),"already replaced progression not changed again");
Check(!HardenedSoul.ReplaceCapstone(null,selection)&&!HardenedSoul.ReplaceCapstone(medium,null),"missing class/selection safe");
Console.WriteLine($"PASS {checks} actual HardenedSoul production blueprint/config integration assertions (API model; no Unity).");
public class Context{public Fixes Fixes;}
public class Fixes{public Group AlternateCapstones;}
public class Group{public bool DisableAll;}
public class Throws{public object Fixes=>throw new InvalidOperationException("optional API unavailable");}
