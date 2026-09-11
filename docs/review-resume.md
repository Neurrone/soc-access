# Review fixes: resume file

For a session continuing the run that started 2026-09-11. The owner's short handover is
`docs/review-handover.md`; keep both current. The source list is `code-review.md` (untracked,
repo root); item numbers below refer to its sections.

## How the run is organised

- Work is delegated to Opus subagents, one area each, with a shared brief at
  `<scratchpad>\fix-brief.md` (scratchpad: `C:\Users\Dickson\AppData\Local\Temp\claude\C--Users-Dickson-desktop-projects-soc-access\b9cf375d-19bf-4bcc-b68c-186c4d8ba0dd\scratchpad`).
  If the scratchpad is gone, the brief's rules are the owner decisions listed in the handover
  plus: one logical commit per finding, `--no-gpg-sign`, build + tests + `Localization -- validate`
  green before each commit, commit trailers `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`
  and `Claude-Session: https://claude.ai/code/session_011a9WfYz2euiSfWZ2r8Z8dq`.
- Parallel agents work in git worktrees under `C:\Users\Dickson\Desktop\projects\soc-access-wt\<name>`
  on branches `wt-<name>`, created from main with `soc-access/GamePaths.props` copied in (it is
  gitignored). They build with `/p:DeployToGame=false` and never touch the game. Merge with
  `git merge --no-edit --no-gpg-sign wt-<name>` on main, then build, test, validate. The
  Agent tool's own worktree isolation fails here (path case: `desktop` vs `Desktop`); make
  worktrees by hand.
- Only one agent at a time owns the running game (dev server on 127.0.0.1:8772). It builds in
  the main checkout, reloads, drives, measures. `docs/dev-loop.md` is the loop.
