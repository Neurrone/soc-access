# Autonomous run handover, 2026-09-11

Everything below is committed on `main` (linear, unsigned). Tests: 899 pass. Localization validates.
Only `performance.md` is left untracked, on purpose. The game was closed at the end.

## What landed

Follow-ups to the earlier eight fixes:
- Edit fields (`be8a6ac`): the real cause was mod.io's `InputFieldCoadjutant`, which de-focuses its
  own text box on a standalone build to route to a platform virtual keyboard the game never wires
  up. The mod now disables that component while it drives a mod.io box. Confirmed by you.
- Type-ahead (`1935cc8`): the letter that opens a page (c, v) no longer starts a search on the page
  it opened. Confirmed by you.
- Button names (`93d892c`, `200cc3e`): the wielder-sheet, spellbook, and battle spellbook/end-turn
  buttons take their label from their own tooltip, so the hotkey line is not read twice.
- Category names (`a6d11d5`): creation skips an already-taken default name; a blank name is refused.

New features:
- Options Controls keybinding table (`6a46426`, `339ec24`): the game's 65 rebindable actions read as
  a three-column table (name, chip, "+") grouped by category regions; the mod stands down while the
  game captures a key.
- Mod Keybinds tab (`6d90bd9`, `9f3c5b1`, `36a0e8d`, `36e3052`, `51e5cb5`): a tab in mod settings
  rebinds the mod's own gestures, same table, grouped into six named regions, with per-row and
  reset-all restore. Mod-gesture capture matches the game (no cancel, no timeout). Overrides persist
  under a `[Keybinds]` config section.
- Mod/game conflict warning (`36a0e8d`): warns, both directions, when a rebind crosses the mod/game
  boundary onto a key the other side uses. Warn-and-allow, queued speech, no within-side checks.
- Send-bug-report screen (`65e247d`, `0bb880f`): the reporter wizard is one accessible graph screen;
  the compose form says why Submit is disabled; escape is left to the game.

## Needs you (physical-key tests I could not run)

Windows blocked foreground capture while you were away, so `POST /key` was unusable. These need a
real keyboard, everything else was verified through the dev server:
- Options Controls: on an action's "+" cell, Enter, then press a key; the chip should update and the
  new key should work. Escape during capture binds Escape (clear the row to undo).
- Mod Keybinds tab (Ctrl+M): same rebind round-trip on a mod gesture.
- Bug report: type a description with the keyboard and confirm the echo; confirm Escape closes it.
- The bug report ListSearchResult, ThankYou, and Error windows need real YouTrack results or an
  actual submit, so they were only covered by offline tests. Do not send a test report to the tracker.

## Decisions to check

- Build cost of the two keybinding tables is over the one-millisecond guideline (about 4.2 ms for the
  game Controls table, 3.0 ms for the mod Keybinds tab), inherent to 60-plus-row immediate-mode
  tables on a settings page. Dropping the 40-row Bookmarks group is the main lever if you want it
  lower. Bug-report screen is 0.13 ms.
- `map_secondary_action` keeps its backslash display-name fallback after a rebind, so backslash still
  triggers it on an OEM1 keyboard. Say if the override should drop it.
- `troop_split_*` (Ctrl+digit) are excluded from the Keybinds tab; add a group if you want them.
- Bug-report Loading text is "Sending report..."; search-row reads title, state, bare upvote number.
- One lint allowlist entry was added: `BugReportScreen._lastWindow` as `baseline` (mod-owned state
  for header re-announcement across the wizard's windows). Flagging per the allowlist rule.

## Earlier session

The first eight fixes and the tooltip verbosity note are in `docs/improvements-handover.md`.
