# Songs of Conquest Access

This repository contains a BepInEx-based mod to make the Songs of Conquest game screen reader accessible. The mod is split in two: `soc-access/loader/` builds `SongsOfConquest.Access.Loader.dll`, the BepInEx plugin that never reloads and owns the loopback dev server, and `soc-access/soc-access.csproj` builds `SongsOfConquest.Access.dll`, a plain library the loader loads from bytes and swaps out on `POST /reload`. The development loop (launch, reload, speech capture, input injection, REPL) is documented in `docs/dev-loop.md`; read it before verifying anything in-game.

## Project Structure & Module Organization

Keep all authored mod code under `soc-access/`. Current reusable speech code lives in `soc-access/speech/` and wraps Prism for screen-reader output. Treat `decompiled/` as reference material only: it contains decompiled game and support assemblies used to find hook points, lifecycle flow, and UI structure. Do not edit decompiled files as part of the mod itself.

Layout:

- `soc-access/adapters/` for code that interacts directly with the game. Adapters must never build graph nodes directly or expose graph concepts such as node ids, stops, regions, row or column ids, or screen layout/grouping. Adapters may expose native/game text and semantic facts, such as localized building names, resource names, troop names, button text, tooltip text, counts, owners, indexes, levels, enabled/available/met state, and native focus/action hooks. Screens are responsible for constructing nodes and for accessibility/UI wording: combined row labels, slot labels, positional text, grouping labels, status strings like `unavailable`, `missing`, `disabled`, `dragging`, and any prefix/suffix such as `Missing ...` or `... lost`.
- `soc-access/input/` for receiving keyboard input
- `soc-access/patches/` for Harmony or BepInEx hook classes
- `soc-access/speech/` for Prism and output plumbing
- `soc-access/screens/` for accessible screen models: one `GraphScreen` per game surface, building its nodes every frame from adapters
- `soc-access/ui/graph/` for the immediate-mode graph engine (copied from Endless Space 2 Access; re-sync against it rather than editing it for a screen), and `soc-access/ui/` for what screens build with: the node factories (`GraphNodes`), the navigator, and the shared node groups (troop rows, artifact slots, menu forms)
- `soc-access/dev/` for the mod-side dev server routes and probes (`ModRoutes`, `GraphDump`, `DevProbe`, `DevFixtures`); development only, never spoken
- `soc-access/loader/` for the loader plugin and the loader-side dev server. Changing anything here needs a game restart; prefer a mod-side probe over a new loader member
- `soc-access/tests/` for tests
- `soc-access/soc-access.csproj` is the live mod project and currently targets `.NET Framework 4.7.2`

Adapter label rule: ask whether a string is authored by the game or composed for the accessibility tree. Game-authored/native text can come from adapters. Accessibility wording and composition belongs in screens and the shared node groups under `ui/`.

## Localization

All user-facing text that the mod authors must be localizable. Do not add hard-coded English for spoken text, node labels, status text, review-buffer labels, scanner messages, or accessibility-only composition.

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

Use C# conventions: 4-space indentation, PascalCase for types and public members, camelCase for locals and private fields unless the surrounding code already uses underscore-prefixed fields. Types and members are public by default, as in Endless Space 2 Access, so the dev server's REPL can name any of them; use `private` for what a type keeps to itself and never `internal`. Avoid repeating words in file names. For example, use `adapters/ContinueMenuButton.cs` instead of `adapters/ContinueMenuButtonAdapter.cs` to avoid repeating adapter.

Never collapse whitespace across newlines in text the game wrote: clean native text with `SpokenLines.Clean` (one string) or `SpokenLines.Of` (lines), which split on newlines and `<br>` first and strip tags per line. A normaliser that flattens a whole string to one line turns a multi-line tooltip into one breath.

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

## Screen Resolution

A screen finds its own menu; no hook tells it. `IsActive()` is "the source found the menu and
`Live.IsPresent()`", both read from the game every frame. The source (`ScreenSource<T>`) resolves
the menu one of six ways and memoises the answer, hit or miss, keyed on the set of loaded scene
handles, so a scene change, a hot reload and a new game are the same event and none needs a
signal:

- the Zenject project container (`ProjectContext.Instance.Container.TryResolve<T>()`) for the
  system menus: pause, options, save/load, popups, codex, tutorial, platform user;
- the scene container (`SceneContext` on a scene root object, then `TryResolve<T>()`) for the
  adventure and battle menus; a binding marked `WhenInjectedInto` is invisible from outside and
  resolves through its owner's field instead (the kingdom HUD's four menus, the battle menu's
  settings); never resolve a lazy binding, it constructs the object;
- a `GameObjectContext`'s own sub-container (`SceneSubContainers`) for what a HUD installer binds
  into a container of its own, which neither the scene nor the project container can see: the
  kingdom HUD, the commander HUD's settings, the chat window's and button's behaviours. One walk
  of the loaded scenes' root objects answers every one of them, about 7 ms per scene load;
- a component on a scene ROOT object (`GetComponent<T>` per root) for a scene's installers, which
  share the `SceneContext`'s own object: the adventure view installer the map reads;
