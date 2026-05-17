# Adventure Map

## Adventure Map Grid

The adventure map has an accessibility cursor. The mod speaks the tile under the cursor and keeps a visual overlay on the tile that has accessibility focus.

### Moving Around

Use the arrow keys to move the map cursor.

The mod keeps the focused tile in view and reads the tile as you move.

Tiles within the movement range of the currently selected wielder are indicated as reachable.

Tiles that are impassable due to terrain are announced as being blocked.

A tile can be in one of three visibility states:

- Currently visible: in view radius of your wielder or building
- Unseen: explored but there are no eyes on the tile (i.e, fog of war). Such tiles show their last seen state
- Unexplored

The mod mentions if a tile is unexplored or currently unseen.

### Fog Tiles

Some tiles may read as "fog". This is a terrain / decorative feature, not an indication of tiles in the fog of war, which the mod indicates as being unseen.

### Actions

- Enter: perform the primary action on the focused tile, corresponding to a left-click. If the tile has a selectable entity such as a building, this brings up the entity's menu for further interaction. Otherwise, this clears the wielder's destination.
- Backslash: perform the secondary action on the focused tile, corresponding to a right-click. Press once on a tile to set it as the destination. Press again to actually move. If the tile has an interactable entity and the wielder is nearby, this causes your wielder to interact with the entity.
- W: select the next wielder
- S: select the next settlement
- Space: move focus to the selected wielder's tile

### Route Previews

After setting a destination, the game draws a route preview. When focus is on a tile with a route preview marker, the mod speaks the following information:

- `On route`: part of the planned route.
- `On route, furthest reachable this turn`: furthest point the wielder can reach this turn.
- `On route, in n turns`: the tile is on the route, but can only be reached on a later turn.
- `On route, furthest reachable in n turns`: furthest point the wielder can reach by that later turn.
- `Destination`: the tile is the selected destination.
- `Destination, in n turns`: the destination cannot be reached this turn.
- `Destination, interactable`: the destination has an interactable object.
- `Destination, interactable next turn`: the destination is reachable this turn, but there is not enough movement left to interact with it this turn.
- `Destination, no route preview`: the destination is set, but the game is not showing route preview markers for it.
- `cost n`: the route has crossed the indicated movement cost marker.

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
