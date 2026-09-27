using BlueprintCore.Utils;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using MediumClass.Medium;
using MediumClass.Medium.NewUnitParts;
using MediumClass.Utilities;

internal static class ActionBarRefreshScenarios
{
    internal static void Run(BlueprintSpellbook mediumBlueprint, Action<bool, string> check)
    {
        var spirits = new[] { (Guids.Archmage, Guids.ArchmageSpellbook), (Guids.Hierophant, Guids.HierophantSpellbook) };
        foreach (var (spiritId, bookId) in spirits)
        for (int level = 1; level <= 6; level++)
        {
            string context = $"cached action bar / {spiritId} / Medium {level}: ";
            void Assert(bool value, string message) => check(value, context + message);
            var owner = new UnitDescriptor();
            owner.Progression.MediumLevel = level;
            var main = owner.DemandSpellbook(mediumBlueprint);
            main.RawBaseLevel = level;
            main.Rest();
            var state = owner.Unit.Ensure<UnitPartMedium>();
            var selected = PreparePersisted(owner, bookId, level);
            var bar = new CachedActionBar(owner);
            Assert(bar.GroupSpells.Count == 0, "initial empty group is actually cached before channeling");
            state.PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(spiritId) };
            MediumSpiritSpellbookRules.RefreshForChannel(owner);
            Assert(owner.Unit.UISettings.Dirty, "channel completion invalidates an already-created empty group");
            Assert(bar.GroupSpells.Count == 0, "no imaginary LearnSpell or Rest notification refreshes the model");
            bar.Tick();
            Assert(selected.All(spell => bar.GroupSpells.Any(item => item.Blueprint == spell.Blueprint && item.Spellbook == main)),
                "next native update contains every preselected cantrip and available circle");
            Assert(bar.SpellGroupEnabled && bar.GroupSpells.Count == selected.Count, "full category and its circle buttons can be rebuilt from empty");
            Assert(bar.GroupSpells.All(item => item.Spellbook == main), "preparation book entries stay hidden");
            var granted = main.Known(selected.First(s => s.SpellLevel == 1).Blueprint);
            Assert(main.Spend(granted, true), "shared main book casts immediately after channel");
            var spent = main.SaveRemaining();
            int refreshes = bar.RefreshCount;
            int notices = owner.Unit.UISettings.DirtyNotifications;
            for (int n = 0; n < 30; n++)
            {
                owner.Unit.Get<UnitPartMediumPreparedSpells>().Sync();
                ActionBarSpellbookHelper.Fetch(owner.Unit);
                bar.Tick();
            }
            Assert(bar.RefreshCount == refreshes && owner.Unit.UISettings.DirtyNotifications == notices && !owner.Unit.UISettings.Dirty,
                "unchanged Sync/Fetch do not schedule a recursive or per-frame rebuild");
            Assert(main.SaveRemaining().SequenceEqual(spent), "UI collection never refills spent slots");

            // Dismissing/rechanneling keeps the two preparation books as selection sheets.
            state.PrimarySpirit = null;
            owner.Unit.Get<UnitPartMediumPreparedSpells>().Clear();
            MediumSpiritSpellbookRules.ClampRemainingSlots(owner);
            bar.Tick();
            Assert(bar.GroupSpells.Count == 0, "dismissal removes revoked cached entries");
            state.PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(spiritId) };
            MediumSpiritSpellbookRules.RefreshForChannel(owner);
            bar.Tick();
            Assert(bar.GroupSpells.Count == selected.Count, "rechanneling shows unchanged saved selections without re-Memorize");

            // Native deserialization does not send the Memorize event. An early spirit
            // activation may precede restoring its prepared slots; final UI Fetch repairs it.
            var loaded = new UnitDescriptor();
            loaded.Progression.MediumLevel = level;
            var loadedMain = loaded.DemandSpellbook(mediumBlueprint);
            loadedMain.RawBaseLevel = level;
            loadedMain.LoadRemaining(spent);
            loaded.Unit.Ensure<UnitPartMedium>().PrimarySpirit = state.PrimarySpirit;
            loaded.Unit.Ensure<UnitPartMediumPreparedSpells>().Sync();
            var loadedSelected = PreparePersisted(loaded, bookId, level);
            loaded.Unit.UISettings.TryToInitialize();
            int loadNotices = loaded.Unit.UISettings.DirtyNotifications;
            var loadedBar = new CachedActionBar(loaded);
            Assert(loadedSelected.All(spell => loadedBar.GroupSpells.Any(item => item.Blueprint == spell.Blueprint && item.Spellbook == loadedMain)),
                "first UI Fetch repairs already-prepared low-level load after an earlier empty synchronization");
            Assert(loaded.Unit.UISettings.DirtyNotifications == loadNotices && !loaded.Unit.UISettings.Dirty,
                "repair inside Fetch does not mark its own rebuild dirty");
            Assert(loadedMain.SaveRemaining().SequenceEqual(spent), "late load repair retains spent slots");
            loaded.ApplyPostLoadFixes();
            loadedBar.Tick();
            Assert(loadedBar.GroupSpells.Count == loadedSelected.Count && loadedMain.SaveRemaining().SequenceEqual(spent),
                "final load lifecycle is idempotent and preserves the repaired display and pool");

