using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using MediumClass.Medium;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

var source = FindSourceRoot();
var failures = new List<string>();
int checks = 0;
var mediumBlueprint = BlueprintTool.Register(Guids.MediumSpellbook, new BlueprintSpellbook
{
    Spontaneous = true,
    SpellsPerDay = ReadTable(Path.Combine(source, "Medium", "MediumSpellbook.cs"), "ConfigureSpellSlotsTable", true)
});
BlueprintTool.Register(Guids.Medium, new BlueprintCharacterClass());
BlueprintTool.Register(Guids.SpiritPower, new BlueprintFeature());
var spirits = new[] { ("Archmage", Guids.Archmage, Guids.ArchmageSpellbook), ("Hierophant", Guids.Hierophant, Guids.HierophantSpellbook) };
foreach (var (name, spiritId, bookId) in spirits)
{
    BlueprintTool.Register(spiritId, new BlueprintCharacterClass());
    var file = Path.Combine(source, "Medium", "Spirits", name, name + ".cs");
    var preparationBlueprint = BlueprintTool.Register(bookId, new BlueprintSpellbook { SpellsPerDay = ReadTable(file, "var SpellsPerDayTable"), SpellSlots = ReadTable(file, "var SpellSlotsTable") });
    for (int circle = 0; circle <= 6; circle++) preparationBlueprint.SpellList.Spells[circle] = [Spell(name + " selection " + circle)];
}

// Verify that the API-model blueprint IDs are the real IDs, not accidental stand-ins.
var guidsSource = File.ReadAllText(Path.Combine(source, "Utilities", "Guids.cs"));
foreach (var field in typeof(Guids).GetFields(BindingFlags.Public | BindingFlags.Static))
    Check(guidsSource.Contains($"string {field.Name} = \"{field.GetValue(null)}\""), "real GUID " + field.Name);

var harmony = new Harmony("MediumClass.LowLevelProductionScenarios");
harmony.PatchAll(Assembly.GetExecutingAssembly());
Check(Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m)?.Owners.Contains(harmony.Id) == true) >= 13,
    "all 13 spellbook and action-bar production Harmony targets are installed");

// Independent expected base daily counts for levels 1 through 6. Source files supply actual fixtures.
int[][] expectedSpiritDaily = [[0], [0, 1], [0, 2], [0, 3], [0, 3, 1], [0, 4, 2], [0, 4, 3]];
int[] expectedOrdinaryMax = [0, 0, 0, 0, 1, 1, 1];

