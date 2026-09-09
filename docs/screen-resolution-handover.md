# Screen resolution handover (2026-09-09)

What landed: every screen finds its own menu (`screen-resolution-plan.md`, commits 7b928b9..HEAD).
The detector, `RecoverRuntimeState`, every readiness hook and 55 patch files are gone; 15 patch
files remain, all event streams or interceptions, on the lint inventory. Every moved screen was
dumped before and after and across a hot reload with it open, identical unless the commit body
says why. The lints are armed; a failing one lists its sites and ends with the exception
sentence once.

## Screens to walk (unreachable from the test save)

- Online lobby with a second player: the players table after a team, faction, wielder or AI pick
  returns the cursor to the column it left (653c5a2 explains the one remaining way it cannot);
  the invite providers panel; the lobby chat row and the chat page.
- Platform user menu in the lobby ("Show Player Actions"): it never showed before 3e.
- Community maps behind a mod.io login: collection, details, subscribe; search results heading
  and labels on first entry.
- A campaign story series: between two pages of one series the map may be active for a frame
  and announce itself (the trigger state the map reads is per trigger, not per series).
- A random event dialog; a custom message; a world choice; the hostile join offer page.
- End of an adventure: the result page, the stats page (graph rows follow the dropdown), and the
  map not speaking underneath.
- A battle that ends in defeat or retreat, a manual battle to the end, spell or ability targeting
  across a reload, multiplayer battle.
- Combat: the hex tooltip under a still cursor after a troop moves or dies; the chat button in a
  first single-player battle; the troop cycles at the start of a second battle.
- Post-battle result and the pause menu's routes after a reload (both fixed, both quick).
- Teleport mode after a reload keeps its stop and buttons; world confirm cost lines read
  "-30 Gold" style before and after a reload; campaign map select after a reload.
- Custom campaign select lands on "Find More" until the mod.io fetch arrives (no fetch-done
  state exists on `IModManager`); say whether that is acceptable.

## Decisions taken

- Readiness hooks are forbidden; every readiness gate is a game-visible end state (AGENTS.md).
- Per-menu state lives on the adapter; `OnLiveChanged` is deleted; the slot disposes the adapter
  it replaces.
- The pause menu's two-second handover is removed: the gap it spanned was the mod's own
  next-frame notify (measured zero frames with neither menu present).
- Community maps read mod.io's singletons per frame instead of a scene-keyed source, because
  the browser opens and closes without a scene change.
- `LiveScreen` keeps its name and one type parameter; a typed two-parameter base was rejected
  because fifteen screens have no single source.

## Needs the owner

- The walk above.
- `performance.md` lists what is still over budget (trading 1.77 ms, lobby map select 2.64,
  search results 1.17); the "performance audit" session has it.
- Dev loop: `DevFixtures.SpawnAt` no longer lands an entity; the attack-command recipe
  (accepted at any distance) replaces it and should go into `docs/dev-loop.md`; `IMenuSystem`
  resolves from a scene sub-container for a pause-menu recipe without window focus; a failed
  `/eval` leaving an unfinished dynamic type stalled the next save load at 99%.
- Stale comments naming the deleted detector: `ITroopManagementHostAdapter.IdPrefix`,
  `TroopManagementScreenBase`, `StoryTextScreen`.
- The lint allowlists (commit "The lints are armed"): every entry is a site you are agreeing to;
  patches 42, scene-scans 52, screen-state 33, patch-statics 2.
- `PlatformUserMenuPatches` is a debug log probe with no reader left for its timestamp; delete the
  class or keep it, your call.
- A spawned Raider's Market sat at 48,19 in the running game from a measurement fixture; unsaved.
