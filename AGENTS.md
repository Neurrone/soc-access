# Songs of Conquest Access

This repository contains a BepInEx-based mod to make the Songs of Conquest game screen reader accessible. The mod is split in two: `soc-access/loader/` builds `SongsOfConquest.Access.Loader.dll`, the BepInEx plugin that never reloads and owns the loopback dev server, and `soc-access/soc-access.csproj` builds `SongsOfConquest.Access.dll`, a plain library the loader loads from bytes and swaps out on `POST /reload`. The development loop (launch, reload, speech capture, input injection, REPL) is documented in `docs/dev-loop.md`; read it before verifying anything in-game.

## Project Structure & Module Organization

Keep all authored mod code under `soc-access/`. Current reusable speech code lives in `soc-access/speech/` and wraps Prism for screen-reader output. Treat `decompiled/` as reference material only: it contains decompiled game and support assemblies used to find hook points, lifecycle flow, and UI structure. Do not edit decompiled files as part of the mod itself.

Layout:

- `soc-access/adapters/` for code that interacts directly with the game. Adapters must never create accessibility widgets directly or expose widget-tree concepts such as widget ids, menu item ids, row ids, column ids, or screen layout/grouping. Adapters may expose native/game text and semantic facts, such as localized building names, resource names, troop names, button text, tooltip text, counts, owners, indexes, levels, enabled/available/met state, and native focus/action hooks. Screens are responsible for constructing widgets and for accessibility/UI wording: combined row labels, slot labels, positional text, grouping labels, status strings like `unavailable`, `missing`, `disabled`, `dragging`, and any prefix/suffix such as `Missing ...` or `... lost`.
- `soc-access/input/` for receiving keyboard input
- `soc-access/patches/` for Harmony or BepInEx hook classes
- `soc-access/speech/` for Prism and output plumbing
- `soc-access/screens/` for accessible screen models. Screens can depend on adaptors and use widgets
- `soc-access/ui/` for UI widgets in the accessibility tree
- `soc-access/dev/` for the mod-side dev server routes and probes (`ModRoutes`, `WidgetDump`, `DevProbe`); development only, never spoken
- `soc-access/loader/` for the loader plugin and the loader-side dev server. Changing anything here needs a game restart; prefer a mod-side probe over a new loader member
- `soc-access/tests/` for tests
- `soc-access/soc-access.csproj` is the live mod project and currently targets `.NET Framework 4.7.2`

Adapter label rule: ask whether a string is authored by the game or composed for the accessibility widget tree. Game-authored/native text can come from adapters. Accessibility wording and composition belongs in screens/widgets.

## Localization

All user-facing text that the mod authors must be localizable. Do not add hard-coded English for spoken text, widget labels, status text, review-buffer labels, scanner messages, or accessibility-only composition.

Use the game's localized text whenever the wording is authored by the game: native UI labels, tooltips, entity names, resource names, troop names, spell names, building names, and other text that already exists in the game localization tables or UI components. Use `GameText.Get(...)` only after verifying the localization key in decompiled source or reading the native UI component text through an adapter.

Use `ModText.Get(ModStrings...)`, `ModText.Plural(...)`, and `ModText.JoinList(...)` for mod-authored accessibility wording. Avoid splitting phrases into fragments when word order could vary by language; prefer a single `ModString` with placeholders, for example `"{0}, {1}. {2}"`, instead of concatenating `", "` and `". "`.

English source text lives in `ModStrings.cs`. Non-English `.po` files live in `soc-access/translations/` and are deployed to `BepInEx/config/SongsOfConquestAccess/translations`. The `.po` filenames must match the game's `CurrentLanguage.LanguageCode` values: `de`, `es`, `fr`, `it`, `ja`, `ko`, `pl`, `ru`, `tr`, `uk`, `pt-BR`, `zh-CN`, and `zh-TW`.

After adding, removing, or changing a `ModString` or `ModPluralString`, run `dotnet run --project soc-access\tools\Localization -- update-pot`, update every `.po` file with proper translations of the string in every language (never leave English placeholders), then run `dotnet run --project soc-access\tools\Localization -- validate`. The validator must pass; it catches missing, stale, duplicate, changed-source, empty, and placeholder-mismatched translations.

## Build, Test, and Development Commands

- `dotnet build soc-access\soc-access.csproj` to build the mod and the loader. When the game folder in `GamePaths.props` exists, the build also deploys both into `BepInEx\plugins\SongsOfConquestAccess`; pass `/p:DeployToGame=false` to skip that. The mod DLL is never locked, so `dotnet build` then `POST /reload` swaps it into the running game; a changed loader DLL is locked while the game runs and needs a game restart
- `.\run-game.ps1` to build and launch the game with the dev server on; `.\wait-game.ps1 <state>` to block until it is usable (see `docs/dev-loop.md`)
- `dotnet test soc-access\tests\SongsOfConquestAccess.Tests.csproj` to run unit tests
- `dotnet run --project soc-access\tools\Localization -- update-pot` to regenerate `soc-access\translations\strings_template.pot` from `ModStrings.cs`
- `dotnet run --project soc-access\tools\Localization -- validate` to check `.po` files for missing, stale, empty, duplicate, changed-source, or placeholder-mismatched translations

