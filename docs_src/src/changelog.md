# Changelog

## V1.0.0

- Added a procedural sound system: moving the accessibility cursor now plays a short cue for every tile, on the adventure map (roads, open ground, sand, water, trees, impassable and unexplored tiles) and on the battlefield and troop deployment grids (empty hexes, elevation levels 1 to 3, obstacles, and troops)
  - Things on the map play a two-part gesture instead of their terrain sound: a category sound for wielders, settlements, resource deposits, and pickups, followed by an `Ally` or `Enemy` marker. Neutral things play the category sound alone, keeping ally and enemy easy to pick out
  - Added a sonar sweep on `P`: every entity the scanner can see within the look around radius plays its gesture from west to east, positioned by direction and quieter with distance, giving the shape of your surroundings in a few seconds
  - Moving through scanner results now plays each result's sound positioned relative to the cursor. This replaces the "Scanner plays directional beep" setting and its sound file
  - Added an audio glossary, reachable from the new Audio tab in mod settings: play any cue on demand to learn it, and tune each cue's volume, pitch, and duration or disable it
  - A refused move, such as bumping the map edge or a skip that finds nothing, now plays a falling bonk sound
  - Added an [Audio chapter](audio.md) to the documentation consolidating all sound features in one place

## V0.7.4

- Fixed tiles no longer being announced when returning to them after passing over tiles that announce nothing, which could happen when announcement elements such as coordinates and terrain are disabled

## V0.7.3

- Added `D` command on the adventure map that describes the position of the accessibility cursor and the size of the map
- Fixed movement between two tiles on the adventure map and battlefield that announce identically being silent when coordinate announcements are disabled

## V0.7.2

- Information spoken on the focused tile or in scanner results are now a composable set of announcements that you can toggle, reorder, and customize individually from the mod settings screen
- Using the composable announcements setting introduced above, added an option to read the movement cost on the adventure map. There are two settings for controlling whether it is read when focusing on adventure map tiles and on the scanner. This is disabled by default
- Fixed untranslated caster and effect names being narrated in combat
- Increase log verbosity to help with debug Prism issues when it doesn't detect screen readers properly
- Update Prism to 0.16.7 to fix issues with Baoyi screen reader support

## V0.7.1

- Fixed combat grid tooltips lingering after combat ends or while loading another game. This caused the game to hang while loading a game from the combat screen
- Make the faction selector on the research screen accessible when playing in mixed factions mode
- Group announcements of effects caused by aura buffs like Fortune and Inspire together, instead of announcing it individually per stack
- Try fixing edge cases where if combat ends in story text or level ups, the adventure map screen did not get initialized. Please let me know if you still see this happening
- Added `Ctrl+Space` shortcut to move focus to the centre tile on troop placement and combat hex grids
- Added announcement when cancelling ability targeting
- Fixed combat narration using untranslated spell, ability, and modifier names
- Update known issues to mention `Shift+Tab` being too responsive
- Work around Prism not detecting the Baoyi screen reader by bundling its controler DLL
- Fixed use of untranslated text for dismissing adventure map notifications and cancelling spellcast messages

## V0.7.0

- Updates to support the Yulan DLC:
  - Fixed reading of Yulan troop upgrade costs and building upgrade choices
  - Upgraded Yulan buildings now include the essence type if applicable on the adventure map
  - Add label for Yulan maps on the lobby screen
- Changed command to focus the objectives widget from `O` to `B` to avoid overriding the game hotkey for opening the chat window
- Added support for the chat window
- Text on story screens are now buttons, allowing `Enter` to advance to the next screen. Fixes having to `tab` to the next button to advance to the next screen
- The first line of tooltips is no longer suppressed in cases where doing so would cause confusion. Previously, the first line was hidden if it was a substring of the current widget's label. This is now done only if it is an exact match with the widget's label
- Combat improvements:
  - Enemies that are attackable by the current troop are indicated when focused
  - Mention which direction beam troops face
  - Improved narration of beam attacks
  - Fixed narration of leap ability
- Fixed the secondary action command being impossible to use on misbehaving systems that don't send an actual `\` key code when pressing it on the keyboard
- Rewrote troop management section in the documentation to mention the disband tooltip action
- Improved Portuguese translations for generated essence and hostile troop labels

## V0.6.5

- Fixed incorrect use of a helper to join strings, causing strange wording in objectives, build, spellbook and quickbar screens
- Localize message on build screen for no build slot being selected

## V0.6.4

- The mod now has an automated GUI-based installer
- The scanner now identifies unexplored tiles
- Translation fixes

## V0.6.3

- Fixed scanner bug where hitting `End` would say "No scanner results" if there are no pickups in view, but there are results in other categories
- Add support for artifact market screen
- Add support for portals that allow selection between multiple teleport destinations

