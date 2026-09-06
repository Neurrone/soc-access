using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>The outcome of a navigation operation, for the caller (the engine-side navigator) to
    /// announce. The core never speaks — it returns what happened.</summary>
    public struct MoveResult
    {
        public bool Moved;              // focus actually changed nodes
        public GraphNode From;          // node before the operation (null on first landing)
        public GraphNode To;            // node after (== From when at an edge; null when the graph is empty)
        public string TransitionLabel;  // the crossed edge's spoken line, when it had one
    }

    /// <summary>
    /// The navigation engine: a directed graph of controls rebuilt from a render callback on each
    /// operation, with focus persisting in an external <see cref="GraphState"/>. Ported from Tanglebeep
    /// (with permission), itself from Factorio Access's key-graph.lua. Two invariants carry over:
    ///
    /// <para><b>Down-right total order</b> (<see cref="ComputeOrder"/>): from the start node, go right
    /// until stuck, queueing each down — visits a planar UI in reading order. Nodes down-right can't reach
    /// (later Tab-stops) are appended in declaration order, keeping the order total.</para>
    ///
    /// <para><b>Focus recovery on rebuild</b> (<see cref="Reconcile"/>): if the focused control vanished,
    /// land on the nearest survivor rather than jumping to the start — following the backing object that
    /// moved (tier 1) or the logical control whose backing object was rebuilt (tier 2) first.</para>
    ///
    /// <para>The same walk repairs a stop's REMEMBERED position
    /// (<see cref="RepairStopMemory"/>), which no cursor move ever passes through: a stop's memory is
    /// repaired on the rebuild its control DIES — to the nearest earlier survivor in the same stop — so
    /// Tab back into the stop lands beside where the player was, rather than at the top of it. It has to
    /// happen on that rebuild, because the previous traversal order is what knows the dead control's
    /// neighborhood and the next rebuild will have forgotten it. A stop that is wholly absent from this
    /// render (a hidden panel, a modal up) is left alone: it may come back with the very keys it
    /// remembers.</para>
    ///
    /// Extensions over the original: Tab-stop cycling and region jumps as operations over node metadata
    /// (with per-stop remembered positions), and per-node secondary/tooltip/adjust behaviors.
    ///
    /// Every operation re-renders first (immediate mode): the graph is a projection of live game state, so
    /// there is no retained tree to invalidate and no staleness to reason about.
    /// </summary>
    public sealed class KeyGraph
    {
        private readonly Func<GraphRender> _renderCallback;
        private readonly GraphState _state;
        private GraphRender _current;

        public KeyGraph(Func<GraphRender> renderCallback, GraphState state)
        {
            _renderCallback = renderCallback;
            _state = state;
        }

        public GraphState State
        {
            get { return _state; }
        }

        /// <summary>The most recently built render, or null if not yet rendered / empty.</summary>
        public GraphRender Current
        {
            get { return _current; }
        }

        /// <summary>The focused node in the current render, or null.</summary>
        public GraphNode CurrentNode
        {
            get { return _current == null ? null : _current.NodeAt(_state.CurKey); }
        }

        /// <summary>Rebuild the render and reconcile focus into it. False when the callback produced
        /// nothing (the caller should treat the graph as closed/empty).</summary>
        public bool Rerender()
        {
            _current = _renderCallback();
            if (_current == null || _current.Nodes.Count == 0)
            {
                _current = null;
                return false;
            }
            Reconcile(_current, _state);
            return true;
        }

        /// <summary>
        /// Move focus from the cached <see cref="GraphState.CurKey"/> to a valid control in
        /// <paramref name="render"/>, then recompute the traversal order.
        /// </summary>
        public static void Reconcile(GraphRender render, GraphState state)
        {
            // Honor a pending suggested move first, if its target still exists (consumed either way).
            if (state.NextSuggestedMove != null)
            {
                if (render.Nodes.ContainsKey(state.NextSuggestedMove))
                    state.CurKey = render.Nodes[state.NextSuggestedMove].Id;
                state.NextSuggestedMove = null;
            }

            ControlId old = state.CurKey;
            ControlId resolved = null;

            if (old != null)
            {
                // Tier 1: the same backing object, even if its structural key changed (it moved).
                if (old.Subject != null)
                {
                    foreach (KeyValuePair<ControlId, GraphNode> kv in render.Nodes)
                        if (kv.Value.Id.SubjectMatches(old.Subject)) { resolved = kv.Value.Id; break; }
                }

                // Tier 2: the same structural key, even if the backing object was rebuilt.
                if (resolved == null)
                {
                    GraphNode structural;
                    if (render.Nodes.TryGetValue(old, out structural)) resolved = structural.Id;
                }

                // Tier 3: the row that CONTAINED it, when this build has stopped showing whole families
                // of rows rather than losing one (<see cref="GraphBuilder.SeatOnContainer"/>) — the
                // same thing the player was reading, read less closely.
                if (resolved == null && render.SeatOnContainer)
                {
                    GraphNode container = DeepestDeclaredAncestor(render, old);
                    if (container != null && container.Focusable) resolved = container.Id;
                }

                // Fallback: nearest survivor walking the previous order backward.
                if (resolved == null)
                {
                    GraphNode survivor = SurvivorBefore(render, state.KeyOrder, old, null);
                    if (survivor != null) resolved = survivor.Id;
                }
            }

            // Nothing matched (or first render): the start node — but when the start is itself ONE OF A
            // SET OF ALTERNATIVES (it declares a selected-kind part: a tab, a radio, a row of a list),
            // prefer whichever of them is in force, so focus lands on the checked tab rather than the
            // top of a long list. A start that declares no such part is not one of a set — it is a
            // block of text a popup wants read first, with alternatives merely sharing its stop (the
            // dots marking which page a tutorial is on) — and the screen's own choice stands.
            if (resolved == null)
            {
                GraphNode startNode = render.Nodes.ContainsKey(render.StartKey) ? render.Nodes[render.StartKey] : null;
                GraphNode sel = startNode != null && Selectable(startNode)
                    ? SelectedNodeInStop(render, startNode.StopKey)
                    : null;
                if (sel != null) resolved = sel.Id;
                else if (startNode != null) resolved = startNode.Id;
                else resolved = render.StartKey;
            }

            state.CurKey = resolved;
            RememberStop(render, state, resolved);
            RepairStopMemory(render, state);
            state.KeyOrder = ComputeOrder(render);
        }

        /// <summary>
        /// The down-right total order: go right until stuck (recording each node), queue every down for a
        /// later pass, repeat — then append any node the walk never reached (e.g. later Tab-stops, which
        /// have no cross-stop edges) in declaration order, so the order is total.
        /// </summary>
        public static List<ControlId> ComputeOrder(GraphRender render)
        {
            List<ControlId> order = new List<ControlId>();
            HashSet<ControlId> seen = new HashSet<ControlId>();
            List<ControlId> downFringe = new List<ControlId> { render.StartKey };

            int i = 0;
            while (i < downFringe.Count)
            {
                ControlId k = downFringe[i];
                while (!seen.Contains(k))
                {
                    seen.Add(k);
                    order.Add(k);

                    GraphNode n;
                    if (!render.Nodes.TryGetValue(k, out n)) break;

                    Transition d, t;
                    if (n.Transitions.TryGetValue(GraphDir.Down, out d) && d != null)
                        downFringe.Add(d.Destination);
                    if (!n.Transitions.TryGetValue(GraphDir.Right, out t) || t == null) break;
                    k = t.Destination;
                }
                i++;
            }

            foreach (GraphNode node in render.Order)
                if (seen.Add(node.Id)) order.Add(node.Id);

            return order;
        }

        private static int IndexOf(List<ControlId> order, ControlId key)
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i].Equals(key)) return i;
            return -1;
        }

        /// <summary>
        /// Move every stop memory whose control has DIED to the nearest earlier survivor of the same
        /// stop, so that Tab back into the stop lands beside where the player was rather than at the top.
        ///
        /// This runs on every reconcile because it can only work on the ONE rebuild that the death
        /// happens on: <see cref="GraphState.KeyOrder"/> is still the order from BEFORE it, the only
        /// record of what stood next to the dead control, and the last line of Reconcile is about to
        /// replace it. Nothing else notices these deaths — a stop the player is not standing in has no
        /// cursor to reconcile, which is exactly the case that stranded them (a fleet disbanded from its
        /// panel: the map's memory kept naming the dead fleet, and coming back landed on the first node
        /// of the tree).
        ///
        /// A memory is left ALONE where the walk finds no same-stop survivor: the whole stop being
        /// absent means a panel is hidden or a modal is up, and it may return with the very keys the
        /// memory names. Same where the previous order never listed the dead key (a first render, a
        /// control that came and went between rebuilds) — there is no neighborhood to fall back into,
        /// and <see cref="StopLanding(object)"/>'s selected/declared/first chain covers it.
        /// </summary>
        private static void RepairStopMemory(GraphRender render, GraphState state)
        {
            List<object> stops = null;
            List<ControlId> landings = null;
            foreach (KeyValuePair<object, ControlId> memory in state.StopMemory)
            {
                GraphNode remembered = render.NodeAt(memory.Value);
                if (remembered != null && Equals(remembered.StopKey, memory.Key)) continue;

                // The same container rule the focused cursor gets: a stop whose rows went away by the
                // family has to be returned to on the thing that held them, or Tab back into it lands
                // wherever the reading order happens to have left a survivor.
                GraphNode survivor = null;
                if (render.SeatOnContainer)
                {
                    GraphNode container = DeepestDeclaredAncestor(render, memory.Value);
                    if (container != null && container.Focusable && Equals(container.StopKey, memory.Key))
                        survivor = container;
                }

                if (survivor == null)
                    survivor = SurvivorBefore(render, state.KeyOrder, memory.Value, memory.Key);
                if (survivor == null) continue;

                if (stops == null) { stops = new List<object>(); landings = new List<ControlId>(); }
                stops.Add(memory.Key);
                landings.Add(survivor.Id);
            }

            // Collected first: the dictionary cannot be written while it is being walked.
            if (stops == null) return;
            for (int i = 0; i < stops.Count; i++) state.StopMemory[stops[i]] = landings[i];
        }

        /// <summary>The nearest survivor at or before <paramref name="dead"/> in a previous traversal
        /// order — the one recovery mechanism, serving both the focused cursor and a stop's memory.
        /// Null where the order is unknown, no longer lists the dead key, or holds no survivor that
        /// satisfies <paramref name="stopKey"/> (null = any stop).</summary>
        private static GraphNode SurvivorBefore(GraphRender render, List<ControlId> order, ControlId dead, object stopKey)
        {
            if (order == null) return null;
            for (int i = IndexOf(order, dead); i >= 0; i--)
            {
                GraphNode survivor;
                if (render.Nodes.TryGetValue(order[i], out survivor)
                    && (stopKey == null || Equals(survivor.StopKey, stopKey)))
                    return survivor;
            }
            return null;
        }

        private static void RememberStop(GraphRender render, GraphState state, ControlId key)
        {
            GraphNode node = render.NodeAt(key);
            if (node != null && node.StopKey != null) state.StopMemory[node.StopKey] = key;
        }

        private void SetCurrent(GraphNode node)
        {
            _state.CurKey = node.Id;
            if (node.StopKey != null) _state.StopMemory[node.StopKey] = node.Id;
        }

        // ---- navigation operations ----

        /// <summary>One step in <paramref name="dir"/>. Not moved (at an edge / empty) → To == From.</summary>
        public MoveResult Move(GraphDir dir)
        {
            MoveResult result = default(MoveResult);
            if (!Rerender()) return result;

            GraphNode node = CurrentNode;
            result.From = node;
            result.To = node;
            if (node == null) return result;

            Transition t;
            node.Transitions.TryGetValue(dir, out t);
            GraphNode dest = t != null ? _current.NodeAt(t.Destination) : null;
            if (dest == null || dest == node) return result;

            SetCurrent(dest);
            result.To = dest;
            result.Moved = true;
            result.TransitionLabel = t.Label;
            return result;
        }

        /// <summary>As far as possible in <paramref name="dir"/> (Home/End within a row or column).</summary>
        public MoveResult MoveToEdge(GraphDir dir)
        {
            MoveResult result = default(MoveResult);
            if (!Rerender()) return result;

            GraphNode node = CurrentNode;
            result.From = node;
            result.To = node;
            if (node == null) return result;

            GraphNode cur = node;
            while (true)
            {
                Transition t;
                if (!cur.Transitions.TryGetValue(dir, out t) || t == null) break;
                GraphNode next = _current.NodeAt(t.Destination);
                if (next == null || next == cur) break;
                cur = next;
            }

            if (cur != node)
            {
                SetCurrent(cur);
                result.To = cur;
                result.Moved = true;
            }
            return result;
        }

        /// <summary>Cycle to the next/previous Tab-stop (declaration order), landing on the stop's
        /// remembered position (else its first node). <paramref name="wrap"/> continues past the ends;
        /// without it, at the last/first stop the result is not-moved (the caller may blur instead).</summary>
        public MoveResult MoveStop(int dir, bool wrap)
        {
            MoveResult result = default(MoveResult);
            if (!Rerender()) return result;

            GraphNode node = CurrentNode;
            result.From = node;
            result.To = node;
            if (node == null) return result;

            List<object> stops = StopOrder();
            if (stops.Count <= 1) return result;

            int idx = stops.IndexOf(node.StopKey);
            if (idx < 0) return result;
            int ni = idx + dir;
            if (wrap) ni = ((ni % stops.Count) + stops.Count) % stops.Count;
            if (ni < 0 || ni >= stops.Count || ni == idx) return result;

            GraphNode dest = StopLanding(stops[ni]);
            if (dest == null) return result;

            SetCurrent(dest);
            result.To = dest;
            result.Moved = true;
            return result;
        }

        /// <summary>Jump to the next/previous region within the current stop (declaration order), landing
        /// on the region's first node.</summary>
        public MoveResult MoveRegion(int dir)
        {
            MoveResult result = default(MoveResult);
            if (!Rerender()) return result;

            GraphNode node = CurrentNode;
            result.From = node;
            result.To = node;
            if (node == null || node.RegionKey == null) return result;

            List<object> regions = new List<object>();
            foreach (GraphNode n in _current.Order)
                if (Equals(n.StopKey, node.StopKey) && n.RegionKey != null && !regions.Contains(n.RegionKey))
                    regions.Add(n.RegionKey);

            int idx = regions.IndexOf(node.RegionKey);
            int ni = idx + dir;
            if (idx < 0 || ni < 0 || ni >= regions.Count) return result;

            foreach (GraphNode n in _current.Order)
                if (Equals(n.StopKey, node.StopKey) && Equals(n.RegionKey, regions[ni]))
                {
                    SetCurrent(n);
                    result.To = n;
                    result.Moved = true;
                    return result;
                }
            return result;
        }

        /// <summary>Move focus to a specific control (a node just revealed, a screen's chosen landing).
        /// False when it isn't in the render.</summary>
        public bool Focus(ControlId id)
        {
            if (id == null || !Rerender()) return false;
            GraphNode node = _current.NodeAt(id);
            if (node == null) return false;
            SetCurrent(node);
            return true;
        }

        /// <summary>Tier-1 focus sync from the game: if a node's backing object is
        /// <paramref name="reference"/>, move focus there. True if focus changed nodes.</summary>
        public bool FocusByReference(object reference)
        {
            if (reference == null || _current == null) return false;
            foreach (KeyValuePair<ControlId, GraphNode> kv in _current.Nodes)
                if (kv.Value.Id.SubjectMatches(reference))
                {
                    bool changed = _state.CurKey == null || !_state.CurKey.Equals(kv.Value.Id);
                    SetCurrent(kv.Value);
                    return changed;
                }
            return false;
        }

        private List<object> StopOrder()
        {
            List<object> stops = new List<object>();
            foreach (GraphNode n in _current.Order)
                if (n.StopKey != null && !stops.Contains(n.StopKey)) stops.Add(n.StopKey);
            return stops;
        }

        /// <summary>Where focus lands when entering a stop with no active cursor: the remembered
        /// position, else the SELECTED member (a radio/tab/list item currently checked — a boon on long
        /// lists), else the stop's first node.</summary>
        public GraphNode StopLanding(object stopKey)
        {
            return StopLanding(_current, _state, stopKey);
        }

        public static GraphNode StopLanding(GraphRender render, GraphState state, object stopKey)
        {
            ControlId remembered;
            if (state.StopMemory.TryGetValue(stopKey, out remembered))
            {
                GraphNode node = render.NodeAt(remembered);
                if (node != null && Equals(node.StopKey, stopKey)) return node;
            }
            GraphNode selected = SelectedNodeInStop(render, stopKey);
            if (selected != null) return selected;
            GraphNode declared = DeclaredLanding(render, stopKey);
            if (declared != null) return declared;
            foreach (GraphNode n in render.Order)
                if (Equals(n.StopKey, stopKey)) return n;
            return null;
        }

        /// <summary>Whether this render declares <paramref name="stopKey"/> at all — the availability
        /// question a jump-to-stop key asks, and exactly the question
        /// <see cref="StopLanding(GraphRender,GraphState,object)"/> answers with null (every branch of it
        /// returns a node whose StopKey is this one, so "a landing exists" and "the stop is declared" are
        /// one fact). It is asked separately because the CLAIM half runs inside the game's own key scans
        /// several times a frame: this stops at the first node of the stop, where the landing walk asks
        /// every node in it what it is announcing.</summary>
        public static bool DeclaresStop(GraphRender render, object stopKey)
        {
            if (render == null || stopKey == null) return false;
            foreach (GraphNode n in render.Order)
                if (Equals(n.StopKey, stopKey)) return true;
            return false;
        }

        /// <summary>Where the stop said Tab should land (<see cref="GraphBuilder.LandStopOn"/>), or null.
        /// </summary>
        private static GraphNode DeclaredLanding(GraphRender render, object stopKey)
        {
            ControlId id;
            if (stopKey == null || !render.StopLandings.TryGetValue(stopKey, out id)) return null;
            GraphNode node = render.NodeAt(id);
            return node != null && Equals(node.StopKey, stopKey) ? node : null;
        }

        /// <summary>Whether a node is one of a set of alternatives - it declares a selected-kind part,
        /// whether or not it is the one currently in force.</summary>
        private static bool Selectable(GraphNode node)
        {
            IList<NodeAnnouncement> anns = node.Vtable != null ? node.Vtable.Announcements : null;
            if (anns == null) return false;
            foreach (NodeAnnouncement a in anns)
                if (a != null && a.Kind == AnnouncementKinds.Selected) return true;
            return false;
        }

        /// <summary>The first node in a stop that reads as SELECTED — carries a non-empty selected-kind
        /// announcement part (list selection / choice option / tab / radio all declare one), or null.
        /// The search starts at the stop's declared landing where it has one, so a table's sort headings
        /// — where the SORTED column reads "selected" — are not mistaken for the chosen row.</summary>
        public static GraphNode SelectedNodeInStop(GraphRender render, object stopKey)
        {
            GraphNode from = DeclaredLanding(render, stopKey);
            bool reached = from == null;
            foreach (GraphNode n in render.Order)
            {
                if (!Equals(n.StopKey, stopKey)) continue;
                if (!reached)
                {
                    if (!ReferenceEquals(n, from)) continue;
                    reached = true;
                }
                IList<NodeAnnouncement> anns = n.Vtable != null ? n.Vtable.Announcements : null;
                if (anns == null) continue;
                foreach (NodeAnnouncement a in anns)
                    if (a != null && a.Kind == AnnouncementKinds.Selected)
                    {
                        string t = null;
                        try { if (a.Text != null) t = a.Text(); } catch { }
                        if (!string.IsNullOrEmpty(t)) return n;
                    }
            }
            return null;
        }

        // ---- tree operations (Right/Left semantics for expandable groups) ----

        /// <summary>What a tree side-step did (the caller composes the speech).</summary>
        public enum TreeMove
        {
            None,       // not applicable here (not in a tree / nothing to do) — caller decides consume/bubble
            Collapsed,  // the focused group collapsed (focus unchanged; speak its new state). There is
                        // no Expanded to match it: opening a group moves into it in the same press, so
                        // the answer is Descended and the state word is never spoken on the way in.
            EmptyGroup, // expanding found no children — the group is left OPEN (speak "no details")
            Descended,  // moved to the group's first child, opening it first where it was shut
                        // (announce as a move)
            Ascended,   // moved to the nearest focusable ancestor, shutting it behind us where it was an
                        // open group (announce as a move)
            Leaf,       // Right on a non-group inside a tree — consumed, nothing to descend into
            Followed,   // the leaf named a place elsewhere in the graph and sent the cursor there
                        // (NodeVtable.OnFollow) — consumed SILENTLY: the landing speaks for itself
        }

        public struct TreeResult
        {
            public TreeMove Kind;
            public MoveResult Move; // valid for Descended/Ascended

            /// <summary>The group this press OPENED, where it opened one - so a caller can come back to
            /// it (<see cref="TreeDescend"/>) once the page it acted on has settled. Null on a descend
            /// into a branch that was already open, and on every other answer.</summary>
            public GraphNode Opened;
        }

        /// <summary>Is this node part of an expandable structure (itself a group, or under one)? The
        /// navigator uses this to decide whether Left/Right get tree semantics.</summary>
        public static bool InTree(GraphNode node)
        {
            for (GraphNode n = node; n != null; n = n.Parent)
                if (n.Expandable) return true;
            return false;
        }

        /// <summary>Right on a group: open it AND move to its first child, in ONE press (owner ruling
        /// 2026-08-22). A shut group and an open one answer Right the same way, so the child is
        /// announced with its position and the header's "expanded" word is never heard - the player is
        /// no longer standing on the header for it to be said about. Right on a leaf that names a place
        /// elsewhere in the graph: follow the reference (<see cref="NodeVtable.OnFollow"/>). Right
        /// elsewhere in a tree: Leaf (consume).
        ///
        /// A group that turns out to hold NOTHING is left OPEN and reports EmptyGroup ("Nothing in
        /// here"). It used to bounce shut, which undid whatever the expansion itself did for the player:
        /// a galaxy system's <see cref="NodeVtable.OnExpand"/> brings the camera in, and the
        /// re-collapse zoomed straight back out. Left is how such a group is shut again.
        ///
        /// The order is the contract: a group's own children win, so OnFollow is only ever reached on a
        /// node that has nothing to descend into. Following is deliberately NOT modelled as an
        /// OnExpand override - a group whose expansion declares no children reports EmptyGroup, which
        /// speaks "no details" over the very move the handler just made.</summary>
        public TreeResult TreeRight()
        {
            TreeResult result = new TreeResult { Kind = TreeMove.None };
            if (!Rerender()) return result;
            GraphNode node = CurrentNode;
            if (node == null) return result;

            if (node.Expandable && !node.Expanded)
            {
                SetExpanded(node, true);
                if (!Rerender()) return result;
                GraphNode header = _current.NodeAt(node.Id);
                if (header == null) return result;
                GraphNode opened = FirstChildOf(header);
                if (opened == null)
                {
                    // A lazy drill-in that resolved to nothing. The branch STAYS open: expanding is
                    // allowed to act, and shutting it again the same frame would undo the act as well
                    // as the expansion.
                    result.Kind = TreeMove.EmptyGroup;
                    return result;
                }

                result.Move.From = header;
                SetCurrent(opened);
                result.Move.To = opened;
                result.Move.Moved = true;
                result.Kind = TreeMove.Descended;
                result.Opened = header;
                return result;
            }

            if (node.Expandable && node.Expanded)
            {
                GraphNode child = FirstChildOf(node);
                if (child == null) { result.Kind = TreeMove.Leaf; return result; }
                result.Move.From = node;
                SetCurrent(child);
                result.Move.To = child;
                result.Move.Moved = true;
                result.Kind = TreeMove.Descended;
                return result;
            }

            if (node.Vtable != null && node.Vtable.OnFollow != null)
            {
                node.Vtable.OnFollow();
                result.Kind = TreeMove.Followed;
                return result;
            }

            result.Kind = InTree(node) ? TreeMove.Leaf : TreeMove.None;
            return result;
        }

        /// <summary>Come back to a group this press already opened and seat the cursor on its first
        /// child, off the build as it stands NOW (<see cref="TreeResult.Opened"/>).
        ///
        /// What a branch holds is not always known on the frame it is opened: a page whose expansion
        /// makes the GAME show somewhere else only learns its real children once the game has drawn
        /// them, and the first child of the half-built list is not the first child of the finished one.
        /// So the descend <see cref="TreeRight"/> makes on such a page is provisional, and this is how
        /// it is re-made when the page has settled: the same first-child rule against a fresh render.
        ///
        /// Answers Descended whether or not the cursor actually changed nodes - the settled first child
        /// is what the player is told about either way - and EmptyGroup where the group has since lost
        /// every child, which is the "no details" the provisional descend was too early to judge. None
        /// where the group itself is gone: something else has changed the page, and the cursor it left
        /// behind is the answer.</summary>
        public TreeResult TreeDescend(ControlId groupId)
        {
            TreeResult result = new TreeResult { Kind = TreeMove.None };
            if (groupId == null || !Rerender()) return result;
            GraphNode header = _current.NodeAt(groupId);
            if (header == null) return result;

            GraphNode child = FirstChildOf(header);
            if (child == null)
            {
                result.Kind = TreeMove.EmptyGroup;
                return result;
            }

            result.Move.From = header;
            SetCurrent(child);
            result.Move.To = child;
            result.Move.Moved = true;
            result.Kind = TreeMove.Descended;
            return result;
        }

        /// <summary>Left on an expanded group: collapse, cursor unmoved. Left anywhere else in a tree: go
        /// up to the nearest focusable ancestor AND shut it behind us, in ONE press (owner ruling
        /// 2026-08-22) - the parent is announced, and its "collapsed" word rides that announcement.
        /// Whatever closing acts on (the galaxy's collapse un-zoom) therefore acts once per press,
        /// exactly as it did over the two presses this replaces.</summary>
        public TreeResult TreeLeft()
        {
            TreeResult result = new TreeResult { Kind = TreeMove.None };
            if (!Rerender()) return result;
            GraphNode node = CurrentNode;
            if (node == null) return result;

            if (node.Expandable && node.Expanded)
            {
                SetExpanded(node, false);
                Rerender(); // focus stays on the header by identity
                result.Kind = TreeMove.Collapsed;
                return result;
            }

            for (GraphNode p = node.Parent; p != null; p = p.Parent)
            {
                if (!p.Focusable || !_current.Nodes.ContainsKey(p.Id)) continue;
                result.Move.From = node;
                GraphNode target = _current.NodeAt(p.Id);
                // The cursor moves BEFORE the branch shuts: the node it was standing on is about to stop
                // being declared, and reconciliation would otherwise have to guess where it went.
                SetCurrent(target);
                if (target.Expandable && target.Expanded)
                {
                    SetExpanded(target, false);
                    if (Rerender())
                    {
                        GraphNode shut = _current.NodeAt(target.Id);
                        if (shut != null)
                        {
                            SetCurrent(shut);
                            target = shut;
                        }
                    }
                }

                result.Move.To = target;
                result.Move.Moved = true;
                result.Kind = TreeMove.Ascended;
                return result;
            }

            result.Kind = InTree(node) ? TreeMove.Leaf : TreeMove.None;
            return result;
        }

        /// <summary>Home/End inside a tree: the first/last node sharing the focused node's parent (its
        /// siblings at the current depth) — and its STOP, always. A root-level node has no parent to
        /// compare, so parent alone made every root-level node on the page a sibling and End on a
        /// top-level group walked out of the panel onto another stop's last control. Home and End never
        /// leave the stop they were pressed in, in a tree exactly as anywhere else.</summary>
        public MoveResult MoveToSiblingEdge(bool first)
        {
            MoveResult result = default(MoveResult);
            if (!Rerender()) return result;
            GraphNode node = CurrentNode;
            result.From = node;
            result.To = node;
            if (node == null) return result;

            GraphNode target = null;
            foreach (GraphNode n in _current.Order)
            {
                if (!ReferenceEquals(n.Parent, node.Parent)) continue;
                if (!Equals(n.StopKey, node.StopKey)) continue;
                if (first) { target = n; break; }
                target = n; // last match wins
            }
            if (target == null || target == node) return result;
            SetCurrent(target);
            result.To = target;
            result.Moved = true;
            return result;
        }

        // ---- reaching a control that is not declared yet ----

        /// <summary>
        /// How close the standing render is to being able to focus <paramref name="id"/>, opening one
        /// level of ancestry towards it where that is what is missing.
        ///
        /// A collapsed group declares no children, so a landing aimed inside one is aimed at nothing:
        /// the id cannot be looked up, and the node it hangs under cannot be read off the render
        /// either. What CAN be read is the id itself - see <see cref="AncestorKeys"/> - so the deepest
        /// declared ancestor is found by key and opened, one per call, because its children only exist
        /// on the build that follows. Opening goes through <see cref="SetExpanded"/> like every other
        /// expansion, so a group whose <see cref="NodeVtable.OnExpand"/> is an override does its own
        /// bookkeeping and its own side effects (a camera flying into the thing being opened).
        ///
        /// Asked of the render as it stands rather than re-rendering: the caller is the per-frame
        /// focus pass, which has just built one.
        /// </summary>
        public ReachStep Reach(ControlId id)
        {
            if (_current == null || id == null)
            {
                return ReachStep.Unreachable;
            }

            if (_current.Nodes.ContainsKey(id))
            {
                return ReachStep.Present;
            }

            GraphNode ancestor = DeepestDeclaredAncestor(_current, id);
            if (ancestor == null)
            {
                return ReachStep.Unreachable;
            }

            // Already open, or not a group at all (a row that becomes a group only once the game draws
            // the control its children are: a planet's card). Either way there is nothing to open here
            // and the only question left is whether the game produces the child - the caller's budget.
            if (!ancestor.Expandable || ancestor.Expanded)
            {
                return ReachStep.Waiting;
            }

            SetExpanded(ancestor, true);
            return ReachStep.Opened;
        }

        /// <summary>The deepest node in <paramref name="render"/> that <paramref name="id"/> hangs
        /// under, or null where the render holds none of its ancestry.</summary>
        public static GraphNode DeepestDeclaredAncestor(GraphRender render, ControlId id)
        {
            if (render == null || id == null)
            {
                return null;
            }

            IList<object> keys = AncestorKeys(id.StructuralKey);
            for (int i = 0; i < keys.Count; i++)
            {
                GraphNode node = render.NodeAt(ControlId.Structural(keys[i]));
                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// The keys of the controls a control hangs under, deepest first - read out of the id, because
        /// an undeclared control has no node to read a parent chain off.
        ///
        /// This is the one place the engine assumes anything about what a structural key IS: a PATH,
        /// whose <c>/</c>-separated head names the thing this control belongs to
        /// (<c>galaxy:system/548/planet/0/action/0</c> hangs under <c>galaxy:system/548/planet/0</c>,
        /// which hangs under <c>galaxy:system/548</c>). Splitting on the separator rather than
        /// comparing raw string prefixes is what keeps <c>system/5</c> from claiming
        /// <c>system/548</c>'s children. Not every head is a declared control - the ones that are not
        /// are simply missed - and a key that is not a path (a composite, an object) answers with
        /// nothing, which leaves such a control reachable only while it is declared.
        /// </summary>
        public static IList<object> AncestorKeys(object structuralKey)
        {
            List<object> keys = new List<object>();
            string path = structuralKey as string;
            if (path == null)
            {
                return keys;
            }

            for (int cut = path.LastIndexOf('/'); cut > 0; cut = path.LastIndexOf('/', cut - 1))
            {
                keys.Add(path.Substring(0, cut));
            }

            AddGrouping(keys, structuralKey);
            return keys;
        }

        /// <summary>
        /// A LEVEL OF THE TREE THAT IS NOT IN THE KEY, declared by the page that builds it.
        ///
        /// The path rule above is what lets every programmatic landing - a lane hop, a scanner go-to, a
        /// bookmark jump, a type-ahead result, a leap restored - open the branches it is aimed inside,
        /// one level per build. It fails for exactly one shape: a group whose members deliberately KEEP
        /// the keys they have elsewhere, so that the cursor costs nothing when the page re-groups them
        /// (the scan lens's owner headings, whose stars are keyed as the ordinary map keys them). Such a
        /// heading is nowhere in its members' ancestry, so a landing inside a shut one is aimed at a
        /// node nothing declares and does nothing at all - silently.
        ///
        /// So a page may name the extra parent itself. The hook is asked about the key AND about each
        /// of its path ancestors, so a row deep inside such a member - a planet, a lane, a dossier -
        /// gets the same heading as its star; the answer is added OUTERMOST, since a level the key does
        /// not mention is by construction above everything the key does. One hook rather than a call at
        /// every landing site: they all come through the ancestry, which is why the ancestry is where
        /// the fix belongs.
        ///
        /// Injected the way the announcer's and the sheet's wording is (<see cref="Reset"/> clears it),
        /// because this is a fact about one page's tree and the engine has no page to ask.
        /// </summary>
        public static Func<object, object> GroupingAncestor;

        /// <summary>Drop the injected hook - mod teardown, and test isolation.</summary>
        public static void Reset()
        {
            GroupingAncestor = null;
        }

        private static void AddGrouping(List<object> keys, object structuralKey)
        {
            Func<object, object> ask = GroupingAncestor;
            if (ask == null)
            {
                return;
            }

            object grouping = ask(structuralKey);
            for (int i = 0; grouping == null && i < keys.Count; i++)
            {
                grouping = ask(keys[i]);
            }

            if (grouping != null && !keys.Contains(grouping))
            {
                keys.Add(grouping);
            }
        }

        // Change a group's expansion: through its vtable override when declared (an adapter driving a
        // retained game-side container), else the persistent set.
        private void SetExpanded(GraphNode group, bool expanded)
        {
            if (expanded && group.Vtable.OnExpand != null) { group.Vtable.OnExpand(); return; }
            if (!expanded && group.Vtable.OnCollapse != null) { group.Vtable.OnCollapse(); return; }
            if (expanded) _state.Expanded.Add(group.Id);
            else _state.Expanded.Remove(group.Id);
        }

        /// <summary>The first node a Right into this group should land on.
        ///
        /// A group's children are not always parented straight onto it: a CONTEXT
        /// (<c>GraphBuilder.PushContext</c>) is a non-focusable level, so a group that puts its
        /// children under a named section has the section as their parent and the group as their
        /// grandparent. Comparing parents alone therefore reported such a group EMPTY and
        /// auto-recollapsed it - "Nothing in here" over a group full of nodes. The chain is walked
        /// instead, stopping at the first FOCUSABLE level: a node under a nested group belongs to that
        /// group, not to this one.</summary>
        private GraphNode FirstChildOf(GraphNode group)
        {
            foreach (GraphNode n in _current.Order)
                if (Under(n, group)) return n;
            return null;
        }

        private static bool Under(GraphNode node, GraphNode group)
        {
            for (GraphNode at = node.Parent; at != null; at = at.Parent)
            {
                if (ReferenceEquals(at, group)) return true;
                if (at.Focusable) return false;
            }
            return false;
        }

        // ---- behavior invokers (the caller announces fallbacks / state) ----

        /// <summary>Run the focused control's primary activation. False = it has none.</summary>
        public bool Activate()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null || node.Vtable.OnActivate == null) return false;
            node.Vtable.OnActivate();
            return true;
        }

        /// <summary>Run the focused control's secondary activation. False = it has none.</summary>
        public bool Secondary()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null || node.Vtable.OnSecondary == null) return false;
            node.Vtable.OnSecondary();
            return true;
        }

        /// <summary>Run the focused control's other activation - the game's own Alt+click - or, where
        /// the control wires no handler of its own for it, replay its plain click
        /// (<see cref="ModifiedClick"/>). False = it has neither.</summary>
        public bool Alternate()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null) return false;
            return ModifiedClick(node.Vtable.OnAlternate, node.Vtable.OnActivate);
        }

        /// <summary>Run the focused control's contextual command - the game's right click. False =
        /// it has none, and the caller says so.</summary>
        public bool Contextual()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null || node.Vtable.OnContextual == null) return false;
            node.Vtable.OnContextual();
            return true;
        }

        /// <summary>Whether the focused control offers a go-to-location - the key's availability, asked
        /// off the standing render so a key scan can ask it many times a frame.</summary>
        public bool OffersGoTo
        {
            get
            {
                GraphNode node = CurrentNode;
                return node != null && node.Vtable != null && node.Vtable.OnGoTo != null;
            }
        }

        /// <summary>Go to where the focused control's thing happened. False = it offers none, and the
        /// caller leaves the press alone.</summary>
        public bool GoToLocation()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null || node.Vtable.OnGoTo == null) return false;
            node.Vtable.OnGoTo();
            return true;
        }

        /// <summary>Whether the focused control offers a clear - the key's availability, asked off the
        /// standing render so a key scan can ask it many times a frame.</summary>
        public bool OffersClear
        {
            get
            {
                GraphNode node = CurrentNode;
                return node != null && node.Vtable != null && node.Vtable.OnClear != null;
            }
        }

        /// <summary>Empty the focused control. False = it offers no such thing, and the caller leaves
        /// the press alone.</summary>
        public bool Clear()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null || node.Vtable.OnClear == null) return false;
            node.Vtable.OnClear();
            return true;
        }

        /// <summary>Run the focused control's double-click command - the game's own second click.
        /// False = it has none, and the caller says nothing rather than falling back to the single
        /// click.</summary>
        public bool DoubleClick()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null || node.Vtable.OnDoubleClick == null) return false;
            node.Vtable.OnDoubleClick();
            return true;
        }

        /// <summary>Add the focused control's item to the game's selection, or take it out - and where
        /// the control is not part of a selection, replay its plain click instead
        /// (<see cref="ModifiedClick"/>). False = it has neither.</summary>
        public bool SelectToggle()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null) return false;
            return ModifiedClick(node.Vtable.OnSelectToggle, node.Vtable.OnActivate);
        }

        /// <summary>Extend the game's selection to the focused control's item - and where the control
        /// is not part of a selection, replay its plain click instead (<see cref="ModifiedClick"/>).
        /// False = it has neither.</summary>
        public bool SelectRange()
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null) return false;
            return ModifiedClick(node.Vtable.OnSelectRange, node.Vtable.OnActivate);
        }

        // A modified click a control does not wire a handler for is still a CLICK, and the player is
        // physically holding the modifier while it runs (the chord keys pass it straight through). So
        // replaying the control's own click is the whole implementation of every modified click the GAME
        // understands and the mod has never heard of - Ctrl+click to locate a technology in the tree,
        // Alt+click to queue at the head - with no per-screen wiring, because the game's handler is what
        // reads the modifier and branches. A handler that ignores modifiers just does its ordinary thing,
        // exactly as a modified mouse click would. A wired slot stays an OVERRIDE, for the controls where
        // the game runs a genuinely different handler. With neither, nothing runs and the caller stays
        // silent: the chord is never lent to some other control's command.
        private static bool ModifiedClick(Action slot, Action click)
        {
            Action run = slot ?? click;
            if (run == null) return false;
            run();
            return true;
        }

        /// <summary>If the focused control adjusts horizontally (a slider), adjust and return true;
        /// false = the caller should navigate instead.</summary>
        public bool TryAdjust(int sign, bool large)
        {
            if (!Rerender()) return false;
            GraphNode node = CurrentNode;
            if (node == null || node.Vtable.OnAdjust == null) return false;
            node.Vtable.OnAdjust(sign, large);
            return true;
        }
    }
}
