using System.Text.RegularExpressions;
using MediumClass.Medium;
using Mono.Cecil;
using Mono.Cecil.Cil;

// This runs the exact production arithmetic helper, not a reimplementation.
// Golden daily counts are independent rule data; source tables are parsed only as inputs.
var sourceRoot = FindSourceRoot();
var assertions = 0;
var failures = new List<string>();

int[][] expectedRows =
[
    [0],
    [0, 1],
    [0, 2],
    [0, 3],
    [0, 3, 1],
    [0, 4, 2],
    [0, 4, 3],
    [0, 4, 3, 1],
    [0, 4, 4, 2],
    [0, 5, 4, 3],
    [0, 5, 4, 3, 1],
    [0, 5, 4, 4, 2],
    [0, 5, 5, 4, 3],
    [0, 5, 5, 4, 3, 1],
    [0, 5, 5, 4, 4, 2],
    [0, 5, 5, 5, 4, 3],
    [0, 5, 5, 5, 4, 3, 1],
    [0, 5, 5, 5, 4, 4, 2],
    [0, 5, 5, 5, 5, 4, 3],
    [0, 5, 5, 5, 5, 5, 4],
    [0, 5, 5, 5, 5, 5, 5]
];
int[] expectedMax = [0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6, 6, 6, 6];

// Explicit high-signal boundaries, CHA 18, no extra daily slots.
var boundaryCasts = new Dictionary<int, int[]>
{
    [1] = [2], [3] = [4], [4] = [4, 2], [6] = [5, 4],
    [7] = [5, 4, 2], [10] = [6, 5, 4, 2],
    [13] = [6, 6, 5, 4, 1], [16] = [6, 6, 6, 5, 3, 1],
    [20] = [6, 6, 6, 6, 5, 5]
};

foreach (var spirit in new[] { "Archmage", "Hierophant" })
{
    var path = Path.Combine(sourceRoot, "Medium", "Spirits", spirit, spirit + ".cs");
    var rows = ReadDailyTable(path);
    Equal(21, rows.Length, spirit + " has class levels 0 through 20");
    for (var level = 0; level <= 20 && level < rows.Length; level++)
    {
        Check(expectedRows[level].SequenceEqual(rows[level]), spirit + " golden progression row " + level);
        Equal(expectedMax[level], SpiritSpellcastingMath.MaxSpellLevel(rows[level]), spirit + " maximum circle at class level " + level);
        for (var circle = 1; circle <= 6; circle++)
        {
            int count = circle < rows[level].Length ? rows[level][circle] : -1;
            int expected = circle < expectedRows[level].Length ? expectedRows[level][circle] : 0;
            // The exact casting threshold permits casting but yields no attribute bonus.
            Equal(expected, SpiritSpellcastingMath.DailySlots(count, circle, 10 + circle,
                10 + circle, 10 + circle, true, 0),
                spirit + " base daily casts at class level " + level + ", circle " + circle);
            if (circle > expectedMax[level])
                Equal(0, SpiritSpellcastingMath.DailySlots(count, circle, 40, 40, 40, true, 100),
                    spirit + " unavailable circle stays closed despite CHA/extra slots " + level + "/" + circle);
        }
        if (boundaryCasts.TryGetValue(level, out var expectedCasts))
            for (var circle = 1; circle <= expectedCasts.Length; circle++)
                Equal(expectedCasts[circle - 1], SpiritSpellcastingMath.DailySlots(rows[level][circle],
                    circle, 18, 18, 18, true, 0), spirit + " CHA 18 boundary " + level + "/" + circle);
    }
}

