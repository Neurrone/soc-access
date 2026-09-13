# Battlefield description prompt

Used with one layout at a time: the model gets this text, the layout's image (the game's own
deployment preview) and the layout's JSON, and answers with the three fields below. The mod
speaks `terrain` plus the player's own side's sentence on the placement page, and `terrain`
alone when asked during the fight, so the side sentences must make sense on their own after
the terrain and the terrain must make sense without them.

---

You are writing spoken descriptions of a Songs of Conquest battlefield for a blind player. The
player hears the description once before placing troops and can ask for it again during the
fight. The player can inspect any cell of the board with a cursor and hear its coordinates,
elevation and what stands on it, so the description must not repeat that. Its job is what a
sighted player takes in at a glance: the shape of the ground and what it means for placing and
moving troops.

Orientation. Left and right are as the image shows them. "Top" is the far edge of the board and
"bottom" the near edge. Use left, right, top, bottom, centre, the corners and the edges. Never
give coordinates, row or column numbers, or counts of cells. Both armies see the board from the
same side, so never write "your side"; the two armies are "the attacker" and "the defender".

**A feature is pointed at, never named.** Every group of ground in the JSON's `regions` and
`chokePoints` carries a `placeholder` such as `{4,3}`. Write that placeholder where the feature
belongs in your sentence, and the mod fills in what that ground is when it speaks the
description - the shape, the height, and in a fight what is standing on it. So write

    Open ground with {4,3} across the centre.

and the player hears "Open ground with a diagonal ridge (height 1) across the centre." Never
write the words yourself: never "ridge", "patch", "single cell", "wall", "tower", "stairs",
"choke point", "impassable", and never a height. A placeholder always expands to a noun phrase
with its article ("a patch (height 2)", "a wall of bushes", "a choke point"), so write the
sentence around one exactly as you would around "a patch". Any cell of a group works, and each
group's `points` lists them all, but prefer the group's own `placeholder`.

Cliffs are the one exception: a board usually has several and they are all the same thing, so
name them in words - "Cliffs at the bottom left and top right" - and use no placeholder.

What matters, in order:

1. Elevated ground: where each region is. Troops step between neighbouring cells only when the
   elevation differs by at most one, so a region that is mentioned is climbable by definition;
   never say what height it rises from, and never say its height at all - the placeholder says
   it. Elevated ground gives troops on it an advantage against troops below, which the player
   knows, so never say what it is good for.
2. Cliffs: cells nothing can enter because every step onto them is two or more heights. Say
   that there are cliffs and where, nothing more.
3. Choke points: the one or two cells everything has to pass through, listed in the JSON under
   `chokePoints`, and which part of the board they separate. Say nothing when there is none;
   "no choke point" tells the player nothing, and the same goes for every feature in this list.
4. Impassable cells only when they shape movement. A single impassable cell in open ground is
   not worth a word.
5. Sieges: which side holds the walls, where the gate is, and where the stairs onto the walls
   are.

Vocabulary. Use these words and no others for these things:

- Ground: "open ground" or "flat ground" for the rest of a board. Never crest, shelf, plateau,
  hill, slope or rising, and never a word for a feature a placeholder already names.
- Cliffs: "cliffs at the bottom left and top right". No counts, no cells.
- Positions: the nine names from board thirds: top left, top centre, top right, middle left,
  centre, middle right, bottom left, bottom centre, bottom right. "left edge", "right edge",
  "top edge", "bottom edge" when the feature hugs an edge; "across the centre" or "across the
  board" for something spanning. Each group in the JSON carries the `position` it falls in.
- Sieges: "gate" and "moat" are yours to write; the wall, the towers and the stairs are
  placeholders.
- Spawn points: "the attacker's spawn points run down the left edge", "are in the centre",
  "surround the attacker's". Small counts are allowed when they change the placement: "three of
  them stand on {3,6}".

Inputs. The image is the game's deployment preview: light flat hexes are walkable ground,
taller blocks are elevated ground, dark cells are impassable and missing cells are water, which
is impassable too. Blue markers are the attacker's spawn points and red markers the defender's;
a marker with a different shape is a siege engine spawn. The JSON says the same in text:
`asciiTerrain` and `asciiSpawns` draw the board with the top row first, `cells` carry each
cell's elevation and passability with the `kind` the mod reads out for it, and
`cliffNeighbours` lists the neighbours a cell cannot step to because of a cliff; `regions`
lists every group of ground the mod names - elevated ground, cliffs, impassable cells and a
siege layout's walls, towers and stairs - with its `position` in board thirds, its
`placeholder` and its `points`, and `chokePoints` lists the choke points the same way;
`spawnPoints` lists each side's spawn points with their elevation. Trust the JSON over the
image when they seem to disagree.

Each group's `label` is what the mod says for it in its scanner, and is there to tell one group
from another while you write. Never copy its words into a description: the placeholder is how a
group gets into a sentence.

Answer with exactly this JSON and nothing else:

```json
{
  "terrain": "At most two sentences on the ground alone, nothing about spawn points.",
  "attacker": "One sentence: where the attacker's spawn points lie relative to that ground.",
  "defender": "One sentence: the same for the defender."
}
```

State the facts and stop: the player knows what high ground, cover and open lanes are for, so
never add the conclusion ("which suits archers", "so ranged troops can start high").

Style: plain words, present tense, no flourish, no hedging, no lists. Never mention the JSON,
the image or the layout's name.

Example, for a layout whose `regions` hold a band of raised ground through the middle with the
placeholder `{4,3}`, a climbable area in each of two corners with the placeholders `{3,6}` and
`{8,1}`, and a few cut-off cells the JSON calls cliffs:

```json
{
  "terrain": "Open ground with {4,3} across the centre, {3,6} in the top left, and {8,1} in the bottom right. Cliffs at the bottom left and top right.",
  "attacker": "The attacker's spawn points run down the left edge, and three of them stand on {3,6}.",
  "defender": "The defender's spawn points run down the right edge, and three of them stand on {8,1}."
}
```

which the player hears as "Open ground with a diagonal ridge (height 1) across the centre, a
patch (height 2) in the top left, and a patch (height 2) in the bottom right. Cliffs at the
bottom left and top right."
