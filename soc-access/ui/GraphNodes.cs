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
        /// becomes available under the cursor says so. The word is "unavailable", the state word
        /// Endless Space 2 Access uses (owner ruling 2026-09-06: the graph screens take ES2's role
        /// and state words exactly), so the widget screens' "disabled" stands only on the screens
        /// this migration has not reached yet.</summary>
        public static NodeAnnouncement DisabledPart(Func<bool> enabled)
        {
            return new NodeAnnouncement(
                () => enabled == null || enabled() ? null : ModText.Get(ModStrings.UI.StatusUnavailable),
                live: true,
                kind: AnnouncementKinds.Enabled);
        }

        /// <summary>Which one of a set of alternatives is in force. Only the chosen one says anything,
        /// which is the silence a tab bar keeps and is also what lets focus entering the group land on
        /// the alternative already chosen rather than at the top of the list.</summary>
        public static NodeAnnouncement SelectedPart(Func<bool> selected)
        {
            return new NodeAnnouncement(
                () => selected != null && selected() ? ModText.Get(ModStrings.UI.Selected) : null,
                live: true,
                kind: AnnouncementKinds.Selected);
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
                () => SpokenLines.Of(it.TextLines),
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
            Add(ref list, NodeSection.Buffer(details == null ? null : (Func<IList<string>>)(() => SpokenLines.Of(details()))));
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
        /// Free text the player types into the game's own editor. Activating it is the request for
        /// the keyboard; the handover itself, the words on the way in and out and the echo of what is
        /// typed all belong to <see cref="GameTextEditor"/>.
        ///
        /// <paramref name="value"/> reports null while the game holds the keyboard: the mod is
        /// already speaking the keys as they land, and re-reading the whole field on top of them
        /// buries them. It is NOT watched live for the same reason - the only thing a watch could
        /// ever catch is the field's text reappearing as the edit ends, which the editor has just
        /// said itself ("edited", then the text), or, on a cancel, the text the player already knows
        /// is back (measured on the join-game popup: the watch read "ABCDE" straight after
        /// "Cancelled", 2026-09-06).
        /// </summary>
        public static NodeVtable EditField(
            Func<string> label,
            Func<string> value,
            Action edit,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null)
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(ValuePart(value, watch: false));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.EditField,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                OnActivate = Guarded(edit, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// A setting the player turns on and off. Its state is both announced live - so a box the game
        /// ticks on the player's behalf says so - and spoken immediately after a toggle, which is what
        /// makes holding the key down readable.
        ///
        /// <paramref name="value"/> is a number the box itself DRAWS beside its tick, and reads before
        /// the state, in the order the box is read on screen.
        ///
        /// A box that is REFUSING says nothing at all: see <see cref="ActedState"/>.
        /// </summary>
        public static NodeVtable Checkbox(
            Func<string> label,
            Func<bool> state,
            Action toggle,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null,
            Func<string> value = null)
        {
            Func<string> stateText = () => ModText.Get(
                state != null && state() ? ModStrings.UI.StatusChecked : ModStrings.UI.StatusNotChecked);

            List<NodeAnnouncement> parts = Parts(label, enabled);
            if (value != null)
            {
                parts.Add(ValuePart(value));
            }

            parts.Add(ValuePart(stateText));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Checkbox,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(stateText, enabled),
                OnActivate = Guarded(toggle, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>A value the player moves along a range with Left and Right, and by a coarse step
        /// with the same arrows held with Shift. <paramref name="valueText"/> is already in the form
        /// the player should hear it - a percentage, a count, a number of seconds - because only the
        /// screen knows what the number means.</summary>
        public static NodeVtable Slider(
            Func<string> label,
            Func<string> valueText,
            Action<int, bool> adjust,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null)
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(ValuePart(valueText));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(valueText, enabled),
                // Declared even while the slider is refusing, so Left and Right stay the slider's keys
                // rather than quietly turning back into navigation on a control that looks exactly like
                // the one beside it.
                OnAdjust = (sign, large) =>
                {
                    if (enabled != null && !enabled())
                    {
                        return;
                    }

                    if (adjust != null)
                    {
                        adjust(sign, large);
                    }
                },
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// One of a set where exactly one is in force and picking is not yet doing - the game's own
        /// select-then-confirm model.
        ///
        /// Only the chosen one says so, which is the silence a tab bar keeps and is what lets focus
        /// entering the group land on the choice already made rather than at the top of the list.
        /// Activating says "selected" at once, interrupting: unlike a checkbox there is no other
        /// state the keypress could have produced, and the player needs to hear that it took.
        /// </summary>
        public static NodeVtable Radio(
            Func<string> label,
            Func<bool> selected,
            Action choose,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null)
        {
            Func<string> chosen = () => selected != null && selected() ? ModText.Get(ModStrings.UI.Selected) : null;
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Insert(1, SelectedPart(selected));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(chosen, enabled),
                OnActivate = Guarded(choose, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>A setting chosen from a list the control opens. Activating it is the screen's
        /// business - what the list is and how it is navigated belongs to whoever declared it.</summary>
        public static NodeVtable ComboBox(
            Func<string> label,
            Func<string> valueText,
            Action open,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null)
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(ValuePart(valueText));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(valueText, enabled),
                OnActivate = Guarded(open, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>One page of a screen. Only the showing tab says it is selected, and saying nothing
        /// is how the rest stay quiet - which is also what lets focus entering the tab bar land on the
        /// page the player is actually looking at rather than on the first tab.
        ///
        /// How a tab is switched to is the screen's business: set <c>OnActivate</c> on the returned
        /// vtable where the game needs a click, leave it unset for a bar that changes page on focus.
        /// </summary>
        public static NodeVtable Tab(
            Func<string> label,
            Func<bool> selected,
            Func<bool> enabled = null,
            Tooltip tooltip = null,
            Func<IList<string>> details = null)
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(SelectedPart(selected));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = parts,
                Sections = Sections(details, tooltip),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>One entry of a list the player has opened to pick from. It carries no role word:
        /// the control that opened the list has just been read as the combo box it is, and repeating
        /// "list item" on every entry of a twenty-line list only slows the reading down. The entry the
        /// list is currently set to says so, which is also how focus lands on it.</summary>
        public static NodeVtable Choice(
            Func<string> label,
            Func<bool> selected,
            Action choose,
            Func<bool> enabled = null,
            Func<IList<string>> details = null,
            Tooltip tooltip = null)
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Insert(1, SelectedPart(selected));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = parts,
                Sections = Sections(details, tooltip),
                OnActivate = Guarded(choose, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// Keep a control's tooltip in the review buffer but stop the game's own tooltip being DRAWN
        /// for it.
        ///
        /// Drawing a tooltip means <c>NativeTooltipUtility.ShowTooltipForComponent</c>, which SELECTS
        /// the component the tooltip hangs on - for a text row, the row's own label - and that
        /// selection takes the keyboard straight back off the field the player just asked to type in.
        /// Measured 2026-09-06 on the lobby's game settings: with the tooltip aimed, the field never
        /// reported focus and the edit ended in silence; with it cleared, the same keys answered
        /// "standing down" and the edit ended properly. Only an EDIT control needs this; nothing else
        /// competes with the game for the keyboard.
        /// </summary>
        public static void DoNotDrawTooltip(NodeVtable vtable)
        {
            if (vtable != null)
            {
                vtable.PointsAt = null;
            }
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

        // The other half of the swallow: what the control reports right after the player acted on it,
        // which for a refused action is nothing at all. Re-reading the state after a keypress that
        // changed nothing is heard as the keypress having worked ("not checked" from a box that would
        // not untick), and a refusal word here would be the second "unavailable" in a row - the player
        // heard the first on focus.
        private static Func<string> ActedState(Func<string> state, Func<bool> enabled)
        {
            if (state == null || enabled == null)
            {
                return state;
            }

            return () => enabled() ? state() : null;
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