// Isolated native-rule expectations, chosen to distinguish common incorrect formulas.
Slots("CHA 10 cannot cast first circle", 0, 1, 1, 10, 10, 10, true, 0);
Slots("CHA 11 meets first-circle threshold without bonus", 1, 1, 1, 11, 11, 11, true, 0);
Slots("CHA 12 earns first-circle bonus", 2, 1, 1, 12, 12, 12, true, 0);
Slots("CHA 18 earns only one first-circle bonus", 2, 1, 1, 18, 18, 18, true, 0);
Slots("CHA 20 earns two first-circle bonuses", 3, 1, 1, 20, 20, 20, true, 0);
Slots("CHA 22 earns two second-circle bonuses", 4, 2, 2, 22, 22, 22, true, 0);
Slots("temporary CHA cannot unlock casting", 0, 1, 1, 18, 10, 10, true, 4);
Slots("temporary CHA grants no extra daily slots", 2, 1, 1, 26, 12, 12, true, 0);
Slots("permanent CHA is used for player bonus", 4, 2, 2, 22, 22, 12, true, 0);
Slots("NPC uses base attribute for bonus", 2, 2, 2, 22, 22, 12, false, 0);
Slots("NPC still checks permanent casting threshold", 0, 2, 2, 22, 11, 22, false, 4);
Slots("attribute damage blocks casting despite permanent CHA", 0, 2, 2, 11, 22, 22, true, 4);
Slots("sixth-circle threshold CHA 16", 1, 1, 6, 16, 16, 16, true, 0);
Slots("sixth-circle threshold fails at CHA 15", 0, 1, 6, 15, 18, 18, true, 4);
Slots("extra slots combine with attribute bonus", 9, 4, 2, 18, 18, 18, true, 4);
Slots("unavailable circle ignores extra slots", 0, -1, 2, 30, 30, 30, true, 4);
Slots("zero table count allows earned bonus", 1, 0, 1, 12, 12, 12, true, 0);
Slots("zero table count allows extra slots", 4, 0, 1, 11, 11, 11, true, 4);
Slots("zero circle has no attribute bonus", 0, 0, 0, 40, 40, 40, true, 0);
Slots("zero circle preserves explicit extra slots", 3, 0, 0, 40, 40, 40, true, 3);
Slots("seventh circle is never unlocked", 0, 5, 7, 40, 40, 40, true, 4);
Slots("negative circle is rejected", 0, 5, -1, 40, 40, 40, true, 4);

Equal(0, SpiritSpellcastingMath.MaxSpellLevel(null!), "null progression has no positive circle");
Equal(0, SpiritSpellcastingMath.MaxSpellLevel([]), "empty progression has no positive circle");
Equal(1, SpiritSpellcastingMath.MaxSpellLevel([0, 0, -1, -1]), "zero slots differs from unavailable circle");
Equal(2, SpiritSpellcastingMath.MaxSpellLevel([0, 1, 1, -1]), "negative trailing entry does not unlock circle");
Equal(6, SpiritSpellcastingMath.MaxSpellLevel([0, 1, 1, 1, 1, 1, 1, 1, 1, 1]), "progression capped at circle six");

