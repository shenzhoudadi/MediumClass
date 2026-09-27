extern alias CecilStandalone;
using System.Reflection;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using MediumClass.Medium;
using MediumClass.Utilities;
var assertions=0;
void Check(bool pass,string message) { assertions++; if(!pass) throw new Exception(message); }
var nativePath=args.FirstOrDefault(a=>a.EndsWith("Assembly-CSharp.dll",StringComparison.OrdinalIgnoreCase));
if(nativePath!=null)
{
    using var native=CecilStandalone::Mono.Cecil.AssemblyDefinition.ReadAssembly(nativePath);
    void Method(string type,string name,params string[] parameters)
    {
        var t=native.MainModule.GetType(type);
        Check(t!=null&&t.Methods.Count(m=>m.Name==name&&m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(parameters))==1,"native patch target: "+type+"."+name);
    }
    const string ui="Kingmaker.UI.UnitSettings.UnitUISettings";
    const string helper="Kingmaker.UI.MVVM._VM.ActionBar.ActionBarSpellbookHelper";
    const string ability="Kingmaker.UnitLogic.Abilities.AbilityData";
    const string spellSlot="Kingmaker.UnitLogic.SpellSlot";
    Method(helper,"TryAddAbility","System.Collections.Generic.List`1<"+ability+">",ability);
    Method(helper,"TryAddSpell","System.Collections.Generic.List`1<"+spellSlot+">",spellSlot);
    Method(ui,"SetSlot","Kingmaker.UI.UnitSettings.MechanicActionBarSlot","System.Int32");
    Method(ui,"GetSlot","System.Int32","Kingmaker.EntitySystem.Entities.UnitEntityData");
    Method(ui,"GetBadSlotReplacement","Kingmaker.UI.UnitSettings.MechanicActionBarSlot","Kingmaker.UnitLogic.UnitDescriptor");
    Method(ui,"UpdateBadSlots");
    Method("Kingmaker.UnitLogic.UnitDescriptor","ApplyPostLoadFixes");
    var nativeUi=native.MainModule.GetType(ui);
    Check(nativeUi.Fields.Any(f=>f.Name=="m_Slots"&&f.FieldType.FullName=="Kingmaker.UI.UnitSettings.MechanicActionBarSlot[]"),"native saved-slot injection field");
    Check(nativeUi.Methods.Count(m=>m.Name=="CollectSpells"&&m.Parameters.Count>0&&m.Parameters[0].ParameterType.FullName=="Kingmaker.UnitLogic.Spellbook")==2,"native auto-collection target overloads");
    var badUpdate=nativeUi.Methods.Single(m=>m.Name=="UpdateBadSlots");
    Check(badUpdate.Body.Instructions.Any(i=>i.Operand is CecilStandalone::Mono.Cecil.MethodReference m&&m.Name=="GetBadSlotReplacement")
        && !badUpdate.Body.Instructions.Any(i=>i.Operand is CecilStandalone::Mono.Cecil.MethodReference m&&m.Name=="GetSlot"),"native UpdateBadSlots bypasses GetSlot and calls replacement point");
    var managerUpdate=native.MainModule.GetType("Kingmaker.UI.ActionBar.ActionBarManager").Methods.Single(m=>m.Name=="Update");
    Check(managerUpdate.Body.Instructions.Any(i=>i.Operand is CecilStandalone::Mono.Cecil.MethodReference m&&m.Name=="UpdateBadSlots"),"native action bar refresh invokes raw-slot cleanup");
    LegendaryConversionChecks.NativePrecedence(native,Check);
    ActionBarRefreshChecks.NativeContracts(native,Check);
}
var modPath=args.FirstOrDefault(a=>a.EndsWith("MediumClass.dll",StringComparison.OrdinalIgnoreCase));
if(modPath!=null)
{
    LegendaryConversionChecks.ProductionWiring(modPath,Check);
    ActionBarRefreshChecks.ProductionWiring(modPath,Check);
}
LegendaryConversionChecks.LevelScenarios(Check);
var currentLegendary=new BlueprintAbility{AssetGuid=BlueprintGuid.Parse(Guids.HierophantSupremeAbility2)};
var legacyLegendary=new BlueprintAbility{AssetGuid=BlueprintGuid.Parse(Guids.HierophantSupremeAbility4)};
BlueprintTool.Assets[Guids.HierophantSupremeAbility2]=currentLegendary;
BlueprintTool.Assets[Guids.HierophantSupremeAbility4]=legacyLegendary;
if(args.Contains("--reflection-smoke"))
{
    Check(typeof(Harmony).Assembly.GetName().Version==new Version(2,0,4,0),"must load actual game Harmony 2.0.4");
    object Invoke(Type t,string method,params object[] values)=>t.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,values);
    foreach(var kind in new[]{"archmage","hierophant","medium","wizard"})
    {
        var prep=kind is "archmage" or "hierophant";var owner=new UnitDescriptor();var sourceBook=new Spellbook{Id=kind};var main=new Spellbook{Id="medium"};owner.Spellbooks.Add(main);
        foreach(var circle in Enumerable.Range(0,7))
        {
            var source=new AbilityData{Blueprint=new(),Spellbook=sourceBook,SpellLevel=circle};
            Check((bool)Invoke(typeof(MediumSpiritPreparationActionBarPatch),"Prefix",source)==!prep,"spontaneous filter old-Harmony smoke");
            Check((bool)Invoke(typeof(MediumSpiritPreparationMemorizedActionBarPatch),"Prefix",new SpellSlot{SpellShell=source})==!prep,"memorized filter old-Harmony smoke");
            var match=new AbilityData{Blueprint=source.Blueprint,Spellbook=main,SpellLevel=circle};main.Known.Add(match);
            var slot=new MechanicActionBarSlotSpontaneousSpell(source);var changed=MediumSpiritActionBarRules.TryRemap(slot,owner,out var mapped);
            Check(changed==prep,"remapping isolates preparation books");
            if(prep) Check(ReferenceEquals(MediumSpiritActionBarRules.SlotAbility(mapped),match),"mapping retains actual main copy");
        }
    }
    var targets=(IEnumerable<MethodBase>)Invoke(typeof(MediumSpiritPreparationAutomaticShortcutPatch),"TargetMethods");
    Check(targets.Count()==2,"both native CollectSpells overloads selected");
    var emptyOwner=new UnitDescriptor();
    Invoke(typeof(MediumLegendaryHierophantLegacyAbilityPatch),"Postfix",emptyOwner);
    Check(emptyOwner.Abilities.Entries.Count==0,"unrelated post-load does not resolve mod blueprints or add facts");
    Console.WriteLine($"PASS {assertions} old-Harmony production-method smoke assertions; no detour installation, no Unity rendering.");
    return;
}
var harmony=new Harmony("MediumClass.Tests.SpiritActionBar");
harmony.PatchAll(Assembly.GetExecutingAssembly());
{
    var owner=new UnitDescriptor();
    var main=new Spellbook{Id="medium"};owner.Spellbooks.Add(main);
    var repaired=new AbilityData{Blueprint=new(),Spellbook=main};
    MediumSpiritSpellbookRules.BeforeActionBarFetch=unit=>{Check(ReferenceEquals(unit,owner),"Fetch repair receives exact selected owner");main.Known.Add(repaired);};
    Check(ActionBarSpellbookHelper.Fetch(owner.Unit).Contains(repaired),"Fetch prefix repairs before native collection takes its snapshot");
    MediumSpiritSpellbookRules.BeforeActionBarFetch=null;
}
foreach(var spirit in new[]{"archmage","hierophant"})
foreach(var circle in Enumerable.Range(0,7))
{
    var owner=new UnitDescriptor(); var ui=new UnitUISettings{Owner=owner};
    var main=new Spellbook{Id="medium"};var prep=new Spellbook{Id=spirit};var other=new Spellbook{Id="wizard"};
    owner.Spellbooks.AddRange(new[]{prep,other,main});
    var bp=new BlueprintAbility();
    var source=new AbilityData{Blueprint=bp,Spellbook=prep,SpellLevel=circle,Available=false};
    var known=new AbilityData{Blueprint=bp,Spellbook=main,SpellLevel=circle,Available=false};
    main.Known.Add(known);
    var actualSlot=new MechanicActionBarSlotSpontaneousSpell(known){Unit=owner.Unit};
    var preparedSlots=new MechanicActionBarSlot[]{new MechanicActionBarSlotMemorizedSpell(new(){SpellShell=source}),new MechanicActionBarSlotSpontaneousSpell(source),new MechanicActionBarSlotSpontaneusConvertedSpell{Spell=source},new MechanicActionBarSlotAbility{Ability=source}};
    foreach(var slot in preparedSlots)
    {
        ui.SetSlot(slot,0);
        Check(ReferenceEquals(MediumSpiritActionBarRules.SlotAbility(ui.Raw[0]),known),"manual prepared slot must become main book copy");
        ui.Raw[0]=slot;
        var displayed=ui.GetSlot(0,owner.Unit);
        Check(ReferenceEquals(MediumSpiritActionBarRules.SlotAbility(displayed),known),"saved slot must become main book copy");
        Check(ReferenceEquals(displayed,ui.Raw[0]),"saved migration must persist in settings");
    }
    ui.SetSlot(actualSlot,0);
    Check(ReferenceEquals(actualSlot,ui.Raw[0]),"depleted legitimate main spell must remain");
    main.Known.Clear();
    ui.SetSlot(preparedSlots[0],0);
    Check(ReferenceEquals(actualSlot,ui.Raw[0]),"adding unprepared choice must not overwrite target");
    ui.Raw[1]=preparedSlots[0];
    Check(ui.GetSlot(1,owner.Unit) is MechanicActionBarSlotEmpty,"unavailable saved preparation must clear");
    Check(ui.Raw[1] is MechanicActionBarSlotEmpty,"cleared saved slot must persist");
    main.Custom.Add(known);source.MetamagicData="heighten";
    Check(MediumSpiritActionBarRules.TryRemap(preparedSlots[0],owner,out var missingMeta)&&missingMeta is MechanicActionBarSlotEmpty,"must not replace metamagic with normal spell");
    known.MetamagicData="heighten";
    Check(MediumSpiritActionBarRules.TryRemap(preparedSlots[0],owner,out var meta)&&ReferenceEquals(MediumSpiritActionBarRules.SlotAbility(meta),known),"custom metamagic exact match must work");
    var abilities=new List<AbilityData>();ActionBarSpellbookHelper.TryAddAbility(abilities,source);ActionBarSpellbookHelper.TryAddAbility(abilities,known);
    Check(abilities.Count==1&&ReferenceEquals(abilities[0],known),"preparation cannot shadow main before deduplication");
    var slots=new List<SpellSlot>();ActionBarSpellbookHelper.TryAddSpell(slots,new(){SpellShell=source});
    Check(slots.Count==0,"prepared spell popup must exclude prepbook");
    ActionBarSpellbookHelper.TryAddSpell(slots,new(){SpellShell=new(){Blueprint=bp,Spellbook=other,Available=false}});
    Check(slots.Count==1,"depleted unrelated memorized spell must remain");
    ui.CollectSpells(prep);ui.CollectSpells(prep,circle);Check(ui.Collected==0,"auto-add both overloads exclude prepbook");
    ui.CollectSpells(main);ui.CollectSpells(other,circle);Check(ui.Collected==2,"auto-add preserves other books");
    Check(owner.Spellbooks.Count==3&&owner.Spellbooks.Contains(prep),"filters cannot remove preparation book");
}
{
    var owner=new UnitDescriptor();var current=new Ability{Data=new(){Blueprint=currentLegendary}};var old=new Ability{Data=new(){Blueprint=legacyLegendary}};
    owner.Abilities.Entries.AddRange(new[]{current,old});owner.ApplyPostLoadFixes();
    Check(old.Hidden&&!current.Hidden,"legacy fourth entry must hide without hiding current eighth circle");
    var ui=new UnitUISettings{Owner=owner};ui.Raw[0]=new MechanicActionBarSlotAbility{Ability=old.Data};
    Check(ReferenceEquals(MediumSpiritActionBarRules.SlotAbility(ui.GetSlot(0,owner.Unit)),current.Data),"legacy fourth shortcut must become eighth circle");
    ui.Raw[0]=new MechanicActionBarSlotAbility{Ability=old.Data};
    Check(ui.UpdateBadSlots(),"native raw-slot cleanup visits hidden legacy ability before GetSlot");
    Check(ReferenceEquals(MediumSpiritActionBarRules.SlotAbility(ui.Raw[0]),current.Data),"legacy shortcut migration survives UpdateBadSlots-before-GetSlot");
    var other=new Ability{Data=new(){Blueprint=new()},Hidden=true};owner.Abilities.Entries.Add(other);
    ui.Raw[1]=new MechanicActionBarSlotAbility{Ability=other.Data};
    ui.UpdateBadSlots();
    Check(ui.Raw[1] is MechanicActionBarSlotEmpty,"unrelated hidden ability retains native cleanup");
}
Console.WriteLine($"PASS {assertions} linked production UI patch assertions (native API model; not Unity rendering).");
