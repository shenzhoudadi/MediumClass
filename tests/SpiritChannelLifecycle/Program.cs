using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using MediumClass.Medium.NewUnitParts;
using MediumClass.NewComponents;
using MediumClass.Utilities;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using MediumClass.Medium;

int checks = 0;
var failures = new List<string>();
foreach (var id in new[] { Guids.Archmage, Guids.Hierophant, Guids.Trickster, "Champion", "Guardian", "Marshal" })
    BlueprintTool.Assets[id] = new BlueprintCharacterClass { Name = id };
foreach (var id in new[] { Guids.SpiritPower, Guids.AstralBeacon, Guids.MediumSpellcasterFeatProhibitArchmage, Guids.MediumSpellcasterFeatProhibitHierophant })
    BlueprintTool.Assets[id] = new BlueprintFeature { Name = id, Ranks = id == Guids.SpiritPower ? 4 : 1 };
BlueprintTool.Assets[Guids.MediumSpiritBonus] = new BlueprintFeature { Name = Guids.MediumSpiritBonus, Ranks = 10 };
BlueprintTool.Assets[MythicSpirits.MultipleSpiritsGuid] = new BlueprintFeature { Name = "Mythic Multiple Spirits", Ranks = 1 };

foreach (bool beacon in new[] { false, true })
foreach (int powerRank in new[] { 1, 2, 3, 4 })
foreach (int forgone in new[] { 0, 1, 2, 3, 4 })
foreach (string primary in new[] { "Champion", Guids.Archmage, Guids.Trickster })
{
    if (beacon && powerRank < 4) continue; // Astral Beacon is a level-20 feature.
    string context = $"beacon={beacon}, rank={powerRank}, forgone={forgone}, primary={primary}";
    var (owner, state, refs) = NewOwner(powerRank, forgone, beacon);
    state.PrimarySpirit = refs[primary];
    var component = new ApplySpirits { Owner = owner };
    component.OnActivate();
    foreach (var id in new[] { Guids.Archmage, Guids.Hierophant, Guids.Trickster, "Guardian" }.Where(id => id != primary))
    {
        state.AdditionalSpirits.Add(refs[id]);
        component.RefreshActiveSpirits();
    }
    // In practice powers are already restored when the channel buff activates on load.
    // Repeating activation must neither duplicate ranks nor refresh resources.
    var beforeReapply = Snapshot(owner);
    component.OnActivate();
    component.RefreshActiveSpirits();
    Equal(beforeReapply, Snapshot(owner), context + ": repeat activation and refresh are idempotent");
    foreach (var active in state.ActiveSpiritClasses)
    {
        var entry = state.Spirits[active];
        int effective = powerRank - forgone;
        bool caster = active.Get().Name is Guids.Archmage or Guids.Hierophant;
        int lesser = effective >= 1 && !caster ? 1 : caster && effective < 1 ? 1 : 0;
        Equal(lesser, owner.Progression.Features.GetRank(entry.SpiritLesserPower.Get()), context + ": lesser/prohibition " + active.Get().Name);
        int expectedIntermediate = effective >= 2
            ? active.Get().Name == Guids.Trickster ? 6 : 1 : 0;
        Equal(expectedIntermediate, owner.Progression.Features.GetRank(entry.SpiritIntermediatePower.Get()), context + ": intermediate ranks " + active.Get().Name);
        Equal(effective >= 3 ? 1 : 0, owner.Progression.Features.GetRank(entry.SpiritGreaterPower.Get()), context + ": greater follows effective rank " + active.Get().Name);
        Equal(effective >= 4 ? 1 : 0, owner.Progression.Features.GetRank(entry.SpiritSupremePower.Get()), context + ": supreme follows effective rank even after Beacon " + active.Get().Name);
        if (entry.OverwriteIntermediatePower != null)
            Equal(0, owner.Progression.Features.GetRank(entry.OverwriteIntermediatePower.Get()), context + ": active spirit has no secondary overwrite");
        if (entry.OverwriteGreaterPower != null)
            Equal(0, owner.Progression.Features.GetRank(entry.OverwriteGreaterPower.Get()), context + ": active spirit has no greater overwrite");
    }
    foreach (var inactive in state.Spirits.Keys.Where(s => !state.IsActiveSpirit(s)))
    {
        var entry = state.Spirits[inactive];
        var intermediate = entry.OverwriteIntermediatePower ?? entry.SpiritIntermediatePower;
        Equal(beacon ? inactive.Get().Name == Guids.Trickster ? 6 : 1 : 0,
            owner.Progression.Features.GetRank(intermediate.Get()), context + ": secondary intermediate " + inactive.Get().Name);
        if (entry.OverwriteIntermediatePower != null)
            Equal(0, owner.Progression.Features.GetRank(entry.SpiritIntermediatePower.Get()), context + ": secondary intermediate never duplicates base");
        var greater = entry.OverwriteGreaterPower ?? entry.SpiritGreaterPower;
        Equal(beacon ? 1 : 0, owner.Progression.Features.GetRank(greater.Get()), context + ": secondary greater " + inactive.Get().Name);
        if (entry.OverwriteGreaterPower != null)
            Equal(0, owner.Progression.Features.GetRank(entry.SpiritGreaterPower.Get()), context + ": secondary greater never duplicates base");
    }
    component.HandleUnitReapplyFeaturesOnLevelUp();
    Equal(beforeReapply, Snapshot(owner), context + ": upgrade callback preserves already correct ranks");
    component.OnDeactivate();
    foreach (var entry in state.Spirits.Values)
    foreach (var feature in Powers(entry).Where(f => !f.Name.StartsWith("Forbid", StringComparison.Ordinal)))
        Equal(0, owner.Progression.Features.GetRank(feature), context + ": deactivation removes " + feature.Name);
    Check(!state.ActiveSpiritClasses.Any(), context + ": all active selections cleared");
    Equal(0, owner.Influence, context + ": influence reset on deactivation");
    Equal(1, owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpellcasterFeatProhibitArchmage)), context + ": Archmage preparation prohibited after channel");
    Equal(1, owner.Progression.Features.GetRank(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpellcasterFeatProhibitHierophant)), context + ": Hierophant preparation prohibited after channel");
}

