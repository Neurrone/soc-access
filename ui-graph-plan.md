# UI graph rewrite plan

Self-contained brief for a fresh session. Goal: replace the retained, stack-based widget
tree in `soc-access/ui/` with the immediate-mode graph engine that Endless Space 2 Access
uses. Every screen is ported (phases A to E) and the manager polls (phase F, walked and closed
2026-09-08). What remains is the cleanup (phase G).

Prerequisite: the dev server (`docs/dev-loop.md`). This plan uses `/gui/graph`, `/gui/unity`,
`/input`, `/key`, `/speech`, `/eval`, `/reload` and `run-game.ps1` throughout.

Resuming in a fresh session: read §0's docs, then §1 (what F replaces) and §2 (what exists),
§3 (keys), then phase F in §5 and the inventory in §6, then `docs/dev-loop.md`. Commit per
logical step and update this file as decisions are taken; prune it at the end of each phase
so it holds only what the remaining work needs. Start at phase G.

Owner decisions already made (do not re-ask):

- The owner approves the model of anything user-facing before it is implemented, and walks
  each new kind with real keys. Phase F changes no screen's reading: the `walks/after/`
  captures are the contract.
- Types and members are public by default, as in ES2; nothing is declared `internal`.
- Escape follows ES2: the game keeps it on its own surfaces wherever it registers its own
  exit action in keyboard mode (`AddInputCallback` for `UI.ExitMenu` outside a gamepad branch
  in the decompiled class; `UI.Cancel` is the gamepad binding); elsewhere the screen claims
  Back and presses the drawn close control; a mod-owned surface always denies the game the
  key. Recorded per screen in its doc comment; F keeps every one.
- The handover to the owner is ONE SHORT PAGE per phase (§4). This plan holds only what the
  remaining work needs.
- The mod's own settings are DRAWN dialogs built from the game's parts: "Mod options" entries
  on the main menu and the pause menu, a dialog cloned from the options panel, sub-dialogs
  stacked over it with the layers beneath inert to the mouse. In F they are child screens.
- Performance (`AGENTS.md`, Performance): a graph build costs under a millisecond, measured
  with `docs/dev-loop.md` §4a; F's manager polls `IsActive()` for every registered screen
  each frame, so a predicate never scans the scene. `performance.md` in the repo root,
  untracked, lists the per-frame costs still to fix on other screens.

## 0. Read first

In `../endless-space-2-access/docs/generic/`: `ui-navigation.md`, the adapter section on
screens (poll-and-diff, `Layer`, `IsActive()`, child screens, `KeepStateOnPop`) and
"Camera-follows-focus" for the map's seat after a dialog; `input.md` for the stand-down
doctrine G lifts; `performance.md` for bounded rebuilds. Source exemplars to imitate, never
copy, under `docs/generic/src/engine-example/`: `ScreenManager.cs` and `Screen.cs` (phase F),
`GraphNavigator.cs`. Live ES2 screens under `../endless-space-2-access/ES2Access/Screens/`
only where a predicate's shape is in doubt (`GalaxyHudScreen.IsActive`).

## 1. What phase F replaces and phase G deletes

Paths relative to `soc-access/`.

- `screens/Screen.cs` is ES2's shape (`Key`, `Layer`, `IsActive()`, `KeepStateOnPop`, the
  lifecycle, the child chain); `screens/LiveScreen.cs` is the slot. `screens/ScreenManager.cs` polls every registered screen's `IsActive()` and diffs the
  result (phase F): a page that turns in place keeps its instance and its cursor and says its
  new name itself (`GraphScreen.SayNameIfChanged` against `GraphScreen.SpokenName`).
- `screens/ScreenDetector.cs` is the readiness layer: about 150 `On*Ready` / `On*Changed` /
  `On*Closed` handlers called from `patches/*Patches.cs`, each writing a screen's slot;
  `RecoverRuntimeState` points every slot at whatever is showing after a hot reload;
  `StorySequenceActive` is the flag the map's predicate reads. Its knowledge is the most
  expensive thing in the repo to lose: move it, never rewrite it.
- `ui/UIManager.cs` and `ui/Widget.cs` are the widget focus engine; no screen puts a widget in a
  tree any more. `AdventureMapGrid`, `TroopPlacementHexGrid` and `CombatHexGrid` still derive
  from `Widget` but are the modes' cursors, in no tree (drop the base in G). `TroopHudMenu` and
  every other `ui/*Widget.cs` are dead code for G. `Portrait` is still read by ported screens as
  a native-portrait reader. `TextInputEchoHelper` survives as the graph editor's echo.
  `TooltipActionsMenuScreen` (Backquote) is unreachable from any screen and goes in G with the
  `TooltipAction`s the adapters still build.
- Widget-era input actions (`input/AccessibilityActions.cs`): `next_widget`, `next_menu_item`,
  `activate`, `cancel`, `start_drag`, `slider_*`; the map, combat, scanner and bookmark sets
  are still the modes' keys and stay. The input stand-down for a focused game text box applies
  on graph screens only (`AccessibilityInputRouter.StandingDown`); lift the limit in G.
