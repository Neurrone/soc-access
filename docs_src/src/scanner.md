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
- `Home` or `J`: move the accessibility cursor to the current scanner result
- `Backspace`: return the cursor to the tile it was on before the last jump
- `End`: read how far away the current result is and which way it lies

## Notes

- Results are re-queried every time you press a scanner key, so there is no refresh key. Something that has moved is announced at its new position, and something that is gone is dropped as you navigate past it.
- When nothing has been scanned yet, the first scanner key press scans and lands on the first category that has anything in it.
- Category, subcategory, item, and instance navigation all wrap.
- Readouts describe the thing that was scanned rather than everything else on its tile. The mod setting `Scanner result announcements` controls what each readout includes.
- Directions are relative to the current accessibility cursor and use a short form such as `3ne` by default. The mod setting `Long directions` reads them as `3 northeast` instead.
- `Backspace` is not a toggle. The remembered tile is cleared once you return to it, and it is replaced every time you jump again. On the adventure map it also returns you from a bookmark jump.
- Paging through results also plays the sound of the result you land on, positioned relative to the accessibility cursor. See [Audio](audio.md#scanner-results).

## Custom Categories

You can define your own categories that collect the things you care about. Press `Ctrl+m` to open mod settings, go to the scanner tab, and choose `Adventure map custom categories` or `Battle custom categories`. Each context keeps its own set.

A custom category is built from two kinds of rule:

- Subcategories picked from the scanner's own categories, which contribute everything in them.
- Keywords, which contribute every result whose name matches the word you typed. Matching ignores case and matches whole words and prefixes, the same way scanner search does.

Each rule becomes a subcategory of your category, and an `All` subcategory gathers everything the rules found with duplicates removed. Custom categories are placed before the built-in ones in the category cycle.

Selecting a custom category opens an editor where you can rename it, choose its subcategories, add and remove keywords, and delete it. Your categories are saved in the mod's configuration file and persist between sessions.

### One Key Per Category

On the adventure map, a custom category can be walked with a single key instead of the four-level cycle. Three keys are available: `,`, `.`, and `/`. Each press moves to the next entry in the category on that key, and holding `Shift` walks backwards.

A new custom category takes the first key nobody is using, so the first category you create lands on `,`, the second on `.`, and the third on `/`. Categories created after that start with no key. A category that has no key, including any you made before the keys existed, can be given one from its editor.

To change which category a key walks, open the category's editor and choose `Key`. The list shows every key and which category holds it. Picking a key that another category already has moves it, leaving that category with no key. Picking `None` clears the key without giving it to anyone else, and it stays free until you assign it.

Deleting a category frees its key. The freed key is not handed to another category automatically; the next category you create takes it, or you can assign it yourself.

These keys ignore the item grouping and walk the category's `All` subcategory as one flat list, nearest first. Twelve chests are twelve stops rather than one, because a single key has no separate cycle for the copies of an item. The count you hear is your position in that flat walk, and the category name is not repeated on every press.

The walk answers "nearest first from where I am". Once the accessibility cursor moves, by arrow keys, by a jump to the current result, or by a bookmark jump, the next press starts a fresh sweep from wherever the cursor now is. When the cursor is sitting on the entry you are already on, the press steps to the next-nearest rather than announcing the same entry again, so pressing `J` and the key in turn walks the map by nearest neighbour.

`J` is a second key for the jump that `Home` also does. It is here so that the jump and these three keys sit under the same hand.

`Home`, `J`, `End`, and `Backspace` act on whatever the key lands on, exactly as they do for the paging cycles. Pressing a key that no category holds says so.

These keys are adventure map only. In combat, `,` and `.` remain the acting and enemy troop cycles.

## Adventure Map Features

On the adventure map grid, the following additional features are supported.

### Search

Use `Ctrl+f` to perform a search across all categories of the scanner. This is useful for finding specific things quickly. Results are placed in a temporary scanner category.

### Look Around

Use `L` to look around from the currently focused tile. Results are placed in a temporary category, similar to the search command. Results start north of the cursor and continue clockwise.

Use `K` to increase the look radius and `Shift+K` to decrease it. The default radius is 15 tiles. Changing the radius does not refresh existing results; press `L` again to scan with the new radius.

### One Key Per Custom Category

`,`, `.`, and `/` each walk one of your custom categories in a single keypress. See [One Key Per Category](#one-key-per-category) above.
