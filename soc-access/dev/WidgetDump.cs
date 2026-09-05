using System;
using System.Collections.Generic;
using System.Text;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// The accessible tree as text: every widget the top screen offers, in walk order, each read the
    /// way arriving on it would sound.
    ///
    /// It answers the question a screenshot cannot - what a blind player can reach here, and what it
    /// says - and it answers it without moving anything. Nothing in this file focuses, ensures focus,
    /// or requests focus; the focus marker is read off <see cref="UIManager.CurrentWidget"/>. Two
    /// dumps of a settled screen are byte-identical, which is what makes the migration diff of this
    /// dump against the UI rewrite's meaningful.
    ///
    /// Main thread only: all of it reads live widget state.
    /// </summary>
    internal static class WidgetDump
    {
        /// <summary>Widgets that present many positions through one object - a map, a hex grid, an
        /// inventory. There is nothing below them to walk, so each gets one placeholder line naming
        /// where its cursor is standing rather than a subtree.</summary>
        private static readonly HashSet<Type> MultiPositionTypes = new HashSet<Type>
        {
            typeof(InventoryGridWidget),
            typeof(ArmyExchangeGridWidget),
            typeof(AdventureMapGrid),
            typeof(CombatHexGrid),
            typeof(TroopPlacementHexGrid),
            typeof(AnnouncementOrderMenuWidget),
            typeof(CodexContentWidget),
        };

        public static string Dump(ScreenManager screens, bool buffers, bool flat)
        {
            StringBuilder text = new StringBuilder();
            Screen top = screens == null ? null : screens.CurrentScreen;
            if (top == null)
            {
                text.Append("screen: none | stack: (empty)");
                return text.ToString();
            }

            text.Append("screen: ").Append(top.GetType().Name).Append(" | stack: ");
            IReadOnlyList<Screen> stack = screens.Stack;
            for (int i = 0; i < stack.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(" > ");
                }

                text.Append(stack[i].GetType().Name);
            }

            List<Visit> visits = new List<Visit>();
            Walk(top.RootWidget, 0, visits);

            if (flat)
            {
                WriteFlat(text, visits, buffers);
            }
            else
            {
                WriteTree(text, visits, buffers);
            }

            return text.ToString();
        }

        /// <summary>One printed widget and how deep it sits. Collected before anything is written so
        /// the focus marker can fall back to the focused ancestor when the focused widget itself is
        /// not in the tree - a table's cell is built on demand and is never the same object twice.
        /// </summary>
        private sealed class Visit
        {
            public Widget Widget;
            public int Depth;
        }

        private static void Walk(Widget widget, int depth, List<Visit> visits)
        {
            if (widget == null || !widget.IsVisible)
            {
                return;
            }

            visits.Add(new Visit { Widget = widget, Depth = depth });
            if (IsMultiPosition(widget))
            {
                return;
            }

            foreach (Widget child in widget.EnumerateChildren())
            {
                Walk(child, depth + 1, visits);
            }
        }

        private static void WriteTree(StringBuilder text, List<Visit> visits, bool buffers)
        {
            Widget focused = FindFocused(visits);
            // A context of its own, fed every printed widget in order: each line then carries the
            // ancestor labels that changed since the line above it, exactly as walking would.
            FocusContext context = new FocusContext();

            for (int i = 0; i < visits.Count; i++)
            {
                Widget widget = visits[i].Widget;
                string indent = Indent(visits[i].Depth);
                text.Append('\n').Append(indent);
                text.Append(ReferenceEquals(widget, focused) ? "[*] " : "[ ] ");
                text.Append(widget.GetType().Name).Append(" #").Append(widget.Id);

                if (IsMultiPosition(widget))
                {
                    text.Append(" (multi-position) current=\"")
                        .Append(widget.GetFocusMessage())
                        .Append("\" key=")
                        .Append(widget.GetAnnouncementKey());
                    continue;
                }

                text.Append(" \"").Append(UIManager.BuildAnnouncement(widget, context)).Append('"');
                if (IsContainer(widget) && !HasVisibleChild(widget))
                {
                    text.Append(" (empty)");
                }

                if (buffers)
                {
                    List<string> lines = UIManager.BuildReviewLines(widget);
                    for (int line = 0; line < lines.Count; line++)
                    {
                        text.Append('\n').Append(indent).Append("    buffer: ").Append(lines[line]);
                    }
                }

                string actions = ActionLabels(widget);
                if (actions.Length > 0)
                {
                    text.Append('\n').Append(indent).Append("    actions: ").Append(actions);
                }
            }
        }

        /// <summary>One line per leaf, four columns, nothing positional - the shape a walk of two
        /// implementations can be sorted and diffed in.</summary>
        private static void WriteFlat(StringBuilder text, List<Visit> visits, bool buffers)
        {
            for (int i = 0; i < visits.Count; i++)
            {
                Widget widget = visits[i].Widget;
                if (IsMultiPosition(widget))
                {
                    text.Append('\n')
                        .Append(widget.GetType().Name)
                        .Append(" (multi-position) | ")
                        .Append(widget.GetFocusMessage());
                    continue;
                }

                if (IsContainer(widget) || HasVisibleChild(widget))
                {
                    continue;
                }

                text.Append('\n')
                    .Append(widget.GetLabel())
                    .Append(" | ")
                    .Append(widget.GetStatus())
                    .Append(" | ")
                    .Append(buffers ? string.Join(" / ", UIManager.BuildReviewLines(widget).ToArray()) : string.Empty)
                    .Append(" | ")
                    .Append(ActionLabels(widget));
            }
        }

        /// <summary>The widget the marker belongs on: the one the UI says is current, or - when that
        /// is an object the walk cannot produce - the deepest printed ancestor still holding focus.
        /// Read only; nothing here focuses anything.</summary>
        private static Widget FindFocused(List<Visit> visits)
        {
            Widget current = UIManager.CurrentWidget;
            for (int i = 0; i < visits.Count; i++)
            {
                if (ReferenceEquals(visits[i].Widget, current))
                {
                    return current;
                }
            }

            Widget fallback = null;
            for (int i = 0; i < visits.Count; i++)
            {
                if (visits[i].Widget.IsFocused)
                {
                    fallback = visits[i].Widget;
                }
            }

            return fallback;
        }

        private static string ActionLabels(Widget widget)
        {
            Tooltip tooltip = widget.GetTooltip();
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

        private static bool IsMultiPosition(Widget widget)
        {
            return MultiPositionTypes.Contains(widget.GetType());
        }

        private static bool IsContainer(Widget widget)
        {
            return widget is ContainerWidget || widget is MenuWidget || widget is TableWidget;
        }

        private static bool HasVisibleChild(Widget widget)
        {
            foreach (Widget child in widget.EnumerateChildren())
            {
                if (child != null && child.IsVisible)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Indent(int depth)
        {
            return new string(' ', depth * 2);
        }
    }
}
