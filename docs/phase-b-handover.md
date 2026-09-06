# Phase B handover

One page. Detail per screen is in the commit messages (`git log 5f4825d..`) and each screen's
doc comment; the rulings are in `ui-graph-plan.md`.

## State (2026-09-06)

All 33 out-of-game screens are graph screens. The invite providers page is ported unmeasured
(it never draws here, one provider) and waits for you. Your first walk's defects are fixed:
chat trap and mod.io typing, slider "provide a number" buttons, description readout, "table",
Enter on any cell, filters first, campaign title twice, browse items' Subscribe and More
options. Still to build: the random maps redesign (K) and the mod options dialogs (J).

## Screens to test, and what to watch for

- **Menu pages** (campaign menu, tales, community campaigns, map type): card order matches
  the picture; Escape returns through the drawn Back; the hovered card stays raised when
  you leave it (known: the adapters have no hover-out).
- **Dialogs** (quit, join with code, delete save, reset statistics, set name, mod.io login):
  buttons read in drawn order (No before Yes on most, Confirm before Cancel on the options
  confirm); Enter inside a dialog's text box submits; Escape is the game's on the confirm and
  popup-menu dialogs and the mod's on the system popup.
- **Loading screen**: arrows read the tip without continuing; Enter on the button continues;
  an unclaimed key (a letter, Space) also continues.
- **Options** and the lobby settings, host game, search filter: Options tabs switch on focus;
  Enter on a slider opens the game's number popup, Left/Right adjust; a combo box opens a
  drop list walked Up/Down landing on the current value; Escape is the game's in Options and
  the game settings, the mod's on the others.
- **Tables** (map select, challenge maps, game list, player statistics): the page lands on
  "Filters"; arriving on a map row selects it and Enter on any cell selects; win conditions
  read one icon at a time; on the game list Enter selects, never joins; statistics tabs
  switch on Enter.
- **Browse pages** (codex, load menu, campaign map select, community maps): the codex is four
  stops with categories as regions; load menu tabs switch on Enter and arriving on a save
  only highlights it; a browse item opens with Right to its Subscribe and More options; the
  results page stays under the home page after Back (known, detector).
- **Lobby**: player rows are a table whose cells are the row's own controls; the invite
  providers popup (two providers needed) reads its buttons then Cancel. **Chat**: Enter sends
  and nothing else is spoken; the chat button is a row of the lobby.

## Decisions you should know

- ES2 wording everywhere: "unavailable", "not checked", "checkbox", "combo box", "tab",
  "radio button", "editable", "table".
- Escape: the game keeps it wherever it registers its own exit action in keyboard mode;
  elsewhere the mod claims it and presses the drawn close control. Measured per screen.
- Text boxes: Enter ends the edit and nothing else, Escape restores; a dialog's box submits,
  the chat box sends. While a game box has the keyboard the mod is silent; a box the game
  focuses on its own is taken back by the mod.
- Drop lists walk Up/Down even where the game draws a strip. Always-drawn descriptions read
  after the label. A refreshed screen keeps its cursor and does not repeat its name.

## Needs your attention

- Unverified with real keys: letters echoing and Escape restoring in the mod.io keyword box
  (the mechanism was checked by injection); the mod.io e-mail login (behind the account
  terms) and five-digit code; the chat's recipient selector (in-game chat, phase C); the
  invite providers popup.
- The game binds a physical Enter on some pages regardless of the cursor: START MISSION on
  the mission page, load on the save menu, Yes on the quit popup.
- Pushed to later phases: the adapters' `Normalize` sweep and the stand-down limit (phase G);
  a map adapter crash on quit to main menu (phase E); community maps item labels keep HTML
  entities; opening Community Maps speaks "unavailable" once before the browser opens.
