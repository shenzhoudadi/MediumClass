# Mythic ability registration regression

Run from the source root:

```text
dotnet run --project tests/MythicRegistration/MythicRegistration.csproj -- bin/Release/net472/BlueprintCore.dll
```

This suite links the actual production MythicSpirits, MythicInfluence and Guids
files. It checks their two original saved GUIDs, names, groups and prerequisites,
one entry per ability in the normal and extra mythic ability selections, and
preservation of unrelated entries. It currently passes 36 assertions.

The explicit BlueprintCore model includes automatic group population with GUID
de-duplication and the manual append path without it. A negative control performs
the former automatic registration followed by the manual append and verifies
that it reproduces two entries. The production code now uses only automatic
registration, which reaches all matching selection groups.

Mono.Cecil reads the supplied BlueprintCore DLL to verify the model's relevant
contract: New enables selection updates, OnConfigureCompleted calls
PopulateSelections, and population checks group membership and existing entries
before appending. These are offline checks of production configuration and an
explicit registration model; they do not start Unity or render the game's UI.
