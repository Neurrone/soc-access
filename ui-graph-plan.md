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
Phases A and B are done (2026-09-06). Start at phase C.

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
  `GraphScreen.ArrivedByRefresh`), so a refresh neither re-seats nor repeats the name.
- `screens/ScreenDetector.cs` is the readiness layer: about 150 `On*Ready` / `On*Changed` /
  `On*Closed` handlers called from `patches/*Patches.cs`; `ResyncFromRuntimeState` rebuilds
  the stack after a hot reload by asking each registered factory's screen `IsPresent()`;
  `_storySequenceActive` is the flag behind `StoryFocusBlockerScreen`. Its knowledge is the
  most expensive thing in the repo to lose: in phase F move it, never rewrite it.
- `ui/UIManager.cs` and `ui/Widget.cs` are the widget focus engine; the widget kinds still
  in use by unported screens are `ContainerWidget`, `MenuWidget` + `MenuItemWidget`,
  `ButtonWidget`, `CheckboxWidget`, `SliderWidget`, `TextWidget`, `TextInputWidget`,
  `TableWidget`, `DraggableMenuWidget`, `InventoryGridWidget`, `ArmyExchangeGridWidget`,
  `AdventureMapGrid` + `TileSkipNavigator`, `CombatHexGrid`, `TroopPlacementHexGrid`,
  `TroopHudMenu`, `Portrait`. `TextInputEchoHelper` survives as the graph editor's echo.
  `TooltipActionsMenuScreen` (Backquote) stays until no unported screen hands out a
  `TooltipAction`.
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
  `InstallWiring`/`ResetWiring`. Not wired yet: carry (phase D), modes (phase E), pointer
  hover simulation.
