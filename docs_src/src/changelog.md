# Changelog

## V0.1.4

- Don't announce a story camera focus event if it is identical to the last announced camera movement
- When setting a destination, the route preview indicators on tiles from the wielder to the destination are now indicated
- When reading tiles, "impassable" now describes impassable terrain. "Blocked" now indicates tiles that can't be accessed due to dynamic game state like an occupying or blocking entity, and no longer describes impassable terrain.

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