foreach (var (name, spiritId, bookId) in spirits)
for (int level = 1; level <= 6; level++)
foreach (var initialPreparationLevel in new[] { -1, 0, Math.Max(0, level - 1) }.Distinct())
{
    string context = $"{name}, Medium {level}, initial preparation level {initialPreparationLevel}";
    var owner = NewOwner(level);
    var book = owner.DemandSpellbook(mediumBlueprint);
    book.RawBaseLevel = level;
    book.Rest();
    Equal(expectedOrdinaryMax[level], book.MaxSpellLevel, context + ": before channel max");
    Equal(level < 4 ? 0 : 2, book.GetSpellsPerDay(1), context + ": before channel first-circle casts (CHA 18)");
    Equal(0, book.GetSpellsPerDay(2), context + ": before channel second-circle casts");

    var prepBlueprint = BlueprintTool.Get<BlueprintSpellbook>(bookId);
    if (initialPreparationLevel >= 0) owner.DemandSpellbook(prepBlueprint).RawBaseLevel = initialPreparationLevel;
    owner.Unit.Ensure<UnitPartMedium>().PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(spiritId) };

    int demandsBeforeQueries = owner.DemandCalls;
    int booksBeforeQueries = owner.Books.Count;
    int expectedMax = level < 4 ? 1 : 2;
    Equal(expectedMax, book.MaxSpellLevel, context + ": channel max independent of preparation book state");
    Equal(6, book.GetLastSpellbookLevel(), context + ": final progression ceiling");
    var ui = UIUtilityUnit.GetSpellNumberBaseTable(book);
    for (int circle = 0; circle <= 6; circle++)
    {
        int baseCount = circle <= expectedMax ? expectedSpiritDaily[level][circle] : 0;
        Equal(baseCount, ui[circle], context + $": UI base circle {circle}");
        Equal(circle == 0 || circle > expectedMax ? 0 : baseCount + 1,
            book.GetSpellsPerDay(circle), context + $": daily capacity circle {circle}");
    }
    Equal(demandsBeforeQueries, owner.DemandCalls, context + ": read-only queries do not demand books");
    Equal(booksBeforeQueries, owner.Books.Count, context + ": read-only queries do not create books");

    MediumSpiritSpellbookRules.RefreshForChannel(owner);
    var prep = owner.GetSpellbook(prepBlueprint);
    Check(prep != null, context + ": channel creates preparation book when missing");
    Equal(level, prep.RawBaseLevel, context + ": channel repairs preparation book level");
    Check(prepBlueprint.SpellList.GetSpells(0).All(spell => prep.IsKnownOnLevel(spell, 0)), context + ": zero-circle list learned for preparation UI");
    for (int circle = 0; circle <= expectedMax; circle++)
        Equal(1, prep.PreparationSlots(circle), context + $": one selectable prepared spell circle {circle}");
    Equal(0, prep.PreparationSlots(expectedMax + 1), context + ": unavailable preparation circle stays closed");
    for (int circle = 1; circle <= expectedMax; circle++)
        Equal(expectedSpiritDaily[level][circle] + 1, book.Remaining(circle), context + $": channel refresh circle {circle}");

    var originalSlots = new List<SpellSlot>();
    var selectedSpells = new List<BlueprintAbility>();
    var cachedGrants = new List<AbilityData>();
    for (int circle = 0; circle <= expectedMax; circle++)
    {
        var spell = prepBlueprint.SpellList.GetSpells(circle).Single();
        var preparedAbility = new AbilityData { Blueprint = spell, SpellLevel = circle, Spellbook = prep };
        var slot = new SpellSlot { SpellLevel = circle };
        Check(prep.Memorize(preparedAbility, slot), context + $": prepare circle {circle}");
        originalSlots.Add(slot); selectedSpells.Add(spell);
        var granted = book.Known(spell);
        if (granted != null) { cachedGrants.Add(granted); owner.Unit.UISettings.Slots.Add(granted); }
        Check(granted?.IsTemporary == true && granted.SpellLevel == circle, context + $": automatically grant temporary known circle {circle}");
        Check(!prep.CanSpend(preparedAbility, true), context + $": preparation book cannot cast ability circle {circle}");
        Check(!prep.CanSpend(spell), context + $": preparation book cannot cast blueprint circle {circle}");
        Check(!prep.Spend(preparedAbility, true), context + $": preparation book refuses spend circle {circle}");
        Check(!prep.SpendInternal(spell, preparedAbility, true, false), context + $": preparation book refuses internal spend circle {circle}");
        Equal(0, prep.GetAvailableForCastSpellCount(preparedAbility), context + $": preparation book no cast count circle {circle}");
        // Keep collecting useful failures in the deliberately broken historical run.
        if (granted == null) continue;
        foreach (bool preparationFirst in new[] { true, false })
        {
            var actionBar = new List<AbilityData>();
            foreach (var candidate in preparationFirst ? new[] { preparedAbility, granted } : new[] { granted, preparedAbility })
                ActionBarSpellbookHelper.TryAddAbility(actionBar, candidate);
            Check(actionBar.Count == 1 && actionBar[0].Spellbook == book,
                context + $": action bar retains castable Medium spell circle {circle}, preparation-first={preparationFirst}");
            var memorizedBar = new List<SpellSlot>();
            var spontaneousBar = new List<AbilityData>();
            if (preparationFirst) ActionBarSpellbookHelper.TryAddSpell(memorizedBar, slot);
            ActionBarSpellbookHelper.TryAddAbility(spontaneousBar, granted);
            if (!preparationFirst) ActionBarSpellbookHelper.TryAddSpell(memorizedBar, slot);
            Check(memorizedBar.Count == 0 && spontaneousBar.Count == 1 && spontaneousBar[0].Spellbook == book,
                context + $": memorized-slot action bar retains Medium spell circle {circle}, preparation-first={preparationFirst}");
        }
        int beforeCast = book.Remaining(circle);
        Check(book.CanSpend(granted, true) && book.Spend(granted, true), context + $": main book casts selected circle {circle}");
        Equal(beforeCast - (circle == 0 ? 0 : 1), book.Remaining(circle), context + $": main book spends only its pool circle {circle}");
    }

    var savedRemaining = JsonSerializer.Serialize(book.SaveRemaining());
    book.LoadRemaining(JsonSerializer.Deserialize<int[]>(savedRemaining));
    owner.ApplyPostLoadFixes();
    Equal(savedRemaining, JsonSerializer.Serialize(book.SaveRemaining()), context + ": load-sync preserves spent slots");
    Check(selectedSpells.All(spell => book.Known(spell) != null), context + ": load-sync preserves prepared grants");

    // Change the first-circle selection to a spell already intrinsically known by the Medium.
    var permanent = Spell("permanently known first-circle spell");
    book.AddKnownPermanent(1, permanent);
    prep.ForgetMemorized(originalSlots[1]);
    Check(!book.IsKnownOnLevel(selectedSpells[1], 1), context + ": forgetting selection removes old temporary known");
    AssertRevoked(book, cachedGrants.Single(s => s.SpellLevel == 1), context + ": forgotten selection");
    Check(prep.Memorize(new() { Blueprint = permanent, SpellLevel = 1 }, new() { SpellLevel = 1 }), context + ": select intrinsic known");
    Check(book.Known(permanent)?.IsTemporary == false, context + ": selecting intrinsic known does not convert it");

    // A fresh channel restores its own full capacities; dechannel must clamp rather than replenish.
    MediumSpiritSpellbookRules.RefreshForChannel(owner);
    owner.Unit.Get<UnitPartMedium>().PrimarySpirit = null;
    owner.Unit.Get<UnitPartMediumPreparedSpells>().Clear();
    MediumSpiritSpellbookRules.ClampRemainingSlots(owner);
    Equal(expectedOrdinaryMax[level], book.MaxSpellLevel, context + ": dechannel returns to ordinary max");
    Equal(level < 4 ? 0 : 2, book.Remaining(1), context + ": dechannel clamps first-circle pool");
    Equal(0, book.Remaining(2), context + ": dechannel removes second-circle pool");
    Check(selectedSpells.All(spell => book.Known(spell) == null), context + ": dechannel removes only temporary selections");
    foreach (var cached in cachedGrants) AssertRevoked(book, cached, context + ": dechannel circle " + cached.SpellLevel);
    Check(book.Known(permanent)?.IsTemporary == false, context + ": dechannel preserves intrinsic known");
    var beforeInactiveLoad = JsonSerializer.Serialize(book.SaveRemaining());
    owner.ApplyPostLoadFixes();
    Equal(beforeInactiveLoad, JsonSerializer.Serialize(book.SaveRemaining()), context + ": inactive load does not refill");
}

