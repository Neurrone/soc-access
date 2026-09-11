using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// Drives the key graph from the player's keys and says what happened. The engine in
    /// <see cref="KeyGraph"/> only moves a cursor and reports the outcome; every spoken word of a
    /// graph screen originates here.
    ///
    /// The design rule that makes it predictable: <b>one place announces a focus change, whatever
    /// caused it</b>. <see cref="EnsureFocus"/> runs once a frame, compares where focus is against
    /// what was last spoken, and reads the difference. An arrow key, a screen choosing its landing
    /// spot, a rebuild that had to recover onto a surviving control - all of them arrive at the same
    /// comparison, so nothing is announced twice and nothing is silently skipped. Handlers that want
    /// to speak immediately (so a held arrow reads the item you land on rather than queueing behind
    /// the previous one) do so and then write the differ's memory themselves.
    ///
    /// The same comparison feeds the UI review buffer and the game's native tooltip window: whatever
    /// the focus readout says, the buffer is refilled with the long form of it, and the tooltip the
    /// focused node points at is drawn. Both only when something actually changed - the screen
    /// rebuilds every frame, and a review cursor that reset on every rebuild would be unusable.
    ///
    /// One <see cref="GraphState"/> is kept per live screen, so returning to a page returns to where
    /// you were on it. Imitated from Endless Space 2 Access's navigator, trimmed to what this game's
    /// ported screens need so far.
    /// </summary>
    public sealed partial class GraphNavigator
    {
        private readonly Dictionary<GraphScreen, GraphState> _states = new Dictionary<GraphScreen, GraphState>();

        private GraphScreen _screen;
        private GraphState _state;
        private KeyGraph _graph;

        // Whether the attached screen has had an Update of its own yet (HasTicked).
        private bool _ticked;

        // What the differ last read out, by identity and by node (the node carries the parent chain
        // the next readout is diffed against).
        private ControlId _lastSpokenKey;
        private GraphNode _lastSpokenNode;


        // A requested landing, applied on the next EnsureFocus - and kept across the frames a branch
        // takes to open where the control asked for is inside a collapsed one (see FocusRequest).
        private FocusRequest _pendingFocus;

        // The live-part watch: the focus it is baselined against, and the last resolved text of each
        // effective announcement part (index-parallel, with nulls where a part is not live).
        private ControlId _liveKey;
        private readonly List<string> _liveValues = new List<string>();

        // The node the player last activated, until the cursor moves: its availability flipping
        // afterwards is the consequence of the press and is not read by the watch.
        private ControlId _actedKey;

        // What the player is holding, if anything. Owned here because the carry keys are dispatched
        // here and because a carry is scoped to the page it started on, which is what this class
        // already tracks. One per navigator, so a hot reload takes it with the rest of the mod.
        private readonly CarryState _carry = new CarryState();

        // What the UI review buffer currently holds: the control it was filled from, the readout it was
        // filled at, and the lines themselves.
        private ControlId _bufferKey;
        private string _bufferReadout;
        private List<string> _bufferLines;

        // Which control the game is currently being made to look focused on (its tooltip drawn), and
        // the node whose blur hook undoes it. Kept by id: the graph is rebuilt every frame.
        private ControlId _visualKey;
        private GraphNode _visualNode;
        private object _visualAim;

        /// <summary>
        /// Install the engine's static injection points: the localized wording the announcer and the
        /// sheet compose with. Once per mod load; <see cref="ResetWiring"/> drops them on Stop, because
        /// they are delegates over this assembly.
        /// </summary>
        public static void InstallWiring()
        {
            GraphAnnouncer.PositionText = (index, count) => ModText.Get(ModStrings.Common.CountOf, index, count);
            GraphAnnouncer.ExpandedStateText = expanded =>
                ModText.Get(expanded ? ModStrings.UI.StatusExpanded : ModStrings.UI.StatusCollapsed);
            GraphSheet.BlankText = () => ModText.Get(ModStrings.UI.Blank);
            // "table", the word ES2 uses, not the widget tree's "grid": a sheet is a table of rows and
            // columns, and a grid is the map.
            GraphSheet.TableRoleText = () => ModText.Get(ModStrings.UI.RoleTable);
            GraphSheet.TextCellType = ControlTypes.Text;

            // How a USAGE HINT spells the gesture it names. The engine is game-free and cannot see
            // the action table, so the renderer is injected here and reads the LIVE bindings: a
            // re-bound gesture re-words every hint that names it, with nothing to keep in step.
            NodeHints.Chord = ChordNames.Of;

            // Whether a hint is SAID. One stable delegate for the whole mod load: the announcer's
            // per-node memo has the filter reference in its key, so re-assigning this when the
            // setting changes would throw away every memo instead of changing one answer.
            GraphAnnouncer.PartFilter = UsageHints.Speaks;

            // The carry's three gestures, named to the engine so its pick-up announcement and its
            // two derived hints spell whatever chords those actions are bound to now.
            CarryState.PickUpAction = AccessibilityActions.UiCarry.Key;
            CarryState.DropAction = AccessibilityActions.UiLeftClick.Key;
            CarryState.CancelAction = AccessibilityActions.UiBack.Key;
        }

        public static void ResetWiring()
        {
            GraphAnnouncer.Reset();
            GraphSheet.Reset();
            NodeHints.Reset();
            KeyGraph.Reset();
            CarrySounds.Reset();
        }

        public GraphNavigator()
        {
            _typeAhead.OnLand = LandOnSearchResult;
            _typeAhead.OnNoMatch = SayNoMatch;
            // The announcer derives the "draggable" and "drop target" words, and the buffer the two
            // drag hints, from THIS carry: one live drag per navigator, dropped again by
            // GraphAnnouncer.Reset in ResetWiring.
            GraphAnnouncer.Carry = _carry;
            // And the game's own drag noises, so the keyboard's carry sounds like the mouse's drag.
            // Which cargo has a sound at all is a screen's answer (ui/CarrySounds.cs), never this
            // file's: nothing is wired to a sound here.
            _carry.Started = CarrySounds.Started;
            _carry.Ended = CarrySounds.Ended;
        }

        /// <summary>What the player is carrying - see <see cref="CarryState"/>. Never null; an empty
        /// carry is the normal state.</summary>
        public CarryState Carry
        {
            get { return _carry; }
        }

        public GraphScreen Screen
        {
            get { return _screen; }
        }

        public GraphNode CurrentNode
        {
            get { return _graph == null ? null : _graph.CurrentNode; }
        }

        /// <summary>Where the cursor is, without needing the render it points into.</summary>
        public ControlId FocusedKey
        {
            get { return _state == null ? null : _state.CurKey; }
        }

        /// <summary>
        /// The index the focused node's structural key carries after <paramref name="prefix"/>, or -1
        /// when the cursor is anywhere else.
        ///
        /// A screen that acts on "the row the player is on" reads it off the key rather than off a
        /// remembered index, because the cursor moves for reasons the screen never hears about: a
        /// type-ahead landing, a rebuild that renumbered nothing, a jump from another feature. It is
        /// also the guard that keeps a graph dump or a search pass from choosing something nobody
        /// asked for (the tutorial pager turns the game's page from the row's label resolver, and only
        /// when that row is the focused one).
        ///
        /// The remainder is trimmed at the first <c>/</c>, so a cursor on something NESTED under the
        /// row - the row's own button, one of its entries - still answers the row's index.
        /// </summary>
        public int FocusedIndex(string prefix)
        {
            ControlId key = FocusedKey;
            string structural = key == null ? null : key.StructuralKey as string;
            if (structural == null || string.IsNullOrEmpty(prefix) || !structural.StartsWith(prefix, StringComparison.Ordinal))
            {
                return -1;
            }

            string rest = structural.Substring(prefix.Length);
            int slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                rest = rest.Substring(0, slash);
            }

            int index;
            return int.TryParse(rest, out index) ? index : -1;
        }

        /// <summary>The tooltip the focused node points at, or null - what the tooltip actions menu
        /// opens on, read the way the widget engine's <c>CurrentWidget.GetTooltip()</c> is.</summary>
        public Tooltip FocusedTooltip
        {
            get { return Aim(CurrentNode) as Tooltip; }
        }

        /// <summary>
        /// A render of the focused screen built purely to be READ - the dev server's accessible-tree
        /// dump. Exactly the build path navigation uses, so what the dump shows is what navigation
        /// sees; and nothing else, so reading the screen cannot change it: the cursor is untouched, no
        /// focus visual runs, and the render goes away with the caller.
        /// </summary>
        public GraphRender InspectRender()
        {
            return _screen == null ? null : BuildRender(_screen, _state);
        }

        /// <summary>The same for a screen nobody is on: built over a THROWAWAY state, so a screen the
        /// player has never opened can be read and neither its cursor nor the focused screen's is
        /// touched (<c>/gui/graph?screen=KEY</c>).</summary>
        public GraphRender InspectRender(GraphScreen screen)
        {
            if (screen == null)
            {
                return null;
            }

            return ReferenceEquals(screen, _screen)
                ? BuildRender(_screen, _state)
                : BuildRender(screen, new GraphState());
        }

        /// <summary>Point the navigator at a screen (null when none is focused). The screen's cursor
        /// is restored if it has one, and the differ starts fresh so the arrival reads in full.</summary>
        public void Attach(GraphScreen screen)
        {
            if (ReferenceEquals(screen, _screen))
            {
                return;
            }

            _screen = screen;
            _ticked = false;
            CarryFollowedThePage();
            ClearSearch();
            _lastSpokenKey = null;
            _lastSpokenNode = null;
            _actedKey = null;
            _liveKey = null;
            _liveValues.Clear();
            _bufferKey = null;
            _bufferReadout = null;
            _bufferLines = null;
            ClearVisual();

            if (screen == null)
            {
                _state = null;
                _graph = null;
                return;
            }

            if (!_states.TryGetValue(screen, out _state))
            {
                _state = new GraphState();
                _states.Add(screen, _state);
            }

            GraphScreen built = screen;
            GraphState state = _state;
            _graph = new KeyGraph(() => BuildRender(built, state), state);
        }

        /// <summary>Whether a cursor is being kept for this screen - what the manager's
        /// <c>KeepStateOnPop</c> decides, read back.</summary>
        public bool HasState(GraphScreen screen)
        {
            return screen != null && _states.ContainsKey(screen);
        }

        /// <summary>Forget a closed screen's cursor, so re-opening it starts at the top - and with it
        /// any landing that screen was still waiting to make.</summary>
        public void ScreenClosed(GraphScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            _states.Remove(screen);
            if (_pendingFocus != null && ReferenceEquals(_pendingFocus.Owner, screen))
            {
                _pendingFocus = null;
            }

            CarryFollowedThePage();

            if (ReferenceEquals(screen, _screen))
            {
                Attach(null);
            }
        }

        /// <summary>Give up the cursor entirely; the next EnsureFocus seats it again.</summary>
        public void Blur()
        {
            if (_state != null)
            {
                _state.CurKey = null;
            }

            ClearSearch();
            _lastSpokenKey = null;
            _lastSpokenNode = null;
            _actedKey = null;
            _liveKey = null;
            _liveValues.Clear();
            _bufferKey = null;
            _bufferReadout = null;
            _bufferLines = null;
            ClearVisual();
        }

        /// <summary>
        /// Ask for focus to land on a control (a screen choosing where to put the player). Applied on
        /// the next tick. The control does not have to be in the render: a landing aimed inside a
        /// COLLAPSED branch opens that branch on the way, one level per build (<see cref="FocusRequest"/>).
        /// </summary>
        public void FocusNode(ControlId id, bool announce = true)
        {
            _pendingFocus = id == null ? null : new FocusRequest(id, announce, _screen);
        }

        private FocusRequest OwnPendingFocus
        {
            get
            {
                return _pendingFocus != null && ReferenceEquals(_pendingFocus.Owner, _screen)
                    ? _pendingFocus
                    : null;
            }
        }

        /// <summary>
        /// Whether the focused graph screen takes this action - asked by the input router BEFORE the
        /// press, for the same reason every claim is: the game reads the same keys, and a key the mod
        /// does not claim is the game's. The navigation set is always ours on a graph screen; the
        /// value, drag and back keys only where the focused node or screen answers them; the letters
        /// only while type-ahead is armed.
        /// </summary>
        public bool Claims(string actionKey)
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            if (_typeAhead.IsActive && (actionKey == AccessibilityActions.UiBack.Key
                || actionKey == AccessibilityActions.UiClearSearch.Key))
            {
                return true;
            }

            // A mode of the screen that is driving takes its keys before the tree does
            // (<see cref="GraphScreen.ModeClaims"/>); the search above is innermost still.
            if (_screen.ModeClaims(actionKey))
            {
                return true;
            }

            switch (actionKey)
            {
                case "ui_up":
                case "ui_down":
                case "ui_left":
                case "ui_right":
                case "ui_next":
                case "ui_prev":
                case "ui_home":
                case "ui_end":
                case "ui_left_click":
                    return true;
                case "ui_region_prev":
                case "ui_region_next":
                    return InRegion();
                case "ui_coarse_increase":
                case "ui_coarse_decrease":
                    return HasAdjust();
                case "ui_right_click":
                    return HasContextual();
                case "ui_carry":
                    return CarryActions.Claims(FocusedVtable(), _carry);
                case "ui_back":
                    // Putting down what is being held is the back key's first meaning, whatever the
                    // screen would otherwise do with it.
                    return _carry.IsCarrying || _screen.ConsumesBack;
                default:
                    // ui_clear_search is claimed above, only while a search is live. Anything else is
                    // the screen's to take or to leave to the game.
                    return _screen.ClaimsAction(actionKey);
            }
        }

        /// <summary>Run an action by name. The input layer calls this; so does the dev server, which is
        /// how navigation is tested without a keyboard.</summary>
        public bool Dispatch(string actionKey)
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            if (actionKey == "ui_carry" && _typeAhead.HasBuffer)
            {
                // A space typed into a search is TEXT, and the search takes it in TypeAheadTick.
                // Claimed all the same, so the game does not also act on it.
                return true;
            }

            if (_typeAhead.IsActive && SearchAction(actionKey))
            {
                return true;
            }

            if (_screen.ModeClaims(actionKey))
            {
                return _screen.OnAction(actionKey);
            }

            switch (actionKey)
            {
                case "ui_up":
                    return Arrow(GraphDir.Up);
                case "ui_down":
                    return Arrow(GraphDir.Down);
                case "ui_left":
                    return Arrow(GraphDir.Left);
                case "ui_right":
                    return Arrow(GraphDir.Right);
                case "ui_next":
                    return Stop(1);
                case "ui_prev":
                    return Stop(-1);
                case "ui_home":
                    return JumpEdge(true);
                case "ui_end":
                    return JumpEdge(false);
                case "ui_region_prev":
                    return InRegion() && Region(-1);
                case "ui_region_next":
                    return InRegion() && Region(1);
                case "ui_coarse_increase":
                    return Adjust(1, true);
                case "ui_coarse_decrease":
                    return Adjust(-1, true);
                case "ui_left_click":
                    return Activate();
                case "ui_right_click":
                    return Contextual();
                case "ui_carry":
                    return CarryKey();
                case "ui_back":
                    // Putting down what is being held comes before anything the screen does with the
                    // key: the carry is the mode the player is in, and the screen underneath is not.
                    return CancelCarry() || _screen.Back();
                default:
                    // ui_clear_search only reaches here with no search live, which its claim
                    // never allows.
                    return _screen.OnAction(actionKey);
            }
        }

        /// <summary>The per-frame work for the focused graph screen: take what was typed, then seat,
        /// announce and watch the cursor.</summary>
        public void Update()
        {
            TypeAheadTick();
            EnsureFocus();
            _ticked = true;
        }

        /// <summary>Whether this screen has had a frame of its own: false from the moment it is
        /// attached until its first <see cref="Update"/> has run. The input layer asks before it lets
        /// a typed character reach the search, so the key that opened the page is not typed into it.</summary>
        public bool HasTicked(GraphScreen screen)
        {
            return _ticked && ReferenceEquals(screen, _screen);
        }

        /// <summary>
        /// Seat the cursor if it needs seating, announce it if it moved, and watch the focused
        /// control's live parts. The single announcement site.
        /// </summary>
        public void EnsureFocus()
        {
            if (_screen == null || _graph == null)
            {
                return;
            }

            FocusRequest pending = OwnPendingFocus;
            if (_state.CurKey == null && pending == null)
            {
                // No content yet - a window still animating in. Reconcile will seat the start node
                // as soon as there is something to seat it on.
                if (!_graph.Rerender())
                {
                    return;
                }

                object stop = _screen.InitialFocusStop;
                if (stop != null)
                {
                    GraphNode landing = KeyGraph.StopLanding(_graph.Current, _graph.State, stop);
                    if (landing != null)
                    {
                        _graph.Focus(landing.Id);
                    }
                }
            }
            else
            {
                if (!_graph.Rerender())
                {
                    return;
                }

                if (pending != null)
                {
                    FocusOutcome outcome = PendingOutcome(pending);
                    if (outcome == FocusOutcome.Land)
                    {
                        _graph.Focus(pending.Id);
                        if (!pending.Announce)
                        {
                            _lastSpokenKey = pending.Id;
                            _lastSpokenNode = _graph.CurrentNode;
                        }
                    }

                    if (outcome != FocusOutcome.Wait)
                    {
                        _pendingFocus = null;
                    }
                }
            }

            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return;
            }

            SyncVisual(node);

            // A landing of this screen's still in flight: a row the cursor is standing on that nobody
            // asked for is not where the player is going, and is not said.
            bool inFlight = OwnPendingFocus != null;
            bool moved = _lastSpokenKey == null || !_lastSpokenKey.Equals(node.Id);
            if (moved && !inFlight && RecoveredWhileLeaving())
            {
                // The control the player was on vanished because the page is being switched off (a
                // header the game hides as the menu closes) and the cursor fell onto whatever
                // survived. That is not somewhere the player went, so it is not said: the differ
                // adopts it silently and the screen that replaces this one speaks.
                _lastSpokenKey = node.Id;
                _lastSpokenNode = node;
                moved = false;
            }

            if (moved && !inFlight)
            {
                // Queued: an arrival follows the screen name rather than cutting it off.
                Say(GraphAnnouncer.Compose(_lastSpokenNode, node), false);
                _lastSpokenKey = node.Id;
                _lastSpokenNode = node;
                _actedKey = null;
            }

            FillBuffer(node);
            WatchLive(node);
        }

        // Whether the cursor stands on a node it was RECOVERED onto rather than moved to: the node
        // last spoken is gone from the render, and the screen says it cannot be worked right now. A
        // workable screen keeps announcing recoveries, because there a vanished control (a row the
        // game deleted) is real news.
        private bool RecoveredWhileLeaving()
        {
            return _lastSpokenKey != null
                && _graph.Current != null
                && _graph.Current.NodeAt(_lastSpokenKey) == null
                && !Workable();
        }

        private FocusOutcome PendingOutcome(FocusRequest pending)
        {
            if (_state.CurKey == null)
            {
                return _graph.Current.Nodes.ContainsKey(pending.Id) ? FocusOutcome.Land : FocusOutcome.Drop;
            }

            return pending.Step(_graph.Reach(pending.Id));
        }

        /// <summary>Give up an outstanding landing because the player has moved the cursor themselves.
        /// A request that survived would yank them off wherever they had got to.</summary>
        private void CancelPendingFocus()
        {
            if (OwnPendingFocus != null)
            {
                _pendingFocus = null;
            }
        }

        /// <summary>
        /// Refill the UI review buffer from the focused control - its name, the state words its
        /// readout would append, then its detail lines - and only when something actually changed.
        /// Sitting still on a control keeps the player's place in the buffer, and a control that
        /// changes under them refills with the truth. Mirrors the widget engine's fill.
        /// </summary>
        private void FillBuffer(GraphNode node)
        {
            ReviewBufferManager buffers = SocAccessMod.Instance == null ? null : SocAccessMod.Instance.ReviewBuffers;
            if (buffers == null)
            {
                return;
            }

            string readout = GraphAnnouncer.LeafText(node);
            if (_bufferKey != null && _bufferKey.Equals(node.Id) && string.Equals(_bufferReadout, readout))
            {
                return;
            }

            _bufferKey = node.Id;
            _bufferReadout = readout;
            List<string> lines = BufferLines(node);
            if (Same(_bufferLines, lines))
            {
                return;
            }

            _bufferLines = lines;
            buffers.ReplaceLines(ReviewBufferKind.Ui, lines);
            buffers.SetCurrentBuffer(ReviewBufferKind.Ui);
        }

        private static bool Same(List<string> left, List<string> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The lines a control fills the UI review buffer with - public because the dev
        /// server's graph dump shows what a control has to say without focusing it.</summary>
        public static List<string> BufferLines(GraphNode node)
        {
            return NodeBuffer.Lines(node);
        }

        private static GraphRender BuildRender(GraphScreen screen, GraphState state)
        {
            try
            {
                GraphBuilder builder = new GraphBuilder(state.Expanded);
                screen.Build(builder);
                return builder.Build();
            }
            catch (Exception e)
            {
                SocAccessMod.Instance?.LogWarning("GraphNavigator: " + screen.Key + ".Build threw: " + e);
                return null;
            }
        }

        // Left/right first offer to adjust a value, then to move along a wired edge, and only then
        // fall back on tree semantics - so a slider adjusts, a row steps sideways, and a group
        // expands, each without knowing about the others.
        private bool Arrow(GraphDir dir)
        {
            GraphNode focused = _graph.CurrentNode;
            if (focused == null)
            {
                return false;
            }

            bool horizontal = dir == GraphDir.Left || dir == GraphDir.Right;
            if (horizontal && Adjust(dir == GraphDir.Right ? 1 : -1, false))
            {
                return true;
            }

            MoveResult move = _graph.Move(dir);
            if (move.Moved)
            {
                AnnounceMove(move);
                return true;
            }

            if (horizontal)
            {
                KeyGraph.TreeResult tree = dir == GraphDir.Right ? _graph.TreeRight() : _graph.TreeLeft();
                switch (tree.Kind)
                {
                    case KeyGraph.TreeMove.Collapsed:
                        SpeakFocusedState();
                        return true;
                    case KeyGraph.TreeMove.EmptyGroup:
                        // The branch is OPEN and holds nothing. The cursor stays on the header, which
                        // is the one place left to press Left from to shut it again.
                        Say(ModText.Get(ModStrings.UI.NoDetails), true);
                        return true;
                    case KeyGraph.TreeMove.Descended:
                    case KeyGraph.TreeMove.Ascended:
                        AnnounceMove(tree.Move);
                        return true;
                    case KeyGraph.TreeMove.Followed:
                        // The leaf sent the cursor elsewhere itself; the landing announces once, by
                        // the pending-focus path.
                        return true;
                    case KeyGraph.TreeMove.Leaf:
                        return true;
                }
            }

            // Nothing that way. Inside a tree the key is still ours; on a plain list it is consumed
            // too - the claim already said so - and simply does nothing.
            return true;
        }

        /// <summary>Tab and Shift+Tab, which WRAP: a player who cannot see the panels has no way to
        /// know a page has run out of them. A page with exactly one stop consumes the key silently.</summary>
        private bool Stop(int step)
        {
            MoveResult move = _graph.MoveStop(step, true);
            if (move.Moved)
            {
                AnnounceMove(move);
            }

            return true;
        }

        private bool JumpEdge(bool first)
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            MoveResult move = KeyGraph.InTree(node)
                ? _graph.MoveToSiblingEdge(first)
                : _graph.MoveToEdge(EdgeDir(node, first));
            if (move.Moved)
            {
                AnnounceMove(move);
            }

            return true;
        }

        // Which way "the start" and "the end" lie: along whichever axis this stop's nodes are wired.
        private static GraphDir EdgeDir(GraphNode node, bool first)
        {
            bool vertical = Wired(node, GraphDir.Up) || Wired(node, GraphDir.Down);
            if (vertical)
            {
                return first ? GraphDir.Up : GraphDir.Down;
            }

            return first ? GraphDir.Left : GraphDir.Right;
        }

        private static bool Wired(GraphNode node, GraphDir dir)
        {
            Transition transition;
            return node.Transitions != null
                && node.Transitions.TryGetValue(dir, out transition)
                && transition != null;
        }

        private bool InRegion()
        {
            GraphNode node = _graph == null ? null : _graph.CurrentNode;
            return node != null && node.RegionKey != null;
        }

        private bool HasAdjust()
        {
            GraphNode node = _graph == null ? null : _graph.CurrentNode;
            return node != null && node.Vtable != null && node.Vtable.OnAdjust != null;
        }

        private bool HasContextual()
        {
            GraphNode node = _graph == null ? null : _graph.CurrentNode;
            return node != null && node.Vtable != null && node.Vtable.OnContextual != null;
        }

        private bool Region(int step)
        {
            MoveResult move = _graph.MoveRegion(step);
            if (move.Moved)
            {
                AnnounceMove(move);
            }

            return true;
        }

        // The left click. While something is being carried this is also the key that PUTS IT DOWN: on
        // a control that will take the cargo it drops there and nothing else happens, and on every
        // other control it is the plain click it always was, with the carry still live underneath.
        private bool Activate()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (_carry.IsCarrying)
            {
                if (!_graph.Rerender())
                {
                    return false;
                }

                node = _graph.CurrentNode;
                CarryOutcome drop = CarryActions.Activate(node == null ? null : node.Vtable, _carry);
                if (drop.Handled)
                {
                    Say(drop.Speech, true);
                    return true;
                }

                if (node == null)
                {
                    return false;
                }
            }

            if (node.Vtable.OnActivate != null)
            {
                _graph.Activate();
                SpeakStateAfterChange();
                // The acted node's AVAILABILITY flipping is the consequence the player caused (a
                // button that switched its page off, Statistics on the result page) and is not news;
                // its WORDS changing are (a dialogue line replaced in place by Enter), and stay the
                // live watch's to read.
                _actedKey = node.Id;
            }

            return true;
        }

        /// <summary>
        /// Pick something up, put it down, or swap what is being held - the whole decision is
        /// <see cref="CarryActions.Press"/>'s, so it can be read (and tested) in one place. False
        /// means the key was never ours here and the game should have it, which is the same answer
        /// <see cref="Claims"/> gave the router before the press.
        /// </summary>
        private bool CarryKey()
        {
            if (!CarryActions.Claims(FocusedVtable(), _carry))
            {
                // Not ours here, and answered off the standing render rather than by building one:
                // Space is pressed on screens that have nothing to do with carrying.
                return false;
            }

            if (!_graph.Rerender())
            {
                return false;
            }

            GraphNode node = _graph.CurrentNode;
            CarryOutcome outcome = CarryActions.Press(node == null ? null : node.Vtable, _carry, _screen);
            if (!outcome.Handled)
            {
                return false;
            }

            Say(outcome.Speech, true);
            return true;
        }

        // The back key while something is held: put it down, and go no further - the screen the
        // player was carrying across is not the thing they were trying to leave.
        private bool CancelCarry()
        {
            CarryOutcome outcome = CarryActions.Cancel(_carry);
            if (!outcome.Handled)
            {
                return false;
            }

            Say(outcome.Speech, true);
            return true;
        }

        /// <summary>
        /// The focused screen changed. A carry belongs to the PAGE it started on - that is where its
        /// drop targets are - and a screen opened OVER that page is still that page, so the question
        /// is whether the owner is anywhere in the screen stack rather than whether it is on top. A
        /// player can pick something up, open the drop list, a mod dialog or the split popup, and
        /// come back still holding it; walking off the page drops it, silently.
        /// </summary>
        private void CarryFollowedThePage()
        {
            if (!_carry.IsCarrying)
            {
                return;
            }

            _carry.ScreenChanged(OwnerIsOnTheScreenStack());
        }

        private bool OwnerIsOnTheScreenStack()
        {
            ScreenManager manager = SocAccessMod.Instance == null ? null : SocAccessMod.Instance.ScreenManager;
            Screen owner = _carry.Owner as Screen;
            // A child screen opened over the owner is still the owner's page, and so is the owner
            // itself when a child is what the player is on: the question is whether the page is
            // anywhere in the stack, counting the chains hanging off it.
            return manager != null && owner != null && manager.IsOnStack(owner);
        }

        // The focused control's vtable off the STANDING render - what the claim questions are
        // answered from, since they are asked several times a frame and must not build anything.
        private NodeVtable FocusedVtable()
        {
            GraphNode node = _graph == null ? null : _graph.CurrentNode;
            return node == null ? null : node.Vtable;
        }

        // The command the game puts on a right click here. Claimed only where the control has one, so
        // the key stays the game's everywhere else.
        private bool Contextual()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (node.Vtable.OnContextual != null)
            {
                _graph.Contextual();
                SpeakStateAfterChange();
            }

            return true;
        }

        // The one adjust path, fine or coarse. A control with no value to adjust does not answer for
        // either, so the coarse keys fall through and the arrows go back to being navigation.
        private bool Adjust(int sign, bool large)
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null || node.Vtable.OnAdjust == null)
            {
                return false;
            }

            _graph.TryAdjust(sign, large);
            SpeakStateAfterChange();
            return true;
        }

        // The synchronous half of state feedback: an action the player just took reports its result
        // at once, interrupting. A control that answers with nothing - one that refused the action -
        // is left alone entirely, live watch included.
        private void SpeakStateAfterChange()
        {
            // The action may have pushed another screen and detached this navigator (Quit opens
            // its popup synchronously): then there is nothing left to report on.
            GraphNode node = _graph == null ? null : _graph.CurrentNode;
            Func<string> state = node == null ? null : node.Vtable.StateText;
            if (state == null)
            {
                return;
            }

            string text = state();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Say(text, true);
            _liveKey = null;
        }

        private void SpeakFocusedState()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return;
            }

            Say(GraphAnnouncer.LeafText(node), true);
            CancelPendingFocus();
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            _liveKey = null;
        }

        private void AnnounceMove(MoveResult result)
        {
            GraphNode node = result.To;
            if (node == null)
            {
                return;
            }

            Say(GraphAnnouncer.Compose(result.From, node, result.TransitionLabel), true);
            CancelPendingFocus();
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
        }

        /// <summary>
        /// Watches the focused control's live parts and speaks the ones that change - a button that
        /// becomes unavailable, a value the game flips on its own. Nothing is spoken on the frame the
        /// baseline is taken: the focus readout has just said all of it. Nor while the screen says it
        /// cannot be worked (<see cref="GraphScreen.IsWorkable"/>): a page being switched off turns
        /// every control unavailable at once, and that is a fact about the page. The baseline is still
        /// taken, so nothing is announced late once the page comes back.
        /// </summary>
        private void WatchLive(GraphNode node)
        {
            List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
            if (parts.Count == 0)
            {
                return;
            }

            // A drawn control whose game object has gone inactive has left with its page (a result
            // page hiding itself as Statistics opens): its parts flipping to unavailable is the page
            // leaving, not the control changing, so that is muted the same way.
            bool mute = !Workable() || !StillDrawn(node);
            bool sameNode = _liveKey != null && _liveKey.Equals(node.Id);
            bool baseline = !sameNode || _liveValues.Count != parts.Count;
            if (baseline)
            {
                // The same node with a different NUMBER of parts is that node changed under the
                // cursor (a dialogue's next line with more or fewer paragraphs): the whole readout is
                // what changed, and it is said before the new baseline is taken.
                if (sameNode && !mute && HasLivePart(parts))
                {
                    Say(GraphAnnouncer.LeafText(node), false);
                }

                _liveKey = node.Id;
                _liveValues.Clear();
            }

            for (int i = 0; i < parts.Count; i++)
            {
                NodeAnnouncement part = parts[i];
                if (part == null || !part.Live)
                {
                    if (baseline)
                    {
                        _liveValues.Add(null);
                    }

                    continue;
                }

                string text = null;
                try
                {
                    if (part.Text != null)
                    {
                        text = part.Text();
                    }
                }
                catch (Exception)
                {
                }

                if (baseline)
                {
                    _liveValues.Add(text);
                    continue;
                }

                if (!string.Equals(_liveValues[i], text))
                {
                    _liveValues[i] = text;
                    bool acted = _actedKey != null && _actedKey.Equals(node.Id)
                        && part.Kind == AnnouncementKinds.Enabled;
                    if (!mute && !acted)
                    {
                        Say(text, false);
                    }
                }
            }
        }

        private static bool HasLivePart(List<NodeAnnouncement> parts)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null && parts[i].Live)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a drawn node's subject is still active in the hierarchy; true for a node
        /// drawn by nothing the engine can ask.</summary>
        private static bool StillDrawn(GraphNode node)
        {
            DrawnNode drawn = node == null ? null : node.Declared as DrawnNode;
            UnityEngine.Component component = drawn == null ? null : drawn.DrawnBy as UnityEngine.Component;
            try
            {
                return component == null || component.gameObject.activeInHierarchy;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private bool Workable()
        {
            try
            {
                return _screen == null || _screen.IsWorkable;
            }
            catch (Exception e)
            {
                SocAccessMod.Instance?.LogWarning("GraphNavigator: IsWorkable threw: " + e);
                return true;
            }
        }

        /// <summary>
        /// Make the game look the way it would with the pointer resting on the focused control - its
        /// native tooltip drawn - so someone watching the screen can follow where the keyboard is.
        /// Alongside the announcement, on the same comparison: whatever moved focus, the game's
        /// appearance follows it exactly once, and again when what the control points at changes
        /// under a standing cursor.
        /// </summary>
        private void SyncVisual(GraphNode node)
        {
            object aim = Aim(node);
            if (_visualKey != null && _visualKey.Equals(node.Id) && SameAim(aim, _visualAim))
            {
                return;
            }

            ClearVisual();
            _visualKey = node.Id;
            _visualNode = node;
            _visualAim = aim;
            Tooltip tooltip = aim as Tooltip;
            NativeTooltipUtility.ShowVisualTooltip(tooltip == null ? null : tooltip.VisualMetadata);
            if (_screen != null)
            {
                try
                {
                    _screen.OnFocusVisual(node);
                }
                catch (Exception e)
                {
                    SocAccessMod.Instance?.LogWarning("GraphNavigator: a screen's OnFocusVisual threw: " + e);
                }
            }

            Safe(node.Vtable.OnFocusVisual, "OnFocusVisual");
        }

        /// <summary>Leave the game looking as though nothing were focused - focus has gone somewhere
        /// this navigator does not describe, or the mod is going away.</summary>
        public void ClearVisual()
        {
            if (_visualNode != null)
            {
                Safe(_visualNode.Vtable.OnBlurVisual, "OnBlurVisual");
                NativeTooltipUtility.HideTooltip();
            }

            _visualKey = null;
            _visualNode = null;
            _visualAim = null;
        }

        // Whether two aims point the game at the same thing. A screen builds a fresh Tooltip object on
        // every rebuild, so two aims are the same when what they DRAW is the same: the component the
        // native tooltip is read from, at the same anchor, or the same map tooltip at the same point.
        // Compared by reference alone, every frame looked like a new aim and the visual was torn down
        // and re-drawn while the cursor stood still.
        private static bool SameAim(object a, object b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            Tooltip x = a as Tooltip;
            Tooltip y = b as Tooltip;
            if (x == null || y == null)
            {
                return false;
            }

            VisualTooltipMetadata m = x.VisualMetadata;
            VisualTooltipMetadata n = y.VisualMetadata;
            if (m == null || n == null)
            {
                return m == null && n == null;
            }

            return ReferenceEquals(m.Component, n.Component)
                && ReferenceEquals(m.Anchor, n.Anchor)
                && ReferenceEquals(m.MapTooltipable, n.MapTooltipable)
                && m.IsMapTooltip == n.IsMapTooltip
                && m.ScreenPoint == n.ScreenPoint;
        }

        private static object Aim(GraphNode node)
        {
            try
            {
                Func<object> points = node == null || node.Vtable == null ? null : node.Vtable.PointsAt;
                return points == null ? null : points();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Safe(Action action, string what)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception e)
            {
                SocAccessMod.Instance?.LogWarning("GraphNavigator: " + what + " threw: " + e);
            }
        }

        private static void Say(string text, bool interrupt)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            SpeechPipeline.Output(new SpeechRequest(text, interrupt));
        }
    }
}
