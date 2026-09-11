# Review fixes: handover

Fixes from `code-review.md` (untracked, repo root), 2026-09-11 to 2026-09-12, one logical commit
each on `main` from 0a6d953 (about 200 commits). Detail for another session is in
`docs/review-resume.md`. The game was closed at the end of the run.

## Know first

- Your decisions were applied as given: map select is the reference for the challenge page;
  settlement troops follow the defence menu; HUD groups need alpha, interactable and
  blocksRaycasts everywhere (two battle HUD questions got stricter, verified in-game); button
  visible and enabled consolidated with every site's answer unchanged; one shared .po parser;
  mod.io's Downloads word; empty fallbacks for verified game keys, ModStrings for text with no
  game source; node ids minted in screens; combat speech composed in the combat screen and the
  narrator's diagnostics deleted; unreferenced members deleted; catches removed with evidence or
  made to log once; `ui/graph/` untouched; game closed when done.
- Every screen's accessible graph was diffed against a 65-screen baseline after round two and
  again at the end; differences are only the intended ones. The final pass found two regressions
  (a blank chat Send label, the rally point screen still outliving its destroyed building); both
  are fixed and verified in-game (08b46c4, cb0219e).
- Build times: challenge map select 1.42 to 0.5 ms, spellbook 1.20 to 0.61, keybinds 2.37 to
  1.7, and a dozen menus under 0.1 ms; `performance.md` has the table. Keybinds, map select and
  the Controls tab stay over 1 ms on engine cost (`ui/graph`, not touched).
- Spoken English changed in five places, each its own commit: bookmark keybind rows (were raw
  identifiers), challenge map preview (win conditions in the buffer, not spoken), a bacteria
  name in a damage line keeps its capital, a spell with no game name says "spell", modifier
  names come from the game's own keys.
- Findings that were wrong, left alone: "enemy" dropped from influence lists is deliberate; the
  two player-stats labels share one caption; `RecruitGroups.CoarseStep` is 5; a filter option's
  `IsVisible` is right; the bug report's sixth slider stop is reachable; the native commander
  `Get(id)` returns destroyed commanders, so the linear walk stays (off the per-frame path now).
- `git rerere` silently mis-resolved one translation merge; it is now off in this repository's
  config. Turn it back on if you rely on it.

## Needs your verdict

- `audio/synth/NoisePool.cs` is production-dead but its tests keep it alive: delete both or keep.
- `InventorySlotReader` and any game `IDetails` type cannot load in the net472 test host
  (default interface methods), so the three artifact-slot readers stay untested offline.
- `dev/UnityDump` keeps eight empty catches (one per dumped component); left as is.
- Golden-graph tests through the dev server: the baseline routes are replayable and would have
  caught the fork drift; recommended follow-up, not built.
- Two spoken-text edge cases named in commits, harmless in play: a blank purchase tooltip line
  no longer yields ". "; a gift/mini-menu title with a blank first name still starts with a
  separator.

## Screens to test

- Combat: cursor tiles and cues, Inspect on a troop and an unreachable tile, spell and ability
  targeting (selected/unselected wording), essence rows, battle log, timeline, options button and
  the AI row on the HUD, the cursor square gone after the battle.
- Troop placement: walk, scanner jump/return, troop labels; two battles in a row.
- Adventure map: route stepping, scanner zone-of-control and terrain groups, sonar sweep, HUD
  experience and resource rows, objectives, kingdom band, teleport menu, tile click hints.
- Settlement pages: build menu costs and header, purchase wielder (silent arrival on the shown
  candidate), marketplace, research, defences; rally point and town menus close when their
  building is gone.
- Wielder sheet, trade, artifact market: slots, equip/unequip, auto-arrange, buy/sell, drag.
- Options and mod options (Controls and Keybinds rows and tooltips; bookmark rows translated),
  save/load order, codex, main-menu foldouts (unfold sound now plays), campaign cards and map
  select (disabled Start Mission reads disabled), community maps pages, lobby settings and
  player settings, chat button wording, dropdowns anywhere.
