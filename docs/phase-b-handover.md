# Phase B handover

The consolidated record of the phase B ports (every screen outside a running game moved to
the graph engine): what each family's screens became, what was measured that changed the
approved model, what was found and left alone, and the manual test the owner runs with real
keys. Written as the phase progressed on 2026-09-06 while the owner was away; the plan and
its rulings are `ui-graph-plan.md`.

Conventions the manual tests rely on: graph screens use Up/Down, Left/Right, Tab, Home/End,
Alt+Up/Down (regions), Enter, Backspace (ends a search), Backslash (right click), Escape
(only where the screen says it claims it), and letters (type-ahead). "Diff clean" means the
sorted flat dumps before and after the port differ only in the ways listed.

## Family A: menu pages

### CampaignMenuScreen (representative)

Built: two stops. `campaign-cards` holds the four campaign cards and the Tales card sorted
by measured left edge every build, then Community Campaigns; `campaign-header` holds Back
then Options. Screen name is the drawn title; focus starts on the first card; Escape presses
the drawn Back button while it is drawn (the menu registers no keyboard input of its own). A
card reads its label (number, name, subtitle), "button", then its description and its
progress line, the latter watched live because the game fills it in after the page is ready.

Diff: exactly the four campaign card lines changed, the description having moved out of the
label into the status column; nothing missing.

Deviations, each measured: Community Campaigns is drawn in a band below the cards
(rect 493,681,294,87 on another canvas), so it reads last rather than sorted among them; no
separate buffer section, an announcement part already being a buffer line; the Tales and
Community Campaigns cards keep one label because only the campaign cards have named text
fields.

Engine rule the port exposed (commit `ec87b75`): when a page hides controls as it closes and
the focused one vanishes, the navigator's recovery onto a survivor is silent while the screen
reports itself unworkable; the campaign menu is unworkable while its header band is gone.

Follow-ups, not fixed: `CampaignButtonAdapter.FocusNative` hovers a card in but nothing hovers
it out, so the previous card stays raised (pre-existing); the same adapter composes
"Campaign N" in English (pre-existing, against the adapter rule); the campaign menu was once
seen twice on the stack after returning from a mission page, not reproduced.

Manual test:

1. From the main menu open Campaigns & Tales. Hear "Choose Campaign or Tale", then the first
   card with its description, then "Completed: 4 / 4 missions" a moment later.
2. Down through the five cards and Community Campaigns; Home and End; a letter starts a
   search over the card names, Backspace ends it.
3. Tab to the header: "Main Menu, button, 1 of 2"; Down: "Options, button, 2 of 2"; Shift+Tab
   back to the card you left.
4. Escape: the main menu returns with no stray card name spoken first.
5. Enter on a card opens its mission page (still a widget screen); its Back returns.
6. Watch the picture: the card under the cursor should look hovered; note that the previous
   one stays raised (follow-up above).

### TaleSelectScreen

Built: two stops, exactly as the representative. `tale-cards` holds the seven tale cards
sorted by measured left edge every build (2026-09-06 at 1280x800: x 113, 355, 586, 827,
1064, 1300, 1536 inside `Canvas` > `TaleSelectMenu` > `Scroll View`, a band wider than the
window); `tale-header` holds the main menu's shared band, Back (x 21) then Options (x 1233).
Screen name is the drawn title ("Choose Campaign or Tale"); focus starts on the first card.
A card reads its name, "button", then its description and its progress line, the progress
watched live for the same reason the campaign cards' is.

Escape: neither `TaleButton` nor `TaleButtonLayoutCoordinator` registers an input callback
at all, so the screen claims Back and presses the drawn Back button. Verified: `ui_back`
answered `consumed` and the campaign menu returned with nothing stray spoken.

Diff: exactly the seven card lines changed, the description having moved out of the label
into the status column; Back and Options unchanged; nothing missing.

