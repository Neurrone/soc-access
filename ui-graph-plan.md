# UI graph rewrite plan

Self-contained brief for a fresh session. Goal: replace the retained, stack-based widget
tree in `soc-access/ui/` with the immediate-mode graph engine that Endless Space 2 Access
uses, one screen at a time, with both systems live until the last screen is ported. Every
screen the mod supports today is listed in §8 with its proposed model and its place in the
order.

Prerequisite: the dev server (done 2026-09-05; `docs/dev-loop.md` is its reference). This plan uses `/gui/widgets`, `/gui/graph`,
`/input`, `/speech`, `/eval`, `/reload` and `run-game.ps1` throughout. Do not start without them.

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

## 2. Target architecture

- `ui/graph/` — the engine, namespace `SongsOfConquestAccess.UI.Graph`, copied verbatim,
  unit-tested offline.
- `ui/GraphNavigator.cs` — this game's adapter: one `GraphState` per screen, `Attach`,
  `Dispatch(actionKey)`, `EnsureFocus` (the single announce/buffer/visual site),
  `InspectRender` for the dump. Starts at a few hundred lines; grows per phase (carry in
  E, modes in F, type-ahead only if the owner wants it).
- `ui/GraphNodes.cs` — node factories per game widget kind (§5), all taking the same
  cross-cutting parameters (tooltip, sections, activation).
- `ui/ControlTypes.cs` — the role registry, reusing the existing role `ModStrings`.
- `screens/GraphScreen.cs` — the bridge (§3). Screens port by changing their base class
  and replacing their constructor's widget tree with `Build(GraphBuilder)`.
- `dev/GraphDump.cs` — `/gui/graph`, plus `flat=1` in the shared grammar
  (`docs/dev-loop.md` §2a), and `/gui/tree` which answers whichever dump fits the
  focused screen's kind.
- Final phase only: `screens/ScreenManager.cs` becomes ES2's poll-and-diff manager with
  layers and child screens; `ScreenDetector` shrinks to state recording; `UIManager`,
  `FocusContext`, and every `ui/*Widget.cs` are deleted.

## 3. The bridge: `GraphScreen`

```csharp
public abstract class GraphScreen : Screen
{
    protected GraphScreen() : base(rootWidget: null) { }
    public abstract string Key { get; }
    public abstract void Build(GraphBuilder builder);
    public virtual string ScreenName { get { return null; } }   // spoken once on focus
    public virtual object InitialFocusStop { get { return null; } }
    public virtual bool Back() { return false; }                // cancel key; false = game keeps it
    public override void OnFocus()      { Navigator.Attach(this); speak ScreenName (queued) }
    public override void OnUnfocus()    { Navigator.Attach(null); }
    public override void Update()       { Navigator.EnsureFocus(); }
    public override bool OnActionJustPressed(InputAction a) { return Navigator.Dispatch(a.Key); }
    public override bool HasClaimed(string key)              { return Navigator.Claims(key); }
    public override bool HasFocusedWidgetClaimed(string key) { return Navigator.Claims(key); }
    public override Tooltip CurrentTooltip { get { return Navigator.FocusedTooltip; } }
}
```

Seams to change in the shared code, each small:

1. `Screen` gains `virtual Tooltip CurrentTooltip` (widget screens answer
   `UIManager.CurrentWidget?.GetTooltip()`); `ScreenManager.HandleGlobalAction` and
   `CanHandleGlobalAction` use it instead of `UIManager.CurrentWidget`.
2. `Screen` defaults tolerate a null `RootWidget` (`HasClaimed`, `OnFocus`); or `GraphScreen`
   overrides all of them, which it does above. `ScreenManager.Push` calling
   `UIManager.Reset()` is harmless for a graph screen.
3. Claims: `Navigator.Claims(key)` answers true for the navigation set the graph always
   takes (`next_widget`, `previous_widget`, `next_menu_item`, `previous_menu_item`,
   `first_menu_item`, `last_menu_item`, `next_heading`, `previous_heading`, `activate`,
   `cancel` when `Back()` would answer true) and, from the focused node's vtable, for
   `slider_*` when it has `OnAdjust`, `start_drag` when it has a carry, `next_column` etc.
   when it sits in a sheet, `map_*`/`hex_grid_*`/`combat_*`/`scanner_*` when the node is a
   mode that declares them (`Screen.AnyKey` in ES2). This keeps the router's
   claim-before-dispatch contract intact.
