# Dev loop: build, reload, verify

This file is only the loop: the dev server, the REPL, and the screen-agnostic verification
patterns. Screen-specific knowledge lives with the screen (`soc-access/screens/README.md`).
The design and its reasons are in the reference mod's `docs/generic/dev-server.md` and
`docs/generic/hot-reload.md` (`../endless-space-2-access`); this repo's copies of the source
are `soc-access/loader/` (loader side) and `soc-access/dev/` (mod side).

## 1. Gates

Off by default for players: `devServer = true` under `[Dev]` in
`BepInEx\config\songs.of.conquest.access.cfg` turns the server on, and `muteSpeech = true`
in the same section keeps the screen reader silent while `/speech` still captures every
line; `run-game.ps1` writes both (`-NoDev`, `-NoSpeech`). `muteSpeech` is one-shot: the
loader writes it back to `false` as soon as it has read it, so it covers the launch the
script made and never the owner's next launch from Steam (a hot reload stays muted, the
loader remembers the answer). The environment variables
`SOCACCESS_NO_DEV=1`, `SOCACCESS_DEV_PORT` and `SOCACCESS_NO_SPEECH=1` do the same, but only
for a game started by hand: `SongsOfConquest.exe` hands itself to Steam, which relaunches it
without the launcher's environment, which is why the config file is the switch. The server
binds `http://127.0.0.1:8772` only.

`maxFrameRate = N` under `[Performance]` in the same file caps the game at N frames per second
(0, the default, leaves the game alone), re-asserted every frame because the game's own
`FrameRateManager` rewrites the target rate and vertical sync whenever video settings are
applied. `resolution = WIDTHxHEIGHT` beside it forces the render size the same way (empty
leaves the game alone). Both exist for a development machine without GPU acceleration;
nothing writes them, so set them once by hand. They are read by the loader at start, so a
change needs a game restart.

The REPL needs `mcs.dll` next to the loader DLL; the build deploys it and the release ships
it too, as Endless Space 2's does (the evaluator is built on the first `/eval`, so with the
server off the file is never loaded).

## 2. Route reference

Loader routes keep answering while the mod is broken or unloaded:

- `GET /loader/status`: `modLoaded`, `modAssemblyName` (`SongsOfConquest.Access-rN`, a new
  N per load attempt), `reloadCount`, `failedReloadCount`, `lastReloadError`, `staleBuild`
  (the DLL on disk is newer than the one running).
- `POST /reload`: swap in the DLL on disk on the next frame. Answers `{"ok":true}` before the
  swap runs; the swap itself takes 5 to 10 s here because Harmony patching is slow, and
  `/loader/status` reports `modLoaded:false` while the new mod is still starting. Poll it
  until `modLoaded:true` and `modAssemblyName` incremented; never rebuild in the meantime.
  A build that does not load is refused with the old mod untouched (`failedReloadCount`
  goes up, `lastReloadError` says why) and the attempt still consumes an identity number.
