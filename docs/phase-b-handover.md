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

### CommunityMapsModalScreen (one variant verified, three blocked)

Built: one stop in the family's three-part shape. The first line the modal draws is its heading
- a text node of its own AND the screen name - then what it has to say, where focus starts,
then the field where the modal has one, then the buttons sorted by drawn left edge. Measured
2026-09-06 at 1280x800 on the authentication modal (`Authentication Popup` > `Main Panel`):
heading "Authentication" at y 315, its paragraph at y 403, Back at x 373, Connect with Steam at
x 509, Connect with email at x 721. The screenshot matches.

Escape: claimed. The game's host of the browser (`LavapotionModIOBrowserUtilityBehaviour`)
registers no input callback at all, and mod.io's own cancel (`InputReceiver.OnCancel`, calling
`Navigating.Cancel()`) is reached only from its Input System actions, which this build does not
bind. The screen claims Back and runs mod.io's `Navigating.Cancel` through the adapter, rather
than picking a drawn button: that native path already knows what each modal state closes to
(the code panel's own Cancel, the context menu's Close). Verified: `ui_back` answered
`consumed` and the modal closed; the drawn Back button does the same thing when activated.

Diff: one line added, the heading, which the widget tree spoke only as the container's name.
Nothing missing, nothing changed.

Deviations, measured: the text nodes get marker subjects kept in a small per-screen map, so a
rebuild seats the cursor on the same line; the widget's signature polling and its retained-tree
`Refresh()` are gone, since the graph is declared afresh every operation - `Refresh()` stays as
an empty method because the detector calls it, and `State` stays a constructor snapshot because
the detector compares it against a freshly read one to tell a CHANGED modal from a changed
panel; the widget's separate context-menu branch is gone, the same actions being declared the
same way whatever the state is; on the download queue the heading is now the heading node
rather than also a body line.

BLOCKED, declared but never opened here (the account authenticates through Steam, so the e-mail
login is unreachable):

- The e-mail box (`AuthenticationPanels.AuthenticationPanelEmailField`, a bare
  `TMP_InputField`): an edit field driven by `GameTextEditor`. `OpenPanel_Email` (decompiled
  lines 640 to 685) wires no `onSubmit` or `onEndEdit` on it, so Enter ends the edit and does
  nothing else - the plain ES2 contract, without the dialog exception. `GameTextEditor` grew a
  `Request(TMP_InputField)` and a private `Field` that speaks to either toolkit; the game-field
  path is unchanged and was re-verified on the join-game popup afterwards ("editing", the
  stand-down answering `standing down`, then "Cancelled").
