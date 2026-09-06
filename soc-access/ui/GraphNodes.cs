using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// Factories for the control descriptions screens hand to the graph builder. A screen says what
    /// a control is and how to work it; everything about how that reads aloud lives here, so two
    /// screens with a button announce it identically.
    ///
    /// Every piece of text is a delegate, resolved at speak time, never a captured string: a graph is
    /// rebuilt from live game state on every operation, and a control that cached its label would go
    /// on announcing the state the game was in when the screen was first built.
    ///
    /// Every factory takes the same cross-cutting parameters (enabled, tooltip, details), even where
    /// a kind rarely uses one: a factory whose signature omits a concern loses it screen by screen.
    /// </summary>
    public static class GraphNodes
    {
        /// <summary>The control's name - always the first part, so the path diff can tell when a
        /// container's label merely repeats the control inside it.</summary>
        public static NodeAnnouncement LabelPart(Func<string> label)
        {
            return new NodeAnnouncement(label, kind: AnnouncementKinds.Label);
        }

        /// <summary>Speaks only while the control is unavailable, and watched live so a control that
        /// becomes available under the cursor says so. The wording is the one every widget screen
        /// still uses, so a control reads the same whichever engine declares it.</summary>
        public static NodeAnnouncement DisabledPart(Func<bool> enabled)
        {
            return new NodeAnnouncement(
                () => enabled == null || enabled() ? null : ModText.Get(ModStrings.UI.StatusDisabled),
                live: true,
                kind: AnnouncementKinds.Enabled);
        }

        /// <summary>What the control currently holds, watched live by default so a value the game
        /// changes on its own speaks under the cursor.</summary>
        public static NodeAnnouncement ValuePart(Func<string> value, bool watch = true)
        {
            return new NodeAnnouncement(value, live: watch, kind: AnnouncementKinds.Value);
        }

        /// <summary>
        /// A control's tooltip as a declared SECTION - the single place it is written down, from which
        /// the engine derives what the review buffer holds and what the focus readout says about it.
        ///
        /// This mod's ruling on tooltips is buffer-only: the readout says nothing about them and the
        /// player reads them from the UI buffer, so every native tooltip is an
        /// <see cref="TooltipMode.Indicate"/> section, whatever its length. The section still marks
        /// the node as pointing at a tooltip, which is what makes focus draw the game's own tooltip
        /// window for it. Null when there is no tooltip.
        /// </summary>
        public static NodeSection TooltipSection(Tooltip tooltip)
        {
            if (tooltip == null)
            {
                return null;
            }

            Tooltip it = tooltip;
            return NodeSection.Derived(
                () => new List<string>(it.TextLines),
                TooltipMode.Indicate,
                () => it.VisualMetadata != null,
                it);
        }

        /// <summary>
        /// The declared sections of a control, in the order they read: what the control DRAWS beyond
        /// its readout first, then its tooltip, then the structured actions the tooltip offers (named
        /// so the player knows the tooltip actions menu has commands). Null when there is none of
        /// them, which is a complete declaration - the buffer still has the control's own readout.
        /// </summary>
        public static IList<NodeSection> Sections(Func<IList<string>> details, Tooltip tooltip)
        {
            List<NodeSection> list = null;
            Add(ref list, NodeSection.Buffer(details));
            Add(ref list, TooltipSection(tooltip));
            if (tooltip != null && tooltip.Actions != null && tooltip.Actions.Count > 0)
            {
                Tooltip it = tooltip;
                Add(ref list, NodeSection.Buffer(() => ActionLines(it)));
            }

            return list;
        }

        /// <summary>The buffer line naming a tooltip's structured actions, as the widget engine
        /// writes it today.</summary>
        public static IList<string> ActionLines(Tooltip tooltip)
        {
            List<string> labels = new List<string>();
            IReadOnlyList<TooltipAction> actions = tooltip != null ? tooltip.Actions : null;
            for (int i = 0; actions != null && i < actions.Count; i++)
            {
                TooltipAction action = actions[i];
                if (action != null && !string.IsNullOrWhiteSpace(action.Label))
                {
                    labels.Add(action.Label);
                }
            }

            List<string> lines = new List<string>(1);
            if (labels.Count > 0)
            {
                lines.Add(ModText.Get(ModStrings.UI.AvailableActions, ModText.JoinList(labels)));
            }

            return lines;
        }

        /// <summary>A control the player activates. An unavailable one stays focusable and readable
        /// and simply swallows the activation.</summary>
        public static NodeVtable Button(
            Func<string> label,
            Action activate,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null)
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = Parts(label, enabled),
                Sections = Sections(details, tooltip),
                OnActivate = Guarded(activate, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>A container the player expands and collapses. Declare it with the builder's
        /// BeginGroup, which stamps the expanded state and parents the children onto it. A group
        /// that is also a button (opening it is what clicking it does) takes an activation too.</summary>
        public static NodeVtable Group(
            Func<string> label,
            Action activate = null,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null)
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Group,
                Announcements = Parts(label, enabled),
                Sections = Sections(details, tooltip),
                OnActivate = activate == null ? null : Guarded(activate, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>A line the player reads but does not work. No role word: there is no control
        /// here to name.</summary>
        public static NodeVtable Text(
            Func<string> label,
            Func<IList<string>> details = null,
            Tooltip tooltip = null)
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { LabelPart(label) },
                Sections = Sections(details, tooltip),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// THE RAISING HALF: what the navigator draws the game's own tooltip for when focus lands on
        /// this node. Written down beside the content (<see cref="NodeVtable.PointsAt"/>) rather than
        /// hidden in a closure, so the tooltip actions menu and the dev dump can ask the node which
        /// tooltip it points at. Every factory goes through here.
        /// </summary>
        public static void Aim(NodeVtable vtable, Tooltip tooltip)
        {
            if (vtable == null || tooltip == null)
            {
                return;
            }

            Tooltip it = tooltip;
            vtable.PointsAt = () => it;
        }

        // The swallow every unavailable control shares: it stays focusable and readable, and the
        // action goes nowhere.
        private static Action Guarded(Action action, Func<bool> enabled)
        {
            return () =>
            {
                if (enabled != null && !enabled())
                {
                    return;
                }

                if (action != null)
                {
                    action();
                }
            };
        }

        // The readout every control here is built from: what it is called and whether it is refusing.
        private static List<NodeAnnouncement> Parts(Func<string> label, Func<bool> enabled)
        {
            return new List<NodeAnnouncement> { LabelPart(label), DisabledPart(enabled) };
        }

        private static void Add(ref List<NodeSection> list, NodeSection section)
        {
            if (section == null)
            {
                return;
            }

            if (list == null)
            {
                list = new List<NodeSection>(3);
            }

            list.Add(section);
        }
    }
}
