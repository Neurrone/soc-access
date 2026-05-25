# Changelog

## V0.3.1

- Wielders being recruited are now announced. This is mainly for situations where they join via story events
- Fixed the revealed buffer being reset after combat
- Fixed resource generating buildings not appearing in the scanner. Results are in a dedicated "Resource generators" category
- Fixed multi-tile entities being read even though they aren't fully revealed. In these situations, the entity will no longer be considered to be revealed
- Fixed artifact names not being announced when revealed
- Combat narration:
  - Effects being removed from a destroyed troop stack are no longer announced
  - If a spell is cast multiple times on the same tile, that tile is now only announced once
  - Recognize situations where your / enemy melee / ranged troops are affected by effects to avoid announcing each troop individually
  - When moving from a high ground tile to another tile that is on high ground, don't say that the high ground effect was added and then removed
  - When casting Acid Cloud, don't announce individual acid clouds appearing on each tile
  - Fixed more cases where the casting of the spell was read after narration of its effects
- Shorten combat influence readouts.

  Before: "in movement range of 59 enemy Plague Rats at 6, 8, zone of control and movement range of 15 enemy Oathbound at 9, 6, movement range of 19 enemy Oathbound at 7, 4, movement range of 5 enemy Spectres at 11.5, 3."

  After: Zone of control: 15 Oathbound at 9, 6. Movement range: 59 Plague Rats at 6, 8, 19 Oathbound at 7, 4, 5 Spectres at 11.5, 3."

- Fixed combat screens not being properly cleaned up if doing a quick battle, followed by a manual battle. This was causing the hotkey to read resources out of combat to fail
- Fixed regression in V0.2.0 that broke reading of marketplace screen button labels

## V0.3.0

- Added `Ctrl+f` command on the adventure map grid to search all scanner categories
- Newly revealed entities will now be announced. This includes entities spawning in areas visible to the player, although this is subject to change in case this provides information typically not available to a sighted player
- The scanner now has a Revealed category where revealed entities are placed
- Added support for bookmarking specific positions on the adventure map:
  - There are 10 bookmark slots, 1 through 0
  - `Ctrl+number`: saves a bookmark at that slot
  - `Shift+number`: jump to bookmarked position
  - `Alt+number`: read directions from the currently focused tile to the bookmarked position
  - `Ctrl+shift+number`: toggle playback of an audio beacon at the bookmarked position
- Add setting to control playback of a positional beep when moving through scanner result entries. This is turned off by default
- The troop purchase and upgrade screens have been rewritten to work similar to the marketplace instead of being a faithful translation of the game's UI. The number of tab stops no longer scales with the available troops
- On the troop deployment screen:
  - Fix wrong enemy names
  - Fix opponent wielder portrait tooltips not being reliably shown in cases where you are the defender
- Fix being stuck on the victory screen after successfully laying siege to a settlement
- In combat, tile effects like Acid Clouds are now read
- Edit fields no longer require pressing enter to edit them, just type text into them when focused

## V0.2.0

- Localization support for all languages supported by the game
- Fix move troop amount dialog not being detected when troops are looking to join your army, and made escape only cancel the move instead of also exiting from the "troops are looking to join" screen
- Stop reading effect and decorative features on adventure map tiles to reduce verbosity, since they have no gameplay effect
- Objectives now indicate approximate direction and distance relative to the currently selected wielder. This provides information similar to objective markers rendered on the minimap.
- Added the following hotkeys on the combat grid to move focus to various troops:
  - `,` and `shift+,` moves focus between your troops in initiative order
  - `Space` moves focus to your currently acting troop
  - `.` and `Shift+.` moves focus between enemy troops in initiative order
- Added the following hotkeys for the adventure map to move focus to various HUD controls:
  - `O`: objectives
  - `N`: notifications
  - `R`: resources
  - `T`: troops
- Added new setting to control whether story camera focus events are read. This is enabled by default
- Fix various things in the defences screen not reading correctly
- Fixed wrong label for spellbook screen checkbox
- Add missing label for the wielder list menu in adventure map HUD

## V0.1.4

- Don't announce a story camera focus event if it is identical to the last announced camera movement
- When setting a destination, the route preview indicators on tiles from the wielder to the destination are now indicated
- When reading tiles, "impassable" now describes impassable terrain. "Blocked" now indicates tiles that can't be accessed due to dynamic game state like an occupying or blocking entity, and no longer describes impassable terrain.
- Scanner improvements:
  - Scanner results now refresh automatically when using scanner commands, so a separate refresh step is no longer required
  - Scanner navigation now wraps between results, subcategories, and categories
  - Scanner results are now tracked by stable identity, so stale results are pruned more reliably after the game state changes
  - Added `All` subcategories to all categories with more than one subcategory for adventure map, troop placement, and combat
  - Improved adventure map terrain scanning performance
- For hex grids, the mod now uses a half-x hex coordinate system. Diagonal movement changes x by 0.5 instead of exposing the game's staggered raw grid coordinates
- The scanner now recognizes beacons of power, results will be in a beacons category
- Add mod settings screen accessed by `ctrl+m`. It currently contains a setting that controls whether enemy influence (attack, deadly, movement range and zone of control information) is read on tiles in combat. If disabled, such threatened tiles are indicated by "threatened"
- Introduce an `s` hotkey in combat to read enemy influence

## V0.1.3

- Remove brackets around coordinate announcements, so `x, y` instead of `(x, y)`. This shortens speech when the screen reader is configured to announce more punctuation
- Tab and shift+tab now wrap for convenience. A sound is played to indicate when this happens
- When focus is not on the grid in the adventure map or combat screens, pressing escape now moves focus back to the grid
- Enemy wielders moving outside your view range will no longer be announced, woops
- Teleports are now announced as teleports instead of move. The accessibility cursor also follows the wielder to the teleport destination
- Teleports and movement events from wielders that don't belong to you are now saved to the notifications buffer
- Shortened "Not visible" indicator for fog of war tiles to "unseen" for brevity. Also added documentation about fog of war
- Added sound indicator when cancelling a drag
- When unchecking the auto populate quickvar option in the spellbook, fix add to quickbar action not being exposed unless the spellbook was reopened
- Documentation updates:
  - Move changelog to documentation site
  - Add development status and list of known issues
  - Add a separate section for tooltips
  - Clarify the 3 tile visibility states and "fog" is a decorative terrain feature, not an indication of fog of war

## V0.1.2

This is an emergency release because I left out a critical config file, so this wasn't working for anyone else. Woops.

## V0.1.1

- Fixed regression that broke support for popup dialogs
- Make the claim menu accessible. This screen appears when interacting with an enemy settlement
- Fix notifications widget in adventure map HUD not appearing even when there were notifications to display

## V0.1.0

Initial release