// Saves made before multiple spirits have no additional-spirit field. Existing
// feature ranks must remain stable on activation and all be removed on teardown.
{
    var (owner, state, refs) = NewOwner(4, 0, false);
    state.PrimarySpirit = refs[Guids.Trickster];
    state.AdditionalSpirits = null;
    var entry = state.Spirits[state.PrimarySpirit];
    foreach (var feature in Powers(entry)) owner.AddFact(feature);
    owner.Progression.Features.Counts[entry.SpiritIntermediatePower.Get()] = 6;
    var component = new ApplySpirits { Owner = owner };
    component.OnActivate();
    Equal(6, owner.Progression.Features.GetRank(entry.SpiritIntermediatePower.Get()), "old save reactivation preserves restored Trickster ranks");
    component.OnDeactivate();
    Equal(0, owner.Progression.Features.GetRank(entry.SpiritIntermediatePower.Get()), "old save channel ends with no residual Trickster rank");
    Check(!state.ActiveSpiritClasses.Any(), "old save without additional list clears safely");
}

// A class-level increase adds only the newly earned Trickster rank.
{
    var (owner, state, refs) = NewOwner(2, 0, false);
    owner.SpiritClassLevel = 8;
    state.PrimarySpirit = refs[Guids.Trickster];
    var component = new ApplySpirits { Owner = owner };
    component.OnActivate();
    var intermediate = state.Spirits[state.PrimarySpirit].SpiritIntermediatePower.Get();
    Equal(2, owner.Progression.Features.GetRank(intermediate), "level 8 Trickster receives 2 ranks");
    owner.SpiritClassLevel = 9;
    component.HandleUnitReapplyFeaturesOnLevelUp();
    Equal(3, owner.Progression.Features.GetRank(intermediate), "level 9 Trickster adds one earned rank");
    component.HandleUnitReapplyFeaturesOnLevelUp();
    Equal(3, owner.Progression.Features.GetRank(intermediate), "repeat level-up callback cannot duplicate rank");
    component.OnDeactivate();
    Equal(0, owner.Progression.Features.GetRank(intermediate), "all earned ranks removed after channel ends");
}
// Old shared-seance runtimes deserialize without data. Post-load must preserve
// a one-time cleanup marker even when OnTurnOn encounters existing boon facts.
BlueprintTool.Assets[Guids.MediumSharedSeanceBuff] = new BlueprintBuff { Name = "SharedSeance" };
var sharedBoon = new BlueprintFeature { Name = "shared boon" };
var unrelatedBoon = new BlueprintFeature { Name = "unrelated boon" };
{
    var (owner, state, refs) = NewOwner(4, 0, false);
    var ally = new UnitEntityData();
    Game.Instance.Player.ActiveCompanions = [owner, ally];
    state.PrimarySpirit = refs["Champion"];
    state.Spirits[state.PrimarySpirit].SpiritSeanceBoon = new() { Blueprint = sharedBoon };
    owner.AddFact(sharedBoon); ally.AddFact(sharedBoon); ally.AddFact(unrelatedBoon);
    var component = Seance(owner);
    Check(component.MaybeData == null, "legacy shared-seance runtime starts without data");
    component.OnPostLoad();
    Check(component.Data.LegacyCleanupPending && component.Data.LegacySpirit == refs["Champion"], "legacy post-load marks cleanup and remembers the saved primary");
    component.OnTurnOn();
    Check(component.Data.LegacyCleanupPending, "turn-on cannot overwrite pending legacy cleanup");
    Equal(0, component.Data.Grants.Count, "legacy existing boons are not treated as newly created facts");
    // The channel component can clear its primary before the shared buff turns off.
    state.ClearChannelSelection();
    component.OnTurnOff();
    Check(!owner.HasFact(sharedBoon) && !ally.HasFact(sharedBoon), "legacy cleanup removes the captured primary's old grants after channel state clears");
    Check(ally.HasFact(unrelatedBoon), "legacy cleanup leaves unrelated boon facts intact");
    Check(!component.Data.LegacyCleanupPending, "legacy cleanup is consumed exactly once");
    component.OnTurnOff();
    Check(ally.HasFact(unrelatedBoon), "repeated cleanup cannot touch unrelated boons");
}
{
    var (first, firstState, firstRefs) = NewOwner(4, 0, false);
    var (second, secondState, secondRefs) = NewOwner(4, 0, false);
    var ally = new UnitEntityData();
    Game.Instance.Player.ActiveCompanions = [first, second, ally];
    firstState.PrimarySpirit = firstRefs["Champion"];
    secondState.PrimarySpirit = secondRefs["Champion"];
    firstState.Spirits[firstState.PrimarySpirit].SpiritSeanceBoon = new() { Blueprint = sharedBoon };
    secondState.Spirits[secondState.PrimarySpirit].SpiritSeanceBoon = new() { Blueprint = sharedBoon };
    var firstComponent = Seance(first);
    var secondComponent = Seance(second);
    firstComponent.OnInitialize(); secondComponent.OnInitialize();
    firstComponent.OnTurnOn(); secondComponent.OnTurnOn();
    Equal(3, firstComponent.Data.Grants.Count, "fresh seance tracks only facts it created");
    Equal(0, secondComponent.Data.Grants.Count, "second seance does not claim already supplied rank-one boons");
    Check(!firstComponent.Data.LegacyCleanupPending && !secondComponent.Data.LegacyCleanupPending, "new runtimes never enter legacy cleanup");
    firstComponent.OnTurnOff();
    first.Buffs.Enumerable.Remove((Buff)firstComponent.Fact);
    Check(Game.Instance.Player.ActiveCompanions.All(unit => unit.HasFact(sharedBoon)), "ending first provider keeps the other provider's shared boons");
    Equal(3, secondComponent.Data.Grants.Count, "remaining provider receives ownership for eventual cleanup");
    secondComponent.OnPostLoad();
    Check(!secondComponent.Data.LegacyCleanupPending, "saved new ownership data is not reclassified as legacy");
    secondComponent.OnTurnOff();
    Check(Game.Instance.Player.ActiveCompanions.All(unit => !unit.HasFact(sharedBoon)), "ending last provider cleans the transferred facts");
}

