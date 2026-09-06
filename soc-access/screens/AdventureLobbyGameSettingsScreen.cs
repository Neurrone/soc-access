using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's game settings window, made navigable as a graph. Two stops: the settings the page
    /// draws, and the Cancel and Confirm buttons under them.
    ///
    /// Measured 2026-09-06 at 1280x800 through `/gui/unity`: the window at [325,147,630,519], one
    /// scrolling column of rows at x 364 (539 wide, 861 px of rows in a 379 px viewport), each row
    /// drawing its label at x 367 and its control at x 570, and the buttons under it, Cancel at
    /// x 508 and Confirm at x 646. The page draws NO captions over its rows, so it declares no
    /// regions; a text the game does draw is a read-only row where it stands.
    ///
    /// The rows are whatever the menu's factory built, in the order the page draws them: text
    /// fields, toggles, dropdowns, the turn-timer time fields and the button that resets them.
    /// A dropdown is a combo box opening <see cref="DropListScreen"/> over the game's own popup.
    ///
    /// A TIME field is two edit fields, because that is what the game draws for a keyboard:
    /// <c>UITimeInputField</c> has a minutes field and a seconds field under its "Keyboard" header
    /// and a slider under a "Gamepad" one, and the slider is switched off outside gamepad mode
    /// (measured: `GamepadSlider` reads `visible=false` on every drawn row). Both nodes are named
    /// with the row's own label and say which half they are in the game's words.
    ///
    /// Escape is the game's (<see cref="ConsumesBack"/> false): <c>LobbyMapSettingsMenu.Show</c>
    /// registers <c>InputActions.UI.ExitMenu</c> outside its gamepad branch (decompiled, line 333),
    /// so the key already cancels the window.
    /// </summary>
    public sealed class AdventureLobbyGameSettingsScreen : GraphScreen
    {
        private const string RowsStop = "game-settings-rows";
        private const string ButtonsStop = "game-settings-buttons";

        private readonly AdventureLobbyGameSettingsAdapter _adapter;
        private readonly GameTextEditor _editor = new GameTextEditor();

        public AdventureLobbyGameSettingsScreen(AdventureLobbyGameSettingsAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            LobbyMapSettingsMenu menu = FindActiveMenu(null);
            if (menu == null)
            {
                return null;
            }

            AdventureLobbyGameSettingsAdapter adapter = new AdventureLobbyGameSettingsAdapter(menu);
            return adapter.IsPresent() ? new AdventureLobbyGameSettingsScreen(adapter) : null;
        }

        public bool Matches(LobbyMapSettingsMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override string Key
        {
            get { return "game-settings"; }
        }

        /// <summary>The window's own drawn title ("Game settings").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return RowsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>While the keyboard is on its way to one of the page's text fields, what the player
        /// types next is meant for that field and must not start a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>Kept for the detector, which calls it whenever the window's content changes. The
        /// graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public override void Update()
        {
            base.Update();
            _editor.Update(IsPresent());
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _editor.Abandon();
        }

        public override void OnPop()
        {
            base.OnPop();
            _editor.Abandon();
        }

        public static LobbyMapSettingsMenu FindActiveMenu(LobbyMapSettingsMenu targetMenu)
        {
            LobbyMapSettingsMenu[] menus = Resources.FindObjectsOfTypeAll<LobbyMapSettingsMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                LobbyMapSettingsMenu menu = menus[i];
                if (menu == null)
                {
                    continue;
                }

                if (targetMenu != null && !ReferenceEquals(targetMenu, menu))
                {
                    continue;
                }

                GameObject gameObject = ((Component)menu).gameObject;
                if (gameObject == null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
                {
                    continue;
                }

                AdventureLobbyGameSettingsAdapter adapter = new AdventureLobbyGameSettingsAdapter(menu);
                if (adapter.IsPresent())
                {
                    return menu;
                }
            }

            return null;
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(RowsStop);
            IReadOnlyList<AdventureLobbyGameSettingsAdapter.ControlItem> controls = _adapter.GetContentControls();
            for (int i = 0; i < controls.Count; i++)
            {
                AddRow(builder, controls[i]);
            }

            builder.BeginStop(ButtonsStop);
            AddButton(builder, _adapter.GetCancelButton());
            AddButton(builder, _adapter.GetApplyButton());
        }

        private void AddRow(GraphBuilder builder, AdventureLobbyGameSettingsAdapter.ControlItem control)
        {
            object item = control != null ? control.Item : null;
            Component subject = control != null ? control.Transform : null;
            if (item == null || subject == null)
            {
                return;
            }

            AdventureLobbyGameSettingsAdapter.TextItem text = item as AdventureLobbyGameSettingsAdapter.TextItem;
            if (text != null)
            {
                if (text.IsVisible() && !string.IsNullOrWhiteSpace(text.GetText()))
                {
                    builder.AddItem(new DrawnNode(
                        ControlId.For(subject, "game-settings:" + text.Id),
                        GraphNodes.Text(text.GetText),
                        subject));
                }

                return;
            }

            AdventureLobbyGameSettingsAdapter.ToggleItem toggle = item as AdventureLobbyGameSettingsAdapter.ToggleItem;
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
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "game-settings:" + toggle.Id),
                    vtable,
                    subject));
                return;
            }

            AdventureLobbyGameSettingsAdapter.DropdownItem dropdown = item as AdventureLobbyGameSettingsAdapter.DropdownItem;
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
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "game-settings:" + dropdown.Id),
                    vtable,
                    subject));
                return;
            }

            AdventureLobbyGameSettingsAdapter.TextInputItem input = item as AdventureLobbyGameSettingsAdapter.TextInputItem;
            if (input != null)
            {
                if (!input.IsVisible())
                {
                    return;
                }

                // No native focus visual on an edit row: the visual is re-asserted while the cursor
                // stands here, and re-selecting the ROW takes the keyboard straight back off the
                // field the player just asked to type in (measured below). The dialog's edit field,
                // the exemplar, declares none either.
                NodeVtable vtable = EditField(
                    input.GetLabel,
                    input.GetField,
                    input.IsEnabled,
                    input.GetTooltip());
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "game-settings:" + input.Id),
                    vtable,
                    subject));
                return;
            }

            AdventureLobbyGameSettingsAdapter.TimeInputItem time = item as AdventureLobbyGameSettingsAdapter.TimeInputItem;
            if (time != null)
            {
                if (time.IsVisible())
                {
                    AddTimeRow(builder, time);
                }

                return;
            }

            AdventureLobbyGameSettingsAdapter.ButtonItem button = item as AdventureLobbyGameSettingsAdapter.ButtonItem;
            if (button != null && button.IsVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "game-settings:" + button.Id),
                    Button(button),
                    subject));
            }
        }

        /// <summary>The two halves of a turn-timer row, each on the game's own field, each named with
        /// the row's label and saying how much of what it holds in the game's words
        /// (<c>Adventure/PostGameMenu/TotalPlayTime/Minutes</c> and <c>.../Seconds</c>, the keys the
        /// widget screen read them with).</summary>
        private void AddTimeRow(GraphBuilder builder, AdventureLobbyGameSettingsAdapter.TimeInputItem time)
        {
            AddTimeField(builder, time, time.GetMinutesField, "minutes", true);
            AddTimeField(builder, time, time.GetSecondsField, "seconds", false);
        }

        private void AddTimeField(
            GraphBuilder builder,
            AdventureLobbyGameSettingsAdapter.TimeInputItem time,
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
                () => _editor.Editing ? null : TimeText(getField, minutes),
                () => _editor.Request(getField()),
                time.IsEnabled,
                time.GetTooltip());
            GraphNodes.DoNotDrawTooltip(vtable);
            builder.AddItem(new DrawnNode(
                ControlId.For(subject, "game-settings:" + time.Id + "/" + part),
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

        /// <summary>The game's own text box: activating it is the request for the keyboard, and the
        /// value reports nothing while the game holds it because the echo is already speaking the
        /// keys.</summary>
        private NodeVtable EditField(
            Func<string> label,
            Func<IUITextMeshInputField> getField,
            Func<bool> enabled,
            Tooltip tooltip)
        {
            NodeVtable vtable = GraphNodes.EditField(
                label,
                () =>
                {
                    IUITextMeshInputField field = getField != null ? getField() : null;
                    return field == null || _editor.Editing ? null : field.InputFieldValue;
                },
                () => _editor.Request(getField != null ? getField() : null),
                enabled,
                tooltip);
            GraphNodes.DoNotDrawTooltip(vtable);
            return vtable;
        }

        private static void AddButton(GraphBuilder builder, AdventureLobbyGameSettingsAdapter.ButtonItem button)
        {
            if (button == null || !button.IsVisible())
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.Structural("game-settings:" + button.Id),
                Button(button)));
        }

        private static NodeVtable Button(AdventureLobbyGameSettingsAdapter.ButtonItem button)
        {
            NodeVtable vtable = GraphNodes.Button(
                button.GetLabel,
                () => button.Activate(),
                button.IsEnabled,
                button.GetTooltip());
            vtable.OnFocusVisual = button.Focus;
            return vtable;
        }

        private static string CurrentOption(AdventureLobbyGameSettingsAdapter.DropdownItem dropdown)
        {
            IReadOnlyList<string> options = dropdown.GetOptions();
            int value = dropdown.GetValue();
            return options != null && value >= 0 && value < options.Count ? options[value] : string.Empty;
        }
    }
}