## V0.6.2

- Downgrade Prism from 0.16.5 to 0.16.1 to fix a [Prism regression](https://github.com/ethindp/prism/issues/49) causing other screen readers besides NVDA not to be properly detected

## V0.6.1

- Fixed language translations using mojibake accents or ASCII fallbacks

## V0.6.0

- Switched from Tolk to Prism to support more screen readers
- Added support for hostile join screen when hostiles propose to join for gold
- Documentation updates to prepare for public release

## V0.5.0

- Experimental support for starting multiplayer games. Will need other players to test actual games with to ensure this works properly
- Support screens for trading resources and settlements in the players menu
- Added support for community campaigns
- Pressing shift with a movement key skips to the next interesting tile in that direction
- Added two new commands on the adventure map grid:
  - `A` summarizes entities reachable by the current wielder. This excludes visited pickups or claimed resource generators
  - `L` looks around from the current tile and places results into a temporary `Look around` scanner category. `K` and `Shift+K` adjust the look radius. Results are sorted clockwise, starting from the north.
- Fix troop rewards being unselectable when your army has no slots, even after you disband a troop to make room
- Fixed an edge case where cycling through your own stacks with `,` would miss some if initiative buffs cause the current stack to act sooner next round
- In combat, abbreviations like enemy ranged troops are only used when the group has two or more stacks
- To help with fighting enemies with the same units as you, the battle timeline now specifically indicates if a troop is an enemy
- The current troop indicator and timeline entries now include the troop's coordinates
- Navigating back to a previously visited column in the army exchange and inventory grids restores focus to the last visited row
- When narrating enemy spellcasting, just say "Enemy" instead of "Enemy wielder"
- In the post-game victory / defeat screen, clear all other screens in the stack to prevent the adventure map from being heard after the game ends
- More spellcast narration fixes
- Condense effects applied to troops when abilities like protect are used, similar to how effects from spells are already being read
- Fixed some help text not reading on defences screen
- The post-game player stats screen is now accessible
- Hide the map editor option in the main menu, since that isn't supported

## V0.4.1

- Fixed adventure map fog handling for explored-but-not-currently-visible entities:
  - Resource generators and other remembered entities are now read on focused tiles and kept in revealed/scanner results while in explored fog.
  - Tooltip-derived details are only exposed when part of the entity is actively visible.
  - Enemy wielder visibility announcements continue to distinguish wielders that are no longer actively visible.

## V0.4.0

- Add support for conquest, challenge and random maps
- Wielders being recruited are now announced. This is mainly for situations where they join via story events
- Fixed resource generating buildings not appearing in the scanner. Results are in a dedicated "Resource generators" category
- Revealed entity readouts:
  - Multi-tile entities are no longer considered as revealed unless they are fully revealed
  - Artifact names are now announced when visible
  - Dead wielders are no longer announced as no longer being visible
  - The revealed buffer is no longer being reset after combat
- Combat narration:
  - Effects being removed from a destroyed troop stack are no longer announced
  - If a spell is cast multiple times on the same tile, that tile is now only announced once
  - Recognize situations where your / enemy melee / ranged troops are affected by effects to avoid announcing each troop individually
  - When moving from a high ground tile to another tile that is on high ground, don't say that the high ground effect was added and then removed
  - Fixed more cases where the casting of the spell was read after narration of its effects
  - Shorten essence gain notifications for units that generate multiple types of essence. "+1 order essence and +1 arcana essence" would now be read as "+1 order, +1 arcana essence"
  - Read wielder essence generation at the start of their turn. Due to game limitations, this works from the second round.
  - Don't announce individual tiles spawning Acid cloudss when casting the acid cloud spell
- Shorten combat influence readouts.

  Before: "in movement range of 59 enemy Plague Rats at 6, 8, zone of control and movement range of 15 enemy Oathbound at 9, 6, movement range of 19 enemy Oathbound at 7, 4, movement range of 5 enemy Spectres at 11.5, 3."

  After: Zone of control: 15 Oathbound at 9, 6. Movement range: 59 Plague Rats at 6, 8, 19 Oathbound at 7, 4, 5 Spectres at 11.5, 3."

- Fixed combat screens not being properly cleaned up if doing a quick battle, followed by a manual battle. This was causing the hotkey to read resources out of combat to fail
- Fixed regression in V0.2.0 that broke reading of marketplace screen button labels
- Fixed placeholder titles being read on story screens when no title was actually visually shown
- Artifact colours should now be read everywhere
- On the adventure map, revealed announcements and reading the focused tile now respect scouting level and no longer reads full artifact names unless the game would actually show it
- Restored reading of some decorative and effect types on tiles. This fixes walls being only read as "impassable"
- Fix missing close button label for options and a reward choice screen
- Support random events popup
- Many translation string fixes

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