// The action-bar filter is narrowly scoped to the two preparation-book IDs.
{
    var owner = NewOwner(4);
    var unrelated = owner.DemandSpellbook(new BlueprintSpellbook { AssetGuid = new BlueprintGuid(Guid.NewGuid()), SpellsPerDay = mediumBlueprint.SpellsPerDay });
    var ordinary = new AbilityData { Blueprint = Spell("unrelated spellbook ability"), SpellLevel = 1, Spellbook = unrelated };
    var abilityWithoutBook = new AbilityData { Blueprint = Spell("spell-like ability without book"), SpellLevel = 1 };
    var entries = new List<AbilityData>();
    ActionBarSpellbookHelper.TryAddAbility(entries, ordinary);
    ActionBarSpellbookHelper.TryAddAbility(entries, abilityWithoutBook);
    Check(entries.SequenceEqual(new[] { ordinary, abilityWithoutBook }), "action bar retains unrelated books and spell-like abilities");
    var memorizedOrdinary = new AbilityData { Blueprint = Spell("unrelated depleted memorized spell"), SpellLevel = 1, Spellbook = unrelated };
    var memorizedEntries = new List<SpellSlot>();
    ActionBarSpellbookHelper.TryAddSpell(memorizedEntries, new SpellSlot { SpellShell = memorizedOrdinary, SpellLevel = 1 });
    Check(memorizedEntries.Any(slot => slot.SpellShell == memorizedOrdinary), "action bar retains unrelated memorized spells even when their book has no casts");
}

