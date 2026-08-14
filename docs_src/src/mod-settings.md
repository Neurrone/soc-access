# Mod Settings

Press `Ctrl+m` to open the mod's settings screen.

## Detailed Threat Info

This option is enabled by default. Focusing a tile will indicate if it is within zone of control, attack, deadly or movement range by an enemy troop stack. These are known as threatened tiles. However, this can be very spammy.

Disabling this option shortens the readout for the focus tile to only mention if it is threatened by an enemy. The full details are still available with the `S` key.

## Audio

The Audio tab controls the short sound cues that accompany cursor movement.

`Play tile sound cues` is the master switch for all of them. Turning it off silences every cue without losing the individual settings.

Scanner results play the sounds of what they land on, positioned relative to the accessibility cursor: stereo pan for east and west, pitch for north and south, and volume for distance. Results too far away to hear play nothing.

Wielders, settlements and other interactive buildings, resource deposits and pickups have their own sounds: `Wielder`, `Settlement`, `Resource deposit` and `Pickup`, listed under map contents in the glossary and tunable like any other cue. A tile holding one of them plays that sound instead of its terrain sound, whether you step onto it, page to it in the scanner or hear it in the [sonar sweep](adventure-map.md).

In combat, an unoccupied hex that an enemy threatens plays a falling two-note warning, `Battlefield: threatened hex`, ahead of the rest of the tile's sounds. It fires exactly when speech would call the hex threatened.

`Audio glossary` opens a list of every cue, grouped by terrain, map contents and battlefield. Press Enter on a cue to hear it with your current settings, which is the quickest way to learn what each sound means. To change a cue, focus it in the list, then activate the `Configure` button below the list.

The configure screen for a cue offers:

- `Enabled`: whether this cue plays at all.
- `Volume`: 0 to 100.
- `Pitch`: -12 to +12 semitones.
- `Duration`: 50% to 200% of the cue's default length.
- `Play`: hear the cue again.
- `Reset to defaults`: restore this cue's original sound.

Every change replays the cue immediately, so you can hold left or right on a slider and listen for the setting you want.
