using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// What a type-ahead search looks through on the screen the player is on, and what landing on a
    /// result means.
    ///
    /// Three things, because that is all a search needs: how many items there are, what each of them
    /// reads as, and how to reach one. <see cref="Land"/> answers with the control focus should end
    /// up on rather than moving focus itself - the navigator owns focus and announcements, so a
    /// screen supplying its own scope can do the work only it knows about (open the branch a
    /// collapsed item is buried in) and still leave the landing to the one place that speaks.
    ///
    /// <see cref="OverStop"/> is what every screen gets without declaring anything: the controls of
    /// the Tab-stop the cursor is in. A screen overrides it (<c>Screen.TypeAheadScope</c>) only when
    /// the thing the player is searching for is not declared - a tree whose collapsed branches hold
    /// most of the items.
    /// </summary>
    public sealed class SearchScope
    {
        /// <summary>How many items there are to match against.</summary>
        public readonly int Count;

        /// <summary>What item <c>i</c> reads as - the text the player is typing at.</summary>
        public readonly Func<int, string> TextOf;

        /// <summary>Bring item <c>i</c> within reach and answer with the control to put focus on,
        /// or null when it cannot be reached. Called once per result the player lands on, so a
        /// screen may do real work here.</summary>
        public readonly Func<int, ControlId> Land;

        /// <summary>Which control item <c>i</c> IS, asked with no side effects - so that two sources of
        /// results can be merged without offering the same control twice (<see cref="Extend"/>).
        /// Defaults to <see cref="Land"/>, which is the same answer wherever landing is just focusing.
        /// A scope whose landing does real work supplies the pure half here instead.</summary>
        public readonly Func<int, ControlId> IdOf;

        public SearchScope(
            int count,
            Func<int, string> textOf,
            Func<int, ControlId> land,
            Func<int, ControlId> idOf = null
        )
        {
            Count = count;
            TextOf = textOf;
            Land = land;
            IdOf = idOf ?? land;
        }

        /// <summary>
        /// Everything the page would declare IF THE PLAYER HAD OPENED IT ALL, added to what a scope
        /// already offers.
        ///
        /// A tree hides most of itself. The ordinary scope can only match controls that exist, so on
        /// any page with collapsed branches typing finds what the player has already opened - a
        /// confirmation, not a search. <paramref name="deep"/> is the same page built with every group
        /// forced open (<c>GraphBuilder.ExpandAll</c>), which is the page's OWN enumeration of its
        /// contents - structural children and the dossiers a node hangs in its "Tooltips" region alike,
        /// because both are declared by the same build. Nothing here knows what any of them are.
        ///
        /// Offered once each: a control the standing render already holds, or that the scope being
        /// extended already offers (<see cref="IdOf"/>), is skipped. Landing on one is
        /// <paramref name="reveal"/>'s job - open the branches it is buried in and answer with the
        /// control - which is the host's, because opening a group can be a screen's own side effect.
        ///
        /// Built ONCE per search, not per keystroke: a whole page rebuilt with everything open is the
        /// most expensive thing a search does, and what it holds cannot change while the player is
        /// typing at it.
        /// </summary>
        public static SearchScope Extend(
            SearchScope basis,
            GraphRender standing,
            GraphRender deep,
            object stopKey,
            Func<GraphNode, ControlId> reveal
        )
        {
            if (basis == null || deep == null || reveal == null)
            {
                return basis;
            }

            HashSet<ControlId> offered = new HashSet<ControlId>();
            if (standing != null)
            {
                foreach (ControlId id in standing.Nodes.Keys)
                {
                    offered.Add(id);
                }
            }

            if (basis.IdOf != null)
            {
                for (int i = 0; i < basis.Count; i++)
                {
                    ControlId id = basis.IdOf(i);
                    if (id != null)
                    {
                        offered.Add(id);
                    }
                }
            }

            List<GraphNode> hidden = new List<GraphNode>();
            foreach (GraphNode node in deep.Order)
            {
                NodeVtable vtable = node.Vtable;
                if (
                    !Equals(node.StopKey, stopKey)
                    || vtable.ExcludeFromSearch
                    || (vtable.Column > 0 && !vtable.SearchesAsItself)
                    || node.Id == null
                    || offered.Contains(node.Id)
                )
                {
                    continue;
                }

                offered.Add(node.Id);
                hidden.Add(node);
            }

            if (hidden.Count == 0)
            {
                return basis;
            }

            SearchScope outer = basis;
            List<GraphNode> found = hidden;
            Func<GraphNode, ControlId> open = reveal;
            int already = basis.Count;
            return new SearchScope(
                already + found.Count,
                index => index < already ? outer.TextOf(index) : TextFor(found[index - already]),
                index =>
                    index < already ? outer.Land(index) : open(found[index - already]),
                index =>
                    index < already
                        ? (outer.IdOf == null ? null : outer.IdOf(index))
                        : found[index - already].Id
            );
        }

        /// <summary>
        /// The default scope: every control of <paramref name="stopKey"/>, in declaration order.
        ///
        /// A tabular row contributes ONE item, its primary cell (<see cref="NodeVtable.Column"/> 0):
        /// the metadata cells all search as their row's name, so without this every row would appear
        /// once per column and stepping the results would walk cells rather than rows. The exception is
        /// a cell that searches as ITSELF (<see cref="NodeVtable.SearchesAsItself"/>) - a table whose
        /// rows have no name, where each cell is a thing of its own and the filter would make seven
        /// columns of eight unreachable by typing.
        /// </summary>
        public static SearchScope OverStop(GraphRender render, object stopKey)
        {
            List<GraphNode> nodes = new List<GraphNode>();
            if (render != null)
            {
                foreach (GraphNode node in render.Order)
                {
                    NodeVtable vtable = node.Vtable;
                    if (
                        Equals(node.StopKey, stopKey)
                        && !vtable.ExcludeFromSearch
                        && (vtable.Column <= 0 || vtable.SearchesAsItself)
                    )
                    {
                        nodes.Add(node);
                    }
                }
            }

            return new SearchScope(
                nodes.Count,
                index => TextFor(nodes[index]),
                index => nodes[index].Id
            );
        }

        /// <summary>The text a control is searched by: what it declared for the purpose, else its
        /// label - the first part of what focusing it would say. A control whose text cannot be
        /// resolved is simply not matched, rather than taking the whole search down.</summary>
        public static string TextFor(GraphNode node)
        {
            if (node == null)
            {
                return null;
            }

            try
            {
                Func<string> search = node.Vtable.SearchText;
                return search != null ? search() : GraphAnnouncer.FirstPartText(node);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
