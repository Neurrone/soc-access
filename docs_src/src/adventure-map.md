# Adventure Map

## Adventure Map Grid

The adventure map has an accessibility cursor. The mod speaks the tile under the cursor and keeps a visual overlay on the tile that has accessibility focus.

### Moving Around

Use the arrow keys to move the map cursor.

The mod keeps the focused tile in view and reads the tile as you move.

A tile can be in one of three states:

- Currently visible: in view radius of your wielder or building
- Unseen: explored but there are no eyes on the tile (i.e, fog of war). Such tiles show their last seen state
- Unexplored

The mod mentions if a tile is unexplored or currently unseen.

### Actions

- Enter: perform the primary action on the focused tile, corresponding to a left-click. If the tile has a selectable entity such as a building, this brings up the entity's menu for further interaction. Otherwise, this clears the wielder's destination.
- Backslash: perform the secondary action on the focused tile, corresponding to a right-click. Press once on a tile to set it as the destination. Press again to actually move. If the tile has an interactable entity and the wielder is nearby, this causes your wielder to interact with the entity.
- W: select the next wielder
- S: select the next settlement
- Space: move focus to the selected wielder's tile

## HUD

The HUD provides various controls for the currently selected wielder such as the wielder portrait, resources and objectives. Visibility is toggled with `h`. These controls are visible by default, and I recommend leaving it that way.

Press tab from the map grid to cycle through HUD controls.

When focus is in the HUD controls, pressing escape moves focus back to the grid.

The game provides the following hotkeys:

- C: open character sheet
- V: open spellbook
- E: end turn
- Q: move wielder to selected destination
- F5: quick save
- F9: quick load
- X: build menu
- F1: owned entities
- F2: troop income
- F3: research
- F4: marketplace

### Troop Management

The selected wielder's troop slots are exposed as a menu. Use Tab until you reach the troop widget, then use Up, Down, Home, and End to move through slots.

When focus is on a draggable troop stack, press Space to start dragging it, move to the destination slot, and press Enter to drop. Press Escape to cancel.

## Reading Resources

Press Ctrl+R to hear a summary of your current resources, this works in all non-combat situations. You can also Tab to the resources widget and review individual resource entries and tooltips.
