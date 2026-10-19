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
single file `Class1.cs` (~2150 lines). The rest is XML defs referencing classes from that
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
- `LangVersion latest`. The committed DLLs are the build artifacts, so rebuild and commit
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
  `GeneGizmo_ResourceFuel` draws the bar; `Gene_FuelDrainMod` modifies drain.
  `HediffComp_SeverityFromFuel` ties hediff severity to fuel level. Fuel is
  refilled/consumed via `IngestionOutcomeDoer_OffsetFuel`, `JobGiver_GetFuel`,
  `Recipe_FuelInjection`, and the warden/administer workgivers.
- **Abilities** — each power is an `Ability` subclass paired with a `HediffWithComps`
  (ChargeBoost, Overdrive, Guardian). `CompAbilityEffect_FuelCost` gates casting on fuel;
  `CompAbilityEffect_DeployShield` and `CompAbilityEffect_SelfBomb` are other effects.
  `CommandAbilityToggle` is the toggle gizmo.
- **`HarmonyPatches`** (static ctor runs `new Harmony("feaurie.GS_Core")`) — e.g.
  `Patch_Pawn_PreApplyDamage`.
- **Mod settings** — `GS_Evade : Mod` + `GS_EvadeModSettings`.

When adding content: define the XML def, reference the C# class by name, rebuild the DLL.
