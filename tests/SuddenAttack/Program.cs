using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.References;
using Kingmaker.RuleSystem.Rules;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using Mono.Cecil;
using Mono.Cecil.Cil;
using static Kingmaker.UnitLogic.FactLogic.AddMechanicsFeature;

int checks = 0;
var failures = new List<string>();
void Check(bool condition, string label) { checks++; if (!condition) failures.Add(label); }
void Equal(int expected, int actual, string label) => Check(expected == actual, $"{label}: expected {expected}, got {actual}");
IEnumerable<TypeDefinition> Flatten(TypeDefinition type) => new[] { type }.Concat(type.NestedTypes.SelectMany(Flatten));
int? Constant(Instruction instruction) => instruction?.OpCode.Code switch
{
    Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2,
    Code.Ldc_I4 => (int)instruction.Operand,
    Code.Ldc_I4_S => Convert.ToInt32(instruction.Operand), _ => null
};
int? Assignment(MethodDefinition method, string owner, string field, bool defaultZero)
{
    var stores = method.Body.Instructions.Where(i => i.OpCode.Code == Code.Stfld &&
        i.Operand is FieldReference f && f.DeclaringType.FullName == owner && f.Name == field).ToArray();
    if (stores.Length == 0) return defaultZero ? 0 : null;
    return stores.Length == 1 ? Constant(stores[0].Previous) : null;
}
IEnumerable<int[]> Permutations(int[] values)
{
    if (values.Length == 0) { yield return Array.Empty<int>(); yield break; }
    foreach (var value in values)
        foreach (var rest in Permutations(values.Where(v => v != value).ToArray()))
            yield return new[] { value }.Concat(rest).ToArray();
}

