using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using MediumClass.Medium;
using MediumClass.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;

int checks = 0;
void Check(bool passed, string label) { checks++; if (!passed) throw new Exception(label); }
void Equal<T>(T expected, T actual, string label) => Check(EqualityComparer<T>.Default.Equals(expected, actual), label + $" ({expected} != {actual})");
if (args.Length != 1) throw new ArgumentException("Pass the shipped BlueprintCore.dll path.");
Equal("e7cf953e-cc45-42dd-806e-faf29dcf49da", MythicSpirits.MultipleSpiritsGuid, "multiple-spirits saved GUID remains unchanged");
Equal("ae859d6d-ffb3-4ba4-93c7-f6458f02db1e", MythicInfluence.FeatureGuid, "expanded-influence saved GUID remains unchanged");
using (var bpc = AssemblyDefinition.ReadAssembly(args[0]))
{
    var type = bpc.MainModule.Types.Single(t => t.FullName == "BlueprintCore.Blueprints.CustomConfigurators.Classes.FeatureConfigurator");
    bool Calls(MethodDefinition method, string name) => method.Body.Instructions.Any(i => i.Operand is MethodReference reference && reference.Name == name);
    var create = type.Methods.Single(m => m.Name == "New");
    var callFor = create.Body.Instructions.Single(i => i.Operand is MethodReference reference && reference.Name == "For");
    Check(callFor.Previous.OpCode == OpCodes.Ldc_I4_1, "native New enables automatic selection updates");
    Check(Calls(type.Methods.Single(m => m.Name == "OnConfigureCompleted"), "PopulateSelections"), "native configuration populates selections");
    var populate = type.Methods.Single(m => m.Name == "PopulateSelections");
    Check(Calls(populate, "HasGroup"), "native selection population matches feature group");
    Check(Calls(populate, "Contains"), "native automatic population checks existing GUID");
    Check(Calls(populate, "AddToAllFeatures"), "native automatic population adds matching features");
    Check(populate.Body.Instructions.Any(i => i.Operand is FieldReference field && field.Name == "m_AllFeatures"), "native duplicate check reads actual feature list");
}
var primary = new BlueprintFeatureSelection { Name = "Mythic Ability", Group = FeatureGroup.MythicAbility };
var extra = new BlueprintFeatureSelection { Name = "Extra Mythic Ability", Group = FeatureGroup.MythicAbility };
var ordinary = new BlueprintFeatureSelection { Name = "Ordinary Feat", Group = FeatureGroup.Feat };
FeatureConfigurator.Selections.AddRange(new[] { primary, extra, ordinary });
var sentinel = new BlueprintFeature { AssetGuid = BlueprintGuid.Parse("bd33e6c0-86af-4fd3-a6cf-e163fe2d27d9") };
primary.AllFeatures.Add(sentinel);
MythicSpirits.Configure();
MythicInfluence.Configure();
foreach (var (guid, key) in new[] {
    (MythicSpirits.MultipleSpiritsGuid, "MythicMultipleSpirits"),
    (MythicInfluence.FeatureGuid, "MythicExpandedInfluence")
})
{
    var feature = BlueprintTool.Get<BlueprintFeature>(guid);
    Check(feature != null, "original saved blueprint GUID resolves");
    Equal(key + ".Name", feature.DisplayName, "name localization unchanged");
    Equal(key + ".Description", feature.Description, "description localization unchanged");
    Equal(1, feature.Ranks, "ability remains a single rank");
    Check(feature.Groups.SequenceEqual(new[] { FeatureGroup.MythicAbility }), "mythic group retained");
    Equal(2, feature.Prerequisites.Count, "both class prerequisites retained");
    Check(feature.Prerequisites.All(p => p.Group == Prerequisite.GroupType.Any), "Medium or Prowler is sufficient");
    Check(feature.Prerequisites.Select(p => p.Feature).ToHashSet().SetEquals(new[] { Guids.MediumChannelSpirit, Guids.ProwlerSpirit }), "exact old prerequisite GUIDs retained");
    Equal(1, primary.AllFeatures.Count(f => f.AssetGuid == feature.AssetGuid), "first mythic selection contains exactly one entry");
    Equal(1, extra.AllFeatures.Count(f => f.AssetGuid == feature.AssetGuid), "extra mythic ability selection contains exactly one entry");
    Equal(0, ordinary.AllFeatures.Count(f => f.AssetGuid == feature.AssetGuid), "ordinary feat selection is unaffected");
    FeatureConfigurator.PopulateSelections(feature);
    Equal(1, primary.AllFeatures.Count(f => f.AssetGuid == feature.AssetGuid), "repeated automatic population is harmless");
    // Negative control recreates the former two-registration paths and proves
    // this model can detect the reported duplicate instead of auto-deduping it.
    FeatureSelectionConfigurator.For(primary).AddToAllFeatures(feature).Configure();
    Equal(2, primary.AllFeatures.Count(f => f.AssetGuid == feature.AssetGuid), "old manual append after automatic registration reproduces duplicate");
    primary.AllFeatures.RemoveAt(primary.AllFeatures.Count - 1);
}
Equal(3, primary.AllFeatures.Count, "other mods' existing entry remains alongside our two abilities");
Check(ReferenceEquals(sentinel, primary.AllFeatures[0]), "unrelated entry identity/order untouched");
// A buff without recoverable selection must not be replaced by another first
// channel: the old buff's OnDeactivate resets accumulated influence.
BlueprintTool.Assets[BlueprintGuid.Parse(Guids.MediumChannelSpiritPrimarySpiritBuff)] = new Kingmaker.UnitLogic.Buffs.Blueprints.BlueprintBuff();
BlueprintTool.Assets[BlueprintGuid.Parse(Guids.Champion)] = new BlueprintCharacterClass { AssetGuid = BlueprintGuid.Parse(Guids.Champion) };
BlueprintTool.Assets[BlueprintGuid.Parse(Guids.Guardian)] = new BlueprintCharacterClass { AssetGuid = BlueprintGuid.Parse(Guids.Guardian) };
var champion = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Champion);
var guardian = BlueprintTool.GetRef<BlueprintCharacterClassReference>(Guids.Guardian);
var unit = new Kingmaker.EntitySystem.Entities.UnitEntityData { State = new() };
unit.State.Spirits.Add(champion, new object());
unit.State.Spirits.Add(guardian, new object());
Check(MultiSpiritRules.CanChannel(unit, champion), "first channel works without an existing buff");
unit.ChannelPresent = true;
Check(!MultiSpiritRules.CanChannel(unit, champion), "orphan saved buff cannot be replaced");
unit.MultipleSpirits = true;
Check(!MultiSpiritRules.CanChannel(unit, champion), "mythic ownership cannot bypass orphan-buff protection");
unit.State.ActiveSpiritClasses.Add(champion);
Check(!MultiSpiritRules.CanChannel(unit, champion), "restored existing spirit cannot be channeled twice");
Check(MultiSpiritRules.CanChannel(unit, guardian), "restored channel still accepts a distinct mythic spirit");
unit.MultipleSpirits = false;
Check(!MultiSpiritRules.CanChannel(unit, guardian), "without mythic ability no second spirit is allowed");
Console.WriteLine($"PASS {checks} production mythic registration assertions and shipped BlueprintCore contracts (explicit API model; no Unity UI).");