Deviations, measured: `TaleButtonAdapter` gained `GetDescription()` and its `GetLabel()`
dropped the description, the same split `CampaignButtonAdapter` took. The description is
empty while a card draws its purchase state, because the card's `_ownedContainer` (which
holds `DescriptionText`) is switched off then and the purchase text is what the card says
instead - which is what the old label did too. No details section beside the parts: an
announcement part is a buffer line already.

Follow-ups, not fixed: `TaleButtonAdapter.FocusNative` hovers a card in but nothing hovers
it out, so the previous card stays raised (the same pre-existing fault the campaign menu
has); the seventh card is drawn off-screen at x 1536 and nothing scrolls it into view when
focus lands on it (scroll-into-view arrives with family D).

Manual test:

1. Campaign menu, Down to the Tales card, Enter. Hear "Choose Campaign or Tale", then
   "Tales of Wonder, button", its description, then "Completed: 0 / 2 missions".
2. Down through all seven cards; Home and End; a letter searches the card names, Backspace
   ends the search.
3. Tab: "Back, button, 1 of 2"; Down: "Options, button, 2 of 2"; Shift+Tab returns to the
   card you left.
4. Enter on a card opens its mission page (still a widget screen); its Back returns.
5. Escape: the campaign menu returns with no stray card name spoken first.

### CustomCampaignSelectScreen

Built: two stops. `custom-campaign-cards` holds the community campaign entries and the
download tip in one band, sorted by measured left edge every build (2026-09-06 at 1280x800:
x 74, 362, 650 and 939 inside `Canvas` > `Menu` > `Scroll View`, the tip drawn as the
rightmost card); `custom-campaign-header` holds Back (x 21) then Options (x 1233). Screen
name is the drawn title ("Community Campaigns"); focus starts on the first entry. An entry
reads its title, "button", its description, then its status line - the card's own button text
("DOWNLOAD CAMPAIGN") and, while a download runs, the installation line the card draws over
itself - watched live.

Escape: `CustomCampaignSelectMenuBehavior` registers no input callback at all, so the screen
claims Back and presses the drawn Back button. Verified: `ui_back` answered `consumed` and
the campaign menu returned.

Diff: every before line is accounted for. The entry cards' descriptions moved out of the
label into the status column (the before labels held an embedded newline, which is why those
lines wrap in the raw file). The tip changed more: it was labelled with its sentence AND its
button text, and is now labelled "Find More" with the sentence reading after the label, so
its buffer reads button-then-sentence rather than sentence-then-button. Nothing missing.

Deviations, measured: the tip is drawn as the fourth card of the same band, not as a control
below it, so it is declared in the cards stop in its drawn place rather than after them; the
tip has no title text of its own (only `DescriptionText` and a `Button`, measured), which is
why it takes the button's text as its label while an entry takes its title;
`AnnounceStatusChanged` is now empty - the detector still calls it, and the live-watched
status part says what it used to say by hand.

Follow-ups, not fixed: after a hot reload the resync builds the adapter with a null
behaviour (`TryBuildActiveScreen`), which cannot find `_downloadTip`, so the Find More card
disappears until the page is reopened (pre-existing, seen as "1 of 3" against "1 of 4");
`CustomCampaignEntryAdapter.FocusNative` selects a card but nothing deselects the previous
one, the same fault the campaign menu has.

Manual test:

1. Campaign menu, End, Enter on Community Campaigns. Hear "Community Campaigns", then
   "Flame And Shadow, button", its description, then "DOWNLOAD CAMPAIGN".
2. Down through the three entries to "Find More, button, If you're looking for more campaigns
   please browse the user created mods."; Home and End; a letter searches the titles.
3. Tab: "Back, button, 1 of 2"; Down: "Options, button, 2 of 2"; Enter opens the options
   window and its Close returns you to Options.
4. Escape: the campaign menu returns.
5. With a download running, stand on that card: the changing installation line should be
   spoken as it changes, without re-reading the whole card.