// A blueprint component is shared between all unit facts. Switch the explicit
// model's Owner/Data as native RequestEventContext does for each runtime.
{
    var (first, firstState, firstRefs) = NewOwner(3, 0, false);
    var (second, secondState, secondRefs) = NewOwner(3, 0, false);
    firstState.PrimarySpirit = firstRefs["Champion"];
    secondState.PrimarySpirit = secondRefs["Champion"];
    var power = firstState.Spirits[firstRefs[Guids.Trickster]].SpiritIntermediatePower;
    secondState.Spirits[secondRefs[Guids.Trickster]].SpiritIntermediatePower = power;
    first.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus)] = 3;
    second.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus)] = 5;
    var trance = new MediumTranceOfThreeComponent { Owner = first, BP = power };
    trance.OnInitialize(); trance.OnTurnOn();
    var firstData = trance.Data;
    Equal(3, firstData.GrantedRanks, "first trance records its own actual grant");
    trance.OnTurnOn();
    Equal(3, first.Progression.Features.GetRank(power.Get()), "repeat turn-on cannot duplicate trance ranks");
    trance.Owner = second; trance.LoadData(new());
    trance.OnInitialize(); trance.OnTurnOn();
    var secondData = trance.Data;
    Equal(5, secondData.GrantedRanks, "second trance has independent runtime grant count");
    trance.Owner = first; trance.LoadData(firstData); trance.OnTurnOff();
    Equal(0, first.Progression.Features.GetRank(power.Get()), "first caster removes exactly its three ranks after second caster borrows five");
    Equal(5, second.Progression.Features.GetRank(power.Get()), "ending first trance leaves second caster intact");
    trance.Owner = second; trance.LoadData(secondData); trance.OnTurnOff();
    Equal(0, second.Progression.Features.GetRank(power.Get()), "second caster removes its five ranks");
    trance.OnTurnOff();
    Equal(0, second.Progression.Features.GetRank(power.Get()), "repeated trance teardown is harmless");
}
{
    var (owner, state, refs) = NewOwner(3, 0, false);
    state.PrimarySpirit = refs["Champion"];
    var power = state.Spirits[refs[Guids.Trickster]].SpiritIntermediatePower;
    owner.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus)] = 3;
    owner.Progression.Features.Counts[power.Get()] = 9;
    var trance = new MediumTranceOfThreeComponent { Owner = owner, BP = power };
    trance.OnInitialize(); trance.OnTurnOn();
    Equal(1, trance.Data.GrantedRanks, "feature cap records one real addition rather than three attempted grants");
    var restored = new MediumTranceOfThreeComponent { Owner = owner, BP = power };
    restored.LoadData(new() { Version = 1, Applied = true, GrantedRanks = trance.Data.GrantedRanks });
    restored.OnPostLoad(); restored.OnTurnOn();
    Equal(10, owner.Progression.Features.GetRank(power.Get()), "restored runtime does not grant ranks a second time");
    restored.OnTurnOff();
    Equal(9, owner.Progression.Features.GetRank(power.Get()), "restored capped grant removes only its one real rank");
}
foreach (bool beacon in new[] { false, true })
foreach (int current in new[] { 5, 6, 10 })
{
    var (owner, state, refs) = NewOwner(4, 0, beacon);
    state.PrimarySpirit = refs["Champion"];
    var power = state.Spirits[refs[Guids.Trickster]].SpiritIntermediatePower;
    owner.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus)] = 6;
    owner.Progression.Features.Counts[power.Get()] = current;
    var legacy = new MediumTranceOfThreeComponent { Owner = owner, BP = power };
    legacy.OnPostLoad(); legacy.OnApplyPostLoadFixes(); legacy.OnTurnOn();
    Equal(current, owner.Progression.Features.GetRank(power.Get()), $"legacy trance load is idempotent beacon={beacon}, current={current}");
    legacy.OnTurnOff();
    int minimum = beacon ? 6 : 0;
    int removed = Math.Min(6, Math.Max(0, current - minimum));
    Equal(current - removed, owner.Progression.Features.GetRank(power.Get()), $"legacy cleanup preserves permanent ranks beacon={beacon}, current={current}");
}
{
    var (owner, state, refs) = NewOwner(4, 0, false);
    owner.SpiritClassLevel = 19;
    state.PrimarySpirit = refs["Champion"];
    var power = state.Spirits[refs[Guids.Trickster]].SpiritIntermediatePower;
    owner.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus)] = 5;
    var trance = new MediumTranceOfThreeComponent { Owner = owner, BP = power };
    trance.OnInitialize(); trance.OnTurnOn();
    Equal(5, owner.Progression.Features.GetRank(power.Get()), "level 19 borrows five ranks");
    owner.SpiritClassLevel = 20;
    owner.AddFact(BlueprintTool.Get<BlueprintFeature>(Guids.AstralBeacon));
    new ApplySpirits { Owner = owner }.RefreshActiveSpirits();
    Equal(6, owner.Progression.Features.GetRank(power.Get()), "level 20 Beacon provides its six ranks even during a trance");
    trance.OnTurnOff();
    Equal(6, owner.Progression.Features.GetRank(power.Get()), "trance expiry preserves newly earned Beacon ranks");
    trance.OnTurnOn();
    Equal(0, trance.Data.GrantedRanks, "trance cannot stack another copy over Beacon");
    Equal(6, owner.Progression.Features.GetRank(power.Get()), "Beacon plus repeated trance never doubles Surprise Strike");
    trance.OnTurnOff();
}
{
    var (owner, state, refs) = NewOwner(4, 0, false);
    state.PrimarySpirit = refs["Champion"];
    var power = state.Spirits[refs[Guids.Trickster]].SpiritIntermediatePower;
    owner.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpiritBonus)] = 6;
    var trance = new MediumTranceOfThreeComponent { Owner = owner, BP = power };
    trance.OnInitialize(); trance.OnTurnOn();
    trance.OnTurnOff(); // MultiSpiritRules.RemoveTrance runs before adding the spirit.
    Equal(0, owner.Progression.Features.GetRank(power.Get()), "promotion removes the temporary grant before the formal channel");
    state.AdditionalSpirits.Add(refs[Guids.Trickster]);
    new ApplySpirits { Owner = owner }.RefreshActiveSpirits();
    Equal(6, owner.Progression.Features.GetRank(power.Get()), "formal additional Trickster receives the correct permanent ranks");
    trance.OnTurnOff();
    Equal(6, owner.Progression.Features.GetRank(power.Get()), "late duplicate trance teardown cannot erase the formal channel");
}