4. Review buffer: `EnsureFocus` fills `ReviewBufferKind.Ui` from the node's `NodeBuffer`
   lines through the existing `ReviewBufferManager`, mirroring `UIManager.PopulateUiReviewBuffer`.
5. Native tooltip visual: `EnsureFocus` calls `NativeTooltipUtility.ShowVisualTooltip` with
   the focused node's `VisualMetadata`, as `UIManager.Update` does today. Pointer hover
   simulation (`PointerFocus` in ES2) is a later addition once a screen shows it matters.
6. Speech: announcements go through `SpeechPipeline.Output`; the navigator never calls
   Prism. Interrupt policy stays what the router does today.
7. `GraphState` is keyed by screen instance. Today every push constructs a new screen, so
   cursor memory across pushes is lost exactly as it is today; the final phase's registered
   singletons restore it (`KeepStateOnPop`).

Action-to-operation table for `Navigator.Dispatch`:

| Action key | KeyGraph operation |
|---|---|
| `next_widget` / `previous_widget` | `MoveStop` forward / back |
| `next_menu_item` / `previous_menu_item` | `Move` Down / Up |
| `first_menu_item` / `last_menu_item` | `MoveToEdge` Up / Down |
| `next_column` / `previous_column` | `Move` Right / Left (sheets) |
| `next_row` / `previous_row`, `first_row` / `last_row` | `Move` Down / Up, `MoveToEdge` (sheets) |
| `next_heading` / `previous_heading` | `MoveRegion` next / prev |
| `activate` | `Activate` |
| `cancel` | `Screen.Back()`; unhandled falls through to the game |
| `slider_increase` / `slider_decrease` / `slider_minimum` / `slider_maximum` | `OnAdjust` +1 / -1 / min / max |
| `start_drag` | `Carry` pick up / drop |
| mode keys (`map_*`, `hex_grid_*`, `combat_*`, `scanner_*`, bookmarks) | the mode node's own handler |

Owner decision needed in phase A, before the main menu port: whether to keep the current
key bindings as they are, or adopt ES2's four-arrow model where Left/Right also expand and
collapse tree groups and Tab cycles stops. The engine supports both; the answer decides
whether `next_column`/`previous_column` and tree navigation share keys.

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

Rationale for the order: prove the seams on the cheapest screen; then convert the screens
that are pure menus so the factories for buttons, items and text settle; then forms (adds
toggles, sliders, edits); then tables (adds sheets); then the composite grids (adds carry
and the two-sided sheets); then the three modes, which are the largest and depend on
everything before; then swap the manager, because the map and combat predicates carry the
most conditions and are best written once those screens are graph screens.

### Phase A — engine, bridge, first screen

1. Copy the engine into `ui/graph/`; copy and convert the tests; `dotnet test` green.
2. Write `GraphScreen`, `GraphNavigator`, `ControlTypes`, the first `GraphNodes` factories
   (context, stop, button, menu item, text), the `CurrentTooltip` seam, `GraphDump` with
   `flat=1`, `/gui/tree`.
3. Localization batch (§6).
4. Owner decision on the key model (§3).
5. Port `MainMenuScreen` (menu of buttons) and `FoldoutMenuScreen` (the main menu's
   foldouts) through the §4 loop. Exit: both dumps diff clean, the owner has walked it.

### Phase B1 — out-of-game menus and dialogs (push/pop stays)

`PauseMenuScreen`, `PlatformUserMenuScreen`, `QuitToDesktopPopupScreen`,
`MessageDialogScreen` (the dialog family: confirm, system, popup menu, map message, random
event, custom message, dialogue; 488 lines, the one to do carefully), `LoadingCompleteScreen`,
`CampaignMenuScreen`, `TaleSelectScreen`, `CustomCampaignSelectScreen`,
`WorldChoiceMenuScreen`, `WorldConfirmMenuScreen`, `TutorialSimpleScreen`,
`TutorialSlideshowScreen`, `StoryTextScreen`, `TooltipActionsMenuScreen`,
`AudioGlossaryScreen`.

### Phase B2 — in-game menus and popups