var gamePath = args.Length > 0 ? args[0] : null;
if (gamePath != null)
{
    using var assembly = AssemblyDefinition.ReadAssembly(gamePath);
    var spellbook = Type("Kingmaker.UnitLogic.Spellbook");
    var ui = Type("Kingmaker.UI.Common.UIUtilityUnit");
    var descriptor = Type("Kingmaker.UnitLogic.UnitDescriptor");
    var levelUpHandler = Type("Kingmaker.PubSubSystem.IUnitReapplyFeaturesOnLevelUpHandler");
    var actionBar = Type("Kingmaker.UI.MVVM._VM.ActionBar.ActionBarSpellbookHelper");
    Signature(spellbook, "GetSpellsPerDay", "System.Int32", "System.Int32");
    Signature(spellbook, "GetMaxSpellLevel", "System.Int32");
    Signature(spellbook, "GetLastSpellbookLevel", "System.Int32");
    Signature(spellbook, "get_RawBaseLevel", "System.Int32");
    Signature(spellbook, "AddBaseLevel", "System.Void");
    Signature(spellbook, "UpdateAllSlotsSize", "System.Void", "System.Boolean");
    Signature(spellbook, "UpdateSlotsSize", "System.Void", "System.Int32", "System.Boolean");
    Signature(spellbook, "Rest", "System.Void");
    Signature(spellbook, "Memorize", "System.Boolean", "Kingmaker.UnitLogic.Abilities.AbilityData", "Kingmaker.UnitLogic.SpellSlot");
    Signature(spellbook, "ForgetMemorized", "System.Void", "Kingmaker.UnitLogic.SpellSlot");
    Signature(spellbook, "CanSpend", "System.Boolean", "Kingmaker.UnitLogic.Abilities.AbilityData", "System.Boolean");
    Signature(spellbook, "Spend", "System.Boolean", "Kingmaker.UnitLogic.Abilities.AbilityData", "System.Boolean");
    Signature(spellbook, "SpendInternal", "System.Boolean", "Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility",
        "Kingmaker.UnitLogic.Abilities.AbilityData", "System.Boolean", "System.Boolean");
    Signature(spellbook, "GetAvailableForCastSpellCount", "System.Int32", "Kingmaker.UnitLogic.Abilities.AbilityData");
    Signature(spellbook, "RemoveCustomSpell", "System.Void", "Kingmaker.UnitLogic.Abilities.AbilityData");
    var nativeSpend = spellbook.Methods.Single(m => m.Name == "SpendInternal");
    Check(!Calls(nativeSpend, "Kingmaker.UnitLogic.Spellbook", "IsKnownOnLevel"),
        "native spending does not verify that cached temporary spells are still known");
    Signature(ui, "GetSpellNumberBaseTable", "System.Collections.Generic.List`1<System.Int32>", "Kingmaker.UnitLogic.Spellbook");
    Signature(descriptor, "ApplyPostLoadFixes", "System.Void");
    Signature(levelUpHandler, "HandleUnitReapplyFeaturesOnLevelUp", "System.Void");
    Signature(descriptor, "DemandSpellbook", "Kingmaker.UnitLogic.Spellbook", "Kingmaker.Blueprints.Classes.Spells.BlueprintSpellbook");
    Signature(actionBar, "TryAddAbility", "System.Void",
        "System.Collections.Generic.List`1<Kingmaker.UnitLogic.Abilities.AbilityData>",
        "Kingmaker.UnitLogic.Abilities.AbilityData");
    Signature(actionBar, "TryAddSpell", "System.Void",
        "System.Collections.Generic.List`1<Kingmaker.UnitLogic.SpellSlot>", "Kingmaker.UnitLogic.SpellSlot");
    Check(spellbook.Methods.Single(m => m.Name == "UpdateSlotsSize").IsPrivate,
        "native zero-circle slot initialization requires the private instance method");
    Check(actionBar.Methods.Single(m => m.Name == "TryAddAbility").IsStatic,
        "native action-bar candidate filter is a static method");
    var lastLevel = spellbook.Methods.Single(m => m.Name == "GetLastSpellbookLevel");
    var baseTable = ui.Methods.Single(m => m.Name == "GetSpellNumberBaseTable");
    foreach (var method in new[] { lastLevel, baseTable })
    {
        Check(Calls(method, "Kingmaker.Blueprints.Classes.Spells.BlueprintSpellbook", "get_SpellsPerDay"), method.Name + " directly reads blueprint daily table");
        Check(!Calls(method, "Kingmaker.UnitLogic.Spellbook", "GetSpellsPerDay"), method.Name + " bypasses per-day patch");
    }
    var perDay = spellbook.Methods.Single(m => m.Name == "GetSpellsPerDay");
    Check(Calls(perDay, "Kingmaker.EntitySystem.Stats.ModifiableValueAttributeStat", "CalculatePermanentValueWithoutTempBuffs"), "native daily slots use permanent attribute");
    Check(perDay.Body.Instructions.Any(i => i.Operand is FieldReference f && f.DeclaringType.FullName == "Kingmaker.UnitLogic.Parts.UnitPartExtraSpellsPerDay" && f.Name == "BonusSpells"), "native extra daily slots source matches helper caller");
    Console.WriteLine("Native IL signature and bypass checks ran against: " + gamePath);

    TypeDefinition Type(string name) => assembly.MainModule.Types.Single(t => t.FullName == name);
    void Signature(TypeDefinition type, string name, string returnType, params string[] parameters)
    {
        Check(type.Methods.Any(m => m.Name == name && m.ReturnType.FullName == returnType &&
            m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters)),
            "native signature " + type.Name + "." + name + "(" + string.Join(", ", parameters) + ")");
    }
}
else
    Console.WriteLine("Native IL checks skipped: pass Assembly-CSharp.dll as the first argument.");

