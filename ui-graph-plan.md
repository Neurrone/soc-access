# UI graph rewrite plan

Self-contained brief for a fresh session. Goal: replace the retained, stack-based widget
tree in `soc-access/ui/` with the immediate-mode graph engine that Endless Space 2 Access
uses, one screen at a time, with both systems live until the last screen is ported. Every
screen the mod supports is listed in §8 with its state or its proposed model.

Prerequisite: the dev server (`docs/dev-loop.md`). This plan uses `/gui/widgets`,
`/gui/graph`, `/gui/unity`, `/input`, `/key`, `/speech`, `/eval`, `/reload` and `run-game.ps1`
throughout.

Resuming in a fresh session: read §0's docs, then §2 (what is built) and §3 (keys), then the
current phase in §7 and its rows in §8, then `docs/dev-loop.md` for the verification loop.
Every screen goes through §4's loop; the before-capture must be taken on the unported build,
before the screen is touched. Commit per logical step and update this file as decisions are
taken; prune it at the end of each phase so it holds only what the remaining work needs.
Phases A to D are done (2026-09-08). Start at phase E.

Owner decisions already made (do not re-ask):

- The owner gives input on every screen: each screen's model is proposed and approved before
  it is implemented. Screens are proposed by FAMILY: one representative per family is
  proposed and approved first, then the siblings are shown against it, and the owner walks
  the representative of each new kind with real keys.
- Types and members are public by default, as in ES2; nothing is declared `internal`.
- Graph screens use ES2's four-arrow key model (§3), and ES2's words for roles and states
  exactly: "unavailable", "checked" / "not checked", "checkbox", "combo box", "tab", "radio
  button", "editable", "table"; positions are announced.
- Escape follows ES2: the game keeps it on its own surfaces wherever it registers its own
  exit action in keyboard mode (`AddInputCallback` for `UI.ExitMenu` outside a gamepad branch
  in the decompiled class; `UI.Cancel` is the gamepad binding); elsewhere the screen claims
  Back and presses the drawn close control; a mod-owned surface always denies the game the
  key. Measured per screen, recorded in the screen's doc comment.
- Text boxes follow ES2: Enter ends the edit and nothing else, Escape restores the pre-edit
  text, the mod echoes typing and says "editing" / "edited" / "Cancelled". Two exceptions: a
  dialog's box submits the dialog on Enter, and the chat box sends (silently). While a game
  box has the keyboard the mod's input layer is silent; a box the game focuses on its own is
  taken back by the mod.
