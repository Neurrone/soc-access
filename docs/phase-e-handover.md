# Phase E handover

One page. Detail per screen is in the commit messages (`git log ffe71f7..`) and each screen's
doc comment; the rulings and measurements are in `ui-graph-plan.md` (Phase E).

## State (2026-09-08)

The three modes are graph screens, each verified by a dump diff against its before-capture and
by injected keys: the adventure map, troop placement (pre-battle), and combat. Every screen the
mod supports is now a graph screen except `TooltipActionsMenuScreen` (deleted in G). The
build cost of every ported screen was measured in-game and is under half a millisecond; the
placement page's 89 ms per frame and the one-time cause (a whole-scene scan per property read)
were found and fixed in the same session, and `AGENTS.md` now carries the performance rules.

Not verified, for want of a fixture: on the map, teleport aiming, the town list, the chat and
bug report buttons; on placement, the hot-seat defender turn and Ready button, an attacker-side
threat level, the scouting information lines, the multiplayer wait state; in combat, the
quickbar, a defender wielder, the battle log, spell and ability aiming, and the multiplayer
player names and turn timer. Physical keys were exercised on placement only; the map and
combat walks were injected because the game never held the foreground.

## Screens to test, and what to watch for

- **Adventure map**: the map is one stop named "Map"; arrows and Shift+arrows move and skip,
  the tile reads once per move, queued after a skip count. All the old letter keys work on the
  map node only. Tab walks Wielder, Troops, Resources, Kingdom, Wielders, Towns, Objectives,
  Notifications, Turn; T, R, B, N jump; Escape from any of them returns to the map, and on the
  map is the game's. Type-ahead is off on the whole screen. Watch for a doubled tile line, and
  for a stop that appears while its panel is not drawn.
- **Troop placement**: Attacker, Defender, the board named by the instruction, Buttons, the
  drag hint. Space picks a troop up, Enter drops it through the game's own drag, Escape cancels;
  a refused drop says "Invalid destination". Type-ahead works everywhere but the board. Escape
  otherwise is the game's. Watch for the drag's pick-up sound playing twice on a drop (known,
  the game's move replays its grab).
- **Combat**: the battlefield first, named by the combat grid word or the aiming instruction;
  I inspects, Escape leaves inspect and only then reaches the game; comma and period cycle
  troops, W relevant tiles, S threat, T the turn order, Backslash acts. Then Quickbar, Attacker,
  Defender, Current troop (its ability under it), Turn order, Game menu with Chat, Battle log,
  End turn. Watch the first aim of a spell: the instruction should be spoken once, not twice.

## Decisions taken

- A mode is one node with a fixed id; its cursor class speaks each move itself, queued, and the
  navigator never announces one. The mode's keys go through `GraphScreen.ModeClaims`, asked
  before the navigator's own set, and the graph's Home, End and Backspace are translated onto
  the scanner on a mode node.
- Type-ahead is off screen-wide on the map (its letter hotkeys) and off on the board node only
  elsewhere.
- Escape stays the game's on every mode node unless the mod owns a state there (a carry,
  inspect, aiming); from a HUD stop it returns to the mode node with the game's close sound.
- Cancel spell and Cancel ability sit where the game draws them (the Spells spot, the acting
  troop's row), not in a stop of their own; the battle log sits just before End turn.

## Needs the owner

- A walk with real keys on the map and in combat, especially Escape on a HUD stop, Escape mid
  inspect, and C and V reaching the game from the map.
- The fixtures listed above, when the game offers them.
- `performance.md` in the repo root, untracked: the ranked list of per-frame costs on every
  other screen for a fresh session to fix; delete it when done.
- `translations/zh-TW.po` carries simplified characters in several older entries.
