# Phase C handover

One page. Detail per screen is in the commit messages (`git log 4d0528e..`) and each screen's
doc comment; the rulings and measurements are in `ui-graph-plan.md` (Phase C).

## State (2026-09-07)

Twenty in-game screens are graph screens, each verified by a dump diff against its
before-capture and by injected keys: pause menu, world confirm, claim menu, both tutorials,
story text (panel and letterbox), level up, purchase wielder, research, build menu, owned
entities, troop overview, map entity mini menu, players menu, gift town, send resource,
marketplace, post-battle result, post-adventure result, post-adventure statistics. Also
checked in-game on their shared classes: the codex, the save menu, the map message popup.
Spellbook and world choice moved to phase D. The co-op, battle and post-game screens were
done in a fresh 4-player random skirmish with an AI ally on my team (no save on this machine
is a non-campaign game; the campaign saves are refused as content not available).

Not verified, for want of game data: the random event, custom message and dialogue sources;
the in-game chat; the mod options dialog opened from the pause menu.

## Screens to test, and what to watch for

- **Pause menu**: arrival lands on Continue Game (the game's own default), read last because
  it is drawn last; Escape is the game's.
- **World confirm** (paying at a site): Cancel reads before Confirm, as drawn; Escape presses
  Cancel. **Claim menu** (after a siege): choices are buttons reading title, duration,
  description; Escape reaches the game, which refuses it on a freshly captured settlement.
- **Tutorials**: a multi-page tutorial is a list of pages; Up/Down turn the page, Home and End
  jump; "Got it!" stays unavailable until the last page was shown; Escape closes from any page
  (the game's own behaviour, new for the mod). The simple popup reads heading, body, OK, toggle.
- **Story text** (cutscenes, letterbox): heading then body, Enter or Escape advances; the
  paragraphs are separate buffer lines.
- **Level up**: arrival lands on the first skill card; Up/Down walk the three cards (drawn side
  by side); the focused card is drawn highlighted; the close cross is the last stop.
- **Purchase wielder**: arriving on a wielder selects it and the details stop follows; the
  close cross is the last stop.
- **Research**: Enter on a building tab or faction switches it; the research stop has one
  region per category (Alt+Up/Down); Close is a mod-authored last node.
- **Build menu**: Enter on a size tab switches the list; arriving on a building selects it; the
  details stop lands on the selected tier tab, with captioned regions for income, garrison and
  requirements; the close cross is the last stop. The game's own Enter (purchase) and Up/Down
  (building cycling) are hidden while the mod claims them: check a physical Enter buys nothing
  by itself.
- **Owned entities, troop overview**: one table each; a category or town header row is a
  button that moves the camera, Right reads its tier and the six income figures; building and
  troop rows cycle the camera on Enter, Right reads count and tier; Alt+Up/Down jump
  categories; Close is a mod-authored last node.
- **Mini menu** (Enter on an owned entity): heading, details, actions, close; the stored
  wielder is one button that ejects; Escape is the game's.
- **Players menu** (co-op): a table with Allies and Enemies regions; Right on an ally row
  reaches its Resources and Towns buttons; the ally's resource summary is a stop that follows
  the row you are on; Close is the last node; Escape is the game's.
- **Gift town, send resource**: bands of buttons, the amount and any refusal reason read as
  parts; Escape presses the drawn close cross.
- **Marketplace** (a court of trade or an owned marketplace): a table of resource rows with
  Sell -1, Sell -5, Purchase +1, Purchase +5 columns whose cells are the price buttons; there is
  no Gold row (gold is the currency); Escape closes.
- **Post-battle**: attacker stop, defender stop, loot, then Manual Battle and Accept; the
  outcome is the screen name; Escape confirms (the game's binding).
- **Post-adventure result**: title, description or objectives, Statistics, then Main Menu and
  Load game in drawn order, then the player statistics link; no Escape.
- **Post-adventure statistics**: the graph type is a combo box opening a drop list, teams are
  checkboxes, the rounds table is a sheet under a heading band; the close cross is the last stop.

## Decisions taken in your absence

- Pause menu lands on Continue Game rather than the first drawn button.
- Story text reads the body as one spoken line and holds only the paragraphs in the buffer
  (your ruling, applied).
- Claim menu gets no close node: the game draws none and its own Escape handler decides.
- Slideshow dumps show the shown page's text on every row (a dump turns no pages, by design).
- Level up says "New Level. level 13" and "Archery. level 1" through the existing lowercase
  "level {0}" ModString instead of the old hard-coded "Level".
- Purchase wielder rows say "owned" / "dead" through two new ModStrings; the cost line is
  composed from the game's resource names. The players menu reuses "dead".
- Build menu requirement rows read "Peasant hut, missing" rather than "Missing Peasant hut,
  missing"; nine ModStrings are now unused and left for the phase G cleanup.
- Research rows no longer hand out tooltip actions (Enter is the buy).
- In the tables a building row reads count after the name (the sheet always walks the primary
  cell first, though the count is drawn to its left); the "Claimed" catch-all header is a plain
  text line, not a button, because the game moves no camera for it.
- The mini menu has no screen name: its heading is the start node and already says the name.
- The owned entities table reads tier texts as the game wrote them, so the prefab's "9999" and
  "1/4" placeholders on the claimed rows are silent.
- The players menu keeps the non-aggression pact button as a row cell; the local player's own
  disabled name button is not declared.
- The gift town and send resource buttons carry no tooltip section: their parts come from the
  tooltip and a section would read them twice.
- The marketplace lost its Gold row (the game draws none); the column captions are composed
  "Sell -1" through a new ModString from the game's two drawn captions.
- Post-adventure stats: the 13 graph types live in the drop list, not on the page.

## Needs your attention

- Real-key walks: none done by me (the `/key` route takes the desktop's focus).
- Rulings applied after the first handover: only the paragraphs in the story buffer; the
  lowercase "level 13" stays; returning from a child screen keeps re-announcing the page's
  name; the stray "unavailable" after a button that switches its page off is fixed in the
  navigator (an activation's effect on its own control is never reported by the live watch,
  and a control whose game object has gone inactive is muted).
- A hot reload while the post-battle screen is open loses the patch's bookkeeping, so the mod
  does not notice the Accept afterwards until the next reload (dev loop only).
- Research and build menu were captured with no owned research building and an empty site;
  the marketplace only as a standalone court of trade with nothing tradeable.