### AdventureLobbyMapTypeScreen

Built: two stops. `map-type-cards` holds the map-type cards sorted by measured left edge
every build (2026-09-06 at 1280x800: x 221, 510 and 799 inside `MapTypeMenu` > `Container`,
Conquest maps, Challenge maps, Random maps); `map-type-header` holds Back ("Main Menu", x 21,
on the lobby scene's own header) then Options (x 1233, on the main menu's `UtilityButtons`).
Screen name is the drawn title ("Map type"); focus starts on the first card. A card reads its
name and sub-header, "button", then its description.

Escape: neither `MapTypeMenu` nor `LobbyNavigation` registers an input callback -
`LobbyNavigation` only subscribes to the sub-menus' own `OnCancel` events - so the screen
claims Back and presses the drawn Back button. Verified: `ui_back` answered `consumed` and the
main menu came back through the scene change with nothing stray spoken.

Diff: exactly the three card lines changed, the description having moved out of the label
into the status column (and into a buffer line of its own); Main Menu and Options unchanged;
nothing missing.

Deviations, measured: the adapter declares Challenge last while the page draws it in the
middle, so the drawn-order sort is doing real work here rather than confirming the
declaration; the card's label is name then sub-header ("Conquest maps. Handcrafted maps")
although the page draws the sub-header above the name, which is the order the widget screen
already read and the order that names the card first. `MapTypeMenuButtonAdapter` became
public and gained `GetDescription()`, and its `BuildLabel()` dropped the third text part; the
three card properties are typed as it rather than as `IMenuButtonAdapter`.

