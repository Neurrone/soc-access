using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// THE ROWS OF A SETTINGS FORM, AS GRAPH NODES.
    ///
    /// The game draws every settings form the same way, so every settings form reads the same way:
    /// a caption is the REGION its rows belong to rather than a row of its own, toggles are
    /// checkboxes, sliders take Left and Right with the drawn value spoken, dropdowns are combo
    /// boxes opening the game's own list, and buttons are buttons. It was written for the Options
    /// window and is shared with the mod's own options dialog, which is drawn with the same factory
    /// out of a copy of the same panel: one description of a form, so the two cannot drift.
    ///
    /// A caption that heads nothing stays a read-only row - measured on the Options window's Controls
    /// page, where the key binding rows are drawn by <c>AddKeyBinding</c>, which the reader does not
    /// see, so the categories there head nothing at all and would otherwise be lost.
    /// </summary>
    public sealed class MenuFormNodes
    {
        /// <summary>How many fine steps one coarse slider step is worth.</summary>
        private const int CoarseSteps = 10;

        private readonly string _prefix;

        // A subject of its own per synthesized row, kept across rebuilds so the reconciler seats the
        // cursor on the same line: a caption that heads nothing, and the window's own close button.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public MenuFormNodes(string prefix)
        {
            _prefix = prefix;
        }

        public void BuildRows(GraphBuilder builder, IReadOnlyList<MenuRow> controls)
        {
            bool inCaption = false;
            for (int i = 0; i < controls.Count; i++)
            {
                MenuRow control = controls[i];
                object item = control != null ? control.Item : null;
                MenuRowText caption = item as MenuRowText;
                if (caption != null)
                {
                    if (inCaption)
                    {
                        builder.PopContext();
                        inCaption = false;
                    }

                    builder.SetRegion(null);
                    if (!caption.IsVisible() || string.IsNullOrWhiteSpace(caption.GetText()))
                    {
                        continue;
                    }

                    if (HasRowsUnder(controls, i))
                    {
                        builder.PushContext(caption.GetText());
                        builder.SetRegion(caption.Id);
                        inCaption = true;
                    }
                    else
                    {
                        builder.AddItem(new SyntheticNode(
                            ControlId.For(Marker(caption.Id), _prefix + ":caption/" + caption.Id),
                            GraphNodes.Text(caption.GetText)));
                    }

                    continue;
                }

                AddRow(builder, control);
            }

            if (inCaption)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>A button the window draws itself rather than a row of the form - the OK along the
        /// bottom. It has no component the reconciler can key on, so it gets a subject of its own.
        /// </summary>
        public void AddWindowButton(GraphBuilder builder, MenuRowButton button, Func<string> label)
        {
            if (button == null || !button.IsVisible())
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(button.Id), _prefix + ":" + button.Id),
                Button(button, label)));
        }

        /// <summary>Whether the caption at <paramref name="index"/> heads any rows: anything before the
        /// next caption that is not a caption itself.</summary>
        private static bool HasRowsUnder(IReadOnlyList<MenuRow> controls, int index)
        {
            for (int i = index + 1; i < controls.Count; i++)
            {
                object item = controls[i] != null ? controls[i].Item : null;
                if (item is MenuRowText)
                {
                    return false;
                }

                if (item != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddRow(GraphBuilder builder, MenuRow control)
        {
            object item = control != null ? control.Item : null;
            Component subject = control != null ? control.Transform : null;
            if (item == null || subject == null)
            {
                return;
            }

            MenuRowToggle toggle = item as MenuRowToggle;
            if (toggle != null)
            {
                if (!toggle.IsVisible())
                {
                    return;
                }

                NodeVtable vtable = GraphNodes.Checkbox(
                    toggle.GetLabel,
                    toggle.IsChecked,
                    toggle.Toggle,
                    toggle.IsEnabled,
                    toggle.GetTooltip());
                vtable.OnFocusVisual = toggle.Focus;
                builder.AddItem(new DrawnNode(ControlId.For(subject, _prefix + ":row/" + toggle.Id), vtable, subject));
                return;
            }

            MenuRowSlider slider = item as MenuRowSlider;
            if (slider != null)
            {
                if (slider.IsVisible())
                {
                    AddSlider(builder, slider, subject);
                }

                return;
            }

            MenuRowDropdown dropdown = item as MenuRowDropdown;
            if (dropdown != null)
            {
                if (!dropdown.IsVisible())
                {
                    return;
                }

                NodeVtable vtable = GraphNodes.ComboBox(
                    dropdown.GetLabel,
                    () => CurrentOption(dropdown),
                    () => DropListScreen.Open(dropdown, dropdown.GetLabel(), index => dropdown.SetValue(index)),
                    dropdown.IsEnabled,
                    dropdown.GetTooltip());
                vtable.OnFocusVisual = dropdown.Focus;
                builder.AddItem(new DrawnNode(ControlId.For(subject, _prefix + ":row/" + dropdown.Id), vtable, subject));
                return;
            }

            MenuRowButton button = item as MenuRowButton;
            if (button != null && button.IsVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, _prefix + ":row/" + button.Id),
                    Button(button, button.GetLabel),
                    subject));
            }
        }

        /// <summary>
        /// A slider row. Left and Right move the value; Enter opens the game's own "provide a number"
        /// popup through the value box the row draws beside the handle.
        ///
        /// The value box used to be a child node of its own, which put a "Please provide a number"
        /// button under every slider in the window and made the list of settings twice as long to
        /// walk. The box is one way of setting the same number the arrows set, so it is the row's
        /// activation instead; a row that draws no box has no activation at all.
        /// </summary>
        private void AddSlider(GraphBuilder builder, MenuRowSlider slider, Component subject)
        {
            string editorLabel = slider.GetValueEditorLabel != null ? slider.GetValueEditorLabel() : null;
            NodeVtable vtable = GraphNodes.Slider(
                slider.GetLabel,
                slider.GetValueText,
                (sign, large) => Adjust(slider, sign, large),
                slider.IsEnabled,
                slider.GetTooltip(),
                activate: string.IsNullOrWhiteSpace(editorLabel)
                    ? (Action)null
                    : () => slider.OpenValueEditor());
            vtable.OnFocusVisual = slider.Focus;
            builder.AddItem(new DrawnNode(ControlId.For(subject, _prefix + ":row/" + slider.Id), vtable, subject));
        }

        private static void Adjust(MenuRowSlider slider, int sign, bool large)
        {
            float step = slider.GetStep();
            if (step <= 0f)
            {
                step = 1f;
            }

            if (large)
            {
                step *= CoarseSteps;
            }

            slider.SetValue(slider.GetValue() + sign * step);
        }

        private static string CurrentOption(MenuRowDropdown dropdown)
        {
            IReadOnlyList<string> options = dropdown.GetOptions();
            int value = dropdown.GetValue();
            return options != null && value >= 0 && value < options.Count ? options[value] : string.Empty;
        }

        private static NodeVtable Button(MenuRowButton button, Func<string> label)
        {
            NodeVtable vtable = GraphNodes.Button(
                label,
                () => button.Activate(),
                button.IsEnabled,
                button.GetTooltip());
            vtable.OnFocusVisual = button.Focus;
            return vtable;
        }

        private object Marker(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers.Add(key, marker);
            }

            return marker;
        }
    }
}
