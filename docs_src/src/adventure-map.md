# Adventure Map

## Adventure Map Grid

The adventure map has an accessibility cursor. The mod speaks the tile under the cursor and keeps a visual overlay on the tile that has accessibility focus.

### Moving Around

Use the arrow keys to move the map cursor. Use the shift+arrow keys to move to the next interesting tile, where interesting means it differs from the current tile.

The mod keeps the focused tile in view and reads the tile as you move.

Tiles within the movement range of the currently selected wielder are indicated as reachable.

Tiles that cannot be entered because of terrain are announced as impassable. Tiles that are otherwise blocked by game state, such as an occupying unit or blocking map entity, are announced as blocked.

A tile can be in one of three visibility states:

- Currently visible: in view radius of your wielder or building
- Unseen: explored but there are no eyes on the tile (i.e, fog of war). Such tiles show their last seen state
- Unexplored

The mod mentions if a tile is unexplored or currently unseen.

### Actions

- `Enter`: perform the primary action on the focused tile, corresponding to a left-click. If the tile has a selectable entity such as a building, this brings up the entity's menu for further interaction. Otherwise, this clears the wielder's destination.
- `\`: perform the secondary action on the focused tile, corresponding to a right-click. Press once on a tile to set it as the destination. Press again to actually move. If the tile has an interactable entity and the wielder is nearby, this causes your wielder to interact with the entity.
- `A`: get a summary of entities reachable by the current wielder excluding visited pickups or friendly resource generators
- `W`: select the next wielder
- `S`: select the next settlement
- `Space`: move focus to the selected wielder's tile

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

### Revealed Entity Announcements

Entities revealed during exploration are announced. They can also be found in a separate category in the [scanner](scanner.md).

### Bookmarks

Bookmarks let you save adventure map locations and return to them later. There are ten bookmark slots, using the number keys `1` through `9` and `0`.

- `Ctrl+number`: saves the current position to that bookmark slot
- `Shift+number`: jump to that bookmark slot
- `Alt+number`: read directions from the currently focused tile to the bookmarked position

Saving to an existing slot overwrites it. There is no separate delete command.

### Beacons

Beacons are looping sounds that allow bookmarks to serve as spatial audio markers.

Use `Ctrl+Shift+number` to toggle the beacon for that bookmark slot

The beacon sound changes relative to the currently focused tile:

- east and west changes stereo position
- north and south changes pitch
- distance changes volume

## HUD

The HUD provides various controls for the currently selected wielder such as the wielder portrait, resources and objectives. Visibility is toggled with `h`. These controls are visible by default, and I recommend leaving it that way.

Press tab from the map grid to cycle through HUD controls. When on the adventure map, press the following keys to move focus to the following HUD controls:

- `N`: notifications
- `O`: objectives
- `R`: resources
- `T`: troops

When focus is in the HUD controls, pressing escape moves focus back to the grid.

Additionally, the game provides the following hotkeys that work anywhere on the adventure map screen:

- `C`: open character sheet
- `V`: open spellbook
- `E`: end turn
- `Q`: move wielder to selected destination
- `F5`: quick save
- `F6`: players menu
- `F9`: quick load
- `X`: build menu
- `F1`: owned entities
- `F2`: troop income
- `F3`: research
- `F4`: marketplace

### Troop Management

The selected wielder's troop slots are exposed as a menu. From the adventure map grid, press `T` to focus the troops widget, then use `Up`, `Down`, `Home`, and `End` to move through slots.

When focus is on a draggable troop stack, press `Space` to start dragging it, move to the destination slot, and press `Enter` to drop. Press `Escape` to cancel dragging.

## Reading Resources

Press `Ctrl+R` to hear a summary of your current resources, this works in all non-combat situations. You can also Tab to the resources widget and review individual resource entries and tooltips. From the adventure map, `R` moves focus to the resources widget.
