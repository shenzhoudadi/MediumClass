using System.Runtime.CompilerServices;
using System.Reflection;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using MediumClass.Medium;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

// This process deliberately does not install any Harmony patches. It exercises the
// production rules initializer and direct calls against the game's old Harmony DLL.
// The game-facing types are explicit API doubles; this is not a Unity gameplay test.
bool expectMissingMethod = args.Contains("--expect-missing-method");
// Old Harmony's bundled dynamic-method emitter predates .NET 9. Its existing Cecil
// backend lets this modern test host execute FieldRefAccess without replacing the
// Harmony DLL or changing production code. This is a host setting, not a game fix.
Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", "cecil");
Console.WriteLine("Runtime Harmony: " + typeof(AccessTools).Assembly.GetName().Version);
try
{
    int checks = RunSmoke();
    if (expectMissingMethod)
        throw new InvalidOperationException("The old-source control unexpectedly passed; MissingMethodException was not reproduced.");
    Console.WriteLine($"PASS: {checks} production compatibility checks; no Harmony patch installation and no Unity gameplay execution.");
}
catch (Exception exception)
{
    var chain = new List<Exception>();
    for (var current = exception; current != null; current = current.InnerException) chain.Add(current);
    var missing = chain.OfType<MissingMethodException>().FirstOrDefault(e => e.Message.Contains("MethodDelegate"));
    if (expectMissingMethod && missing != null)
    {
        Console.WriteLine("EXPECTED OLD-SOURCE FAILURE: " + missing.GetType().Name + ": " + missing.Message);
        Console.WriteLine("The ordinary spellbook rules initializer failed before any channeling.");
    }
    else
    {
        Console.Error.WriteLine(exception);
        Environment.ExitCode = 1;
    }
}