- a field off a resolved owner for a menu the owner holds (commander sheet, spellbook, the
  pre- and post-battle menus, a lobby row's dropdown);
- a walk of the scene's root objects (`GetComponentInChildren<T>(true)`) for the unbound
  menu-scene objects (main menu, campaign menu, tale select, lobby, game list, player stats),
  gated to the scenes that can hold them; about 3.5 ms on the adventure scene, so never there.

One family does not use `ScreenSource<T>` at all, and the reason is a lifetime the memo cannot
see: mod.io's community-maps browser is a prefab instantiated into the scene already loaded, so
it opens and closes without the scene set changing, and a source over it would cache "closed" for
the session. Its six screens read their panel from mod.io's own singletons every frame
(`CommunityMapsSources`), which is a static field access and needs no memo. `Browser.IsOpen` is
asked first and `SingletonIsInstantiated()` second, because a `SelfInstancingMonoSingleton`
CREATES its object when asked for `Instance`.

Nothing tells a screen when its menu is ready any more: the readiness layer
(`ScreenDetector`) is gone, and so is the hot-reload scan it ran. A reload starts every source
with no memo, so the first tick resolves as first entry does.

Readiness is read from the game: `IsPresent` gates on the end state the menu's own coroutine
leaves behind (a title set, a canvas alpha at 1, buttons activated, entries instantiated), never
on the object merely existing. Readiness hooks are forbidden; the four menus once thought to
need one (campaign menu, tale select, custom campaign select, player stats) all leave such a
trace. A new menu that seems not to gets its coroutine read again in the decompiled source, not
a patch.

A Harmony patch may deliver an event (a notification, a chat line, a battle response, a story
trigger) or alter game behaviour (force a tooltip, route a close). It is never the source of
truth for anything a `Build`, `IsActive`, `ScreenName` or tooltip reads, and losing one call may
cost one announcement and nothing else. Patch classes hold no static state without a `Reset`
that `SocAccessMod.Stop` calls. Every patch is on the inventory allowlist as `event` or
`interception`, and a patch may call or assign only a screen or adapter member marked
`[HookWritable]`, the announcement-side surface (a narrator queue, a review buffer, a captured
text no game state answers); the lints under `soc-access/tests/Lint/` enforce this and the rest
of this section, and an exception to any of them is reported to the owner before it merges.

Per-menu state lives on the adapter, never on the screen. A screen object lives for the whole
mod load; an adapter lives exactly as long as the menu instance it wraps (`SyncLive` builds one
per instance and the slot disposes the one it replaces), so a cache, a stage or a probe that is
"about this menu" is an adapter field and needs no reset. A screen keeps a mutable field only
for mod-owned state that outlives the menu (a cursor intent, a said-name baseline), with a
comment saying so; there is no reset hook to remember. What an adapter attaches to the game (a
handler, a subscription) is released in its `Dispose`. Every cache on a build path is keyed on
something read from the game each frame (frame count, object identity, a count, a generation
the game owns). A cache dropped only when a hook says so
is the bug this section exists to prevent (2026-09-09 audit: the post-battle snapshot, the
teleport mode, the map installer).

## Testing Guidelines

Tests are MSTest under `soc-access/tests/`; name files after the subject under test, for example `AnalyticsConsentScreenTests.cs`. Prioritize offline tests for text generation, focus logic, and adapter behavior. Runtime verification should be done in-game through the dev server after each hook change (`docs/dev-loop.md`): `POST /input` for keys, `GET /speech` for what was said, `GET /gui/graph` for the whole accessible tree, `GET /screens` for which screen the mod thinks you are on.

## Security & Configuration Notes

Current environment assumptions:

- `BepInEx.cfg` uses `HideManagerGameObject = true`
- The dev server is off for players: `[Dev] devServer = false` in `BepInEx/config/songs.of.conquest.access.cfg`. `run-game.ps1` writes it true for development runs; `SOCACCESS_NO_DEV=1` forces it off, `SOCACCESS_DEV_PORT` overrides the port, `SOCACCESS_NO_SPEECH=1` mutes the screen reader while `/speech` still captures
- `mcs.dll` (the REPL compiler) sits next to the loader in both the development deployment and the release; it is only loaded on the first `/eval`, so with the server off it never runs

## Performance

A graph screen's `Build` runs every frame. Nothing inside it, or inside a predicate, context
name or eagerly passed argument it evaluates, may scan the scene (`Resources.FindObjectsOfTypeAll`,
`FindObjectOfType`), walk a subtree (`GetComponentsInChildren`), invoke a game refresh through
reflection, or iterate a whole game collection. That work belongs in the adapter's constructor,
in a `FrameSweep` keyed on the frame, in a snapshot keyed on game state (an entry count, an
object identity, a game-owned generation), or inside a tooltip's lines function. Row text is
built when the row is read, never for every row per frame.

An adapter resolves each game object, `FieldInfo`, `PropertyInfo` and `MethodInfo` once per
instance. Cache misses too, behind a probed flag, so an absent panel costs one lookup and not
one per frame.

A game-side refresh (a tooltip recomposed on hover) runs when the text is read, never when the
build asks whether a tooltip exists; `PostBattleResultScreen`'s portrait tooltip is the pattern.

Before handing over a graph screen, measure one build with the eval recipe in `docs/dev-loop.md`
("Measuring a screen's build") and put the number in the commit message. Over one millisecond
is a finding to fix, not a note.

## Logs

Look at `GamePaths.props` for the path to the local game install. Logs are in the `BepInEx/LogOutput.log` in the game installation; while the game runs with the dev server, `GET http://127.0.0.1:8772/log?since=N&grep=TEXT` answers the same lines without reading the file.

If you are unable to implement a feature correctly even after inspecting the decompiled source code, you should offer to add runtime logging to help with debugging, instead of continuing to guess at what might be wrong. Then once the task is complete, offer to remove the now unneeded logs.
