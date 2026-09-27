# Sudden Attack / Haste regression

From the source root, run:

```powershell
dotnet run --project tests/SuddenAttack/SuddenAttack.csproj -- '<game>/Wrath_Data/Managed/Assembly-CSharp.dll' 'artifacts/Release/MediumClass/MediumClass.dll' '<game>/blueprints.zip'
```

The game DLL is required. The staged mod DLL and blueprint archive are optional, but use all three arguments for release verification. Omitted arguments are reported as skipped coverage. No game files are changed or copied into the test project.

The project compiles the actual production `AddSuddenAttack.cs` **and** `SuddenAttack.cs`; the component and `ConfigureEnabled()` callback execute against narrow API doubles. The attack-pool merge is not a rewritten formula: the test reads the complete installed game's static `AddExtraAttackForHand` IL, redirects its three integer fields to a test pool and its `Math.Max` call to the host runtime, then executes that IL as a dynamic method. Unsupported instructions or method shapes stop the test. This catches the precise regression where an attack intended to stack is classified into the non-stacking haste pool.

Coverage:

- Production component defaults, actual blueprint component initialization, one extra attack, unpenalized attack, and retained Manyshot suppression.
- Sudden Attack before and after Haste; another Haste remains non-stacking; after-trigger adds nothing.
- Every order of Sudden Attack, ordinary Haste, stronger haste-pool bonus, another independent bonus, and an iterative bonus. Existing full-BAB and iterative counts remain in their original pools.
- Existing charging, Flurry of Blows, and Flurry of Blows level 11 exclusions, preserving another source's Haste.
- Installed Haste blueprint reference to its effect, and that effect's actual `Number=1`, `Haste=true`, `Penalized=false` input.
- Installed native `MainAttacks` sum and its attack enumerator's zero iterative penalty path. This establishes that the independent bonus is full BAB; total attack bonus modifiers are not simulated.
- Staged delivery DLL constructor defaults, blueprint initialization values, delegate wiring, generic component attachment, and forwarding of those fields to the native API. The game DLL and delivery DLL SHA256 values are printed.

## Old-source control

Both linked source files can be replaced independently without editing production or test code:

```powershell
dotnet run --project tests/SuddenAttack/SuddenAttack.csproj '-p:ProductionComponentPath=<absolute-old-source>/AddSuddenAttack.cs' '-p:ProductionBlueprintPath=<absolute-old-source>/SuddenAttack.cs' -- '<game>/Wrath_Data/Managed/Assembly-CSharp.dll' 'artifacts/Release/MediumClass/MediumClass.dll' '<game>/blueprints.zip'
```

The delivery DLL argument always checks that exact DLL separately; overriding linked source does not rebuild or substitute the delivery DLL. Return codes: `0` all checks passed; `1` assertions failed; `2` unsupported native shape or another test error.

Verified on 2026-09-27: 0.2.5 production source + 0.2.5 staged DLL passed **796 checks**. Replacing both linked files with the backed-up 0.2.4 source yielded **256 failed assertions out of 796**, including three baseline attacks plus Sudden Attack plus Haste producing four attacks instead of the expected five. Binary-only checks in that control run still targeted the separate 0.2.5 DLL.

## Limits

This is not Unity gameplay, a combat UI test, or save-file restoration. The outer `AddExtraAttacks` routing is a narrow ordinary primary-hand, `weapon=null` model; weapon selection, secondary-hand/double-weapon routing, natural attacks, charge resolution, and other mods' Harmony changes are not executed. Only the installed native per-hand pool merger is executed; native full-BAB enumeration is verified by IL shape rather than played in combat. BlueprintCore configuration is a capture double; the staged DLL wiring is checked separately. The existing flurry/charging behavior is pinned as-is and not asserted to be a new design change. In-game full attacks with/without Haste, both application orders, and relevant weapon setups still need tester confirmation.
