# The UI graph

How the mod's accessible tree is built and driven. Every screen is an immediate-mode graph
screen; the retained widget tree it replaced is gone (the rewrite ran in phases A to G,
2026-09). Paths relative to `soc-access/`. Read `../endless-space-2-access/docs/generic/`
(`ui-navigation.md`, the adapter section on screens, `input.md`, `performance.md`) for the
doctrine this engine was copied from.

## 1. The graph side

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

Exemplars, one per kind: menu page `screens/CampaignMenuScreen.cs`
(header band + cards, drawn-order sort, `IsWorkable`); dialog `MessageDialogScreen.cs` (the
three-part contract, per-source Escape, an edit field); form `OptionsScreen.cs` (tabs, regions
per caption, rows, scroll-into-view through native selection); table
`AdventureLobbyMapSelectScreen.cs` (filters stop first, a sheet with pieces, a details node);
browse page `CodexScreen.cs` (a list stop with regions, a content stop named after its
heading); a table of control cells `AdventureLobbyPlayersScreen.cs`; radio group
`AdventureLobbyRandomLayoutScreen.cs`; chat `ChatScreen.cs`; mode-less loading page
`LoadingCompleteScreen.cs`.


## 2. How the navigator handles keys

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
| letters, Space mid-search | `AllowsTypeahead && !CapturesRawInput && !GameTextEditor.Owned`, no Ctrl or Alt held, no game box focused | type-ahead over the focused stop plus the fully-open build |

A mode's keys (`GraphScreen.ModeClaims`) are asked before this whole table and run through
`OnAction`; anything else a screen takes (`ClaimsAction`) is asked after it. `troop_split_1..10`
(Ctrl+1..0) are claimed only while a troop row is focused.
Type-ahead ranks by match tier before list order; a chord is never typing; a group header
the game wires no click to gets no `OnActivate` (Right is the way in). `GraphState` is keyed by
screen instance; the singletons keep it while covered, and `KeepStateOnPop` keeps it across
a pop.


## 3. Verifying a screen

`GET /gui/graph?flat=1&buffers=1` is a screen's whole reading; capture it before and after a
change, `sort -u` both and `diff`. Walk with `POST /input`, confirm with `/speech`, end with
real keys (`POST /key`) for Escape, typing and held keys. Measure one build with
`docs/dev-loop.md` §4a before handing a screen over; over one millisecond is a finding.
