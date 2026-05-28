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

## Troop Deployment

Before combat, the troop deployment screen allows placement of your troops in spawn points, representing their starting location in combat.

Move through the hex grid with:

- A: west
- D: east
- Q: northwest
- E: northeast
- Z: southwest
- C: southeast
- Use with shift to move to the next interesting tile, where interesting means it differs from the current tile

Use drag and drop to rearrange your troops.

The deployment grid also supports the [Scanner](scanner.md) for finding enemy troops, spawn points and terrain features.

## Combat

The combat hex grid uses the same keys for navigation and also supports the [Scanner](scanner.md).

Every troop stack has a tooltip which can be reviewed with the UI buffer.

If focus is on a tile with an enemy or destructable entity, the tooltip shows an attack preview if it is possible for your acting troop to attack it.

If focused on a tile in movement range, the tooltip shows the movement cost.

### Combat Actions

The following hotkeys work when on the hex grid:

- `\`: performs the secondary action on the focused tile, corresponding to a right-click. Use this to move to a tile or perform an attack
- `S`: read detailed threat information for the focused tile. Nothing is spoken if the tile is not threatened.
- `T`: move focus to the timeline
- Enter: performs the primary action on the focused tile, corresponding to a left-click. Use to select a target for spells
- Escape: cancels spellcasting
- `,` and `Shift+,`: move focus between your troops in initiative order
- `Space`: move focus to your currently acting troop
- `.` and `Shift+.`: move focus between enemy troops in initiative order

The following hotkeys work anywhere on the combat screen:

- `Ctrl+R`: read your essence
- `Alt+R`: read enemy essence

### Inspect Mode

Press I on the combat grid to enter inspect mode for the focused hex. In inspect mode, navigation keys are restricted to moving within the relevant tiles.

If inspecting a tile within movement range of the acting troop, inspect mode shows you the path the stack would take to move there.

If inspecting a troop stack, each tile will read whether it is within that stack's zone of control, attack, deadly or movement range.

Press escape to exit inspect mode.

### HUD Controls

Visibility is toggled with `h`. These controls are visible by default, and I recommend leaving it that way.

Press tab from the combat grid to cycle through HUD controls.

When focus is in the HUD controls, pressing escape moves focus back to the grid.

For convenience, press T from the combat grid to move focus to the turn order. Press Enter on a troop in the turn order to focus that troop in the combat grid.

The game provides the following hotkeys:

- Q: use ability
- E: end turn
- V: open spellbook

Since Q and E conflict with hex grid movement keys, move focus out of the grid first before using those keys.
