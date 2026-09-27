extern alias CecilStandalone;
using Cecil = CecilStandalone::Mono.Cecil;
using Cil = CecilStandalone::Mono.Cecil.Cil;

// Anchor the cached-UI scenario model to the installed game's real notification
// contracts. In particular, learning/resting is NOT a pretend UI-refresh event.
internal static class ActionBarRefreshChecks
{
    internal static void NativeContracts(Cecil.AssemblyDefinition native, Action<bool, string> check)
    {
        var module = native.MainModule;
        var book = module.GetType("Kingmaker.UnitLogic.Spellbook");
        var vm = module.GetType("Kingmaker.UI.MVVM._VM.ActionBar.ActionBarVM");
        var ui = module.GetType("Kingmaker.UI.UnitSettings.UnitUISettings");
        var helper = module.GetType("Kingmaker.UI.MVVM._VM.ActionBar.ActionBarSpellbookHelper");
        var view = module.GetType("Kingmaker.UI.MVVM._PCView.ActionBar.ActionBarGroupPCView");
        var fetch = helper.Methods.Single(m => m.Name == "Fetch");
        check(fetch.Parameters.Count == 1 && fetch.Parameters[0].ParameterType.FullName == "Kingmaker.EntitySystem.Entities.UnitEntityData",
            "native Fetch prefix targets the actual unit argument");
        check(Calls(fetch, "get_Spellbooks") && Calls(fetch, "get_MaxSpellLevel") && Calls(fetch, "GetKnownSpells"),
            "native Fetch uses all runtime books and runtime maximum circle");
        check(!Calls(fetch, "IsEnable") && !Calls(fetch, "GetClassLevel"),
            "native Fetch has no ordinary-Medium-level or menu-enable gate");
        var learnedEvents = Events(book.Methods.Single(m => m.Name == "AddKnownTemporary")).ToArray();
        check(learnedEvents.SequenceEqual(new[] { "Kingmaker.PubSubSystem.ILearnSpellHandler" }),
            "native AddKnownTemporary only raises LearnSpell");
        check(!vm.Interfaces.Any(i => i.InterfaceType.Name == "ILearnSpellHandler"), "native cached ActionBarVM does not listen to LearnSpell");
        check(!Events(book.Methods.Single(m => m.Name == "Rest")).Any(), "native Spellbook.Rest emits no menu or rest notification");
        check(Events(book.Methods.Single(m => m.Name == "Memorize")).Contains("Kingmaker.PubSubSystem.ISpellBookUIHandler")
            && vm.Interfaces.Any(i => i.InterfaceType.Name == "ISpellBookUIHandler"), "manual Memorize does notify the native cached action bar");
        var setDirty = ui.Methods.Single(m => m.Name == "SetDirty");
        check(setDirty.IsPublic && Calls(setDirty, "set_Dirty") && Events(setDirty).Contains("Kingmaker.PubSubSystem.IUnitActionBarUpdateHandler"),
            "supported native SetDirty both marks state and notifies action-bar handlers");
        var update = vm.Methods.Single(m => m.Name == "OnUpdateHandler");
        check(Calls(update, "get_Dirty") && Calls(update, "OnUnitChanged"), "native update rebuilds the cached view on Dirty");
        var selected = vm.Methods.Single(m => m.Name == "OnUnitChanged");
        check(Calls(selected, "CollectSpells") && !Calls(selected, "IsEnable"),
            "selected-unit refresh collects spells even if the old spell group was empty");
        check(Calls(vm.Methods.Single(m => m.Name == "CollectSpells"), "Fetch"), "native collection always reaches the repair boundary");
        check(selected.Body.Instructions.Any(i => i.Operand is Cecil.FieldReference f && f.Name == "OnUnitUpdated") && Calls(selected, "Execute"),
            "native complete rebuild publishes OnUnitUpdated");
        var bind = view.Methods.Single(m => m.Name == "BindViewImplementation");
        check(bind.Body.Instructions.Any(i => i.Operand is Cecil.FieldReference f && f.Name == "OnUnitUpdated")
            && view.Methods.Where(m => m.Name.StartsWith("<BindViewImplementation>")).Any(m => Calls(m, "SetGroup")),
            "native spell group redraw subscribes to that complete rebuild");
        var initialize = ui.Methods.Single(m => m.Name == "TryToInitialize");
        var clearDirty = initialize.Body.Instructions.First(i => i.Operand is Cecil.MethodReference m && m.Name == "set_Dirty");
        check(clearDirty.Previous.OpCode == Cil.OpCodes.Ldc_I4_0, "native TryToInitialize consumes Dirty while rebuilding");
        var spellEnable = vm.NestedTypes.SelectMany(t => t.Methods).Single(m => m.Name.StartsWith("<IsEnable>")
            && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "Kingmaker.UnitLogic.Spellbook");
        check(Calls(spellEnable, "get_MaxSpellLevel") && Calls(spellEnable, "GetKnownSpells"),
            "native menu enable reads dynamic spellbook ceiling and known choices");
        var entityPostLoad = module.GetType("Kingmaker.EntitySystem.EntityDataBase").Methods.Single(m => m.Name == "PostLoad");
        int LastCallIndex(string name) => entityPostLoad.Body.Instructions.ToList().FindLastIndex(i => i.Operand is Cecil.MethodReference m && m.Name == name);
        check(LastCallIndex("OnPostLoad") < LastCallIndex("PostLoad") && LastCallIndex("PostLoad") < LastCallIndex("OnComponentsDidPostLoad"),
            "native loading restores entity/facts/parts before component UI completion");
    }

