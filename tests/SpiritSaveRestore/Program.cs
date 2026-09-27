using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using MediumClass.Medium;
using MediumClass.Medium.NewActions;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using MediumClass.Medium.NewUnitParts;
using MediumClass.NewComponents;
using MediumClass.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Mono.Cecil;

int checks = 0;
var failures = new List<string>();
var all = new[] { Guids.Archmage, Guids.Hierophant, Guids.Champion, Guids.Guardian, Guids.Marshal, Guids.Trickster };
foreach (var id in all) BlueprintTool.Assets[id] = new BlueprintCharacterClass { Name = id };
foreach (var id in new[] { Guids.MediumChannelSpirit, Guids.ProwlerSpirit, Guids.SpiritPower, Guids.MediumSpiritBonus,
    Guids.MediumSpiritMastery, Guids.AstralBeacon, Guids.MediumSpellcasterFeatProhibitArchmage,
    Guids.MediumSpellcasterFeatProhibitHierophant, MythicSpirits.MultipleSpiritsGuid })
    BlueprintTool.Assets[id] = new BlueprintFeature { Name = id, Ranks = 10 };
foreach (var id in new[] { Guids.MediumChannelSpiritPrimarySpiritBuff, Guids.MediumSharedSeanceBuff,
    Guids.MediumSpiritBonusBuff, Guids.MediumInfluenceDebuff, Guids.WeakerSpiritChannelBuff })
    BlueprintTool.Assets[id] = new BlueprintBuff { Name = id };
foreach (var id in new[] { Guids.WeakerSpiritChannelOneAbility, Guids.WeakerSpiritChannelTwoAbility,
    Guids.WeakerSpiritChannelThreeAbility, Guids.WeakerSpiritChannelFourAbility })
    BlueprintTool.Assets[id] = new BlueprintAbility();
var definitions = all.Select(Definition).ToArray();
BlueprintTool.Get<BlueprintFeature>(Guids.MediumChannelSpirit).Components.AddRange(definitions);
BlueprintTool.Get<BlueprintFeature>(Guids.ProwlerSpirit).Components.AddRange(definitions.Skip(2));
var json = new JsonSerializerSettings { ContractResolver = new GameOptInResolver() };

