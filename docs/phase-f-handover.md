# Phase F handover: the screen manager swap

The manager now polls every registered screen's `IsActive()` each frame and diffs the result;
nothing pushes or pops. `GET /screens` lists every screen with its layer and whether it is
active, on the stack and focused; `GET /gui/graph?screen=KEY` dumps any screen by key.

## Verified with the dev server (dumps diffed against the phase E captures, same reading)

Map, pause menu, options (all seven tabs, in-game and at the main menu), load menu, wielder
sheet, spellbook, building overview, troop overview, players menu, codex, tutorial slideshow,
quit confirmation and quit-to-desktop popup, custom message, world choice, story text and
letterbox text, settlement mini menu, defence menu with its draft page and Back (the cursor
returns to the row that opened it), hire wielder, artifact market, troop placement, combat with
its tutorial above it, the combat game menu and its load menu, the post-battle result (its title
turns from "Victory" to "Victory!" under the cursor and is said again, as before), the loading
screen, main menu, campaign menu, the lobby's map type, map select, players page, platform user
menu and game settings with a drop list opened as a child over it, mod options over the map and
over the main menu with the audio glossary as a child of a child. A hot reload on the map, in
combat and on the main menu recovers the screen. `ScreenManager.Tick` on the map: 1.0 ms, of
which the poll of 68 predicates is 0.02 ms and the map's own build 1.0 ms (pre-existing,
`performance.md` B).

Where a capture differed, the difference was content (a different save state, a read tutorial)
or a phase E commit later than the capture (the story heading folded into its button, the mini
menu's names moved to the screen name, the slider value box made the slider's Enter); the three
overview screens were also compared against the pre-F build in the same state and read the same.

## Screens to walk with real keys

- The settlement landing page and its draft, upgrade, build, research and marketplace pages
  (the move validator refuses the town tile and the mod's Enter opens the mini menu there, so
  walking in needs the mouse's double click); the rally point and dwelling; message dialogs
  from the map's own events (a custom message raised from the REPL with raw strings read its
  two buttons as "button"); the lobby's icon dropdowns and player settings; community maps;
  chat; the post-adventure pages.
- Escape everywhere: the key route refused to send while the game window could not take the
  foreground, so Escape was exercised only on the pause menu, options and the load menu; the
  mod-owned surfaces' Escape (drop list, mod dialogs) went through `/input`.

## What to watch for

- A page the game has closed that the mod still reads, or one it shows that the mod does not:
  `GET /screens` says which slot is stale.
- The handover gap. The pause menu used to hand the map back for the frames between its own
  close and options, the load menu or the codex opening ("Adventure map", the tile, then the
  menu; the pre-F build did the same). The pause screen now spans that gap (`BeginHandover`,
  ended by the target's Ready handler or after two seconds); walk those three and listen for
  the map. The same gap still exists on the way out of a battle or a game, as before.
- A page that turns in place (the community maps modal, the post-battle title) says its new
  name itself now; nothing else should re-announce.
- Once, closing the defence menu left the map's cursor on the last resource instead of the Game
  Menu button it had stood on; every later round trip kept the cursor. Not explained.
- The build menu refused to open by build-site id from the REPL (a game-side null), so it is
  untested here.

## Decisions taken beyond the plan's list

The slot lives on the screen (`LiveScreen<TAdapter>`), not on the adapter; `ModDialogScreen` is
made per `Open` and pushed as a child, not registered; popups cover the map by layer rather than
deactivating it; `AnswersOnly` dropped (nothing to gate); the dev server's widget routes went in
F; the tooltip-actions action key and the adapters' `TooltipAction`s stay for G.
