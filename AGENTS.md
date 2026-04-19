# Songs of Conquest Access

This repository contains a BepInEx-based mod to make the Songs of Conquest game screen reader accessible. The active mod project is built for Script Engine hot reload.

Use the game accessibility skills in this repository.

## Project Structure & Module Organization

Keep all authored mod code under `soq-access/`. Current reusable speech code lives in `soq-access/speech/` and wraps Tolk for screen-reader output. Treat `decompiled/` as reference material only: it contains decompiled game and support assemblies used to find hook points, lifecycle flow, and UI structure. Do not edit decompiled files as part of the mod itself.

Layout:

- `soq-access/speech/` for Tolk and output plumbing
- `soq-access/patches/` for Harmony or BepInEx hook classes
- `soq-access/screens/` for accessible screen models
- `soq-access/tests/` for any offline tests introduced later
- `soq-access/soq-access.csproj` is the live mod project and currently targets `.NET Framework 4.7.2`

## Build, Test, and Development Commands

There is no root solution or committed build script yet. Add new build artifacts under `soq-access/`.

Useful commands:
- `rg "ShowAnalyticsConsentIfNecessary" decompiled` to trace game behavior quickly
- `dotnet build soq-access\soq-access.csproj` to build the mod locally
- `dotnet build soq-access\soq-access.csproj /p:DeployToGame=true` to build and copy the DLL to `BepInEx\scripts` for Script Engine hot reload
- `dotnet test <path-to-test.csproj>` to run automated tests once test projects are added

Prefer fast text search over manual browsing when tracing the game code. Do not change deployment to `BepInEx\plugins`; Script Engine loads the mod from `BepInEx\scripts`.

## Coding Style & Naming Conventions

Use C# conventions: 4-space indentation, PascalCase for types and public members, camelCase for locals and private fields unless the surrounding code already uses underscore-prefixed fields. Keep engine-specific access isolated in patch or adapter classes; keep speech composition out of hook methods. Favor small, explicit wrappers around reflected or patched game objects.

Because the mod is hot-reloaded by Script Engine, every change must be reload-safe:
- unsubscribe any events in `OnDestroy()`
- dispose native resources in `OnDestroy()`
- unpatch Harmony hooks on unload
- avoid leaving loose `GameObject`s or components alive across reloads

## Testing Guidelines

No test framework is committed yet. When tests are added, place them under `soq-access/tests/` and name files after the subject under test, for example `AnalyticsConsentScreenTests.cs`. Prioritize offline tests for text generation, focus logic, and adapter behavior. Runtime verification should be done in-game through BepInEx after each hook change.

Current smoke test: on startup, the mod should speak and log `Songs of Conquest Access v0.1 ready`.

## Commit & Pull Request Guidelines

This directory is not currently a git repository, so there is no local commit history to mirror. Use short imperative commit messages, such as `Add analytics consent popup hook`. PRs should describe the user-facing behavior, target game screen, affected hook points, manual test steps, and any required screenshots or speech transcripts.

## Security & Configuration Notes

Game DLLs live outside the repo at `D:\games\steam\steamapps\common\SongsOfConquest\SongsOfConquest_Data\Managed`. Reference them for analysis and local builds, but do not copy proprietary binaries into `soq-access/`.

Current environment assumptions:
- `BepInEx.cfg` uses `HideManagerGameObject = true`
- Script Engine config uses `DumpAssemblies = true` for debugging reload behavior
