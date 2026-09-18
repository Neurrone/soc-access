using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Deployment;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The troop placement page a battle opens on, and the smallest of phase E's three MODES: the
    /// deployment board is one node whose cursor is not the focus cursor
    /// (<see cref="TroopPlacementHexGrid"/>), with the menu's own panels as ordinary stops around it.
    /// A sibling of <see cref="PostBattleResultScreen"/> - the same <c>AdventureBattleMenu</c> frame,
    /// the same Attacker/Defender sides, the same commander-as-a-line - so it is built the same way.
    ///
    /// Measured on the 1280x800 fixture 2026-09-08 (<c>PreBattleMenu</c>, Cecilia Stoutheart against
    /// a neutral "Risen Dead"): the attacker's panel LEFT (<c>AttackerCommander</c> [177,73,335,654],
    /// the name at x 325 beside a portrait <c>Button</c> [225,121,65,65] carrying the wielder's stat
    /// tooltip), the defender's RIGHT (<c>DefenderCommander</c> [768,73], the name at x 681 with NO
    /// portrait button - a neutral army draws none), the defender's
    /// <c>DefenderThreathLevelHolder</c> [844,89] holding "Threat Level - Fair" and carrying the
    /// tooltip "Scouting provided by Cecilia". At the bottom the hint "Drag troops to rearrange"
    /// [325,568], the instruction "Place attacker troops" [212,583], and the three buttons Withdraw
    /// (x 412), Manual Battle (x 547) and Quick Battle (x 720), declared in the order of their drawn
    /// left edges, measured every build.
    ///
    /// THE BOARD IS ONE NODE with a fixed identity, alone in a stop NAMED BY THE INSTRUCTION the menu
    /// draws ("Place attacker troops"), so entering it says what the game is asking for. Its label is
    /// the tile under the cursor and its buffer that tile's own troop tooltip.
    /// <see cref="ModeClaims"/> hands the grid its whole key set - the six hex moves, their skips,
    /// Ctrl+Space for the centre tile, the scanner families - and TRANSLATES the graph's own Home,
    /// End and Backspace onto the scanner's jump, distance and return, exactly as the map does, so an
    /// injected key behaves as the physical one. The grid speaks each landing itself, queued; the
    /// navigator never announces a move because the node's id never changes.
    ///
    /// THE DRAG IS THE ENGINE'S CARRY. Space picks a troop up off a tile the player owns (cargo is
    /// the tile point, kind "pre-battle-troop", with the game's own <c>Common_PreBattle_Grab</c>
    /// registered through <see cref="CarrySounds"/>), Enter drops it on the tile under the cursor
    /// through the game's own <c>Grab</c>-and-<c>Drop</c> pair, and Escape gives up. Every tile takes
    /// a drop: the GAME decides whether a destination is legal, and a move it refused answers with
    /// the existing "Invalid destination". The widget era's "draggable"/"dragging" wording is gone -
    /// the carry says both itself.
    ///
    /// THE DESCRIPTION STOP is what the battlefield IS: the authored description of the layout this
    /// battle is fought on (<see cref="BattlefieldDescriptions"/>, keyed by the map's own
    /// LevelType/PathName and found by the path alone where the authored type disagrees), three
    /// lines of one node, followed by the drag hint that was this stop before it. It sits after
    /// the two sides and before the board (owner ruling 2026-09-17). The same three lines answer
    /// Ctrl+D on the board, which is where a layout nobody has described yet says so.
    ///
    /// TYPE-AHEAD is on everywhere EXCEPT the board (owner ruling 2026-09-08): the side panels, the
    /// buttons and the hint search normally, while on the board the letters A, D, Q, E, Z and C are
    /// the hex moves. <see cref="AllowsTypeahead"/> is therefore a live answer rather than a
    /// constant, which <c>GraphNavigator.TypeAheadArmed</c> already asks per keypress and per frame,
    /// so a search still live when the cursor lands on the board is dropped on the next tick.
    ///
    /// ESCAPE IS THE GAME'S (<see cref="ConsumesBack"/> false): <c>PreBattleMenu</c> registers no
    /// exit action of its own, and Withdraw has side effects. The navigator still cancels a held
    /// carry with it first, which is the carry's own rule.
    ///
    /// UNVERIFIED on this fixture: the attacker's threat level and both scouting-information lines
    /// (the game draws neither against an unscouted neutral army), the Ready button and the
    /// defender-placement instruction (hot-seat and multiplayer states only).
    /// </summary>
    public sealed class PreBattleMenuScreen : LiveScreen<PreBattleMenuAdapter>
    {
        private const string AttackerStop = "pre-battle-attacker";
        private const string DefenderStop = "pre-battle-defender";
        private const string GridStop = "pre-battle-grid";
        private const string ButtonsStop = "pre-battle-buttons";
        private const string DescriptionStop = "pre-battle-description";

        /// <summary>The cargo kind of a troop lifted off the deployment board. Its own kind rather
        /// than the army bar's "troop": the two carries are different drags with different noises,
        /// and <see cref="CarrySounds"/> is keyed by kind.</summary>
        private const string TroopCargo = "pre-battle-troop";

        /// <summary>What the game's own mouse plays when it lifts a troop off a spawn point
        /// (<c>DeploymentUIController.Grab</c>).</summary>
        private const string PickUpSound = "Common_PreBattle_Grab";

        /// <summary>The one node the whole board is. Fixed, so walking the cursor is never a move as
        /// far as the navigator is concerned.</summary>
        public static readonly ControlId GridNodeId = ControlId.Structural("pre-battle:tile");

        // The placement cursor, built over the menu it walks. Rebuilt when the slot is pointed at a
        // DIFFERENT battle and kept otherwise.
        private TroopPlacementHexGrid _hexGrid;
        private PreBattleMenuAdapter _hexGridAdapter;

        // The tile tooltip is the game's whole troop-details capture and the graph is rebuilt for
        // every navigation operation, so it is composed once per tile - which is exactly as often as
        // the widget engine's focus commit composed it.
        private Vector2Int _tooltipTile;
        private Tooltip _tooltip;
        private bool _tooltipRead;

        // The instruction the menu rewrites as the deployment moves through its states, watched
        // passively: baselined on arrival, so only a CHANGE is spoken.
        private string _instruction;

        /// <summary>The cursor is built over one placement: a new one gets a new grid. The tile
        /// tooltip is about that same placement's board, so it is dropped here too - keyed on the
        /// identity of the adapter, which <see cref="LiveScreen{TAdapter}.SyncLive"/> reads from
        /// the game every frame. Without this a second battle whose cursor starts on the same
        /// coordinates would be served the previous battle's tooltip.</summary>
        private TroopPlacementHexGrid HexGrid()
        {
            if (_hexGrid != null && ReferenceEquals(_hexGridAdapter, Live))
            {
                return _hexGrid;
            }

            _hexGridAdapter = Live;
            _hexGrid = Live == null ? null : new TroopPlacementHexGrid(Live, ReadGridTile);
            _tooltip = null;
            _tooltipRead = false;
            return _hexGrid;
        }

        // The battle menu the adventure scene binds, and the placement page it holds in its
        // settings: the page is a child of one menu that lives for the whole game, so it is read
        // through its owner rather than looked for (AGENTS.md, "Screen Resolution").
        private readonly ScreenSource<IAdventureBattleMenu> _battleMenu =
            ScreenSource<IAdventureBattleMenu>.FromScene(LoadedScenes.AdventureScene);

        private readonly ScreenSource<PreBattleMenu> _source;

        public PreBattleMenuScreen()
        {
            _source = ScreenSource<PreBattleMenu>.FromOwner(
                _battleMenu,
                battleMenu => PreBattleMenuAdapter.GetPreBattleMenu((AdventureBattleMenu)battleMenu));
        }

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override PreBattleMenuAdapter Adapt(object menu)
        {
            return new PreBattleMenuAdapter((PreBattleMenu)menu);
        }

        public override string Key
        {
            get { return "pre-battle-menu"; }
        }

        /// <summary>Layer 12: over the map, under what the battle raises.</summary>
        public override int Layer
        {
            get { return 12; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.TroopPlacement); }
        }

        /// <summary>On everywhere but the board, where A, D, Q, E, Z and C are the hex moves rather
        /// than letters to search with. Read live by the navigator, per keypress and per frame.</summary>
        public override bool AllowsTypeahead
        {
            get { return !IsGridFocused(); }
        }

        public override void OnPush()
        {
            // A FRESH CURSOR PER VISIT: the adapter lives as long as the placement menu
            // object, which outlives one battle, so the board cursor is dropped here
            // rather than when the slot changes.
            _hexGrid = null;
            _hexGridAdapter = null;
            Live?.AddDeploymentChangedHandler(HandleDeploymentChanged);
            _instruction = Live != null ? Live.InstructionText : null;
        }

        public override void OnUnfocus()
        {
            Live?.HideNativeTooltip();
            Live?.ClearFocusedTileOverlay();
            base.OnUnfocus();
        }

        public override void OnPop()
        {
            Live?.RemoveDeploymentChangedHandler();

            Live?.HideNativeTooltip();
            Live?.ClearFocusedTileOverlay();
            base.OnPop();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            WatchInstruction();
        }

        /// <summary>The menu rewrites the instruction as the deployment changes hands ("Place
        /// attacker troops", "Waiting for opponent", "Ready to battle"). Nobody is standing on it -
        /// it is the board's stop name - so it is watched and said, queued, when it changes.
        ///
        /// The instruction changes exactly when the placement state does, and a hot-seat hand-over
        /// makes the board the OTHER side's without the deployment menu raising anything
        /// (<c>HandleHotSeatDefenderPlacementEnter</c> clears the troops and lays out the other
        /// side's directly): the grid is rebuilt here so the cursor starts again at the side that is
        /// now placing, the tile it lands on is read out behind the instruction where the player is
        /// on the board, and anything the player who is leaving picked up is let go, as
        /// <see cref="HandleDeploymentChanged"/> lets it go there.</summary>
        private void WatchInstruction()
        {
            string text = Live != null ? Live.InstructionText : null;
            if (string.Equals(text, _instruction, StringComparison.Ordinal))
            {
                return;
            }

            _instruction = text;
            ReleaseCarry();
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }

            _tooltipRead = false;
            HexGrid()?.RebuildAfterStateChanged(IsGridFocused());
        }

        /// <summary>The board changed: a troop was placed or moved. The cursor keeps its tile, the
        /// tile is read again where the player is standing on the board, and anything being carried
        /// is let go - the tile it came from no longer holds what was picked up.</summary>
        private void HandleDeploymentChanged(OnChangedPayload payload)
        {
            ReleaseCarry();
            _tooltipRead = false;
            HexGrid()?.RebuildAfterPlacementChanged(IsGridFocused());
        }

        /// <summary>Let go of a troop being carried: the tile it was lifted off no longer holds it.</summary>
        private void ReleaseCarry()
        {
            GraphNavigator navigator = Navigator;
            if (navigator != null && navigator.Carry != null)
            {
                navigator.Carry.Clear();
            }
        }

        // ---- the graph ----

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            // Each side's stop is NAMED, "Attacker" and "Defender", as the post-battle page's are:
            // the game draws no such caption, so the words are the mod's.
            builder.BeginStop(AttackerStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Attacker));
            ControlId start = BuildSide(builder, attacker: true);
            builder.PopContext();

            builder.BeginStop(DefenderStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Defender));
            BuildSide(builder, attacker: false);
            builder.PopContext();

            // The description before the board (owner ruling 2026-09-17): what the battlefield is
            // comes before where the troops stand on it.
            BuildDescription(builder);
            BuildGrid(builder);
            BuildButtons(builder);

            if (start != null)
            {
                // Top left, where the page is read from.
                builder.SetStart(start);
            }
        }

        // ---- one side of the page ----

        /// <summary>One side's panel, in drawn order: the commander, the threat level the menu draws
        /// for a side that was scouted, and the partial scouting information it draws instead of the
        /// full army. Answers the commander's node, which is where the page starts.</summary>
        private ControlId BuildSide(GraphBuilder builder, bool attacker)
        {
            string key = attacker ? "attacker" : "defender";
            ControlId commander = BuildCommander(builder, attacker, key);

            AddLine(
                builder,
                key + "-threat",
                attacker ? (Func<string>)(() => Live.AttackerThreatText) : () => Live.DefenderThreatText,
                attacker ? Live.AttackerScoutingTooltip : Live.DefenderScoutingTooltip);

            // The scouting information is one line per troop the scout saw; a Text node would put
            // the whole block in the review buffer as one line.
            Func<string> scouting = attacker ? (Func<string>)(() => Live.AttackerScoutingText) : () => Live.DefenderScoutingText;
            if (!string.IsNullOrWhiteSpace(scouting()))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker(key + "-scouting"), "pre-battle:" + key + "-scouting"),
                    GraphNodes.Paragraphs(() => SpokenLines.Of(new[] { scouting() }))));
            }

            return commander;
        }

        /// <summary>The commander's name as the portrait the game draws it beside: a line carrying the
        /// portrait's native tooltip, which is where the stats, skills and status are read. A side
        /// with no portrait - a neutral army - is the name alone.</summary>
        private ControlId BuildCommander(GraphBuilder builder, bool attacker, string key)
        {
            Func<string> name = attacker
                ? (Func<string>)(() => Live.AttackerName)
                : () => Live.DefenderName;
            if (string.IsNullOrWhiteSpace(name()))
            {
                return null;
            }

            Component button = attacker ? Live.AttackerPortraitButton : Live.DefenderPortraitButton;
            Tooltip tooltip = button == null
                ? null
                : (attacker ? Live.AttackerPortraitTooltip : Live.DefenderPortraitTooltip);
            NodeVtable vtable = GraphNodes.Text(name, null, tooltip);
            if (button == null)
            {
                ControlId synthetic = ControlId.For(Marker(key + "-commander"), "pre-battle:" + key + "-commander");
                builder.AddItem(new SyntheticNode(synthetic, vtable));
                return synthetic;
            }

            // The mouse resting on the portrait is what makes the game draw its tooltip, and the
            // portrait refreshes its own contents on the way.
            vtable.OnFocusVisual = attacker
                ? (Action)Live.FocusAttackerPortrait
                : Live.FocusDefenderPortrait;
            ControlId id = ControlId.For(button, "pre-battle:" + key + "-commander");
            builder.AddItem(new DrawnNode(id, vtable, button));
            return id;
        }

        // ---- the board ----

        /// <summary>The deployment board: one node, named by the tile under the cursor, alone in a
        /// stop the menu's own instruction names.</summary>
        private void BuildGrid(GraphBuilder builder)
        {
            builder.BeginStop(GridStop);
            builder.PushContext(GridContext());

            // The game's own drag noise, for the keyboard's carry. Registered on every build: the
            // registration is a delegate over this load and must not outlive it.
            CarrySounds.Register(TroopCargo, () => NativeSoundUtility.PostEvent(PickUpSound), null);

            NodeVtable vtable = GraphNodes.Text(() => HexGrid().GetLabel(), null, TileTooltip());
            vtable.OnFocusVisual = () => HexGrid().ShowOverlay();
            vtable.OnBlurVisual = () => HexGrid().HideOverlay();

            // Only a troop of the player's own can be lifted, and every tile takes a drop: which
            // destinations are legal is the GAME's answer, given when the drop replays its drag. The
            // pick-up is DECLARED ON EVERY TILE and answers for itself with null where there is
            // nothing to give, which is the engine's contract for a pure query - and here it is also
            // what keeps the readout's part COUNT the same from tile to tile, so the live watch does
            // not read the whole node a second time every time the cursor steps off a troop.
            vtable.OnPickUp = () => HexGrid().CanPickUp
                ? new CarryItem(HexGrid().CursorTile, HexGrid().FocusedTroopLabel, TroopCargo)
                : null;

            vtable.DropKind = TroopCargo;
            vtable.OnDrop = Drop;
            builder.AddItem(new SyntheticNode(GridNodeId, vtable));

            builder.PopContext();
        }

        private string GridContext()
        {
            string instruction = Live != null ? Live.InstructionText : null;
            return string.IsNullOrWhiteSpace(instruction) ? ModText.Get(ModStrings.Screens.TroopPlacement) : instruction;
        }

        private Tooltip TileTooltip()
        {
            Vector2Int tile = HexGrid().CursorTile;
            if (_tooltipRead && tile == _tooltipTile)
            {
                return _tooltip;
            }

            _tooltipTile = tile;
            _tooltipRead = true;
            _tooltip = HexGrid().GetTooltip();
            return _tooltip;
        }

        /// <summary>The drop, through the game's own grab-and-drop pair. A destination the game
        /// refuses leaves the deployment untouched and the troop still held.</summary>
        private SongsOfConquestAccess.UI.Graph.DropResult Drop(CarryItem held)
        {
            Vector2Int? source = held == null ? null : held.Cargo as Vector2Int?;
            return source.HasValue && Live.TryMoveTroop(source.Value, HexGrid().CursorTile)
                ? SongsOfConquestAccess.UI.Graph.DropResult.Done()
                : SongsOfConquestAccess.UI.Graph.DropResult.Refused(ModText.Get(ModStrings.UI.InvalidDestination));
        }

        // ---- the buttons and the hint ----

        /// <summary>The buttons the menu is drawing, in the order of their drawn left edges, measured
        /// every build: Withdraw, Manual Battle, Quick Battle, and the Ready button the hot-seat and
        /// multiplayer states add.</summary>
        private void BuildButtons(GraphBuilder builder)
        {
            List<KeyValuePair<float, NodeDeclaration>> drawn = new List<KeyValuePair<float, NodeDeclaration>>(4);
            AddButton(drawn, "withdraw", Live.IsWithdrawButtonVisible(), Live.WithdrawButton,
                () => Live.WithdrawButtonLabel, () => Live.Withdraw(),
                Live.IsWithdrawButtonEnabled, () => Live.WithdrawButtonTooltip, Live.FocusWithdrawButton);
            AddButton(drawn, "manual-battle", Live.IsManualBattleButtonVisible(), Live.ManualBattleButton,
                () => Live.ManualBattleButtonLabel, () => Live.ManualBattle(),
                Live.IsManualBattleButtonEnabled, () => Live.ManualBattleButtonTooltip, Live.FocusManualBattleButton);
            AddButton(drawn, "quick-battle", Live.IsQuickBattleButtonVisible(), Live.QuickBattleButton,
                () => Live.QuickBattleButtonLabel, () => Live.QuickBattle(),
                Live.IsQuickBattleButtonEnabled, () => Live.QuickBattleButtonTooltip, Live.FocusQuickBattleButton);
            AddButton(drawn, "ready", Live.IsReadyButtonVisible(), Live.ReadyButton,
                () => Live.ReadyButtonLabel, () => Live.Ready(),
                Live.IsReadyButtonEnabled, () => Live.ReadyButtonTooltip, Live.FocusReadyButton);
            if (drawn.Count == 0)
            {
                return;
            }

            DrawnOrder.SortByKey(drawn);
            builder.BeginStop(ButtonsStop);
            for (int i = 0; i < drawn.Count; i++)
            {
                builder.AddItem(drawn[i].Value);
            }
        }

        private void AddButton(
            List<KeyValuePair<float, NodeDeclaration>> into,
            string key,
            bool drawn,
            Component button,
            Func<string> label,
            Func<bool> activate,
            Func<bool> enabled,
            Func<Tooltip> tooltip,
            Action focus)
        {
            if (!drawn || button == null)
            {
                return;
            }

            // The tooltip is composed AFTER the drawn check: a button the menu is not showing must
            // not cost a read of the game's details for it.
            NodeVtable vtable = GraphNodes.Button(label, () => activate(), enabled, tooltip());
            vtable.OnFocusVisual = focus;
            into.Add(new KeyValuePair<float, NodeDeclaration>(
                DrawnOrder.LeftOf(button),
                new DrawnNode(ControlId.For(button, "pre-battle:" + key), vtable, button)));
        }

        /// <summary>
        /// WHAT THIS BATTLEFIELD IS, and how to move a troop on it: the authored description of the
        /// layout the battle is fought on, three lines of one node so the review buffer holds three,
        /// and then the hint the menu draws under the board ("Drag troops to rearrange"), which was
        /// this stop before the description joined it (owner ruling 2026-09-08).
        ///
        /// The build asks only WHETHER the layout is described, which is one dictionary hit; the
        /// three lines are composed when the node is read. A layout nobody has written about yet has
        /// no node at all rather than an empty one - the gesture is where "no description" is said.
        /// </summary>
        private void BuildDescription(GraphBuilder builder)
        {
            BattlefieldDescription description;
            bool described = BattlefieldDescriptions.TryGet(Live.BattlefieldKey, out description);
            bool hint = !string.IsNullOrWhiteSpace(Live.DragHintText);
            if (!described && !hint)
            {
                return;
            }

            builder.BeginStop(DescriptionStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Description));
            if (described)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker("description"), "pre-battle:description"),
                    GraphNodes.Paragraphs(() => Describe())));
            }

            if (hint)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker("hint"), "pre-battle:hint"),
                    GraphNodes.Text(() => Live.DragHintText)));
            }

            builder.PopContext();
        }

        /// <summary>The description's three lines, composed when the node is read.</summary>
        private IList<string> Describe()
        {
            BattlefieldDescription description;
            BattlefieldDescriptions.TryGet(Live.BattlefieldKey, out description);
            return BattlefieldText.Lines(description, Live.GetTerrain(), Live.WarnUnknownRegion);
        }

        // ---- keys ----

        /// <summary>
        /// The tile cursor's whole key set, while the board's node is the one the cursor is on. Asked
        /// BEFORE the navigator's own set, which is what makes the hex letters walk the board rather
        /// than start a search; on any other stop none of it is claimed.
        /// </summary>
        public override bool ModeClaims(string actionKey)
        {
            return IsGridFocused() && HexGrid() != null && ModeAction(actionKey) != null;
        }

        /// <summary>The board action a key means here, or null where the key is not the cursor's. The
        /// mod's own hex and scanner keys answer for themselves; Home, End and Backspace are
        /// TRANSLATED onto the scanner's jump, distance and return, so the dev server's injections
        /// behave exactly as the physical keys the router resolves to the scanner first. The arrows
        /// are NOT translated: a hex board has no north or south, and its six directions are the
        /// letters.</summary>
        private string ModeAction(string actionKey)
        {
            if (HexGrid().ClaimsAction(actionKey))
            {
                return actionKey;
            }

            if (actionKey == AccessibilityActions.UiHome.Key)
            {
                return AccessibilityActions.ScannerJumpToResult.Key;
            }

            if (actionKey == AccessibilityActions.UiEnd.Key)
            {
                return AccessibilityActions.ScannerSpeakDistanceAndDirection.Key;
            }

            if (actionKey == AccessibilityActions.UiClearSearch.Key)
            {
                return AccessibilityActions.ScannerReturnFromJump.Key;
            }

            return null;
        }

        public override bool OnAction(string actionKey)
        {
            if (!IsGridFocused() || HexGrid() == null)
            {
                return false;
            }

            string action = ModeAction(actionKey);
            return action != null && HexGrid().HandleAction(AccessibilityActions.FindByKey(action));
        }

        /// <summary>
        /// The board's tile cursor has landed on another tile, and the readout is deliberately the
        /// NAVIGATOR's rather than the grid's. The whole board is one node, so a step inside it
        /// moves no focus and the engine would say nothing on its own; a tile said in the grid
        /// instead would be the bare label, without the node's tooltip and without its usage hints.
        ///
        /// A landing only ever announces where the board already has the cursor: the one landing
        /// that comes from elsewhere - the deployment changing under a side panel - is asked not to
        /// announce at all (<see cref="TroopPlacementHexGrid.RebuildAfterPlacementChanged"/>).
        /// </summary>
        private void ReadGridTile()
        {
            if (IsGridFocused())
            {
                Navigator.ReannounceFocused();
            }
        }

        private bool IsGridFocused()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null
                && ReferenceEquals(navigator.Screen, this)
                && GridNodeId.Equals(navigator.FocusedKey);
        }

        // ---- the lines the menu gives nothing to key on ----

        private void AddLine(GraphBuilder builder, string key, Func<string> text, Tooltip tooltip)
        {
            GraphNodes.TextLine(builder, Marker(key), "pre-battle:" + key, text, tooltip);
        }
    }
}
