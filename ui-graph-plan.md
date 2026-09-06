# UI graph rewrite plan

Self-contained brief for a fresh session. Goal: replace the retained, stack-based widget
tree in `soc-access/ui/` with the immediate-mode graph engine that Endless Space 2 Access
uses, one screen at a time, with both systems live until the last screen is ported. Every
screen the mod supports today is listed in §8 with its proposed model and its place in the
order.

Prerequisite: the dev server (done 2026-09-05; `docs/dev-loop.md` is its reference). This plan uses `/gui/widgets`, `/gui/graph`,
`/input`, `/speech`, `/eval`, `/reload` and `run-game.ps1` throughout. Do not start without them.

Resuming in a fresh session: read §0's docs, then §2 (what is built) and §3 (keys), then
the current phase in §7 and its rows in §8, then `docs/dev-loop.md` for the verification
loop. Every screen goes through §4's dump-and-diff; the before-capture must be taken on the
unported build, before the screen is touched. Commit per logical step and update this file
as decisions are taken. Phase A's manual walk by the owner is still pending; its steps are
in the phase A section.

Owner decisions already made (do not re-ask):

- The owner gives input on every screen. Each screen's model is proposed and approved
  before it is implemented. Nothing in this plan is ported without that approval.
- The multi-position widgets may have a placeholder in the "before" capture instead of an
  enumerated one.
- The order is: engine and bridge first, then screens in the phases of §7, then the screen
  manager swap last.
- Types and members are public by default, as in ES2. The mod was flipped from
  internal-by-default in one mechanical commit before phase A (2026-09-05), so the engine
  copied from ES2 lands in matching surroundings and `/eval` can name any mod type. New
  and ported code follows that; nothing is declared `internal`.

## 0. Read first

In `../endless-space-2-access/docs/generic/`:

- `ui-navigation.md` — the engine: immediate mode, `ControlId`, `GraphTypes`,
  `GraphBuilder` (menu mode, raw mode, contexts, stops, regions, expandable groups),
  `GraphAnnouncer`, `KeyGraph`, `GraphSheet`, `TypeAheadSearch`; then the adapter section
  (navigator, screens, node factories, focus visuals, scroll-into-view).
- `making-screens-accessible.md` — the per-screen process: measure, propose, approve,
  implement, verify with evidence, hand over the manual test.
- `widgets.md` — the widget vocabulary, gesture parity, popups and child screens, the
  confirmation-dialog screen.
- `buffers.md` and `tooltips.md` — sections as the one declaration behind both the tooltip
  announcement and the review buffer.
- `input.md` — the key model and the stand-down doctrine, for the map and combat phases.
- `performance.md` — bounded immediate-mode rebuilds.

Source:

- Engine, copy verbatim from `docs/generic/src/graph-ui/`: `ControlId.cs`, `GraphTypes.cs`,
  `GraphBuilder.cs`, `GraphAnnouncer.cs`, `KeyGraph.cs`, `NodeBuffer.cs`, `TooltipParts.cs`,
  `GraphSheet.cs`, `Carry.cs`, `Nudge.cs`, `TextUtil.cs`, `TypeAheadSearch.cs`. About
  4,200 lines, BCL-only. Live originals under `ES2Access/Core/UI/` and `Core/UI/Graph/`.
- Engine tests, copy and convert xunit to MSTest, from `ES2Access.Tests/UI/`:
  `Graphs.cs` (helpers), `GraphBuilderTests`, `GraphAnnouncerTests`, `KeyGraphTests`,
  `GraphSheetTests`, `NodeBufferTests`, `TooltipPartTests`, `CarryTests`, `NudgeTests`,
  `TypeAheadSearchTests`.
- Adapter exemplars, imitate rather than copy, from `docs/generic/src/engine-example/`:
  `GraphNavigator.cs` (2,382 lines in the live version; most of it is ES2-specific and is
  not needed on day one), `GraphNodes.cs`, `ControlTypes.cs`, `Screen.cs`,
  `ScreenManager.cs`, `UiActions.cs`, `ModInput.cs`, `PointerFocus.cs`, `ScrollIntoView.cs`,
  `MainMenuScreen.cs`, `MessageBoxScreen.cs`, `DropListScreen.cs`, `LoadingScreen.cs`.
- Dump: `docs/generic/src/dev-server/GraphDump.cs` (531 lines).
- Rules the ES2 repo later added on top of the engine, take when the need appears:
  `ES2Access/Core/UI/Graph/FocusRequest.cs`, `NodeHint.cs`, `OneTooltipRule.cs`,
  `SiblingNameRule.cs`, `TooltipAimRule.cs`, `TooltipKindRule.cs`.

## 1. Facts about the current state

Verified 2026-09-03. Paths relative to `soc-access/`.

- Screens derive from `screens/Screen.cs` (64 lines): `RootWidget` (a `ContainerWidget`),
  `IsPresent()`, `VisibleReviewBuffers`, `OnPush/OnFocus/OnUnfocus/OnPop`, `Update`,
  `HasClaimed(actionKey)`, `HasFocusedWidgetClaimed(actionKey)`,
  `OnActionJustPressed(action)`. The defaults route everything to `RootWidget`.
