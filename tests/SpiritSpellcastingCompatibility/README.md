# Legacy Harmony compatibility smoke

This is an isolated executable test of the linked, unmodified production
`MediumSpiritSpellbookPatches.cs`, `UnitPartMediumPreparedSpells.cs`, and
`SpiritSpellcastingMath.cs`. It reuses the explicitly modeled game API from the
scenario suite. It does not load Unity or install Harmony patches.

The regression was a `MethodDelegate(MethodInfo, object, bool, Type[])` reference
in the rules type initializer. A query for an unrelated class's spellbook can
initialize those rules too, so the error was not limited to channeling a spirit.

The fixed code is compiled directly against the game's actual Harmony 2.0.4.0
DLL. The smoke checks initialization, ordinary spellbook queries, the actual
maximum-circle postfix preserving an ordinary result, both spirits at class
levels 1–6, and the real framework-reflection invocation that initializes the
private zero-circle preparation slot. It also initializes both active preparation
books at levels 1–6 and verifies that an existing caster spirit cannot authorize
another pool refresh. There are **183 checks**.

Run from the source root, supplying the location of the installed game DLL:

```powershell
$legacyHarmony = 'absolute/path/to/game/Wrath_Data/Managed/0Harmony.dll'
dotnet run --project tests/SpiritSpellcastingCompatibility/SpiritSpellcastingCompatibility.csproj "-p:HarmonyReferencePath=$legacyHarmony"
```

Expected output:

```text
Runtime Harmony: 2.0.4.0
PASS: 183 production compatibility checks; no Harmony patch installation and no Unity gameplay execution.
```

The smoke selects the old Harmony/MonoMod library's existing `cecil` dynamic
method backend through `MONOMOD_DMD_TYPE` in this process. The legacy default
dynamic emitter predates .NET 9 and fails while creating `FieldRefAccess` on
this test host. This setting is only for the test host; it is not shipped in,
or required by, the mod. The compatibility run does not replace Harmony or
its `FieldRefAccess` implementation.

## Old-source negative control

Use the archived 0.1.9 compatibility test sources with an external copy of the
0.1.8 production patch file; current tests call newer multiple-spirit APIs.
Compile that saved test project against
modern Harmony, then execute it with the old game Harmony. Because .NET 9
enforces an assembly version check before resolving the missing method,
rebind only the test assembly's Harmony reference version to 2.0.4.0. This
models the Unity/Mono binding visible in the reported game log. **No production
method body, call signature, or DLL from either Harmony version is edited.**

The following uses Mono.Cecil from the existing static suite. Set the three
input file locations before running from the source root:

```powershell
$legacyHarmony = 'absolute/path/to/game/Wrath_Data/Managed/0Harmony.dll'
$modernHarmony = 'absolute/path/to/Harmony/2.3.6/0Harmony.dll'
$oldPatches = 'absolute/path/to/0.1.8/MediumSpiritSpellbookPatches.cs'
$project = 'tests/SpiritSpellcastingCompatibility/SpiritSpellcastingCompatibility.csproj'
$controlDir = 'tests/SpiritSpellcastingCompatibility/bin/OldSource'

dotnet build tests/SpiritSpellcasting/SpiritSpellcasting.Tests.csproj
dotnet build $project "-p:HarmonyReferencePath=$modernHarmony" "-p:ProductionPatchesPath=$oldPatches" -o $controlDir
Copy-Item -LiteralPath $legacyHarmony -Destination "$controlDir/0Harmony.dll" -Force

$cecilPath = (Resolve-Path 'tests/SpiritSpellcasting/bin/Debug/net9.0/Mono.Cecil.dll').Path
[void][Reflection.Assembly]::LoadFrom($cecilPath)
$smokePath = (Resolve-Path "$controlDir/SpiritSpellcastingCompatibility.dll").Path
$smokeModule = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($smokePath)
$harmonyReference = $smokeModule.MainModule.AssemblyReferences | Where-Object Name -eq '0Harmony'
$harmonyReference.Version = [Version]'2.0.4.0'
$reboundPath = [IO.Path]::ChangeExtension($smokePath, '.rebound.dll')
$smokeModule.Write($reboundPath)
$smokeModule.Dispose()
Copy-Item -LiteralPath $reboundPath -Destination $smokePath -Force
dotnet "$controlDir/SpiritSpellcastingCompatibility.dll" --expect-missing-method
```

The control must print `EXPECTED OLD-SOURCE FAILURE` followed by a
`MissingMethodException` naming the four-argument `AccessTools.MethodDelegate`
overload. The command exits successfully only because this is the expected
negative result. An unrelated failure, or an unexpected pass, exits with code 1.

The arithmetic/progression regression and installed game IL contracts are
covered separately by `SpiritSpellcasting.Tests`; complete Harmony patch
behavior against the API model is covered by `SpiritSpellcastingScenarios`.
None of these tests claims to exercise the in-game class-selection UI or to
verify an actual Kineticist-to-Sorcerer selection sequence.
