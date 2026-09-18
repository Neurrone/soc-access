# Hot-seat plan — mod state follows the player in control

Written 2026-09-18. Builds on `adapter-lifetime-plan.md`; do not start before its Phase 4 has
landed, because this plan puts the player into the slot key that plan introduces.

## Goal

In a hot-seat game every piece of mod state that is ABOUT A PLAYER follows the team in control:
player B never hears, reviews or jumps to what belongs to player A, and each player finds their
own cursor, lists and beacons as they left them when control comes back. Single-player and online
multiplayer (one local team for the whole game) must behave exactly as they do now.

## What is known, and how well

From the decompiled source only - hot-seat has never been run in-game with this mod's dev server:

- `ClientTeamFacade.LocalTeamInControl` (`.../Client/Gamestate/Facade/ClientTeamFacade.cs:34-61`)
  is the first local team in `CurrentTeams`, so it changes with the turn. No scene change, no
  new `AdventureViewInstaller`, same map adapter.
- The hand-over shows a popup (`ReactiveAdventureMenuSystem.cs:392-409`); it does not pop the map.
- `Teams.GetIsLocal(teamId)` is CLIENT-wide: in hot-seat every human's teams are local.
- Pre-battle placement has its own hot-seat states (`HotSeatAttackerPlacement`,
  `HotSeatDefenderPlacement`, `HotSeatBothReady`) and raises no `OnChanged` between them.
- `KingdomEntityOverviewMenu.Show(teamId)` / `KingdomTroopOverviewMenu.Show(teamId)` take the team.
- Bookmarks are already per team on disk (`AdventureBookmarkGameIdentity.SameStorageAs:56-61`).

Not known: what the game itself shows and says at a hand-over; whether a battle between two local
humans has a "local side" at all; whether simultaneous-turn or team modes exist in hot-seat.
Phase 0 settles these in the running game before any code moves.

## What the audit already did (three mechanisms for one idea)

| Commit | Mechanism |
|---|---|
| `f0d9e2af` | `AdventureMapEventListener.SyncDiscoveryTeam()`: a team watcher that empties and retakes the discovery baseline |
| `4450c14a` | `AdventureMapRevealedRegistry` partitioned by team id at every read and write |
| `540f78d2`, `5e550cde` | `AdventureMapGrid.SyncBeaconTeam()`: a second team watcher; `AdventureBeaconArming`: a dictionary keyed on game + team |
| `a228128a`, `8cc474d7` | pre-battle: snapshot and carry follow the placement state |

Each is correct on its own and each re-reads the team in its own place. This plan replaces the
first three with two shared pieces.

## Design

### 1. The player is part of the subject

`PlayerToken` (new, `soc-access/adapters/`): the pair (adventure game, team in control), both read
from the game - the `IClientAdventureFacade` identity `AdventureMapAdapter.AdventureGame` already
answers, and `Teams.LocalTeamInControlId`. Equality is reference + int. Null outside an adventure.