- Always-drawn text (a card's description) reads after the label; hover-revealed text
  (tooltips) is buffer-only. Drop lists walk Up/Down even where the game draws a strip.
  Tables read as tables, a drawn icon per piece under its column, Enter on any cell acts on
  the row. Tabs switch on focus where the game's switch is instant and free, on Enter
  otherwise (measured). A choice among alternatives is a radio group that never chooses on
  arrival.
- The multi-position widgets may have a placeholder in a "before" capture.
- The handover to the owner is ONE SHORT PAGE per phase (§4 step 7). This plan holds only
  what the remaining work needs.
- The mod's own settings are DRAWN dialogs built from the game's parts (done in phase B):
  "Mod options" entries on the main menu and the pause menu, a dialog cloned from the
  options panel, sub-dialogs stacked over it with the layers beneath inert to the mouse; no
  categories are ever added to the game's own options window.

## 0. Read first

In `../endless-space-2-access/docs/generic/`:

- `ui-navigation.md` — the engine: immediate mode, `ControlId`, `GraphTypes`,
  `GraphBuilder` (menu mode, raw mode, contexts, stops, regions, expandable groups),
  `GraphAnnouncer`, `KeyGraph`, `GraphSheet`, `TypeAheadSearch`; then the adapter section
  (navigator, screens, node factories, focus visuals, scroll-into-view); "Rows, columns and
  tables"; "A mode whose cursor is not the focus cursor" (phase E).
- `making-screens-accessible.md` — the per-screen process: measure, propose, approve,
  implement, verify with evidence, hand over.
- `widgets.md` — the widget vocabulary, gesture parity, the keyboard drag (`Carry`, phase
  D), popups and child screens, the confirmation-dialog screen.
- `buffers.md` and `tooltips.md` — sections as the one declaration behind both the tooltip
  announcement and the review buffer.
- `input.md` — the key model and the stand-down doctrine, for the map and combat phases.
- `performance.md` — bounded immediate-mode rebuilds.

Source: the engine under `docs/generic/src/graph-ui/` (already copied, §2); adapter
exemplars to imitate, never copy, under `docs/generic/src/engine-example/` (`GraphNavigator.cs`,
`GraphNodes.cs`, `Screen.cs`, `ScreenManager.cs` for phase F, `PointerFocus.cs`,
`ScrollIntoView.cs`); live ES2 screens under `../endless-space-2-access/ES2Access/Screens/`
(`GalaxyHudScreen*.cs` and `GalaxyInspect*.cs` for the map mode, `BattleTacticsScreen.cs`
and `AdvancedEncounterPlayScreen*.cs` for combat, `HeroInspectionScreen.cs` and
`ShipDesignScreen.cs` for inventories with `Carry`).

## 1. What still stands of the widget engine

Paths relative to `soc-access/`. This is what the unported screens (§8) are built on.

- `screens/Screen.cs` is the base of both engines: `IsPresent()`, `OnPush/OnFocus/OnUnfocus/
  OnPop`, `Update`, `HasClaimed`, `OnActionJustPressed`, `CurrentTooltip`.
  `screens/ScreenManager.cs` is a push/pop stack (`Push`, `RefreshTop<T>`, `PushBelowTop`,
  `PushBottom`, `Pop<T>`, `Remove<T>`, global actions); `RefreshTop` lets a graph screen adopt
  the cursor and the spoken memory of the instance it replaces (`GraphNavigator.Adopt`,
  `GraphScreen.ArrivedByRefresh`), so a refresh neither re-seats nor repeats the name (the incoming `ScreenName` must be what
  the outgoing instance spoke, `GraphScreen.SpokenName`).
- `screens/ScreenDetector.cs` is the readiness layer: about 150 `On*Ready` / `On*Changed` /
  `On*Closed` handlers called from `patches/*Patches.cs`; `ResyncFromRuntimeState` rebuilds
  the stack after a hot reload by asking each registered factory's screen `IsPresent()`;
  `_storySequenceActive` is the flag behind `StoryFocusBlockerScreen`. Its knowledge is the
  most expensive thing in the repo to lose: in phase F move it, never rewrite it.
- `ui/UIManager.cs` and `ui/Widget.cs` are the widget focus engine; the widget kinds still
  in use by the two unported screens are `ContainerWidget`, `MenuWidget` + `MenuItemWidget`,
  `ButtonWidget`, `TextWidget`, `CombatHexGrid`, `TroopPlacementHexGrid`. `AdventureMapGrid`
  + `TileSkipNavigator` still derive from `Widget` but are in no widget tree any more (the map
  is ported); `TroopHudMenu` is dead. Every other `ui/*Widget.cs` is dead code for G. `Portrait` is still
  read by ported screens as a native-portrait reader. `TextInputEchoHelper` survives as the
  graph editor's echo. `TooltipActionsMenuScreen` (Backquote) stays until no unported screen
  hands out a `TooltipAction`.
- Widget-era input actions (`input/AccessibilityActions.cs`): `next_widget`, `next_menu_item`,
  `activate`, `cancel`, `start_drag`, `slider_*`, the map, combat, scanner and bookmark sets;
  the physical bindings are in `input/KeyboardBinding.cs`. The input stand-down for a
  focused game text box applies on graph screens only (`AccessibilityInputRouter.StandingDown`)
  because widget-era text inputs rely on the mod's own keys to leave a field; lift it in G.
- Review buffers (`buffers/`, `ReviewBufferKind.Ui/AdventureMapNotifications/CombatEvents`)
  and speech (`SpeechPipeline.Output`, silenced by the router on every claimed key) stay as
  they are. Localization: every `ModString` costs `update-pot`, 13 `.po` translations and
  `validate`; batch per phase. Tests are MSTest under `tests/`.
- Every adapter still normalises text with `SpeechTextSanitizer.Normalize`, which collapses
  newlines; the graph cleans tooltip and details lines itself (`ui/SpokenLines.cs`). The
  sweep is phase G's.

## 2. What exists now: the graph side

Paths relative to `soc-access/`. Read these before porting a screen.

- `ui/graph/` — the engine, 20 files copied from ES2, namespace `SongsOfConquestAccess.UI.Graph`.
  Changed only where the repo's rules required (`public`, `ModText`, `NodeHint.Template` a
  `ModString`). Never edit these for a screen's needs; re-sync against ES2 instead. Tests
  under `tests/` (`Graph*Tests`, `KeyGraphTests`, `GraphSheetTests`, `TypeAhead*Tests`, ...)
  with `tests/GraphFixtures.cs` as the helper.
