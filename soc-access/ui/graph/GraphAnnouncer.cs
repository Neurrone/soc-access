using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// Composes the spoken line for a focus change by diffing the old and new focus PATHS — each node's
    /// ancestor chain (<see cref="GraphNode.Parent"/>) plus the node itself, compared by identity. Newly-
    /// entered levels read outermost-first, then the landing control: "Difficulty settings, list, Normal,
    /// radio button, selected", recursing as deep as the hierarchy goes. Sibling moves share the whole
    /// prefix and read just the control; ascends likewise; and descending from a group onto its own child
    /// re-announces nothing but the child — the group is on the child's chain AND is the from-node, so the
    /// prefix swallows it. A retained-path diff, reconstructed per render from parent pointers.
    ///
    /// Parts are joined with <see cref="ModStrings.Graph.ListSeparator"/> via <see cref="MessageBuilder"/>, so
    /// the punctuation is translatable rather than baked in here.
    ///
    /// The three injection points (<see cref="PartFilter"/>, <see cref="PositionText"/>,
    /// <see cref="ExpandedStateText"/>) are static because every node's readout flows through them and
    /// threading them per-call would touch every node factory. They are process state, so
    /// <see cref="Reset"/> exists for mod teardown and for test isolation.
    ///
    /// WHERE a part lands in the readout is decided by its KIND, not its declaration order: the
    /// control type's kind order sorts first, declaration index only breaks ties within one kind.
    /// So "speak this immediately after the name" means giving the part the name's own kind (a
    /// second Label), never inserting it early in the part list — an early Value-kind part still
    /// sorts after the role word.
    /// </summary>
    public static class GraphAnnouncer
    {
        /// <summary>The line for landing on <paramref name="to"/> having come from <paramref name="from"/>
        /// (null = from nothing: the full path reads). <paramref name="transitionLabel"/> is the crossed
        /// edge's spoken line, when it had one. Null when there is nothing to say.</summary>
        public static string Compose(GraphNode from, GraphNode to, string transitionLabel = null)
        {
            if (to == null) return null;

            List<GraphNode> toPath = PathOf(to);
            List<GraphNode> fromPath = from != null ? PathOf(from) : EmptyPath;

            // Common prefix by identity — levels we were already inside (or ON: descending from a group
            // onto its child keeps the group in the prefix) stay silent.
            int i = 0;
            while (i < fromPath.Count && i < toPath.Count && fromPath[i].Id.Equals(toPath[i].Id)) i++;

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(transitionLabel)) parts.Add(transitionLabel);

            // The column the landing is IN, where no edge was crossed to say it - said next to the
            // control rather than at the head of the line, because the levels above it (the table's
            // own name) are outside it and read first.
            string column = string.IsNullOrEmpty(transitionLabel) ? ColumnEntered(from, to) : null;

            // The row's position, worked out here because it is the one part that depends on where
            // focus came FROM - and handed to the landing's own composition rather than appended after
            // it, because the usage hints come last and a position said after them would not be.
            string row = RowPosition(from, to);

            if (i >= toPath.Count)
            {
                // Ascended (or same node): announce just the now-innermost focus.
                if (!string.IsNullOrEmpty(column)) parts.Add(column);
                string text = LeafText(to, row);
                if (!string.IsNullOrEmpty(text)) parts.Add(text);
            }
            else
            {
                for (int j = i; j < toPath.Count; j++)
                {
                    if (j == toPath.Count - 1 && !string.IsNullOrEmpty(column)) parts.Add(column);
                    string text = LeafText(toPath[j], j == toPath.Count - 1 ? row : null);
                    if (string.IsNullOrEmpty(text)) continue;
                    // Dedupe: a level whose label just duplicates the next level down (or the control
                    // itself — "a 'Game difficulty' section wrapping the 'Game difficulty' control").
                    if (j + 1 < toPath.Count)
                    {
                        string label = FirstPartText(toPath[j]);
                        string next = FirstPartText(toPath[j + 1]);
                        if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(next)
                            && DuplicatesNext(label, next)) continue;
                    }
                    parts.Add(text);
                }
            }

            return Join(parts);
        }

        /// <summary>
        /// The column heading a landing has just arrived under, or null.
        ///
        /// Only cells that carry one (<see cref="NodeVtable.ColumnHeader"/> - a table whose rows have
        /// no name) and only where the column CHANGED: walking down a column says its heading once, and
        /// a step sideways is already labelled by the edge it crossed, which is why the caller asks
        /// this only when there was no such label. Compared by the heading's WORDS rather than by the
        /// column number, because everything outside a table sits at column 0 and a Tab landing on the
        /// primary would otherwise look like a move that had not left the column.
        /// </summary>
        private static string ColumnEntered(GraphNode from, GraphNode to)
        {
            NodeVtable vt = to != null ? to.Vtable : null;
            string header = vt != null ? vt.ColumnHeader : null;
            if (string.IsNullOrEmpty(header)) return null;
            NodeVtable was = from != null ? from.Vtable : null;
            return was != null && header == was.ColumnHeader ? null : header;
        }

        /// <summary>
        /// Where the landing row sits in its table ("3 of 12"), or null.
        ///
        /// A table's position is a fact about the ROW, so it is said on arriving in a row and on moving
        /// to a different one, and NOT while the player walks that row's columns - including the step
        /// back onto column 0, which arrives at the same node an arrival would and is the case no
        /// per-node part could tell apart. That is what makes it live here rather than in
        /// <see cref="LeafText"/>: it is the one part of a readout that depends on where focus came FROM.
        /// Rows are compared by <see cref="TableRow.Key"/>, not by object, because the two nodes come
        /// from different renders.
        /// </summary>
        private static string RowPosition(GraphNode from, GraphNode to)
        {
            NodeVtable vt = to != null ? to.Vtable : null;
            TableRow row = vt != null ? vt.Row : null;
            if (row == null || row.Count <= 0 || row.Index <= 0 || PositionText == null) return null;
            if (vt.SpeaksOwnPosition || HasKind(vt.Announcements, AnnouncementKinds.Position)) return null;
            TableRow was = from != null && from.Vtable != null ? from.Vtable.Row : null;
            if (was != null && was.Key != null && was.Key == row.Key) return null;
            if (PartFilter != null && !PartFilter(vt.ControlType, AutoPositionProbe)) return null;
            return PositionText(row.Index, row.Count);
        }

        /// <summary>The full readout for a landing with no prior focus (screen entry, focus restore).</summary>
        public static string ComposeFull(GraphNode to)
        {
            return Compose(null, to);
        }

        /// <summary>Drop every injected delegate — mod teardown, and test isolation.</summary>
        public static void Reset()
        {
            PartFilter = null;
            PositionText = null;
            ExpandedStateText = null;
            Carry = null;
            _memoNode = null;
            _memoParts = null;
            _memoFilter = null;
            _memoCarry = null;
        }

        /// <summary>The live drag (<see cref="CarryState"/>) - the same object the carry keys act on, so
        /// what a control SAYS about dragging and what the keys do to it cannot disagree. Null (tests,
        /// boot, a game with no drags) = no control says anything about dragging.</summary>
        public static CarryState Carry;

        private static readonly List<GraphNode> EmptyPath = new List<GraphNode>();

        // The node's path: ancestors outermost-first, then the node itself.
        private static List<GraphNode> PathOf(GraphNode node)
        {
            List<GraphNode> path = new List<GraphNode>();
            for (GraphNode n = node; n != null; n = n.Parent) path.Add(n);
            path.Reverse();
            return path;
        }

        private static string Join(List<string> parts)
        {
            MessageBuilder mb = new MessageBuilder();
            for (int i = 0; i < parts.Count; i++) mb.ListItem(parts[i]);
            return mb.Build();
        }

        /// <summary>Pluggable per-part filter — installed by the host to consult the user's announcement
        /// settings (per control type + per kind); null (tests, boot) = everything speaks. Returning false
        /// drops the part from readouts AND from the live watch.</summary>
        public static Func<ControlType, NodeAnnouncement, bool> PartFilter;

        /// <summary>
        /// A node's EFFECTIVE announcement parts: the control type's common parts (the role word) merged
        /// with the node's own AND the tooltip part its <see cref="NodeVtable.Sections"/> project to — a
        /// node part overrides a common part of the same kind — sorted by the type's kind order
        /// (unknown/kindless parts append in declaration order), then filtered by the user's settings.
        /// This is the single list readouts and the live watch operate on.
        ///
        /// The tooltip part is DERIVED here rather than declared by a screen: the sections are the one
        /// place a control's content is written down, and a screen that also hand-built the part is how
        /// the two used to drift apart.
        /// </summary>
        public static List<NodeAnnouncement> EffectiveAnnouncements(GraphNode node)
        {
            // Computed once per node per render and shared by every caller - the focus readout, the
            // review buffer and the live watch all ask for it on the same node in the same frame. A
            // GraphNode instance is allocated fresh by every GraphBuilder.Build, so the node REFERENCE
            // is the (node id, render) key: a memo cannot outlive the render that made the node, and
            // no render counter has to be threaded through Core to say so. The two injection points
            // that change what the list CONTAINS join the key, because a host (or a test) can swap
            // either between calls with no rebuild in between. Callers read the list, never mutate it.
            if (ReferenceEquals(node, _memoNode)
                && ReferenceEquals(PartFilter, _memoFilter)
                && ReferenceEquals(Carry, _memoCarry))
                return _memoParts;

            List<NodeAnnouncement> result = new List<NodeAnnouncement>();
            NodeVtable vt = node != null ? node.Vtable : null;
            if (vt == null) return Memoize(node, result);
            ControlType type = vt.ControlType;

            IList<NodeAnnouncement> common = type != null && type.Common != null ? type.Common() : null;
            if (common != null)
                foreach (NodeAnnouncement c in common)
                    if (c != null && !HasKind(vt.Announcements, c.Kind)) result.Add(c);
            if (vt.Announcements != null)
                foreach (NodeAnnouncement a in vt.Announcements)
                    if (a != null) result.Add(a);
            // Always, never "unless the node declared one of its own": a screen CAN still add a
            // tooltip-kind part for something a section cannot express (a drop-list entry's live
            // refusal), and suppressing the derived part when it does would silently take the
            // indication away from exactly the rows that have the most to review.
            //
            // It is composed against the parts already in hand, which is why it is derived HERE and
            // not by the node: a control the game named nowhere but in its tooltip has its label read
            // off that tooltip's first line, and the one place both the label and the tooltip exist
            // together is this list. Passing them over is what lets EVERY such control announce its
            // whole tooltip without any of them saying its first line twice.
            // WHAT IT COSTS, derived for the same reason and from the same declaration: the game
            // draws a cost line for a tooltip CLASS, so which controls have a price to say is already
            // written down in the sections and no screen composes one. Before the tooltip part so
            // that a price is never taken for one of the tooltip's own lines by the dedupe, and
            // kinded as a second Label so it speaks beside the name (TooltipParts.CostPart).
            NodeAnnouncement cost = TooltipParts.CostPart(vt.Sections);
            if (cost != null) result.Add(cost);

            NodeAnnouncement tooltip = TooltipParts.Part(vt.Sections, result);
            if (tooltip != null) result.Add(tooltip);

            // The drag indication ("draggable" / "drop target"), derived for the same reason and in the
            // same place: which controls can be picked up and which will take a drop is already written
            // down in the vtable, so no screen composes the word and every screen with a drag has it.
            // Kindless, so it sits at the tail of the readout - what a control has to SAY about
            // itself, after everything it IS.
            if (Carry != null)
            {
                NodeAnnouncement source = Carry.DraggablePart(vt);
                if (source != null) result.Add(source);
                NodeAnnouncement target = Carry.DropTargetPart(vt);
                if (target != null) result.Add(target);
            }

            // The USAGE HINTS, derived from the same declaration the review buffer reads them off
            // (<see cref="NodeHints"/>) and spoken LAST - after the position, after everything the
            // control has to say about itself, because they are about the keyboard rather than about
            // the thing (owner ruling 2026-09-03). Added after the drag words and kinded with a kind
            // no control type orders, which is what keeps them at the tail whatever else a node
            // declares.
            NodeAnnouncement hints = HintPart(vt);
            if (hints != null) result.Add(hints);

            if (type != null && type.Order != null && type.Order.Length > 0 && result.Count > 1)
            {
                // Insertion sort on the kind's order index, in place. Stable by construction - an
                // equal key never moves past its predecessor - so same-bucket (kindless) parts keep
                // declaration order, which List.Sort would scramble. The composite (order, index) key
                // list and its comparison delegate that used to buy that stability were two
                // allocations on a path every focused node walks.
                for (int i = 1; i < result.Count; i++)
                {
                    NodeAnnouncement moving = result[i];
                    int key = OrderIndex(type.Order, moving.Kind);
                    int j = i - 1;
                    while (j >= 0 && OrderIndex(type.Order, result[j].Kind) > key)
                    {
                        result[j + 1] = result[j];
                        j--;
                    }

                    result[j + 1] = moving;
                }
            }

            if (PartFilter != null)
            {
                // Compacted in place rather than RemoveAll: the predicate would capture the control
                // type and allocate a closure per node per frame.
                int kept = 0;
                for (int i = 0; i < result.Count; i++)
                    if (PartFilter(type, result[i])) result[kept++] = result[i];
                if (kept < result.Count) result.RemoveRange(kept, result.Count - kept);
            }

            return Memoize(node, result);
        }

        /// <summary>
        /// The control's usage hints as one announcement part, or null where it declared none.
        ///
        /// On a control whose tooltip the game assembles on hover AND that this player has asked to
        /// hear (<see cref="TooltipParts.LateReader"/>), the part waits for the same words the tooltip
        /// part waits for and says nothing until they arrive: the hint is the last thing heard about
        /// the control, and on those controls the tooltip is heard frames after the readout. It is
        /// live for exactly that reason, and the wait is the whole of it - a tooltip that never draws
        /// before the player walks away takes the hint with it, the same way it takes its own words.
        /// </summary>
        private static NodeAnnouncement HintPart(NodeVtable vt)
        {
            if (vt.Hints == null || vt.Hints.Count == 0) return null;
            NodeVtable it = vt;
            Func<IList<string>> late = TooltipParts.LateReader(vt.Sections);
            return new NodeAnnouncement(
                () => HintText(it, late),
                live: late != null,
                kind: AnnouncementKinds.Hint
            );
        }

        private static string HintText(NodeVtable vt, Func<IList<string>> late)
        {
            if (late != null)
            {
                IList<string> words = late();
                if (words == null || words.Count == 0) return null;
            }

            List<string> lines = new List<string>(2);
            NodeHints.Lines(lines, vt);
            return lines.Count == 0 ? null : Join(lines);
        }

        private static GraphNode _memoNode;
        private static List<NodeAnnouncement> _memoParts;
        private static Func<ControlType, NodeAnnouncement, bool> _memoFilter;
        private static CarryState _memoCarry;

        private static List<NodeAnnouncement> Memoize(GraphNode node, List<NodeAnnouncement> parts)
        {
            _memoNode = node;
            _memoParts = parts;
            _memoFilter = PartFilter;
            _memoCarry = Carry;
            return parts;
        }

        private static bool HasKind(IList<NodeAnnouncement> anns, string kind)
        {
            if (anns == null || kind == null) return false;
            foreach (NodeAnnouncement a in anns)
                if (a != null && a.Kind == kind) return true;
            return false;
        }

        // Sort key: declared kinds by their order index; everything else after (one shared bucket, with
        // the declaration-index tie-break above keeping their relative order).
        private static int OrderIndex(string[] order, string kind)
        {
            if (kind != null)
                for (int i = 0; i < order.Length; i++)
                    if (order[i] == kind) return i;
            return order.Length;
        }

        /// <summary>A node's own readout: its effective announcement parts, resolved live, non-empty ones
        /// joined — plus, for an expandable group, its expanded/collapsed state word. The first part is
        /// the control's label, so path dedupe's prefix check applies.</summary>
        public static string LeafText(GraphNode node)
        {
            return LeafText(node, null);
        }

        /// <summary>The same, with the table position the CALLER worked out ("3 of 12") folded in rather
        /// than said after it. The two cannot be separated any more: the usage hints are the last thing
        /// said about a control and a position comes before them, so the only place that can put one in
        /// front of them is the composition holding both.</summary>
        private static string LeafText(GraphNode node, string tablePosition)
        {
            List<NodeAnnouncement> anns = EffectiveAnnouncements(node);
            List<string> parts = new List<string>(anns.Count + 2);
            // Where the tooltip starts, if the node speaks one: everything a control has to SAY comes
            // after everything it IS, so the expanded/collapsed word goes in ahead of it rather than
            // at the end ("New Game, button, collapsed, Start a new game...").
            int tooltipAt = -1;
            // And where the usage hints start, if it speaks any: they are the tail of the readout, so
            // every position goes in ahead of them rather than after them.
            int hintAt = -1;
            for (int i = 0; i < anns.Count; i++)
            {
                string t = null;
                if (anns[i] != null && anns[i].Text != null) t = anns[i].Text();
                if (string.IsNullOrEmpty(t)) continue;
                if (tooltipAt < 0 && anns[i].Kind == AnnouncementKinds.Tooltip) tooltipAt = parts.Count;
                if (hintAt < 0 && anns[i].Kind == AnnouncementKinds.Hint) hintAt = parts.Count;
                parts.Add(t);
            }

            if (node != null && node.Expandable && !node.Vtable.SpeaksOwnExpansion && ExpandedStateText != null)
            {
                string state = ExpandedStateText(node.Expanded);
                if (!string.IsNullOrEmpty(state))
                {
                    if (tooltipAt >= 0)
                    {
                        parts.Insert(tooltipAt, state);
                        if (hintAt >= tooltipAt) hintAt++;
                    }
                    else Place(parts, ref hintAt, state);
                }
            }

            // The auto-stamped sibling position, unless the node carries its own (an explicit
            // position-kind part, or a composed message that already reads it). Honors the user's
            // per-kind setting.
            if (node != null && node.PositionCount > 0 && PositionText != null
                && !node.Vtable.SpeaksOwnPosition && !HasKind(node.Vtable.Announcements, AnnouncementKinds.Position)
                && (PartFilter == null || PartFilter(node.Vtable.ControlType, AutoPositionProbe)))
            {
                Place(parts, ref hintAt, PositionText(node.PositionIndex, node.PositionCount));
            }

            Place(parts, ref hintAt, tablePosition);
            return Join(parts);
        }

        // A part that belongs before the usage hints: inserted where they start, or appended where the
        // node speaks none.
        private static void Place(List<string> parts, ref int hintAt, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (hintAt < 0)
            {
                parts.Add(text);
                return;
            }

            parts.Insert(hintAt, text);
            hintAt++;
        }

        /// <summary>Pluggable "n of m" wording (localized by the host); null = no auto positions.</summary>
        public static Func<int, int, string> PositionText;

        // A stand-in part handed to the PartFilter so the user's position-kind toggle governs the
        // auto-stamped position too.
        private static readonly NodeAnnouncement AutoPositionProbe =
            new NodeAnnouncement(() => null, kind: AnnouncementKinds.Position);

        /// <summary>Pluggable expanded/collapsed wording for group headers (localized by the host);
        /// null = groups don't speak their state.</summary>
        public static Func<bool, string> ExpandedStateText;

        /// <summary>The first announcement part's text (the label) — for dedupe and search fallbacks.</summary>
        public static string FirstPartText(GraphNode node)
        {
            IList<NodeAnnouncement> anns = node != null && node.Vtable != null ? node.Vtable.Announcements : null;
            if (anns == null || anns.Count == 0) return null;
            NodeAnnouncement first = anns[0];
            return first != null && first.Text != null ? first.Text() : null;
        }

        // The next part "starts as" this label: equal, or its first list-separated segment is the label
        // (a control's readout leads with its label: "Game difficulty, menu button").
        private static bool DuplicatesNext(string label, string next)
        {
            // Ordinal on both: this is a comparison of the mod's OWN composition against itself - the
            // same label written into two parts, and the list separator it wrote between them - so the
            // running culture has no business in it, and a culture-sensitive StartsWith can call two
            // identical strings different (or two different ones equal) depending on where the player
            // lives.
            if (!next.StartsWith(label, StringComparison.Ordinal)) return false;
            if (next.Length == label.Length) return true;
            string sep = ModText.Get(ModStrings.Graph.ListSeparator).TrimEnd();
            return sep.Length > 0
                && next.Substring(label.Length).StartsWith(sep, StringComparison.Ordinal);
        }
    }
}
