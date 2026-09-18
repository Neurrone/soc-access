# Scanner

The scanner helps you find important things without manually checking every tile.

## Availability

The scanner is available in the following screens:

- Adventure map
- Troop placement
- Combat

## Visibility

On the adventure map, scanner results will only show you what you have explored.

Currently visible things are read normally. Explored tiles that are not currently visible are read as `Unseen` (i.e, in fog of war).

## Controls

Results are grouped in four levels: category, subcategory, item and instance. An item is one named thing and an instance is one copy of it. This prevents multiple copies of something from clogging up the list.

- `Ctrl+Page Down`: next category
- `Ctrl+Page Up`: previous category
- `Shift+Page Down`: next subcategory
- `Shift+Page Up`: previous subcategory
- `Page Down`: next item
- `Page Up`: previous item
- `Alt+Page Down`: next instance of the current item
- `Alt+Page Up`: previous instance of the current item
- `Home` or `J`: move the accessibility cursor to the current scanner result's tile
- `Backspace`: return the cursor to the tile it was on before the last jump
- `End`: directions from the cursor to the result's position

## Directions and Sorting

Directions read to each result are line of sight distances relative to the current cursor position. They are abbreviated such as `3ne` by default, this can be configured in mod settings.

By default, results are sorted by straight line distance to the cursor, which ignores the actual path a wielder would have to traverse. In mod settings, this can be changed to sort by walkable path cost instead.

## Custom Categories

You can define up to 3 custom categories in the adventure map or in combat. These custom categories can show you things you care about.

- `,` or `Shift+,`: cycles through results of custom category 1
- `.` or `Shift+.`: cycles through results of custom category 2
- `/` or `Shift+/`: cycles through results of custom category 3

For example, an "Explorer" custom category could include unvisited pickups, resource generators, wielders or settlements that don't belong to you. Results in custom categories are still available in the `Page up` / `Page down` sequence. If this was the first custom category, use `,` and `Shift+,` to cycle through results and either `Home` or `J` to move the cursor to it; cycling through results in custom categories this way is more convenient than using the normal 4-level hierarchy.

These 3 hotkeys are only available in the adventure map because they conflict with commands on the combat screen.

## Adventure Map Features

On the adventure map grid, the following additional features are supported.

### Search

Use `Ctrl+f` to perform a search across all categories of the scanner. This is useful for finding specific things quickly. Results are placed in a temporary scanner category.

### Look Around

Use `L` to look around from the currently focused tile. Results are placed in a temporary category, similar to the search command. Results start north of the cursor and continue clockwise.

Use `K` to increase the look radius and `Shift+K` to decrease it. The default radius is 15 tiles. Changing the radius does not refresh existing results; press `L` again to scan with the new radius.