- The five-digit code: ONE node, not five. `KeyInput5Digits` reads the keyboard in its own
  `Update` while the panel is drawn, keeps one string and one index, and `KeyInput5DigitsUi.Open`
  clears the event system's selection; the five boxes are `TMP_Text`s it renders that string
  into, with nothing to focus and no way to move between them but typing and Backspace. The
  screen speaks only the character that changed (the widget's own behaviour, ported) and turns
  type-ahead off while that panel is up, because the letters typed there are the code.
- The context menu, the notification, the confirm-uninstall and the download queue: declared by
  the same code, never opened here.

Follow-ups, not fixed: `CommunityMapsModalAdapter.GetActions()` returns an empty list for the
ContextMenu state, so the context menu has never had any rows to read - pre-existing, and
unchanged by this port; `POST /type` still bypasses the stand-down (seen again here, the
type-ahead search ran while the game held the keyboard).

Manual test:

1. Main menu, Community Maps. Hear "Authentication", then "mod.io is a 3rd party utility...".
2. Home: the heading; Down through the paragraph, Back, Connect with Steam, Connect with email.
3. Escape: the modal closes and the community maps home reads again.
4. Reopen it and press Enter on Back: the same.
5. If you ever sign in by e-mail: the e-mail box should say "editable", Enter should say
   "editing", typing should echo, Enter should end the edit without sending; the code panel
   should read one control, speak each character as it is typed, and Enter should continue.

### PlatformUserMenuScreen

Built: one stop. The heading is a text node and the screen name, then the action rows, then a
Cancel. Focus starts on the first action, there being no body to start on. Measured 2026-09-06
at 1280x800 under `PlatformUserMenu`: a `PlatformUserMainContainer` at [141,122,163,41] holding
one `PlatformUserButtonEntry` per action ("Set Name" here) over a full-screen `UIBlocker`, and
nothing else. The screenshot confirms it: no heading and no Cancel are drawn.

Escape: claimed. `PlatformUserMenu.Show` registers `UI.Cancel` on `Hide` (line 271) and nothing
else, and `UI.Cancel` is this game's GAMEPAD binding throughout - every keyboard branch
registers `UI.ExitMenu` instead (`ConfirmPopup.Show` lines 182 to 208 and
`QuitToDesktopPopup.Show` lines 145 to 152 both show the pair). So Escape does nothing here and
the screen takes it, running the same `Hide` the game's own callback would. Verified: `ui_back`
answered `consumed` and the popup closed.

Diff: one line added, the heading, which the widget tree spoke only as the container's name.
Nothing missing, nothing changed.

Deviations, measured: the heading is NOT drawn - it is the menu's own localized name
(`Lobby/LobbyPlayerMenu/ShowPlayerActions`), which the widget screen also spoke, and it is
declared as a node because the family's contract puts the heading in the readout as a line of
its own; the Cancel is the mod's own control for the same reason it was in the widget tree,
the popup drawing no way out and the game's Cancel being bound to the gamepad only. Both take
marker subjects. The action rows are `DrawnNode`s keyed on the row the game draws, for which
`ActionItem` gained an `Entry` component.

Follow-ups, not fixed: closing the Set Name popup with Escape closes the player-actions popup
behind it as well, so the cursor lands back in the lobby rather than on Set Name - that is the
game hiding the menu once the action finishes, not the mod.

Manual test:

1. Conquest, a Conquest map, Confirm; in the lobby, the player row's Show Player Actions.
   Hear "Show Player Actions", then "Set Name, button, 2 of 3".
2. Home: the heading; End: "Cancel, button, 3 of 3".
3. Enter on Set Name: the system popup opens with its field ("Set Name, editable, <name>");
   Escape there restores the name and closes both popups.
4. Escape on the popup itself, and Enter on its Cancel: both close it.

## Family C: loading screen

### LoadingCompleteScreen (representative; no siblings)

Built: one stop with two rows in the order the page draws them. Measured 2026-09-06 at
1280x800 on the cropped screenshot: the tip line ("Tip: Is an enemy blocking your path in
battle? Hold Ctrl to target the ground instead") above "PRESS ANY KEY TO CONTINUE". The tip
is a read-only text node and the start node, so arrival reads it; the prompt is the button,
and activating it runs the game's own `FinalizeLoadingScreen` through
`LoadingScreenAdapter.Continue()` - the same member `DevProbe.PressContinue` already used,
so the probe and the screen share one call. No screen name: the two rows say where the
player is, and a name over a page whose whole purpose is to be dismissed is a line in the
way.

Diff: one line added, the tip, which the widget screen never read. Nothing missing, nothing
changed. (The stack line differs too because the after-capture was taken with the map
already resynced underneath.)

Keys, per the owner's ruling: the arrows are the mod's, so the two lines can be read;
`AllowsTypeahead` is false so a letter is one of the keys the game is waiting for; Escape is
left to the game (`ConsumesBack` false). Enter is the one key the navigator always claims, so
Enter on the tip row does nothing rather than continuing - the button row is where Enter
continues.

Deviations, measured: the tip is read as the LAID-OUT text (`TMP_Text.GetParsedText`), not as
the string the menu assigned. A tip carries the game's own action tokens - the assigned string
here was "Hold <action name=ToggleHexTargetingMode> to target the ground instead" - which
`UITextMesh.UpdateText` rewrites into the bound key as it draws, and the picture reads "Hold
Ctrl". `UITextMeshTextUtility.GetEffectiveText` answers with the pre-substitution string,
which is why this one label does not go through it. Both rows are `DrawnNode`s on the labels
the page paints (`TipLabel`, `PromptLabel`, new on the adapter), with marker subjects only as
the fallback when the game has no label to give.

Follow-ups, not fixed: `LoadingScreenAdapter.PromptText` still normalises through
`SpeechTextSanitizer.Normalize` (pre-existing, and against the repo's standing rule); every
other adapter that reads a `UITextMesh` through `GetEffectiveText` has the same action-token
gap this screen's tip just fixed for itself, so any label carrying `<action name=...>`
elsewhere is being read as the raw token.

Manual test:

1. Load a save from the main menu and wait for "press any key to continue". Hear the tip.
2. Down: "PRESS ANY KEY TO CONTINUE, button, 2 of 2". Up: the tip again.
3. Any letter, Space or Escape should continue the load, because the mod does not claim them.
4. Enter on the button continues the load; Enter on the tip row does nothing.

## Family D: settings forms

### OptionsScreen (representative)

Built: three stops. `options-tabs` holds the seven category tabs, `options-rows` the settings
of the category showing, `options-buttons` the OK button (labelled "Close", as the widget
screen labelled it). Screen name is the mod's "Options"; focus starts on the tab bar's
SELECTED tab, so arrival reads "Options", then "Gameplay, tab, selected, 1 of 7".

Measured 2026-09-06 at 1280x800 through `/gui/unity`: the window at [270,141,741,519], the
tab column at x 270 (seven `OptionsTabButtonWithText(Clone)` 30 px apart), the content panel
`ContentScrollEntry` at [489,218,500,406] over 904 px of rows, each row drawing its label at
x 496 and its control at x 707 to 968. A slider draws its value in a `PercentText` with an
`EditButton` over it at [930,347,38,21]; a dropdown draws the current option in a
`DropdownElement` label; captions ("General" at y 221, "Battle" at y 421, "Adventure" at
y 687) head groups of rows.

The tab bar switches ON FOCUS, as ES2's does (the switch is instant and costs nothing, so
arriving at a tab and arriving at its page are one event); Enter switches too. A caption is
the REGION its rows belong to and not a row of its own - Alt+Up and Alt+Down jump between
them and the name is spoken on the way in ("General, Language, combo box, English, 1 of 5").
Scroll-into-view is the game's: every row's focus visual is the adapter's `Focus`, which
selects the row natively, and the panel's own `AutoScrollToSelected` does the rest. Verified:
with the cursor on Reset player statistics, the content had scrolled to y -280 and the row's
rect [496,587,471,36] sat inside the panel's [489,218,500,406].

Escape is the game's (`ConsumesBack` false): `OptionsMenu.ReregisterInput` registers
`UI.ExitMenu` on its keyboard branch (decompiled lines 532 to 544). The old screen's own
Cancel handling is gone with it.

Diff, all seven tabs, and every difference is one of five:

1. "unchecked" became "not checked" (the phase B word).
2. The showing tab now reads "selected".
3. A dropdown's OPTIONS are gone from the page - they are the drop list screen now - and each
   dropdown has become one labelled line with its current value ("Language | English",
   "Battle log | Always hide", "End turn behaviour | Instantly end the turn (default)",
   "Resolution", "Max Framerate Limit", "Energy Saving Framerate", "Graphics quality",
   "UI & Font Size"). The widget screen never read a dropdown's NAME at all, only its options.
4. A caption that heads rows became a region and so left the flat dump: "General", "Battle",
   "Adventure" on Gameplay, "Zoom settings" and "Keybindings" on Controls, "Send Bug Report"
   on Report Issue.
5. Each slider gained a "Please provide a number" child row.

Two differences on Video are the machine, not the port: the game was relaunched windowed
during this session, so Fullscreen reads "not checked" and the resolution list is the
window's, where the before-capture was taken fullscreen at 1920 x 1200.

Deviations, measured:

- A caption with NO rows under it stays a read-only row rather than becoming an empty region.
  That is the whole Controls page below Keybindings: its key binding rows are drawn by
  `MenuFactoryController.AddKeyBinding`, which the adapter does not read, so "Adventure",
  "Build Menu", "Kingdom Overview", "Split Troop Size", "Battle", "Common", "Map Editor" and
  "UI" head nothing at all. Making every caption a region would have silently dropped those
  eight lines.
- The slider's value box is a CHILD of the slider row, declared always open and reached with
  Down: Left and Right belong to the value, so they cannot open the group, and the group says
  nothing about being expanded (`SpeaksOwnExpansion`) because there is no closing it.
- The value box is opened by running the delegate the slider itself installed
  (`UISlider.OnEnable` puts `HandleTextClicked` on the button's `OnClickedInside`, which
  `UITransform.Update` raises from a real mouse press INSIDE the box). A synthesized pointer
  click reaches `UIButton.OnClicked`, which is null here, and does nothing - so the brief's
  `NativeSelectionUtility.Click` would have been silent. Verified: Enter on the child row
  opened the game's own "Please provide a number" popup as `MessageDialogScreen`, and Escape
  there returned the cursor to the child row with the value unchanged.
- The child row's label is the game's own prompt (`Common/ProvideNumber`, the key
  `UISlider.HandleTextClicked` passes to `ISystemPopups.AskForInput`), read through the
  adapter and empty for a slider that draws no such box.
- One coarse step is ten fine steps (the item's own step), as in ES2.

Follow-ups, not fixed:

- A tooltip line reaches the graph buffer RAW, where the widget engine normalised it: on
  Report Issue the Report new issue button's tooltip is now one buffer line reading
  "Report Bug", a newline, then the highlight markup around "Press B three times (B+B+B) for
  better screenshot", against three clean lines before. This is the graph engine's
  `NodeSection` against `UIManager.BuildReviewLines`, not this screen's, and it will show on
  every ported screen whose game tooltip carries markup. The widget engine's cleaner is
  `SpeechTextSanitizer.Normalize`, which this repo does not allow, so the fix wants a
  markup-aware splitter of its own.
- Quitting to the main menu from the pause menu after loading a save left the game stuck on a
  Game Menu whose buttons all reported unavailable, with `SceneLoader.SetState` calling
  `ScreenDetector.OnAdventureMapReady` in a loop and `AdventureMapAdapter.GetInitialTile`
  throwing a NullReferenceException every time. The game had to be restarted. Pre-existing
  and unrelated to this port, but it is what the log fills with.
- Screenshots could not be taken after that restart: `/screenshot` answers an all-black
  1280x800 frame and the desktop reports no foreground window, so the session is locked. The
  layout evidence above is `/gui/unity`, which is side-effect free and reads the same rects.

Manual test:

1. Main menu, Tab to Options, Enter. Hear "Options", then "Gameplay, tab, selected, 1 of 7".
2. Down through the tabs: each one reads and the page under it changes; Enter does the same.
3. Tab to the rows: "General, Language, combo box, English, 1 of 5". Alt+Down and Alt+Up jump
   between General, Battle and Adventure, naming each on the way in.
4. On Auto save round interval: Right says "4", Left says "3"; Shift+Right moves ten.
5. Down from the slider: "Please provide a number, button, 1 of 1". Enter opens the game's own
   number popup; Escape there comes back to the row.
6. On Screen shake: Enter says "not checked", Enter again says "checked".
7. On Battle log: Enter opens the drop list (below); Escape leaves the setting as it was.
8. Every tab reads its rows. On Controls, the key binding categories read as plain lines and
   the bindings themselves are not there at all - the adapter has never read them.
9. Escape closes the window (the game's own key); so does Enter on Close.
10. Watch the picture on a long page: the row under the cursor should scroll itself into view.

### AdventureLobbyRandomLayoutScreen

Built: two stops. `random-layout-rows` holds the four map cards, then the selected card's three
win-condition checkboxes and its layout combo box; `random-layout-buttons` holds Confirm, Back
and Options. Screen name is the page's drawn title ("Select layout"); focus starts on the rows.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: four
`LobbyRandomMapEntry(Clone)` cards side by side at x 187, 417, 648 and 878, each 215 wide and
481 tall, each drawing a player count ("2 PLAYERS"), a paragraph of description, three win
condition toggles (y 502, 530, 558) and a layout dropdown (y 586) whose element draws only its
current value ("Quad"); Confirm at [562,662]; the lobby's Back at [21,20] and Options at
[1233,11] in the header band. No caption is drawn anywhere on the page, so the screen declares
no regions.

A card is a RADIO BUTTON, the new control type: exactly one of the four is in force, picking one
is not doing anything, and Confirm is what does. `ControlTypes.RadioButton` and
`GraphNodes.Radio` are ES2's, and the role word is the new `UI.RoleRadioButton` ("radio button",
translated in all 13 `.po` files, validator passing).

The win-condition toggles and the layout dropdown are the SELECTED card's, as they were for the
widget screen. All four cards draw their own copies, but the other three belong to maps the
player has not chosen; the selected card's read after the cards because that is where the page
draws them.

The dropdown is a real `UITextMeshDropdown`, so it is a combo box opening `DropListScreen` over
the game's own popup - which is what made the list screen take any adapter's drop list rather
than only the options menu's (`adapters/IDropList.cs`, below).

Escape is CLAIMED and presses the drawn Back button: `LobbyRandomMapSelectionMenu.Show`
registers only `InputActions.UI.Confirm` (decompiled, line 113), and neither `LobbyNavigation`
nor `MapTypeMenu` registers any input callback at all, so the key would otherwise do nothing.
Verified: `ui_back` answered `consumed` and left the page for the map type screen.

Diff: every difference is one of three kinds.

1. A card's description left the label and became a value part, so "2 Players and A randomly
   generated map for two players" is now "2 Players | A randomly generated map for two players".
   The same words, the same buffer lines, and the description still reads after the label.
2. "unchecked" became "not checked" (the phase B word).
3. The layout dropdown's OPTIONS ("Quad", "Corridor", "Random") are gone from the page - they are
   the drop list screen now - and the dropdown has become one labelled line, "Layout | Quad".

Deviations, measured:

- ARRIVING ON A CARD CHOOSES IT. The game raises the entry's `OnSelect` from the button's own
  Unity selection (`LobbyRandomMapPreviewEntry.HandleSelect`, fed by a `UISelectionProxy`), and
  the menu answers it with `SetSelectedEntry`, so a mouse click and a keyboard selection are the
  same event to the game and there is no native way to look at a card without picking it. The
  focus visual is therefore the game's own selection, exactly as the tab bar's is on the options
  window. Verified: walking down the four cards moved the win conditions and the layout under
  them (the layout read "Quad" on the 2-player card and "Dijkstra" on the 8-player one).
- Because the choice is made by the focus visual, which runs after the arrival is composed, the
  card just arrived on does NOT say "selected" in the same breath; the card the page opened on
  does. Enter on a card is still declared and is the same `SetSelectedEntry`.
- The combo box row is named with the mod's "Layout" (`ModStrings.Screens.Layout`), as the widget
  screen named it: the dropdown draws no label of its own, only its current value.

Follow-ups, not fixed:

- Entering the ONLINE lobby leaves the game's own game-code field holding the keyboard, so the
  mod stands down (`/input` answers `standing down`) until something takes the focus off it. The
  same happens on the game settings popup, whose Name field the game activates on open. Neither
  is this port; both are the stand-down working as designed against a game that hands its fields
  the keyboard unasked.

Manual test:

1. Conquest, Random maps. Hear "Select layout", then "2 Players, radio button, A randomly
   generated map for two players, selected, 1 of 8".
2. Down through the cards: each reads its name and description, and the picture's orange border
   follows the cursor.
3. Down again: the three win conditions read as checkboxes, then "Layout, combo box, Quad".
   Enter on a checkbox says "checked", Enter again "not checked".
4. Enter on Layout opens the game's own list; Up and Down walk it, Escape leaves it unchanged.
5. Tab: "Confirm, button, 1 of 3", then Back and Options.
6. Escape leaves the page for the map type screen, the same as pressing Back.

### AdventureLobbyGameSettingsScreen

Built: two stops. `game-settings-rows` holds the settings in the order the page draws them,
`game-settings-buttons` holds Cancel then Confirm. Screen name is the window's drawn title
("Game settings"); focus starts on the rows.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: the window at
[325,147,630,519], one scrolling column of rows at x 364 (539 wide, 861 px of rows in a 379 px
viewport) with the label at x 367 and the control at x 570, Cancel at x 508 and Confirm at
x 646. The page draws NO caption over any group of rows, so the screen declares no regions;
the adapter's text rows would read as plain lines where the game drew one, and it draws none.

Offline the page is six dropdowns, five toggles and the Random Seed field; online it adds the
game Name field and the Invite Only, Simultaneous turns, Humans fight for hostiles and Turn
timers toggles, plus the Force quickbattles dropdown. Turn timers ON adds seven time rows and
a "Reset to defaults" button, a variant no capture had; it was taken on the widget build first
(`walks/before/AdventureLobbyGameSettingsScreen-online-timers.txt`).

A TIME row is TWO edit fields, minutes and seconds, because that is what the game draws for a
keyboard: `UITimeInputField` keeps `_minutesInputfield` and `_secondsInputfield` under a
"Keyboard" header and `_gamepadSlider` under a "Gamepad" one, and `OnControlsChanged` only
brings the slider back in gamepad mode - measured, `GamepadSlider` reads `visible=false` on
every drawn row while `KeyboardInput` holds the two boxes and a ":" between them. So there is
no slider to model. Each node is named with the row's drawn label and says which half it holds
in the game's own words ("Base turn time, editable, 1 minute", then "Base turn time, editable,
0 seconds"), through the same text keys the widget read them with
(`Adventure/PostGameMenu/TotalPlayTime/Minutes` and `.../Seconds`).

Escape is the game's (`ConsumesBack` false): `LobbyMapSettingsMenu.Show` registers
`InputActions.UI.ExitMenu` outside its gamepad branch (decompiled, line 333). Verified:
`ui_back` answered `unclaimed` on the page, and `consumed` on the drop list over it.

Diff, offline and online and online-with-timers: every difference is one of four kinds.

1. "unchecked" became "not checked".
2. A dropdown's OPTIONS are gone from the page - they are the drop list screen now - and each
   dropdown is one labelled line with its current value ("Starting resources | Map default",
   "Force quickbattles | Never", ...). The widget screen never read a dropdown's name at all.
3. A text field's label and value separated: "Random Seed, 353629319" is now
   "Random Seed | 3255587122" (the seed itself is this session's, not the port's), and the same
   for the online Name field.
4. A time row became two: "Base turn time, 1:00" is now "Base turn time | 1 minute" and
   "Base turn time | 0 seconds".

Deviations, measured:

- AN EDIT ROW DECLARES ITS TOOLTIP FOR THE BUFFER BUT DOES NOT AIM AT IT. Drawing the game's
  tooltip for a control means `NativeTooltipUtility.ShowTooltipForComponent`, which SELECTS the
  component the tooltip hangs on - for a text row, the row's own label - and that selection takes
  the keyboard straight back off the field the player just asked to type in. Measured: with the
  tooltip aimed, `ui_activate` said "editing", the field never reported focus, `/input ui_down`
  answered `consumed` (no stand-down) and the edit ended in silence; with `PointsAt` cleared,
  the same sequence answered `standing down` and a native deselect said "Cancelled". The
  tooltip's lines are still in the review buffer, which is where this mod's ruling puts them.
- The turn-timer rows' labels are read off the row's own text mesh
  (`UITextMeshTextUtility.GetEffectiveText`), not off `UITimeInputField.Text`: measured, every
  one of the seven rows answered `Text` with the prefab's placeholder "Label" while the page
  drew "Base turn time" and the rest. The adapter now reads them the way it already read every
  other row kind.

Follow-ups, not fixed:

- THE FOCUS VISUAL IS RE-ASSERTED WHILE THE CURSOR STANDS STILL, which is what the deviation
  above works around. `GraphNavigator.SyncVisual` skips its work when the node id and the AIM are
  unchanged, but the aim is compared with `ReferenceEquals` and every build hands it a fresh
  `Tooltip` object, so a row with a tooltip re-draws it - and re-selects its component - over and
  over. Measured: with the cursor on a row, a selection put anywhere else by `/eval` was taken
  back within a second, every time. It affects every ported screen with tooltips (the options
  window included); nothing there holds the keyboard, so only the text fields showed it.
- Entering the ONLINE lobby leaves the game's own game-code field holding the keyboard, and
  opening this window leaves its Name field holding it, so `/input` answers `standing down`
  until something takes the focus off them. The game activates both itself; the stand-down is
  working as designed.
- The online Name row's tooltip is the raw key `Lobby/MapSettings/GameName/Tooltip`, which the
  game itself fails to localize. Present in the before capture too.

Manual test:

1. A lobby, Game settings. Hear "Game settings", then the first row.
2. Down the rows: dropdowns read "combo box" and their value, toggles read their state, the
   Random Seed row reads "editable" and its number.
3. Enter on a dropdown opens the game's own list; Up and Down walk it; Escape leaves it alone.
4. Enter on a checkbox says "checked", Enter again "not checked".
5. Enter on Random Seed says "editing"; type and hear the characters; Enter ends the edit and
   says "edited" with the new number; Escape instead says "Cancelled" and puts the old one back.
6. Online, tick Turn timers: seven rows appear, each twice - minutes then seconds - followed by
   "Reset to defaults". Enter on one, type a number, Enter to end.
7. Tab: "Cancel, button, 1 of 2", "Confirm, button, 2 of 2".
8. Escape closes the window without applying (the game's own key).

### AdventureLobbyPlayerSettingsScreen

Built: two stops. `player-settings-rows` holds the three multiplier sliders, the Start with
Marketplace checkbox and the "Reset to default" button in drawn order;
`player-settings-buttons` holds Cancel then Confirm. Screen name is the popup's drawn title
("Player settings"); focus starts on the rows.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: the popup at
[333,226,613,348], the title at y 252, one column of rows at x 450 - Income multiplier (y 298),
Troop production multiplier (y 337), Start with Marketplace (y 382), XP bonus multiplier
(y 421), "Reset to default" (y 453) - and Cancel (x 508) with Confirm (x 646) at y 514. No
caption is drawn, so the screen declares no regions.

Every slider draws a value box over its number (an `EditButton` at x 792, measured on all
three), so every slider row carries the same always-open child the options window's sliders do:
a button whose label is the game's own "Please provide a number" and whose activation runs the
delegate the slider installed on the box. Verified: Enter on the child opened the game's number
popup as `MessageDialogScreen`, and Escape there came back to the child with the value unchanged.
One coarse step is ten fine steps, as on the options window (measured: Right said "1.1", Left
"1", Shift+Right "2", Shift+Left "1").

Escape is the game's (`ConsumesBack` false): `LobbyPlayerSettingsMenu.Show` registers
`InputActions.UI.ExitMenu` outside its gamepad branch (decompiled, line 146). Verified:
`ui_back` answered `unclaimed` on the popup, and `consumed` on the number popup over it.

Diff: exactly two kinds.

1. Each slider gained a "Please provide a number" child row (the options window's shape).
2. "unchecked" became "not checked".

Deviations, measured: none beyond the representative's. The adapter's slider gained the two
value-box facts the options adapter already had, and both now read them from one place
(`adapters/SliderValueEditor.cs`).

Manual test:

1. A lobby, a player row's Player settings. Hear "Player settings", then "Income multiplier,
   slider, 1, 1 of 5".
2. Right and Left move the multiplier by 0.1; Shift+Right and Shift+Left by 1.
3. Down: "Please provide a number, button, 1 of 1". Enter opens the game's number popup; Escape
   there comes back to the row with the slider unchanged.
4. Down through the rest: the Marketplace checkbox and "Reset to default".
5. Tab: "Cancel, button, 1 of 2", "Confirm, button, 2 of 2".
6. Escape closes the popup without applying (the game's own key).

### OnlineHostGameScreen

Built: two stops. `host-game-rows` holds the sentence the popup asks with, the name box and the
Invite Only toggle; `host-game-buttons` holds Cancel then Host. Screen name is the popup's drawn
heading ("Host Game"); focus starts on the rows.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: the popup at
[427,269,427,261], Header at y 295, the sentence at y 328, `OptionsTextMeshInput` at y 365, the
Invite Only toggle at y 421, and Cancel (x 509) with Host (x 647) at y 470. The sentence is a
read-only row because it is drawn and heads nothing; the heading is the screen's name and so is
not a row (the family B contract, and the sentence is what the popup actually asks).

Escape is CLAIMED and presses the drawn Cancel: `GameListMenu.ShowHostGame` registers
`InputActions.UI.Cancel` only on its GAMEPAD branch and gives the keyboard `UI.Confirm` instead
(decompiled, lines 546 to 555), so the key would otherwise do nothing. Verified: `ui_back`
answered `consumed` and the game list returned.

Diff: exactly two kinds.

1. The name row's label and value separated: "Host Game, Neurrone's Heroic Adventure" is now
   "Host Game | Neurrone's Bombastic Buffoonery" (the name itself is the one the game generated
   this session, not the port).
2. "unchecked" became "not checked".

Deviations, measured: the name row declares its tooltip for the buffer but does not aim at it,
for the reason the game settings entry records.

Follow-ups, not fixed:

- THE GAME TAKES THE KEYBOARD AS THE POPUP OPENS. `ShowHostGame` calls
  `HostGameInputField.ActivateInputField()`, so the field is focused before the player has done
  anything and the mod stands down: measured, the first `/input ui_down` after the popup opened
  answered `standing down`, and only after the field was deselected did the keys work. While the
  field holds the keyboard the mod's echo is not running either (it echoes an edit the mod
  started), and the game's own Enter on that branch presses HOST. Nothing here fixes that; it
  would want the mod to take the keyboard back from the game on arrival, which is a decision
  about native behaviour rather than a port.

Manual test:

1. Main menu, Multiplayer, Find online game, then Host Game. Hear "Host Game", then "Please
   enter the name of the game you want to create, 1 of 3".
   Note the field may already have the keyboard (follow-up above): press Escape or click
   elsewhere first if the arrows do nothing.
2. Down: "Host Game, editable, <the generated name>"; then "Invite Only Game, checkbox, not
   checked".
3. Enter on the name box says "editing"; type and hear the keys; Escape says "Cancelled" and puts
   the name back. Careful: Enter inside the box is the game's own submit and creates the game.
4. Enter on Invite Only says "checked", Enter again "not checked".
5. Tab: "Cancel, button, 1 of 2", "Host, button, 2 of 2".
6. Escape closes the popup, the same as pressing Cancel.

### CommunityMapsSearchFilterScreen

Built: two stops. `search-filter-rows` holds the keyword box and then the tag checkboxes, one
region per drawn category caption; `search-filter-buttons` holds the footer's buttons in drawn
order. Screen name is the panel's drawn title ("Search & filter"); focus starts on the rows.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: the panel down the
right of the window at [853,0,427,800] - the keyword box at [880,60,373,32], then a scrolling
list of tags (1035 px of rows in a 608 px viewport) in which each category draws a caption of its
own ("Content Type" at y 128, "Map Type" at y 336, "Languages" at y 651, "Contests" at y 1045)
over its rows - and the footer at y 747 with Search (x 880), Clear filter (x 977) and Cancel
(x 1164). So this page DOES draw captions, and each is the region its tags belong to: Alt+Down
read "Map Type, Single Player, checkbox, not checked, 1 of 10" and Alt+Up came back.

Escape is CLAIMED and runs mod.io's own `Close`: the panel is the browser's, not the game's, and
nothing registers the key for it - the finding the community maps modal already recorded.
Verified: `ui_back` answered `consumed` and the home page returned.

Diff: one kind. "unchecked" became "not checked", on all 33 tags. Nothing else changed - the
keyword box, Search and Clear filter read exactly as before. (The stack line differs because the
search results page was still under the home page from the Search test below.)

Deviations, measured:

- The keyword box takes a native focus visual of its own (`NativeSelectionUtility.Select` on the
  field). Without it, an activation that FOLLOWED a tag row - whose focus visual selects mod.io's
  own toggle - selected the box but never made it focused: measured, `ui_activate` said "editing",
  `/input ui_down` answered `consumed` rather than `standing down`, and the edit ended in silence,
  reproducibly. With it, the same sequence answers `standing down` and a native deselect says
  "Cancelled".
- The tag rows are `SyntheticNode`s keyed on category and tag rather than on the drawn row: mod.io
  rebuilds the row objects as the list scrolls, and a tag is a category and a name either way.

Follow-ups, not fixed:

- The footer's third button, Cancel, is not read: `GetActions` looks for a button whose onClick
  names `Close` and falls back to the third of the panel's labelled buttons, and neither finds it
  (the drawn control is "Close (Controller Icon)"). Pre-existing - the before capture is missing
  the same line - and Escape does what it would do.
- Pressing Search left the stack as `CommunityMapsSearchResultsScreen > CommunityMapsHomeScreen >
  CommunityMapsSearchFilterScreen`: the results page went UNDER the home page rather than being
  popped when Back returned to it. The detector's, not this port's.

Manual test:

1. Main menu, Community Maps; close the mod.io modal with Escape if it appears; then the search
   and filter button. Hear "Search & filter", then "Enter keyword, editable".
2. Down: "Content Type, Map, checkbox, not checked, 1 of 6". Enter says "checked", Enter again
   "not checked".
3. Alt+Down and Alt+Up jump between Content Type, Map Type, Languages and Contests, naming each
   on the way in; watch the list scroll itself to the row under the cursor.
4. Home, then Enter on the keyword box: "editing". Type, hear the keys, Escape says "Cancelled".
5. Tab: "Search, button, 1 of 2", "Clear filter, button, 2 of 2". Enter on Search opens the
   results; its Back returns.
6. Escape closes the panel.


## Family E: drop lists

### DropListScreen (new, mod-owned; the list every family D combo box opens)

Built: `screens/DropListScreen.cs`, one stop of Choice nodes in option order, walked Up and
Down. The entry the setting is currently on says "selected", which is also what the list lands
on. Screen name is the setting's own label. Enter takes the entry through the adapter's
`SetValue` and pops; Escape pops without touching the setting.

`ConsumesBack` is TRUE: this is the one surface here the MOD put on the screen, so it is the
one that denies the game the key, per the phase B ruling. `IsPresent()` is "still the list the
mod asked for, and its control still drawn".

The game's real popup is opened on push (`UITextMeshDropdown.Show`) and hidden on pop
(`TMP_Dropdown.Hide` through the wrapper's own dropdown), so the picture shows what the player
is doing; the game closing it underneath - a click elsewhere, the page going - is noticed in
`Update` and takes the screen with it. The highlight DOES follow the cursor: each entry's focus
visual selects the toggle TMP built for it (`TMP_Dropdown.m_Items`, one per option in option
order), which is the same selection the game's own hover and the template's
`AutoScrollToSelected` follow. The harness cannot photograph that, so it is in the manual test.

Verified on Battle log: Enter read "Battle log" then "Always hide, selected, 3 of 3"; Up read
"Always show, 2 of 3"; Escape came back to "Battle, Battle log, combo box, Always hide, 7 of 7"
unchanged, with the game's `Dropdown List` gone from the hierarchy; reopening and pressing
Enter on the current value closed the list and left the setting alone.

Deviations, measured: the entries are `SyntheticNode`s keyed on the option index, because TMP
builds a row per entry only while the popup is open and the mod's row has to answer either
way; no tooltip is declared, the game putting none on an options dropdown's entries.

Manual test:

1. Options, Gameplay, the Language row. Enter: hear "Language", then the language you are on,
   "selected".
2. Up and Down walk the list; Home and End reach its ends.
3. Watch the picture: the game's own list should be open, and the entry under the cursor
   highlighted and scrolled into view.
4. Escape: the list closes, the setting is unchanged, and the cursor is back on the row.
5. Enter on an entry: the list closes and the row reads the value you picked.

### AdventureLobbyIconDropdownScreen

Built: one stop of Choice nodes in the order the game spawned the entries, walked Up and Down
although the popup draws them as a horizontal strip (owner ruling: a list of values is read
down whatever the page does with it), then the mod's own Cancel row last. The entry the
dropdown was opened ON says "selected" and is where the list lands. Screen name is what the
dropdown is choosing, in the game's words ("Colour", "AI Difficulty"). Enter is the game's own
click on the entry, through the same detector notifications the widget screen sent.

Which entry is the current value is a new adapter fact, read where the game records it: every
`IconDropdown.SetupAs*` keeps the entry whose value matches the current one and hands it to
`Show`, which parks it on `_selectionLayer.DefaultSelectable` (decompiled). `OptionItem`
answers `IsCurrentValue` by comparing that against its own button's selectable.

Escape: claimed. `IconDropdown.Show` registers `InputActions.UI.Cancel` on `Hide`, and
`UI.Cancel` is this game's GAMEPAD binding throughout - every keyboard branch registers
`UI.ExitMenu` instead, the finding `PlatformUserMenuScreen` established - so Escape does
nothing here and the screen takes it, running the same `Hide` the Cancel row runs. Verified:
`ui_back` answered `consumed` and the popup closed with the value unchanged, and `ui_back` on
the lobby underneath answered `unclaimed`.

Diff, on all five variants: exactly two kinds of change, plus lobby state.

1. The current value gained "selected" (colour, faction, starting wielder, partnership, AI
   difficulty - one line each).
2. "disabled" became "unavailable" on the colours the game is refusing.

The rest is the lobby, not the port: this session's slot is Yulan in dark red where the
before-capture was Arleon in another colour, so a different colour is refused and the starting
wielders are Yulan's.

Deviations, measured: the Cancel row is the mod's own control and keeps a marker subject of its
own, the popup drawing no way out; the entries are `DrawnNode`s on the spawned
`IconDropdownEntry`, which is what the game destroys when the list closes.

Follow-ups, not fixed: the tooltip-cleaning gap the options port found shows badly here. A
faction's description and a wielder's dossier are the entry's tooltip, and they reach the
buffer raw, so the italics, colour and highlight tags the game writes ("<i>", "<color=...>",
"<hl>") are now in the text, and the newlines inside a dossier are not split into lines - which
is why a wielder's flat line runs on and the dump shows a stray " | " continuation. The widget
engine cleaned both with `SpeechTextSanitizer.Normalize`; the graph engine's `NodeSection`
does not, and this repo does not allow that cleaner. This is the highest-value thing to fix
before the rest of family D and E are batched.

Manual test:

1. Conquest, a Conquest map, Confirm; in the lobby, the colour button. Hear "Colour", then the
   colour you are on, "selected" (and "unavailable", because the game refuses the colour you
   already hold).
2. Up and Down walk the nine colours; End reaches Cancel.
3. Escape closes the popup and leaves the colour alone; so does Enter on Cancel.
4. The faction, starting wielder and partnership buttons behave the same; select the AI slot
   and its AI difficulty button does too.
5. Enter on a colour: the popup closes and the lobby row reads the colour you picked.
6. Listen to a faction or wielder entry: note the markup now being read out (follow-up above).

### Buffer lines lose the game's markup (all graph screens)

Found on the drop lists: faction and wielder entries read their tooltips with the game's
rich-text tags spoken aloud and their paragraphs as one line, because the widget engine
cleaned tooltip lines and the graph's sections did not. `ui/SpokenLines.cs` now splits every
raw tooltip or details string on its newlines first and then strips the tags and doubled
spaces from each line, and `GraphNodes.Sections` routes both through it (commit below). The
repo's rule against `SpeechTextSanitizer.Normalize` stands: that normaliser collapses the
newlines before anything can split on them. Verified: no `<` in any buffer line of the
seven Options tabs after the change. Manual test: on a lobby's faction list, Ctrl+Down
through the buffer of an entry; the description must read as sentences, with no tag names.

### Three adapter rules from the forms (all graph screens)

- The focus visual is re-drawn only when what it draws changes: `GraphNavigator.SameAim`
  compares two tooltips by the component, anchor or map tooltip they point at, not by
  reference, since every rebuild makes a fresh `Tooltip` object. Before this the native
  tooltip was torn down and re-drawn every frame under a standing cursor, which is what the
  edit-field rule ("an edit row's tooltip is not drawn") worked around.
- A field the GAME focuses on its own as a page opens (the online lobby's game code, the
  game settings name, the host popup's name) is released as the graph screen takes focus
  (`GameTextFocus.Release`, called from `GraphScreen.OnFocus` unless a handover is pending),
  so the mod's keys work on arrival and the field is reached through its own edit node.
- The stand-down applies on graph screens only until phase G: a widget screen's text input
  focuses the game's field itself and relies on the mod's Tab staying live to leave it, and
  with the stand-down live there the lobby's game code field trapped every key (seen on the
  widget lobby page after activating "Copy game code"). Manual test: on the online lobby
  (still a widget page), Tab past the game code and type nothing; the mod must keep answering.

## Family F: table pages

### AdventureLobbyMapSelectScreen (representative)

Built: four stops. `map-select-filters` holds the header band's filter buttons as expandable
groups of checkboxes, then the Clear filters button while it is drawn; `map-select-table` holds
the drawn heading band as a row above a `GraphSheet` of one region; `map-select-details` holds the
preview panel as one line; `map-select-buttons` holds Back, Options and Confirm. Screen name is the
page's drawn title ("Select Map"); focus starts on the table, landing on the map the page opened on.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: `HeaderWithSortButtons`
at [87,104,858,26] with seven `TableSortUIButton`s at x 95, 151, 376, 523, 690, 795 and 880 (Type
and Played draw an icon and no caption); SIX filter buttons drawn over that band at y 108, x 122,
495, 661, 767, 851 and 906 (Type, Tag, Win Condition, Players, Size, Played); the maps as
`SelectMapLobbyMenuEntry(Clone)` rows 34 px tall at x 95, drawing a type icon (x 110), the name
(151), the tag text (377), the win-condition icons (532), the player count (727), the size (791) and
a played badge (892); `LobbyMapPreview` at x 954 with the map's name at y 319, its description in a
scroll rect at y 367 and the win-condition icons at y 298; Confirm at [974,677]; Back at [21,20] and
Options at [1233,11] in the band above. The screenshot matches, the six funnels included.

Escape is CLAIMED and presses the drawn Back button: `MapSelectMenu.SetupAndAnimateAfterLoad`
registers only `InputActions.UI.Confirm` on its non-gamepad branch (decompiled, line 296),
`UIFilterDropdown.Show` registers `UI.Cancel` (this game's gamepad binding) for its own list, and
`LobbyNavigation` registers no input callback at all - it only subscribes to the menu's own
`OnCancel`, which the drawn Back button raises. Verified: `ui_back` answered `consumed` and the map
type page came back with nothing stray spoken.

Diff: 440 before lines against 462 after, and every before line is accounted for. The mapping, row
by row, for New Beginnings: the widget's `New Beginnings, Type, Official` (buffer
`New Beginnings, Type, Official / Official`) is now the cell `Official`, with the column spoken as
the edge crossed into it and `Type, Official / Official` as its buffer; and the same for `Name`,
`Tag, blank`, `Win Condition, King of the Hill`, `Players, 2`, `Size, 65 x 65` and `Completed`.
Every other map's seven cells map the same way, which is why the sorted after file holds fewer
per-row lines than the before one: two maps of the same size are one `65 x 65` line once the row
name has left the cell. Ninety-three before lines have no identical after line, in five groups:

1. Every map's NAME cell (57 lines): the widget's buffer read "New Beginnings, Name" and the
   sheet's primary opens its buffer with its own readout, "New Beginnings" - the column's caption is
   the edge crossed into the cell, and "Name, New Beginnings" over "New Beginnings" would be the same
   words twice. Checked mechanically: 56 of the 57 bare names are an after buffer of their own, and
   the 57th is the selected map, whose after buffer is "New Beginnings / selected".
2. The seven column headings lost the words "column header" (7 lines): the band's nodes are
   buttons, and a role word is excluded from a flat line by design.
3. The fifteen maps with more than one win condition lost their joined cell
   ("King of the Hill and Beacons of Power"): that cell is now one piece per drawn icon, each with
   that icon's own tooltip, which is the owner's ruling for this column.
4. Thirteen filter checkboxes: "unchecked" became "not checked" on Completed and Not completed (the
   phase B word), and eleven tag and win-condition boxes gained their tooltip as a buffer line. That
   second half is the CAPTURE, not the port: a toggle's text mesh is inactive while the game's list
   is shut, so the before capture (taken with the lists closed) read no tooltip from them, and the
   after capture was taken with all six lists opened so the diff could see the boxes at all.
5. The preview line: the description moved out of the label into a declared SECTION, so the label is
   the map's name alone and the buffer holds the description one drawn line at a time, with the win
   conditions the panel draws icons for added after them.

Deviations, each measured:

- SIX filter buttons, not the four the proposal named: the game draws Size and Played funnels too
  (measured above), and the adapter's seventh filter (Content profile) reports itself not drawn on
  this machine and is skipped. Declaring only four would have dropped 7 of the 30 checkboxes the
  before capture holds.
- THE PRIMARY IS WALKED FIRST, though the game draws the type icon to the left of the name.
  `GraphSheet` emits the primary cell before every other cell of a row, so the walk is Name, Type,
  Tag, Win Condition, Players, Size, Completed. The heading band is declared in that same order (and
  each heading stamped with the column it stands over) so that the band and the rows are walked
  identically and Up out of a row reaches its own column's heading.
- ARRIVING ON A ROW SELECTS THAT MAP. The row's focus visual is the adapter's `FocusNative`, which
  is the menu's own `SetSelectedEntry` - the call that fills the preview - so there is no native way
  to look at a map without picking it, exactly as on the random layout page. The row therefore says
  "selected" only for the map the page opened on (the focus visual runs after the arrival is
  composed), and Enter is the entry's own click, which says nothing the live watch has not said.
- The cells carry a `BufferHead` of the column's caption and the value, because the caption is
  spoken as the edge crossed into the cell and nobody arrives at a review buffer across an edge.
  The primary keeps its readout as its buffer head: "Name, New Beginnings" over "New Beginnings"
  would be the same words twice.
- The sort heading says the DIRECTION the game draws its arrow in ("ascending", "descending",
  nothing when the column is unsorted), through the `ModStrings.UI.SortAscending` pair the widget
  table already used, rather than saying "selected".
- The preview is ONE line, not a stop of several: the panel draws a name, a paragraph and a row of
  icons, so the node's label is the name (watched live) and its SECTION is the paragraph's own lines
  followed by the win conditions. A section rather than value parts because a dossier runs to several
  drawn lines and the review buffer must hold them one at a time - a part is exactly one buffer line,
  newlines and all - while a section is announced on arrival just the same. Verified: with the cursor
  on that line, clicking another map natively made it speak "Haven", then the description, then
  "Beacons of Power and King of the Hill"; and the buffer of a multi-paragraph map reads its
  paragraph, then "Forced factions", then each faction on a line of its own.
- The adapter gained six kinds of member, each a game fact the screen had no way to ask for:
  `PreviewTitle` (the name the preview panel draws), `IsSelectedEntry` with the row's `IsSelected`,
  `Entry` (the drawn row), `WinConditionTooltips` (one per drawn icon, for the pieces),
  `MapSelectSortButtonAdapter.Button`, and `MapSelectFilterAdapter.Subject`/`IsOpen` with
  `Option.Subject`.

Walk (all through `/input` and `/type`): Tab cycles table, preview, buttons, filters; Right across a
row said "Type, Official", "Tag, blank", "Win Condition, King of the Hill", "Players, 2"; Down in the
Players column said "A Clash in the Marsh, 3, 2 of 57" and kept the column; Left into a two-icon win
condition said "Win Condition, Beacons of Power" and then "King of the Hill" with NO caption between
the pieces; End reached "Arti- Test, 57 of 57" and Home the band's "Name, button"; `/type haven`
landed on "Haven, selected, 29 of 57" with ONE result; Enter on the Name heading said "ascending" and
put "A Clash in the Marsh" first, again said "descending" and put "Woods And Walls" first; Right on
the Type filter opened the game's list on "Official, checkbox, checked, 1 of 3", Enter said "not
checked" and the table refiltered to one row, Enter again said "checked" and it was 57 again.

Follow-ups, not fixed:

- A third press of a sort heading clears the arrow but does NOT restore the order the page opened
  with: `TableSortUIButton.Reset` only blanks the graphic and the menu re-sorts by the same column.
  The game's, and the widget table had it too.
- Type-ahead re-announces the landing once per typed character, so "haven" read "Haven, selected"
  five times. The engine's, and it shows here because every letter narrows to the same row.
- `AdventureLobbyMapSelectRowAdapter.Name` and the sort headings still normalise through
  `SpeechTextSanitizer.Normalize` (pre-existing, and against the repo's standing rule).
- `MapSelectFilterAdapter.Label` for the Played filter is that column's caption, so the group and
  the last column read the same word, "Completed"; that is what the widget screen read too.

Manual test:

1. Main menu, Conquest, Conquest maps. Hear "Select Map", then "Select Map, grid, New Beginnings,
   selected, 1 of 57".
2. Down and Up walk the maps, each reading its name and its place; the picture's orange frame and the
   preview panel follow the cursor.
3. Right walks the columns, naming each on the way in: Type, Tag, Win Condition, Players, Size,
   Completed. On a map with two win conditions, Right steps from one icon to the other with no
   caption in between. Left comes back the same way.
4. From a metadata column, Down says the next map's name, then that column's value, then "N of 57".
5. Home reaches the heading band ("Name, button"); Enter there sorts and says "ascending", Enter
   again "descending". Down returns to the maps.
6. Type a few letters of a map's name: the cursor lands on it. Backspace ends the search.
7. Tab: the preview line (name, description, win conditions); Tab: "Back, button, 1 of 3", then
   Options and Confirm; Tab: "Type, group, collapsed, 1 of 6".
8. On a filter group, Right opens the game's own checkbox list and lands on its first box; Enter
   ticks and unticks it and the table refilters under you; Left closes the list again.
9. Escape leaves the page for the map type page, the same as pressing Back.

### AdventureLobbyChallengeMapSelectScreen

Built: three stops, the representative's minus its header band. `challenge-map-table` holds a
`GraphSheet` of one region (Name as the primary, then Win condition and Completed);
`challenge-map-details` holds the preview panel as one line; `challenge-map-buttons` holds Back,
Options and Confirm. Screen name is the page's drawn title ("Challenge maps"); focus starts on the
table, landing on the challenge the page opened on.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: `MapEntryContainer` at
[87,96,858,613] holding ten `ChallengeMapEntry(Clone)` rows 48 px tall at x 95, each drawing its name
(x 152) and its win-condition icons (x 739) and NOTHING ELSE; `LobbyMapPreview` at x 954 with the
name at y 307, the dossier in a scroll rect at y 356 and the win-condition icons at y 287; Confirm at
[982,679]; Back at [21,20] and Options at [1233,11] under the drawn title at [399,19]. The screenshot
matches: no heading band, no funnels, one icon per row.

Escape is CLAIMED and presses the drawn Back button, the representative's finding on this page's own
class: `ChallengeMapsMenu.SetupAndAnimateAfterLoad` registers only `InputActions.UI.Confirm` on its
non-gamepad branch (decompiled, line 232), and `LobbyNavigation` registers nothing. Verified:
`ui_back` answered `consumed` and the map type page came back.

Diff: 37 before lines against 36 after, sixteen of them with no identical after line, in four groups
- the same four the representative has, minus its filters:

1. Every challenge's NAME cell (10 lines): "A Hot Deal, Name" became "A Hot Deal", the primary
   opening its buffer with its own readout.
2. The three column headings are GONE (3 lines): the game draws no heading band on this page at all,
   so none is declared. The captions survive where they belong - as the edge labels the sheet speaks
   on the way into a column, heard in the walk below.
3. The two challenges with two win conditions lost their joined cell ("King of the Hill and Find the
   Object"): one piece per drawn icon now, each with that icon's own tooltip.
4. The preview line: the dossier moved out of the label into a declared section, so the label is the
   challenge's name alone, the buffer holds the dossier one drawn line at a time exactly as before,
   and the win conditions were added after them.

Deviations, measured:

- NO HEADING BAND IS DECLARED, because the game draws none. The three captions the widget screen
  invented rows for ("Name", "Win condition", "Completed") are localized game strings and are kept as
  the sheet's column captions, so they are still spoken - as the crossing into each column - but
  there is nothing drawn for them to be a row of.
- No Completed cell is drawn either: `LobbyChallengeMapEntry._playedContainer` is switched off on
  every one of these ten. The column is still declared, and reads "Not completed" - never dropped, or
  the columns would not be the same all the way down.
- The adapter gained the same members the representative's did: `PreviewTitle`, `IsSelectedEntry`
  with the row's `IsSelected`, `Entry` and `WinConditionTooltips`.

Walk: Tab cycles table, preview, buttons; Right said "Win condition, Beacons of Power" then
"Completed, Not completed"; Down in the Completed column said "Choices, Not completed, 2 of 10";
End reached "King of the Hill, 10 of 10" and Home came back, both keeping the column; `/type marsh`
landed on The Marsh Provides with ONE result; Right across its win condition said
"Win condition, King of the Hill" then "Find the Object" with no caption between the pieces, and
Left came back the same way; Enter on the row said nothing beyond what arriving had already done, and
Tab read the preview with the new dossier; `ui_back` answered `consumed`.

Follow-ups: none of this screen's own. `AdventureLobbyChallengeMapRowAdapter.Name` still normalises
through `SpeechTextSanitizer.Normalize` (pre-existing).

Manual test:

1. Main menu, Conquest, Challenge maps. Hear "Challenge maps", then "Challenge maps, grid, A Hot
   Deal, selected, 1 of 10".
2. Down and Up walk the ten challenges; the preview panel follows the cursor.
3. Right says "Win condition, ..." then "Completed, Not completed"; on The Marsh Provides or The
   Rotten Caves, Right steps from one win-condition icon to the other with no caption in between.
4. From the Completed column, Down says the next challenge's name, then its value, then "N of 10".
5. Type a few letters of a challenge's name: the cursor lands on it. Backspace ends the search.
6. Tab: the preview line, read as name then dossier; Ctrl+Down through its review buffer must give
   the dossier one line at a time. Tab: "Back, button, 1 of 3", then Options and Confirm.
7. Escape leaves the page for the map type page, the same as pressing Back.

### OnlineGameListScreen

Built: three stops. `online-game-region` holds the Region combo box drawn above the list;
`online-game-table` holds the status line while the game draws it, then the drawn heading band as a
row, then a `GraphSheet` of one region (Game name as the primary, then status and Players);
`online-game-buttons` holds the selected-game line while the game draws it, then Host Game, Load and
Host Game, Join With Game Code, Join Game, Options and Main Menu. Screen name is the page's drawn
title ("Game List"); focus starts on the list.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: the window at
[196,104,889,593]; `RegionSelector` at [803,125,238,27]; the heading band `TitleBar` at
[237,157,795,26] with `TitleStatus` (an icon, x 237, no caption of its own), "Game name" (x 309) and
"Players" (x 837); the games as `GameListEntry(Clone)` rows 36 px tall at x 237, each drawing a
status icon (x 256), its name (x 312) and its player count (x 839); the commands at y 632 - Host Game
(x 233), Load and Host Game (416), Join With Game Code (689), Join Game (873); Back ("Main Menu",
x 21) and Options (x 1233) in the main menu's header band. The status overlay `Buffer` is the whole
list area [237,187,797,425] with its line at y 378, so it REPLACES the games rather than sitting
beside them. The screenshot matches: an icon heading, GAME NAME, PLAYERS, and a lock on every row.

Escape is CLAIMED and presses the drawn Main Menu button: `GameListMenu.ReregisterDefaultInput`
registers `Gamepad.UIExtraButton2` and nothing else (decompiled, line 251), so the key would do
nothing here. Verified: `ui_back` answered `consumed` and the main menu came back.

Diff: every before line is accounted for, in four groups.

1. Thirty game-row lines are a DIFFERENT SET OF GAMES: this list comes off the network, and the ten
   games the before capture caught were gone hours later (18 games now). Row for row the shape is
   the same: "Sirhk's Legendary Escapade, Game name" is the primary cell "KogsonCZ's Epic Song",
   "..., status, Can't join. Game is in progress." is the status cell reading its value alone with
   the caption as an edge and "status, Can't join..." as its buffer head, and "..., Players,
   2/&lt;low&gt;4&lt;/low&gt;" is "2/4".
2. The four region options (Asia, Europe, Russia, USA East) left the page: they are the drop list
   screen now, and the dropdown is one line, "Region | Europe".
3. The three column headings lost the words "column header".
4. "Join Game | disabled" became "unavailable" (the phase B word).

Deviations, each measured:

- THE HEADING BAND IS READ-ONLY. The game draws one (`TitleBar`), so it is declared as a row above
  the first game and Up out of a row reaches its own column's heading - but the three headings are
  images with no click on them: this list has no sorting at all. They are text nodes, not buttons.
  The status column's heading draws an icon and no words, so it takes the mod's own `UI.Status`, the
  word the widget table already gave it.
- THE RENDERER'S MARKUP IS STRIPPED IN THE SCREEN. The game writes the player count with its own
  colour tags ("2/&lt;low&gt;4&lt;/low&gt;"), which the widget spoke as its label and cleaned only in
  the buffer. Every value and every name here goes through `SpokenLines`, so the tags are gone from
  both. Measured before the change: the graph's cell read the tags aloud.
- The primary is walked first, as on the map select page: the sheet emits it before every other cell,
  so the walk is Game name, status, Players though the game draws the status icon leftmost. The
  heading band is declared in that same order for the same reason.
- The status line is at the TOP OF THE LIST'S OWN STOP rather than in a stop of its own, because the
  game draws it over the list area: with it up there are no games, and the stop is then the band and
  that one line. Verified by switching the region to Russia: the list emptied, the band stayed, and
  the line read "Connecting" and then "Looking for games".
- The selected-game line is at the HEAD OF THE COMMANDS, which is where the widget put it and what
  it belongs to: it names what Join Game would take. It is not watched live - the selection can only
  be changed from the table, so nobody is standing on the line while it changes; verified that
  arriving on it after an Enter in the table reads the new game.
- The adapter gained two members: `GameRow.Entry` (the drawn row) and `Region`, an `IDropList` over
  the region dropdown so the mod's list screen can be opened over the game's own popup.
- NOT declared: a per-row "selected". The menu keeps the chosen game in `_selectedGameNameUniqueId`,
  but the resync factory (`TryCreateActive`) builds the adapter with a null menu, so the fact would
  be right only on a first entry and wrong after every hot reload. The selected-game line names the
  choice instead, as the widget screen did.

Walk: Tab cycles table, commands, region; Right across a row said "status, Can't join. Game is in
progress." then "Players, 2/4"; Down in the Players column said "Wild Mission von Budi (GER), 2/8,
2 of 18" and kept the column; Left said "Game name, KogsonCZ's Epic Song"; Home reached the band's
"Game name" and End "Ekscentryczne przygody gracza GDR., 18 of 18"; `/type yegard` landed on that
game with ONE result; Enter on a row selected it and Tab read "Selected game: Yegard's Fascinating
Myth"; Enter on Region opened the drop list ("Region", then "Europe, selected, 2 of 4"), Russia
emptied the list and Europe brought it back; Host Game opened `OnlineHostGameScreen` and Escape there
came back to the button; `ui_back` on the list answered `consumed`.

Follow-ups, not fixed:

- Opening Host Game leaves the game's own name field holding the keyboard, so the mod stands down
  until something takes the focus off it - the follow-up `OnlineHostGameScreen` already records. It
  bit this walk: `ui_back` answered `standing down` until `GameTextFocus.Release()` was called by
  hand.
- Type-ahead re-announces the landing once per typed character, the engine's behaviour the map select
  entry records.

Manual test:

1. Main menu, Multiplayer, Find online game. Hear "Game List", then the first game and "1 of N".
2. Down and Up walk the games; Right says "status, ..." then "Players, 2/4"; Left comes back.
3. From the Players column, Down says the next game's name, then its count, then "N of 18".
4. Home reaches the heading band ("Game name"); Enter there does nothing - this table does not sort.
5. Type a few letters of a game's name: the cursor lands on it. Backspace ends the search.
6. Enter on a game selects it; Tab to the commands and the first line names it.
7. Tab: the commands (Host Game, Load & Host Game, Join With Game Code, Join Game - "unavailable"
   while the selected game refuses you - Options, Main Menu); Tab: "Region, combo box, Europe".
8. Enter on Region opens the game's own list; pick another region and the list empties and reloads,
   reading "Connecting" then "Looking for games" where there are no games.
9. Host Game opens the host popup; note the field may already hold the keyboard (follow-up above).
10. Escape leaves for the main menu, the same as pressing Main Menu.

### PlayerStatsScreen

Built: three stops. `player-stats-tabs` holds the two tabs; `player-stats-content` is ONE `GraphSheet`
whose REGIONS are the panels the showing tab draws, in drawn order; `player-stats-buttons` holds Back
then Options. Screen name is the page's drawn title ("Player stats"); focus starts on the tab bar, so
arrival says which page is showing before its first line.

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop, Conquest - Overall: the
tabs at y 54 (Conquest - Overall at x 480, Conquest - Battle at x 643); a band at y 94 of three
panels - `GeneralContainer` at x 30 under "General", `FactionContainer` at x 446 under "Factions,
play distribution", `TopMapsContainer` at x 861 under "Top maps, #games"; then
`WieldersAndTroopsContainer` at y 420 under ONE caption, "Top wielder* and troops**", holding the
wielders at x 30 (three entries, then "Wielder max level: 15", "Played wielders: 13/64" and "*Based
on number of times recruited or started with") and the troops at x 674 (three entries, then "Total
units trained: 394", "Different units trained: 13/121" and "**Based on total gold spent on troops").
Back ("Main Menu", x 21) and Options (x 1233) are the main menu's header band. The screenshot
matches.

The tabs switch on ENTER, not on focus, and this is the measurement the proposal asked for:
`PlayerStatsMenuNavigation.HandleSwitchedTab` calls `Show`/`Hide` on the two views WITH AN ANIMATION
(decompiled, lines 77 to 96), so arriving at a tab is not the same event as arriving at its page - the
opposite of the options window, where the switch is instant and free. Verified: Down then Enter on
Conquest - Battle said "selected" and the battle page was declared.

Escape is CLAIMED and presses the drawn Back button: `PlayerStatsMenuNavigation` injects an input
manager and registers no callback on it at all. Verified: `ui_back` answered `consumed` and the main
menu came back.

Diff, tab 0: every before line is accounted for, in five groups.

1. Every table's NAME cell: "Arleon, Faction" became the primary cell "Arleon", and the same for
   every map, wielder and troop.
2. Every figure cell: "Arleon, Rank, #1" became "#1" with "Rank, #1" as its buffer head, and so on
   for Play distribution, Details, Games, Faction, Times recruited or started with and Times trained.
3. The seventeen column headings are GONE: the page draws NO column captions anywhere, so no heading
   band is declared. The captions the widget screen invented are kept as the sheet's columns, which
   is where they are said - on the crossing into a column.
4. The two summary blocks became one row per drawn line: "Wielder max level: 15 / Played wielders:
   13/64 / *Based on ..." was one widget line with three buffer lines and is now three rows of the
   wielders' own region, and the same for the troops'.
5. The three stat tiles lost the line break the prefab wraps their caption on: "Games\nPlayed: 5" is
   now "Games Played: 5". That break is a rendering accident, not two things to say, and the widget
   read it as two buffer lines.

Diff, tab 1: not comparable line by line, and the reason is the capture. The before capture of the
battle tab was taken before the game had filled that menu in, so it holds the prefab's placeholders
("Spellname with very long name that can span over two lines", "name", 345, 111) where this session
has the real figures (53 battles, Clouded Vision cast 17 times, Horned Ones 37 kills). The SHAPE
matches line for line: twelve general lines, a spells table of three rows with Rank and Times cast, its
two summary lines, and an enemy-troops table of three rows with Rank, Faction and Kills - with the
same five kinds of change as tab 0.

Deviations, each measured:

- NO HEADING BAND ANYWHERE. Unlike the map select page and the online game list, this page draws no
  column captions at all: a faction row is a number, a name and a percentage with nothing over them.
  The captions stay the mod's (`ModStrings.UI.Column*`, the words the widget table used) and live
  only as the sheet's column array.
- GENERAL IS A LIST, NOT A TABLE, which is the other measurement the proposal asked for: the page
  draws three stat tiles side by side and then four full-width lines, so its region declares no
  columns and every entry is a line of its own. Making it a two-column sheet would have invented a
  lattice the page does not draw.
- THE WIELDERS AND THE TROOPS SHARE ONE DRAWN CAPTION. `TopWieldersLabel` and `TopTroopsLabel` both
  read `WieldersAndTroopsContainer/Title`, because that is the only caption the game draws over
  either. They are still two regions - their columns differ (Times recruited against Times trained)
  and their rows are different things - and the path diff makes the repeat harmless: Alt+Down from
  the wielders into the troops says only "Faey Queens, 1 of 6", the caption having just been said.
- The summary lines are `GraphSheet.Line` rows of their table's region, so "N of 6" counts three
  entries and three summary lines. That is the owner's model for them and it is what makes End reach
  the footnote rather than the last troop.
- Every label and figure goes through `SpokenLines`, which is what joins a wrapped stat tile back
  into one line.
- The adapter is UNCHANGED: every fact this port needed - the rows, their cells, their
  `RectTransform`s for scrolling, the captions, the tab labels and `ActivateTab` - was already there.

Follow-ups, not fixed:

- The second tab's own label changes with the page: it reads "Conquest — Battle" while the overall
  page is showing and "Battle general" once the battle page is up, because `FindTabLabel` falls back
  to a text the menu re-uses. Present in the before captures too.
- `ActivateTab` invokes the navigation's private `HandleSwitchedTab` rather than clicking the drawn
  tab button, so `UITabGroup`'s own state is not told. Pre-existing, and it has never misbehaved.
- Scrolling into view is the adapter's `ScrollIntoView`, driven from every node's focus visual; the
  harness cannot photograph it, so it is in the manual test.

Manual test:

1. Main menu, Extras, Player statistics. Hear "Player stats", then "Conquest — Overall, tab,
   selected, 1 of 2".
2. Tab: "General, Games Played: 5, 1 of 7". Down through the general lines.
3. Alt+Down: "Factions, play distribution, grid, Arleon, 1 of 6"; again: "Top maps, #games, grid, New
   Beginnings, 1 of 5"; again: "Top wielder* and troops**, grid, Peradine, 1 of 6"; again: the troops,
   which say only the row (the caption is the same one). Alt+Up walks back.
4. In a table, Right names each column on the way in (Rank, Faction, Times trained) and Left comes
   back; from a figure column, Down says the next row's name, then that figure, then "N of 6".
5. End reaches the last footnote ("**Based on total gold spent on troops"); Home reaches the first
   general line.
6. Type a few letters of a wielder, troop or map name: the cursor lands on it.
7. Tab: "Main Menu, button, 1 of 2", then Options. Shift+Tab twice returns to the tabs.
8. Down then Enter on Conquest — Battle: the page switches and says "selected"; its content is the
   battle general lines, the spells table with its two summary lines, and the enemy troops table.
9. Watch the picture on the battle page: the row under the cursor should scroll itself into view.
10. Escape leaves for the main menu, the same as pressing Main Menu.
- The arrival release runs over a window of thirty frames after the screen takes focus, not
  once: the host-game popup selects its name box a frame after the mod's screen arrived, and
  the single release found nothing. Screens that own a `GameTextEditor` answer
  `OwnsGameField` while it is pending or editing, which the window leaves alone. Verified on
  the host popup: keys answered on arrival ("Please enter the name of the game...", then the
  field, then Invite Only).

## Family G: browse pages

### CodexScreen (representative)

Built: four stops. `codex-tabs` holds the eight icon tabs; `codex-articles` holds every category
as a REGION over its own articles; `codex-content` holds the article's body, named after the
article's own heading; `codex-footer` holds Reset Tutorials, Show tutorials and Close while they
are drawn. Screen name is the window's drawn heading ("Tutorials & Codex"); focus starts on the
tab row, so arrival reads "Tutorials & Codex", then "Tutorials, tab, selected, 1 of 8".

Measured 2026-09-06 at 1280x800 through `/gui/unity` and a screenshot crop: the window
`CodexContainer` at [205,85,870,630] drawing a title PAIR - `SubHeader` ("Tutorials & Codex") at
y 106 over `Title` at y 125, which is the showing tab's own name; eight
`CodexCategoryTabButton(Clone)` icons at y 157 (x 360 to 847); `NavigationScrollView` at
[223,206,259,444] holding one `CodexCategorySection` per category, each drawing its name over its
article buttons; `ContentScrollView` at [505,206,522,444]; `TutorialSettings` at [231,657,819,45]
with "Reset Tutorials" (x 264) and the "Show tutorials" toggle (x 466), drawn on the Tutorials tab
only; `CloseButton` at [1032,93,34,34] on every tab. The screenshot matches.

Escape is the GAME's (`ConsumesBack` false): `CodexMenu.Show` registers `InputActions.UI.ExitMenu`
on `Hide` outside its gamepad branch (decompiled, line 108; the gamepad branch adds `UI.Cancel`
beside it). Verified: `ui_back` answered `unclaimed`. The screen's old Cancel handling is gone
with it.

Diff, both captures, and every before line is accounted for in two groups:

1. The category rows left the flat dump (six on Tutorials, seven on Wielders): a category is the
   REGION its articles belong to now, spoken on the way in, which is the options window's rule for
   a caption over rows. The focused category's "selected" went with them - that state was the
   widget's own cursor memory, not a game fact.
2. The content placeholder (`CodexContentWidget (multi-position)`) became one line per drawn
   paragraph. Proven for Level up Wielder: the body draws a `Header` and four
   `CodexTutorialText(Clone)` blocks whose laid-out text (`TMP_Text.GetParsedText`, read through
   `/eval`) holds 1, 3, 1, 2 and 2 paragraphs; the graph declares the header as the stop's name and
   eight text nodes, one per paragraph, in that order.

Everything else the after captures add is the model the owner asked for: the widget listed only
the FOCUSED category's articles, and the graph lists every category's (33 rows on Tutorials, 79 on
Wielders).

Deviations, each measured:

- ARRIVING ON AN ARTICLE DOES NOT SHOW IT, unlike the map select and random layout pages.
  `CodexContentButton` raises its `OnClicked` from `UIButton`'s click and submit paths only;
  `UIButton.OnSelect` plays a hover sound and nothing else (decompiled). So the focus visual is the
  native selection alone (`FocusArticle`) and Enter is what draws the article - verified: Enter on
  Battle Advanced said nothing itself and Tab then read "Battle Advanced, Obstacles, such as
  stakes, ..., 1 of 11".
- THE SHOWING TAB IS NEVER RE-SELECTED NATIVELY. The event system's selection is where the game
  records which article it is drawing (`CodexMenu.HandleContentButtonClicked` sets it), so the tab
  bar's focus visual switches tab only when the tab under the cursor is not the one showing - the
  guard the options window uses for a different reason. Without it, arriving on the tab row took
  the selection off the article and the article stop lost its landing (measured: every
  `ArticleItem.IsSelected` false). The showing tab still has the game's own marker under it.
- The tab bar switches ON FOCUS: `CodexMenu.SetActiveTab` re-spawns the categories and draws the
  first article at once, only the tab marker being tweened (decompiled), so arriving at a tab and
  arriving at its page are one event. Enter does it too. As on the random layout page, the tab just
  arrived on does not say "selected" in the same breath - the focus visual runs after the arrival
  is composed - while the tab the window opened on does.
- The article's body is a stop NAMED after its top-level heading, through a context wrapping the
  whole stop, so entering it always says which article is open once. Every other heading is the
  region its lines belong to; Alt+Down and Alt+Up name each on the way in (verified on Militia:
  "Cost, 100 Gold", "Reload, Troop can perform one ranged attack...", "Sappers, Generates Essence
  of type:: Destruction, 1 of 13"). A heading with nothing under it stays a read-only line, the
  options window's rule for a caption over no rows.
- The essence block reads its label and its amounts as one line (`ModStrings.UI.LabelValue` and
  `JoinList`, exactly what the retired widget composed).
- Reset Tutorials is labelled from the menu's own key (`Options/ResetTutorials`, decompiled
  `CodexTutorialSettings.OnEnable`), NOT off the drawn button: `UIButton.Text` answers the
  unresolved localization token the renderer substitutes as it draws ("_Reset tutorials",
  measured), the same class of gap the loading screen's tip found for action tokens.
- The adapter gained `Title` (the window's drawn `SubHeader`, which the menu never writes - it only
  writes the tab name under it), `ResetButtonLabel`, `ResetButton`, `TutorialsToggle` and
  `CloseButton` as the drawn components a node keys on, and `GetActiveTabIndex` became public. It
  LOST `FocusedCategoryIndex`, `FocusCategory` and `EnsureFocusedCategory`: a "focused category" is
  a widget-tree concept (which slice of the article list to draw), and with every category declared
  there is no slice to choose.
- Close runs the menu's `Hide`, as the widget screen did, which is the same method the drawn
  button's own `OnClicked` is combined with (decompiled `CodexMenu.Initialize`).

Walk (all through `/input` and `/type`): Tab cycles tabs, articles, content, footer; the articles
stop landed on "Level up Wielder", the article the window was drawing; Alt+Down read "Battle,
Battle Advanced, button, 1 of 4" then "Towns and Settlements, Buildings and Build Sites, button,
1 of 9" and Alt+Up came back; Home and End reached the ends of the body (11 lines); Down the tab
row switched the page each time; `/type rally` landed on "Towns and Settlements, Rally Point,
button, 6 of 9" with ONE result; Enter on Show tutorials said "not checked" and Enter again
"checked" (restored); Enter on Close closed the window and the main menu read again; `ui_back`
answered `unclaimed`. Reset Tutorials was never pressed.

Follow-ups, not fixed:

- `CodexMenuAdapter` still normalises every label it reads through `SpeechTextSanitizer.Normalize`
  (the category names, the article names, the content lines, its localized text). Pre-existing and
  against the repo's standing rule; the content lines survive it only because the adapter splits
  the raw text on its newlines BEFORE normalising each piece.
- The essence line reads "Generates Essence of type:: Destruction" - the game's label already ends
  in a colon and the mod's label-value string adds another. Present in the widget screen too.
- Type-ahead re-announces the landing once per typed character, the engine's behaviour the map
  select entry records.
- Activating a footer or tab control takes the native selection off the article, so the articles
  stop's landing is only right until the cursor has been elsewhere; the stop's own remembered
  position covers it after that.

Manual test:

1. Main menu, Extras, Tutorials & Codex. Hear "Tutorials & Codex", then "Tutorials, tab, selected,
   1 of 8".
2. Down and Up walk the eight tabs and the page changes under each; Enter does the same.
3. Tab: "Wielders, Level up Wielder, button, 1 of 5" - the article the window is showing. Down
   walks that category's articles, and past the last one into the next category, naming it.
4. Alt+Down and Alt+Up jump between categories; watch the list scroll itself to the row.
5. Enter on an article: the body on the right changes and nothing is spoken. Tab: the body, which
   says the article's heading, then its first paragraph, then "1 of N".
6. Down and Up read the body a paragraph at a time and the panel scrolls itself; on a unit or
   wielder article, Alt+Down and Alt+Up jump between its headings (Cost, Reload, Starting Troops)
   naming each.
7. Tab: on the Tutorials tab, "Reset Tutorials, button, 1 of 3", "Show tutorials, checkbox,
   checked", "Close, button"; on any other tab, "Close, button" alone. Enter on Show tutorials
   toggles it. DO NOT press Reset Tutorials unless you mean it.
8. Escape closes the window (the game's own key); so does Enter on Close.
