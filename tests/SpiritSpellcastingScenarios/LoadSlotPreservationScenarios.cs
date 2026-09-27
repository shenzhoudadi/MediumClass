using System.Text.Json;
using BlueprintCore.Utils;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UnitLogic;
using MediumClass.Medium;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

internal static class LoadSlotPreservationScenarios
{
    internal static void Run(BlueprintSpellbook mediumBlueprint, Action<bool, string> check)
    {
        // Exercise the production load postfix on fresh owners with serialized slot
        // counts. Attribute recovery is deliberately staged instead of assuming
        // every bonus is present when ApplyPostLoadFixes runs. This is an API model,
        // not a claim that a particular tester save has CHA 20 or this load order.
        foreach (int level in new[] { 1, 4, 7, 10, 20 })
        foreach (int channel in new[] { 0, 1, 2, 3 })
        foreach (int spent in new[] { 0, 1, 99 })
        foreach (bool delayedAttribute in new[] { false, true })
        {
            string context = $"load slot preservation / level {level} / channel {channel} / spent {spent} / delayed CHA {delayedAttribute}: ";
            void Assert(bool value, string message) => check(value, context + message);
            var original = Owner(20);
            var book = original.GetSpellbook(mediumBlueprint);
            book.Rest();
            var saved = book.SaveRemaining();
            for (int circle = 1; circle < saved.Length; circle++)
                saved[circle] = Math.Max(0, saved[circle] - spent);
            string serialized = JsonSerializer.Serialize(saved);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                var loaded = Owner(delayedAttribute ? 18 : 20);
                var restored = loaded.GetSpellbook(mediumBlueprint);
                restored.LoadRemaining(JsonSerializer.Deserialize<int[]>(serialized));
                int beforeFirst = restored.Remaining(1);
                int interimCapacity = restored.GetSpellsPerDay(1);
                loaded.ApplyPostLoadFixes();
                Assert(restored.SaveRemaining().SequenceEqual(saved), $"cycle {cycle} preserves every saved count during load");
                if (cycle == 0 && delayedAttribute && spent == 0 && beforeFirst > 0)
                    Assert(beforeFirst == interimCapacity + 1, "fixture exposes exactly one first-circle bonus missing during load");
                loaded.Stats.Charisma.ModifiedValue = loaded.Stats.Charisma.PermanentValue = 20;
                MediumSpiritSpellbookRules.SyncForActionBar(loaded);
                Assert(restored.SaveRemaining().SequenceEqual(saved), $"cycle {cycle} keeps spent casts after bonus recovery and UI sync");
                Assert(Enumerable.Range(1, 10).All(circle => restored.GetSpellsPerDay(circle) == book.GetSpellsPerDay(circle)),
                    $"cycle {cycle} restores the same maximum capacities");
                serialized = JsonSerializer.Serialize(restored.SaveRemaining());
            }

            UnitDescriptor Owner(int attribute)
            {
                var owner = new UnitDescriptor();
                owner.Progression.MediumLevel = level;
                owner.Stats.Charisma.BaseValue = 18;
                owner.Stats.Charisma.ModifiedValue = owner.Stats.Charisma.PermanentValue = attribute;
                var state = owner.Unit.Ensure<UnitPartMedium>();
                if (channel > 0)
                    state.PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(
                        channel == 2 ? Guids.Hierophant : Guids.Archmage) };
                if (channel == 3)
                    state.AdditionalSpirits.Add(new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(Guids.Hierophant) });
                owner.DemandSpellbook(mediumBlueprint).RawBaseLevel = level;
                return owner;
            }
        }
    }
}