// Explicit early-save repairs, forfeited lesser powers, and CHA casting thresholds.
foreach (var (name, spiritId, bookId) in spirits)
for (int level = 1; level <= 6; level++)
{
    string context = name + " edge level " + level;
    var owner = NewOwner(level);
    owner.Unit.Ensure<UnitPartMedium>().PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(spiritId) };
    MediumSpiritSpellbookRules.RefreshForChannel(owner);
    var book = owner.GetSpellbook(mediumBlueprint);
    Check(book != null, context + ": missing main book recovered");
    Equal(level, book.RawBaseLevel, context + ": recovered main book class level");
    book.RawBaseLevel = 0;
    MediumSpiritSpellbookRules.RefreshForChannel(owner);
    Equal(level, book.RawBaseLevel, context + ": stale main book repaired");
    owner.Stats.Charisma.ModifiedValue = owner.Stats.Charisma.PermanentValue = owner.Stats.Charisma.BaseValue = 10;
    MediumSpiritSpellbookRules.RefreshForChannel(owner);
    Equal(0, book.GetSpellsPerDay(1), context + ": CHA 10 blocks first circle");
    Equal(0, book.Remaining(1), context + ": low-CHA refresh grants no slots");
    Check(!book.CanSpend(new() { Blueprint = Spell("low charisma"), SpellLevel = 1 }, true), context + ": low-CHA cannot cast");
    if (level >= 4)
    {
        owner.Stats.Charisma.ModifiedValue = owner.Stats.Charisma.PermanentValue = owner.Stats.Charisma.BaseValue = 11;
        MediumSpiritSpellbookRules.RefreshForChannel(owner);
        Equal(expectedSpiritDaily[level][1], book.GetSpellsPerDay(1), context + ": CHA 11 first circle without bonus");
        Equal(0, book.GetSpellsPerDay(2), context + ": CHA 11 blocks second circle");
    }
    owner.Unit.Get<UnitPartMedium>().ForgonePowers = 1;
    Equal(expectedOrdinaryMax[level], book.MaxSpellLevel, context + ": forfeited lesser power disables six-circle progression");
    owner.Unit.Get<UnitPartMediumPreparedSpells>().Sync();
    MediumSpiritSpellbookRules.ClampRemainingSlots(owner);
    owner.ApplyPostLoadFixes();
    Equal(0, book.Remaining(2), context + ": explicit forfeiture clamps higher pool and loading preserves it");
}

