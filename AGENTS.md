# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Two RimWorld mods for 1.6 and 1.7, community ports of Feaurie V's originals (author stopped
maintaining). No ownership claimed — see each mod's `README.org`.

- **greyscythe-cybergenetics** (`packageId feaurie.GreyscytheGenes`) — Biotech cybernetic
  genes for supersoldiers. Ships a C# assembly (`GS_Core.dll`).
- **greyscythe-bionics-catalogue** (`packageId feaurie.GreyscytheBionics`) — Prosthetics/
  bionics for every tech level (neolithic → archotech). Pure XML defs, no assembly.
  Depends on cybergenetics at load time.

Both depend on Harmony + Vanilla Expanded Framework (VEF). Cybergenetics optionally
integrates with Vanilla Races Expanded: Android (via `loadFolders.xml` `IfModActive`).

## Layout

Each mod is a standard RimWorld mod: `About/` (metadata + `PublishedFileId.txt` for
Workshop), versioned content dirs `1.6/` and `1.7/` (`Defs/`, `Assemblies/`), `Textures/`,
`loadFolders.xml`. `loadFolders.xml` has a `<v1.6>` and a `<v1.7>` block, each also mapping
active-mod conditions to overlay dirs (the Android-compat patch for cybergenetics).

`1.6/` is frozen: don't edit it for 1.7 work. 1.7 is the Waylith engine (.NET 10, Harmony 3);
`1.7/Defs` started as a copy of `1.6/Defs`, so a def fix usually needs applying to both.
The Android overlay in `1.7/Mods/` is unverified: Vanilla Races Expanded: Android has no 1.7 branch yet.

C# source lives at `greyscythe-cybergenetics/Source/GS_Core/`; all game logic is in the
single file `Class1.cs` (~1500 lines). The rest is XML defs referencing classes from that
assembly by name.

## Build

Enter the devenv shell first (provides .NET 10 SDK + csharpier); direnv auto-loads it via
`.envrc`. Only cybergenetics has code to build.

```sh
devenv shell -- dotnet build greyscythe-cybergenetics/Source/GS_Core/GS_Core.csproj -c Release
```

- `-p:GameVersion=1.7` (default) builds for 1.7: `net10.0`, `DefineConstants v1_7`,
  `Krafs.Rimworld.Ref 1.7.4982-unstable`, `Lib.Harmony 3.0.0-rc.3`, output
  `1.7/Assemblies/GS_Core.dll`.
- `-p:GameVersion=1.6` builds for 1.6: `net472`, `DefineConstants v1_6`,
  `Krafs.Rimworld.Ref 1.6.4871`, `Lib.Harmony 2.3.6`, output `1.6/Assemblies/GS_Core.dll`.
  The committed 1.6 DLL is frozen (SDK 9 and SDK 10 emit different bytes for the same
  source); only rebuild it deliberately (`just build-1-6`).
- `LangVersion 9.0` (set in `Directory.Build.props`); no newer C# syntax. The committed DLLs are the build artifacts, so rebuild and commit
  the 1.7 DLL when changing C#. The source must compile for both versions.
- Harmony 3 type-checks injected parameters: `__instance` must be the plain declaring type,
  never `ref`.
- VEF types are referenced at runtime only (a mod dependency, not a package).

No test suite — verify in-game.

## Package for Workshop

Each mod's `justfile` has a `rel` target that stages the shippable folders (both `1.6` and `1.7`) into `~/.cache/greyscythe-workshop`:

```sh
cd greyscythe-cybergenetics && just rel   # → ~/.cache/greyscythe-workshop/Greyscythe_Cybergenetics
cd greyscythe-bionics-catalogue && just rel # → ~/.cache/greyscythe-workshop/Greyscythe_Bionics
```

## Conventions

- C# formatted with **CSharpier** (`devenv shell -- csharpier format .`).
- XML formatted with `xmllint` (see commit `9203800` for the reformat pass).
- Line endings normalized (dos2unix); PNGs run through oxipng.
- Commit messages are tagged by area: `[gs-gene]`, `[gs-bionics]`, `[misc]`.

## Class1.cs architecture

All in `namespace GS_Core`. Key pieces, grouped:

- **`*DefOf` static classes** (`GS_GeneDefOf`, `GS_HediffDefOf`, `GS_StatDefOf`,
  `GS_ThingDefOf`) — link XML def names to code.
- **Fuel resource system** — `Gene_Fuel : Gene_Resource, IGeneResourceDrain` is the core
  resource gene; `FuelUtility` holds the shared offset/drain/gizmo helpers;
  `GeneGizmo_ResourceFuel` draws the bar.
  `HediffComp_SeverityFromFuel` ties hediff severity to fuel level. Fuel is
  refilled/consumed via `IngestionOutcomeDoer_OffsetFuel`, `JobGiver_GetFuel`,
  `Recipe_FuelInjection`, `Workgiver_AdministerFuel` and `WorkGiver_Warden_DeliverFuel`.
- **Abilities** — all three powers (ChargeBoost, Overdrive, Guardian) use the single
  `Ability_FuelToggleBuff : Ability`, a toggle that applies the matching hediff from
  `GS_HediffDefOf`. `CompAbilityEffect_FuelCost` gates casting on fuel;
  `CompAbilityEffect_DeployShield` and `CompAbilityEffect_SelfBomb` are other effects.
  `CommandAbilityToggle` is the toggle gizmo.
- **`HarmonyPatches`** (static ctor runs `new Harmony("feaurie.GS_Core")`) — e.g.
  `Patch_Pawn_PreApplyDamage`.
- **Mod settings** — `GS_Evade : Mod` + `GS_EvadeModSettings`.

When adding content: define the XML def, reference the C# class by name, rebuild the DLL.

## Dev loop & publishing

Root `justfile` (needs `nu`; `run` also needs `steam-run` and a Steam install of RimWorld
under `$XDG_DATA_HOME/Steam/steamapps/common`):

- `just link` symlinks both mod folders into the game's `Mods/` dir, so the game always
  sees the working tree. It replaces existing symlinks and stops if a real folder is there.
- `just mods-config` **overwrites** the game's `ModsConfig.xml` with a fixed load order
  (Harmony, DLCs, VEF core, VGE, both mods, `ISOREX.PawnEditor`). The first run copies the
  original to `ModsConfig.xml.bak` and later runs keep that copy. The mod list is hardcoded;
  edit the justfile to change it.
- `just run` = `link` + `mods-config` + launch the game via `steam-run`.

Each mod's `justfile` also publishes to the Workshop (needs `steamcmd`, `nu`):

- `just vdf` runs `rel`, then writes the steamcmd workshop VDF (from `About/PublishedFileId.txt`,
  `desc.txt`, `About/preview.png`) to `~/.cache/greyscythe-workshop/<name>.vdf`.
  `CHANGENOTE` sets the change note (default "Update from source repo").
- `just publish` runs `vdf`, then uploads with steamcmd. It refuses to upload when the mod
  directory has uncommitted changes, including a rebuilt DLL that differs from the committed
  one. Requires `STEAM_USER` (e.g. `STEAM_USER=me just publish`); the first login prompts
  for password + Steam Guard.
