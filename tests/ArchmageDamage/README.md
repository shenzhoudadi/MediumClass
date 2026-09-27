# Archmage seance damage

Runs the production component with explicit damage-event doubles, and reads the installed game DLL and `blueprints.zip` to validate the tested spell structure. No game blueprints or binaries are copied into the source package.

```powershell
dotnet run --project tests/ArchmageDamage/ArchmageDamage.csproj -- 'D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/Assembly-CSharp.dll' 'D:/steam/steamapps/common/Pathfinder Second Adventure/blueprints.zip'
```

The bonus is a fixed +2 to the first eligible chunk of a spell damage bundle, regardless of dice count or element count. Independent rays and ongoing ticks each have a new damage event. Existing native critical, empower, half-damage and resistance rules still apply. Hellfire Ray's unholy half and subsequent AoE targets reuse an already modified raw roll, so they receive no second modifier.

Scenarios cover Elemental Assessor's four zero-dice damage chunks and periodic ticks, Hellfire Ray's fire/raw-write/unholy/raw-read sequence with multiple rays, critical/empower cases, AoE reuse, duplicate notifications, precision/empty/ignored chunks, independent bonus sources and exclusion of weapon hits whose reason points at a spell buff. The simplified numerical evaluator follows the relevant native `CalculateDamageValue` branches; it is not an execution of Unity or the whole game damage pipeline.