- Review buffers (`buffers/`, `ReviewBufferKind.Ui/AdventureMapNotifications/CombatEvents`)
  and speech (`SpeechPipeline.Output`, silenced by the router on every claimed key) stay as
  they are. Tests are MSTest under `tests/`.
- Every adapter still normalises text with `SpeechTextSanitizer.Normalize`, which collapses
  newlines; the graph cleans tooltip and details lines itself (`ui/SpokenLines.cs`). The
  sweep is phase G's.

## 2. What exists now: the graph side

Paths relative to `soc-access/`. The graph side as it stands; phase F rewires it, phase G
does not touch it.

- `ui/graph/` — the engine, 20 files copied from ES2, namespace `SongsOfConquestAccess.UI.Graph`.
  Changed only where the repo's rules required (`public`, `ModText`, `NodeHint.Template` a
  `ModString`). Never edit these for a screen's needs; re-sync against ES2 instead. Tests
  under `tests/` (`Graph*Tests`, `KeyGraphTests`, `GraphSheetTests`, `TypeAhead*Tests`, ...)
  with `tests/GraphFixtures.cs` as the helper.
- `screens/GraphScreen.cs` — the bridge. A screen ports by deriving from it, dropping its
  widget tree, and writing `Key`, `Layer`, `Build(GraphBuilder)`, `IsActive()`, and optionally
  `ScreenName`, `InitialFocusStop`, `Back()`/`ConsumesBack`, `IsWorkable` (mutes the live
  watch while the page fades and silences the re-seat when the focused control vanishes with
  the page), `AllowsTypeahead`, `CapturesRawInput`, `OwnsGameField` (a screen whose own
  editor holds or awaits a field; otherwise a field the game focuses on its own is released
  every frame), `TypeAheadScope`, `OnFocusVisual`. A screen reading a native menu derives from
  `LiveScreen<TAdapter>` and reads `Live`, which the detector writes.
- `ui/GraphNavigator.cs` + `.Search.cs` — the adapter: one `GraphState` per screen instance,
  `Attach`, `Claims`, `Dispatch`, `Update` (type-ahead tick then `EnsureFocus`, the
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
  `NodeHints.Chord`. Modes: `GraphScreen.ModeClaims(action)` is asked BEFORE the
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
  screen-level action hook the modes also use); over `adapters/WielderInteract.cs`. The
  map's HUD stop calls `Rows` alone.
- `ui/ArtifactSlotNodes.cs` over `adapters/IArtifactSlots.cs` — the Equipment and Inventory
  stops (Both Hands merge, positions, Auto arrange first, cargo `artifact`, `DropAccepts` off
  the game's `CanRearrangeArtifact`); each screen passes its own click meanings and hints.
  `ui/CommanderBands.cs` (stats band, modifier tab row + rows), `ui/SettlementNodes.cs`
  (defending-wielder band, garrison lines), `ui/RecruitGroups.cs` + `ui/ResourceCosts.cs`
  (a recruit or upgrade card as a collapsed group, or a line when it has no children).
- Input rule: a character typed in the frame the focused screen changed is dropped
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
- `dev/GraphDump.cs` — `/gui/graph?buffers=1&flat=1&edges=1&screen=KEY`, `GET /screens`,
  `POST /type`; `/status` reports `focusedNodeId`/`focusedNodeType`.
- Dev-loop guards: a failed `/eval` no longer breaks the game's type scans
  (`patches/DynamicAssemblyTypesPatches.cs`, dev-only); a reload logs posted-work failures.

Exemplars, one per kind, all approved and walked (phase F must leave each reading exactly as
its `walks/after/` capture does): menu page `screens/CampaignMenuScreen.cs`
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

A mode's keys (`GraphScreen.ModeClaims`) are asked before this whole table and run through
`OnAction`; anything else a screen takes (`ClaimsAction`) is asked after it. `troop_split_1..10`
(Ctrl+1..0) are claimed only while a troop row is focused.
Type-ahead ranks by match tier before list order; a chord is never typing; a group header
the game wires no click to gets no `OnActivate` (Right is the way in). `GraphState` is keyed by
screen instance; the singletons keep it while covered, and `KeepStateOnPop` keeps it across
a pop.

## 4. Verification and handover

`walks/after/<Screen>[-variant].txt` (gitignored, `GET /gui/graph?flat=1&buffers=1` on each
ported screen) is the reading every screen has today. After the manager swap, reopen each
screen, capture again, `sort -u` both and `diff`: any difference is a regression unless F's
approved design explains it (a cursor kept across a pop, a child screen). Walk with
`POST /input`, confirm with `/speech`, end with real keys (`POST /key`, which refuses while
the game window is not in the foreground) for Escape, typing and held keys. Measure the mod
frame with `docs/dev-loop.md` §4a on the map and in combat.

The handover is ONE SHORT PAGE per phase (`docs/phase-<x>-handover.md`, deleted when the phase
closes): which screens to test, what to watch for, the decisions taken, and what needs the
owner's attention. No key-by-key steps and no expected speech; detail goes in the commit
message and the doc comment. The owner tests; the phase is done when they say so.

Localization: every `ModString` added, removed or changed costs `update-pot`, real
translations in all 13 `.po` files, and `validate` passing. G removes strings; the validator
catches the stale entries.

## 5. Phases

Phases A to F are done: the engine and bridge, every screen, the composite grids with the
carry, the three modes, and the polling screen manager (2026-09-08).

### Phase G — cleanup

Delete `ui/UIManager.cs`, `ui/FocusContext.cs`, every `ui/*Widget.cs`, `ui/MenuWidget.cs`,
`ui/TableWidget.cs`, the three grid classes' widget base, `TooltipActionsMenuScreen` with its
Backquote action and the adapters' `TooltipAction`s, `/gui/widgets` and `dev/WidgetDump.cs`,
the widget-era input actions, `adapters/NativeTextPrompt.cs` (unused since the mod dialogs),
and the unused drag strings (`UI.DragStartedTroopPlacement`, `UI.DragComplete`). Sweep every
adapter's `SpeechTextSanitizer.Normalize` into per-line handling (`ui/SpokenLines.cs` is the
shape). Lift the graph-screens-only limit on the input stand-down. Update `AGENTS.md` (the
adapter rule stays; widget-tree wording becomes graph wording) and `screens/README.md`.

