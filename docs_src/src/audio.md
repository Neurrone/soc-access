# Audio

The mod uses sound and speech for different jobs. Sound carries geometry and category: what kind of thing is on a tile, whether it is friendly or hostile, and which direction it lies in. Speech keeps the names, numbers and exact states. A cue always acknowledges that the cursor moved, even when the announcement for that tile is short or silent.

Every cue is listed in the [audio glossary](#the-audio-glossary-and-tuning), where you can play it on demand and change how it sounds.

## Adventure Map Tile Sounds

Landing on a tile plays a short cue for it. This happens on every arrow move, every `Shift+arrow` skip, when you jump to a bookmark or scanner result, and when focus returns to the map grid.

### Terrain

An ordinary tile plays one terrain sound:

- `Road`: a hard tick. Roads, dirt and cobblestone roads, and bridges.
- `Open ground`: a soft scuff. Grass, dirt, farmland, cleared forest and anything the mod does not recognise.
- `Sand`: a duller, longer scuff.
- `Water`: two short blips, the second higher than the first. All water, shallow or deep, including water edges.
- `Trees`: a brief bright rustle.
- `Impassable tile`: a low thud. Mountains and walls, and tiles that are blocked with nothing standing on them.
- `Unexplored tile`: two quiet low pulses with a noticeable gap between them.

### Map Contents

Anything worth naming on a tile plays a gesture instead of the terrain sound, because what is standing there is the more useful fact. The gesture is in two parts: a category sound, then the affiliation marker immediately behind it.

The category sounds are:

- `Wielder`: a short rising horn.
- `Settlement`: a two-note chord. Also used for other interactive buildings and troop dwellings.
- `Resource deposit`: a very high double blip.
- `Pickup`: a single high tick.

The affiliation markers are:

- `Ally`: one clear tone, for anything yours or allied.
- `Enemy`: two quick hard taps.

Neutral things, which most map objects are, play the category sound alone. Silence is the neutral marker, so ally and enemy stay easy to pick out. Speech still names neutral entities normally.

Notes on how the gesture behaves:

- A tile with one of these entities plays only the gesture: no terrain sound, and no thud even if the game flags the tile as impassable.
- A wielder standing on top of a map object sounds as a wielder. Speech still names both.
- A tile whose occupant has no category of its own keeps its terrain sound, followed by the ally or enemy marker if the occupant is not neutral.

### Map Edge

`Map edge` is a short falling two-note bonk. It plays when a move is refused: the cursor is against the edge of the map, or a `Shift+arrow` skip found nothing further in that direction. The hex grids use the same sound when a move is refused there.

## Battlefield Sounds

The combat hex grid and the troop deployment grid play a cue for each hex you land on.

- `Battlefield: empty hex`: a soft tick for an empty, passable hex at ground level.
- `Battlefield: hex, elevation 1`, `Battlefield: hex, elevation 2`, `Battlefield: hex, elevation 3`: the same tick raised one step higher for each level of elevation. Levels above three sound the same as three. An empty raised hex plays only its elevation cue. On an occupied or obstructed hex, the elevation cue plays first and the rest of the hex follows just behind it, so the raised ground stays audible.
- Troops use the same `Ally` and `Enemy` sounds as the adventure map, so affiliation is one sound language everywhere.
- `Battlefield: acting troop`: two high blips after the occupant's sound, marking the stack whose turn it is.
- Obstacles and impassable hexes use `Impassable tile`, the same low thud as impassable terrain.
- `Battlefield: threatened hex`: a falling two-note warning, the one jarring interval in the set. It leads the hex's sounds, and the rest of the hex is heard just after it. It plays only on unoccupied hexes, and fires exactly when speech would call the hex threatened.

## Hearing Things at a Distance

Some sounds describe a tile that is not under the cursor. Those are positioned relative to the cursor in the same way:

- east and west set stereo position
- north and south set pitch
- distance sets volume

Anything too far away to hear plays nothing at all rather than playing silently.

### Sonar Sweep

Press `P` on the adventure map to sweep the area around the cursor. Every entity the [scanner](scanner.md) can see within the look around radius plays its gesture, one after another from west to east, so a few seconds of listening gives you the shape of your surroundings without any speech.

The sweep uses the look around radius, so `K` and `Shift+K` widen and narrow it. Wielders, settlements and other interactive buildings, resource deposits and pickups are included, friendly ones as well as hostile. Beacons, objectives, obstacles, terrain and unexplored regions are left out. Each position pings once, however many scanner results sit on it.

Pings never overlap: a crowded area takes longer to sweep rather than blurring together. Pressing `P` again cancels the sweep in progress and starts a new one. If nothing is nearby, the sweep is silent.

### Scanner Results

Paging through [scanner](scanner.md) results with `Page Up` and `Page Down` plays the sound of the result you land on, positioned relative to the accessibility cursor. Entities play the same gesture the sweep pings them with and the same one you hear when the cursor steps onto them, so what you hear from a distance is what you hear when you get there. Results that are not entities, such as terrain groups, unexplored regions, objectives, obstacles and zones of control, play the sounds of the tile they sit on.

This works on the combat and troop deployment grids too, where the direction is measured across the hex grid.

## The Audio Glossary and Tuning

Press `Ctrl+M` to open mod settings, then choose the `Audio` tab.

`Play tile sound cues` is the master switch for every cue described on this page. Turning it off silences all of them without losing the individual settings.

`Audio glossary` opens a list of every cue, in three groups: terrain, map contents and battlefield. Press `Enter` on a cue to hear it flat and centred with your current settings, which is the quickest way to learn what each sound means.

To change a cue, focus it in the list, then activate the `Configure` button below the list, which names the cue it will configure. The configure screen offers:

- `Enabled`: whether this cue plays at all.
- `Volume`: 0 to 100.
- `Pitch`: -12 to +12 semitones.
- `Duration`: 50% to 200% of the cue's default length.
- `Play`: hear the cue again.
- `Reset to defaults`: restore this cue's original sound.

Every change replays the cue immediately, so you can hold left or right on a slider and listen for the setting you want.

## Bookmark Beacons

Beacons are looping sounds that turn [bookmarks](adventure-map.md#bookmarks) into spatial audio markers. Use `Ctrl+Shift+number` to toggle the beacon for that bookmark slot. Several can run at once.

A beacon's sound changes relative to the currently focused tile, using the same mapping as the cues above: stereo position for east and west, pitch for north and south, and volume for distance.

Beacons are separate from the tile cues, so the master switch and the glossary do not affect them. They play a sound file, `beacon.wav`, in the `BepInEx\config\SongsOfConquestAccess\sounds` folder of your game installation. Replace that file to use a beacon sound of your own.

## Game Sounds

A few interface sounds are the game's own, played by the mod for feedback:

- A click when a list wraps around, moving between widgets with `Tab` and `Shift+Tab`, and when cycling scanner categories, subcategories or results past the end.
- The game's cancel sound when a drag is cancelled or abandoned.
- A click when you move between mod settings tabs, and when you return to the map or combat grid from another screen.

These are not part of the cue system and are not affected by the audio settings.