- `screens/ScreenManager.cs` (473 lines) is a push/pop stack: `Push`, `RefreshTop<T>`,
  `PushBelowTop`, `PushBottom`, `Pop<T>`, `Remove<T>`, `Get<T>`, `Clear`, `Update`,
  `DispatchAction`, `HandleGlobalAction` / `CanHandleGlobalAction` (tooltip actions menu,
  mod settings, resource summaries, review buffer keys), `CurrentScreenClaimsAction`.
  The tooltip actions menu reads `UIManager.CurrentWidget.GetTooltip()` directly
  (lines 262 and 335); this is a seam the bridge must abstract.
- `screens/ScreenDetector.cs` (3,062 lines) is the readiness layer: about 150 `On*Ready`,
  `On*Changed`, `On*Closed` handlers called from `patches/*Patches.cs`, each deciding when
  to push, refresh or pop which screen. `ResyncFromRuntimeState` (line 2877) rebuilds the
  stack from `_runtimeScreenFactories` after a hot reload by asking each factory's screen
  `IsPresent()`. `_storySequenceActive` (line 40) is the flag behind
  `StoryFocusBlockerScreen`.
- `ui/UIManager.cs` (297 lines) is the static focus engine: `RequestFocus` (deferred to
  `Update`), `CommitFocus` (path diff, `Focus`/`Unfocus` on the path), announcement
  composition (`BuildAnnouncement`: optional parent label + `FocusContext` path diff of
  `AnnounceName` ancestors + `GetFocusMessage()`), duplicate suppression keyed on
  `GetAnnouncementKey()`, review buffer fill (`PopulateUiReviewBuffer`), native tooltip
  display (`NativeTooltipUtility.ShowVisualTooltip`).
- `ui/Widget.cs` (208 lines): `GetLabel/GetRole/GetStatus/GetFocusMessage`, `GetTooltip()`
  returning `adapters/Tooltip.cs` (`TextLines`, `VisualMetadata`, `Actions` of
  `TooltipAction(label, invoke)`), `ClaimsAction`, `HandleAction`, `GetFocusedWidget`.
- Widget kinds (`ui/`): `ContainerWidget` (394), `MenuWidget` (364) + `MenuItemWidget`,
  `ButtonWidget`, `CheckboxWidget`, `SliderWidget`, `TextWidget`, `TextInputWidget`,
  `TmpInputFieldWidget`, `TimeInputTextWidget`, `FiveDigitCodeInputWidget`,
  `TableWidget` (570), `DraggableMenuWidget`/`DraggableMenuItemWidget`,
  `AnnouncementOrderMenuWidget` (570), `CodexContentWidget`, `InventoryGridWidget` (593),
  `ArmyExchangeGridWidget` (584), `AdventureMapGrid` (877), `CombatHexGrid` (816),
  `TroopPlacementHexGrid` (635), `TileSkipNavigator` (430), `TroopHudMenu`, `Portrait`,
  `TextInputEchoHelper`. Multi-position widgets (one widget, many spoken positions, via
  `GetAnnouncementKey`): the three grids, `InventoryGridWidget`, `ArmyExchangeGridWidget`,
  `TableWidget`, `AnnouncementOrderMenuWidget`, `CodexContentWidget`.
- Input actions (`input/AccessibilityActions.cs`), claim scope in brackets:
  navigation `next_widget`/`previous_widget` [Screen], `next_menu_item`/`previous_menu_item`/
  `first_menu_item`/`last_menu_item` [Screen], `next_heading`/`previous_heading` [Focused],
  `next_row`/`previous_row`/`first_row`/`last_row`/`next_column`/`previous_column` [Focused],
  `activate` [Screen], `cancel` [Screen], `start_drag` [Focused], `slider_*` [Focused];
  map `map_move_*`, `map_skip_*` [Screen], `map_secondary_action`, `next_wielder`,
  `next_settlement`, `summarize_reachable_entities`, `describe_position`, `sonar_sweep`
  [Focused], `focus_hud_*` [Screen], `scanner_*` [Focused], bookmark arrays; combat
  `hex_grid_*`, `hex_grid_skip_*`, `combat_*`, `read_threat` [Focused]; global
  `tooltip_actions_menu`, `open_mod_settings`, `summarize_resources`,
  `summarize_enemy_resources`, review buffer keys. The physical bindings are in
  `input/KeyboardBinding.cs` and the defaults wherever `InputBinding` instances are built;
  read them before proposing any key change.
- Review buffers: `buffers/ReviewBuffer.cs` (`ReviewBufferKind.Ui`,
  `AdventureMapNotifications`, `CombatEvents`), `ReviewBufferManager.ReplaceLines` /
  `SetCurrentBuffer`. Keep this; do not import ES2's buffer classes.
- Speech: `SpeechPipeline.Output(new SpeechRequest(text, interrupt))`; the input router
  calls `SpeechPipeline.Silence()` on every claimed key, so navigation already interrupts.
- Localization: all mod wording through `ModText.Get(ModStrings...)`; every new
  `ModString` costs an `update-pot`, translations in 13 `.po` files, and `validate`
  (`AGENTS.md`, Localization). Budget that per phase, not per screen.