// Execute the production fixed-integer modifier components, not a mocked
// Reapply method. Native CallComponents supplies the correct runtime context;
// disabled facts remain disabled until the native TurnOn callback occurs.
BlueprintTool.Assets[Guids.MediumSpiritBonusBuff] = new BlueprintBuff { Name = "Spirit bonus" };
BlueprintTool.Assets[Guids.MediumInfluenceDebuff] = new BlueprintBuff { Name = "Influence penalty" };
BlueprintTool.Assets[Guids.MediumChannelSpirit] = new BlueprintFeature
{
    Name = "Channel catalogue",
    Components = { new MediumSpiritComponent { Stats = Enum.GetValues<StatType>(), Penalties = Enum.GetValues<StatType>() } }
};
{
    var (owner, state, refs) = NewOwner(3, 0, false);
    state.PrimarySpirit = refs["Champion"];
    var bonusFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus);
    owner.Progression.Features.Counts[bonusFeature.Get()] = 2;
    var champion = state.Spirits[refs["Champion"]];
    champion.SpiritBonus.SpiritBonusFeature = bonusFeature;
    champion.SpiritBonus.Stats = new[] { StatType.AdditionalAttackBonus, StatType.SaveFortitude };
    champion.SpiritPenalty.Stats = new[] { StatType.SkillKnowledgeArcana };
    var guardian = state.Spirits[refs["Guardian"]];
    guardian.SpiritBonus.SpiritBonusFeature = bonusFeature;
    guardian.SpiritBonus.Stats = new[] { StatType.AC, StatType.SaveFortitude };
    guardian.SpiritPenalty.Stats = new[] { StatType.AdditionalDamage };
    guardian.SpiritFocus = 1;
    var bonusBuff = new Buff { Blueprint = BlueprintTool.Get<BlueprintBuff>(Guids.MediumSpiritBonusBuff) };
    var penaltyBuff = new Buff { Blueprint = BlueprintTool.Get<BlueprintBuff>(Guids.MediumInfluenceDebuff) };
    var bonus = new MediumContextSpiritBonusComponent { Owner = owner, Fact = bonusBuff };
    var penalty = new MediumInfluencePenaltyComponent { Owner = owner, Fact = penaltyBuff };
    bonusBuff.Components.Add(bonus); penaltyBuff.Components.Add(penalty);
    owner.Buffs.Enumerable.Add(bonusBuff); owner.Buffs.Enumerable.Add(penaltyBuff);
    bonus.OnTurnOn(); penalty.OnTurnOn();
    var channel = new ApplySpirits { Owner = owner };
    var unrelated = new object();
    owner.Stats.GetStat(StatType.AdditionalAttackBonus).AddModifier(7, unrelated, ModifierDescriptor.UntypedStackable);
    void Modifier(StatType stat, object source, int expected, string label)
    {
        var own = owner.Stats.GetStat(stat).Modifiers.Where(m => m.Source == source).ToArray();
        Equal(1, own.Length, label + ": one modifier");
        Equal(expected, own.Sum(m => m.Amount), label + ": correct amount");
    }
    Modifier(StatType.AdditionalAttackBonus, bonus.Runtime, 2, "initial single-spirit bonus");
    Modifier(StatType.SkillKnowledgeArcana, penalty.Runtime, -2, "initial single-spirit penalty");
    owner.Progression.Features.Counts[bonusFeature.Get()] = 3;
    channel.HandleUnitReapplyFeaturesOnLevelUp();
    Modifier(StatType.AdditionalAttackBonus, bonus.Runtime, 3, "level-up immediately refreshes fixed bonus");
    Modifier(StatType.SkillKnowledgeArcana, penalty.Runtime, -3, "level-up immediately refreshes fixed penalty");
    Modifier(StatType.AdditionalAttackBonus, unrelated, 7, "level-up preserves another source's modifier");
    state.AdditionalSpirits.Add(refs["Guardian"]);
    channel.RefreshActiveSpirits();
    Modifier(StatType.AC, bonus.Runtime, 4, "additional Guardian bonus includes Spirit Focus");
    Modifier(StatType.SaveFortitude, bonus.Runtime, 4, "overlapping spirits use highest bonus instead of stacking");
    Modifier(StatType.AdditionalDamage, penalty.Runtime, -4, "additional Guardian penalty is applied");
    owner.Progression.Features.Counts[bonusFeature.Get()] = 4;
    for (int i = 0; i < 5; i++) channel.HandleUnitReapplyFeaturesOnLevelUp();
    Modifier(StatType.AdditionalAttackBonus, bonus.Runtime, 4, "repeated upgrade refresh never duplicates primary bonus");
    Modifier(StatType.SaveFortitude, bonus.Runtime, 5, "repeated upgrade refresh never duplicates shared stat");
    Modifier(StatType.AdditionalDamage, penalty.Runtime, -5, "repeated upgrade refresh never duplicates secondary penalty");
    Modifier(StatType.Initiative, penalty.Runtime, -2, "fixed initiative penalty is not duplicated");
    Modifier(StatType.SaveWill, penalty.Runtime, 2, "fixed Will modifier is not duplicated");
    // Preview/load owners can have attached active facts whose manager has not
    // turned them on. Refresh must not introduce stats before native activation.
    bonus.OnTurnOff(); penalty.OnTurnOff();
    bonusBuff.IsTurnedOn = false; penaltyBuff.IsTurnedOn = false;
    owner.Progression.Features.Counts[bonusFeature.Get()] = 5;
    channel.HandleUnitReapplyFeaturesOnLevelUp();
    Check(owner.Stats.Values.Values.All(stat => stat.Modifiers.All(m => m.Source != bonus.Runtime && m.Source != penalty.Runtime)),
        "upgrade skips attached but not turned-on buff runtimes");
    bonusBuff.IsTurnedOn = true; penaltyBuff.IsTurnedOn = true;
    bonus.OnTurnOn(); penalty.OnTurnOn();
    Modifier(StatType.SaveFortitude, bonus.Runtime, 6, "native turn-on uses latest rank after deferred upgrade");
    Modifier(StatType.AdditionalDamage, penalty.Runtime, -6, "native penalty turn-on uses latest rank");
    channel.OnDeactivate();
    // These are the native checked-fact-loss buff callbacks after channel removal.
    bonus.OnTurnOff(); penalty.OnTurnOff();
    Check(!state.ActiveSpiritClasses.Any(), "modifier refresh does not prevent complete channel teardown");
    Check(owner.Stats.Values.Values.All(stat => stat.Modifiers.All(m => m.Source != bonus.Runtime && m.Source != penalty.Runtime)),
        "channel teardown removes all refreshed primary and additional-spirit modifiers");
    Modifier(StatType.AdditionalAttackBonus, unrelated, 7, "channel teardown retains unrelated modifiers");
}