- `POST /eval?settle=MS&speech=0`: C# REPL, body = source. Answers `{ok, result, error,
  speech:[...]}`; the speech field holds what the code made the mod say, gathered by waiting
  for a quiet settle window (700 ms default). `speech=0` skips the wait entirely.
- `POST /wait?timeout=MS`: body = a C# boolean expression, compiled once and evaluated every
  frame until true or the timeout (capped at 60 s). Catches single-frame transients that
  polling from outside cannot see.
- `GET /gui/game?path=&depth=`: the raw Unity hierarchy as JSON.
- `GET /screenshot`: the rendered frame as PNG. Never read a full frame into an agent's
  context; crop it first (§5).
- `GET /log?since=N&grep=TEXT`: the BepInEx log ring. Without `since` only the last 100
  entries come back (`capped:true`); `grep` searches the whole ring first.
- `POST /quit`: exit the game (answers first, quits next frame).

Mod routes answer 404 while the mod is down:

- `GET /status`: `version`, `modAssemblyName` (as the mod itself sees it), `speechAvailable`,
  `speechMuted`, `lastSpoken`, `screenStack` (bottom first), `topScreen`, `focusedWidgetId`,
  `focusedWidgetType`, `gameObjectCount`.
- `GET /speech?since=N&wait=MS`: everything spoken since sequence N, `{entries:[{seq,text}],
  next}`; `wait` holds the answer until the next line (capped at 30 s). The ring resets on
  reload. The ready line spoken at start is captured too.
- `POST /input`: body = one action key from `AccessibilityActions` (`next_menu_item`,
  `activate`, `cancel`, `map_move_north`, `hex_grid_east`, ...). The action runs inside the
  input router's own frame tick through the same claim check and dispatch a key press takes.
  Answers `{ok, action, outcome, speech:[...]}` with `outcome` one of `consumed`,
  `consumed (global)`, `claimed, not handled`, `unclaimed`, `no screen`; an unknown key is a
  400 listing every registered action. No physical key is down during an injection, so
  anything that branches on held-key state (the release debounce in the router, the game's
  own key scans) is not exercised.
- `GET /gui/widgets?buffers=1&flat=1`: the whole accessible tree of the focused screen, one
  line per widget reading what arriving on it would speak; `buffers=1` adds each widget's
  review-buffer lines, `flat=1` answers one `label | status | buffer | actions` line per
  leaf for diffing. Side-effect free: two calls answer identically. Multi-position widgets
  (map grid, hex grids, inventory grid, army exchange grid, announcement order menu, codex
  content) print one placeholder line each. Grammar in §2a below.
- `GET /gui/graph?buffers=1&flat=1&edges=1`: the same for a graph screen (`screens/GraphScreen.cs`):
  one line per node in navigation order, indented by its depth, with `-- stop:` markers between
  Tab stops and `(collapsed)` on a shut group; `edges=1` adds where each arrow goes from every
  node (a wired edge, `adjust value`, `expand`, `descend to`, `collapse`, `ascend to`). A widget
  screen answers with one line saying to use `/gui/widgets`, and vice versa.
- `GET /gui/tree?buffers=1&flat=1&edges=1`: whichever of the two dumps fits the focused screen.
- `POST /type`: body = characters typed into the focused graph screen's type-ahead search,
  through the same per-frame tick a keypress takes. Answers `{ok, searchText, searchActive,
  results, speech:[...]}`; 409 when the focused screen is a widget screen.
- `GET /gui/unity?path=&depth=&visibleOnly=&fields=`: the game's own UI read as accessible
  meaning - the coverage baseline the mod's tree is diffed against. Per node `name`, `kind`
  (button/toggle/slider/dropdown/input/text/image/canvas/panel), `text` (markup stripped),
  `tooltip` (read without hovering), `value`, `interactable` (computed over the whole ancestor
  chain), `visible` when false, and `rect` as `[x,y,w,h]` in screen pixels with a top-left
  origin, which is what `crop-shot.ps1` crops by. Roots are every root canvas in the loaded
  scenes, topmost sorting order first; the top-level `windows` array names them all, visible or
  not, so a caller can pick a `path=`. `path=` matches a root by name, then any named transform
  under the roots (exact, then case-insensitive substring); a name nothing answers to is a 404
  carrying `windows[]`, and a match emptied by `depth=` or `visibleOnly=` says which. Decoration
  is pruned, but a node at the `depth=` frontier is kept and carries `"more": true`. `fields=`
  answers plain text instead: one line per node, two spaces of indent per level, the requested
  fields separated by ` | `. Side-effect free: it never hovers, selects or focuses anything.
- `POST /loadsave`: body = save name (empty = newest), through the game's own load menu.
  Answers `{"result":"loading '<name>'"}` once the native load button was clicked, 404 for a
  name that is not a save, 422 with the menu's details text when the game itself refuses
  the save ("Content not available", version mismatch), 409 while a load this route started
  is still running, and a retryable 503 `[not ready]` from any state that cannot start a
  load (loading screen, combat, lobby, dialog, menu not yet interactable). From a running
  session it opens the same load menu the pause menu's Load button opens, without showing
  the pause menu. Every load ends on a "press any key to
  continue" screen (`State()` says `loading`); the route keeps watching for up to five
  minutes and presses it natively, so waiting for `ingame` is enough. A game loaded some
  other way is continued with `/eval DevProbe.ContinueLoading()`.
- `POST /key?hold=MS&gap=MS&text=1`: body = a key sequence pressed as real OS key events at
  the game's window (`Return`, `Escape`, `Ctrl+I`, `Shift+Tab`, `DownArrow`; `+Name` holds
  and `-Name` releases; `text=1` types the body's characters). Unity `KeyCode` names plus
  `Ctrl`, `Shift`, `Alt`, `Enter`. The only route where a key is physically down, so the only
  way to exercise the mod's raw `InputSystem.onEvent` subscription, the release debounce, and
  the game's own handling of the same key. It raises the game window and REFUSES with 409,
  sending nothing, unless the foreground window then belongs to the game, re-checked before
  every step; 400 for a key name it does not know (the answer lists the vocabulary). It
  takes the desktop's focus while it runs, so never call it while the owner is working.

Every route declares its query parameters; an undeclared one answers 400 naming it. A
bodyless POST answers 411 before any handler runs: always send a body, even an empty one.
From Bash, `curl -s -X POST --data-raw '' http://127.0.0.1:8772/reload` works; from the
PowerShell tool use `Invoke-RestMethod -Method Post -Body ''` because `curl.exe --data-raw ''`
drops the empty argument there.

