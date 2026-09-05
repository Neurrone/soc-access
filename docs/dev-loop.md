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
line; `run-game.ps1` writes both (`-NoDev`, `-NoSpeech`). The environment variables
`SOCACCESS_NO_DEV=1`, `SOCACCESS_DEV_PORT` and `SOCACCESS_NO_SPEECH=1` do the same, but only
for a game started by hand: `SongsOfConquest.exe` hands itself to Steam, which relaunches it
without the launcher's environment, which is why the config file is the switch. The server
binds `http://127.0.0.1:8772` only.

The REPL needs `mcs.dll` next to the loader DLL; the build deploys it and the release never
ships it (the evaluator is built on the first `/eval`, so a missing `mcs.dll` is never
touched when the server is off).

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
  content) print one placeholder line each. Grammar in `dev-server-plan.md` §5.
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

Every route declares its query parameters; an undeclared one answers 400 naming it. A
bodyless POST answers 411 before any handler runs: always send a body, even an empty one.
From Bash, `curl -s -X POST --data-raw '' http://127.0.0.1:8772/reload` works; from the
PowerShell tool use `Invoke-RestMethod -Method Post -Body ''` because `curl.exe --data-raw ''`
drops the empty argument there.

Main-thread routes answer 503 when a frame takes longer than 5 s (boot, loading, the first
Harmony pass): retry, and confirm state-changing requests through their status route rather
than assuming they failed.

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
- Reaching a mod internal from `/eval`: mod types are `internal`, so go through the public
  `DevProbe` and `ModEntry`, or add a probe there (mod side, hot-reloads) rather than
  widening a type.

## 4. The loops

**Session.** `.\run-game.ps1 -NoSpeech -NoWait -LoadSave "<a save DevProbe.Saves() lists>"`
builds, launches `SongsOfConquest.exe`, turns the server on, and drives the load;
`.\wait-game.ps1 ingame` blocks until the map is up (exit 1 prints the state it saw, exit 2
means the process died). Both are run from the PowerShell tool. One game at a time: the
script refuses to launch over a running one and never kills anything; `POST /quit` first
(the process is gone about 6 s later). The game boots twice: the process the script starts
loads BepInEx, the loader and the mod, answers on 8772, then asks Steam to relaunch it and
exits; the script tracks the second process and only talks to a server that process owns.
Anything else polling the port during boot can be answered by the first process.

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
- 2026-09-05: a reload on the map after the wielder sheet, the options screen and the load
  menu had been opened and closed resynced with `OptionsScreen` and `PauseMenuScreen`
  stacked over the map although nothing was open, and `/loadsave` then drove a stale menu.
  Reload from a plain map (or reload, then `POST /loadsave` twice) until the detector's
  resync is fixed; `State()` reports `dialog` when this has happened.
- 2026-09-05: the "The Enemy Revealed" campaign saves (`QuickSave_*`, `AutoSave_1..3`) are
  refused in-session with "Content not available", and loading `QuickSave_4` from the main
  menu crashed the game natively while building the scene (`The file 'none' is corrupted`,
  no mod frame). Use `test` (Vassals and Villains, round 20) as the fixture.