            // A second preparation book contributes choices, never a second daily pool.
            var second = spirits.Single(pair => pair.Item1 != spiritId);
            var extraSelections = PreparePersisted(owner, second.Item2, level);
            var beforeSecond = main.SaveRemaining();
            state.AdditionalSpirits.Add(new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(second.Item1) });
            MediumSpiritSpellbookRules.RefreshForAdditionalSpirit(owner, null);
            bar.Tick();
            Assert(bar.GroupSpells.Count == selected.Count + extraSelections.Count,
                "adding the other caster spirit refreshes both saved preparation selections together");
            Assert(main.SaveRemaining().SequenceEqual(beforeSecond), "adding a second preparation book preserves shared remaining casts");
            Assert(bar.GroupSpells.All(item => item.Spellbook == main), "both preparation books remain absent from combat spell list");
            // Refresh by selected-unit change also covers a UI created after load, without
            // relying on Dirty surviving native TryToInitialize at construction time.
            var switchedBar = new CachedActionBar(owner);
            Assert(switchedBar.GroupSpells.Count == bar.GroupSpells.Count, "selection/construction builds the same union without preparation edits");
        }

        // Ordinary units / an incompletely restored spirit part are no-op at Fetch.
        var unrelated = new UnitDescriptor();
        unrelated.Progression.MediumLevel = 3;
        var book = unrelated.DemandSpellbook(mediumBlueprint);
        book.RawBaseLevel = 3;
        var spell = mediumBlueprint.SpellList.GetSpells(0).FirstOrDefault()
            ?? new Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility { Name = "permanent cantrip" };
        var permanent = book.AddKnownPermanent(0, spell);
        int demands = unrelated.DemandCalls;
        var noSpirit = ActionBarSpellbookHelper.Fetch(unrelated.Unit);
        check(noSpirit.Contains(permanent) && unrelated.DemandCalls == demands && !unrelated.Unit.UISettings.Dirty,
            "UI repair is read-only for inactive / partially restored spirit state");

        // Dismissal starts while PrimarySpirit still exists. Native custom-removal
        // and SetDirty events are synchronous, so an event consumer may collect UI
        // again before OnDeactivate gets to clearing the channel selection.
        var dismissing = new UnitDescriptor();
        dismissing.Progression.MediumLevel = 6;
        dismissing.Unit.Ensure<UnitPartMedium>().PrimarySpirit = new() { Blueprint = BlueprintTool.Get<BlueprintCharacterClass>(Guids.Archmage) };
        var selections = PreparePersisted(dismissing, Guids.ArchmageSpellbook, 6);
        MediumSpiritSpellbookRules.RefreshForChannel(dismissing);
        var dismissBook = dismissing.GetSpellbook(mediumBlueprint);
        var source = dismissBook.Known(selections.First(s => s.SpellLevel == 1).Blueprint);
        var recipe = new AbilityData { Blueprint = source.Blueprint, SpellLevel = 2, SpellLevelInSpellbook = 1, MetamagicData = new(), Spellbook = dismissBook };
        dismissBook.AddCustomSpell(recipe);
        int nestedFetches = 0;
        void FetchDuringClear()
        {
            nestedFetches++;
            ActionBarSpellbookHelper.Fetch(dismissing.Unit);
        }
        dismissing.Unit.CustomSpellRemoved += _ => FetchDuringClear();
        dismissing.Unit.UISettings.ActionBarUpdated += FetchDuringClear;
        dismissing.Unit.UISettings.TryToInitialize();
        dismissing.Unit.Get<UnitPartMediumPreparedSpells>().Clear();
        check(nestedFetches == 2, "dismissal regression actually reenters Fetch from both custom-removal and dirty events");
        check(selections.All(s => dismissBook.Known(s.Blueprint) == null) && !dismissBook.GetCustomSpells(2).Contains(recipe),
            "synchronous UI reentry cannot regrant spells or mutate the grant enumeration during dismissal");
    }

    private static List<AbilityData> PreparePersisted(UnitDescriptor owner, string bookId, int level)
    {
        var blueprint = BlueprintTool.Get<BlueprintSpellbook>(bookId);
        var book = owner.DemandSpellbook(blueprint);
        book.RawBaseLevel = level;
        book.UpdateAllSlotsSize(false);
        var result = new List<AbilityData>();
        for (int circle = 0; circle <= book.MaxSpellLevel; circle++)
        {
            var ability = new AbilityData { Blueprint = blueprint.SpellList.GetSpells(circle).Single(), SpellLevel = circle, Spellbook = book };
            book.RestoreMemorized(new() { SpellShell = ability, SpellLevel = circle });
            result.Add(ability);
        }
        return result;
    }

    // Native ActionBarVM caches GroupSpells. It does not subscribe to ILearnSpellHandler;
    // Spellbook.Rest emits no UI event. Only Dirty / the explicit Memorize event or
    // selected-unit change rebuild the entire group via Fetch and OnUnitUpdated.
    private sealed class CachedActionBar
    {
        private readonly UnitDescriptor owner;
        private bool needReset;
        public List<AbilityData> GroupSpells = new();
        public int RefreshCount;
        public bool SpellGroupEnabled => GroupSpells.Count != 0;
        public CachedActionBar(UnitDescriptor owner)
        {
            this.owner = owner;
            owner.Unit.MemorizedSpellsChanged += () => needReset = true;
            Rebuild();
        }
        public void Tick()
        {
            if (!needReset && !owner.Unit.UISettings.Dirty) return;
            Rebuild();
            needReset = false;
        }
        private void Rebuild()
        {
            owner.Unit.UISettings.TryToInitialize();
            GroupSpells.Clear();
            GroupSpells = ActionBarSpellbookHelper.Fetch(owner.Unit);
            RefreshCount++;
            owner.Unit.UISettings.TryToInitialize();
        }
    }
}
