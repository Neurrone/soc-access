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
            GraphSheet.TableRoleText = () => ModText.Get(ModStrings.UI.RoleGrid);
            GraphSheet.TextCellType = ControlTypes.Text;
        }

        public static void ResetWiring()
        {
            GraphAnnouncer.Reset();
            GraphSheet.Reset();
            NodeHints.Reset();
            KeyGraph.Reset();
        }

        public GraphNavigator()
        {
            _typeAhead.OnLand = LandOnSearchResult;
            _typeAhead.OnNoMatch = SayNoMatch;
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

        /// <summary>The render the cursor is standing in - the last one built; null before the first
        /// build.</summary>
        public GraphRender Render
        {
            get { return _graph == null ? null : _graph.Current; }
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

        /// <summary>Point the navigator at a screen (null when none is focused). The screen's cursor
        /// is restored if it has one, and the differ starts fresh so the arrival reads in full.</summary>
        public void Attach(GraphScreen screen)
        {
            if (ReferenceEquals(screen, _screen))
            {
                return;
            }

            _screen = screen;
            ClearSearch();
            _lastSpokenKey = null;
            _lastSpokenNode = null;
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
                case "ui_activate":
                    return true;
                case "ui_region_prev":
                case "ui_region_next":
                    return InRegion();
                case "ui_coarse_increase":
                case "ui_coarse_decrease":
                    return HasAdjust();
                case "ui_right_click":
                    return HasContextual();
                case "ui_back":
                    return _screen.ConsumesBack;
                default:
                    // ui_clear_search is claimed above, only while a search is live.
                    return false;
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

            if (_typeAhead.IsActive && SearchAction(actionKey))
            {
                return true;
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
                case "ui_activate":
                    return Activate();
                case "ui_right_click":
                    return Contextual();
                case "ui_back":
                    return _screen.Back();
                default:
                    // ui_clear_search only reaches here with no search live, which its claim
                    // never allows.
                    return false;
            }
        }

        /// <summary>The per-frame work for the focused graph screen: take what was typed, then seat,
        /// announce and watch the cursor.</summary>
        public void Update()
        {
            TypeAheadTick();
            EnsureFocus();
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
            if ((_lastSpokenKey == null || !_lastSpokenKey.Equals(node.Id)) && !inFlight)
            {
                // Queued: an arrival follows the screen name rather than cutting it off.
                Say(GraphAnnouncer.Compose(_lastSpokenNode, node), false);
                _lastSpokenKey = node.Id;
                _lastSpokenNode = node;
            }

            FillBuffer(node);
            WatchLive(node);
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

        private bool Activate()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (node.Vtable.OnActivate != null)
            {
                _graph.Activate();
                SpeakStateAfterChange();
            }

            return true;
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

            bool mute = !Workable();
            bool baseline = _liveKey == null || !_liveKey.Equals(node.Id) || _liveValues.Count != parts.Count;
            if (baseline)
            {
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
                    if (!mute)
                    {
                        Say(text, false);
                    }
                }
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
            if (_visualKey != null && _visualKey.Equals(node.Id) && ReferenceEquals(aim, _visualAim))
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
