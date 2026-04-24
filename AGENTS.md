# Songs of Conquest Access

This repository contains a BepInEx-based mod to make the Songs of Conquest game screen reader accessible. The active mod project is built for Script Engine hot reload using the BepInEx script reload plugin.

Use the game accessibility skills in this repository.

## Project Structure & Module Organization

Keep all authored mod code under `soq-access/`. Current reusable speech code lives in `soq-access/speech/` and wraps Tolk for screen-reader output. Treat `decompiled/` as reference material only: it contains decompiled game and support assemblies used to find hook points, lifecycle flow, and UI structure. Do not edit decompiled files as part of the mod itself.

Layout:

- `soq-access/adapters/` for any code that interacts directly with the game
- `soq-access/input/` for receiving keyboard input
- `soq-access/patches/` for Harmony or BepInEx hook classes
- `soq-access/speech/` for Tolk and output plumbing
- `soq-access/screens/` for accessible screen models
- `soq-access/ui/` for UI widgets in the accessibility tree
- `soq-access/tests/` for any offline tests introduced later
- `soq-access/soq-access.csproj` is the live mod project and currently targets `.NET Framework 4.7.2`

## Build, Test, and Development Commands

Useful commands:

- `rg "ShowAnalyticsConsentIfNecessary" decompiled` to trace game behavior quickly
- `dotnet build soq-access\soq-access.csproj` to build the mod locally
- `dotnet build soq-access\soq-access.csproj /p:DeployToGame=true` to build and copy the DLL to `BepInEx\scripts` for Script Engine hot reload
- `dotnet test <path-to-test.csproj>` to run automated tests once test projects are added

Prefer fast text search over manual browsing when tracing the game code. Do not change deployment to `BepInEx\plugins`; Script Engine loads the mod from `BepInEx\scripts`.

## Coding Style & Naming Conventions

Use C# conventions: 4-space indentation, PascalCase for types and public members, camelCase for locals and private fields unless the surrounding code already uses underscore-prefixed fields. Avoid repeating words in file names. For example, use `adapters/ContinueMenuButton.cs` instead of `adapters/ContinueMenuButtonAdapter.cs` to avoid repeating adapter.

Keep engine-specific access isolated in patch or adapter classes; keep speech composition out of hook methods. Favor small, explicit wrappers around reflected or patched game objects. Avoid creating unneeded abstractions and change code sergically so that you never implement more than what is requested.

Because the mod is hot-reloaded by Script Engine, every change must be reload-safe:

- unsubscribe any events in `OnDestroy()`
- dispose native resources in `OnDestroy()`
- unpatch Harmony hooks on unload
- avoid leaving loose `GameObject`s or components alive across reloads

## Screen Readiness Hooks

Do not treat Unity scene load or `MonoBehaviour.Awake()` as proof that a menu is accessible-ready.

For menu screens, first identify the game's own readiness point:

- prefer a screen-specific coroutine or callback that runs after the UI is actually shown
- if the screen has a `Start()` coroutine that enables containers, waits for animations, sets the title, or plays an entry sound, hook the end of that coroutine
- if an owner or manager exposes an `OnSceneLoaded` event and the target UI is still hidden afterward, use that event only to start waiting for the specific visible container
- avoid patching `Awake()` for accessibility screen activation; `Awake()` is often too early, and under Script Engine hot reload or additive scene loading it may already have run before our patch is applied

Examples:

- Main menu: `MainMenu.HandleSceneLoaded(MainMenuSceneType.MainMenu)` is the stable transition signal, but the menu is not usable until `_leftButtonContainer.activeInHierarchy`. Start a coroutine from the scene-loaded hook and push `MainMenuScreen` only after that container is active.
- Campaign select: `CampaignMenu.Awake()` is unreliable and too early. The reliable readiness point is the end of `CampaignMenu.Start()`, after it enables `_campaignButtonContainer`, waits `0.3s`, sets the title, and plays the campaign select entry sound. Wrap the `Start()` coroutine and call the accessibility detector after the original coroutine completes.

When adding a new screen:

1. Inspect decompiled lifecycle code before choosing hooks.
2. Find the exact game state that means "the user can now interact with this screen."
3. Gate `Screen.IsPresent()` on that state, not merely on the object existing.
4. Prefer a semantic readiness hook over frame-count delays.
5. Use bounded runtime probes for hot reload recovery, not as the primary first-entry mechanism.
6. Keep temporary readiness logs only while debugging; remove them once the hook is proven.

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

## Logs

Look at `GamePaths.props` for the path to the local game install. Logs are in the `BepInEx/LogOutput.log` in the game installation.

If you are unable to implement a feature correctly even after inspecting the decompiled source code, you should offer to add runtime logging to help with debugging, instead of continuing to guess at what might be wrong. Then once the task is complete, offer to remove the now unneeded logs.
