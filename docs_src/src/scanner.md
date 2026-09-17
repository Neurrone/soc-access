# Scanner

The scanner helps you find important things without manually checking every tile.

## Availability

The scanner is available in the following screens:

- Adventure map
- Troop placement
- Combat

## Visibility

On the adventure map, scanner results will only show you what you have explored.

Currently visible things are read normally. Explored tiles that are not currently visible are read as `Unseen` (i.e, fog of war).

## How Results Are Grouped

Results are grouped in four levels: category, subcategory, item, and instance.

An item is one named thing and an instance is one copy of it. Twelve chests on the map are a single stop in the item cycle, and the instance cycle walks the twelve chests themselves. This keeps a map full of repeated pickups from burying everything else.

Moving to a different item reads its name. Stepping between copies of the same item leaves the name out, since you were just told what you are walking through.

## Controls

- `Ctrl+Page Down`: next category
- `Ctrl+Page Up`: previous category
- `Shift+Page Down`: next subcategory
- `Shift+Page Up`: previous subcategory
- `Page Down`: next item
- `Page Up`: previous item
- `Alt+Page Down`: next copy of the current item
- `Alt+Page Up`: previous copy of the current item
- `Home` or `J`: move the accessibility cursor to the current scanner result and read the tile it lands on. A jump to the tile you are already on says `here`
- `Backspace`: return the cursor to the tile it was on before the last jump
- `End`: read how far away the current result is and which way it lies

## Notes

- Results are re-queried every time you press a scanner key, so there is no refresh key. Something that has moved is announced at its new position, and something that is gone is dropped as you navigate past it.
- When nothing has been scanned yet, the first scanner key press scans and lands on the first category that has anything in it.
- Category, subcategory, item, and instance navigation all wrap.
- Readouts describe the thing that was scanned rather than everything else on its tile. The mod setting `Scanner result announcements` controls what each readout includes.
- Directions are relative to the current accessibility cursor and use a short form such as `3ne` by default. The mod setting `Long directions` reads them as `3 northeast` instead.
- Directions are always the straight line between the cursor and the result. The order the results come in is the straight line too, unless the mod setting `Sort scanner results by` on the adventure map tab is set to the walk; a result nothing can reach today is read as `blocked`, which names the army in the way when you can see one. See [Mod Settings](mod-settings.md#sort-scanner-results-by).
- `Backspace` is not a toggle. The remembered tile is cleared once you return to it, and it is replaced every time you jump again. On the adventure map it also returns you from a bookmark jump.
- Paging through results also plays the sound of the result you land on, positioned relative to the accessibility cursor. See [Audio](audio.md#scanner-results).

## Custom Categories

You can define your own categories that collect the things you care about. Each scanner context has three of them, numbered 1 to 3, and each context keeps its own three. Open mod settings with the `Mod options` button on the main menu or the pause menu, go to the scanner tab, and choose `Adventure map custom categories` or `Battle custom categories`. The list that opens always has the same three rows, `Custom category 1`, `Custom category 2` and `Custom category 3`, each naming what it holds or reading `empty`.

A custom category is built from two kinds of rule:

- Subcategories picked from the scanner's own categories, which contribute everything in them.
- Keywords, which contribute every result whose name matches the word you typed. Matching ignores case and matches whole words and prefixes, the same way scanner search does.

Each rule becomes a subcategory of your category, and an `All` subcategory gathers everything the rules found with duplicates removed. Custom categories are placed before the built-in ones in the category cycle.

Selecting a custom category opens an editor where you can rename it, choose its subcategories, and add and remove keywords. `Clear this custom category` empties it; the numbered slot stays and can be filled again. A category needs a name, and a name another category already answers to is refused. Your categories are saved in the mod's configuration file and persist between sessions.

### One Key Per Category

On the adventure map, each of the three custom categories can be walked with a single key instead of the four-level cycle. `,` walks custom category 1, `.` walks custom category 2, and `/` walks custom category 3. Each press moves to the next entry, and holding `Shift` walks backwards. The key always belongs to the same number, so a key never changes what it walks because of something you did to another category.

These keys ignore the item grouping and walk the category's `All` subcategory as one flat list, nearest first. Twelve chests are twelve stops rather than one, because a single key has no separate cycle for the copies of an item. The count you hear is your position in that flat walk, and the category name is not repeated on every press.

The walk answers "nearest first from where I am". Once the accessibility cursor moves, by arrow keys, by a jump to the current result, or by a bookmark jump, the next press starts a fresh sweep from wherever the cursor now is. When the cursor is sitting on the entry you are already on, the press steps to the next-nearest rather than announcing the same entry again, so pressing `J` and the key in turn walks the map by nearest neighbour.

`J` is a second key for the jump that `Home` also does. It is here so that the jump and these three keys sit under the same hand.

`Home`, `J`, `End`, and `Backspace` act on whatever the key lands on, exactly as they do for the paging cycles. Pressing a key whose category is empty says so.

These keys are adventure map only. In combat, `,` and `.` remain the acting and enemy troop cycles, so the three battle categories are reached through the category cycle.

## Adventure Map Features

On the adventure map grid, the following additional features are supported.

### Search

Use `Ctrl+f` to perform a search across all categories of the scanner. This is useful for finding specific things quickly. Results are placed in a temporary scanner category.

### Look Around

Use `L` to look around from the currently focused tile. Results are placed in a temporary category, similar to the search command. Results start north of the cursor and continue clockwise.

Use `K` to increase the look radius and `Shift+K` to decrease it. The default radius is 15 tiles. Changing the radius does not refresh existing results; press `L` again to scan with the new radius.

### One Key Per Custom Category

`,`, `.`, and `/` walk custom categories 1, 2 and 3 in a single keypress. See [One Key Per Category](#one-key-per-category) above.
