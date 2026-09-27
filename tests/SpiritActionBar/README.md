# Spirit action bar regression checks

This suite links the production preparation-book filters and shortcut migration patches. Its game-facing types are a small API model; it does not launch Unity or prove rendering/gameplay compatibility.

Modern Harmony run (installs the actual patches on the model and checks the real game DLL's target signatures):

```powershell
dotnet run --project tests/SpiritActionBar/SpiritActionBar.csproj -- 'D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/Assembly-CSharp.dll' artifacts/Release/MediumClass/MediumClass.dll
```

The UI suite covers both preparation books at circles 0–6, memorized and spontaneous popup paths, manual/context-menu shortcut insertion, four slot representations, saved shortcut remapping/cleanup, exact metamagic matching, preserving depleted real spells and all three preparation-screen books, and the old fourth Hierophant entry. It tests raw-slot cleanup running before `GetSlot`, as confirmed by the native `ActionBarManager.Update` implementation. With the native DLL and without the optional built mod, the suite currently runs 555 assertions.

Refresh checks inspect native IL rather than assuming that learning or resting rebuilds the UI. They verify AddKnownTemporary's LearnSpell-only event, the ActionBarVM subscription gap, silent Spellbook.Rest, Dirty-triggered full collection, unconditional Fetch even for an empty prior group, runtime spell-level visibility, and OnUnitUpdated-to-view redraw. Compiled-mod checks require Fetch's repair to use Sync(false) without refill/dirty-loop entry and require channel notification after slot initialization. The linked prefix test checks that synchronization runs before the collection snapshot; the full low-level cached-menu scenarios are in SpiritSpellcastingScenarios.

The conversion regression inspects the actual game's `AbilityData.SpellLevel` precedence and the compiled mod's conversion constructors and call sites. All Archmage (including Wild Arcana) and Hierophant plain/variant conversions must pass the selected spell-list circle into `OverrideSpellLevel`. A small model reproduces the former error where a converted spell inherited the menu's half-character-level fallback. Passing the built mod DLL adds compiled production wiring checks; the model alone is not a test of Unity spell execution.

Old Harmony full patch installation on Windows .NET Framework:

```powershell
dotnet restore tests/SpiritActionBar/SpiritActionBar.csproj -p:TargetFramework=net472 '-p:HarmonyReferencePath=D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/0Harmony.dll'
dotnet run --no-restore --project tests/SpiritActionBar/SpiritActionBar.csproj -p:TargetFramework=net472 '-p:HarmonyReferencePath=D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/0Harmony.dll' -- 'D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/Assembly-CSharp.dll'
```

This uses the actual 2.0.4.0 Harmony DLL to install all linked production UI patches on the API model. It does not run Unity. Restore the project again when switching target frameworks or Harmony references.

Old Harmony reflection-only smoke on .NET 9:

```powershell
dotnet run --project tests/SpiritActionBar/SpiritActionBar.csproj '-p:HarmonyReferencePath=D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/0Harmony.dll' -- --reflection-smoke 'D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/Assembly-CSharp.dll'
```

This checks the production methods directly under the actual Harmony 2.0.4.0 assembly and validates native target signatures. It deliberately does not install detours: the old bundled MonoMod cannot emit methods containing locals on a .NET 9 host (`LocalBuilder` is now abstract). This test-host limitation is not patched into the game and must not be described as successful Unity execution.

The production UI migration reads saved slots before native `IsBad` cleanup and also handles the shared `GetBadSlotReplacement` fallback, because `UpdateBadSlots` can otherwise discard a hidden legacy shortcut before `GetSlot` runs. The two preparation book IDs alone determine preparation filtering; generic disabled/resource-depleted status never does.
