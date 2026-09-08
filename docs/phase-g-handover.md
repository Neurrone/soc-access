# Phase G handover

Phase G deleted the widget engine and swept the text cleaning; it changed three things a
player can hear. Everything else should read exactly as it did after phase F.

## What to test

- **The map and the battlefield, the tile under the cursor.** A tile the game offers a click
  on now ends its buffer with a usage hint in the mod's shape: "Enter selects", "Backslash
  visits", "Backslash trades", "Backslash attacks", "Backslash picks up", "Backslash
  teleports"; in combat "Backslash moves" and "Backslash attacks". The old "Available
  actions: ..." line is gone. The hint is buffer-only, like the tile's tooltip: cursor moves
  speak the tile alone, by your decision. While the teleport menu is up the tile says no
  hint. Watch for a tile that should offer something and says nothing: the log then carries
  "unrecognized tooltip instruction row" once for that wording.
- **Multi-line native text anywhere.** Every adapter now cleans text per line instead of
  flattening it, so a tooltip, notification, story line or details block the game wrote with
  newlines is heard as its lines. Walk a few long tooltips (a troop's, a building's), a
  notification, and a story text. Watch for a label that gained a pause where a line break
  is not wanted, and for a comparison that stopped matching (a line the mod used to remove
  from a tooltip that now reads twice).
- **Typing in a game field on any screen.** The input stand-down now applies wherever the
  game's own field has the keyboard, not only on graph screens; that limit only protected the
  widget screens that no longer exist. The chat, the save name, the lobby's game name and
  the search filters are the fields to try.

## Decisions taken

- Tile usage hints stay buffer-only; the per-kind verb strings are the same shape as the
  artifact and troop hints.
- `Actions.Cancel` survives as a string: it still labels drawn Cancel buttons.
- `AGENTS.md` now speaks of nodes, stops and regions; the adapter rule itself is unchanged.
  The graph reference is `docs/ui-graph.md`.

## Needs the owner

- Combat's "Backslash moves" and "Backslash attacks" were not walked in a battle.
- The player manual (`docs_src/`) is stale about the widget era and needs its own pass.