var modPath = args.Length > 1 ? args[1] : null;
if (modPath != null)
{
    // Static wiring checks complement pure arithmetic. They do not run Harmony or Unity.
    using var mod = AssemblyDefinition.ReadAssembly(modPath);
    const string rulesName = "MediumClass.Medium.MediumSpiritSpellbookRules";
    const string preparedName = "MediumClass.Medium.NewUnitParts.UnitPartMediumPreparedSpells";
    const string nativeBook = "Kingmaker.UnitLogic.Spellbook";
    const string upgradeInterface = "Kingmaker.PubSubSystem.IUnitReapplyFeaturesOnLevelUpHandler";
    var rules = ModType(rulesName);
    var perDayPatch = ModType("MediumClass.Medium.MediumSpiritSpellsPerDayPatch");
    var perDayPrefix = Method(perDayPatch, "Prefix");
    Check(Calls(perDayPrefix, rulesName, "TryGetProgression") &&
          Calls(perDayPrefix, "MediumClass.Medium.SpiritSpellcastingMath", "DailySlots"),
          "compiled per-day patch connects progression selection to tested production arithmetic");
    var progressionCalls = LocalCallGraph(Method(rules, "TryGetProgression"));
    Check(progressionCalls.Any(c => c.DeclaringType.FullName == "Kingmaker.UnitLogic.UnitProgressionData" && c.Name == "GetClassLevel"),
          "compiled progression uses actual class level");
    Check(!progressionCalls.Any(c => c.DeclaringType.FullName == nativeBook &&
          (c.Name == "get_CasterLevel" || c.Name == "GetSpellsPerDay")),
          "compiled progression does not depend on another spellbook's caster level or daily casts");
    Check(!progressionCalls.Any(c => c.Name == "DemandSpellbook"),
          "compiled progression query never creates spellbooks");
    PatchTarget(perDayPatch, nativeBook, "GetSpellsPerDay");
    PatchTarget(ModType("MediumClass.Medium.MediumSpiritLastSpellbookLevelPatch"), nativeBook, "GetLastSpellbookLevel");
    PatchTarget(ModType("MediumClass.Medium.MediumSpiritBaseSpellNumberTablePatch"),
        "Kingmaker.UI.Common.UIUtilityUnit", "GetSpellNumberBaseTable");
    var actionBarPatch = ModType("MediumClass.Medium.MediumSpiritPreparationActionBarPatch");
    PatchTarget(actionBarPatch, "Kingmaker.UI.MVVM._VM.ActionBar.ActionBarSpellbookHelper", "TryAddAbility");
    var actionBarPrefix = Method(actionBarPatch, "Prefix");
    Check(actionBarPrefix.ReturnType.FullName == "System.Boolean" && actionBarPrefix.Parameters.Count == 1 &&
          actionBarPrefix.Parameters[0].Name == "__1" &&
          actionBarPrefix.Parameters[0].ParameterType.FullName == "Kingmaker.UnitLogic.Abilities.AbilityData",
        "compiled action-bar prefix filters the native second argument");
    Check(Calls(actionBarPrefix, rulesName, "IsPreparationBook"),
        "compiled action-bar filter identifies only the two preparation books");
    var memorizedBarPatch = ModType("MediumClass.Medium.MediumSpiritPreparationMemorizedActionBarPatch");
    PatchTarget(memorizedBarPatch, "Kingmaker.UI.MVVM._VM.ActionBar.ActionBarSpellbookHelper", "TryAddSpell");
    var memorizedBarPrefix = Method(memorizedBarPatch, "Prefix");
    Check(memorizedBarPrefix.ReturnType.FullName == "System.Boolean" && memorizedBarPrefix.Parameters.Count == 1 &&
          memorizedBarPrefix.Parameters[0].Name == "__1" &&
          memorizedBarPrefix.Parameters[0].ParameterType.FullName == "Kingmaker.UnitLogic.SpellSlot" &&
          Calls(memorizedBarPrefix, rulesName, "IsPreparationBook"),
        "compiled separate memorized-slot filter targets the native second argument and only preparation books");
    var preparation = Method(rules, "InitializePreparationBook");
    var preparationInstructions = preparation.Body.Instructions;
    Check(preparationInstructions.Any(i => i.Operand is MethodReference call &&
          call.DeclaringType.FullName == "Kingmaker.Blueprints.Classes.Spells.BlueprintSpellList" &&
          call.Name == "GetSpells" && IsZero(i.Previous)),
        "compiled preparation initialization reads the zero-circle spell list");
    Check(Calls(preparation, nativeBook, "IsKnownOnLevel") && Calls(preparation, nativeBook, "AddKnown"),
        "compiled preparation initialization adds missing known cantrips");
    Check(rules.Fields.Any(f => f.Name == "UpdatePreparationSlots" &&
          f.FieldType.FullName == "System.Reflection.MethodInfo"),
        "compiled private slot updater caches framework MethodInfo instead of a Harmony delegate");
    var initializer = Method(rules, ".cctor");
    Check(initializer.Body.Instructions.Any(i => i.OpCode.Code == Code.Ldstr &&
          Equals(i.Operand, "UpdateSlotsSize")) &&
          initializer.Body.Instructions.Any(i => i.Operand is MethodReference call &&
              call.DeclaringType.FullName == "System.Type" && call.Name == "GetMethod" &&
              call.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] {
                  "System.String", "System.Reflection.BindingFlags", "System.Reflection.Binder",
                  "System.Type[]", "System.Reflection.ParameterModifier[]" })),
        "compiled slot updater binds the native private method through framework reflection");
    Check(initializer.Body.Instructions.Any(i =>
              (i.OpCode.Code == Code.Ldc_I4 || i.OpCode.Code == Code.Ldc_I4_S) && Convert.ToInt32(i.Operand) == 52) &&
          new[] { "System.Int32", "System.Boolean" }.All(type => initializer.Body.Instructions.Any(i =>
              i.OpCode.Code == Code.Ldtoken && i.Operand is TypeReference token && token.FullName == type)),
        "compiled reflection lookup specifies instance public/nonpublic and exact int/bool parameters");
    Check(!mod.MainModule.GetMemberReferences().OfType<MethodReference>().Any(call =>
              call.DeclaringType.FullName == "HarmonyLib.AccessTools" && call.Name == "MethodDelegate"),
        "complete production module does not reference Harmony MethodDelegate");
    var invokeIndex = preparationInstructions.ToList().FindIndex(i => i.Operand is MethodReference call &&
          call.Name == "Invoke" && call.DeclaringType.FullName == "System.Reflection.MethodBase" &&
          call.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.Object", "System.Object[]" }));
    var invokeSetup = preparationInstructions.Skip(Math.Max(0, invokeIndex - 18)).Take(18).ToList();
    Check(invokeIndex >= 0 && invokeSetup.Any(i => i.OpCode.Code == Code.Newarr &&
              i.Operand is TypeReference element && element.FullName == "System.Object" && i.Previous.OpCode.Code == Code.Ldc_I4_2) &&
          new[] { "System.Int32", "System.Boolean" }.All(type => invokeSetup.Any(i =>
              i.OpCode.Code == Code.Box && i.Operand is TypeReference boxed && boxed.FullName == type && IsZero(i.Previous))),
        "compiled preparation initialization updates zero-circle slots without refreshing availability");
    var loadPatch = ModType("MediumClass.Medium.MediumSpiritRestorePreparedSpellsPatch");
    PatchTarget(loadPatch, "Kingmaker.UnitLogic.UnitDescriptor", "ApplyPostLoadFixes");
    var applySpirits = ModType("MediumClass.NewComponents.ApplySpirits");
    Check(applySpirits.Interfaces.Any(i => i.InterfaceType.FullName == upgradeInterface),
          "compiled channel component subscribes to native upgrade lifecycle");
    var upgrade = Method(applySpirits, "HandleUnitReapplyFeaturesOnLevelUp");
    var load = Method(loadPatch, "Postfix");
    Check(!LocalCallGraph(load).Any(c => c.DeclaringType.FullName == rulesName && c.Name == "ClampRemainingSlots"),
        "compiled post-load restoration never truncates saved casts using a transient daily capacity");
    Check(Calls(load, rulesName, "NotifyActionBar"),
        "compiled post-load restoration still refreshes the action bar without changing saved casts");
    foreach (var lifecycle in new[] { load, upgrade })
        Check(LocalCallGraph(lifecycle).Any(c => c.DeclaringType.FullName == preparedName && c.Name == "Sync"),
            lifecycle.DeclaringType.Name + " connects to prepared-spell synchronization");
    var nonRefillEntries = new[]
    {
        load, upgrade, Method(ModType(preparedName), "Sync", 0),
        Method(rules, "MediumBook"), Method(rules, "EnsurePreparationBook"),
        Method(rules, "EnsurePreparationBooks"), Method(rules, "RefreshForAdditionalSpirit")
    };
    foreach (var entry in nonRefillEntries)
        Check(!LocalCallGraph(entry).Any(c => c.DeclaringType.FullName == nativeBook && c.Name == "Rest"),
            "compiled " + entry.DeclaringType.Name + "." + entry.Name + " does not call Rest through mod-owned paths");
    Check(Calls(Method(rules, "RefreshForChannel"), nativeBook, "Rest"),
          "explicit channel refresh retains the authorized daily-slot refill");
    Check(Calls(Method(ModType(preparedName), "SyncPreparedSpells"), rulesName, "EnsurePreparationBooks"),
          "compiled prepared-spell synchronization initializes the entire active book collection");
    foreach (var (patchName, entryName, nativeName, argumentName) in new[] {
        ("MediumSpiritPreparationCannotSpendAbilityPatch", "Postfix", "CanSpend", "__0"),
        ("MediumSpiritPreparationCannotSpendPatch", "Prefix", "Spend", "__0"),
        ("MediumSpiritPreparationCannotSpendInternalPatch", "Prefix", "SpendInternal", "__1") })
    {
        var patch = ModType("MediumClass.Medium." + patchName);
        PatchTarget(patch, nativeBook, nativeName);
        var entry = Method(patch, entryName);
        Check(entry.Parameters.Any(p => p.Name == argumentName && p.ParameterType.FullName == "Kingmaker.UnitLogic.Abilities.AbilityData")
            && Calls(entry, rulesName, "IsRevokedTemporarySpell"), "cached-spell guard uses correct native argument: " + nativeName);
    }
    var revoke = Method(ModType(preparedName), "Revoke");
    Check(Calls(revoke, nativeBook, "RemoveTemporarySpell") && revoke.Body.Instructions.Any(i => i.Operand is MethodReference c
        && c.Name == "RemoveSlot" && c.Parameters.Any(p => p.ParameterType.FullName == "Kingmaker.UnitLogic.Abilities.AbilityData")),
        "compiled revocation removes both temporary known spell and action-bar reference");
    Check(Calls(revoke, nativeBook, "RemoveCustomSpell") && Calls(revoke, "Kingmaker.UnitLogic.Abilities.AbilityData", "set_IsTemporary"),
        "compiled revocation marks and removes derived metamagic recipes");
    Console.WriteLine("Compiled mod static wiring checks ran against: " + modPath);

    TypeDefinition ModType(string name) => mod.MainModule.Types.Single(t => t.FullName == name);
    static MethodDefinition Method(TypeDefinition type, string name, int? parameterCount = null) =>
        type.Methods.Single(m => m.Name == name && (!parameterCount.HasValue || m.Parameters.Count == parameterCount));
    void PatchTarget(TypeDefinition type, string targetType, string targetMethod)
    {
        Check(type.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch" &&
            a.ConstructorArguments.Any(v => v.Value is TypeReference t && t.FullName == targetType) &&
            a.ConstructorArguments.Any(v => v.Value is string text && text == targetMethod)),
            "compiled Harmony target " + targetType + "." + targetMethod);
    }
    List<MethodReference> LocalCallGraph(MethodDefinition entry)
    {
        var result = new List<MethodReference>();
        var visited = new HashSet<string>();
        var pending = new Stack<MethodDefinition>();
        pending.Push(entry);
        while (pending.TryPop(out var method))
        {
            if (!visited.Add(method.FullName) || !method.HasBody) continue;
            foreach (var reference in method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>())
            {
                result.Add(reference);
                // Only follow mod-owned code. Native engine side effects require gameplay tests.
                if (!reference.DeclaringType.FullName.StartsWith("MediumClass.", StringComparison.Ordinal)) continue;
                var localType = mod.MainModule.GetType(reference.DeclaringType.FullName);
                var localMethod = localType?.Methods.FirstOrDefault(m => m.FullName == reference.FullName);
                if (localMethod != null) pending.Push(localMethod);
            }
        }
        return result;
    }
}
else
    Console.WriteLine("Compiled mod wiring checks skipped: pass MediumClass.dll as the second argument.");

