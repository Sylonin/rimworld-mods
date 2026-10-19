# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Two RimWorld 1.6 mods, community 1.6 ports of Feaurie V's originals (author stopped
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
Workshop), `1.6/` (versioned `Defs/`, `Assemblies/`), `Textures/`, `loadFolders.xml`.
`loadFolders.xml` maps active-mod conditions to overlay dirs — that's where the
Android-compat patch is wired in for cybergenetics.

C# source lives at `greyscythe-cybergenetics/Source/GS_Core/`; all game logic is in the
single file `Class1.cs` (~2150 lines). The rest is XML defs referencing classes from that
assembly by name.

## Build

Enter the devenv shell first (provides .NET 9 SDK + csharpier); direnv auto-loads it via
`.envrc`. Only cybergenetics has code to build.

```sh
devenv shell -- dotnet build greyscythe-cybergenetics/Source/GS_Core/GS_Core.csproj
```

- Targets `net472`, `LangVersion 9.0`, `DefineConstants v1_6`.
- Build output goes straight into `1.6/Assemblies/GS_Core.dll` (via `<OutputPath>` in
  `GS_Core.csproj`) — the committed DLL is the build artifact, so rebuild and commit it
  when changing C#.
- Refs come from NuGet: `Krafs.Rimworld.Ref` (game assemblies) + `Lib.Harmony`. VEF types
  are referenced but VEF is a runtime mod dependency, not a package.

No test suite — verify in-game.

## Package for Workshop

Each mod's `justfile` has a `rel` target that stages the shippable folders into `/tmp`:

```sh
cd greyscythe-cybergenetics && just rel   # → /tmp/Greyscythe_Cybergenetics
cd greyscythe-bionics-catalogue && just rel # → /tmp/Greyscythe_Bionics
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
