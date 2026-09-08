# Phase F handover: the screen manager swap

The manager now polls every registered screen's `IsActive()` each frame and diffs the result;
nothing pushes or pops. `GET /screens` lists every screen with its layer and whether it is
active, on the stack and focused; `GET /gui/graph?screen=KEY` dumps any screen by key.

## Verified with the dev server (dumps diffed against the phase E captures, same reading)

Map, pause menu, options (all seven tabs), load menu, wielder sheet, spellbook, building
overview, troop overview, players menu, codex, tutorial slideshow, quit confirmation over the
pause menu, defence menu, defence draft page and Back (the cursor returns to the row that opened
it), hire wielder, troop placement, combat with its tutorial above it, the combat game menu, the
mod options window over the map. A hot reload on the map and in combat recovers the screen.
`ScreenManager.Tick` on the map: 1.0 ms, of which the poll of 68 predicates is 0.02 ms and the
map's own build 1.0 ms (pre-existing, `performance.md` B).

## Screens to walk with real keys

- Settlement landing page, its draft and upgrade pages and Back; build, research, marketplace,
  artifact market, rally point, dwelling; the story text, letterbox and dialogue (no fixture
  reaches them here); message dialogs from every source; the post-battle result and the
  post-adventure pages; chat.
- The main-menu family: campaign, tale select, community maps, the lobby pages, every drop list,
  mod options and its sub-dialogs (child screens; only the window over the map was opened).
- Escape everywhere: the key route refused to send while the game window could not take the
  foreground, so Escape was exercised only on the pause menu, options and the load menu.

## What to watch for

- A page the game has closed that the mod still reads, or one it shows that the mod does not:
  `GET /screens` says which slot is stale.
- The handover gap: "Adventure map" and the tile are spoken between the pause menu closing and
  options opening. Measured on the pre-F build too, so not a regression; options back to the
  pause menu no longer says it. Closing this gap would need the map to stand down under the
  game's menus, which the plan's decision 7 chose not to do.
- A page that turns in place (the community maps modal, the post-battle title) says its new
  name itself now; nothing else should re-announce.
- Once, closing the defence menu left the map's cursor on the last resource instead of the Game
  Menu button it had stood on; a second round trip kept the cursor. Not explained.
- The in-game Options page draws no edit box beside its sliders; the main-menu page does. Both
  builds, pre-existing.

## Decisions taken beyond the plan's list

The slot lives on the screen (`LiveScreen<TAdapter>`), not on the adapter; `ModDialogScreen` is
made per `Open` and pushed as a child, not registered; popups cover the map by layer rather than
deactivating it; `AnswersOnly` dropped (nothing to gate); the dev server's widget routes went in
F; the tooltip-actions action key and the adapters' `TooltipAction`s stay for G.