[MethodImpl(MethodImplOptions.NoInlining)]
static int RunSmoke()
{
    RuntimeHelpers.RunClassConstructor(typeof(MediumSpiritSpellbookRules).TypeHandle);
    int checks = 1;
    void Check(bool condition, string label)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(label);
    }
    Check(typeof(AccessTools).Assembly.GetName().Version == new Version(2, 0, 4, 0),
        "The compatibility run must actually load the game's Harmony 2.0.4.0.");
    BlueprintTool.Register(Guids.Medium, new BlueprintCharacterClass());
    BlueprintTool.Register(Guids.SpiritPower, new BlueprintFeature());
    var main = BlueprintTool.Register(Guids.MediumSpellbook, new BlueprintSpellbook
    {
        SpellsPerDay = Table([0], [0], [0], [0], [0, 1], [0, 1], [0, 1])
    });
    var ordinary = new BlueprintSpellbook { SpellsPerDay = Table([0], [0, 1]) };
    var ordinaryOwner = new UnitDescriptor();
    var ordinaryBook = ordinaryOwner.DemandSpellbook(ordinary);
    Check(!MediumSpiritSpellbookRules.IsMediumBook(ordinaryBook), "ordinary class is not Medium");
    Check(!MediumSpiritSpellbookRules.IsPreparationBook(ordinaryBook), "ordinary class is not preparation");
    Check(!MediumSpiritSpellbookRules.TryGetProgression(ordinaryBook, out _, out _), "ordinary class retains its progression");
    var postfix = typeof(MediumSpiritMaxSpellLevelPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
    object[] ordinaryArguments = [ordinaryBook, 9];
    postfix.Invoke(null, ordinaryArguments);
    Check((int)ordinaryArguments[1] == 9, "real max-circle postfix leaves an ordinary book result intact");
    Check(ordinaryOwner.DemandCalls == 1, "ordinary read-only queries create no additional books");
    Check(!MediumSpiritSpellbookRules.IsMediumBook(null), "null book query is safe");
    Check(!MediumSpiritSpellbookRules.IsPreparationBook(null), "null preparation query is safe");
    foreach (var (spiritId, bookId) in new[] { (Guids.Archmage, Guids.ArchmageSpellbook), (Guids.Hierophant, Guids.HierophantSpellbook) })
    {
        var spirit = BlueprintTool.Register(spiritId, new BlueprintCharacterClass());
        var preparation = BlueprintTool.Register(bookId, new BlueprintSpellbook
        {
            SpellsPerDay = Table([0], [0, 1], [0, 2], [0, 3], [0, 3, 1], [0, 4, 2], [0, 4, 3]),
            SpellSlots = Table([0], [1, 1], [1, 1], [1, 1], [1, 1, 1], [1, 1, 1], [1, 1, 1])
        });
        var cantrip = new BlueprintAbility { Name = "compatibility cantrip" };
        preparation.SpellList.Spells[0] = [cantrip];
        for (int level = 1; level <= 6; level++)
        {
            var owner = new UnitDescriptor();
            owner.Progression.MediumLevel = level;
            var book = owner.DemandSpellbook(main);
            Check(MediumSpiritSpellbookRules.IsMediumBook(book), "main identified at level " + level);
            Check(!MediumSpiritSpellbookRules.TryGetProgression(book, out _, out _), "unchanneled Medium keeps ordinary progression");
            owner.Unit.Ensure<UnitPartMedium>().PrimarySpirit = new() { Blueprint = spirit };
            Check(MediumSpiritSpellbookRules.TryGetProgression(book, out var table, out int actualLevel), "channeled progression found");
            Check(ReferenceEquals(table, preparation.SpellsPerDay) && actualLevel == level, "actual class level chooses spirit table");
            Check(owner.Books.Count == 1 && owner.DemandCalls == 1, "progression lookup did not initialize preparation");
            var preparedBook = MediumSpiritSpellbookRules.EnsurePreparationBook(owner);
            Check(preparedBook.RawBaseLevel == level, "preparation initialized to actual class level");
            Check(MediumSpiritSpellbookRules.IsPreparationBook(preparedBook), "preparation identified");
            Check(preparedBook.IsKnownOnLevel(cantrip, 0), "cantrip known for preparation");
            Check(preparedBook.PreparationSlots(0) == 1, "framework reflection invoked private zero-circle slot update");
            Check(preparedBook.PreparationSlots(1) == 1, "first circle initialized");
            Check(preparedBook.PreparationSlots(2) == (level >= 4 ? 1 : 0), "second circle opens at class level four");
            MediumSpiritSpellbookRules.EnsurePreparationBook(owner);
            Check(preparedBook.PreparationSlots(0) == 1 && preparedBook.RawBaseLevel == level, "repeat initialization is stable");
        }
    }
    for (int level = 1; level <= 6; level++)
    {
        var owner = new UnitDescriptor();
        owner.Progression.MediumLevel = level;
        var state = owner.Unit.Ensure<UnitPartMedium>();
        state.PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(Guids.Archmage) };
        state.AdditionalSpirits.Add(new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(Guids.Hierophant) });
        Check(MediumSpiritSpellbookRules.ActivePreparationBlueprints(owner).Count() == 2, "both caster spirits active at level " + level);
        var books = MediumSpiritSpellbookRules.EnsurePreparationBooks(owner);
        Check(books.Count == 2 && books.All(book => book.RawBaseLevel == level), "both books initialize at actual class level");
        Check(books.All(book => book.PreparationSlots(0) == 1 && book.PreparationSlots(1) == 1), "both early preparation books expose cantrip and first circle");
        Check(books.All(book => book.Blueprint.SpellList.GetSpells(0).All(spell => book.IsKnownOnLevel(spell, 0))), "both cantrip lists learned through old Harmony runtime");
        Check(MediumSpiritSpellbookRules.CaptureChannelSlots(owner) == null, "existing caster progression never authorizes another slot refresh");
    }
    return checks;
}

static BlueprintSpellsTable Table(params int[][] rows) => new()
{
    Levels = rows.Select(row => new SpellsLevelEntry { Count = row }).ToArray()
};
