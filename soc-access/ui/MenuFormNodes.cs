using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
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
    /// A caption that heads nothing stays a read-only row, and a form that draws no captions at all
    /// (the lobby's two settings windows) asks for every text to be a row where it stands.
    ///
    /// The Options window's Controls page is the one form that draws rows this reader once could not
    /// see: the rebindable-action rows, drawn by <c>AddKeyBinding</c>. They are now read as a TABLE of
    /// their own (<see cref="BuildKeyBindingSheet"/>) - one row per action with a name cell, the
    /// binding chip and a "+" - so <see cref="BuildRows"/> stops where the table begins and the
    /// category captions there become the table's row-group regions rather than empty read-only rows.
    /// </summary>
    public sealed class MenuFormNodes
    {
        /// <summary>How many fine steps one coarse slider step is worth.</summary>
        private const int CoarseSteps = 10;

        private readonly string _prefix;
        private readonly string _rowKey;
        private readonly bool _captionsHeadRegions;

        // A subject of its own per synthesized row, kept across rebuilds so the reconciler seats the
        // cursor on the same line: a caption that heads nothing, and the window's own close button.
        private readonly MarkerTable _markers = new MarkerTable();

        public MenuFormNodes(string prefix)
            : this(prefix, null, true)
        {
        }

        /// <param name="rowKey">What a row's node key begins with, null for the "<c>prefix</c>:row/"
        /// the Options window and the mod's dialogs use. It is the form's identity in the tree, so a
        /// window that already had one keeps it.</param>
        /// <param name="captionsHeadRegions">Whether a text the form draws over rows opens a REGION
        /// they belong to. The lobby's settings windows draw no captions at all - every text they
        /// draw stands on its own line - so there it is false and a text is a read-only row where it
        /// stands.</param>
        public MenuFormNodes(string prefix, string rowKey, bool captionsHeadRegions)
        {
            _prefix = prefix;
            _rowKey = rowKey ?? prefix + ":row/";
            _captionsHeadRegions = captionsHeadRegions;
        }

        /// <summary>The editor behind this form's text boxes, so the screen can answer whether it is
        /// holding the game's keyboard.</summary>
        public GameTextEditor Editor
        {
            get { return _editor; }
        }

        private readonly GameTextEditor _editor = new GameTextEditor();

        // The key-binding rows' nodes, built once per ROW LIST rather than once per frame: the
        // reader hands back the same list until the column is redrawn, so its identity is the
        // "same rows" answer, and a row's nodes are rebuilt when its chip changes shape (a rebind
        // makes it a button, a clear a dead label). Composing 65 rows of vtables and closures every
        // frame was a third of the Controls page's build (2026-09-11).
        private IReadOnlyList<MenuRow> _keyBindingSource;
        private readonly Dictionary<MenuRowKeyBinding, KeyBindingNodes> _keyBindingNodes =
            new Dictionary<MenuRowKeyBinding, KeyBindingNodes>();

        private sealed class KeyBindingNodes
        {
            public bool Overridden;
            public NodeVtable Name;
            public List<GraphSheet.SheetCell> Cells;
        }

        public void BuildRows(GraphBuilder builder, IReadOnlyList<MenuRow> controls)
        {
            // The rebindable-action rows are a table of their own; stop where they begin so their
            // category captions are the sheet's regions and not empty read-only rows here.
            int end = FirstKeyBindingCaption(controls);
            if (end < 0)
            {
                end = controls.Count;
            }

            bool inCaption = false;
            for (int i = 0; i < end; i++)
            {
                MenuRow control = controls[i];
                object item = control != null ? control.Item : null;
                MenuRowText caption = item as MenuRowText;
                if (caption != null)
                {
                    if (!_captionsHeadRegions)
                    {
                        AddTextRow(builder, control, caption);
                        continue;
                    }

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
                            ControlId.For(_markers.For(caption.Id), _prefix + ":caption/" + caption.Id),
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

            if (_captionsHeadRegions)
            {
                builder.SetRegion(null);
            }
        }

        /// <summary>A text the form draws on a line of its own, where this form's texts are rows
        /// rather than captions: read-only, keyed on the mesh the game drew it into.</summary>
        private void AddTextRow(GraphBuilder builder, MenuRow row, MenuRowText text)
        {
            Component subject = row != null ? row.Transform : null;
            if (subject == null || !text.IsVisible() || string.IsNullOrWhiteSpace(text.GetText()))
            {
                return;
            }

            builder.AddItem(new DrawnNode(
                ControlId.For(subject, _rowKey + text.Id),
                GraphNodes.Text(text.GetText),
                subject));
        }

        /// <summary>The same, under the label the button itself draws (Cancel, Confirm).</summary>
        public void AddWindowButton(GraphBuilder builder, MenuRowButton button)
        {
            if (button != null)
            {
                AddWindowButton(builder, button, button.GetLabel);
            }
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
                ControlId.For(_markers.For(button.Id), _prefix + ":" + button.Id),
                Button(button, label)));
        }

        /// <summary>
        /// The rebindable-action rows of the Controls page as a table: one region per category
        /// caption, one row per action. The primary cell is the gesture name; a binding cell reads the
        /// current hotkey (or "not bound") and is a button that CLEARS the override where the row has
        /// one; a "+" cell starts the capture - the game's for the Options window, the mod's for its
        /// own Keybinds tab, which draws the same widget. Emitted into the stop the rows above it are
        /// in, so the table is reached with the arrows like any row and not by a Tab of its own. Does
        /// nothing for a form that draws no key bindings.
        /// </summary>
        public void BuildKeyBindingSheet(GraphBuilder builder, IReadOnlyList<MenuRow> controls)
        {
            int start = FirstKeyBindingCaption(controls);
            if (start < 0)
            {
                return;
            }

            if (!ReferenceEquals(controls, _keyBindingSource))
            {
                _keyBindingNodes.Clear();
                _keyBindingSource = controls;
            }

            GraphSheet sheet = new GraphSheet(builder, _prefix + ":keybind:");
            bool regionOpen = false;
            for (int i = start; i < controls.Count; i++)
            {
                object item = controls[i] != null ? controls[i].Item : null;

                MenuRowText caption = item as MenuRowText;
                if (caption != null)
                {
                    // Read once: the caption's text is the game's, tags stripped on every read.
                    string label = caption.IsVisible() ? caption.GetText() : null;
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    // Three columns - name, binding, "+" - so the region reads as a table and the
                    // caption names it.
                    sheet.Region(label, new string[3]);
                    regionOpen = true;
                    continue;
                }

                MenuRowKeyBinding binding = item as MenuRowKeyBinding;
                if (binding == null || !binding.IsVisible())
                {
                    continue;
                }

                if (!regionOpen)
                {
                    sheet.Region(null, new string[3]);
                    regionOpen = true;
                }

                AddKeyBindingRow(sheet, controls[i], binding);
            }

            sheet.Finish();
        }

        private void AddKeyBindingRow(GraphSheet sheet, MenuRow row, MenuRowKeyBinding binding)
        {
            bool overridden = binding.HasOverride();
            KeyBindingNodes nodes;
            if (!_keyBindingNodes.TryGetValue(binding, out nodes) || nodes.Overridden != overridden)
            {
                NodeVtable name = GraphNodes.Text(binding.GetActionName, null, binding.GetTooltip());
                name.OnFocusVisual = binding.Focus;
                nodes = new KeyBindingNodes
                {
                    Overridden = overridden,
                    Name = name,
                    Cells = new List<GraphSheet.SheetCell>
                    {
                        new GraphSheet.SheetCell(1, 0, BindingCell(binding, overridden)),
                        new GraphSheet.SheetCell(2, 0, PlusCell(binding)),
                    },
                };
                _keyBindingNodes[binding] = nodes;
            }

            // The row's identity across rebuilds; the widget it is drawn as is the scroll anchor and
            // the existence evidence.
            sheet.RowAt(nodes.Name, binding.Id, nodes.Cells, row.Transform);
        }

        /// <summary>The binding chip: the current hotkey or "not bound". A BUTTON that clears the
        /// override where the row has one - the game's chip is a dead label otherwise - and its text
        /// is watched live so a rebind or clear the game redraws is spoken with no polling.</summary>
        private static NodeVtable BindingCell(MenuRowKeyBinding binding, bool overridden)
        {
            Func<string> text = () => KeyBindingText.Display(
                binding.GetBindingText(), ModText.Get(ModStrings.Screens.NotBound));

            NodeVtable vtable;
            if (overridden)
            {
                vtable = GraphNodes.Button(text, () => binding.ClearOverride(), null, binding.GetClearTooltip());
            }
            else
            {
                vtable = GraphNodes.Text(text);
            }

            if (vtable.Announcements != null && vtable.Announcements.Count > 0)
            {
                vtable.Announcements[0].Live = true;
            }

            vtable.SearchText = binding.GetActionName;
            vtable.OnFocusVisual = binding.Focus;
            return vtable;
        }

        /// <summary>The "+" cell: starts the game's capture. When it does, the game's own "press a key"
        /// popup text is spoken, queued, so it does not cut off whatever the activation itself said.</summary>
        private static NodeVtable PlusCell(MenuRowKeyBinding binding)
        {
            NodeVtable vtable = GraphNodes.Button(
                binding.GetPlusLabel,
                () =>
                {
                    if (binding.Rebind())
                    {
                        AnnounceCapture();
                    }
                },
                null,
                binding.GetPlusTooltip());
            vtable.SearchText = binding.GetActionName;
            vtable.OnFocusVisual = binding.Focus;
            return vtable;
        }

        /// <summary>Speak the instruction the game draws while it listens for the next key - queued,
        /// never interrupting. The drawn text ("Listening... Press any key to assign it to this
        /// action.") already tells the player any key binds, so the mod's own no-cancel line is only a
        /// fallback for when the popup cannot be read.</summary>
        private static void AnnounceCapture()
        {
            // The mod's own capture (a Keybinds-tab row) spoke its prompt when it was armed; there
            // is no game popup to read for it.
            if (ModKeyCapture.IsArmed)
            {
                return;
            }

            List<string> lines = new List<string>();
            ConfirmPopup popup = KeyCaptureFocus.Popup;
            if (popup != null)
            {
                ConfirmPopupAdapter reader = new ConfirmPopupAdapter(popup);
                if (!string.IsNullOrWhiteSpace(reader.Title))
                {
                    lines.Add(reader.Title);
                }

                IList<string> body = reader.BodyLines;
                if (body != null)
                {
                    for (int i = 0; i < body.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(body[i]))
                        {
                            lines.Add(body[i]);
                        }
                    }
                }
            }

            string spoken = lines.Count > 0
                ? string.Join(" ", lines.ToArray())
                : ModText.Get(ModStrings.Screens.CaptureNoCancel);
            SpeechPipeline.Output(new SpeechRequest(spoken, interrupt: false));
        }

        /// <summary>The index of the caption that heads the FIRST key-binding row - where the table
        /// begins - or -1 for a form that draws none. Backs up over the category (and subcategory)
        /// captions drawn directly above the first row; the header and Reset button above those are a
        /// non-caption boundary the walk stops at, so they stay ordinary rows.</summary>
        private static int FirstKeyBindingCaption(IReadOnlyList<MenuRow> controls)
        {
            int first = -1;
            for (int i = 0; i < controls.Count; i++)
            {
                if ((controls[i] != null ? controls[i].Item : null) is MenuRowKeyBinding)
                {
                    first = i;
                    break;
                }
            }

            if (first < 0)
            {
                return -1;
            }

            int start = first;
            while (start - 1 >= 0 && (controls[start - 1] != null ? controls[start - 1].Item : null) is MenuRowText)
            {
                start--;
            }

            return start;
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
                builder.AddItem(new DrawnNode(ControlId.For(subject, _rowKey + toggle.Id), vtable, subject));
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
                builder.AddItem(new DrawnNode(ControlId.For(subject, _rowKey + dropdown.Id), vtable, subject));
                return;
            }

            MenuRowTimeInput time = item as MenuRowTimeInput;
            if (time != null)
            {
                if (time.IsVisible())
                {
                    AddTimeRow(builder, time);
                }

                return;
            }

            MenuRowInput input = item as MenuRowInput;
            if (input != null)
            {
                if (!input.IsVisible())
                {
                    return;
                }

                // The game's own text box: activating it is the request for the keyboard, and the
                // value reports nothing while the game holds it, because the echo is already
                // speaking the keys. The tooltip stays in the buffer but is never DRAWN: drawing it
                // selects the component it hangs on, which takes the keyboard off the field.
                NodeVtable vtable = GraphNodes.EditField(
                    input.GetLabel,
                    () =>
                    {
                        IUITextMeshInputField field = input.GetField();
                        return field == null || Editor.Editing ? null : field.InputFieldValue;
                    },
                    () => Editor.Request(input.GetField()),
                    input.IsEnabled,
                    input.GetTooltip());
                GraphNodes.DoNotDrawTooltip(vtable);
                builder.AddItem(new DrawnNode(ControlId.For(subject, _rowKey + input.Id), vtable, subject));
                return;
            }

            MenuRowButton button = item as MenuRowButton;
            if (button != null && button.IsVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, _rowKey + button.Id),
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
            builder.AddItem(new DrawnNode(ControlId.For(subject, _rowKey + slider.Id), vtable, subject));
        }

        /// <summary>The two halves of a time row, each on the game's own field, each named with the
        /// row's label and saying how much of what it holds in the game's words
        /// (<c>Adventure/PostGameMenu/TotalPlayTime/Minutes</c> and <c>.../Seconds</c>, the keys the
        /// lobby's turn-timer rows were first read with). The widget's gamepad slider is switched off
        /// outside gamepad mode, so the two boxes are the whole row.</summary>
        private void AddTimeRow(GraphBuilder builder, MenuRowTimeInput time)
        {
            AddTimeField(builder, time, time.GetMinutesField, "minutes", true);
            AddTimeField(builder, time, time.GetSecondsField, "seconds", false);
        }

        private void AddTimeField(
            GraphBuilder builder,
            MenuRowTimeInput time,
            Func<IUITextMeshInputField> getField,
            string part,
            bool minutes)
        {
            IUITextMeshInputField field = getField != null ? getField() : null;
            Component subject = field != null ? field.MonoTransform : null;
            if (subject == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.EditField(
                time.GetLabel,
                () => Editor.Editing ? null : TimeText(getField, minutes),
                () => Editor.Request(getField()),
                time.IsEnabled,
                time.GetTooltip());
            GraphNodes.DoNotDrawTooltip(vtable);
            builder.AddItem(new DrawnNode(
                ControlId.For(subject, _rowKey + time.Id + "/" + part),
                vtable,
                subject));
        }

        private static string TimeText(Func<IUITextMeshInputField> getField, bool minutes)
        {
            IUITextMeshInputField field = getField != null ? getField() : null;
            string raw = field != null ? field.InputFieldValue : null;
            int value;
            if (!int.TryParse(raw, out value))
            {
                return raw;
            }

            string key = minutes
                ? "Adventure/PostGameMenu/TotalPlayTime/Minutes"
                : "Adventure/PostGameMenu/TotalPlayTime/Seconds";
            return GameText.Get(key, raw, value);
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
    }
}