foreach (bool prowler in new[] { false, true })
foreach (int count in new[] { 1, 2, 4 })
foreach (int forgone in new[] { 0, 1 })
foreach (int remaining in new[] { 0, 1 })
{
    string label = $"prowler={prowler}, spirits={count}, weaker={forgone}, freeRemaining={remaining}";
    var owner = NewOwner(prowler);
    var state = owner.Get<UnitPartMedium>();
    var selected = (prowler ? all.Skip(2) : all).Take(count).Select(Ref).ToArray();
    state.PrimarySpirit = selected[0];
    state.AdditionalSpirits.AddRange(selected.Skip(1));
    state.SecondarySpirit = Ref(Guids.Trickster);
    state.ForgonePowers = forgone;
    state.FreeSurgeAmount = remaining;
    var channel = AddChannel(owner, selected[0]);
    MediumChannelRestore.Restore(owner.Descriptor);
    var expectedPowers = Powers(owner);
    Check(expectedPowers.Length > 0, label + ": initial powers granted");

    // Saving and zone unloading turn effects off; neither operation ends a channel.
    var catalogue = owner.Facts.Get(BlueprintTool.Get<BlueprintFeature>(prowler ? Guids.ProwlerSpirit : Guids.MediumChannelSpirit));
    catalogue.CallComponents<MediumSpiritComponent>(c => c.OnTurnOff());
    new MediumSpiritMasteryComponent { Owner = owner }.OnTurnOff();
    new MediumWeakerSpiritComponent { Owner = owner }.OnTurnOff();
    Check(ReferenceEquals(state, owner.Get<UnitPartMedium>()), label + ": temporary turn-off retains part");
    Check(state.ActiveSpiritClasses.Count() == count, label + ": temporary turn-off retains catalogue and selection");
    Check(state.FreeSurgeAmount == remaining && state.ForgonePowers == forgone, label + ": turn-off retains daily counters");
    state.OnPreSave();
    string payload = JsonConvert.SerializeObject(state, json);
    var saved = JObject.Parse(payload);
    foreach (var field in new[] { "ChannelSaveVersion", "PrimarySpirit", "SecondarySpirit", "AdditionalSpirits", "ForgonePowers", "FreeSurgeAmount" })
        Check(saved.ContainsKey(field), label + ": real opt-in JSON includes " + field);
    Check(!saved.ContainsKey("Spirits") && !saved.ContainsKey("Owner"), label + ": derived catalogue and owner excluded");

    // Deserialize a fresh object, with no runtime catalogue and no legacy ability
    // context. New saves must succeed solely from their explicit saved choice.
    var loaded = NewOwner(prowler);
    var restored = JsonConvert.DeserializeObject<UnitPartMedium>(payload, json);
    loaded.LoadPart(restored);
    Check(restored.Spirits.Count == 0, label + ": fresh JSON object starts with empty derived catalogue");
    var savedChannel = AddChannel(loaded, null);
    loaded.Influence = 4;
    restored.OnPostLoad();
    InvokeRestorePrefix(loaded.Descriptor);
    Check(restored.ActiveSpiritClasses.Count() == count, label + ": restored formal spirits");
    Check(restored.PrimarySpirit.Equals(selected[0]), label + ": restored primary");
    Check(restored.SecondarySpirit.Equals(Ref(Guids.Trickster)), label + ": secondary persisted");
    Check(restored.ForgonePowers == forgone && restored.FreeSurgeAmount == remaining, label + ": spent free surges and forgone powers persisted");
    Check(Powers(loaded) == expectedPowers, label + ": powers reconstructed before native spellbook fixes");
    Check(loaded.Influence == 4, label + ": load never resets accumulated influence");
    Check((loaded.Get<UnitPartMediumPreparedSpells>()?.Clears ?? 0) == 0, label + ": no destructive channel reapply");
    restored.OnTurnOn();
    InvokeRestorePrefix(loaded.Descriptor);
    Check(Powers(loaded) == expectedPowers, label + ": repeat restoration never stacks feature ranks");
    Check(restored.FreeSurgeAmount == remaining && loaded.Influence == 4, label + ": repeat/zone turn-on preserves counters");
    catalogue.CallComponents<MediumSpiritComponent>(c => c.OnTurnOn());
    new MediumSpiritMasteryComponent { Owner = owner }.OnTurnOn();
    new MediumWeakerSpiritComponent { Owner = owner }.OnTurnOn();
    Check(state.FreeSurgeAmount == remaining, label + ": re-enable does not replenish free surges");
}

// 0.2.2 saved only AdditionalSpirits. Recover a legacy primary from the buff's
// real source action, even when the old part vanished completely before saving.
foreach (bool prowler in new[] { false, true })
foreach (bool missingPart in new[] { false, true })
{
    var owner = NewOwner(prowler);
    var primary = Ref(prowler ? Guids.Champion : Guids.Archmage);
    var legacy = JsonConvert.DeserializeObject<UnitPartMedium>("{\"AdditionalSpirits\":[]}", json);
    owner.LoadPart(legacy);
    if (missingPart) owner.RemovePart(typeof(UnitPartMedium));
    AddChannel(owner, primary);
    owner.Influence = 3;
    InvokeRestorePrefix(owner.Descriptor);
    Check(owner.Get<UnitPartMedium>().IsActiveSpirit(primary), "legacy primary repaired for both classes, missingPart=" + missingPart);
    Check(owner.Influence == 3, "legacy repair retains influence");
    Check(owner.Get<UnitPartMedium>().FreeSurgeAmount == 0, "legacy cannot invent unspent free surges");
}