Adventure-scene screens answer it from `SubjectToken` (combined with their own subject where they
have one - the settlement's entity, the overview's `_async`). `AdventureMapScreen` answers it
alone. A hand-over is then a `SubjectChanged`: the map adapter is disposed and rebuilt, which
re-attaches the event listener (baseline retaken from the new team's fog), rebuilds the grid
(bookmarks hydrated for the new team, beacons re-armed from the store below) and drops the
navigator's kept HUD cursor. `SyncDiscoveryTeam` and `SyncBeaconTeam` are deleted.

Cost: one map adapter construction per hand-over. Measured in `adapter-lifetime-plan.md` Phase 0;
a hand-over sits behind a popup, so a few milliseconds there are not felt. If the listener's
baseline capture is the expensive part, it stays a listener method and the adapter calls it.

In single-player and online multiplayer the token never moves after the first frame.

### 2. One store for what must outlive an adapter

`PlayerScope` (new, owned by `SocAccessMod`, `Reset()` as a `Stop` step): `Get<T>(PlayerToken)`
answers the one `T` for that game and team, created on first ask. Entries for a game are dropped
when the adventure game identity changes (the weak-reference `Rebind` logic
`AdventureMapScannerState` has today moves here). In memory only; a hot reload loses it, as it
loses the registry and the arming today - the cost is a re-armed beacon and a re-found list,
never a wrong screen.

Moves into it, with its ad-hoc key deleted:
- the revealed registry (its `teamId` parameters go; the registry becomes per-player by where it
  is fetched from);
- the armed beacon slots (`AdventureBeaconArming`'s dictionary goes).

New in it (state that is per player but is not today):
- **the map-notifications review buffer** (`ReviewBufferKind.AdventureMapNotifications`). Today
  it is per GAME: player B can page back through what player A was told - income, discoveries,
  attacks seen. In hot-seat that is an information leak, found while writing this plan and not
  fixed by any audit commit;
- the map cursor position, the scanner's category, subcategory, jump anchor and look-around
  radius: today they live on the grid, so player B starts where A's cursor was and inherits A's
  scanner position. With the adapter rebuilt per hand-over they would reset every turn instead;
  the store is what lets each player come back to their own.

Stays global on purpose: mod settings, keybinds, custom scanner slots and categories (they are
the person's tools, and two people at one machine share one config file - say so in the docs).

Stays per battle: the combat-events review buffer and the combat narrator (both players are in
the same fight and hear the same events).

### 3. Reload safety

`PlayerToken` is two reads from the game; nothing in it comes from a hook. A reload during player
B's turn builds B's adapter on the first frame. The store is mod-owned intent, like a cursor. The
lint from `adapter-lifetime-plan.md` covers `PlayerToken` because it is reached from
`SubjectToken`.

## Phases

**Phase 0 — facts, in the running game, before code.** A two-human hot-seat save on a small map
(make one; keep it as the fixture, named in `docs/dev-loop.md`). With the dev server:
1. Read `LocalTeamInControlId` and the facade identity before and after End Turn.
2. Record `/screens`, `/speech` and `/gui/graph` across the hand-over: which screens are on the
   stack, what the game shows, what the mod says. Decide from that whether the mod needs its own
   "player N's turn" line or the popup already says it.
3. Start a battle between the two humans: read which side `CombatAdapter` calls local, what
   "friendly" and "enemy" mean in the narration, who the pre-battle page thinks is placing.
4. Open each kingdom overview, the wielder list, the town list as each player.
5. An online multiplayer lobby against an AI, to confirm the token is constant there.
Write the answers into this file. Anything that contradicts "What is known" above re-plans the
phase it touches.

**Phase 1 — `PlayerScope`, no behaviour change.** The store, unit tests (two teams, one game; a
new game drops the old game's entries; a collected game is dropped). Move the revealed registry
and the beacon arming into it. The spoken trees and the existing registry tests do not change.

**Phase 2 — the player in the key.** `PlayerToken`; `AdventureMapScreen` and the adventure HUD
screens answer it. Delete `SyncDiscoveryTeam`, `SyncBeaconTeam` and `_beaconTeamId`. Hot-seat
script (below) passes steps 1-6.

**Phase 3 — the notifications buffer per player.** Fetched from `PlayerScope`; the main-menu
clear and the game-change clear become the store's. Script step 7.

**Phase 4 — cursor and scanner memory per player.** The grid reads its starting cursor and the
scanner its position from the store and writes them back on dispose. Owner decision needed first
(below). Script step 8.

**Phase 5 — combat between two local players**, scoped by what Phase 0 found. Likely: the
narration's friendly/enemy wording and the troop cycles follow the troop whose turn it is rather
than a fixed local side. Its own plan section once the facts are in.

**Phase 6 — docs.** `docs_src/src/` gets a hot-seat page: what follows the player, what is shared
(settings, keybinds, custom scanner slots), what a hot reload forgets.

## The hot-seat script (the acceptance test)

Two humans, A then B, dev server on, speech captured.
1. A: reveal something, arm beacon 1 on a bookmark, move the cursor away from A's wielder, leave
   the scanner on a non-default category. End turn.
2. At the hand-over: A's beacon goes silent at once; nothing of A's is spoken.
3. B: the Revealed list holds only B's finds; B's bookmarks, not A's; toggling slot 1 says "no
   bookmark" and there is no sound to stop.
4. B: nothing A discovered is announced as newly revealed; something B newly sees is.
5. B: kingdom overviews, wielder list and town list are B's.
6. B ends turn; A: beacon 1 is on again, A's Revealed list is intact.
7. A cannot page back into B's notifications, nor B into A's.
8. A's cursor and scanner category are where A left them (if Phase 4 is accepted).
9. Hot reload during B's turn: the screens, lists and beacons are B's afterwards (arming excepted).
10. A pre-battle page between A and B: the hand-over between placements gives the board, the
    wording and an empty carry to the defender (`a228128a`, `8cc474d7`, already in).

Regression, single-player: a full turn, a quick battle, a manual battle, a loaded save - the
token never moves, the registry and beacons survive the battle, the spoken trees are unchanged.

## Decisions for the owner

1. **Where does a player's cursor start on their turn** - where they left it (Phase 4), or on
   their selected wielder every time? The second is simpler and is what a sighted player gets
   from the camera; the first is friendlier with many wielders.
2. **Should the "last spoken" UI review buffer be wiped at a hand-over?** It can hold the end of
   A's turn. Wiping is safer; keeping is what a player who missed a line wants.
3. **Is a spoken "player N, <colour>, your turn" line wanted**, if Phase 0 finds the game's popup
   does not already give one? It would be a new `ModString` with 13 translations.
4. **Beacons when control returns**: on again automatically (this plan), or off until re-armed?

## Risks

- The map adapter rebuilt per hand-over is the largest adapter in the mod. If Phase 0's
  measurement says it is slow, the fallback is to keep the adapter and give the listener and the
  grid a `PlayerToken`-keyed reset - which is the three watchers again, but in one place.
- Everything under "What is known" is from reading. Phase 0 exists because the audit's one
  in-game session showed that such reading is usually right and occasionally not (the first
  review of the mini menu's deferred destroy was wrong).
- Online multiplayer must not regress: the token is constant there by construction, and the
  regression list above runs in a lobby against an AI before Phase 2 merges.
