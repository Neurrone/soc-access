using System;
using System.Collections.Generic;
using System.Text;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

// Spelled out once: UnityEngine has a Screen of its own, and every use here is the mod's.
using Screen = SongsOfConquestAccess.Screens.Screen;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// The focused graph screen's whole accessible tree, as text, in the grammar <see cref="WidgetDump"/>
    /// emits for widget screens (docs/dev-loop.md section 2a) so the migration diff of one against
    /// the other is a sort and a diff.
    ///
    /// Two properties make it trustworthy, and both are structural: it reads like navigation sounds
    /// (the render comes from <see cref="GraphNavigator.InspectRender"/>, the same build navigation
    /// runs, and every line from <see cref="GraphAnnouncer.Compose"/>, diffed against the line above
    /// it exactly as a walk down the screen would be), and it cannot change what it reports (the
    /// render is thrown away, the cursor is only read, no focus visual runs).
    ///
    /// Every node is read inside its own try/catch: one control whose getter throws costs its own
    /// line and nothing more. Main-thread only.
    /// </summary>
    public static class GraphDump
    {
        /// <summary>A screen big enough to hit this is a screen whose dump nobody can read anyway.</summary>
        public const int MaxLines = 800;

        private static readonly GraphDir[] Dirs = { GraphDir.Up, GraphDir.Down, GraphDir.Left, GraphDir.Right };

        /// <summary>Whether the focused screen is one this dump reads - what <c>/gui/tree</c> asks
        /// before choosing between the two dumps.</summary>
        public static bool Fits(ScreenManager screens)
        {
            return screens != null && screens.CurrentScreen is GraphScreen;
        }

        public static string Dump(ScreenManager screens, bool buffers, bool flat, bool edges)
        {
            Sink sink = new Sink();
            Screen top = screens == null ? null : screens.CurrentScreen;
            sink.Line(WidgetDump.Header(screens));
            GraphScreen screen = top as GraphScreen;
            if (screen == null)
            {
                sink.Line(top == null
                    ? "(no screen is focused)"
                    : "(" + top.GetType().Name + " is a widget screen: read it with /gui/widgets)");
                return sink.ToString();
            }

            GraphNavigator navigator = screen.Navigator;
            GraphRender render = null;
            try
            {
                render = navigator == null || !ReferenceEquals(navigator.Screen, screen)
                    ? null
                    : navigator.InspectRender();
            }
            catch (Exception e)
            {
                sink.Line("<err: building the screen threw: " + e.Message + ">");
            }

            if (render == null || render.Nodes.Count == 0)
            {
                sink.Line("(no controls declared - the screen has nothing on it yet)");
                return sink.ToString();
            }

            if (flat)
            {
                WriteFlat(sink, render, buffers);
            }
            else
            {
                WriteTree(sink, render, navigator.FocusedKey, buffers, edges);
            }

            return sink.ToString();
        }

        // Tree mode: one line per node in navigation order, indented by its depth in the parent
        // chain, reading what arriving on it from the line above would say; stop boundaries marked.
        private static void WriteTree(Sink sink, GraphRender render, ControlId focused, bool buffers, bool edges)
        {
            object stop = null;
            bool first = true;
            GraphNode previous = null;
            foreach (ControlId key in Order(render))
            {
                GraphNode node = render.NodeAt(key);
                if (node == null)
                {
                    continue;
                }

                if (first || !Equals(stop, node.StopKey))
                {
                    sink.Line("-- stop: " + Describe(node.StopKey));
                    stop = node.StopKey;
                }

                first = false;
                string indent = Indent(Depth(node));
                bool here = focused != null && focused.Equals(node.Id);
                string line = indent
                    + (here ? "[*] " : "[ ] ")
                    + TypeName(node)
                    + " #" + Describe(node.Id.StructuralKey)
                    + " \"" + Text(previous, node) + "\"";
                if (node.Expandable && !node.Expanded)
                {
                    line += " (collapsed)";
                }

                if (!sink.Line(line))
                {
                    return;
                }

                previous = node;
                if (edges && !WriteEdges(sink, render, node, indent))
                {
                    return;
                }

                if (buffers)
                {
                    foreach (string bufferLine in Buffer(node))
                    {
                        if (!sink.Line(indent + "    buffer: " + bufferLine))
                        {
                            return;
                        }
                    }
                }

                string actions = ActionLabels(node);
                if (actions.Length > 0 && !sink.Line(indent + "    actions: " + actions))
                {
                    return;
                }
            }
        }

        /// <summary>One line per node, four columns, nothing positional - the shape a walk of two
        /// implementations can be sorted and diffed in: label, status words, buffer lines, actions.</summary>
        private static void WriteFlat(Sink sink, GraphRender render, bool buffers)
        {
            foreach (ControlId key in Order(render))
            {
                GraphNode node = render.NodeAt(key);
                if (node == null)
                {
                    continue;
                }

                string line = Label(node)
                    + " | " + Status(node)
                    + " | " + (buffers ? string.Join(" / ", Buffer(node).ToArray()) : string.Empty)
                    + " | " + ActionLabels(node);
                if (!sink.Line(line))
                {
                    return;
                }
            }
        }

        // The traversal order the navigator computes for itself.
        private static List<ControlId> Order(GraphRender render)
        {
            try
            {
                return KeyGraph.ComputeOrder(render);
            }
            catch (Exception)
            {
                List<ControlId> declared = new List<ControlId>();
                foreach (GraphNode node in render.Order)
                {
                    declared.Add(node.Id);
                }

                return declared;
            }
        }

        private static string Text(GraphNode from, GraphNode node)
        {
            try
            {
                string text = GraphAnnouncer.Compose(from, node);
                return string.IsNullOrEmpty(text) ? "(says nothing)" : text;
            }
            catch (Exception e)
            {
                return "<err: " + e.Message + ">";
            }
        }

        private static string Label(GraphNode node)
        {
            try
            {
                return GraphAnnouncer.FirstPartText(node) ?? string.Empty;
            }
            catch (Exception e)
            {
                return "<err: " + e.Message + ">";
            }
        }

        // The state words the widget dump's status column carries: every effective part that is
        // neither the name, the role word, the tooltip nor the position - value, selection, enabled.
        private static string Status(GraphNode node)
        {
            List<string> words = new List<string>();
            try
            {
                IList<NodeAnnouncement> declared = node.Vtable.Announcements;
                NodeAnnouncement head = declared != null && declared.Count > 0 ? declared[0] : null;
                List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
                for (int i = 0; i < parts.Count; i++)
                {
                    NodeAnnouncement part = parts[i];
                    if (part == null
                        || ReferenceEquals(part, head)
                        || part.Kind == AnnouncementKinds.Role
                        || part.Kind == AnnouncementKinds.Tooltip
                        || part.Kind == AnnouncementKinds.Position
                        || part.Kind == AnnouncementKinds.Hint
                        || part.Text == null)
                    {
                        continue;
                    }

                    string text = part.Text();
                    if (!string.IsNullOrEmpty(text))
                    {
                        words.Add(text);
                    }
                }
            }
            catch (Exception e)
            {
                words.Add("<err: " + e.Message + ">");
            }

            return string.Join(" ", words.ToArray());
        }

        private static List<string> Buffer(GraphNode node)
        {
            try
            {
                return GraphNavigator.BufferLines(node);
            }
            catch (Exception e)
            {
                return new List<string> { "<err: " + e.Message + ">" };
            }
        }

        private static string ActionLabels(GraphNode node)
        {
            Tooltip tooltip;
            try
            {
                Func<object> points = node.Vtable.PointsAt;
                tooltip = points == null ? null : points() as Tooltip;
            }
            catch (Exception)
            {
                return string.Empty;
            }

            if (tooltip == null || tooltip.Actions == null)
            {
                return string.Empty;
            }

            List<string> labels = new List<string>(tooltip.Actions.Count);
            for (int i = 0; i < tooltip.Actions.Count; i++)
            {
                TooltipAction action = tooltip.Actions[i];
                if (action != null && !string.IsNullOrWhiteSpace(action.Label))
                {
                    labels.Add(action.Label);
                }
            }

            return string.Join(", ", labels.ToArray());
        }

        /// <summary>Where each arrow goes from here, resolved the way the navigator resolves it: a
        /// wired edge to the node it names, and where there is none, the behavior left/right FALL
        /// BACK to - a value to adjust, a group to expand, a level to ascend out of.</summary>
        private static bool WriteEdges(Sink sink, GraphRender render, GraphNode node, string indent)
        {
            for (int i = 0; i < Dirs.Length; i++)
            {
                string line;
                try
                {
                    line = Edge(render, node, Dirs[i]);
                }
                catch (Exception e)
                {
                    line = "<err: " + e.Message + ">";
                }

                if (line != null && !sink.Line(indent + "    " + Word(Dirs[i]) + " -> " + line))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Edge(GraphRender render, GraphNode node, GraphDir dir)
        {
            Transition wired;
            node.Transitions.TryGetValue(dir, out wired);
            GraphNode destination = wired == null ? null : render.NodeAt(wired.Destination);
            bool horizontal = dir == GraphDir.Left || dir == GraphDir.Right;

            if (horizontal && node.Vtable.OnAdjust != null)
            {
                string adjust = dir == GraphDir.Right ? "adjust value up" : "adjust value down";
                return destination == null ? adjust : adjust + " (the edge to " + Quoted(destination) + " is shadowed)";
            }

            if (destination != null)
            {
                string crossing = string.IsNullOrEmpty(wired.Label) ? string.Empty : " (crossing: " + wired.Label + ")";
                return Quoted(destination) + crossing;
            }

            if (!horizontal || !KeyGraph.InTree(node))
            {
                return null;
            }

            if (dir == GraphDir.Right)
            {
                if (node.Expandable)
                {
                    if (!node.Expanded)
                    {
                        return "expand";
                    }

                    GraphNode child = FirstChild(render, node);
                    return child == null ? "nothing to descend into" : "descend to " + Quoted(child);
                }

                return "nothing to descend into";
            }

            if (node.Expandable && node.Expanded)
            {
                return "collapse";
            }

            GraphNode ancestor = Ancestor(render, node);
            return ancestor == null ? null : "ascend to " + Quoted(ancestor);
        }

        private static GraphNode FirstChild(GraphRender render, GraphNode group)
        {
            foreach (GraphNode node in render.Order)
            {
                if (ReferenceEquals(node.Parent, group))
                {
                    return node;
                }
            }

            return null;
        }

        private static GraphNode Ancestor(GraphRender render, GraphNode node)
        {
            for (GraphNode parent = node.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.Focusable && render.Nodes.ContainsKey(parent.Id))
                {
                    return render.NodeAt(parent.Id);
                }
            }

            return null;
        }

        private static int Depth(GraphNode node)
        {
            int depth = 0;
            for (GraphNode parent = node.Parent; parent != null; parent = parent.Parent)
            {
                depth++;
            }

            return depth;
        }

        private static string TypeName(GraphNode node)
        {
            ControlType type = node.Vtable == null ? null : node.Vtable.ControlType;
            return type == null || string.IsNullOrEmpty(type.Key) ? "node" : type.Key;
        }

        private static string Quoted(GraphNode node)
        {
            string label = Label(node);
            return "\"" + (label.Length > 0 ? label : Describe(node.Id.StructuralKey)) + "\"";
        }

        private static string Describe(object key)
        {
            return key == null ? "none" : key.ToString();
        }

        private static string Word(GraphDir dir)
        {
            switch (dir)
            {
                case GraphDir.Up:
                    return "up";
                case GraphDir.Down:
                    return "down";
                case GraphDir.Left:
                    return "left";
                default:
                    return "right";
            }
        }

        private static string Indent(int depth)
        {
            return new string(' ', depth * 2);
        }

        // Counts lines, because the cap exists to keep the answer readable and a reader counts in
        // lines. Line() returns false once full, so callers stop walking.
        private sealed class Sink
        {
            private readonly StringBuilder _text = new StringBuilder();
            private int _lines;
            private bool _full;

            public bool Line(string line)
            {
                if (_lines >= MaxLines)
                {
                    _full = true;
                    return false;
                }

                if (_lines > 0)
                {
                    _text.Append('\n');
                }

                _text.Append(line);
                _lines++;
                return true;
            }

            public override string ToString()
            {
                return _full ? _text + "\n... (truncated at " + MaxLines + " lines)" : _text.ToString();
            }
        }
    }
}