- Tests are MSTest (`tests/SongsOfConquestAccess.Tests.csproj`), referencing the mod project.

Screens per constructor site: every screen is built by `ScreenDetector` except
`ChatScreen` (`patches/ChatPatches.cs`), the six `CommunityMaps*` screens (static
`TryBuildActiveScreen` factories registered at `ScreenDetector.cs:56-61`), and the
mod-owned menus: `ModSettingsScreen` and `TooltipActionsMenuScreen` (pushed by
`ScreenManager.HandleGlobalAction`), `AnnouncementOrderScreen`, `AudioGlossaryScreen`,
`ScannerCustomCategoriesScreen` (pushed by `ModSettingsScreen`),
`AnnouncementElementSettingsScreen`, `AudioCueSettingsScreen`, `ScannerCustomCategoryScreen`,
`ScannerCustomCategoryKeyScreen`, `ScannerCustomCategorySelectorScreen` (pushed by their
parents).

## 2. What exists now (as built in phase A)

Paths relative to `soc-access/`. Read these before porting a screen; they are the whole
graph side of the mod.

- `ui/graph/` — the engine, namespace `SongsOfConquestAccess.UI.Graph`, 20 files copied from
  ES2 (`ControlId`, `GraphTypes`, `GraphBuilder`, `GraphAnnouncer`, `KeyGraph`, `NodeBuffer`,
  `TooltipParts`, `GraphSheet`, `Carry`, `Nudge`, `TypeAheadSearch`, `TypeAhead`,
  `SearchScope`, `NodeHint`, `OneTooltipRule`, `FocusRequest`, `TextUtil`, `Cycle`,
  `MessageBuilder`, `SpokenText`). Changed only where the repo's rules required: `internal`
  is `public`, strings go through `ModText`, `NodeHint.Template` is a `ModString`. Never edit
  these for a screen's needs; re-sync them against ES2 instead. Tests: `tests/Graph*Tests.cs`,
  `KeyGraphTests`, `NodeBufferTests`, `TooltipPartTests`, `CarryTests`, `NudgeTests`,
  `TypeAhead*Tests`, `SearchScopeTests`, `NodeHintTests`, `OneTooltipRuleTests`,
  `FocusReachTests`, with `tests/GraphFixtures.cs` (class `Graphs`) as the helper.
- `screens/GraphScreen.cs` — the bridge. A screen ports by deriving from it, dropping its
  widget tree, and writing `Key`, `Build(GraphBuilder)`, `IsPresent()`, and optionally
  `ScreenName` (spoken once on focus), `InitialFocusStop`, `Back()`/`ConsumesBack` (Escape;
  false = the game keeps it), `IsWorkable` (mutes the live watch while the page fades),
  `AllowsTypeahead`, `CapturesRawInput`, `TypeAheadScope`, `OnFocusVisual`. Everything the
  push/pop `ScreenManager` asks of a screen is answered by the one navigator. Constructor
  sites, detector handlers and `IsPresent()` stay exactly as they were for the widget
  screen; only the class body changes.
- `ui/GraphNavigator.cs` + `GraphNavigator.Search.cs` — the adapter: one `GraphState` per
  screen instance, `Attach`, `Claims(actionKey)`, `Dispatch(actionKey)`, `Update()` (type-ahead
  tick then `EnsureFocus`, the single site that announces, fills `ReviewBufferKind.Ui`, draws
  the native tooltip through `NativeTooltipUtility.ShowVisualTooltip`, and runs the live-part
  watch), `FocusNode` (pending landings through `FocusRequest`), `InspectRender` for the dump,
  `FocusedTooltip` for the tooltip actions menu. Static wiring (`InstallWiring`/`ResetWiring`
  in `SocAccessMod.Start`/`Stop`) injects the position, expanded/collapsed and sheet wording.
  Not wired yet: carry (phase D), modes (phase E), the game-text-field stand-down for
  type-ahead (phase B, with the edit field), pointer hover simulation.
- `ui/GraphNodes.cs` — the factories: `Button(label, activate, enabled, tooltip, details)`,
  `Group(label, activate, enabled, tooltip, details)`, `Text(label, details, tooltip)`, plus
  the parts (`LabelPart`, `DisabledPart`, `ValuePart`) and `TooltipSection` (every native
  `Tooltip` is an `Indicate` section, buffer only, and `Aim` sets `PointsAt` so focus draws
  it). Every new factory takes the same cross-cutting parameters. Phase B adds checkbox,
  slider, radio, tab, choice, edit field, combo box, and the sheet cell.
- `ui/ControlTypes.cs` — the role registry: `Button`, `Group`, `Text` (no role word). New
  types need a role `ModString` in the phase's localization batch.
- `input/AccessibilityActions.cs` — the graph screens' own actions, all `InputClaimScope.Screen`:
  `ui_up/down/left/right`, `ui_coarse_decrease/increase` (Shift+Left/Right), `ui_next/prev`
  (Tab, Shift+Tab), `ui_home/end`, `ui_region_prev/next` (Alt+Up/Down), `ui_activate`
  (Enter), `ui_secondary` (Backslash), `ui_back` (Escape). `AccessibilityInputRouter` also
  claims bare letters (and Space mid-search) while a graph screen searches, feeding the
  characters from the keyboard's text events.
