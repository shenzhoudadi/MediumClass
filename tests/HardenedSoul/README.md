# Hardened Soul optional TTT integration

Run from the source root:

```powershell
dotnet run --project tests/HardenedSoul/HardenedSoul.csproj
```

This executable links the actual production `Medium/HardenedSoul.cs`. It supplies narrow BlueprintCore/game API models and a dynamically created `TabletopTweaks-Base` assembly with the real configuration member names.

32 assertions cover own blueprint availability when TTT is absent, child prerequisites, avoiding a self-invalidating selection prerequisite, missing/disabled/unknown/throwing optional configuration, the actual reflected enable path, level-20 replacement without other-level changes, UI reference replacement, archetypes trading away the capstone, optional TTT generic choices, idempotence, and no mutation of existing unit facts.

The real TTT Base 2.6.17a DLL and local source were inspected for `TabletopTweaks.Base.Main.TTTContext`, `ModContextTTTBase.Fixes`, `Bugfixes.AlternateCapstones`, and `SettingGroup.DisableAll`. TTT creates its capstone blueprints even when the group is disabled, so checking blueprint presence alone is insufficient. Missing optional generic choices are omitted. TTT Base remains optional; no direct assembly reference was added.

The source is compiled against the real game assemblies in the normal mod build. This test is not a Unity level-up UI integration test. The influence arithmetic and 2d8 surge are tested by the separate influence/surge suite.