// Penalties divide their positive magnitude and round down; they never change
// positive effects, costs, or the number of active spirits themselves.
foreach (bool mythic in new[] { false, true })
for (int count = 0; count <= 6; count++)
for (int magnitude = 0; magnitude <= 12; magnitude++)
    Equal(magnitude / (mythic ? Math.Max(1, count) : 1),
        InfluencePenaltyMath.Magnitude(magnitude, count, mythic),
        $"penalty magnitude {magnitude} with {count} formal spirits, mythic={mythic}");
Equal(0, InfluencePenaltyMath.Magnitude(-2, 0, true), "invalid negative magnitude cannot create a positive bonus");
foreach (bool mythic in new[] { false, true })
for (int count = 0; count <= 6; count++)
foreach (bool secondaryOnly in new[] { false, true })
{
    var (owner, state, refs) = NewOwner(4, 0, false);
    if (mythic) owner.AddFact(BlueprintTool.Get<BlueprintFeature>(MythicSpirits.MultipleSpiritsGuid));
    var selection = new[] { "Champion", "Guardian", Guids.Trickster, Guids.Archmage, Guids.Hierophant, "Marshal" };
    if (count > 0) state.PrimarySpirit = refs[selection[0]];
    foreach (var id in selection.Skip(1).Take(Math.Max(0, count - 1))) state.AdditionalSpirits.Add(refs[id]);
    state.SecondarySpirit = refs["Marshal"]; // Must not increase the divisor.
    var bonusFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus);
    owner.Progression.Features.Counts[bonusFeature.Get()] = 5;
    foreach (var entry in state.Spirits.Values)
    {
        entry.SpiritBonus.SpiritBonusFeature = bonusFeature;
        entry.SpiritBonus.Stats = new[] { StatType.AdditionalAttackBonus };
        entry.SpiritPenalty.Stats = new[] { StatType.SkillKnowledgeArcana };
    }
    var buff = new Buff { Blueprint = BlueprintTool.Get<BlueprintBuff>(Guids.MediumInfluenceDebuff) };
    var penalty = new MediumInfluencePenaltyComponent { Owner = owner, Fact = buff, isSecondaryCheck = secondaryOnly };
    buff.Components.Add(penalty); owner.Buffs.Enumerable.Add(buff);
    penalty.OnTurnOn(); penalty.RefreshModifiers();
    int divisor = mythic ? Math.Max(1, count) : 1;
    int penaltyMagnitude = count > 0 || secondaryOnly ? 5 / divisor : 0;
    Equal(-penaltyMagnitude, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "formal-only divisor for a spirit penalty");
    Equal(-(2 / divisor), OwnModifier(owner, StatType.Initiative, penalty.Runtime), "fixed initiative penalty uses the same divisor");
    Equal(2, OwnModifier(owner, StatType.SaveWill, penalty.Runtime), "positive influence Will effect is not divided");
    Check(owner.Stats.Values.Values.All(stat => stat.Modifiers.Count(m => m.Source == penalty.Runtime) <= 1), "refresh never duplicates a stat penalty");
    if (secondaryOnly || count == 6)
    {
        Check(owner.HasFact(BlueprintCore.Blueprints.References.BuffRefs.FightDefensivelyBuff.Reference.Get()), "Marshal defensive fighting remains mandatory");
        Equal(1, buff.Stored.Count, "refresh does not add another forced-defensive buff");
    }
    // Neither unrelated Beacon powers nor a secondary reference change N.
    owner.AddFact(BlueprintTool.Get<BlueprintFeature>(Guids.AstralBeacon));
    penalty.RefreshModifiers();
    Equal(-penaltyMagnitude, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "Beacon-granted powers do not dilute the formal-spirit divisor");
    penalty.OnTurnOff();
    Check(owner.Stats.Values.Values.All(stat => stat.Modifiers.All(m => m.Source != penalty.Runtime)), "divided modifiers clean up completely");
}
{
    var (owner, state, refs) = NewOwner(4, 0, false);
    state.PrimarySpirit = refs["Champion"];
    var mythic = BlueprintTool.Get<BlueprintFeature>(MythicSpirits.MultipleSpiritsGuid);
    owner.AddFact(mythic);
    var feature = BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpiritBonus);
    owner.Progression.Features.Counts[feature.Get()] = 5;
    foreach (var entry in state.Spirits.Values)
    {
        entry.SpiritBonus.SpiritBonusFeature = feature;
        entry.SpiritBonus.Stats = new[] { StatType.AdditionalAttackBonus };
        entry.SpiritPenalty.Stats = new[] { StatType.SkillKnowledgeArcana };
    }
    var penaltyBuff = new Buff { Blueprint = BlueprintTool.Get<BlueprintBuff>(Guids.MediumInfluenceDebuff) };
    var bonusBuff = new Buff { Blueprint = BlueprintTool.Get<BlueprintBuff>(Guids.MediumSpiritBonusBuff) };
    var penalty = new MediumInfluencePenaltyComponent { Owner = owner, Fact = penaltyBuff };
    var bonus = new MediumContextSpiritBonusComponent { Owner = owner, Fact = bonusBuff };
    penaltyBuff.Components.Add(penalty); bonusBuff.Components.Add(bonus);
    owner.Buffs.Enumerable.Add(penaltyBuff); owner.Buffs.Enumerable.Add(bonusBuff);
    penalty.OnTurnOn(); bonus.OnTurnOn();
    var channel = new ApplySpirits { Owner = owner };
    Equal(-5, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "one formal spirit retains full penalty");
    state.AdditionalSpirits.Add(refs["Guardian"]); channel.RefreshActiveSpirits();
    Equal(-2, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "second spirit immediately halves magnitude five to two");
    Equal(-1, OwnModifier(owner, StatType.Initiative, penalty.Runtime), "second spirit reduces initiative minus two to minus one");
    Equal(5, OwnModifier(owner, StatType.AdditionalAttackBonus, bonus.Runtime), "two-spirit penalty mitigation never reduces spirit bonuses");
    state.AdditionalSpirits.Add(refs[Guids.Trickster]); channel.RefreshActiveSpirits();
    Equal(-1, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "third spirit immediately divides by three");
    Equal(0, OwnModifier(owner, StatType.Initiative, penalty.Runtime), "third spirit rounds initiative magnitude down to zero");
    Check(owner.Stats.GetStat(StatType.Initiative).Modifiers.All(m => m.Source != penalty.Runtime), "zero penalty produces no misleading zero modifier");
    state.AdditionalSpirits.Remove(refs[Guids.Trickster]); channel.RefreshActiveSpirits();
    Equal(-2, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "reducing the formal set immediately restores the two-spirit penalty");
    owner.Progression.Features.Counts[feature.Get()] = 6; channel.HandleUnitReapplyFeaturesOnLevelUp();
    Equal(-3, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "level-up refreshes magnitude before dividing");
    owner.RemoveFact(mythic); channel.HandleUnitReapplyFeaturesOnLevelUp();
    Equal(-6, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "losing the mythic ability restores unreduced penalty even if a saved set remains");
    Equal(-2, OwnModifier(owner, StatType.Initiative, penalty.Runtime), "without mythic the base initiative penalty is unchanged");
    owner.AddFact(mythic); channel.HandleUnitReapplyFeaturesOnLevelUp();
    Equal(-3, OwnModifier(owner, StatType.SkillKnowledgeArcana, penalty.Runtime), "gaining the mythic ability immediately reduces an existing channel's penalty");
    channel.OnDeactivate(); penalty.OnTurnOff(); bonus.OnTurnOff();
    Check(owner.Stats.Values.Values.All(stat => stat.Modifiers.All(m => m.Source != penalty.Runtime && m.Source != bonus.Runtime)), "ending all channeling clears reduced penalties and full bonuses");
}

