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

## Controls

- `End`: refresh and repeat scanner results
- `Ctrl+Page Down`: next category
- `Ctrl+Page Up`: previous category
- `Shift+Page Down`: next subcategory
- `Shift+Page Up`: previous subcategory
- `Page Down`: next result
- `Page Up`: previous result
- `Home`: move the accessibility cursor to the current scanner result
- `Shift+Home`: repeat the current scanner result

## Search

On the adventure map, use `Ctrl+f` to perform a search across all categories of the scanner. This is useful for finding specific things quickly.

## Notes

- Scanner commands refresh results automatically as you navigate through them.
- `End` can still be used to explicitly refresh and re-announce scanner results.
- Category, subcategory, and result navigation wraps.
- Directions are relative to the current accessibility cursor.
