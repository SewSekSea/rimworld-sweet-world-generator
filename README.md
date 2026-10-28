# Sweet World Generator

Planet and biome controls, faction counts and habitable underground cavern worlds. Includes cavern ecology, wood-producing trees, violet Hair, muffalo livestock, ground trade, deeper lava chambers and inhabited faction oases.

Harmony is required; Anomaly is required for cavern-world content. Keep package/settings/namespace identifiers stable for compatibility. The legacy 1.6 source is included separately and was not rebuilt in this publication. The portable build targets only the 1.7 source. This is a source release, not a claim of compatibility with every biome or world-generation mod.

- Package ID: `unaidedmistake.worldgenerator`
- Code license: MIT. See LICENSE.
- Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3823644993

## Source release

This is the selected production source and metadata snapshot, with game, Harmony and provider assemblies omitted. Original workspaces, installed mods, active lists and player saves are unchanged. This GitHub publication does not update Steam Workshop. See BUILD-STATUS.md for fresh compilation evidence and its limits.

## Build for 1.7

Use PowerShell 7 and an installed .NET SDK. Supply your licensed game and matching Harmony assembly:

```powershell
./Build17.ps1 -GameDir 'PATH/TO/RIMWORLD' -HarmonyDll 'PATH/TO/0Harmony.dll'
```

The portable helper compiles only 1.7/Source/ into .build/1.7, without downloading dependencies, starting a game, copying a runtime or changing installed mods. Archived IDE project files may retain legacy references; the 1.7 helper is the supported build path for this snapshot. Legacy source, where included, is separate and has not been newly tested. Assets have separate terms in ASSETS.md.
