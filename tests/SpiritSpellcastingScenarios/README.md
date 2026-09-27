# Low-level production-code scenarios

Run from the source directory:

```text
dotnet run --project tests/SpiritSpellcastingScenarios/SpiritSpellcastingScenarios.csproj
```

This console project links the actual `MediumSpiritSpellbookPatches.cs`,
`MediumSpiritActionBarPatch.cs`, `UnitPartMediumPreparedSpells.cs`, and `SpiritSpellcastingMath.cs`. It installs all
production Harmony patches using Lib.Harmony 2.3.6. Progression tables are parsed
from the production blueprint declarations; expected outcomes are separate fixed
low-level rule data.

The scenarios cover Medium levels 1–6 and both Archmage and Hierophant: ordinary
progression, active spirit progression, UI base counts, missing/zero/stale
preparation books, read-only queries, one preparation selection per available
circle, temporary known-spell grants, spending from the Medium pool, blocking
casts from preparation books, replacing preparations, preservation of intrinsic
known spells, dechannel cleanup and slot clamps, restoration without refilling,
missing/stale main books, lesser-power forfeiture, and low Charisma.
It also reproduces the native omission of zero-circle learning and slot creation:
native level-up learning starts above the old maximum (already zero), and native
slot resizing starts at circle one. The production initialization must supply
the cantrip list and zero-circle preparation slot. Action-bar scenarios enumerate
preparation and Medium copies in both orders and ensure the castable Medium copy
survives deduplication while unrelated books and spell-like abilities survive.
The separate memorized-slot insertion path is modeled independently, matching
the native `TryAddSpell(List<SpellSlot>, SpellSlot)` signature. Preparation slots
are hidden there too, while depleted spells from unrelated books remain visible.

Multiple-spirit scenarios cover levels 1–6, 13 and 20 in both caster orders.
They start with a noncasting spirit, preserve casts already spent before adding
the first caster, add only the increase in daily capacity, and verify that the
second caster never refills the shared pool. Both preparation books contribute
selected spells to the main book; overlapping selections survive until the last
source is removed. Removing one caster, removing the last caster, and loading
after a cast exercise synchronization without replenishment.

Revoked-spell tests retain the original AbilityData before forgetting or ending
channeling, verify its shortcut is removed, and call CanSpend, Spend and the
native four-argument SpendInternal separately. The native pool model deliberately
does not check known spells: this reproduces the stale-shortcut vulnerability.
Tests include cantrips, parent variants, converted spells, overlapping preparation
books, permanent promotion, other active temporary grants and unrelated books.
Native metamagic construction is modeled without temporary/source metadata: tests
revoke dependent recipes, retain permanent recipes and distinguish a different
known base circle from the recipe's resulting circle. Revoked UI counts are zero.

Cached-action-bar scenarios cover both spirits at levels 1–6: an existing empty
spell group, preselected spells when rechanneling, preparation fields restored
after an early synchronization, both books together, and selection changes.
The model deliberately caches the group: AddKnownTemporary and Spellbook.Rest
do not refresh it. Native Dirty/Memorize notification paths trigger collection;
the actual production Fetch prefix repairs late selections before enumeration.
Repeated Sync/Fetch must leave Dirty clear and retain spent casts. Synchronous
custom-removal and action-bar-update callbacks reenter Fetch during Clear to
verify dismissal cannot regrant spells or mutate its grant enumeration. The
separate SpiritActionBar suite checks these notification/collection contracts
against IL in the installed game DLL.

Load-slot preservation scenarios cover levels 1, 4, 7, 10 and 20, with no spirit,
either caster spirit, and both together. Fresh owners receive JSON-round-tripped
remaining-count arrays with full, partially spent and exhausted pools. A staged
CHA 20 -> 18 -> 20 recovery exercises a temporarily smaller daily capacity;
three consecutive load cycles and UI synchronization must preserve all saved
counts without refilling. This is an explicit ordering stress case, not evidence
that a tester's actual save necessarily has that exact attribute transition.
True forfeiture and channel-end transitions still clamp through their explicit
lifecycle paths before loading.

The 0.2.4 production sources pass **5,483 assertions**. The identical suite linked
to the saved 0.2.3 spellbook patches fails 114 assertions (5,369/5,483 passed), all
in the new load-slot preservation scenarios.

The earlier 0.1.9 production code passed 2,425 assertions. The earlier tests
linked to the saved 0.1.7 patch source, with the new action-bar patch omitted,
failed 222 assertions (2,067/2,289 passed), including zero-circle selection and
preparation-first action-bar ordering. The total differs because a failed
temporary grant skips dependent casting assertions rather than dereferencing null.

The project has optional MSBuild properties `ProductionPatchesPath` and
`IncludeActionBarPatch`. Historical comparisons require their matching saved
test sources because the current suite calls the new multiple-spirit APIs.
Normal runs use the current production sources.

## Explicit limits

`GameApiModel.cs` is a minimal substitute for game API objects. Storage, native
methods, unit parts, and progression features are modeled; spirit logic is not
copied into it. The tests exercise real production patches on that model, not
real Unity objects. The saved-slot round trip uses JSON for the remaining-count
array and invokes the actual production post-load patch; it does not run the
game's serializer or reconstruct its entire object graph. Native caster-level
modifiers, spellbook registration, Unity UI event dispatch/rendering, buff lifecycle,
full spell-list loading, and native object serialization still require native
code inspection or game testing. A pass is not a claim of in-game validation.