// Multiple spirits share one casting pool. Opening caster progression later grants
// only the extra capacity, while preparing in either book contributes to one union.
foreach (int level in new[] { 1, 2, 3, 4, 5, 6, 13, 20 })
foreach (bool archmageFirst in new[] { true, false })
{
    var ordered = archmageFirst ? spirits : spirits.Reverse().ToArray();
    string context = $"multi-spirit level {level}, {ordered[0].Item1} first";
    var owner = NewOwner(level);
    var state = owner.Unit.Ensure<UnitPartMedium>();
    state.PrimarySpirit = new() { Blueprint = new BlueprintCharacterClass { AssetGuid = new BlueprintGuid(Guid.NewGuid()) } };
    var book = MediumSpiritSpellbookRules.MediumBook(owner);
    book.Rest();
    int originalMax = book.MaxSpellLevel;
    var originalCapacity = Enumerable.Range(0, 11).Select(book.GetSpellsPerDay).ToArray();
    // Spend a base-book cast where available before the caster spirit is added.
    var intrinsic = book.AddKnownPermanent(1, Spell("ordinary known"));
    bool spentBeforeChannel = book.Spend(intrinsic, true);
    var originalRemaining = book.SaveRemaining();
    var firstSnapshot = MediumSpiritSpellbookRules.CaptureChannelSlots(owner);
    state.AdditionalSpirits.Add(new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(ordered[0].Item2) });
    MediumSpiritSpellbookRules.RefreshForAdditionalSpirit(owner, firstSnapshot);
    Equal(6, book.GetLastSpellbookLevel(), context + ": additional caster opens six-circle ceiling");
    Check(book.MaxSpellLevel > originalMax, context + ": additional caster opens correct higher circle");
    for (int circle = 1; circle <= 10; circle++)
        Equal(originalRemaining[circle] + Math.Max(0, book.GetSpellsPerDay(circle) - originalCapacity[circle]),
            book.Remaining(circle), context + $": only capacity increase granted circle {circle}");
    if (spentBeforeChannel)
        Equal(book.GetSpellsPerDay(1) - 1, book.Remaining(1), context + ": ordinary spent cast remains spent");

    var firstPrep = owner.GetSpellbook(BlueprintTool.Get<BlueprintSpellbook>(ordered[0].Item3));
    var firstSpell = firstPrep.Blueprint.SpellList.GetSpells(1).Single();
    var firstSlot = new SpellSlot { SpellLevel = 1 };
    Check(firstPrep.Memorize(new() { Blueprint = firstSpell, SpellLevel = 1, Spellbook = firstPrep }, firstSlot), context + ": prepare first book");
    Check(book.Spend(book.Known(firstSpell), true), context + ": cast before second caster spirit");
    var spentBeforeSecond = book.SaveRemaining();
    var secondSnapshot = MediumSpiritSpellbookRules.CaptureChannelSlots(owner);
    state.AdditionalSpirits.Add(new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(ordered[1].Item2) });
    MediumSpiritSpellbookRules.RefreshForAdditionalSpirit(owner, secondSnapshot);
    Equal(JsonSerializer.Serialize(spentBeforeSecond), JsonSerializer.Serialize(book.SaveRemaining()),
        context + ": second preparation book never refills the shared pool");
    Equal(2, MediumSpiritSpellbookRules.ActivePreparationBlueprints(owner).Count(), context + ": both preparation blueprints active");
    var secondPrep = owner.GetSpellbook(BlueprintTool.Get<BlueprintSpellbook>(ordered[1].Item3));
    Equal(level, secondPrep.RawBaseLevel, context + ": second book initialized to actual class level");
    Equal(1, secondPrep.PreparationSlots(0), context + ": additional book has cantrip preparation slot");
    var secondSpell = secondPrep.Blueprint.SpellList.GetSpells(1).Single();
    var secondSlot = new SpellSlot { SpellLevel = 1 };
    Check(secondPrep.Memorize(new() { Blueprint = secondSpell, SpellLevel = 1, Spellbook = secondPrep }, secondSlot), context + ": prepare second book");
    Check(book.Known(firstSpell) != null && book.Known(secondSpell) != null, context + ": both prepared selections in main book");

    // The same spell in both preparation books remains until both copies are removed.
    secondPrep.ForgetMemorized(secondSlot);
    Check(secondPrep.Memorize(new() { Blueprint = firstSpell, SpellLevel = 1, Spellbook = secondPrep }, secondSlot), context + ": overlapping selection");
    var sharedGrant = book.Known(firstSpell);
    owner.Unit.UISettings.Slots.Add(sharedGrant);
    firstPrep.ForgetMemorized(firstSlot);
    Check(book.Known(firstSpell)?.IsTemporary == true, context + ": other book retains shared selection");
    Check(owner.Unit.UISettings.Slots.Contains(sharedGrant) && !MediumSpiritSpellbookRules.IsRevokedTemporarySpell(book, sharedGrant), context + ": overlapping selection preserves valid shortcut");
    secondPrep.ForgetMemorized(secondSlot);
    Check(book.Known(firstSpell) == null && book.Known(secondSpell) == null, context + ": last selection removes owned temporary spell");
    AssertRevoked(book, sharedGrant, context + ": last overlapping selection");
    Check(book.Known(intrinsic.Blueprint)?.IsTemporary == false, context + ": union cleanup preserves intrinsic spell");

    // Ending only one caster spirit preserves the other's book and six-circle table.
    Check(firstPrep.Memorize(new() { Blueprint = firstSpell, SpellLevel = 1, Spellbook = firstPrep }, firstSlot), context + ": restore first selection");
    Check(secondPrep.Memorize(new() { Blueprint = secondSpell, SpellLevel = 1, Spellbook = secondPrep }, secondSlot), context + ": restore second selection");
    state.AdditionalSpirits.RemoveAt(0);
    owner.Unit.Get<UnitPartMediumPreparedSpells>().Sync();
    Check(book.Known(firstSpell) == null && book.Known(secondSpell) != null, context + ": departed spirit selections removed individually");
    Equal(6, book.GetLastSpellbookLevel(), context + ": remaining caster maintains six-circle progression");
    var savedSlots = JsonSerializer.Serialize(book.SaveRemaining());
    owner.ApplyPostLoadFixes();
    Equal(savedSlots, JsonSerializer.Serialize(book.SaveRemaining()), context + ": multi load preserves spent casts");
    Check(book.Known(secondSpell) != null, context + ": multi load preserves surviving selection");
    var beforeEnd = book.SaveRemaining();
    state.AdditionalSpirits.Clear();
    owner.Unit.Get<UnitPartMediumPreparedSpells>().Clear();
    MediumSpiritSpellbookRules.ClampRemainingSlots(owner);
    owner.ApplyPostLoadFixes();
    Equal(originalMax, book.MaxSpellLevel, context + ": removing last caster returns to ordinary progression");
    Check(book.Known(secondSpell) == null, context + ": removing last caster clears remaining grants");
    for (int circle = 1; circle <= 10; circle++)
        Equal(Math.Min(beforeEnd[circle], originalCapacity[circle]), book.Remaining(circle),
            context + $": explicit end clamps circle {circle} without refilling; load preserves it");
}