Follow-ups, not fixed: nothing hovers a card out again, so the previously focused card stays
raised (the family's shared fault); the online variant of the page (two cards) was not opened
in this session, and is served by the same build only because the cards are read off what is
drawn.

Manual test:

1. Main menu, Conquest. Hear "Map type", then "Conquest maps. Handcrafted maps, button",
   then its description.
2. Down through the three cards; Home and End; a letter searches the card names.
3. Tab: "Main Menu, button, 1 of 2"; Down: "Options, button, 2 of 2"; Shift+Tab returns.
4. Enter on Conquest maps opens the map select page (still a widget screen); its Back returns.
5. Escape: the main menu returns after the scene change, with nothing stray spoken.

## Family B: dialogs

### MessageDialogScreen (representative; seven sources, three verified out of game)

Built (commits `9435d39`, `7a53562`, `4669ed8`): one stop in ES2's three-part shape. The
heading is a text node first and is also the screen name; focus starts on the body text (or
on the edit field when a source draws no body, as the system popup does); then the edit
field when the source has one; then the buttons in drawn x order, read live each build.
`IMessageDialogAdapter` gained `ButtonOf(action)` and `GameHandlesEscape`. Escape: the confirm
popup, the popup menu, the map message popup and the random event menu register the game's
own ExitMenu in keyboard mode and keep the key; the system popup and the custom message
menu register nothing, so there the mod claims Escape and presses the negative button.

The edit field arrived with this screen: `ControlTypes.EditField` ("editable"),
`GraphNodes.EditField`, the stand-down in `input/GameTextFocus.cs` (the whole input layer
goes quiet while a game text field has the keyboard, the dev server's `/input` answers
"standing down"), and `ui/GameTextEditor.cs` (Enter on the node hands the field the
keyboard once Enter is released, says "editing", echoes typing through
`TextInputEchoHelper`, and on the way out says "edited" with the new text or "Cancelled").
A dialog's field keeps the game's submit on Enter (owner ruling). Four `ModString`s were
added and translated in all thirteen `.po` files.

Diffs: the heading is a line of its own on every source; "unavailable" replaces "disabled"
on the join popup's greyed Join; on Set Name the field's value moved from the label into the
status column. Nothing missing. Screenshots matched on all four dialogs, button order
included (No then Yes; tick then cross).

Deviations, measured: each synthetic node (heading, body) needs its own subject object or
the reconciler seats the cursor on the wrong one; the edit node's value is not watched live
(a cancel would re-speak the restored text); the end of an edit is read off TMP's
`isFocused`, since the wrapper's `Focused` also answers true for the gamepad latch; the
body's buffer section only fires for a multi-line body.

Follow-ups, not fixed: `POST /type` bypasses the router's stand-down (no physical key can);
every dialog adapter normalises the body with `SpeechTextSanitizer.Normalize`, so a
multi-paragraph body reads as one line; the crash dialog capture (`-popup-error`) has no
reachable route and was not diffed.

Manual test:

1. Main menu, Multiplayer group (Right), Join with game code. Hear "Join game", then the body.
   Down: the field ("editable"), then Cancel, then Join ("unavailable").
2. Enter on the field: "editing". Type: each character spoken. Backspace deletes and speaks
   the deleted character (it does not end a search). Left/Right walk the caret and read the
   character under it. Nothing else is spoken meanwhile.
3. Escape inside the field: the text is restored and "Cancelled" is spoken; the mod's keys
   work again.
4. Type a code and Enter inside the field: the dialog submits (Join) with nothing spoken
   about the edit.
5. Letters on the dialog while not editing start a type-ahead search; while editing never.
6. Escape on the options "Are you sure?" confirm and on the load menu's Delete Save popup: the
   game's own No; Escape on a lobby's Set Name popup: the mod presses Cancel.
7. Confirm the handover waits for Enter to be released: hold Enter on the field node and
   release; the field must not submit the dialog.

### QuitToDesktopPopupScreen

Built: one stop in the family's three-part shape, with the popup's extra block read where it is
drawn. Measured 2026-09-06 at 1280x800 inside `QuitToDesktopPopup(Clone)` > `Container`: the
follow-us text at y 286, the FOLLOW button at y 356, the heading "Quit to Desktop" at y 418,
the body "Are you sure?" at y 438, No at x 508 and Yes at x 647. So the reading order is the
follow text, FOLLOW, the heading, the body, No, Yes; the heading is also the screen name; focus
starts on the body; the buttons are sorted by drawn left edge. The screenshot matches, button
order included.

Escape: the game's, and this CORRECTS the brief. `QuitToDesktopPopup.Show` registers
`UI.ExitMenu` on `HandleCancelClicked` in its NON-gamepad branch (lines 145 to 152), with
`UI.Confirm` on `HandleConfirmClicked` beside it; only the gamepad branch is the one-callback
branch. So the key already presses No and the screen leaves it alone. Verified: `ui_back`
answered `unclaimed`.

Diff: one line added, the heading, which the widget tree spoke only as the container's name.
Nothing missing, nothing changed.

Deviations, measured: the follow text, the heading and the body each get a marker subject of
their own, the rule the representative established; the FOLLOW button and the two answer
buttons are keyed on the components the adapter now hands out (`ConfirmButton`, `CancelButton`,
`SteamFollowButton`, added as `Component` so a screen can read where they are drawn).

Follow-ups, not fixed: in keyboard mode the popup also binds the game's own Confirm to YES, so
a physical Enter may reach the game and quit whatever the cursor stands on - the harness cannot
press a physical key, so this is the first thing to check by hand; the FOLLOW button opens a
Steam page and was not activated here.

Manual test:

1. Main menu, End, Enter on Quit. Hear "Quit to Desktop", then "Are you sure?".
2. Home: "Follow for more news from Lavapotion!"; Down: "FOLLOW, button"; Down: "Quit to
   Desktop"; Down: "Are you sure?"; Down: "No, button"; Down: "Yes, button".
3. CAREFULLY, on the body rather than on Yes: press Enter and confirm the game does not quit
   (the popup binds its own Confirm to Yes in keyboard mode - follow-up above).
4. Escape: the game closes the popup itself, as No would.
5. Enter on No: the popup closes and the main menu reads again.
