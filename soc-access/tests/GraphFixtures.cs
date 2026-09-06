using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>Shorthand for declaring test graphs — the graph engine takes plain data, so the fixtures
    /// stay readable without a game.</summary>
    internal static class Graphs
    {
        public static ControlId Id(string key)
        {
            return ControlId.Structural(key);
        }

        /// <summary>A label-only control.</summary>
        public static NodeVtable Vt(string label)
        {
            return new NodeVtable { Announcements = new[] { NodeAnnouncement.Static(label) } };
        }

        /// <summary>A control with a label plus extra parts.</summary>
        public static NodeVtable Vt(string label, params NodeAnnouncement[] extra)
        {
            List<NodeAnnouncement> anns = new List<NodeAnnouncement> { NodeAnnouncement.Static(label) };
            anns.AddRange(extra);
            return new NodeVtable { Announcements = anns };
        }

        public static NodeAnnouncement Part(string text, string kind)
        {
            return new NodeAnnouncement(() => text, false, kind);
        }

        /// <summary>A control type whose common part is a role word, in the mod's standard kind order.</summary>
        public static ControlType Type(string key, string roleWord)
        {
            return new ControlType
            {
                Key = key,
                Order = new[]
                {
                    AnnouncementKinds.Label,
                    AnnouncementKinds.Role,
                    AnnouncementKinds.Value,
                    AnnouncementKinds.Selected,
                    AnnouncementKinds.Enabled,
                    AnnouncementKinds.Tooltip,
                    AnnouncementKinds.Position,
                },
                Common = () => new NodeAnnouncement[] { Part(roleWord, AnnouncementKinds.Role) },
            };
        }

        public static GraphNode Node(GraphRender render, string key)
        {
            return render.NodeAt(Id(key));
        }

        public static ControlId Dest(GraphNode node, GraphDir dir)
        {
            Transition t;
            return node.Transitions.TryGetValue(dir, out t) && t != null ? t.Destination : null;
        }

        public static string DestKey(GraphNode node, GraphDir dir)
        {
            ControlId d = Dest(node, dir);
            return d == null ? null : (string)d.StructuralKey;
        }

        public static string Label(GraphNode node)
        {
            return node == null ? null : GraphAnnouncer.FirstPartText(node);
        }

        /// <summary>A node's own structural key, or null for no node — the shape every navigation
        /// assertion is written in.</summary>
        public static string Key(GraphNode node)
        {
            return node == null ? null : (string)node.Id.StructuralKey;
        }

        /// <summary>A tooltip section of the given loudness, spelled out line by line.</summary>
        public static NodeSection Section(TooltipMode mode, params string[] lines)
        {
            List<string> list = new List<string>(lines);
            return NodeSection.Derived(() => list, mode, null);
        }

        /// <summary>The same, for a tooltip whose class draws a cost panel: the price the panel would
        /// show rides on the section, exactly as the door puts it there.</summary>
        public static NodeSection Priced(TooltipMode mode, string cost, params string[] lines)
        {
            List<string> list = new List<string>(lines);
            return NodeSection.Derived(() => list, mode, null, null, () => cost);
        }

        /// <summary>An INDICATED tooltip this player has asked to hear: its own words are the lines,
        /// and <paramref name="late"/> is the reader that answers nothing until the game has drawn
        /// them - the shape <c>LongTooltips.Announced</c> hands the door.</summary>
        public static NodeSection LateSection(Func<IList<string>> late, params string[] lines)
        {
            List<string> list = new List<string>(lines);
            return NodeSection.Derived(() => list, TooltipMode.Indicate, null, null, null, late);
        }

        public static IList<NodeSection> Sections(params NodeSection[] sections)
        {
            return new List<NodeSection>(sections);
        }

        /// <summary>The words a tooltip fixture carries when the words themselves are beside the
        /// point.</summary>
        public static readonly Func<IList<string>> Words = () =>
            new List<string> { "Click to consult the empire summary" };

        /// <summary>What the review buffer holds for a lone node.</summary>
        public static List<string> Buffer(NodeVtable vtable)
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("t"), vtable));
            return NodeBuffer.Lines(Node(b.Build(), "t"));
        }

        /// <summary>What the review buffer holds for a node with a neighbour either side, so it reads
        /// a position too.</summary>
        public static List<string> BufferAmongNeighbours(NodeVtable vtable)
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("Before")));
            b.AddItem(new SyntheticNode(Id("t"), vtable));
            b.AddItem(new SyntheticNode(Id("c"), Vt("After")));
            return NodeBuffer.Lines(Node(b.Build(), "t"));
        }

        /// <summary>A render callback that rebuilds from <paramref name="declare"/> every time, the way a
        /// real screen does.</summary>
        public static Func<GraphRender> Renderer(Action<GraphBuilder> declare, GraphState state = null)
        {
            return () =>
            {
                GraphBuilder b = state != null ? new GraphBuilder(state.Expanded) : new GraphBuilder();
                declare(b);
                return b.Build();
            };
        }
    }
}