## 6. Screen inventory

Every registered screen: `MainMenuScreen`, `CampaignMenuScreen`,
`TaleSelectScreen`, `CustomCampaignSelectScreen`, `AdventureLobbyMapTypeScreen`,
`AdventureLobbyInviteProvidersScreen` (unverified, needs two providers), `MessageDialogScreen`
(three of seven sources verified), `QuitToDesktopPopupScreen`, `PlatformUserMenuScreen`,
`CommunityMapsModalScreen` (login variants unverified), `LoadingCompleteScreen`,
`OptionsScreen`, `AdventureLobbyRandomLayoutScreen`, `AdventureLobbyGameSettingsScreen`,
`AdventureLobbyPlayerSettingsScreen`, `OnlineHostGameScreen`, `CommunityMapsSearchFilterScreen`,
`DropListScreen`, `AdventureLobbyIconDropdownScreen`, `AdventureLobbyMapSelectScreen`,
`AdventureLobbyChallengeMapSelectScreen`, `OnlineGameListScreen`, `PlayerStatsScreen`,
`CodexScreen`, `SaveLoadGameScreen`, `CampaignMapSelectScreen`, `CommunityMapsHomeScreen`,
`CommunityMapsCollectionScreen`, `CommunityMapsSearchResultsScreen`, `CommunityMapsDetailsScreen`,
`AdventureLobbyPlayersScreen`, `ChatScreen` (the in-game recipient selector needs a
multiplayer game, unverified), `ModOptionsScreen`, `ModDialogScreen`, `PauseMenuScreen`,
`WorldConfirmMenuScreen`, `ClaimMenuScreen`, `TutorialSimpleScreen`, `TutorialSlideshowScreen`,
`StoryTextScreen`, `LevelUpScreen`, `PurchaseWielderScreen`, `ResearchScreen`,
`BuildMenuScreen`, `OwnedEntitiesScreen`, `TroopOverviewScreen`, `MapEntityMiniMenuScreen`,
`AdventurePlayerMenuScreen`, `GiftTownPopupScreen`, `SendResourcePopupScreen`,
`MarketplaceScreen`, `PostBattleResultScreen`, `PostAdventureResultScreen`,
`PostAdventureStatsScreen`, `CommanderSheetScreen`, `SpellbookScreen`, `MoveTroopPopupScreen`,
`WorldChoiceMenuScreen`, `ArtifactMarketScreen`, `TradingScreen`, `HostileJoinMenuScreen`,
`SettlementScreen`, `DefenceMenuScreen`, `TroopManagementScreenBase` (`DraftTroopsScreen`,
`UpgradeTroopsScreen`), `RallyPointScreen`, `AdventureMapScreen`, `PreBattleMenuScreen`,
`CombatScreen`.

Unverified corners, for want of a fixture: `MessageDialogScreen`'s random event and custom
message sources, a recruit card's essence tab row; on the map the teleport mode, the town
list, the chat and bug report buttons; on placement the hot-seat defender turn and Ready, an
attacker threat level, the scouting lines, the multiplayer wait; in combat the quickbar, a
defender wielder, the battle log, spell and ability aiming, the multiplayer names and turn
timer.

Not graph screens: `TooltipActionsMenuScreen` (a widget menu, unreachable now; deleted in G)
and `StoryFocusBlockerScreen` (a container; deleted in F, its flag becomes a predicate).

## 7. Risks

- `ScreenDetector`'s readiness knowledge is the most expensive thing in the repo to lose:
  move it, never rewrite it from memory.
- A phase is not done until `validate` passes and the owner has walked the result.