- Baseline graph dumps of 65 screens under build 0a6d953 (+ the IsAlive fix, which changes only
  the destroyed-object case) are in `<scratchpad>\baseline\`: `<key>.txt` (tree, edges),
  `<key>.flat.txt`, `README.md` with the exact route to each screen, `timings.md` with a §4a
  build time per screen. Tree dumps truncate at 800 lines; `.noedges.txt` exists for the three
  that overflow, and `map-select` is complete only in flat form. Not reachable: platform user
  menu, player settings, invite providers (online lobby only), upgrade troops, and the
  event-driven screens (claim, world choice, hostile join, level up, post-battle, story,
  tutorial, bug report). `SpawnAt(37, …)` fails on this map; blueprint 69 works.

## Done (merged to main)

Round one (2026-09-12, merges 2d3a3b0..7718d7c):

- Correctness items 1-4, 6, 7, 10b, 10e, 10f on main (9b6a88b..523fac5). Items 9 and 10g were
  wrong findings. Item 5 landed in wt-slots (8c3f147), item 8 in wt-hexgrid (aa26f7c), 10a in
  wt-mapselect (50bd77e, 408045d), 10d and 10c are in the round-two adapters pass.
- §2 item 7 artifact slots: `adapters/InventorySlotReader.cs`, `SlotSnapshot<T>`, `ui/NodeMemo.cs`;
  §3 items 11 and 12 (market offers memo, tab names once).
- §2 item 8 lobby settings onto `MenuRows`/`MenuFormNodes` (`MenuRowSettings`, `MenuRowTimeInput`,
  `MenuFormNodes.AddTimeRow`), dropdown helpers public, `ui/SliderValueText` + `ModStrings.UI.Percent`,
  random layout memo, LobbyMapPreviewText onto SpokenLines.
- §2 item 11 challenge map select fork: `adapters/LobbyMapRow.cs`, `ui/LobbyMapNodes.cs`,
  `ui/CellText.cs`, `GraphNodes.MenuButton`; MapsLabel/GamesLabel deleted.
- §2 item 10 hex grids: `ui/HexGridScanner.cs`, `ui/HexGridMoves.cs`, `ui/WrapCue.cs`,
  `TileSkipNavigator.SpeakSkipped`; CombatScreen hands itself to the grid.
- §2 item 9 overlay: `adapters/FocusedTileOverlay.cs` (all three dispose it), `ScannerResult.Clone`,
  `ScannerTileKeys`, `adapters/ScreenInputOverride.cs`, `adapters/BattleFacadeState.cs`;
  PreBattleMenuAdapter refresh, AddTiles hoist, BuildTroopLabel via `Combat.TroopQuantity`.
- §2 items 12-16 and audio Clamp: `adapters/KingdomOverviewRead.cs`, `adapters/TroopManagementHost.cs`,
  `patches/KingdomOverviewFocus.cs`, `scanner/ScannerProjection.cs`, `scanner/ScannerScope.cs`,
  `ui/MenuCardPage.cs`; six `scene-scans.allow` lines pruned (stale, none added).

## Done, round two (merged 2026-09-12, main at 3cea894 plus merges)

- Adapters pass on main (44701cb..3cea894): `adapters/Reflect`, `SpokenText`, `GameObjects`,
  `EssenceText`, `ScrollView`, `CommunityMapsText`, `ContainerMemo`, `LobbyLabels`; 55 GetField
  copies, 48 text readers, 70 presence checks, 29 button-state and 10 invoke copies removed.
  Bug fixes in their own commits: community maps home/modal strip every tag (ef9816e), codex stat
  value cleaned (67dd8aa), destroyed component answers false (80bf81b), sibling click handler
  (9303406), container memo notices a rewritten last row (53a4ae8). Owner rule for HUD groups
  applied (7dbb9cd): only `BattleHudAdapter.IsOptionsButtonVisible` and
  `BattleCommanderHudAdapter.IsAiControlSideActive` changed. PopupMenuAdapter.IsButtonActive
  kept (the popup composes Active per button; comment added).
- wt-screens: `ui/MarkerTable` + `GraphScreen.Marker`, `ui/DrawnOrder`, `GraphNodes.DrawnClose/
  ModClose/SyntheticButton/TextLine/AddValue/SwitchingTab/TmpEditField`, `ui/SectionItems` (logs
  once per screen), `ui/EssenceRows`, `TroopHudRows.NameWithPlace` string overload, virtual
  `Editor` and `BackButton` on GraphScreen, generic `Panel<T>` in CommunityMapsSources.
  Twenty-one per-screen marker comments were deleted with their fields.
- wt-events: dead ArmyExchange event, Localize, ctors, Boiyu probes; fog array no longer copied,
  discovery sweep gated on a game-read key; one utterance log line; event text composed once;
  CueLibrary no clone at scale 1; ReviewBuffer capped at 2000; one pending ledger; one square
  coordinate formatter (`Spatial.Coordinates`); one AddIfPresent and one scanner speech context;
  separators are ModStrings (`Common.Sentence`, `SentenceSeparator`, `ClauseSeparator`,
  `PhraseSeparator`, `Spatial.MovementOfMax`); three listener catches removed. NoisePool and
  BufferGrain's array ctor kept: tests use them (owner verdict needed to delete both).
- wt-core: announcement order memo, camera-focus source dropped, `UI.ModReady`, notification
  join via LabelValue, ModRoutes gated on DevServerUp, PlatformUserMenuPatches deleted (two
  allowlist rows removed, the only allowlist edit), input dead code, ModSettings split into
  partials. SpeechPipeline.Observer is main-thread on both sides (no sync needed).
- wt-tests: ScannerFixtures, ModSettingsFixture, TileFixtures, Graphs.Keys; ScannerControllerTests
  split; 33 new tests (PoTranslationCatalog, ModText.JoinList, TextUtil, TroopPlacementTile
  formatter, ArtifactSpeechFormatter, GameText.FormatFallback). 932 tests total. A game
  `IDetails` type cannot load in the net472 test host (default interface methods).
- .po merges conflict at the file end when two branches add strings; resolve by keeping both
  blocks, then `Localization -- validate`. Rewrite with LF (the repo's .po files are LF).

## In progress (round three, launched 2026-09-12)

- Game pass after round two (done): 52 of 65 screens byte-identical, the rest explained by the
  intended changes or live content; no regressions; combat click, ability confirm, troop drag/
  merge/split, popup buttons, foldouts and sibling-handler clicks all work. Timing deltas
  confirmed over repeats: challenge-map-select 1.42 -> 0.22 ms; community-maps-home 0.59 -> 1.03
  and player-stats 0.90 -> 1.24 (tag stripping per row per frame, the menus pass memoises both);
  map-select 1.83 -> 2.1 (noise band; re-check). Dumps under the scratchpad folder `after-round2`.
- wt-uiperf (merged): troop rows and settings rows built once per list, tile hints two per key,
  tooltip dossier check memoised per widget, ScreenManager buffers, ScreenSource.Invalidate gone,
  ArtifactSlotNodes caption per column, ModeFor logs once. PointerHover.Release was already wired.
- wt-advmap (merged edf14e3): reachable set once a frame, zone-of-control and commander index
  once a frame (FindCommanderById kept: the native Get(id) returns destroyed commanders from the
  fog memory cache), HUD resolvers and canvas groups remember misses, teleport mesh once, tile
  lists lazy, kingdom tooltips guarded, node ids and wording in screens (`ui/ResourceStrip`,
  `Screens.WielderExperience`, `Screens.UpgradeTiers`), five `Events.Map*` failure strings,
  relationship enum on the tile (+TileCueSelector), third ScreenInputOverride copy gone,
  `adapters/LogOnce` for static helpers, catches log once, AdventureMapAdapter and
  AdventureHudAdapter split into partials (largest partial 1450 lines). Follow-up: GetTile still
  calls BuildRoutePreviewInfo per tile (cleanup round).
- wt-combat (merged 3e2eb48): combat speech in CombatScreen, `ui/CombatTroopText`,
  `ui/SpellTooltipText`, BattleHud dead members and per-frame caches, one tile read/path/sweep
  per cursor step, narrator diagnostics deleted, narration simplified (planner tests unchanged
  in what they assert), modifier names from the game's `Modifiers/<type>` keys, a bacteria name
  no longer lower-cased in damage lines, `Common.Parenthetical`, `Common.PositiveAmount`,
  `Combat.Spell`, catches log once, five partial-file splits (CombatAdapter.cs still 2253 lines).
- wt-economy (merged 506a61c): node ids in screens (Claim, Spellbook, LevelUp, SendResource,
  PurchaseWielder, hosts), `ui/SpellCosts`, four `Screens.*Number` strings, marketplace/research/
  build memos, non-allocating IsPresent, dead members, purchase-wielder focus no longer re-clicks
  the shown candidate, interaction menus not present once their building is gone (6ac77c6).
- wt-menus (merged 1acf83b): codex, save/load, bug reporter, platform user, options, mod
  options entries (scans only where their menu can be), dialogue menu, button text, community
  maps, player stats, post-adventure, dialog bodies (`adapters/BodyText`), chat cache on the
  adapter, facts-not-wording in lobby/game list/adventure result, last English literals,
  campaign sentence joins, main-menu foldouts via native pointer-enter, mod dialog scene scan in
  `adapters/OptionsMenuHost`, node ids for invite providers and player stats, catches.
  Left: GameTextEditor.Reset unwired, ChatAdapter.ButtonLabel, MenuRows/IDropList/community
  constant ids, CommunityMapsModalScreen.Adapt, BugReport slider clamp (cleanup round).
- Merge hazard found: `git rerere` was enabled and re-applied an earlier .po resolution to the
  wt-advmap merge, silently dropping three combat strings from every .po; restored in the commit
  after edf14e3 and rerere disabled in this repository's config (`git config rerere.enabled
  false`; the owner may want it back). Always run `Localization -- validate` after a merge.
- Round three verification pass: not yet run; the final game pass covers rounds three and four.
- wt-uiperf: TroopHudRows and MenuFormNodes node caches, TileInstructionHints, lazy tooltip
  mode, ScreenManager buffers, ScreenSource.Invalidate, PointerHover reset wired into Stop,
  ArtifactSlotNodes static memo.

## Cleanup round (merged 2026-09-12)

- wt-cleanup1 (fast-forwarded to d7d13c4): CombatAdapter split to eight partials (largest 430
  lines, main file 1165); `adapters/LargeCostSectionText` shared by the build and purchase
  menus (BuildMenuAdapter 1414 after `BuildMenuAdapter.Items.cs`); route preview memoised per
  frame; CommanderHudPortraitAdapter id dropped; MapEntityDestroyed event constant deleted;
  spellbook tooltip via `ui/SpellTooltipText`; purchase status via SentenceSeparator;
  `ui/ChatButtonText`; stale comments fixed. Two small spoken changes named in the commits:
  a spell with no game name says "spell"; a blank purchase tooltip line no longer yields ". ".
- wt-cleanup2 (merged 9ddcd76): `UI.RoleGrid` deleted with its translations; GameTextEditor
  reset wired into Stop; cartography converter parameter dropped through the story patch;
  `GET /gui/graph?lines=N`; adapters hand out keys (`Key`, `RowKey`, `KeyPrefix`) and screens
  spell ids, three unread ids deleted; save/load visible rows ordered on the adapter; BugReport
  six slider stops are right (comment fixed, "Massive" is Critical); three GraphNavigator
  catches commented.
- 9d67835: OptionsScreen spells the tab ids (last decision-4 site).
- wt-cleanup3 (running): repo-wide silent-catch sweep, eleven candidate sites in eight files.
- NoisePool + NoisePoolTests: owner verdict (tests keep dead production code alive).

## Final pass (2026-09-12)

- Game pass at 425d0f1 then 0ba8a6e: 65 screens diffed against after-round2; every screen
  byte-identical or explained by live content; no new warnings, no exceptions; all changed
  paths work (combat cursor/inspect/targeting/essences/log/timeline, map route/scanner/sweep/
  teleport/hints/HUD, settlement pages, wielder sheet, trade, options, save/load, foldouts,
  codex, community maps, bug report slider, announcement order). Dumps under the scratchpad
  folder `after-round3`.
- Two regressions found and fixed after it: the chat Send label went blank because
  `Common/Chat/Send` is the game log's key and the chat window's handler does not resolve it
  (08b46c4: native button text, then the key, then `Screens.ChatSend`); the rally point screen
  still outlived its destroyed entity because `DestroyMapEntityCommand` removes without
  disposing (cb0219e: the three interaction menus also ask `MapEntities.Exists(id)`). Both
  verified on a fresh launch of 08b46c4 (chat reads "Send" from the native button; the rally
  point screen leaves within two seconds of the destroy, the game's own menu object stays drawn).
- Player stats: 0.76-0.78 ms on the fresh process; the 1.38-1.47 was hot-reload inflation.
  Adapter reads are 0.09 ms of it and the row memo is hit; no fix needed.
- The game was closed with POST /quit at the end (owner's instruction).
- wt-fallbacks (merged 0ba8a6e): 84 verified keys lose their English fallback; 13 sites keep
  theirs unverified (listed in 7b5f64b..0ba8a6e bodies); `BuildMenuAdapter.IsSectionHeader`'s
  English second match path is gone.
- Worktrees and wt-* branches removed after merging; everything is in main's history.

## Remaining

Nothing in progress. Left for the owner or a later session, on purpose: `ui/graph/` engine items (§3.16, dead
   engine members, edge labels per cell); NoisePool + its tests; `dev/UnityDump` empty catches;
   golden-graph tests through the dev server; the rerere setting; the 13 unverified fallbacks.