Main-thread routes answer 503 when a frame takes longer than 5 s (boot, loading, the first
Harmony pass): retry, and confirm state-changing requests through their status route rather
than assuming they failed.

## 2a. Dump grammar (shared with the UI rewrite)

Both `/gui/widgets` (today) and `/gui/graph` (the UI rewrite) emit the same grammar so the
migration diff is `sort | diff`.

Tree mode (default):

```
screen: <ScreenType> | stack: <Bottom> > ... > <Top>
<indent>[*] <WidgetType> #<id> "<line as spoken on arrival>"
<indent>    buffer: <line 1>
<indent>    buffer: <line 2>
<indent>    actions: <a>, <b>
<indent>[ ] <MultiPositionType> #<id> (multi-position) current="<GetFocusMessage()>" key=<GetAnnouncementKey()>
```

`[*]` marks the focused widget. Hidden widgets (`IsVisible` false) are omitted; a container
with no visible children still prints, marked `(empty)`.

Flat mode (`flat=1`): one line per leaf, no indentation, no focus marker, no role word,
no position text, no context prefix:

```
<label> | <status> | <buffer lines joined " / "> | <actions joined ", ">
```

Labels come from `GetLabel()`, status from `GetStatus()`, buffer lines from the same
composition the UI review buffer uses (label lines, status, tooltip lines with the duplicated
heading dropped, actions text). Multi-position widgets print one flat line
`<Type> (multi-position) | <current focus message>`. The graph dump maps its node parts onto
the same four columns.

## 3. Probes (`/eval` one-liners)

`SongsOfConquestAccess.Dev.DevProbe` is compile-checked and answers JSON, never throws:

- `State()`: `{"state": menu|loading|ingame|combat|dialog|lobby|none, "top": "<Screen>"}`,
  read off the mod's own screen stack. `wait-game.ps1` polls it. `ingame` means the
  adventure map is the top screen; any screen stacked over the map (a popup, the wielder
  sheet, the load menu) answers `dialog`, and a battle answers `combat` whatever is on top.
- `Screen()`, `Stack()`: the top screen's type name; the whole stack, bottom first.
- `Saves()`: the saves the load menu would list, newest first, with `corrupt` flagged.
- `ContinueLoading()`: press "any key" on the loading-complete screen through the game's own
  `FinalizeLoadingScreen`; `continued:false` when that screen is not up.
- `RuntimeScreens()`: the screens the detector's runtime factories read as present right
  now, which is exactly what a reload's resync would push. Read it when a resync looks wrong.

REPL facts observed on this Mono (Unity 2022.3, `mcs.dll` built for net35):

- Multi-statement bodies work; top-level `var` declarations persist until the next reload.
  No `using` directives: fully qualify everything (`UnityEngine.Application.unityVersion`).
- Bare `Time` binds to `InteractiveBase.Time(Action)`; write `UnityEngine.Time`.
- `/reload` rebuilds the evaluator against the new assembly, so mod type names resolve to the
  build now running; every variable declared before the reload is gone.
- A `foreach` over a `List<T>` of a game type does not poison the session here, unlike the
  older Mono under ES2 (checked 2026-09-05 with `List<SongsOfConquest.Common.LoadGameDefinition>`;
  `1+1` still answered afterwards). Constructed generics over game types are safe to write.
- Private game members are reached with plain reflection
  (`typeof(T).GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance)`), which
  is how a game-owned menu is closed when the mod does not claim the key that closes it.
- Mod types are public, as in ES2, so `/eval` can name them directly
  (`SongsOfConquestAccess.SocAccessMod.Instance.ScreenManager.CurrentScreen`); a question
  asked more than once still belongs in `DevProbe` (mod side, hot-reloads), compile-checked.

## 4. The loops