// Cached conversions, permanent promotion and unrelated features keep their ownership.
{
    var owner = NewOwner(6);
    var state = owner.Unit.Ensure<UnitPartMedium>();
    state.PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(Guids.Archmage) };
    MediumSpiritSpellbookRules.RefreshForChannel(owner);
    var book = owner.GetSpellbook(mediumBlueprint);
    var prep = owner.GetSpellbook(BlueprintTool.Get<BlueprintSpellbook>(Guids.ArchmageSpellbook));
    var root = Spell("parent with variants");
    var slot = new SpellSlot { SpellLevel = 1 };
    prep.Memorize(new() { Blueprint = root, SpellLevel = 1, Spellbook = prep }, slot);
    var granted = book.Known(root);
    // The native metamagic builder stores the base circle but copies neither
    // IsTemporary nor ConvertedFrom. Recreate that independently of production.
    var recipe = new AbilityData { Blueprint = root, SpellLevel = 2, SpellLevelInSpellbook = 1, Spellbook = book, MetamagicData = new() };
    book.AddCustomSpell(recipe); owner.Unit.UISettings.Slots.Add(recipe);
    Check(book.CanSpend(recipe, true), "metamagic made from current temporary selection can cast");
    var recipeConversion = new AbilityData { Blueprint = Spell("metamagic conversion"), SpellLevel = 2, Spellbook = book, ConvertedFrom = recipe };
    var variant = new AbilityData { Blueprint = Spell("variant"), SpellLevel = 1, Spellbook = book, IsTemporary = true };
    variant.Blueprint.Parent = root;
    Check(book.CanSpend(variant, true), "known temporary parent permits variant");
    var conversion = new AbilityData { Blueprint = Spell("conversion"), SpellLevel = 1, Spellbook = book, ConvertedFrom = granted };
    Check(book.CanSpend(conversion, true), "known temporary source permits conversion");
    prep.ForgetMemorized(slot);
    Check(!book.GetCustomSpells(2).Contains(recipe) && recipe.IsTemporary, "revocation removes recipe and marks its cached source");
    AssertRevoked(book, recipe, "forgotten temporary source revokes native metamagic recipe");
    AssertRevoked(book, recipeConversion, "forgotten temporary source revokes recipe conversion");
    AssertRevoked(book, variant, "forgotten parent revokes cached variant");
    AssertRevoked(book, conversion, "forgotten source revokes cached conversion");
    prep.Memorize(new() { Blueprint = root, SpellLevel = 1, Spellbook = prep }, slot);
    Check(book.CanSpend(granted, true), "preparing same blueprint again permits still-equivalent cached source");
    var promoted = book.AddKnownPermanent(1, root);
    var permanentRecipe = new AbilityData { Blueprint = root, SpellLevel = 2, SpellLevelInSpellbook = 1, Spellbook = book, MetamagicData = new() };
    book.AddCustomSpell(permanentRecipe); owner.Unit.UISettings.Slots.Add(permanentRecipe);
    owner.Unit.UISettings.Slots.Add(promoted);
    prep.ForgetMemorized(slot);
    Check(book.CanSpend(promoted, true) && owner.Unit.UISettings.Slots.Contains(promoted), "permanent promotion retains cast and shortcut");
    Check(book.GetCustomSpells(2).Contains(permanentRecipe) && book.CanSpend(permanentRecipe, true)
        && owner.Unit.UISettings.Slots.Contains(permanentRecipe), "permanent promotion retains derived metamagic");
    var otherFeatureGrant = book.AddKnownTemporary(1, Spell("other feature grant"));
    owner.Unit.Get<UnitPartMediumPreparedSpells>().Clear();
    Check(book.CanSpend(otherFeatureGrant, true), "unowned active temporary grant stays castable");
    var differentBase = Spell("same blueprint available at different base circles");
    prep.Memorize(new() { Blueprint = differentBase, SpellLevel = 1, Spellbook = prep }, slot);
    book.AddKnownPermanent(2, differentBase);
    var temporaryRecipe = new AbilityData { Blueprint = differentBase, SpellLevel = 2, SpellLevelInSpellbook = 1, Spellbook = book, MetamagicData = new() };
    var intrinsicRecipe = new AbilityData { Blueprint = differentBase, SpellLevel = 3, SpellLevelInSpellbook = 2, Spellbook = book, MetamagicData = new() };
    book.AddCustomSpell(temporaryRecipe); book.AddCustomSpell(intrinsicRecipe);
    prep.ForgetMemorized(slot);
    AssertRevoked(book, temporaryRecipe, "different-base temporary metamagic revoked");
    Check(book.GetCustomSpells(3).Contains(intrinsicRecipe) && !intrinsicRecipe.IsTemporary,
        "same blueprint metamagic derived from other permanent base circle retained");
    var unrelated = owner.DemandSpellbook(new BlueprintSpellbook { AssetGuid = new BlueprintGuid(Guid.NewGuid()), SpellsPerDay = mediumBlueprint.SpellsPerDay });
    unrelated.RawBaseLevel = 6; unrelated.Rest();
    var unrelatedGrant = unrelated.AddKnownTemporary(1, Spell("unrelated removed spell"));
    unrelated.RemoveTemporarySpell(unrelatedGrant);
    Check(unrelated.CanSpend(unrelatedGrant, true), "revoked-spell guard leaves unrelated books untouched");
    var cycle = new AbilityData { Blueprint = root, SpellLevel = 1, Spellbook = book, IsTemporary = true };
    cycle.ConvertedFrom = cycle;
    Check(book.CanSpend(cycle, true), "malformed conversion cycle terminates and retains known parent");
}