- `screens/GraphScreen.cs` — the bridge. A screen ports by deriving from it, dropping its
  widget tree, and writing `Key`, `Build(GraphBuilder)`, `IsPresent()`, and optionally
  `ScreenName`, `InitialFocusStop`, `Back()`/`ConsumesBack`, `IsWorkable` (mutes the live
  watch while the page fades and silences the re-seat when the focused control vanishes with
  the page), `AllowsTypeahead`, `CapturesRawInput`, `OwnsGameField` (a screen whose own
  editor holds or awaits a field; otherwise a field the game focuses on its own is released
  every frame), `TypeAheadScope`, `OnFocusVisual`. Constructor sites, detector handlers and
  `IsPresent()` stay as they were; only the class body changes.
- `ui/GraphNavigator.cs` + `.Search.cs` — the adapter: one `GraphState` per screen instance,
  `Attach`, `Adopt`, `Claims`, `Dispatch`, `Update` (type-ahead tick then `EnsureFocus`, the
  single site that announces, fills `ReviewBufferKind.Ui`, draws the native tooltip and runs
  the live-part watch; a recovery onto a survivor is silent while the screen is unworkable),
  `FocusNode` (pending landings), `InspectRender` (the dump), `FocusedTooltip`. A focus
  visual is re-drawn only when what it draws changes (`SameAim`). Static wiring in
  `InstallWiring`/`ResetWiring`; `FocusedIndex(prefix)` for a pager's page. The live watch
  re-reads a node whose live part count changed (a dialogue's next line). Carry is wired: one `CarryState`
  per navigator handed to the engine as `GraphAnnouncer.Carry`, `ui_carry`, `ui_left_click`
  and `ui_back` dispatched through `CarryActions`, the owner's page answered off the screen
  stack (a child screen over it is still it), `ui/CarrySounds.cs` the seam a screen registers
  the game's own drag noises for its cargo kind on, `input/ChordNames.cs` installed as
  `NodeHints.Chord`. Modes (phase E): `GraphScreen.ModeClaims(action)` is asked BEFORE the
  navigator's own set in both `Claims` and `Dispatch` (after a live search, which is innermost),
  and an action it answers runs through `OnAction`; a screen answers it only while its mode node
  is focused. `ui/PointerHover.cs` simulates the pointer hover a card
  reveals its detail on, released in `Stop()`.
- `ui/GraphNodes.cs` — the factories, every one taking the same cross-cutting parameters:
  `Button`, `Group`, `Text`, `EditField`, `Checkbox`, `Slider` (Left/Right adjust; an optional
  activation, used for a slider's drawn value box), `ComboBox`, `Tab`, `Radio`, `Choice`, `Paragraphs` / `ParagraphParts` (a body of text as
  one part per paragraph, `live` where the game replaces it in place under a still cursor); the
  parts (`LabelPart`, `DisabledPart`, `ValuePart`, `SelectedPart`), `TooltipSection` (every
  native tooltip is an `Indicate` section, buffer only; `Aim` makes focus draw it;
  `DoNotDrawTooltip` for an edit control, since drawing selects the component and takes the
  keyboard off the field), `ActedState` (a refused activation says nothing). Detail and
  tooltip lines pass through `ui/SpokenLines.cs` (tags stripped after splitting on newlines).
- `ui/ControlTypes.cs` — the role registry: `Button`, `Group`, `Text` (no word), `EditField`,
  `Checkbox`, `Slider`, `ComboBox`, `Tab`, `RadioButton`. A new type needs a role `ModString`.
- Tables: each screen declares its `GraphSheet` directly: the drawn heading band as a menu row
  of the table's stop (headings stamped with `NodeVtable.Column` and `SearchesAsItself`), one
  region per drawn caption, `RowAt` per row with the primary cell first and metadata cells as
  `SheetCell(column, piece, vtable)` carrying a `BufferHead`; the stop's Tab landing pinned
  with `builder.LandStopOn(sheet.FirstRow)`. A cell may carry a real control's vtable (the
  lobby's player rows). The first seating is the start node's, not `InitialFocusStop`'s: a
  landing in the stop holding the start node needs `SetStart` beside `LandStopOn`. A stop is
  named after live content by wrapping it in one `PushContext(...)`.
- `ui/TroopHudRows.cs` — the shared troop rows: one row per drawn UNLOCKED slot of a `TroopHUD`
  in drawn order (a locked slot is not a row) and a `WielderStop` helper that adds the portrait
  row (the wielder's name alone; a settlement's or merchant's banner goes to the screen name)
  above a region named by the game's Troops word, with the carry (cargo `troop`, the game's
  drag replayed through `TroopHudAdapter` including its Ctrl branches), the game's clicks
  (Backslash = disband where the game would take the right click) and its Ctrl+digit quick
  splits (`troop_split_1..10`, answered through `GraphScreen.ClaimsAction`/`OnAction`, the
  screen-level action hook phase E's modes also use); over `adapters/WielderInteract.cs`. The
  map's HUD stop calls `Rows` alone.
- `ui/ArtifactSlotNodes.cs` over `adapters/IArtifactSlots.cs` — the Equipment and Inventory
  stops (Both Hands merge, positions, Auto arrange first, cargo `artifact`, `DropAccepts` off
  the game's `CanRearrangeArtifact`); each screen passes its own click meanings and hints.
  `ui/CommanderBands.cs` (stats band, modifier tab row + rows), `ui/SettlementNodes.cs`
  (defending-wielder band, garrison lines), `ui/RecruitGroups.cs` + `ui/ResourceCosts.cs`
  (a recruit or upgrade card as a collapsed group, or a line when it has no children).
- Input rule since D: a character typed in the frame the focused screen changed is dropped
  (`GraphNavigator.HasTicked`, the router's `TypingScreen`), so the game hotkey that opens a
  graph screen is never typed into its search.
- `screens/DropListScreen.cs` — the mod-owned child screen every combo box opens over the
  game's real dropdown popup (`adapters/IDropList.cs`, `adapters/DropdownPopup.cs`): `Choice`
  nodes Up/Down landing on the current value; Escape claimed.
- `ui/GameTextEditor.cs` + `input/GameTextFocus.cs` — the edit field: the screen-owned editor
  (deferred handover until Enter is released, "editing", the echo, "edited"/"Cancelled";
  `RequestSilentEnd` for the chat) and the stand-down the router asks before every claim,
  typed character and injection (`standing down`).
- The mod options: `adapters/ModOptionsEntries.cs` (the two drawn entries, made lazily,
  removed by name in `Stop()`), `ui/ModDialog.cs` (a dialog cloned from the live options panel
  or the lobby settings popup, rows drawn by the game's `MenuFactoryController` with mod text
  as the key, a blocker behind it and the layer beneath non-interactable), `screens/ModOptionsScreen.cs`
  and `ModDialogScreen.cs` reading them through the shared readers `adapters/MenuRows.cs`
  and `ui/MenuFormNodes.cs` that `OptionsScreen` also uses; `SocAccessMod.OpenModOptions()` is
  the one door for the clicks and Ctrl+M.
- `input/AccessibilityActions.cs` — the graph actions, all `InputClaimScope.Screen`:
  `ui_up/down/left/right`, `ui_coarse_decrease/increase` (Shift+Left/Right), `ui_next/prev`
  (Tab), `ui_home/end`, `ui_region_prev/next` (Alt+Up/Down), `ui_left_click` (Enter, NumpadEnter,
  Ctrl+Enter, Ctrl+NumpadEnter), `ui_carry` (Space),
  `ui_clear_search` (Backspace, live during a search), `ui_right_click` (Backslash,
  Ctrl+Backslash), `ui_back` (Escape); the router claims letters (and Space mid-search) for type-ahead on graph screens.
- `dev/GraphDump.cs` — `/gui/graph?buffers=1&flat=1&edges=1`, `/gui/tree`, `POST /type`;
  `/status` reports the focused node as `focusedWidgetId`/`focusedWidgetType`.
- Dev-loop guards added in phase B: a failed `/eval` no longer breaks the game's type scans
  (`patches/DynamicAssemblyTypesPatches.cs`, dev-only); a reload logs posted-work failures.

Exemplars, one per kind, all approved and walked: menu page `screens/CampaignMenuScreen.cs`
(header band + cards, drawn-order sort, `IsWorkable`); dialog `MessageDialogScreen.cs` (the
three-part contract, per-source Escape, an edit field); form `OptionsScreen.cs` (tabs, regions
per caption, rows, scroll-into-view through native selection); table
`AdventureLobbyMapSelectScreen.cs` (filters stop first, a sheet with pieces, a details node);
browse page `CodexScreen.cs` (a list stop with regions, a content stop named after its
heading); a table of control cells `AdventureLobbyPlayersScreen.cs`; radio group
`AdventureLobbyRandomLayoutScreen.cs`; chat `ChatScreen.cs`; mode-less loading page
`LoadingCompleteScreen.cs`.

## 3. How the navigator handles keys

`Navigator.Claims` answers the router before the press; `Dispatch` runs the action. On any
graph screen the navigation set is always claimed; the rest only where the focused node or
screen answers it, so an unclaimed key still reaches the game.

| Action | Claimed when | What it does |
|---|---|---|
| `ui_up` / `ui_down` | always | `Move` Up / Down; while a search is live, step its results |
| `ui_left` / `ui_right` | always | `OnAdjust` if the node has one, else `Move`, else `TreeLeft`/`TreeRight` (ascend+collapse / expand+descend); consumed silently on a leaf |
| `ui_coarse_decrease` / `ui_coarse_increase` | node has `OnAdjust` | `OnAdjust` with the large step |
| `ui_next` / `ui_prev` | always | `MoveStop`, wrapping; one stop consumes silently |
| `ui_home` / `ui_end` | always | `MoveToSiblingEdge` in a tree, else `MoveToEdge` along the stop's wired axis; in a search, first/last result |
| `ui_region_prev` / `ui_region_next` | node has a region | `MoveRegion` |
| `ui_left_click` | always | a drop where something is being carried and the node takes it, else `OnActivate` then `StateText` interrupting |
| `ui_carry` (Space) | the node has `OnPickUp`, or something is being carried | pick up, swap what is held, or consume silently |
| `ui_clear_search` (Backspace) | a search is live | ends the search, "Search cleared" |
| `ui_right_click` (Backslash) | node has `OnContextual` | `OnContextual`, the right-click command |
| `ui_back` | something is being carried, `Screen.ConsumesBack`, or a search is live | cancel the carry, else `Screen.Back()`; in a search, "Search cleared" |
| letters, Space mid-search | `AllowsTypeahead && !CapturesRawInput`, no Ctrl or Alt held, no game box focused | type-ahead over the focused stop plus the fully-open build |

Still to add: the mode keys (phase E), answered through the existing screen-level hook
`GraphScreen.ClaimsAction`/`OnAction` the quick splits use. `troop_split_1..10` (Ctrl+1..0)
are claimed only while a troop row is focused.
Type-ahead ranks by match tier before list order; a chord is never typing; a group header
the game wires no click to gets no `OnActivate` (Right is the way in). `GraphState` is keyed by
screen instance, so cursor memory across a push and pop is lost until phase F's registered
singletons restore it.

## 4. The dump-and-diff loop (per screen)

1. **Before.** On the unported build, open the screen in-game, then `GET /gui/widgets?flat=1&buffers=1`
   and `GET /gui/widgets?buffers=1` to `walks/before/<Screen>[-variant].txt` and `-tree.txt`.
   Capture each variant (tabs, modes, empty and full states). Placeholders for multi-position
   widgets are expected.
2. **Propose.** Measure the game's own layout (`/gui/unity` rects and a cropped screenshot,
   plus the decompiled view classes), write the model (stops, regions, sheets, groups, which
   controls merge into one node, Escape from the decompiled input registrations), and get the
   owner's approval, by family (§7). Nothing is written before that.
3. **Implement.** Change the base class to `GraphScreen`, write `Build`, delete the widget
   construction. Touch the adapter only for a missing game fact.
4. **After.** `dotnet build`, `POST /reload`, confirm `modAssemblyName` incremented, reopen the
   screen, `GET /gui/graph?flat=1&buffers=1` to `walks/after/<Screen>[-variant].txt`.
5. **Diff.** `sort -u` both and `diff`. Every before-line absent after is a miss unless it is a
   placeholder or the approved model dropped it; explain every difference in the commit.
6. **Walk.** `POST /input` through every stop and a sample of nodes; `/speech` must read as the
   tree dump reads. Activate one control per kind through the game's own click path. Check the
   picture against the mod with a cropped screenshot (`crop-shot.ps1`) and read it. End with real
   keys (`POST /key`) for anything an injection cannot exercise (typing, held keys, Escape).
7. **Hand over.** ONE SHORT PAGE per phase, never per screen: which screens to test, what to
   watch for, the decisions taken, and what needs the owner's attention. No key-by-key steps
   and no expected speech; per-screen detail (measurements, deviations, diff verdicts,
   follow-ups) goes in the commit message and the screen's doc comment. The owner tests; the
   screen is done when they say so.

`walks/` is gitignored. Injected actions never press a physical key: the stand-down, the
release debounce and the game's own key handling are only proved with `/key` or a hand on the
keyboard. `/key` refuses while the game window is not in the foreground (a locked desktop).

## 5. Widget kind to graph model (the kinds still to port)

| Today | Graph model |
|---|---|
| `ContainerWidget` with `AnnounceName` | `PushContext(label)` or a `BeginStop` when it is a panel the player tabs to |
| `MenuWidget` + `MenuItemWidget` | menu mode: one node per item |
| `ButtonWidget` | `Button` |
| `TextWidget` heading / body | region name, never a node (unless it carries a tooltip) / read-only `Text` node with `Sections` |
| `TroopHudMenu` | `TroopHudRows.Rows` in the map's HUD stop |
| `AdventureMapGrid` + `TileSkipNavigator` | a MODE: one node on a map stop whose handler owns the tile cursor; the grid class survives, wrapped |
| `CombatHexGrid`, `TroopPlacementHexGrid` | the same mode shape |
| `Portrait` | an announcement part, not a node |

## 6. Localization

Each phase adds its screen names, role words and other `ModString`s in batches (per
subagent run at most): `update-pot`, real translations in all 13 `.po` files, `validate`.
Never leave English placeholders.

## 7. Phases and order

Order rationale: out-of-game screens first (done), then the in-game menus and forms with the
factories proven, then the composite grids (carry, two-sided sheets), then the three modes,
then the manager swap once the map and combat predicates are graph screens, then cleanup.
Within a phase the order is by kind (menus, dialogs, forms, tables); the first screen of each
new kind goes to the owner's real-key walk before its siblings are batched. Each phase ends
with the localization batch, this file pruned, and a one-page handover.

### Phase A — engine, bridge, main menu (done)

### Phase B — every screen outside a running game (done)

All 33 screens.

### Phase C — in-game menus, popups, forms and tables (done)

Twenty screens, 2026-09-07 (§8). `SpellbookScreen` and `WorldChoiceMenuScreen` moved to phase
D, where drag is introduced. Rules learned, for every later port: a `ControlId` is equal on
its structural key alone, so rows built under one key with different components collide
(index the key); a tooltip that is the SOURCE of a node's parts is not also a section (it
would read twice); a screen whose start node is its heading gets no `ScreenName`; a drawn
band is named by pushing its drawn header as the context of its stop, not by a heading node;
a popup with no name of its own has `ScreenName` null and lands in its first named stop; a
text the game replaces in place (a dialogue) is one node of live parts, `{speaker}: {text}`,
so Enter's advance is read by the watch; a list of allies or players is an expandable group
per row whose children are its facts then its actions; a summary with rows of counts is
lines under named regions, not a table, unless the game draws column headings.

### Phase D — composite grids (done)

Fourteen screens, 2026-09-08 (§8). Rules learned, for every later port: a stop that names no
landing opens on the alternative in force, and a named landing (`LandStopOn(id)`) wins over
it, so a stop whose tail is a tab bar names its landing and keeps the bar a region rather than
a stop of its own (the sheet's and the trade's modifier bars); a
group with nothing to open is a line; an empty slot is a role-less `Text`
node that still takes a drop; Enter and Backslash are the game's left and right clicks with
the Ctrl chords as further bindings of the same actions, so the game's own handlers read the
physical Ctrl; a hint is offered only where the game would take the gesture (a button the game
draws non-interactable refuses its click); a mod-authored drop target exists only where the
game's own gesture is a release over nothing, and reads as a short instruction; a refresh the
game runs only on mouse-over (a tooltip recomposed on hover) is run through the same private
method before the mod reads it; a wielder band inside a menu whose canvas group the game
never enabled refuses every click, so a REPL-opened prompt variant is not a fixture.

### Phase E — modes

Remaining: `PreBattleMenuScreen` (troop placement hex grid; smallest mode, first) and
`CombatScreen` with `CombatTroopCycle` (combat hex grid, timeline, troop cycling, threat).
The grid classes survive as the mode's cursor; the side panels become stops, and the mode node
owns its keys, its buffer and its exit announcement. A battle is reached by walking the map
cursor onto a neutral army and pressing the right-click action twice; the post-game pages by
Surrender in the pause menu (a fresh random skirmish).

`AdventureMapScreen` is done (2026-09-08) and is the worked example of the shape: ONE node
with a fixed `ControlId` on the mode's stop, so the navigator never announces a move and the
cursor class speaks each landing itself (queued, not interrupting - the router has already
silenced the reader for the key, and a skip's "Skipped N tiles" must precede the tile). The
review buffer refills on its own because the node's readout changed, so no buffer override was
needed. `ModeClaims` answers the cursor's whole key set while its node is focused AND
translates the graph's own navigation keys onto it (`ui_up` to the move, `ui_home` to the
scanner jump), so an injected key behaves exactly as the physical one the router resolves to
the mod's map action first.

Approved model for `PreBattleMenuScreen` (2026-09-08), a sibling of `PostBattleResultScreen`
(same `AdventureBattleMenu` frame): screen name stays "Troop placement"; stops Attacker (the
commander line with the portrait tooltip, then a threat-level line with the scouting tooltip
when drawn on that side), Defender (name line, "Threat Level – ..." line with the scouting
tooltip, scouting information line when drawn), the grid (a mode node named by the instruction
text; hex moves, skips, Ctrl+Space, scanner families, Home/End/Backspace through `ModeClaims`;
the carry replaces the widget drag: Space picks up an own troop, Enter drops through the game's
grab-and-drop, a refused drop says the existing "Invalid destination", Escape cancels; a
passive watcher speaks a changed instruction queued), Buttons (Withdraw, Manual Battle, Quick
Battle, Ready when drawn, by drawn left edge), and a final stop holding the drawn "Drag troops to
rearrange" hint as a line (owner ruling). Start node the attacker line. Escape is the game's
(no exit action registered; Withdraw has side effects). Type-ahead is on for the whole screen
EXCEPT while the grid node is focused (`AllowsTypeahead` answers per focused node); the map's
screen-wide off was for the map's own letter hotkeys, which this screen has only on the grid.

Approved model for `CombatScreen` (2026-09-08). Screen name stays "Combat". Stops, in this
order: Battlefield (the mode node, named by the existing combat grid string or by the spell /
ability targeting instruction while aiming; one node with a fixed id; label = the adapter's
tile description with the inspect context, buffer = the inspect tooltip; `ModeClaims` takes the
hex moves and skips, Ctrl+Space, I inspect, the comma/period acting and enemy troop cycles,
Space to the acting troop, W relevant tiles, T to the turn order stop, S threat, the scanner
families with Home/End/Backspace; Enter confirms a target while aiming and is silent
otherwise (the game binds no confirm key in battle); Backslash is the game's right click;
type-ahead off on this node only; inspect is a sub-mode, Escape claimed only while inspecting
or aiming, cancelling through the adapter as today); Quickbar when drawn (the spell slots down
the left edge); Attacker (player name when drawn, portrait line with tooltip, Auto Battle,
Essences region, Spells or Cancel spell, which the game draws in the Spells spot while aiming);
Defender, same shape, when a wielder is drawn; Current troop (a line, Enter moves the cursor to
it; its ability button or Cancel ability as the next row when drawn); Turn order (the queue
with round markers, Enter on a troop moves the cursor; T lands here); Game menu (Chat, Game
Menu); Battle log when drawn; End turn alone. Escape on a HUD stop returns to the battlefield
with today's sound; on the battlefield otherwise it is the game's. Spell cast and ability
targeting land focus on the battlefield silently and speak the instruction; the queue change is
free under immediate mode; `CombatTroopCycle`, the narrator and the combat events buffer stay.
Multiplayer follow-ups, not ported: the turn timer beside End Turn and the per-side player
message panels (textless or unmeasured; need a multiplayer fixture).

### Phase F — the screen manager swap

1. Replace `ScreenManager` with ES2's poll-and-diff manager: registered singleton screens,
   `Layer`, `IsActive()` polled every frame, insertion-sorted, diffed, one focus-change
   site, child screens (`PushChild`) for the mod-owned surfaces (`DropListScreen`, the mod
   options dialogs).
2. Every screen's `IsPresent()` becomes `IsActive()`. Screens that receive the native menu
   instance in their constructor read it from an adapter static the existing `On*Ready` /
   `On*Closed` handlers write; `ScreenDetector` shrinks to those writes and to flags with no
   game-side state (`_storySequenceActive`, the community-maps refresh flags).
   `ResyncFromRuntimeState`, `PushBelowTop`, `PushBottom`, `RefreshTop` go away.
3. Layers: map and combat 10; in-game panels and lobby 20-40; `MessageDialogScreen` 100
   with `AnswersOnly`; story text and letterbox at a cutscene layer above the panels; the
   loading screen above everything. `AdventureMapScreen.IsActive` gates on no popup, no
   story sequence, no loading; `StoryFocusBlockerScreen` is deleted.
4. `KeepStateOnPop` on the map, combat and settlement screens; cursor memory across push
   and pop returns with the singletons.
5. Verify with `/gui/graph?screen=KEY` for every registered screen, the dialog-over-map case,
   and the story sequence gap.

### Phase G — cleanup

Delete `ui/UIManager.cs`, `ui/FocusContext.cs`, every `ui/*Widget.cs`, `ui/MenuWidget.cs`,
`ui/TableWidget.cs`, the three grid classes' widget base once their mode wrappers own them,
`TooltipActionsMenuScreen` with its Backquote action, `/gui/widgets`, the widget-era input
actions, and `adapters/NativeTextPrompt.cs` (unused since the mod dialogs). Sweep every
adapter's `SpeechTextSanitizer.Normalize` into per-line handling (`ui/SpokenLines.cs` is the
shape). Lift the graph-screens-only limit on the input stand-down. Update `AGENTS.md` (the
adapter rule stays; widget-tree wording becomes graph wording) and `screens/README.md`.

## 8. Screen inventory

Ported (one line each): `MainMenuScreen`, `CampaignMenuScreen`, `TaleSelectScreen`,
`CustomCampaignSelectScreen`, `AdventureLobbyMapTypeScreen`, `AdventureLobbyInviteProvidersScreen`
(unverified, needs two providers), `MessageDialogScreen` (three of seven sources verified),
`QuitToDesktopPopupScreen`, `PlatformUserMenuScreen`, `CommunityMapsModalScreen` (login
variants unverified), `LoadingCompleteScreen`, `OptionsScreen`, `AdventureLobbyRandomLayoutScreen`,
`AdventureLobbyGameSettingsScreen`, `AdventureLobbyPlayerSettingsScreen`,
`OnlineHostGameScreen`, `CommunityMapsSearchFilterScreen`, `DropListScreen` (new),
`AdventureLobbyIconDropdownScreen`, `AdventureLobbyMapSelectScreen`,
`AdventureLobbyChallengeMapSelectScreen`, `OnlineGameListScreen`, `PlayerStatsScreen`,
`CodexScreen`, `SaveLoadGameScreen`, `CampaignMapSelectScreen`,
`CommunityMapsHomeScreen`, `CommunityMapsCollectionScreen`, `CommunityMapsSearchResultsScreen`,
`CommunityMapsDetailsScreen`, `AdventureLobbyPlayersScreen`, `ChatScreen` (the in-game
recipient selector needs a multiplayer game, unverified), `ModOptionsScreen` and
`ModDialogScreen` (new; the nine widget-era settings menus are deleted). `FoldoutMenuScreen`
was deleted in A. Phase C: `PauseMenuScreen`, `WorldConfirmMenuScreen`, `ClaimMenuScreen`,
`TutorialSimpleScreen`, `TutorialSlideshowScreen`, `StoryTextScreen`, `LevelUpScreen`,
`PurchaseWielderScreen`, `ResearchScreen`, `BuildMenuScreen`, `OwnedEntitiesScreen`,
`TroopOverviewScreen`, `MapEntityMiniMenuScreen`, `AdventurePlayerMenuScreen`,
`GiftTownPopupScreen`, `SendResourcePopupScreen`, `MarketplaceScreen`, `PostBattleResultScreen`,
`PostAdventureResultScreen`, `PostAdventureStatsScreen`. Phase D: `CommanderSheetScreen`,
`SpellbookScreen`, `MoveTroopPopupScreen`, `WorldChoiceMenuScreen`,
`ArtifactMarketScreen`, `TradingScreen`, `HostileJoinMenuScreen`, `SettlementScreen`,
`DefenceMenuScreen`, `TroopManagementScreenBase`, `DraftTroopsScreen`, `UpgradeTroopsScreen`,
`RallyPointScreen`. Phase E: `AdventureMapScreen`. Unverified corners: `MessageDialogScreen`'s random event and custom
message sources (no reachable state produced them), a recruit card's essence tab row, and on
the map the teleport mode, the town list, the chat button and the bug report button (no
reachable state drew any of them).

Remaining, with what the file constructs today and the proposed model (a proposal, not a
decision, until the owner approves it; phase E's three are proposed by family as before):

| Screen | Widgets today | Proposed model | Phase |
|---|---|---|---|
| `TooltipActionsMenuScreen` | Menu | stays a widget screen until no unported screen hands out `TooltipAction`s; deleted in G | G |
| `PreBattleMenuScreen` | TroopPlacementHexGrid, Buttons, Text | mode node plus buttons stop | E |
| `CombatScreen` (+ `CombatTroopCycle`) | CombatHexGrid, CombatTroopCycle, Menu, Buttons, Text | mode node, timeline stop, actions stop | E |
| `StoryFocusBlockerScreen` | Container | deleted in F; becomes a predicate | F |

## 9. Risks

- `ScreenDetector`'s readiness knowledge is the most expensive thing in the repo to lose. In
  phase F, move it, never rewrite it from memory.
- Each phase's localization batch is real work; a phase is not done until `validate` passes.
