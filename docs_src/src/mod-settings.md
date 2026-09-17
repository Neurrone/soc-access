# Mod Settings

Open the mod's settings screen with the `Mod options` button on the main menu or the pause menu.

## Detailed Threat Info

This option is enabled by default. Focusing a tile will indicate if it is within zone of control, attack, deadly or movement range by an enemy troop stack. These are known as threatened tiles. However, this can be very spammy.

Disabling this option shortens the readout for the focus tile to only mention if it is threatened by an enemy. The full details are still available with the `S` key.

## Audio

The Audio tab holds `Play tile sound cues`, the master switch for the mod's sound cues, and the `Audio glossary` button, where every cue can be played and tuned individually. See [Audio](audio.md#the-audio-glossary-and-tuning).

## Sort Scanner Results By

The Adventure map tab's first row, and the order the scanner reads a category's results in. `Straight line distance`, the default, puts them nearest-first as the crow flies.

`Walkable path cost` puts them in the order of the walk instead: each result is ranked by what your wielder would pay to walk there, so a result just across a lake comes after the one the road reaches first. A result an army or something built on the map blocks is ranked by what the walk to it would cost with nothing in the way, so it keeps its place in the list, and only somewhere no walk reaches at all, such as another island, comes last.

Either way the directions read out are the straight line to the result, such as `4 north, 5 east`. A result nothing can reach today is read as `blocked, 4 north, 5 east`, and names the army in the way when there is one you can see: `blocked by A stand of Roots troops, 4 north, 5 east`. Nothing built on the map is ever named, because what a wielder would actually walk around cannot be told from where the route over bare terrain runs.

## Long Directions

Scanner directions are spoken in a short form by default, such as `3ne` for three tiles to the northeast. Enable this option to hear the full wording instead, such as `3 northeast`.

## Custom Categories

`Adventure map custom categories` and `Battle custom categories` open the three numbered scanner categories you define yourself, built from subcategories you pick and keywords you type. Each scanner context keeps its own three. See [Scanner](scanner.md) for what the rules do and how the results are grouped.

Each row reads `Custom category 1`, `Custom category 2` or `Custom category 3` followed by the category's name, or `empty`. Opening one gives it a name, its subcategories and its keywords, and `Clear this custom category` empties it again. On the adventure map, `,`, `.` and `/` walk categories 1, 2 and 3; in combat those keys are the troop cycles, so the battle categories are reached through the category cycle.

## Bookmarks

The Bookmarks tab holds no setting. It is about the file your [adventure map bookmarks](adventure-map.md#bookmarks) are kept in: one file per game and team, under `BepInEx/config/SongsOfConquestAccess/bookmarks`. While a game is open the tab says where that file is, or that no bookmarks are set for this game yet.

`Copy bookmarks to clipboard` puts the file's text on the clipboard exactly as it stands, and appears only when there is a file. `Import bookmarks from clipboard` reads such a text back. The text says which game it belongs to, so it can be pasted on the main menu and for a game other than the one you are playing. Only the slots the pasted text carries are overwritten: a slot you already have that the text does not mention is left where it is. A dialog then says how many bookmarks were imported and whether they were for the game you are playing, and if they were, the map uses them straight away. `Open bookmarks folder` opens the folder in your file manager.

## Help

The Help tab is three buttons, each opening a page in your browser: `Mod homepage` for this documentation, `Join Discord server` for the place to ask questions and report problems, and `Support my work on Patreon`.
