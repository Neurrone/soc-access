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
    /// (the lobby's two settings windows) asks for every text to be a row where it stands. A form
    /// drawn by the mod can say the same of ONE text (<c>ModDialog.AddText(standalone: true)</c>):
    /// the Bookmarks tab's file path and a message dialog's answer are lines to land on, not names
    /// for the buttons under them.
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

        // The ordinary rows' nodes, on the same terms: built once per ROW LIST, since the reader
        // hands back the same list until the column is redrawn. The subject is kept with them
        // because it is what the reconciler keys on, so a row the game re-seated is rebuilt.
        private IReadOnlyList<MenuRow> _rowSource;
        private readonly Dictionary<object, RowNodes> _rowNodes = new Dictionary<object, RowNodes>();

        private sealed class RowNodes
        {
            public Component Subject;
            public List<NodeDeclaration> Nodes;
        }

        /// <summary>What a table's node keys begin with, after the form's own prefix.</summary>
        private const string TableKey = ":table/";

        // Which table row each drawn row belongs to, worked out once per ROW LIST: a row's layout is
        // settled when the column is drawn and cannot change while the list does not, so a build
        // reads this array and asks the game nothing.
        private ModDialog.FormFacts.DrawnRow[] _tablePlan;

        // The table rows' cells, on the same terms as the ordinary rows' nodes: built once per row
        // list, keyed on the row the form says each layout is.
        private readonly Dictionary<ModDialog.FormFacts.DrawnRow, TableRowNodes> _tableNodes =
            new Dictionary<ModDialog.FormFacts.DrawnRow, TableRowNodes>();

        private sealed class TableRowNodes
        {
            public NodeVtable Primary;
            public List<GraphSheet.SheetCell> Cells;
        }

        /// <summary>Which of this form's rows are a table's, and which row of it each one is. One
        /// pass over the list, whenever the list is a new one.</summary>
        private void PlanTables(IReadOnlyList<MenuRow> controls, ModDialog.FormFacts facts)
        {
            _tablePlan = null;
            if (facts == null || !facts.HasTables || controls == null)
            {
                return;
            }

            ModDialog.FormFacts.DrawnRow[] plan = new ModDialog.FormFacts.DrawnRow[controls.Count];
            for (int i = 0; i < controls.Count; i++)
            {
                Transform transform = controls[i] != null ? controls[i].Transform : null;
                plan[i] = facts.RowOf(transform != null ? transform.parent : null);
            }

            _tablePlan = plan;
        }

        /// <summary>One row of a table: the first cell drawn is the row's primary and names it, the
        /// rest are its columns in drawn order. Built once per row list, because a cell is the same
        /// vtable every frame; whether the row is DRAWN stays the gate's question, asked of the
        /// layout the row is drawn as.</summary>
        private void AddTableRow(
            GraphSheet sheet,
            IReadOnlyList<MenuRow> controls,
            int first,
            int last,
            ModDialog.FormFacts.DrawnRow drawn,
            ModDialog.FormFacts facts)
        {
            TableRowNodes nodes;
            if (!_tableNodes.TryGetValue(drawn, out nodes))
            {
                NodeVtable primary = CellVtable(controls[first], facts);
                if (primary == null)
                {
                    return;
                }

                Func<string> name = primary.Announcements != null && primary.Announcements.Count > 0
                    ? primary.Announcements[0].Text
                    : null;
                List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>(last - first);
                for (int i = first + 1; i <= last; i++)
                {
                    NodeVtable cell = CellVtable(controls[i], facts);
                    if (cell == null)
                    {
                        continue;
                    }

                    // Type-ahead over a table matches the ROW's name from any of its columns.
                    cell.SearchText = name;
                    cells.Add(new GraphSheet.SheetCell(i - first, 0, cell));
                }

                nodes = new TableRowNodes { Primary = primary, Cells = cells };
                _tableNodes[drawn] = nodes;
            }

            sheet.RowAt(nodes.Primary, drawn.RowRef, nodes.Cells, drawn.Layout);
        }

        /// <summary>One cell of a table row, as the same kind of node the row would be on its own:
        /// a checkbox, a button, or the read-only text a row is named by. A control the form says is
        /// called something other than what it draws - an arrow glyph - is read by that name.
        /// </summary>
        private NodeVtable CellVtable(MenuRow control, ModDialog.FormFacts facts)
        {
            object item = control != null ? control.Item : null;
            Transform transform = control != null ? control.Transform : null;
            if (facts != null && facts.IsBlankCell(transform))
            {
                // A cell drawn with nothing in it keeps the column aligned and is not a control;
                // the row is built without it, so the columns around it keep their numbers.
                return null;
            }

            string spoken = facts != null ? facts.SpokenLabelOf(transform) : null;
            Func<string> label = spoken != null ? (Func<string>)(() => spoken) : null;

            MenuRowText text = item as MenuRowText;
            if (text != null)
            {
                return GraphNodes.Text(label ?? text.GetText);
            }

            MenuRowToggle toggle = item as MenuRowToggle;
            if (toggle != null)
            {
                NodeVtable vtable = GraphNodes.Checkbox(
                    label ?? toggle.GetLabel,
                    toggle.IsChecked,
                    toggle.Toggle,
                    toggle.IsEnabled,
                    toggle.GetTooltip());
                vtable.OnFocusVisual = toggle.Focus;
                return vtable;
            }

            MenuRowButton button = item as MenuRowButton;
            return button != null ? Button(button, label ?? button.GetLabel) : null;
        }

        /// <summary>The id of one cell of a table this form drew - what a screen names to put the
        /// cursor back on the very cell the player was working after a redraw moved it. Minted by
        /// the sheet itself, so the key format stays the sheet's own business.</summary>
        public ControlId CellId(string table, string rowRef, int column)
        {
            if (table == null || rowRef == null)
            {
                return null;
            }

            return ControlId.Structural(
                new GraphSheet(null, _prefix + TableKey + table + ":").CellKey(rowRef, column));
        }

        public void BuildRows(GraphBuilder builder, IReadOnlyList<MenuRow> controls)
        {
            BuildRows(builder, controls, null);
        }

        /// <param name="facts">What the form says about itself that its rows cannot
        /// (<see cref="ModDialog.FormFacts"/>): which of its rows are a table's, what that table's
        /// columns are called, and what a control drawn as a glyph is called. Null for a form that
        /// draws no table, which is every form the game itself draws.</param>
        public void BuildRows(GraphBuilder builder, IReadOnlyList<MenuRow> controls, ModDialog.FormFacts facts)
        {
            if (!ReferenceEquals(controls, _rowSource))
            {
                _rowNodes.Clear();
                _tableNodes.Clear();
                _rowSource = controls;
                PlanTables(controls, facts);
            }

            // The rebindable-action rows are a table of their own; stop where they begin so their
            // category captions are the sheet's regions and not empty read-only rows here.
            int end = FirstKeyBindingCaption(controls);
            if (end < 0)
            {
                end = controls.Count;
            }

            bool inCaption = false;
            GraphSheet sheet = null;
            string table = null;
            for (int i = 0; i < end; i++)
            {
                MenuRow control = controls[i];
                object item = control != null ? control.Item : null;

                ModDialog.FormFacts.DrawnRow drawn = _tablePlan != null ? _tablePlan[i] : null;
                if (drawn != null)
                {
                    if (inCaption)
                    {
                        builder.PopContext();
                        inCaption = false;
                    }

                    if (sheet == null || !string.Equals(table, drawn.Table, StringComparison.Ordinal))
                    {
                        if (sheet != null)
                        {
                            sheet.Finish();
                        }

                        table = drawn.Table;
                        sheet = new GraphSheet(builder, _prefix + TableKey + table + ":");
                        // Named where the dialog named it, so the way in says "<name>, table"; a
                        // sheet entered under no name says neither.
                        sheet.Region(facts.LabelOf(table), facts.ColumnsOf(table));
                    }

                    // The cells of one drawn row are consecutive, because the reader hands its rows
                    // back in drawn order and they share the layout the row is drawn as.
                    int last = i;
                    while (last + 1 < end && ReferenceEquals(_tablePlan[last + 1], drawn))
                    {
                        last++;
                    }

                    // A header band is DRAWN and not read: the reader says the column's caption on
                    // the way into it instead.
                    if (drawn.RowRef != null)
                    {
                        AddTableRow(sheet, controls, i, last, drawn, facts);
                    }

                    i = last;
                    continue;
                }

                if (sheet != null)
                {
                    sheet.Finish();
                    sheet = null;
                    table = null;
                    builder.SetRegion(null);
                }

                MenuRowText caption = item as MenuRowText;
                if (caption != null)
                {
                    // A text the form drew as a line of its own heads nothing, wherever this form's
                    // other texts are captions.
                    if (!_captionsHeadRegions
                        || (facts != null && facts.IsStandaloneText(control.Transform)))
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
                        builder.SetRegion(caption.Key);
                        inCaption = true;
                    }
                    else
                    {
                        builder.AddItem(new SyntheticNode(
                            ControlId.For(_markers.For(caption.Key), _prefix + ":caption/" + caption.Key),
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

            if (sheet != null)
            {
                sheet.Finish();
            }

            if (_captionsHeadRegions || sheet != null)
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
                ControlId.For(subject, _rowKey + text.Key),
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
                ControlId.For(_markers.For(button.Key), _prefix + ":" + button.Key),
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
            sheet.RowAt(nodes.Name, binding.Key, nodes.Cells, row.Transform);
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

        /// <summary>One ordinary form row, built once per ROW LIST rather than once per frame - the
        /// key-binding rows' treatment (<see cref="_keyBindingNodes"/>) for the rest of the form.
        /// Every row asked its adapter for a tooltip as it was declared, and each of those is a
        /// Tooltip, a VisualTooltipMetadata and two closures the row then reads from when it is
        /// focused; the Options window's Controls page and the mod's Keybinds tab paid for a
        /// hundred of them a frame.
        ///
        /// Whether the row is DRAWN stays a per-frame question, so a row the game hides leaves the
        /// tree the frame it does. What is kept is what the row is built as, which is settled by the
        /// row itself: a row whose widget the reader could not resolve yet keeps nothing, so the
        /// next build asks again.</summary>
        private void AddRow(GraphBuilder builder, MenuRow control)
        {
            object item = control != null ? control.Item : null;
            Component subject = control != null ? control.Transform : null;
            if (item == null || subject == null || !IsRowVisible(item))
            {
                return;
            }

            RowNodes cached;
            if (!_rowNodes.TryGetValue(item, out cached) || !ReferenceEquals(cached.Subject, subject))
            {
                cached = new RowNodes { Subject = subject, Nodes = new List<NodeDeclaration>(1) };
                BuildRow(cached.Nodes, item, subject);
                if (cached.Nodes.Count == 0)
                {
                    return;
                }

                _rowNodes[item] = cached;
            }

            for (int i = 0; i < cached.Nodes.Count; i++)
            {
                builder.AddItem(cached.Nodes[i]);
            }
        }

        /// <summary>Whether the game is drawing this row right now - asked every frame, for every
        /// kind of row this form knows. A kind it does not know is not a row.</summary>
        private static bool IsRowVisible(object item)
        {
            MenuRowToggle toggle = item as MenuRowToggle;
            if (toggle != null)
            {
                return toggle.IsVisible();
            }

            MenuRowSlider slider = item as MenuRowSlider;
            if (slider != null)
            {
                return slider.IsVisible();
            }

            MenuRowDropdown dropdown = item as MenuRowDropdown;
            if (dropdown != null)
            {
                return dropdown.IsVisible();
            }

            MenuRowTimeInput time = item as MenuRowTimeInput;
            if (time != null)
            {
                return time.IsVisible();
            }

            MenuRowInput input = item as MenuRowInput;
            if (input != null)
            {
                return input.IsVisible();
            }

            MenuRowButton button = item as MenuRowButton;
            return button != null && button.IsVisible();
        }

        private void BuildRow(List<NodeDeclaration> into, object item, Component subject)
        {
            MenuRowToggle toggle = item as MenuRowToggle;
            if (toggle != null)
            {
                NodeVtable vtable = GraphNodes.Checkbox(
                    toggle.GetLabel,
                    toggle.IsChecked,
                    toggle.Toggle,
                    toggle.IsEnabled,
                    toggle.GetTooltip());
                vtable.OnFocusVisual = toggle.Focus;
                into.Add(new DrawnNode(ControlId.For(subject, _rowKey + toggle.Key), vtable, subject));
                return;
            }

            MenuRowSlider slider = item as MenuRowSlider;
            if (slider != null)
            {
                AddSlider(into, slider, subject);
                return;
            }

            MenuRowDropdown dropdown = item as MenuRowDropdown;
            if (dropdown != null)
            {
                NodeVtable vtable = GraphNodes.ComboBox(
                    dropdown.GetLabel,
                    () => CurrentOption(dropdown),
                    () => DropListScreen.Open(dropdown, dropdown.GetLabel(), index => dropdown.SetValue(index)),
                    dropdown.IsEnabled,
                    dropdown.GetTooltip());
                vtable.OnFocusVisual = dropdown.Focus;
                into.Add(new DrawnNode(ControlId.For(subject, _rowKey + dropdown.Key), vtable, subject));
                return;
            }

            MenuRowTimeInput time = item as MenuRowTimeInput;
            if (time != null)
            {
                AddTimeRow(into, time);
                return;
            }

            MenuRowInput input = item as MenuRowInput;
            if (input != null)
            {
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
                into.Add(new DrawnNode(ControlId.For(subject, _rowKey + input.Key), vtable, subject));
                return;
            }

            MenuRowButton button = item as MenuRowButton;
            if (button != null)
            {
                into.Add(new DrawnNode(
                    ControlId.For(subject, _rowKey + button.Key),
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
        private void AddSlider(List<NodeDeclaration> into, MenuRowSlider slider, Component subject)
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
            into.Add(new DrawnNode(ControlId.For(subject, _rowKey + slider.Key), vtable, subject));
        }

        /// <summary>The two halves of a time row, each on the game's own field, each named with the
        /// row's label and saying how much of what it holds in the game's words
        /// (<c>Adventure/PostGameMenu/TotalPlayTime/Minutes</c> and <c>.../Seconds</c>, the keys the
        /// lobby's turn-timer rows were first read with). The widget's gamepad slider is switched off
        /// outside gamepad mode, so the two boxes are the whole row.</summary>
        private void AddTimeRow(List<NodeDeclaration> into, MenuRowTimeInput time)
        {
            AddTimeField(into, time, time.GetMinutesField, "minutes", true);
            AddTimeField(into, time, time.GetSecondsField, "seconds", false);
        }

        private void AddTimeField(
            List<NodeDeclaration> into,
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
            into.Add(new DrawnNode(
                ControlId.For(subject, _rowKey + time.Key + "/" + part),
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
