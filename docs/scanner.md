# Scanner

The scanner helps you find important things without manually checking every tile.

## Availability

The scanner is available in the following screens:

- Adventure map
- Troop placement
- Combat

## Visibility

On the adventure map, scanner results respect what your player currently knows.

Currently visible things are read normally. Explored things that are not currently visible are read as `not visible`.

Troop placement and combat do not use visibility filtering because the whole grid is available.

## Controls

- `End`: refresh scanner results
- `Ctrl+Page Down`: next category
- `Ctrl+Page Up`: previous category
- `Shift+Page Down`: next subcategory
- `Shift+Page Up`: previous subcategory
- `Page Down`: next result
- `Page Up`: previous result
- `Home`: move the accessibility cursor to the current scanner result
- `Shift+Home`: repeat the current scanner result

## Notes

- Refresh after the map changes, such as after picking something up, visiting a location, moving, or killing an enemy's troop.
- Directions are relative to the current accessibility cursor.
- On the adventure map, jumping to a result also moves the camera so the cursor is visible.