Prefer fast text search over manual browsing when tracing the game code.

## Coding Style & Naming Conventions

Use C# conventions: 4-space indentation, PascalCase for types and public members, camelCase for locals and private fields unless the surrounding code already uses underscore-prefixed fields. Avoid repeating words in file names. For example, use `adapters/ContinueMenuButton.cs` instead of `adapters/ContinueMenuButtonAdapter.cs` to avoid repeating adapter.

Never use `SpeechTextSanitizer.Normalize` unless explicitly given approval to do so. It is problematic as it strips newlines.

Keep engine-specific access isolated in patch or adapter classes; keep speech composition out of hook methods. Favor small, explicit wrappers around reflected or patched game objects. Avoid creating unneeded abstractions and change code sergically so that you never implement more than what is requested.

Because the mod is hot-reloaded by the loader, every change must be reload-safe. `ModEntry.Stop()` runs `SocAccessMod.Stop()`, whose steps are isolated so one failure does not skip the rest:

- unsubscribe any events in `SocAccessMod.Stop()` (or a static `Reset()` it calls)
- dispose native resources there too
- Harmony hooks are unpatched there through a per-load unique Harmony id; never create a second Harmony instance
- avoid leaving loose `GameObject`s or components alive across reloads; a `MonoBehaviour` the mod adds to a game object needs a `DetachAll()` called from `Stop()` (see `CampaignMenuLifetimeNotifier`)
- never stop a coroutine by handle after a reload; cancel through a flag the coroutine checks (see `MainMenuPatches`)

## Native Input Equivalence

When implementing keyboard access that is meant to emulate an existing mouse action, always invoke the game's native input path instead of recreating the action's rules in the mod.

Before adding any custom validation logic, inspect the decompiled input flow and identify:

- the native public or private method that handles the action
- the hover, cursor, pointer, or selection state that method expects to already be populated
- the native feedback path for denied actions, sounds, tooltips, and notifications

Do not reconstruct movement, pathing, interaction, or actionability rules in the mod unless the user explicitly approves that tradeoff. If native behavior depends on cursor state, update or synthesize the same cursor/input state the game uses before invoking the native handler.

If native emulation behaves differently from mouse input:

1. Add targeted runtime logging at the native input boundary.
2. Compare native mouse input and accessibility-triggered input using the same tile and screen point.
3. Fix the state mismatch, not the symptom.
4. Avoid adding special-case fallback logic unless the user explicitly asks for it.

## Screen Readiness Hooks

Do not treat Unity scene load or `MonoBehaviour.Awake()` as proof that a menu is accessible-ready.

For menu screens, first identify the game's own readiness point:

- prefer a screen-specific coroutine or callback that runs after the UI is actually shown
- if the screen has a `Start()` coroutine that enables containers, waits for animations, sets the title, or plays an entry sound, hook the end of that coroutine
- if an owner or manager exposes an `OnSceneLoaded` event and the target UI is still hidden afterward, use that event only to start waiting for the specific visible container
- avoid patching `Awake()` for accessibility screen activation; `Awake()` is often too early, and under hot reload or additive scene loading it may already have run before our patch is applied

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

Tests are MSTest under `soc-access/tests/`; name files after the subject under test, for example `AnalyticsConsentScreenTests.cs`. Prioritize offline tests for text generation, focus logic, and adapter behavior. Runtime verification should be done in-game through the dev server after each hook change (`docs/dev-loop.md`): `POST /input` for keys, `GET /speech` for what was said, `GET /gui/widgets` for the whole accessible tree.

## Security & Configuration Notes

Current environment assumptions:

- `BepInEx.cfg` uses `HideManagerGameObject = true`
- The dev server is off for players: `[Dev] devServer = false` in `BepInEx/config/songs.of.conquest.access.cfg`. `run-game.ps1` writes it true for development runs; `SOCACCESS_NO_DEV=1` forces it off, `SOCACCESS_DEV_PORT` overrides the port, `SOCACCESS_NO_SPEECH=1` mutes the screen reader while `/speech` still captures
- `mcs.dll` (the REPL compiler) is deployed next to the loader for development and is never packaged in a release

## Logs

Look at `GamePaths.props` for the path to the local game install. Logs are in the `BepInEx/LogOutput.log` in the game installation; while the game runs with the dev server, `GET http://127.0.0.1:8772/log?since=N&grep=TEXT` answers the same lines without reading the file.

If you are unable to implement a feature correctly even after inspecting the decompiled source code, you should offer to add runtime logging to help with debugging, instead of continuing to guess at what might be wrong. Then once the task is complete, offer to remove the now unneeded logs.
