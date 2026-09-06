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
