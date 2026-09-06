using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's per-player settings popup, made navigable as a graph. Two stops: the settings the
    /// popup draws, and the Cancel and Confirm buttons under them.
    ///
    /// Measured 2026-09-06 at 1280x800 through `/gui/unity`: the popup at [333,226,613,348] with its
    /// title at y 252, then one column of rows at x 450 - Income multiplier (y 298), Troop production
    /// multiplier (y 337), Start with Marketplace (y 382), XP bonus multiplier (y 421) and the
    /// "Reset to default" button (y 453) - and Cancel (x 508) and Confirm (x 646) at y 514. No
    /// caption is drawn over any of it, so the screen declares no regions.
    ///
    /// Each slider draws a value box over its number (an `EditButton` at x 792), so each slider row
    /// is ACTIVATED by opening the game's own "Provide a number" popup, exactly as the options
    /// window's sliders are; the arrows remain the way the value is moved.
    ///
    /// Escape is the game's (<see cref="ConsumesBack"/> false): <c>LobbyPlayerSettingsMenu.Show</c>
    /// registers <c>InputActions.UI.ExitMenu</c> outside its gamepad branch (decompiled, line 146),
    /// so the key already cancels the popup.
    /// </summary>
    public sealed class AdventureLobbyPlayerSettingsScreen : GraphScreen
    {
        private const string RowsStop = "player-settings-rows";
        private const string ButtonsStop = "player-settings-buttons";

        /// <summary>How many fine steps one coarse slider step is worth, as on the options window.</summary>
        private const int CoarseSteps = 10;

        private readonly AdventureLobbyPlayerSettingsAdapter _adapter;

        // A subject of its own per synthesized row, kept across rebuilds so the reconciler seats the
        // cursor on the same line: the popup's own buttons.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public AdventureLobbyPlayerSettingsScreen(AdventureLobbyPlayerSettingsAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            LobbyPlayerSettingsMenu menu = FindActiveMenu(null);
            if (menu == null)
            {
                return null;
            }

            AdventureLobbyPlayerSettingsAdapter adapter = new AdventureLobbyPlayerSettingsAdapter(menu);
            return adapter.IsPresent() ? new AdventureLobbyPlayerSettingsScreen(adapter) : null;
        }

        public bool Matches(LobbyPlayerSettingsMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override string Key
        {
            get { return "player-settings"; }
        }

        /// <summary>The popup's own drawn title ("Player settings").</summary>
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

        /// <summary>Kept for the detector, which calls it whenever the popup's content changes. The
        /// graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public static LobbyPlayerSettingsMenu FindActiveMenu(LobbyPlayerSettingsMenu targetMenu)
        {
            LobbyPlayerSettingsMenu[] menus = Resources.FindObjectsOfTypeAll<LobbyPlayerSettingsMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                LobbyPlayerSettingsMenu menu = menus[i];
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

                AdventureLobbyPlayerSettingsAdapter adapter = new AdventureLobbyPlayerSettingsAdapter(menu);
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
            IReadOnlyList<AdventureLobbyPlayerSettingsAdapter.ControlItem> controls = _adapter.GetContentControls();
            for (int i = 0; i < controls.Count; i++)
            {
                AddRow(builder, controls[i]);
            }

            builder.BeginStop(ButtonsStop);
            AddButton(builder, _adapter.GetCancelButton());
            AddButton(builder, _adapter.GetConfirmButton());
        }

        private void AddRow(GraphBuilder builder, AdventureLobbyPlayerSettingsAdapter.ControlItem control)
        {
            object item = control != null ? control.Item : null;
            Component subject = control != null ? control.Transform : null;
            if (item == null || subject == null)
            {
                return;
            }

            AdventureLobbyPlayerSettingsAdapter.TextItem text = item as AdventureLobbyPlayerSettingsAdapter.TextItem;
            if (text != null)
            {
                if (text.IsVisible() && !string.IsNullOrWhiteSpace(text.GetText()))
                {
                    builder.AddItem(new DrawnNode(
                        ControlId.For(subject, "player-settings:" + text.Id),
                        GraphNodes.Text(text.GetText),
                        subject));
                }

                return;
            }

            AdventureLobbyPlayerSettingsAdapter.ToggleItem toggle = item as AdventureLobbyPlayerSettingsAdapter.ToggleItem;
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
                    ControlId.For(subject, "player-settings:" + toggle.Id),
                    vtable,
                    subject));
                return;
            }

            AdventureLobbyPlayerSettingsAdapter.SliderItem slider = item as AdventureLobbyPlayerSettingsAdapter.SliderItem;
            if (slider != null)
            {
                if (slider.IsVisible())
                {
                    AddSlider(builder, slider, subject);
                }

                return;
            }

            AdventureLobbyPlayerSettingsAdapter.ButtonItem button = item as AdventureLobbyPlayerSettingsAdapter.ButtonItem;
            if (button != null && button.IsVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "player-settings:" + button.Id),
                    Button(button),
                    subject));
            }
        }

        /// <summary>
        /// A slider row - the shape the options window established. Left and Right move the value;
        /// Enter opens the game's own "provide a number" popup through the value box the row draws
        /// beside the handle, rather than the box being a child node of its own under every slider.
        /// </summary>
        private void AddSlider(
            GraphBuilder builder,
            AdventureLobbyPlayerSettingsAdapter.SliderItem slider,
            Component subject)
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
            builder.AddItem(new DrawnNode(ControlId.For(subject, "player-settings:" + slider.Id), vtable, subject));
        }

        private static void Adjust(AdventureLobbyPlayerSettingsAdapter.SliderItem slider, int sign, bool large)
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

        private void AddButton(GraphBuilder builder, AdventureLobbyPlayerSettingsAdapter.ButtonItem button)
        {
            if (button == null || !button.IsVisible())
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(button.Id), "player-settings:" + button.Id),
                Button(button)));
        }

        private static NodeVtable Button(AdventureLobbyPlayerSettingsAdapter.ButtonItem button)
        {
            NodeVtable vtable = GraphNodes.Button(
                button.GetLabel,
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