`AdventurePlayerMenuScreen`, `ClaimMenuScreen`, `GiftTownPopupScreen`,
`SendResourcePopupScreen`, `MapEntityMiniMenuScreen`, `OwnedEntitiesScreen`,
`TroopOverviewScreen`, `LevelUpScreen`, `PurchaseWielderScreen`,
`PostAdventureResultScreen`, `PostBattleResultScreen`, `ResearchScreen`,
`MarketplaceScreen`, `BuildMenuScreen`.

### Phase C — forms, settings, lobby, chat

`OptionsScreen`, `SaveLoadGameScreen`, `OnlineHostGameScreen`, `ChatScreen`,
`AdventureLobbyMapTypeScreen`, `AdventureLobbyRandomLayoutScreen`,
`AdventureLobbyPlayersScreen`, `AdventureLobbyInviteProvidersScreen`,
`AdventureLobbyGameSettingsScreen`, `AdventureLobbyPlayerSettingsScreen`,
`AdventureLobbyIconDropdownScreen`, `CampaignMapSelectScreen`, `ModSettingsScreen`,
`AnnouncementOrderScreen`, `AnnouncementElementSettingsScreen`, `AudioCueSettingsScreen`,
`ScannerCustomCategoriesScreen`, `ScannerCustomCategoryScreen`,
`ScannerCustomCategoryKeyScreen`, `ScannerCustomCategorySelectorScreen`,
`CommunityMapsHomeScreen`, `CommunityMapsCollectionScreen`, `CommunityMapsDetailsScreen`,
`CommunityMapsSearchFilterScreen`, `CommunityMapsSearchResultsScreen`,
`CommunityMapsModalScreen`, `CodexScreen`, `SpellbookScreen`.

### Phase D — tables (adds `GraphSheet`)

`OnlineGameListScreen`, `PlayerStatsScreen`, `PostAdventureStatsScreen`,
`AdventureLobbyMapSelectScreen`, `AdventureLobbyChallengeMapSelectScreen`.

### Phase E — composite grids (adds `Carry` and two-sided sheets)

`CommanderSheetScreen` (inventory), `ArtifactMarketScreen` (inventory), `TradingScreen`
(inventory + army exchange), `SettlementScreen` (army exchange), `DefenceMenuScreen`
(army exchange), `HostileJoinMenuScreen` (army exchange), `TroopManagementScreenBase` with
`DraftTroopsScreen` and `UpgradeTroopsScreen`, `RallyPointScreen`, `MoveTroopPopupScreen`.
The owner's simplification targets are here; each gets its own proposal.

### Phase F — modes

`PreBattleMenuScreen` (troop placement hex grid; smallest mode, do it first),
`CombatScreen` with `CombatTroopCycle` (combat hex grid, timeline, troop cycling, threat),
`AdventureMapScreen` (map grid, tile skipping, scanner, bookmarks, HUD stops, teleport
mode, the summaries). The grid classes survive as the mode's cursor; what changes is that
the HUD and side panels become stops, and the mode node owns its keys, its buffer and its
exit announcement (`ui-navigation.md`, "A mode whose cursor is not the focus cursor").

### Phase G — the screen manager swap

1. Replace `ScreenManager` with ES2's poll-and-diff manager: registered singleton screens,
   `Layer`, `IsActive()` polled every frame, insertion-sorted, diffed, one focus-change
   site, child screens (`PushChild`) for the mod-owned menus (tooltip actions, mod settings
   and its seven sub-screens, foldout menu if it stays mod-owned).
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

### Phase H — cleanup

Delete `ui/UIManager.cs`, `ui/FocusContext.cs`, every `ui/*Widget.cs`, `ui/MenuWidget.cs`,
`ui/TableWidget.cs`, and the three grid classes' widget base once their mode wrappers own
them. Delete `/gui/widgets`. Update `AGENTS.md` (layout, the adapter rule stays, the
widget-tree wording becomes graph wording) and `screens/README.md`.

## 8. Screen inventory

Every screen file in `soc-access/screens/` (78 screens plus the three infrastructure files
and `CombatTroopCycle`). Widgets are what the file constructs today. Model is the proposal
to bring to the owner, not a decision.