Console.WriteLine("Offline regression checks: " + assertions + "; failures: " + failures.Count + ".");
Console.WriteLine("Scope: arithmetic helper, source progression data, optional native IL contracts and compiled mod static wiring. No Unity/gameplay execution.");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
return failures.Count == 0 ? 0 : 1;

void Check(bool condition, string label)
{
    assertions++;
    if (!condition) failures.Add(label);
}
void Equal(int expected, int actual, string label) => Check(expected == actual, label + ": expected " + expected + ", got " + actual);
void Slots(string label, int expected, int count, int circle, int modified, int permanent, int baseValue, bool player, int extra) =>
    Equal(expected, SpiritSpellcastingMath.DailySlots(count, circle, modified, permanent, baseValue, player, extra), label);
static bool Calls(MethodDefinition method, string type, string name) => method.HasBody && method.Body.Instructions.Any(i =>
    i.Operand is MethodReference reference && reference.DeclaringType.FullName == type && reference.Name == name);
static bool IsZero(Instruction? instruction) => instruction?.OpCode.Code == Code.Ldc_I4_0 ||
    (instruction?.OpCode.Code == Code.Ldc_I4 && Equals(instruction.Operand, 0));
static string FindSourceRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "MediumClass.csproj"))) return directory.FullName;
    throw new DirectoryNotFoundException("Cannot find MediumClass.csproj above test output directory.");
}
static int[][] ReadDailyTable(string path)
{
    var source = File.ReadAllText(path);
    var start = source.IndexOf("var SpellsPerDayTable", StringComparison.Ordinal);
    if (start < 0) throw new InvalidDataException("Missing SpellsPerDayTable in " + path);
    var end = source.IndexOf(".Configure();", start, StringComparison.Ordinal);
    if (end < 0) throw new InvalidDataException("Unterminated SpellsPerDayTable in " + path);
    return Regex.Matches(source[start..end], @"new\s+SpellsLevelEntry\s*\{\s*Count\s*=\s*new\s+int\[\]\s*\{([^}]*)\}")
        .Select(m => m.Groups[1].Value.Split(',').Select(value => int.Parse(value.Trim())).ToArray()).ToArray();
}
