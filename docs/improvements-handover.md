# Improvements handover, 2026-09-10

What landed (commits 3033abf..c048a6c), what to watch for on your walk, what needs you, and the
keybinding design that was researched but not built.

## Landed

- Edit fields (3033abf): a letter typed in the frame Enter comes up is dropped, never searched
  with; the editing state is the stand-down's first answer. Both mod.io boxes verified with real
  keys. Walk: type your email at your own speed; the first letter after Enter must land.
- Taken category name (3568dea): a nested "Name already in use" dialog with OK over the editor.
- Tooltips (59392df): every tooltip is read on focus except the thirteen troop and wielder
  dossier classes, which the new General-tab checkbox "Read long tooltips" gates.
- Usage hints (c151709): "Read usage hints", stored as `always`/`never` for a later third value.
- Map tiles (306991d): a settlement or a mine silences the terrain and road under it.
- Loading screen (7716344): active for the whole load; tip first, Down reads the percentage.
- Performance (78b1866..c048a6c): trading 1.42 -> 0.51 ms, wielder sheet 0.70 -> 0.18, artifact
  market 0.76 -> 0.21; equipment is read off the drawn cell, not out of every artifact.

## Watch for

- Verbosity: the map HUD and wielder sheet now speak every artifact, stat and resource tooltip on
  focus. Near-duplicates survive where a tooltip's first line almost repeats the label ("Wielder
  Sheet, button, Wielder Sheet (C)"); a per-line dedupe would need an engine change.
- The first nine seconds of a load are silent: scene teardown, then the game's first loading
  definition draws only a spinner. The screen has no name; a one-line `ScreenName` would mark it.
- The echo after a real Enter on the mod.io email box said "b, b" for "ab" at some timings while
  the collection box echoed "a, b"; the text was right both times. Not chased.
- A troop row's readout speaks its first hint only; the buffer holds both. Pre-existing.

## Needs you

- Creation still mints a duplicate default name after a delete ("Custom 3" twice); a blank name
  confirms silently. Both predate the window move.
- The mini menu opened by `next_settlement` on a visited settlement answers `ui_back` unclaimed.
- `GET /gui/unity` reports a `UITextMesh` the game cleared as still holding its prefab text.
- Trading's remaining 0.43 ms is its two wielder bands; `TroopHudRows.AddRow` composes each
  occupied row's details eagerly. A troop-slot snapshot was measured and costs more than it saves.

## Keybindings: how the game draws them, and a design

The Controls tab draws one `UIKeyBinding` row per overrideable action through
`MenuFactoryController.AddKeyBinding`, which `MenuRows.Read` never asks for, so the rows are
absent today and the category captions head nothing. A row is a label, chips for the current
binding (a chip click removes the override), and a plus button that starts capture. Capture is
Unity's interactive rebind over the keyboard (`UnityInputManager.AddOverride`): no cancel key
(Escape gets bound), no timeout, ends on the first key, and the router would act on that key
too, because its stand-down asks only about text fields. The only native "capturing" signal is
the button-less confirm popup the instructions use, which has exactly one caller.

Design, following Endless Space 2 Access: `MenuRows` gains a key-binding row reading the action
name, the chip text, whether an override exists, and native `Rebind`/`RemoveOverride` through the
row's own buttons; a `KeyCaptureFocus.IsCapturing()` beside `GameTextFocus` reads the button-less
popup and joins the router's stand-down; one button node per row with a live value part so the
new key is spoken when the game redraws the chip, Enter to rebind, a new `ui_clear` action on the
engine's unused `OnClear` to restore the default, and one mod string saying the next key pressed
becomes the binding. Reset-all already works through the existing button and message dialog.
Unknowns for in-game checks: whether any action's expected control type gives it an Escape
cancel, the row prefab's selectable, the private field names, the popup wording, and the build
cost of forty-odd rows.
