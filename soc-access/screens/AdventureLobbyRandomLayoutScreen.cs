using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The random map layout page, made navigable as a graph. Two stops: the settings the page draws,
    /// and the buttons under and above them.
    ///
    /// Measured 2026-09-06 at 1280x800: four cards side by side at x 187, 417, 648 and 878, each
    /// drawing a player count ("2 Players"), a paragraph of description, three win-condition toggles
    /// and a layout dropdown; Confirm at [562,662]; the lobby's Back at [21,20] and Options at
    /// [1233,11] in the header band.
    ///
    /// A card is a RADIO BUTTON, not a button: exactly one of the four is in force at a time (the
    /// menu's own <c>SetSelectedEntry</c>), picking one is not yet doing anything, and Confirm is
    /// what does. Only the card in force says "selected", which is also what makes focus entering
    /// the page land on it.
    ///
    /// The win-condition toggles and the layout dropdown are the SELECTED card's, as they were for
    /// the widget screen: the other three cards draw their own copies, but they are the settings of
    /// maps the player has not chosen. They read after the cards, which is where the selected card
    /// draws them. The dropdown is a real <c>UITextMeshDropdown</c>, so it is a combo box opening
    /// <see cref="DropListScreen"/> over the game's own popup.
    ///
    /// The page draws no captions over the rows, so it declares no regions.
    ///
    /// Escape: the menu registers only <c>InputActions.UI.Confirm</c> (decompiled
    /// <c>LobbyRandomMapSelectionMenu.Show</c>, line 113) and neither <c>LobbyNavigation</c> nor
    /// <c>MapTypeMenu</c> registers any input callback at all, so the key does nothing here and the
    /// screen claims it to press the drawn Back button.
    /// </summary>
    public sealed class AdventureLobbyRandomLayoutScreen : GraphScreen
    {
        private const string RowsStop = "random-layout-rows";
        private const string ButtonsStop = "random-layout-buttons";

        private readonly AdventureLobbyRandomLayoutAdapter _adapter;

        public AdventureLobbyRandomLayoutScreen(AdventureLobbyRandomLayoutAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyRandomLayoutAdapter adapter = FindActiveRandomLayoutMenu(null);
            return adapter != null ? new AdventureLobbyRandomLayoutScreen(adapter) : null;
        }

        public bool Matches(LobbyRandomMapSelectionMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override string Key
        {
            get { return "random-layout"; }
        }

        /// <summary>The page's own drawn title ("Select layout").</summary>
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

        public override bool ConsumesBack
        {
            get { return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.IsVisible(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.Activate();
        }

        /// <summary>Kept for the detector, which calls it whenever the selected card changes. The
        /// graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh(bool announceFocus)
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(RowsStop);
            BuildLayouts(builder);
            BuildSelectedLayoutSettings(builder);

            builder.BeginStop(ButtonsStop);
            // Confirm under the cards, then the header band's Back and Options.
            AddButton(builder, "random-layout:confirm", _adapter.ConfirmButton);
            AddButton(builder, "random-layout:back", _adapter.BackButton);
            AddButton(builder, "random-layout:options", _adapter.OptionsButton);
        }

        private void BuildLayouts(GraphBuilder builder)
        {
            IReadOnlyList<AdventureLobbyRandomLayoutAdapter.RandomLayoutItem> layouts = _adapter.GetLayouts();
            for (int i = 0; i < layouts.Count; i++)
            {
                AdventureLobbyRandomLayoutAdapter.RandomLayoutItem layout = layouts[i];
                Component subject = layout != null ? layout.Entry : null;
                if (subject == null)
                {
                    continue;
                }

                // The description is always on the card, so it reads after the label rather than
                // waiting in the buffer; it is a buffer line by being a part.
                NodeVtable vtable = GraphNodes.Radio(
                    () => layout.Title,
                    () => layout.IsSelected,
                    () => layout.Activate());
                vtable.Announcements.Add(GraphNodes.ValuePart(() => layout.Description, watch: false));
                vtable.OnFocusVisual = layout.FocusNative;
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "random-layout:card/" + layout.Id),
                    vtable,
                    subject));
            }
        }

        private void BuildSelectedLayoutSettings(GraphBuilder builder)
        {
            AdventureLobbyRandomLayoutAdapter.RandomLayoutItem selected = _adapter.SelectedLayout;
            if (selected == null)
            {
                return;
            }

            IReadOnlyList<AdventureLobbyRandomLayoutAdapter.WinConditionToggleItem> toggles =
                selected.GetWinConditionToggles();
            for (int i = 0; i < toggles.Count; i++)
            {
                AdventureLobbyRandomLayoutAdapter.WinConditionToggleItem toggle = toggles[i];
                Component subject = toggle != null ? toggle.Subject : null;
                if (subject == null || !toggle.IsVisible)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Checkbox(
                    () => toggle.Label,
                    () => toggle.IsChecked,
                    toggle.Toggle,
                    () => toggle.IsEnabled,
                    toggle.GetTooltip());
                vtable.OnFocusVisual = toggle.Focus;
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "random-layout:win-condition/" + toggle.Id),
                    vtable,
                    subject));
            }

            AdventureLobbyRandomLayoutAdapter.LayoutDropdownItem dropdown = selected.GetLayoutDropdown();
            Component dropdownSubject = dropdown != null ? dropdown.Subject : null;
            if (dropdownSubject == null || !dropdown.IsVisible())
            {
                return;
            }

            // The dropdown draws only its current value, so the row is named the way the widget
            // screen named it: the mod's word for what is being chosen.
            Func<string> label = () => ModText.Get(ModStrings.Screens.Layout);
            NodeVtable combo = GraphNodes.ComboBox(
                label,
                () => CurrentOption(dropdown),
                () => DropListScreen.Open(dropdown, label(), index => dropdown.SetValue(index)),
                dropdown.IsEnabled,
                dropdown.GetTooltip());
            combo.OnFocusVisual = dropdown.Focus;
            builder.AddItem(new DrawnNode(
                ControlId.For(dropdownSubject, "random-layout:" + dropdown.Id),
                combo,
                dropdownSubject));
        }

        private static string CurrentOption(AdventureLobbyRandomLayoutAdapter.LayoutDropdownItem dropdown)
        {
            IReadOnlyList<string> options = dropdown.GetOptions();
            int value = dropdown.GetValue();
            return options != null && value >= 0 && value < options.Count ? options[value] : string.Empty;
        }

        private static void AddButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(button.GetLabel, () => button.Activate(), button.IsEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button.Button);
            builder.AddItem(new DrawnNode(ControlId.For(button.Button, key), vtable, button.Button));
        }

        public static AdventureLobbyRandomLayoutAdapter FindActiveRandomLayoutMenu(LobbyRandomMapSelectionMenu targetMenu)
        {
            LobbyRandomMapSelectionMenu[] menus = Resources.FindObjectsOfTypeAll<LobbyRandomMapSelectionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                LobbyRandomMapSelectionMenu menu = menus[i];
                if (!IsLiveSceneRandomLayoutMenu(menu))
                {
                    continue;
                }

                if (targetMenu != null && !ReferenceEquals(targetMenu, menu))
                {
                    continue;
                }

                AdventureLobbyRandomLayoutAdapter adapter = new AdventureLobbyRandomLayoutAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneRandomLayoutMenu(LobbyRandomMapSelectionMenu menu)
        {
            if (menu == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)menu).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
