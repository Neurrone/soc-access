# Combat

## Troop Deployment

Before combat, the troop deployment screen allows placement of your troops in spawn points, representing their starting location in combat.

The battlefield is a hex grid. Move with:

- A: west
- D: east
- Q: northwest
- E: northeast
- Z: southwest
- C: southeast

Use drag and drop to rearrange your troops.

The deployment grid also supports the [Scanner](scanner.md) for finding enemy troops, spawn points and terrain features.

## Combat

The combat hex grid uses the same keys for navigation and also supports the [Scanner](scanner.md).

To assist with movement, focusing a tile will indicate if it is within an enemy troop stack's movement, attack or deadly range, or their zone of control.

Every troop stack has a tooltip which can be reviewed with the UI buffer.

If focus is on a tile with an enemy or destructable entity, the tooltip shows an attack preview if it is possible for your acting troop to attack it.

If focused on a tile in movement range, the tooltip shows the movement cost.

### Combat Actions

- Backslash: performs the secondary action on the focused tile, corresponding to a right-click. Use this to move to a tile or perform an attack
- T: move focus to the timeline
- Ctrl+R: read your essence
- Alt+R: read enemy essence
- Enter: performs the primary action on the focused tile, corresponding to a left-click. Use to select a target for spells
- Escape: cancels spellcasting

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