Console.WriteLine($"Production spirit lifecycle model: {checks - failures.Count}/{checks} assertions passed.");
Console.WriteLine("Scope: actual production component on an explicit feature/buff API model; no Unity, real event ordering, or save serialization.");
foreach (string failure in failures) Console.Error.WriteLine("FAIL: " + failure);
return failures.Count == 0 ? 0 : 1;

void Check(bool condition, string label) { checks++; if (!condition) failures.Add(label); }
int OwnModifier(UnitEntityData owner, StatType stat, object source) =>
    owner.Stats.GetStat(stat).Modifiers.Where(m => m.Source == source).Sum(m => m.Amount);
void Equal<T>(T expected, T actual, string label) => Check(EqualityComparer<T>.Default.Equals(expected, actual), label + $" (expected {expected}, actual {actual})");
string Snapshot(UnitEntityData owner) => string.Join(";", owner.Progression.Features.Counts.OrderBy(p => p.Key.Name).Select(p => p.Key.Name + "=" + p.Value));
IEnumerable<BlueprintFeature> Powers(UnitPartMedium.SpiritEntry entry) => new[] {
    entry.SpiritLesserPower, entry.SpiritIntermediatePower, entry.SpiritIntermediatePowerMove,
    entry.SpiritIntermediatePowerSwift, entry.OverwriteIntermediatePower, entry.SpiritGreaterPower,
    entry.OverwriteGreaterPower, entry.SpiritSupremePower }.Where(r => r?.Get() != null).Select(r => r.Get());
