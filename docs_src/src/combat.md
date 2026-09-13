# Combat

The battlefield is always the same size. What changes are the spawn points and terrain features.

## Hex Grid Coordinates

The battlefield is a hex grid. Each tile has six neighbors: west, east, northwest, northeast, southwest and southeast. There is no single north or south step. To move straight north, move northeast and then northwest. To move straight south, move southeast and then southwest.

Coordinates are spoken from the bottom-left origin, `0, 0`. The first number `x` is the horizontal position and the second number `y` is the vertical position. Because hex rows are staggered, diagonal moves change the horizontal position by half a step:

- East adds `1` to x.
- West subtracts `1` from x.
- Northeast adds `0.5` to x and `1` to y.
- Northwest subtracts `0.5` from x and adds `1` to y.
- Southeast adds `0.5` to x and subtracts `1` from y.
- Southwest subtracts `0.5` from x and subtracts `1` from y.

For example, from `0, 0`, moving northeast lands at `0.5, 1`. Moving southeast from there lands at `1, 0`, so one west move returns to `0, 0`.

## Terrain Vocabulary

The cursor, the scanner's `Terrain` category and the written battlefield descriptions all use one set of words for the ground. Every one of them is read off the board itself: troops step between neighbouring hexes only when the elevation differs by at most one, which is what decides most of the table below.

| Term | What it means | How it is judged | Where it is heard |
| --- | --- | --- | --- |
| flat ground | Ground at height 0 | Elevation 0 | Nowhere on the cursor, which says nothing about ordinary ground; descriptions call the rest of a board open ground |
| elevated ground, height N | A raised hex a troop can reach | Elevation above 0, and something can step onto it | Cursor |
| patch | A compact group of raised hexes | A connected group that is neither a ridge nor one hex | Scanner, descriptions |
| ridge | A long thin group of raised hexes | Its longest extent is at least three times its shortest, or it spans more than half the board | Scanner, descriptions |
| single cell | One raised hex on its own | A group of one | Scanner, descriptions |
| cliff | A raised hex nothing can enter | Every step onto it is two heights or more | Cursor, scanner, descriptions |
| impassable | A hex nothing can enter at all | The game says it is not walkable; water is impassable and nothing more | Cursor, scanner |
| wall of impassable cells | A line of impassable hexes that shapes movement | Three or more joined impassable hexes in a ridge shape | Scanner, descriptions |
| choke point | The one or two hexes everything has to pass through | Removing them would leave the walkable board in two halves of at least five hexes | Scanner, descriptions |
| wall | A siege layout's wall, which troops walk on | The layout is a siege and the hex carries the wall decoration | Cursor, scanner, descriptions |
| tower | A siege layout's tower | The same, for the tower decoration | Cursor, scanner, descriptions |
| stairs | The steps onto a siege layout's wall | The same, for the stairs decoration | Cursor, scanner, descriptions |
| spawn point | A hex a side's troops can start on | The layout says so | Cursor, scanner, descriptions |

`blocked` is never spoken on a battlefield. Whether a hex can be reached this turn is not a fact about the ground, and every hex an enemy stands on would be one.

The scanner's `Terrain` category holds one item per group, so a ridge is one stop in the item cycle and `Alt+Page Down` walks its hexes nearest first. A group names itself with its shape, its size and its height, as in `ridge of 7 cells, height 1`, `cliff of 3 cells` or `wall of 4 impassable cells`.

### Where Things Are

The written descriptions place a feature by thirds of the board, never by coordinates:

- `left` is columns 0 to 4, `centre` columns 5 to 8, `right` columns 9 to 12.
- `bottom` is rows 0 to 2, `middle` rows 3 to 5, `top` rows 6 to 8.
- The nine names combine those: `top left`, `top centre`, `top right`, `middle left`, `centre`, `middle right`, `bottom left`, `bottom centre`, `bottom right`.
- `left edge`, `right edge`, `top edge` and `bottom edge` are used when a feature hugs one.
- `across the centre` and `across the board` are used for something that spans the board.

## Troop Deployment

Before combat, the troop deployment screen allows placement of your troops in spawn points, representing their starting location in combat.

Move through the hex grid with:

- `A`: west
- `D`: east
- `Q`: northwest
- `E`: northeast
- `Z`: southwest
- `C`: southeast
- Use with `shift` to move to the next interesting tile, where interesting means it differs from the current tile
- `Ctrl+Space`: focus the centre tile
- `Ctrl+D`: describe the battlefield layout

Use drag and drop to rearrange your troops.

### Battlefield Description

Each battlefield layout the game ships has a written description of its terrain and of
where each side's spawn points are. It is on a tab stop of its own, called Description,
which also holds the drag hint, and `Ctrl+D` on the deployment grid speaks the same three
lines. Not every layout has been described yet; one that has not says so.

The deployment grid also supports the [Scanner](scanner.md) for finding enemy troops, spawn points and the groups of ground in [Terrain Vocabulary](#terrain-vocabulary) above. Scanning from the centre tile is a quick way to understand where troops and terrain features are, since each result is heard in the direction it lies in. See [Audio](audio.md#scanner-results).

## Combat

The combat hex grid uses the same keys for navigation and also supports the [Scanner](scanner.md), whose `Terrain` category holds the same groups of ground it does on the deployment grid.

Each hex you land on plays a short sound for what is on it: whether it is empty, raised, obstructed, holds an ally or an enemy, or is threatened by an enemy. See [Audio](audio.md#battlefield-sounds).

Every troop stack has a tooltip which can be reviewed with the UI buffer.

If focus is on a tile with an enemy or destructable entity, the tooltip shows an attack preview if it is possible for your acting troop to attack it.

If focused on a tile in movement range, the tooltip shows the movement cost.

### Combat Actions

The following hotkeys work when on the hex grid:

- `\`: performs the secondary action on the focused tile, corresponding to a right-click. Use this to move to a tile or perform an attack
- `S`: read detailed threat information for the focused tile. Nothing is spoken if the tile is not threatened.
- `Ctrl+D`: describe the terrain of the battlefield layout. Nothing is said about the spawn points here, since the troops have already been placed.
- `T`: move focus to the timeline
- Enter: performs the primary action on the focused tile, corresponding to a left-click. Use to select a target for spells
- `Escape`: cancels spell or ability targeting
- `,` and `Shift+,`: move focus between your troops in initiative order
- `Space`: move focus to your currently acting troop
- `.` and `Shift+.`: move focus between enemy troops in initiative order

The following hotkeys work anywhere on the combat screen:

- `Ctrl+R`: read your essence
- `Alt+R`: read enemy essence

### Inspect Mode

Press `I` on the combat grid to enter inspect mode for the focused hex. In inspect mode, navigation keys are restricted to moving within the relevant tiles.

If inspecting a tile within movement range of the acting troop, inspect mode shows you the path the stack would take to move there.

If inspecting a troop stack, each tile will read whether it is within that stack's zone of control, attack, deadly or movement range.

Press `Escape` to exit inspect mode.

### HUD Controls

Visibility is toggled with `h`. These controls are visible by default, and I recommend leaving it that way.

Use `tab` or `Shift+Tab` to cycle through controls.

When focus is in the HUD controls, pressing escape moves focus back to the grid.

For convenience, press `T` from the combat grid to move focus to the turn order. Press `Enter` on a troop in the turn order to focus that troop in the combat grid.

The game provides the following hotkeys:

- `Q`: use ability
- `E`: end turn
- `V`: open spellbook
- `O`: open chat

Since `Q` and `E` conflict with hex grid movement keys, move focus out of the grid first before using those keys.