ActionBarRefreshScenarios.Run(mediumBlueprint, Check);
LoadSlotPreservationScenarios.Run(mediumBlueprint, Check);

Console.WriteLine($"Production-code Harmony scenarios: {checks - failures.Count}/{checks} assertions passed.");
Console.WriteLine("Scope: linked production patches and prepared-spell synchronization on an explicit game API model; no Unity, game UI, actual save serializer, or in-game execution.");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

void Check(bool condition, string label) { checks++; if (!condition) failures.Add(label); }
void Equal<T>(T expected, T actual, string label) => Check(EqualityComparer<T>.Default.Equals(expected, actual), label + $" (expected {expected}; actual {actual})");
void AssertRevoked(Spellbook book, AbilityData spell, string context)
{
    var before = JsonSerializer.Serialize(book.SaveRemaining());
    Check(!book.Owner.Unit.UISettings.Slots.Contains(spell), context + ": obsolete shortcut removed");
    Check(!book.CanSpend(spell, true), context + ": cached source cannot cast");
    Equal(0, book.GetAvailableForCastSpellCount(spell), context + ": cached source shows no casts");
    Check(!book.Spend(spell, true), context + ": cached source cannot spend");
    Check(!book.SpendInternal(spell.Blueprint, spell, true, false), context + ": internal spend refuses stale source");
    Equal(before, JsonSerializer.Serialize(book.SaveRemaining()), context + ": refusal preserves spell slots");
}
UnitDescriptor NewOwner(int level) { var owner = new UnitDescriptor(); owner.Progression.MediumLevel = level; return owner; }
BlueprintAbility Spell(string name) => new() { AssetGuid = new BlueprintGuid(Guid.NewGuid()), Name = name };
string FindSourceRoot()
{
    for (var candidate = new DirectoryInfo(AppContext.BaseDirectory); candidate != null; candidate = candidate.Parent)
        if (File.Exists(Path.Combine(candidate.FullName, "MediumClass.csproj"))) return candidate.FullName;
    throw new DirectoryNotFoundException("Source root not found.");
}
BlueprintSpellsTable ReadTable(string file, string startMarker, bool mediumSyntax = false)
{
    var text = File.ReadAllText(file);
    text = text[text.IndexOf(startMarker, StringComparison.Ordinal)..];
    text = text[..text.IndexOf(".Configure()", StringComparison.Ordinal)];
    var pattern = mediumSyntax ? @"CreateSpellLevelEntry\(([^)]*)\)" : @"Count\s*=\s*new int\[\]\s*\{([^}]*)\}";
    var entries = Regex.Matches(text, pattern).Select(match => new SpellsLevelEntry
    {
        Count = match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
    }).ToArray();
    if (entries.Length != 21) throw new InvalidDataException($"Expected 21 source table rows from {file}, got {entries.Length}.");
    return new BlueprintSpellsTable { Levels = entries };
}
