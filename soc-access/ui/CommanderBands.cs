using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The two bands a wielder's overview is made of, wherever the game draws one - the wielder sheet
    /// and the trade both hang the same <c>CommanderStatsInfo</c> and the same
    /// <c>CommanderSheetModifierTabNavigation</c> over a commander. A CONTRIBUTOR rather than a
    /// screen, as <see cref="TroopHudRows"/> and <see cref="ArtifactSlotNodes"/> are.
    ///
    /// A BAND is a run of read-only lines under one of the game's own captions: the caption is the
    /// REGION its lines belong to rather than a row of its own, because there is nothing there to
    /// operate. The lines are not drawn as controls, so each is keyed on a marker its caller keeps
    /// across rebuilds.
    ///
    /// THE TABS ARE ONE BAR: Left and Right walk it, Enter switches. Arriving must not switch -
    /// switching redraws the list under the bar, and Up from the first line of that list lands here.
    /// Their focus visual is the game's own button selection.
    /// </summary>
    public static class CommanderBands
    {
        /// <summary>One read-only line of a band: what the game calls it, what it currently says, and
        /// the breakdown it draws on hover.</summary>
        public sealed class Line
        {
            public Line(string label, string value = null, Tooltip tooltip = null, Action onFocus = null)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Tooltip = tooltip;
                OnFocus = onFocus;
            }

            public string Label { get; private set; }
            public string Value { get; private set; }
            public Tooltip Tooltip { get; private set; }
            public Action OnFocus { get; private set; }
        }

        /// <summary>One tab of the modifier bar, as the game draws it.</summary>
        public sealed class TabItem
        {
            public TabItem(string label, int index, Component button, Tooltip tooltip = null)
            {
                Label = label ?? string.Empty;
                Index = index;
                Button = button;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public int Index { get; private set; }
            public Component Button { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

        /// <summary>A band of lines under the game's own caption, declared into the stop the caller has
        /// opened. <paramref name="marker"/> answers with the caller's own stable subject for a
        /// synthesized node, keyed by the string handed to it.</summary>
        public static void Band(
            GraphBuilder builder,
            string keyPrefix,
            string key,
            string caption,
            IReadOnlyList<Line> lines,
            Func<string, object> marker)
        {
            if (builder == null || lines == null || lines.Count == 0)
            {
                return;
            }

            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(keyPrefix + ":" + key);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                Line it = lines[i];
                NodeVtable vtable = GraphNodes.Text(() => it.Label, null, it.Tooltip);
                if (!string.IsNullOrWhiteSpace(it.Value))
                {
                    vtable.Announcements.Add(GraphNodes.ValuePart(() => it.Value));
                }

                if (it.OnFocus != null)
                {
                    vtable.OnFocusVisual = () => it.OnFocus();
                }

                builder.AddItem(new SyntheticNode(
                    ControlId.For(marker(key + "/" + i), keyPrefix + ":" + key + "/" + i),
                    vtable));
            }

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>The modifier tabs as the ONE BAR the game draws them as. Only tabs the game drew a
        /// button for are declared: the bar is the buttons.</summary>
        public static void Tabs(
            GraphBuilder builder,
            string keyPrefix,
            IReadOnlyList<TabItem> tabs,
            Func<int> selectedIndex,
            Action<int> activate,
            Action<int> select)
        {
            if (builder == null || tabs == null)
            {
                return;
            }

            List<TabItem> drawn = new List<TabItem>();
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i] != null && tabs[i].Button != null)
                {
                    drawn.Add(tabs[i]);
                }
            }

            if (drawn.Count == 0)
            {
                return;
            }

            builder.StartRow(keyPrefix + ":modifier-tabs");
            for (int i = 0; i < drawn.Count; i++)
            {
                TabItem it = drawn[i];
                NodeVtable vtable = GraphNodes.Tab(
                    () => it.Label,
                    () => selectedIndex() == it.Index,
                    null,
                    it.Tooltip);
                if (activate != null)
                {
                    vtable.OnActivate = () => activate(it.Index);
                }

                if (select != null)
                {
                    vtable.OnFocusVisual = () => select(it.Index);
                }

                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Button, keyPrefix + ":modifier-tab/" + it.Index),
                    vtable,
                    it.Button));
            }

            builder.EndRow();
        }
    }
}