- `dev/GraphDump.cs` — `/gui/graph?buffers=1&flat=1&edges=1`, `/gui/tree` (whichever dump
  fits the focused screen), `POST /type` (type-ahead). `/status` reports the focused node's
  key and control type as `focusedWidgetId`/`focusedWidgetType`. Reference: `docs/dev-loop.md`.
- `screens/Screen.cs` has `CurrentTooltip`, which `ScreenManager` reads for the tooltip
  actions menu; `dev/WidgetDump.cs` answers with a pointer to `/gui/graph` on a graph screen.
- Final phase only: `screens/ScreenManager.cs` becomes ES2's poll-and-diff manager with
  layers and child screens; `ScreenDetector` shrinks to state recording; `UIManager`,
  `FocusContext`, and every `ui/*Widget.cs` are deleted.

Exemplar: `screens/MainMenuScreen.cs` (two stops, expandable groups driving a game-side
container through `OnExpand`/`OnCollapse` with `expanded:` read live, drawn order measured
off the buttons, `IsWorkable` off the menu's canvas group).

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
| `ui_secondary` | node has `OnSecondary`, or a search is live | `OnSecondary`; in a search, clears it |
| `ui_back` | `Screen.ConsumesBack`, or a search is live | `Screen.Back()`; in a search, "Search cleared" |
| letters, Space mid-search | `AllowsTypeahead && !CapturesRawInput` | type-ahead over the focused stop plus the fully-open build |

Still to add, each in the phase that needs it: `ui_carry` (Space, phase D), the mode keys
(phase E, the mode node's own handler, claimed through a screen-level `AnyKey`-style hook).

Facts a port relies on: `GraphState` is keyed by screen instance, so cursor memory across a
push and pop is lost exactly as today until phase F's registered singletons restore it;
announcements go through `SpeechPipeline.Output` (moves interrupt, arrivals and live changes
queue); the router silences speech on every claimed key.

Owner decision, taken 2026-09-06 before the main menu port: graph screens use ES2's
four-arrow model. Left/Right adjust a value, else step along a row, else expand/descend and
ascend/collapse; Tab cycles stops; Alt+Up/Down jump regions (H/Shift+H cannot stay, because
type-ahead claims the letters); Backslash is the right-click equivalent, as on the map. The
graph screens get their own `ui_*` actions in `AccessibilityActions.cs`, so the widget actions
stay untouched until phase G. Escape stays the game's on game-owned surfaces. Role words
follow ES2 (`button`); positions are announced.

## 4. The dump-and-diff loop (per screen)

1. **Before.** On the unported build, open the screen in-game (`run-game.ps1`, `/loadsave`,
   `/input`), then `GET /gui/widgets?flat=1&buffers=1` and save it as
   `walks/before/<Screen>[-variant].txt`. Capture each variant the screen has (tabs, modes,
   empty and full states). Multi-position widgets appear as placeholders; that is expected.
2. **Propose.** Measure the game's own layout (`/gui/unity` if built, else `/gui/game` and
   the decompiled view classes), write the proposed model (stops, regions, sheets, groups,
   which controls merge into one node, which tab stops go away), and get the owner's
   approval. Nothing is written before that.
3. **Implement.** Change the base class to `GraphScreen`, write `Build`, delete the widget
   construction. Do not touch the adapter unless a fact is missing from it.
4. **After.** `dotnet build`, `POST /reload`, confirm `modAssemblyName` incremented, reopen
   the screen, `GET /gui/graph?flat=1&buffers=1` to `walks/after/<Screen>[-variant].txt`.
5. **Diff.** `sort -u` both files and `diff`. Every before-line absent after is a miss
   unless it is a placeholder or the approved proposal dropped it. Structural differences
   (fewer stops, merged nodes) do not show in flat mode by design; role words and position
   text are excluded from flat lines for the same reason.
6. **Walk.** `POST /input` through every stop and a sample of nodes; `/speech` must read as
   the tree dump reads. Activate one control per kind through the game's own click path.
   Crop a screenshot (`crop-shot.ps1`) of the native tooltip on a focused node.
7. **Hand over.** The manual test lists what the harness cannot prove (physical keys,
   game-key collisions, scroll-into-view, focus visuals). The owner tests; the screen is
   done when they say so.

`walks/` is gitignored, as in the ES2 repo.

## 5. Widget kind to graph model

| Today | Graph model |
|---|---|
| `ContainerWidget` with `AnnounceName` | `PushContext(label)` (spoken once on entry, never focused) or a `BeginStop` when it is a panel the player tabs to |
| `ContainerWidget` as a Tab stop | `BeginStop(key)` |
| `MenuWidget` + `MenuItemWidget` | menu mode: `StartRow` / `AddItem`, one node per item |
| `ButtonWidget` | button node; activation replays the game's `UIButton` click path |
| `CheckboxWidget` | toggle node with live state part |
| `SliderWidget` | node with `OnAdjust` (fine and coarse) and `StateText` |
| `TextWidget` heading | region name, never a node, unless it carries a tooltip (then a row) |
| `TextWidget` body | read-only node with `Sections` feeding the buffer |
| `TextInputWidget`, `TmpInputFieldWidget`, `TimeInputTextWidget`, `FiveDigitCodeInputWidget` | edit node; activation hands the keyboard to the game's field, `TextInputEchoHelper` stays as the echo source |
| `TableWidget` | `GraphSheet`: one stop, column-preserving rows, edge labels naming the crossed column |
| `DraggableMenuWidget` (spellbook) | menu rows plus `Carry` for the reorder |
| `AnnouncementOrderMenuWidget` | menu rows plus `Carry`; the element toggles as a child screen |
| `CodexContentWidget` | one node per article section with `Sections`; headings as regions |
| `InventoryGridWidget` | `GraphSheet` of slots plus `Carry` for move/equip; slot tooltips via `Sections` |
| `ArmyExchangeGridWidget` | one `GraphSheet` per army with troop slots as cells, `Carry` between them; split/merge through activation opening `MoveTroopPopupScreen` |
| `TroopHudMenu` | a stop of troop rows on the map screen |
| `AdventureMapGrid` + `TileSkipNavigator` | a MODE: one node on a map stop whose handler owns the tile cursor; the grid class survives, wrapped (ES2's "a mode whose cursor is not the focus cursor" rule) |
| `CombatHexGrid`, `TroopPlacementHexGrid` | the same mode shape |
| `Portrait` | an announcement part, not a node |

Node factories must all take the same cross-cutting parameters (tooltip, sections,
activation, secondary), per `ui-navigation.md` "Node factories".

## 6. Localization budget

New `ModString`s the engine adapter needs before any screen ports: position `"{0} of {1}"`,
expanded / collapsed state, group and level role words not already present, a generic
"menu" screen name. Add them in phase A in one batch: `update-pot`, translate in all 13
`.po` files, `validate`. Each later phase adds its screen names and any new role in one
batch at the end of the phase, not per screen.

## 7. Phases and order

Rationale for the order: phase A proved the seams on the cheapest screen. Phase B converts
every screen that exists outside a running game, so the whole out-of-game experience moves
to one engine before any in-game screen does, and the factories for every control kind
settle on screens that can be reached from the main menu in seconds. Phase C converts the
in-game menus, forms and tables with those factories already proven. Then the composite
grids (carry and two-sided sheets), then the three modes, which are the largest and depend
on everything before; then the manager swap, because the map and combat predicates carry
the most conditions and are best written once those screens are graph screens.

Within a phase the order is by kind: pure menus, then dialogs, then forms (adds toggles,
sliders, edits), then tables (adds sheets). The first screen of each new kind goes to the
owner's manual screen-reader review before its siblings are batched
(`making-screens-accessible.md` §2). Each phase ends with one localization batch and a
plan update recording what was learned; commits are per logical step (engine, adapter,
one screen or one kind of screen at a time).

### Phase A — engine, bridge, first screen (code done 2026-09-06; owner walk pending)

What landed, and what differed from the original brief:

- The engine needed eight files the plan did not list (`NodeHint`, `OneTooltipRule`,
  `FocusRequest`, `TypeAhead`, `SearchScope`, `MessageBuilder`, `SpokenText`, `Cycle`); all
  are BCL-only and live in `ui/graph/` too. Its tests are MSTest now: 322 cases, four
  dropped because they only proved an installed translation.
- Key model: ES2's, through graph-only `ui_*` actions (§3). Type-ahead is live on every
  graph screen; the stand-down for the game's own text fields is not wired yet and belongs
  with the edit-field work in phase B.
- `GraphScreen.IsWorkable` mutes the live-part watch while a page fades out (the main
  menu's canvas group), found on the first activation: every button read "disabled" as
  the menu left.
- Disabled wording stays "disabled" (not ES2's "unavailable") until the migration ends,
  so ported and unported screens agree; one line in `GraphNodes.DisabledPart` flips it.
- Every native tooltip is an `Indicate` section (buffer only), this mod's standing ruling.
- The foldouts are expandable groups on the main menu stop, opening the game's own foldout;
  `FoldoutMenuScreen` is deleted rather than ported. Drawn order beat declaration order
  (Conquest above Campaigns), read off the buttons' rectangles every build.
- The flat diff of the main menu and both foldouts is clean except for the group state
  word in the buffer. The localization validator accepts a whitespace translation of a
  whitespace source (the fragment separator).
- Manual test still to be run by the owner: arrive on the main menu ("Main menu", then the
  first button with "1 of 8"); Down to "Multiplayer, group, collapsed, 4 of 8", Right opens
  the foldout on screen and lands on "Host online game, button, 1 of 4", Left closes it and
  re-reads the header; Tab reads "Options, button", Shift+Tab returns to the remembered
  item; typing "q" lands on Quit and Escape says "Search cleared"; Escape with no search
  reaches the game; Enter on Conquest opens the lobby with no "disabled" spoken first.

### Phase B — every screen outside a running game

Everything reachable from the main menu without loading a game, in kind order:

1. Menus and dialogs: `QuitToDesktopPopupScreen`, `MessageDialogScreen` (the confirm,
   system and popup-menu sources now; the four in-game sources get their before-captures
   in phase C, on the same class), `LoadingCompleteScreen` (loading a save from the main
   menu), `CampaignMenuScreen`, `TaleSelectScreen`, `CustomCampaignSelectScreen`,
   `PlatformUserMenuScreen`, `AdventureLobbyMapTypeScreen`, `AdventureLobbyInviteProvidersScreen`,
   `CommunityMapsHomeScreen`.
2. Forms: `OptionsScreen`, `SaveLoadGameScreen` (the load variant; the save variant is
   re-verified in phase C), `OnlineHostGameScreen`, `AdventureLobbyRandomLayoutScreen`,
   `AdventureLobbyPlayersScreen`, `AdventureLobbyGameSettingsScreen`,
   `AdventureLobbyPlayerSettingsScreen`, `AdventureLobbyIconDropdownScreen`,
   `CampaignMapSelectScreen`, `CommunityMapsCollectionScreen`, `CommunityMapsDetailsScreen`,
   `CommunityMapsSearchFilterScreen`, `CommunityMapsSearchResultsScreen`,
   `CommunityMapsModalScreen`, `CodexScreen` (from Extras), `ChatScreen` (the lobby chat).
   The edit field brings the type-ahead stand-down and the game-field handover
   (`widgets.md` "Edit field", `input.md`'s late-frame rule).
3. Tables (adds `GraphSheet`): `OnlineGameListScreen`, `PlayerStatsScreen`,
   `AdventureLobbyMapSelectScreen`, `AdventureLobbyChallengeMapSelectScreen`.
4. Mod settings as a real game screen, last, once Options is a graph screen: `ModSettingsScreen`
   and its sub-screens (`AnnouncementOrderScreen`, `AnnouncementElementSettingsScreen`,
   `AudioCueSettingsScreen`, `AudioGlossaryScreen`, `ScannerCustomCategoriesScreen`,
   `ScannerCustomCategoryScreen`, `ScannerCustomCategoryKeyScreen`,
   `ScannerCustomCategorySelectorScreen`) stop being a mod-owned menu on Ctrl+M and become an
   entry of the game's own options window, as ES2 Access did (`ModSettingsMenuEntry`); the
   Options port is what they are built on, so they come after it. Their own proposal.
5. `TooltipActionsMenuScreen` is not ported in this phase and probably never: on a graph
   screen a control's actions are its child nodes, reached with Right (`widgets.md`, "A
   control's several actions are its DRAWN buttons, modeled as child nodes"), so no ported
   screen offers `TooltipAction`s. The widget menu stays as it is, on Backquote, for as long
   as any unported in-game screen still hands them out (the inventory, army and map screens
   of phases C to E); it is deleted in phase G with the rest of the widget engine, or ported
   then if a consumer turns out to need it.

Exit: every screen's dumps diff clean and the owner has walked each kind. The pause menu
is deliberately not here (it is in-game), and no screen of phase C is touched.

### Phase C — in-game menus, popups, forms and tables

`PauseMenuScreen`, `WorldChoiceMenuScreen`, `WorldConfirmMenuScreen`,
`TutorialSimpleScreen`, `TutorialSlideshowScreen`, `StoryTextScreen`,
`AdventurePlayerMenuScreen`, `ClaimMenuScreen`, `GiftTownPopupScreen`,
`SendResourcePopupScreen`, `MapEntityMiniMenuScreen`, `OwnedEntitiesScreen`,
`TroopOverviewScreen`, `LevelUpScreen`, `PurchaseWielderScreen`,
`PostAdventureResultScreen`, `PostBattleResultScreen`, `ResearchScreen`,
`MarketplaceScreen`, `BuildMenuScreen`, `SpellbookScreen`, `PostAdventureStatsScreen`;
plus the in-game verification of what phase B ported on one class: the map message,
random event, custom message and dialogue sources of `MessageDialogScreen`, the save
variant of `SaveLoadGameScreen`, the in-game chat and codex.

### Phase D — composite grids (adds `Carry` and two-sided sheets)

`CommanderSheetScreen` (inventory), `ArtifactMarketScreen` (inventory), `TradingScreen`
(inventory + army exchange), `SettlementScreen` (army exchange), `DefenceMenuScreen`
(army exchange), `HostileJoinMenuScreen` (army exchange), `TroopManagementScreenBase` with
`DraftTroopsScreen` and `UpgradeTroopsScreen`, `RallyPointScreen`, `MoveTroopPopupScreen`.
The owner's simplification targets are here; each gets its own proposal.

### Phase E — modes

`PreBattleMenuScreen` (troop placement hex grid; smallest mode, do it first),
`CombatScreen` with `CombatTroopCycle` (combat hex grid, timeline, troop cycling, threat),
`AdventureMapScreen` (map grid, tile skipping, scanner, bookmarks, HUD stops, teleport
mode, the summaries). The grid classes survive as the mode's cursor; what changes is that
the HUD and side panels become stops, and the mode node owns its keys, its buffer and its
exit announcement (`ui-navigation.md`, "A mode whose cursor is not the focus cursor").

### Phase F — the screen manager swap

1. Replace `ScreenManager` with ES2's poll-and-diff manager: registered singleton screens,
   `Layer`, `IsActive()` polled every frame, insertion-sorted, diffed, one focus-change
   site, child screens (`PushChild`) for the mod-owned menus that remain (the mod settings
   family if it is not yet an entry of the game's options window).
2. Every screen's `IsPresent()` becomes `IsActive()`. Screens that today receive the native
   menu instance in their constructor read it from an adapter static that the existing
   `On*Ready` / `On*Closed` patch handlers write; `ScreenDetector` shrinks to those writes
   and to flags with no game-side state (`_storySequenceActive`, the community-maps refresh
   flags). `ResyncFromRuntimeState`, `PushBelowTop`, `PushBottom`, `RefreshTop` go away.
3. Layers: map and combat 10; in-game panels and lobby 20-40; `MessageDialogScreen` 100
   with `AnswersOnly`; story text and letterbox at a cutscene layer above the panels; the
   loading screen above everything. `AdventureMapScreen.IsActive` gates on no popup, no
   story sequence, no loading; `StoryFocusBlockerScreen` is deleted.
4. `KeepStateOnPop` on the map, combat and settlement screens.
5. Verify with `/gui/graph?screen=KEY` for every registered screen, the dialog-over-map
   case (open a confirm popup on the map: the map must not speak; close it: the map speaks
   its name and the tile once), and the story sequence gap.

### Phase G — cleanup

Delete `ui/UIManager.cs`, `ui/FocusContext.cs`, every `ui/*Widget.cs`, `ui/MenuWidget.cs`,
`ui/TableWidget.cs`, and the three grid classes' widget base once their mode wrappers own
them, and `TooltipActionsMenuScreen` with its Backquote action once no screen hands out a
`TooltipAction` any more. Delete `/gui/widgets`. Update `AGENTS.md` (layout, the adapter rule stays, the
widget-tree wording becomes graph wording) and `screens/README.md`.

## 8. Screen inventory

Every screen file in `soc-access/screens/` (78 screens plus the three infrastructure files
and `CombatTroopCycle`). Widgets are what the file constructs today. Model is the proposal
to bring to the owner, not a decision.

| Screen | Widgets today | Proposed model | Phase |
|---|---|---|---|
| `MainMenuScreen` | Menu, Buttons | one stop, menu rows | A, done |
| `FoldoutMenuScreen` | Menu | deleted: expandable groups on the main menu stop | A, done |
| `PauseMenuScreen` | Menu | one stop, menu rows | C |
| `PlatformUserMenuScreen` | Menu, Buttons | menu rows | B |
| `QuitToDesktopPopupScreen` | Buttons, Text | dialog: question as read-only node, buttons row | B |
| `MessageDialogScreen` | Buttons, TextInput, Text | dialog with `AnswersOnly`; text node with sections, optional edit node, buttons row; one screen for all seven sources (three out-of-game sources in B, the rest verified in C) | B |
| `LoadingCompleteScreen` | PassiveButton | single node | B |
| `CampaignMenuScreen` | Menu, Buttons | menu rows | B |
| `TaleSelectScreen` | Menu, Buttons | menu rows with `Sections` for the tale blurb | B |
| `CustomCampaignSelectScreen` | Menu (custom entry item), Buttons | menu rows with status parts | B |
| `WorldChoiceMenuScreen` | Menu, Buttons, Text | menu rows, text as sections | C |
| `WorldConfirmMenuScreen` | Buttons, Text | dialog shape | C |
| `TutorialSimpleScreen` | Buttons, Checkbox, Text | text node, toggle, buttons row | C |
| `TutorialSlideshowScreen` | Buttons, Checkbox, Text | as above with prev/next | C |
| `StoryTextScreen` | Buttons, Text | text node with sections, continue button; cutscene layer in G | C |
| `TooltipActionsMenuScreen` | Menu | stays a widget screen while unported screens hand out `TooltipAction`s; a graph control's actions are its expandable children; deleted in G | G |
| `AudioGlossaryScreen` | Menu, Buttons | part of the mod settings game screen; menu rows | B, last |
| `AdventurePlayerMenuScreen` | Menu, Buttons | menu rows | C |
| `ClaimMenuScreen` | Menu, Text | text node, menu rows | C |
| `GiftTownPopupScreen` | Menu, Buttons | menu rows, buttons row | C |
| `SendResourcePopupScreen` | Menu, Buttons | menu rows with adjust for amounts if present | C |
| `MapEntityMiniMenuScreen` | Menu, Buttons, Text | menu rows | C |
| `OwnedEntitiesScreen` | Menu, Text | menu rows; consider a sheet if columns exist | C |
| `TroopOverviewScreen` | Menu, Text | menu rows | C |
| `LevelUpScreen` | Menu, Buttons, Text | skill rows with sections, confirm row | C |
| `PurchaseWielderScreen` | Menu, Buttons, Text | candidate rows with sections, buttons row | C |
| `PostAdventureResultScreen` | Menu, Buttons, Text | text sections, buttons row | C |
| `PostBattleResultScreen` | Menu, Buttons, Text | result sections, menu rows | C |
| `ResearchScreen` | Menu, Buttons | research rows with sections | C |
| `MarketplaceScreen` | Menu, Buttons, Text | offer rows with adjust, buttons row | C |
| `BuildMenuScreen` | Menu, Checkbox, Buttons, Text | category regions, building rows with sections | C |
| `OptionsScreen` | Menu, Checkbox, Slider, Buttons, Text | tab regions, toggle/slider/choice rows | B |
| `SaveLoadGameScreen` | Menu, TextInput, Buttons, Text | save rows, name edit, buttons row (load variant in B, save variant verified in C) | B |
| `OnlineHostGameScreen` | Checkbox, TextInput, Buttons, Text | form rows | B |
| `ChatScreen` | Menu, TextInput, Buttons | message rows, edit node; game-owned field handover | B |
| `AdventureLobbyMapTypeScreen` | Menu, Buttons | menu rows | B |
| `AdventureLobbyRandomLayoutScreen` | Menu, Checkbox, Buttons | option rows | B |
| `AdventureLobbyPlayersScreen` | Menu, Checkbox, Buttons, Text | one region per slot, slot rows | B |
| `AdventureLobbyInviteProvidersScreen` | Menu, Buttons | menu rows | B |
| `AdventureLobbyGameSettingsScreen` | Menu, Checkbox, TextInput, TimeInput, Buttons, Text | setting rows, edit nodes | B |
| `AdventureLobbyPlayerSettingsScreen` | Checkbox, Slider, Buttons, Text | setting rows | B |
| `AdventureLobbyIconDropdownScreen` | Menu, Buttons | drop list (ES2 `DropListScreen` shape) | B |
| `CampaignMapSelectScreen` | Menu, Buttons, Text | map rows with sections, buttons row | B |
| `ModSettingsScreen` | Menu, Checkbox, Buttons | an entry of the game's options window, as in ES2; setting rows | B, last |
| `AnnouncementOrderScreen` | AnnouncementOrderMenu, Buttons | rows plus carry for reorder | B, last |
| `AnnouncementElementSettingsScreen` | Checkbox, Buttons | toggle rows | B, last |
| `AudioCueSettingsScreen` | Checkbox, Slider, Buttons | toggle/slider rows | B, last |
| `ScannerCustomCategoriesScreen` | Buttons | rows | B, last |
| `ScannerCustomCategoryScreen` | Buttons | rows, child screens for key and selector | B, last |
| `ScannerCustomCategoryKeyScreen` | Buttons | key-capture node (`widgets.md` "Key-rebind capture") | B, last |
| `ScannerCustomCategorySelectorScreen` | Checkbox, Buttons | toggle rows | B, last |
| `CommunityMapsHomeScreen` | Menu, Buttons | menu rows | B |
| `CommunityMapsCollectionScreen` | Menu, Checkbox, TmpInputField, Buttons | rows, filter edit | B |
| `CommunityMapsDetailsScreen` | Menu, Buttons, Text | text sections, action rows | B |
| `CommunityMapsSearchFilterScreen` | Menu, TmpInputField, Buttons | filter rows, edit | B |
| `CommunityMapsSearchResultsScreen` | Menu, Buttons, Text | result rows | B |
| `CommunityMapsModalScreen` | Menu, FiveDigitCode, TmpInputField, Buttons, Text | dialog shape with edit nodes | B |
| `CodexScreen` | Menu, Checkbox, CodexContent, Buttons | category stop, article stop, content sections as regions | B |
| `SpellbookScreen` | Menu, DraggableMenu, Checkbox, Buttons | spell rows plus carry for reorder | C |
| `OnlineGameListScreen` | Menu, Table, Buttons, Text | sheet of games, buttons row | B |
| `PlayerStatsScreen` | Menu, Table, Buttons, Text | sheet per tab | B |
| `PostAdventureStatsScreen` | Menu, Table, Buttons, Text | sheet per tab | C |
| `AdventureLobbyMapSelectScreen` | Menu, Table, Buttons, Text | sheet of maps, detail sections | B |
| `AdventureLobbyChallengeMapSelectScreen` | Table, Buttons, Text | sheet of challenges | B |
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
| `CombatScreen` (+ `CombatTroopCycle`) | CombatHexGrid, CombatTroopCycle, Menu, Buttons, Text | mode node, timeline stop, actions stop | F |
| `AdventureMapScreen` | AdventureMapGrid, Menu, Buttons, Text | mode node, HUD stops (troops, resources, objectives, notifications) | E |
| `StoryFocusBlockerScreen` | Container | deleted in F; becomes a predicate | F |

## 9. Risks

- The physical key model (§3 decision) is the one choice that reshapes every screen; take
  it before phase A's port, not after.
- `MessageDialogScreen` serves seven native sources; port it with a before-capture per
  source, or it will lose one.
- The army exchange and inventory proposals are where the owner wants fewer tab stops; do
  not carry the current widget's structure into the sheet by reflex. Measure the game's
  drawn layout first.
- Each phase's localization batch is real work; a phase is not done until `validate` passes.
- `ScreenDetector`'s readiness knowledge is the most expensive thing in the repo to lose.
  In phase F, move it, never rewrite it from memory.