MediumContextSharedSeanceComponent Seance(UnitEntityData owner)
{
    var buff = new Buff { Blueprint = BlueprintTool.Get<BlueprintBuff>(Guids.MediumSharedSeanceBuff) };
    var component = new MediumContextSharedSeanceComponent { Owner = owner, Fact = buff };
    buff.Components.Add(component);
    owner.Buffs.Enumerable.Add(buff);
    return component;
}
(UnitEntityData, UnitPartMedium, Dictionary<string, BlueprintCharacterClassReference>) NewOwner(int rank, int forgone, bool beacon)
{
    var owner = new UnitEntityData();
    owner.Progression.Features.Counts[BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower)] = rank;
    if (beacon) owner.AddFact(BlueprintTool.Get<BlueprintFeature>(Guids.AstralBeacon));
    var state = owner.Ensure<UnitPartMedium>();
    state.ForgonePowers = forgone;
    var refs = new Dictionary<string, BlueprintCharacterClassReference>();
    foreach (var id in new[] { Guids.Archmage, Guids.Hierophant, Guids.Trickster, "Champion", "Guardian", "Marshal" })
    {
        var reference = BlueprintTool.GetRef<BlueprintCharacterClassReference>(id);
        refs[id] = reference;
        BlueprintFeatureReference Feature(string suffix, int maxRanks = 1) => new() { Blueprint = new() { Name = id + suffix, Ranks = maxRanks } };
        state.Spirits[reference] = new()
        {
            SpiritLesserPower = id == Guids.Archmage ? BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpellcasterFeatProhibitArchmage)
                : id == Guids.Hierophant ? BlueprintTool.GetRef<BlueprintFeatureReference>(Guids.MediumSpellcasterFeatProhibitHierophant) : Feature("Lesser"),
            SpiritIntermediatePower = Feature("Intermediate", id == Guids.Trickster ? 10 : 1),
            SpiritIntermediatePowerMove = id == "Marshal" ? Feature("Move") : null,
            SpiritIntermediatePowerSwift = id == "Marshal" ? Feature("Swift") : null,
            OverwriteIntermediatePower = id == Guids.Archmage ? Feature("OverwriteIntermediate") : null,
            SpiritGreaterPower = Feature("Greater"),
            OverwriteGreaterPower = id is "Guardian" or "Marshal" or Guids.Trickster ? Feature("OverwriteGreater") : null,
            SpiritSupremePower = Feature("Supreme")
        };
    }
    owner.AddFact(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpellcasterFeatProhibitArchmage));
    owner.AddFact(BlueprintTool.Get<BlueprintFeature>(Guids.MediumSpellcasterFeatProhibitHierophant));
    return (owner, state, refs);
}