// An early focus callback is safe; the restoration pass reconciles it after
// rebuilding the catalogue. Recovering an absent channel must not resurrect it.
{
    var owner = NewOwner(false);
    var state = owner.Get<UnitPartMedium>();
    state.Spirits.Clear();
    var focusFeature = new BlueprintFeature { Name = "SaveRestoreFocus" };
    var focusFact = owner.AddFact(focusFeature);
    focusFact.Components.Add(new MediumSpiritFocusComponent { Spirit = Ref(Guids.Archmage) });
    focusFact.CallComponents<MediumSpiritFocusComponent>(c => c.OnTurnOn());
    state.PrimarySpirit = Ref(Guids.Archmage);
    AddChannel(owner, state.PrimarySpirit);
    InvokeRestorePrefix(owner.Descriptor);
    Check(state.Spirits[Ref(Guids.Archmage)].SpiritFocus == 1, "focus rebuilt after catalogue");
    owner.Buffs.Enumerable.Clear();
    InvokeRestorePrefix(owner.Descriptor);
    Check(!state.ActiveSpiritClasses.Any(), "no channel buff cannot resurrect saved choices");
    var fact = owner.Facts.Get(BlueprintTool.Get<BlueprintFeature>(Guids.MediumChannelSpirit));
    fact.CallComponents<MediumSpiritComponent>(c => c.OnDeactivate());
    Check(state.Spirits.Count == 0 && owner.Get<UnitPartMedium>() == state, "actual feature removal detaches catalogue without deleting saved part");
    fact.IsActive = false;
    fact.CallComponents<MediumSpiritComponent>(c => c.OnPostLoad());
    Check(state.Spirits.Count == 0, "inactive class fact cannot rebuild a catalogue during loading");
}

if (args.Length > 0)
{
    using var game = AssemblyDefinition.ReadAssembly(args[0]);
    var resolver = game.MainModule.GetType("Kingmaker.EntitySystem.Persistence.JsonUtility.OptInContractResolver")
        .Methods.Single(m => m.Name == "CreateProperties");
    Check(resolver.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_1), "native contract resolver explicitly selects MemberSerialization.OptIn");
    var preSave = game.MainModule.GetType("Kingmaker.EntitySystem.EntityDataBase").Methods.Single(m => m.Name == "PreSave");
    Check(Calls(preSave).Any(m => m.Name == "TurnOff"), "native pre-save may temporarily turn effects off");
    var apply = game.MainModule.GetType("Kingmaker.EntitySystem.Entities.UnitEntityData").Methods.Single(m => m.Name == "OnApplyPostLoadFixes");
    Check(Calls(apply).Any(m => m.DeclaringType.FullName == "Kingmaker.UnitLogic.UnitDescriptor" && m.Name == "ApplyPostLoadFixes"), "native load reaches our restoration boundary");
}
if (args.Length > 1)
{
    using var mod = AssemblyDefinition.ReadAssembly(args[1]);
    var part = mod.MainModule.GetType("MediumClass.Medium.NewUnitParts.UnitPartMedium");
    foreach (var name in new[] { "ChannelSaveVersion", "PrimarySpirit", "SecondarySpirit", "AdditionalSpirits", "ForgonePowers", "FreeSurgeAmount" })
        Check(part.Fields.Single(f => f.Name == name).CustomAttributes.Any(a => a.AttributeType.FullName == "Newtonsoft.Json.JsonPropertyAttribute"), "installer DLL persists " + name);
    Check(part.Fields.Single(f => f.Name == "Spirits").CustomAttributes.Any(a => a.AttributeType.Name == "JsonIgnoreAttribute"), "installer excludes runtime catalogue");
    Check(!Calls(part.Methods.Single(m => m.Name == "RemoveSpiritEntry")).Any(m => m.Name == "RemoveSelf"), "installer cannot delete saved state when catalogue empties");
    var prefix = mod.MainModule.GetType("MediumClass.Medium.MediumChannelRestorePatch");
    var patchAttribute = prefix.CustomAttributes.Single(a => a.AttributeType.Name == "HarmonyPatch");
    Check(((TypeReference)patchAttribute.ConstructorArguments[0].Value).FullName == "Kingmaker.UnitLogic.UnitDescriptor"
        && (string)patchAttribute.ConstructorArguments[1].Value == "ApplyPostLoadFixes", "installer patch targets the native load boundary");
    Check(Calls(prefix.Methods.Single(m => m.Name == "Prefix")).Any(m => m.Name == "Restore"), "installer repairs before original load fixes");
    foreach (var component in new[] { "MediumSpiritComponent", "MediumSpiritMasteryComponent", "MediumWeakerSpiritComponent" })
    {
        var type = mod.MainModule.GetType("MediumClass.Medium.NewComponents.AbilitySpecific." + component);
        Check(type.Methods.All(m => m.Name != "OnTurnOff"), "installer temporary disable preserves " + component);
        Check(type.Methods.Any(m => m.Name == "OnDeactivate"), "installer actual removal still cleans " + component);
    }
    var repair = mod.MainModule.GetType("MediumClass.Medium.MediumChannelRestore");
    var repairMethods = repair.Methods.Concat(repair.NestedTypes.SelectMany(t => t.Methods)).Where(m => m.HasBody);
    Check(!repairMethods.SelectMany(Calls).Any(m => m.Name is "Rest" or "Reapply" or "Reset" or "OnDeactivate"), "installer restoration never rests, reapplies channel, or resets influence");
}
Console.WriteLine($"Save/restore: {checks - failures.Count}/{checks} checks passed. Real Newtonsoft opt-in JSON roundtrip and linked production lifecycle on explicit game API doubles; no Unity gameplay.");
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