try
{
    if (args.Length is < 1 or > 3)
        throw new ArgumentException("Usage: SuddenAttack <Assembly-CSharp.dll> [staged-MediumClass.dll] [blueprints.zip]");
    using var game = AssemblyDefinition.ReadAssembly(args[0]);
    Console.WriteLine("Game DLL SHA256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))));
    NativeAttackPool.Load(game);
    var nativeRule = game.MainModule.Types.Single(t => t.FullName == "Kingmaker.RuleSystem.Rules.RuleCalculateAttacksCount");
    var nativeAdd = nativeRule.Methods.Single(m => m.Name == "AddExtraAttacks");
    Check(nativeAdd.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] {
        "System.Int32", "System.Boolean", "System.Boolean", "Kingmaker.Items.ItemEntity" }), "native four-argument AddExtraAttacks API");
    var nativeMain = nativeRule.NestedTypes.Single(t => t.Name == "AttacksCount").Methods.Single(m => m.Name == "get_MainAttacks");
    Check(nativeMain.Body.Instructions.Where(i => i.OpCode.Code != Code.Nop).Select(i => i.OpCode.Code)
        .SequenceEqual(new[] { Code.Ldarg_0, Code.Ldfld, Code.Ldarg_0, Code.Ldfld, Code.Add, Code.Ret }), "native MainAttacks getter has verified sum shape");
    Check(nativeMain.Body.Instructions.Where(i => i.OpCode.Code == Code.Ldfld).Select(i => ((FieldReference)i.Operand).Name)
        .SequenceEqual(new[] { "AdditionalAttacks", "HasteAttacks" }), "native full-BAB pool sums independent and haste attacks");
    var enumerator = game.MainModule.Types.SelectMany(Flatten).Single(t => t.FullName == "Kingmaker.UnitLogic.Commands.UnitAttack/<EnumerateAttacks>d__89");
    var moveNext = enumerator.Methods.Single(m => m.Name == "MoveNext");
    var mainCall = moveNext.Body.Instructions.Single(i => i.Operand is MethodReference m && m.FullName == nativeMain.FullName);
    var loop = mainCall.Next;
    Check(loop.OpCode.Code is Code.Blt or Code.Blt_S && loop.Operand is Instruction zeroStart &&
        zeroStart.OpCode.Code == Code.Ldarg_0 && Constant(zeroStart.Next) == 0 &&
        zeroStart.Next.Next.OpCode.Code == Code.Stfld &&
        zeroStart.Next.Next.Operand is FieldReference current && current.Name == "<>2__current",
        "native MainAttacks loop yields zero iterative attack penalty");

    int hasteNumber = 1; bool hasteFlag = true, hastePenalized = false;
    if (args.Length == 3)
    {
        using var blueprints = ZipFile.OpenRead(args[2]);
        JsonDocument Read(string path)
        {
            using var stream = blueprints.GetEntry(path)?.Open() ?? throw new InvalidOperationException("Missing game blueprint " + path);
            return JsonDocument.Parse(stream);
        }
        using var effect = Read("Spells/Level3/HasteBuffEffect.jbp");
        using var parent = Read("Spells/Level3/HasteBuff.jbp");
        var effectId = effect.RootElement.GetProperty("AssetId").GetString();
        Check(parent.RootElement.GetProperty("Data").GetProperty("Components").GetRawText().Contains(effectId), "installed Haste parent links the extra-attack effect");
        var extra = effect.RootElement.GetProperty("Data").GetProperty("Components").EnumerateArray()
            .Single(c => c.GetProperty("$type").GetString().EndsWith(", BuffExtraAttack"));
        hasteNumber = extra.GetProperty("Number").GetInt32();
        hasteFlag = extra.GetProperty("Haste").GetBoolean();
        hastePenalized = extra.GetProperty("Penalized").GetBoolean();
        Equal(1, hasteNumber, "installed Haste grants one attack");
        Check(hasteFlag && !hastePenalized, "installed Haste uses haste pool without iterative penalty");
    }
    else Console.WriteLine("NOTE: blueprints.zip omitted; Haste input uses documented Number=1/Haste=true/Penalized=false.");

    var defaults = new AddSuddenAttack();
    Equal(1, defaults.Number, "default Sudden Attack grants exactly one attack");
    Check(!defaults.Haste, "default Sudden Attack is independent of haste pool");
    Check(!defaults.Penalized, "default Sudden Attack uses highest BAB");
    MediumClass.Medium.Spirits.Champion.SuddenAttack.ConfigureEnabled();
    var feature = FeatureConfigurator.Last;
    Check(feature != null && feature.Configured, "production blueprint reaches Configure");
    Equal(1, feature.Components.Count, "production blueprint attaches one attack component");
    var configured = feature.Components.OfType<AddSuddenAttack>().Single();
    Equal(1, configured.Number, "blueprint grants one attack");
    Check(!configured.Haste && !configured.Penalized, "blueprint explicitly creates independent full-BAB attack");
    Check(feature.Mechanics.SequenceEqual(new[] { MechanicsFeatureType.SuppressedManyshot }), "existing Manyshot suppression preserved");

    void Haste(RuleCalculateAttacksCount evt) => evt.AddExtraAttacks(hasteNumber, hasteFlag, hastePenalized, null);
    foreach (var component in new[] { defaults, configured })
    {
        foreach (bool hasteFirst in new[] { false, true })
        {
            var evt = new RuleCalculateAttacksCount();
            evt.PrimaryHand.AdditionalAttacks = 1; evt.PrimaryHand.PenalizedAttacks = 2;
            if (hasteFirst) Haste(evt);
            component.OnEventAboutToTrigger(evt);
            if (!hasteFirst) Haste(evt);
            Equal(5, evt.PrimaryHand.Total, "three original attacks + Sudden + Haste, either order");
            Equal(2, evt.PrimaryHand.AdditionalAttacks, "Sudden enters independent full-BAB pool");
            Equal(1, evt.PrimaryHand.HasteAttacks, "Haste pool remains one");
            Equal(2, evt.PrimaryHand.PenalizedAttacks, "existing iterative attacks unchanged");
            var before = evt.PrimaryHand.Total;
            component.OnEventDidTrigger(evt);
            Equal(before, evt.PrimaryHand.Total, "after-trigger does not grant another attack");
            Check(evt.Calls.Any(c => c.number == 1 && !c.haste && !c.penalized && c.weapon == null), "component passes independent unpenalized native call");
            Haste(evt);
            Equal(before, evt.PrimaryHand.Total, "second ordinary Haste does not stack");
        }
        foreach (var order in Permutations(new[] { 0, 1, 2, 3, 4 }))
        {
            var evt = new RuleCalculateAttacksCount();
            evt.PrimaryHand.AdditionalAttacks = 1; evt.PrimaryHand.PenalizedAttacks = 3;
            foreach (int effect in order)
                switch (effect)
                {
                    case 0: component.OnEventAboutToTrigger(evt); break;
                    case 1: Haste(evt); break;
                    case 2: evt.AddExtraAttacks(2, true, false, null); break;
                    case 3: evt.AddExtraAttacks(2, false, false, null); break;
                    case 4: evt.AddExtraAttacks(1, false, true, null); break;
                }
            string label = "effect order " + string.Join(",", order);
            Equal(4, evt.PrimaryHand.AdditionalAttacks, label + " independent sum");
            Equal(2, evt.PrimaryHand.HasteAttacks, label + " highest haste only");
            Equal(4, evt.PrimaryHand.PenalizedAttacks, label + " iterative pool isolated");
        }
        foreach (string flurry in new[] { "", FeatureRefs.FlurryOfBlows, FeatureRefs.FlurryOfBlowsLevel11 })
            foreach (bool charging in new[] { false, true })
            {
                var evt = new RuleCalculateAttacksCount();
                evt.Initiator.State.IsCharging = charging;
                if (flurry != "") evt.Initiator.Facts.Add(flurry);
                Haste(evt);
                component.OnEventAboutToTrigger(evt);
                bool excluded = charging || flurry != "";
                Equal(excluded ? 1 : 2, evt.PrimaryHand.Total, $"existing exclusion: flurry={flurry}, charging={charging}");
                Equal(1, evt.PrimaryHand.HasteAttacks, "exclusion preserves another source's Haste");
            }
    }

    if (args.Length >= 2)
    {
        using var mod = AssemblyDefinition.ReadAssembly(args[1]);
        const string componentName = "MediumClass.Medium.NewComponents.AbilitySpecific.AddSuddenAttack";
        var component = mod.MainModule.Types.Single(t => t.FullName == componentName);
        var ctor = component.Methods.Single(m => m.IsConstructor && !m.IsStatic);
        Check(Assignment(ctor, componentName, "Number", true) == 1, "delivery DLL component default Number=1");
        Check(Assignment(ctor, componentName, "Haste", true) == 0, "delivery DLL component default Haste=false");
        Check(Assignment(ctor, componentName, "Penalized", true) == 0, "delivery DLL component default Penalized=false");
        var blueprint = mod.MainModule.Types.Single(t => t.FullName == "MediumClass.Medium.Spirits.Champion.SuddenAttack");
        var configure = blueprint.Methods.Single(m => m.Name == "ConfigureEnabled");
        var init = Flatten(blueprint).SelectMany(t => t.Methods).Single(m => m.HasBody &&
            m.Name.StartsWith("<ConfigureEnabled>b__") && m.Parameters.Any(p => p.ParameterType.FullName == componentName));
        Check(configure.Body.Instructions.Any(i => i.OpCode.Code == Code.Ldftn && i.Operand is MethodReference m && m.FullName == init.FullName), "delivery DLL uses the checked component initializer delegate");
        Check(configure.Body.Instructions.Any(i => i.Operand is GenericInstanceMethod m && m.Name == "AddComponent" && m.GenericArguments.Any(a => a.FullName == componentName)), "delivery DLL wires AddSuddenAttack into actual blueprint");
        Check(Assignment(init, componentName, "Number", false) == 1, "delivery DLL blueprint Number=1");
        Check(Assignment(init, componentName, "Haste", false) == 0, "delivery DLL blueprint Haste=false");
        Check(Assignment(init, componentName, "Penalized", false) == 0, "delivery DLL blueprint Penalized=false");
        var trigger = component.Methods.Single(m => m.Name == "OnEventAboutToTrigger");
        var call = trigger.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "AddExtraAttacks");
        var argsIl = trigger.Body.Instructions.TakeWhile(i => i != call).TakeLast(7).ToArray();
        Check(argsIl.Select(i => i.OpCode.Code).SequenceEqual(new[] { Code.Ldarg_0, Code.Ldfld, Code.Ldarg_0, Code.Ldfld, Code.Ldarg_0, Code.Ldfld, Code.Ldnull }) &&
            argsIl.Where(i => i.Operand is FieldReference).Select(i => ((FieldReference)i.Operand).Name).SequenceEqual(new[] { "Number", "Haste", "Penalized" }),
            "delivery DLL forwards the checked Number/Haste/Penalized fields and null weapon to native API");
        Console.WriteLine("Delivery DLL SHA256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))));
    }
    else Console.WriteLine("NOTE: delivery DLL omitted; binary configuration/wiring checks skipped.");
    foreach (var failure in failures.Take(12)) Console.Error.WriteLine("FAIL: " + failure);
    Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}: {checks} checks, {failures.Count} failures. Production component + production blueprint; installed native pool IL executed. Not Unity gameplay.");
    return failures.Count == 0 ? 0 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.ToString());
    return 2;
}
