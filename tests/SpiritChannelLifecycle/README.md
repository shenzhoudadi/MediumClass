# Spirit channel lifecycle scenarios

Run from the source root:

```text
dotnet run --project tests/SpiritChannelLifecycle/SpiritChannelLifecycle.csproj
```

This executable links the actual production `ApplySpirits.cs`,
`MediumContextSharedSeanceComponent.cs`, `MediumTranceOfThreeComponent.cs`,
`MediumContextSpiritBonusComponent.cs` and `MediumInfluencePenaltyComponent.cs`.
It currently passes **4,883 assertions**
across power ranks 1–4, forfeiting 0–4 powers,
three primary-spirit choices, multiple simultaneous spirits, and level-20
Astral Beacon combinations.

Checks include granting newly active powers, caster preparation prohibitions,
optional absent powers, repeated activation and upgrade callbacks, replacing
Beacon's secondary variants, enforcing forfeited tiers after an additional
spirit becomes fully channeled, granting only the newly earned Trickster rank
at a class-level boundary, and removing every Trickster rank when channeling
ends. A saved state with no additional-spirit list exercises old-save teardown.

Shared-seance scenarios exercise legacy null data, the post-load migration
marker surviving turn-on, cleanup after the primary selection has cleared,
preserving unrelated boons, and transferring rank-one boon ownership between
two active Mediums until the last provider ends its channel.

Trickster trance scenarios switch one shared blueprint component between two
owners and two runtime data objects, restore recorded grants, migrate legacy
null runtime data, respect a partially filled feature-rank limit, and preserve
newly earned Astral Beacon or formally channeled ranks when the temporary buff
ends. Each runtime records the ranks actually added, rather than storing a
caster-dependent count on the shared blueprint component.

Modifier scenarios execute the two production stat components against explicit
modifier storage. They check immediate bonus and penalty changes on level-up,
additional spirits and shared stats, Spirit Focus, repeated refresh without
duplication, teardown and preservation of unrelated modifier sources. A fact
that is attached but not turned on is skipped until native activation.

The refresh uses native `EntityFact.CallComponents`, whose inspected IL obtains
the source blueprint component and opens its runtime `RequestEventContext`
before invoking the callback. It does not rely on `Buff.Reapply` activating
anything in a disabled/preview entity. Native level-up recalculates buff contexts;
that alone does not replace integer modifiers previously created by these
components. The added refresh explicitly replaces only their own modifiers.

Mythic penalty scenarios link the production `InfluencePenaltyMath` and execute
the actual penalty component for zero through six formal spirits, with and
without the mythic ability. They verify downward rounding of positive penalty
magnitudes, reduced initiative, unchanged positive Will and spirit bonuses,
immediate changes on additional channels, fewer spirits, rank-up and gaining or
losing the mythic feature. Secondary-only attachment and Astral Beacon powers do
not increase the denominator. Marshal's forced defensive fighting is retained.

Native save compatibility was inspected in the installed game assembly:
`DefaultJsonSettings` registers `ComponentDelegateRuntimeConverter`, which
writes a null data runtime as JSON null. `EmptyComponentData` is a reference
type and the runtime constructor leaves its data null. When reading a null
`EntityFact.ComponentsDictionary` entry, the native setter calls the current
blueprint's `CreateRuntimeFactComponent`. Therefore this old empty runtime is
rebuilt as the new data-bearing runtime rather than deserialized as an obsolete
generic type. Nonempty runtimes retain their generic `$type` and do not have
that same compatibility guarantee.

The native `RestoreOwnerLink` only reconnects the fact. `PostLoadComponents`
then invokes component `OnPostLoad`; it does not call fresh `OnInitialize`.
The production component uses those separate callbacks to distinguish an old
null-data runtime from a newly created buff. Cleanup is postponed until its
normal turn-off, so merely loading a save does not remove another character's
boons.

The API model reproduces feature rank behavior confirmed in the installed game
assembly: attaching the same blueprint increases the existing feature rank up
to the blueprint limit, and each removal decreases only one rank. This is what
makes the multi-rank removal assertions meaningful.

The model replaces game storage, part/catalog infrastructure and book/influence
callbacks; it does not reimplement the production power-grant logic. It does
not execute Unity, actual serialized save graphs, game event order, components
inside individual feature blueprints, or ability execution. Separate suites
exercise the linked preparation-book and influence implementations. These
results are offline checks, not a claim of in-game validation.