- `ui/GraphNodes.cs` — the factories, every one taking the same cross-cutting parameters:
  `Button`, `Group`, `Text`, `EditField`, `Checkbox`, `Slider` (Left/Right adjust; an optional
  activation, used for a slider's drawn value box), `ComboBox`, `Tab`, `Radio`, `Choice`; the
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
  (Tab), `ui_home/end`, `ui_region_prev/next` (Alt+Up/Down), `ui_activate` (Enter),
  `ui_clear_search` (Backspace, live during a search), `ui_right_click` (Backslash), `ui_back`
  (Escape); the router claims letters (and Space mid-search) for type-ahead on graph screens.
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
| `ui_activate` | always | `OnActivate`, then `StateText` interrupting |
| `ui_clear_search` (Backspace) | a search is live | ends the search, "Search cleared" |
| `ui_right_click` (Backslash) | node has `OnContextual` | `OnContextual`, the right-click command |
| `ui_back` | `Screen.ConsumesBack`, or a search is live | `Screen.Back()`; in a search, "Search cleared" |
| letters, Space mid-search | `AllowsTypeahead && !CapturesRawInput`, no Ctrl or Alt held, no game box focused | type-ahead over the focused stop plus the fully-open build |

Still to add, each in the phase that needs it: `ui_carry` (Space, phase D), the mode keys
(phase E, the mode node's own handler, claimed through a screen-level `AnyKey`-style hook).
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
| `ButtonWidget`, `CheckboxWidget`, `SliderWidget` | `Button`, `Checkbox`, `Slider` |
| `TextWidget` heading / body | region name, never a node (unless it carries a tooltip) / read-only `Text` node with `Sections` |
| `TextInputWidget` | `EditField` driven by a screen-owned `GameTextEditor` |
| `TableWidget` | `GraphSheet` as §2 describes |
| `DraggableMenuWidget` (spellbook) | menu rows plus `Carry` for the reorder |
| `InventoryGridWidget` | `GraphSheet` of slots plus `Carry` for move/equip; slot tooltips via `Sections` |
| `ArmyExchangeGridWidget` | one `GraphSheet` per army with troop slots as cells, `Carry` between them; split/merge through activation opening `MoveTroopPopupScreen` |
| `TroopHudMenu` | a stop of troop rows on the map screen |
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

All 33 screens. Verified in phase C on shared
classes: the map message, random event, custom message and dialogue sources of
`MessageDialogScreen`, the save variant of `SaveLoadGameScreen`, the in-game chat (its
recipient selector) and codex, the mod options dialog opened from the pause menu.

### Phase C — in-game menus, popups, forms and tables

`PauseMenuScreen`, `WorldChoiceMenuScreen`, `WorldConfirmMenuScreen`,
`TutorialSimpleScreen`, `TutorialSlideshowScreen`, `StoryTextScreen`,
`AdventurePlayerMenuScreen`, `ClaimMenuScreen`, `GiftTownPopupScreen`,
`SendResourcePopupScreen`, `MapEntityMiniMenuScreen`, `OwnedEntitiesScreen`,
`TroopOverviewScreen`, `LevelUpScreen`, `PurchaseWielderScreen`,
`PostAdventureResultScreen`, `PostBattleResultScreen`, `ResearchScreen`,
`MarketplaceScreen`, `BuildMenuScreen`, `SpellbookScreen`, `PostAdventureStatsScreen`;
plus the phase B verifications listed above. Families to propose: in-game menus (pause menu
representative), in-game dialogs (world confirm), text pages (story text, tutorials),
in-game forms and lists (level up, research, build menu), in-game tables (post-adventure
stats). The pause menu's "Mod options" entry is already drawn and must stay a row of it.

### Phase D — composite grids (adds `Carry` and two-sided sheets)

`CommanderSheetScreen` (inventory), `ArtifactMarketScreen` (inventory), `TradingScreen`
(inventory + army exchange), `SettlementScreen` (army exchange), `DefenceMenuScreen`
(army exchange), `HostileJoinMenuScreen` (army exchange), `TroopManagementScreenBase` with
`DraftTroopsScreen` and `UpgradeTroopsScreen`, `RallyPointScreen`, `MoveTroopPopupScreen`.
Wire `ui_carry` (Space) and `Carry` in the navigator first. The owner's simplification
targets are here (fewer tab stops); each gets its own proposal, measured off the drawn layout.

### Phase E — modes

`PreBattleMenuScreen` (troop placement hex grid; smallest mode, first), `CombatScreen` with
`CombatTroopCycle` (combat hex grid, timeline, troop cycling, threat), `AdventureMapScreen`
(map grid, tile skipping, scanner, bookmarks, HUD stops, teleport mode, the summaries). The
grid classes survive as the mode's cursor; the HUD and side panels become stops, and the
mode node owns its keys, its buffer and its exit announcement. A pre-existing crash on quit
to the main menu (`AdventureMapAdapter.GetInitialTile` throwing on resync) belongs here.

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
`CodexScreen`, `SaveLoadGameScreen` (save variant verified in C), `CampaignMapSelectScreen`,
`CommunityMapsHomeScreen`, `CommunityMapsCollectionScreen`, `CommunityMapsSearchResultsScreen`,
`CommunityMapsDetailsScreen`, `AdventureLobbyPlayersScreen`, `ChatScreen` (in-game selector
verified in C), `ModOptionsScreen` and `ModDialogScreen` (new; the nine widget-era settings
menus are deleted). `FoldoutMenuScreen` was deleted in A.

Remaining, with what the file constructs today and the proposed model (a proposal, not a
decision, until the owner approves it):

| Screen | Widgets today | Proposed model | Phase |
|---|---|---|---|
| `PauseMenuScreen` | Menu | one stop, menu rows (with the drawn Mod options entry) | C |
| `WorldChoiceMenuScreen` | Menu, Buttons, Text | menu rows, text as sections | C |
| `WorldConfirmMenuScreen` | Buttons, Text | dialog shape | C |
| `TutorialSimpleScreen` | Buttons, Checkbox, Text | text node, toggle, buttons row | C |
| `TutorialSlideshowScreen` | Buttons, Checkbox, Text | as above with prev/next | C |
| `StoryTextScreen` | Buttons, Text | text node with sections, continue button; cutscene layer in F | C |
| `AdventurePlayerMenuScreen` | Menu, Buttons | menu rows | C |
| `ClaimMenuScreen` | Menu, Text | text node, menu rows | C |
| `GiftTownPopupScreen` | Menu, Buttons | menu rows, buttons row | C |
| `SendResourcePopupScreen` | Menu, Buttons | menu rows with adjust for amounts if present | C |
| `MapEntityMiniMenuScreen` | Menu, Buttons, Text | menu rows | C |
| `OwnedEntitiesScreen` | Menu, Text | menu rows; a sheet if columns are drawn | C |
| `TroopOverviewScreen` | Menu, Text | menu rows | C |
| `LevelUpScreen` | Menu, Buttons, Text | skill rows with sections, confirm row | C |
| `PurchaseWielderScreen` | Menu, Buttons, Text | candidate rows with sections, buttons row | C |
| `PostAdventureResultScreen` | Menu, Buttons, Text | text sections, buttons row | C |
| `PostBattleResultScreen` | Menu, Buttons, Text | result sections, menu rows | C |
| `ResearchScreen` | Menu, Buttons | research rows with sections | C |
| `MarketplaceScreen` | Menu, Buttons, Text | offer rows with adjust, buttons row | C |
| `BuildMenuScreen` | Menu, Checkbox, Buttons, Text | category regions, building rows with sections | C |
| `SpellbookScreen` | Menu, DraggableMenu, Checkbox, Buttons | spell rows plus carry for reorder | C |
| `PostAdventureStatsScreen` | Menu, Table, Buttons, Text | sheet per tab | C |
| `TooltipActionsMenuScreen` | Menu | stays a widget screen until no unported screen hands out `TooltipAction`s; deleted in G | G |
| `CommanderSheetScreen` | InventoryGrid, Menu, Buttons, Text | stats region, equipment sheet, backpack sheet, carry | D |
| `ArtifactMarketScreen` | InventoryGrid, Menu, Buttons, Text | offers sheet, backpack sheet, carry or buy/sell activation | D |
| `TradingScreen` | InventoryGrid, ArmyExchangeGrid, Menu, Buttons | two army sheets plus two inventory sheets, carry across | D |
| `SettlementScreen` | ArmyExchangeGrid, Menu, Buttons, Text | garrison and visitor sheets, building rows | D |
| `DefenceMenuScreen` | ArmyExchangeGrid, Menu, Buttons, Text | as settlement | D |
| `HostileJoinMenuScreen` | ArmyExchangeGrid, Buttons, Text | army sheet, offer text, buttons row | D |
| `TroopManagementScreenBase` | Buttons, Text | shared base for the two below | D |
| `DraftTroopsScreen` | Menu, Slider, Buttons | troop rows with adjust for count | D |
| `UpgradeTroopsScreen` | Menu, Slider, Buttons, Text | troop rows with adjust | D |
| `RallyPointScreen` | Menu, Slider, Buttons, Text | troop rows with adjust, buttons row | D |
| `MoveTroopPopupScreen` | Slider, Buttons, Text | child dialog: adjust node, buttons row | D |
| `PreBattleMenuScreen` | TroopPlacementHexGrid, Buttons, Text | mode node plus buttons stop | E |
| `CombatScreen` (+ `CombatTroopCycle`) | CombatHexGrid, CombatTroopCycle, Menu, Buttons, Text | mode node, timeline stop, actions stop | E |
| `AdventureMapScreen` | AdventureMapGrid, Menu, Buttons, Text | mode node, HUD stops (troops, resources, objectives, notifications) | E |
| `StoryFocusBlockerScreen` | Container | deleted in F; becomes a predicate | F |

## 9. Risks

- The army exchange and inventory proposals are where the owner wants fewer tab stops; do
  not carry the current widget's structure into the sheet by reflex. Measure the game's
  drawn layout first.
- `ScreenDetector`'s readiness knowledge is the most expensive thing in the repo to lose. In
  phase F, move it, never rewrite it from memory.
- A game text box the mod does not model (the widget era) traps the keyboard while the
  stand-down limit stands; port in-game text inputs early in phase C.
- Each phase's localization batch is real work; a phase is not done until `validate` passes.