**Session.** `.\run-game.ps1 -NoSpeech -NoWait -LoadSave "<a save DevProbe.Saves() lists>"`
builds, launches `SongsOfConquest.exe`, turns the server on, and drives the load;
`.\wait-game.ps1 ingame` blocks until the map is up (exit 1 prints the state it saw, exit 2
means the process died). Both are run from the PowerShell tool. One game at a time: the
script refuses to launch over a running one and never kills anything; `POST /quit` first
(the process is gone about 6 s later). A Steam install (recognised by its app manifest) is
launched through `steam://rungameid/867210`; a GOG install starts the executable. Never start
`SongsOfConquest.exe` by hand on a Steam install: it boots BepInEx, the loader and the mod,
answers on 8772, then the game's own Steam check (`RestartAppIfNecessary` in
`Bitwave.Platform.Steam`) asks Steam to relaunch it and exits, and that relaunch was seen to
fail. The Steam-launched process is a full Steam session: the DLCs the account owns read as
owned (`IAddonManager.OwnsAddons`).

**Reload.** `dotnet build soc-access\soc-access.csproj` (deploys) → `POST /reload` →
poll `GET /loader/status` until `modLoaded:true` with `staleBuild:false` and the new
`modAssemblyName` → then interpret results. The screen stack is rebuilt from the game's
runtime state on start (`ScreenDetector.ResyncFromRuntimeState`), so reloading on the map or
in combat keeps speech going. A loader change (`soc-access/loader/`) needs a restart: the
build cannot overwrite the locked loader DLL and says so.

**Verify a key.** `POST /input <key>` and read the `speech` it answers with; for a sequence,
one request per key about 0.4 s apart, then `GET /speech?since=N` with the `next` cursor
read before the sequence. Silence is evidence only for a control that would have spoken.
An action the top screen does not claim answers `unclaimed` and nothing happens: with a
physical key the game would have seen it instead (Escape closing a game menu is the common
case), so close such a menu through its native method from `/eval`, and treat "the key
reaches the game" as untestable from here.

**Prove a change altered no spoken line.** `GET /gui/widgets?buffers=1` before and after,
diffed; `flat=1` when the tree shape is what changed. Capture every reachable screen the
change could touch.

## 5. Evidence

`.\crop-shot.ps1 -Rect x,y,w,h [-Out path]` fetches `/screenshot` and keeps one region
(top-left origin pixels) with a margin. Never read a full-frame screenshot into an agent's
context; the crop is both the evidence and its region. Invoke it from the PowerShell tool;
`powershell -File` mangles the `-Rect` array.

## 6. Observed here

Filled in as the loop is used; keep entries to one line each with the date.

- 2026-09-05: `POST /reload` on the main menu takes about 6 s end to end; `/loader/status`
  read 4 s after the request still said `modLoaded:false`.
- 2026-09-05: truncating the deployed DLL and reloading answered `failedReloadCount:1` with
  the running mod untouched and `/eval` still working; the next good build recovered.
- 2026-09-05: cold launch to the main menu about 50 s; `run-game.ps1 -LoadSave test` to
  `ingame` 75 s; `POST /loadsave` to `ingame` 12 s on a warm game; `POST /quit` to process
  exit 6 s. Three reloads on the map after using the
  sonar sweep and tile cues left `gameObjectCount` unchanged (1583).
- 2026-09-05: once, a reload on the map after the wielder sheet, the options screen and the
  load menu had been opened and closed resynced with `SaveLoadGameScreen`, then
  `OptionsScreen` and `PauseMenuScreen`, stacked over the map although nothing was open.
  Two replays of the same sequence resynced cleanly. If `State()` answers `dialog` on a bare
  map after a reload, read `DevProbe.RuntimeScreens()` before touching anything and keep the
  answer; that is the evidence the fix needs.
- 2026-09-05: the "The Enemy Revealed" campaign saves (`QuickSave_*`, `AutoSave_1..3`) are
  refused in-session with "Content not available", and loading `QuickSave_4` from the main
  menu crashed the game natively while building the scene (`The file 'none' is corrupted`,
  no mod frame). Use `test` (Vassals and Villains, round 20) as the fixture.
- 2026-09-05: `POST /key` with `Tab` from the map raised the game window, the mod's raw input
  subscription dispatched `next_widget` exactly once (no debounce duplicate) and answered
  `speech:["Cecilia Stoutheart button."]`; the wielder button had the focus afterwards.