    internal static void ProductionWiring(string path, Action<bool, string> check)
    {
        using var mod = Cecil.AssemblyDefinition.ReadAssembly(path);
        var rules = mod.MainModule.GetType("MediumClass.Medium.MediumSpiritSpellbookRules");
        var repair = rules.Methods.Single(m => m.Name == "SyncForActionBar");
        var sync = repair.Body.Instructions.Single(i => i.Operand is Cecil.MethodReference m && m.Name == "Sync");
        check(sync.Previous.OpCode == Cil.OpCodes.Ldc_I4_0 && !Calls(repair, "Rest") && !Calls(repair, "ClampRemainingSlots") && !Calls(repair, "NotifyActionBar"),
            "compiled Fetch repair calls Sync(false) without refill or dirty-loop entry");
        check(Calls(rules.Methods.Single(m => m.Name == "NotifyActionBar"), "SetDirty"), "compiled notification uses the real UI invalidation API");
        var prefix = mod.MainModule.GetType("MediumClass.Medium.MediumSpiritPreparationActionBarRefreshPatch");
        check(prefix != null && Calls(prefix.Methods.Single(m => m.Name == "Prefix"), "SyncForActionBar"),
            "compiled Fetch Harmony prefix is wired to the final preparation reconciliation");
        var channel = rules.Methods.Single(m => m.Name == "RefreshForChannel");
        var calls = channel.Body.Instructions.Where(i => i.Operand is Cecil.MethodReference).Select(i => ((Cecil.MethodReference)i.Operand).Name).ToList();
        check(calls.IndexOf("Rest") < calls.IndexOf("NotifyActionBar"), "compiled initial-channel refresh notifies after capacity/remaining slot initialization");
    }

    private static bool Calls(Cecil.MethodDefinition method, string name) => method.HasBody
        && method.Body.Instructions.Any(i => i.Operand is Cecil.MethodReference m && m.Name == name);
    private static IEnumerable<string> Events(Cecil.MethodDefinition method) => method.Body.Instructions
        .Select(i => i.Operand).OfType<Cecil.GenericInstanceMethod>()
        .Where(m => m.Name == "RaiseEvent").SelectMany(m => m.GenericArguments.Select(t => t.FullName));
}