void Check(bool condition, string name) { checks++; if (!condition) failures.Add(name); }
BlueprintCharacterClassReference Ref(string id) => BlueprintTool.GetRef<BlueprintCharacterClassReference>(id);
IEnumerable<MethodReference> Calls(MethodDefinition method) => method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>();
void InvokeRestorePrefix(UnitDescriptor owner) => typeof(MediumChannelRestorePatch).GetMethod("Prefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { owner });
string Powers(UnitEntityData owner) => string.Join(";", owner.Progression.Features.Counts.Where(p => p.Key.Name.StartsWith("power-"))
    .OrderBy(p => p.Key.Name).Select(p => p.Key.Name + "=" + p.Value));
MediumSpiritComponent Definition(string id)
{
    BlueprintFeatureReference Power(string suffix) => new() { Blueprint = new BlueprintFeature { Name = "power-" + id + suffix, Ranks = 10 } };
    return new MediumSpiritComponent {
        SpiritClass = Ref(id), SpiritBonusFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus),
        SpiritSeanceBoon = Power("boon"), SpiritLesserPower = Power("lesser"), SpiritIntermediatePower = Power("intermediate"),
        SpiritGreaterPower = Power("greater"), SpiritSupremePower = Power("supreme"),
        Stats = new[] { StatType.AdditionalAttackBonus }, Penalties = new[] { StatType.SaveFortitude }
    };
}
UnitEntityData NewOwner(bool prowler)
{
    var owner = new UnitEntityData();
    owner.Ensure<UnitPartMedium>();
    var blueprint = BlueprintTool.Get<BlueprintFeature>(prowler ? Guids.ProwlerSpirit : Guids.MediumChannelSpirit);
    var fact = owner.AddFact(blueprint);
    fact.Components.AddRange(blueprint.Components);
    fact.CallComponents<MediumSpiritComponent>(c => c.OnActivate());
    owner.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower)] = prowler ? 3 : 4;
    return owner;
}
Buff AddChannel(UnitEntityData owner, BlueprintCharacterClassReference primary)
{
    var ability = new BlueprintAbility();
    ability.Components.Add(new AbilityEffectRunAction { Actions = new() { Actions = new object[] { new ContextActionApplySpirit { Spirit = primary } } } });
    var buff = new Buff { Owner = owner, Blueprint = BlueprintTool.Get<BlueprintBuff>(Guids.MediumChannelSpiritPrimarySpiritBuff), Context = new() { SourceAbility = primary == null ? null : ability } };
    buff.Components.Add(new ApplySpirits());
    owner.Buffs.Enumerable.Add(buff);
    return buff;
}

class GameOptInResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        => base.CreateProperties(type, MemberSerialization.OptIn);
}