| Screen | Widgets today | Proposed model | Phase |
|---|---|---|---|
| `MainMenuScreen` | Menu, Buttons | one stop, menu rows | A |
| `FoldoutMenuScreen` | Menu | child of the main menu; menu rows | A |
| `PauseMenuScreen` | Menu | one stop, menu rows | B1 |
| `PlatformUserMenuScreen` | Menu, Buttons | menu rows | B1 |
| `QuitToDesktopPopupScreen` | Buttons, Text | dialog: question as read-only node, buttons row | B1 |
| `MessageDialogScreen` | Buttons, TextInput, Text | dialog with `AnswersOnly`; text node with sections, optional edit node, buttons row; one screen for all seven sources | B1 |
| `LoadingCompleteScreen` | PassiveButton | single node | B1 |
| `CampaignMenuScreen` | Menu, Buttons | menu rows | B1 |
| `TaleSelectScreen` | Menu, Buttons | menu rows with `Sections` for the tale blurb | B1 |
| `CustomCampaignSelectScreen` | Menu (custom entry item), Buttons | menu rows with status parts | B1 |
| `WorldChoiceMenuScreen` | Menu, Buttons, Text | menu rows, text as sections | B1 |
| `WorldConfirmMenuScreen` | Buttons, Text | dialog shape | B1 |
| `TutorialSimpleScreen` | Buttons, Checkbox, Text | text node, toggle, buttons row | B1 |
| `TutorialSlideshowScreen` | Buttons, Checkbox, Text | as above with prev/next | B1 |
| `StoryTextScreen` | Buttons, Text | text node with sections, continue button; cutscene layer in G | B1 |
| `TooltipActionsMenuScreen` | Menu | child screen: menu rows of `TooltipAction` | B1 |
| `AudioGlossaryScreen` | Menu, Buttons | child of mod settings; menu rows | B1 |
| `AdventurePlayerMenuScreen` | Menu, Buttons | menu rows | B2 |
| `ClaimMenuScreen` | Menu, Text | text node, menu rows | B2 |
| `GiftTownPopupScreen` | Menu, Buttons | menu rows, buttons row | B2 |
| `SendResourcePopupScreen` | Menu, Buttons | menu rows with adjust for amounts if present | B2 |
| `MapEntityMiniMenuScreen` | Menu, Buttons, Text | menu rows | B2 |
| `OwnedEntitiesScreen` | Menu, Text | menu rows; consider a sheet if columns exist | B2 |
| `TroopOverviewScreen` | Menu, Text | menu rows | B2 |
| `LevelUpScreen` | Menu, Buttons, Text | skill rows with sections, confirm row | B2 |
| `PurchaseWielderScreen` | Menu, Buttons, Text | candidate rows with sections, buttons row | B2 |
| `PostAdventureResultScreen` | Menu, Buttons, Text | text sections, buttons row | B2 |
| `PostBattleResultScreen` | Menu, Buttons, Text | result sections, menu rows | B2 |
| `ResearchScreen` | Menu, Buttons | research rows with sections | B2 |
| `MarketplaceScreen` | Menu, Buttons, Text | offer rows with adjust, buttons row | B2 |
| `BuildMenuScreen` | Menu, Checkbox, Buttons, Text | category regions, building rows with sections | B2 |
| `OptionsScreen` | Menu, Checkbox, Slider, Buttons, Text | tab regions, toggle/slider/choice rows | C |
| `SaveLoadGameScreen` | Menu, TextInput, Buttons, Text | save rows, name edit, buttons row | C |
| `OnlineHostGameScreen` | Checkbox, TextInput, Buttons, Text | form rows | C |
| `ChatScreen` | Menu, TextInput, Buttons | message rows, edit node; game-owned field handover | C |
| `AdventureLobbyMapTypeScreen` | Menu, Buttons | menu rows | C |
| `AdventureLobbyRandomLayoutScreen` | Menu, Checkbox, Buttons | option rows | C |
| `AdventureLobbyPlayersScreen` | Menu, Checkbox, Buttons, Text | one region per slot, slot rows | C |
| `AdventureLobbyInviteProvidersScreen` | Menu, Buttons | menu rows | C |
| `AdventureLobbyGameSettingsScreen` | Menu, Checkbox, TextInput, TimeInput, Buttons, Text | setting rows, edit nodes | C |
| `AdventureLobbyPlayerSettingsScreen` | Checkbox, Slider, Buttons, Text | setting rows | C |
| `AdventureLobbyIconDropdownScreen` | Menu, Buttons | drop list (ES2 `DropListScreen` shape) | C |
| `CampaignMapSelectScreen` | Menu, Buttons, Text | map rows with sections, buttons row | C |
| `ModSettingsScreen` | Menu, Checkbox, Buttons | child screen root; setting rows | C |
| `AnnouncementOrderScreen` | AnnouncementOrderMenu, Buttons | rows plus carry for reorder | C |
| `AnnouncementElementSettingsScreen` | Checkbox, Buttons | toggle rows | C |
| `AudioCueSettingsScreen` | Checkbox, Slider, Buttons | toggle/slider rows | C |
| `ScannerCustomCategoriesScreen` | Buttons | rows | C |
| `ScannerCustomCategoryScreen` | Buttons | rows, child screens for key and selector | C |
| `ScannerCustomCategoryKeyScreen` | Buttons | key-capture node (`widgets.md` "Key-rebind capture") | C |
| `ScannerCustomCategorySelectorScreen` | Checkbox, Buttons | toggle rows | C |
| `CommunityMapsHomeScreen` | Menu, Buttons | menu rows | C |
| `CommunityMapsCollectionScreen` | Menu, Checkbox, TmpInputField, Buttons | rows, filter edit | C |
| `CommunityMapsDetailsScreen` | Menu, Buttons, Text | text sections, action rows | C |
| `CommunityMapsSearchFilterScreen` | Menu, TmpInputField, Buttons | filter rows, edit | C |
| `CommunityMapsSearchResultsScreen` | Menu, Buttons, Text | result rows | C |
| `CommunityMapsModalScreen` | Menu, FiveDigitCode, TmpInputField, Buttons, Text | dialog shape with edit nodes | C |
| `CodexScreen` | Menu, Checkbox, CodexContent, Buttons | category stop, article stop, content sections as regions | C |
| `SpellbookScreen` | Menu, DraggableMenu, Checkbox, Buttons | spell rows plus carry for reorder | C |
| `OnlineGameListScreen` | Menu, Table, Buttons, Text | sheet of games, buttons row | D |
| `PlayerStatsScreen` | Menu, Table, Buttons, Text | sheet per tab | D |
| `PostAdventureStatsScreen` | Menu, Table, Buttons, Text | sheet per tab | D |
| `AdventureLobbyMapSelectScreen` | Menu, Table, Buttons, Text | sheet of maps, detail sections | D |
| `AdventureLobbyChallengeMapSelectScreen` | Table, Buttons, Text | sheet of challenges | D |
| `CommanderSheetScreen` | InventoryGrid, Menu, Buttons, Text | stats region, equipment sheet, backpack sheet, carry | E |
| `ArtifactMarketScreen` | InventoryGrid, Menu, Buttons, Text | offers sheet, backpack sheet, carry or buy/sell activation | E |
| `TradingScreen` | InventoryGrid, ArmyExchangeGrid, Menu, Buttons | two army sheets plus two inventory sheets, carry across | E |
| `SettlementScreen` | ArmyExchangeGrid, Menu, Buttons, Text | garrison and visitor sheets, building rows | E |
| `DefenceMenuScreen` | ArmyExchangeGrid, Menu, Buttons, Text | as settlement | E |
| `HostileJoinMenuScreen` | ArmyExchangeGrid, Buttons, Text | army sheet, offer text, buttons row | E |
| `TroopManagementScreenBase` | Buttons, Text | shared base for the two below | E |
| `DraftTroopsScreen` | Menu, Slider, Buttons | troop rows with adjust for count | E |
| `UpgradeTroopsScreen` | Menu, Slider, Buttons, Text | troop rows with adjust | E |
| `RallyPointScreen` | Menu, Slider, Buttons, Text | troop rows with adjust, buttons row | E |
| `MoveTroopPopupScreen` | Slider, Buttons, Text | child dialog: adjust node, buttons row | E |
| `PreBattleMenuScreen` | TroopPlacementHexGrid, Buttons, Text | mode node plus buttons stop | F |
| `CombatScreen` (+ `CombatTroopCycle`) | CombatHexGrid, CombatTroopCycle, Menu, Buttons, Text | mode node, timeline stop, actions stop | F |
| `AdventureMapScreen` | AdventureMapGrid, Menu, Buttons, Text | mode node, HUD stops (troops, resources, objectives, notifications) | F |
| `StoryFocusBlockerScreen` | Container | deleted in G; becomes a predicate | G |

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
  In phase G, move it, never rewrite it from memory.
