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
    /// Measured 2026-09-06 at 1280x800 (<c>/gui/unity</c>): four cards side by side at x 187, 417,
    /// 648 and 878, each drawing a player count ("2 Players"), a paragraph of description, three
    /// win-condition toggles and a layout dropdown; Confirm at [562,662]; the lobby's Back at
    /// [21,20] and Options at [1233,11] in the header band, where the page's title is drawn too
    /// ("Select layout", set by <c>LobbyNavigation</c> from <c>Lobby/RandomMapPopup/Header</c>).
    ///
    /// A card is a RADIO BUTTON, not a button: exactly one of the four is in force at a time (the
    /// menu's own <c>SetSelectedEntry</c>), picking one is not yet doing anything, and Confirm is
    /// what does.
    ///
    /// ARRIVING ON A CARD DOES NOT CHOOSE IT (owner ruling K, 2026-09-06). The card's selectable is
    /// what the game watches: <c>LobbyRandomMapPreviewEntry.Awake</c> hangs a <c>UISelectionProxy</c>
    /// on the card's button and that proxy's <c>OnSelect</c> runs the menu's <c>SetSelectedEntry</c>,
    /// so the mod's old focus visual - a native selection on every arrival - silently changed the
    /// choice as the player walked the row, and only the card the page opened on ever said
    /// "selected". The cards therefore declare NO <c>OnFocusVisual</c>, and there is no hover
    /// highlight to put in its place: <c>UIButton.OnPointerEnter</c> (decompiled, line 328) only
    /// plays the hover sound and invokes <c>OnHoverEnter</c>, which nothing on the card subscribes
    /// to. Enter chooses through the card's own selection path and the live "selected" part says
    /// which card is in force, so Up and Down read the choice without changing it. All four cards
    /// are on screen at 1280x800, so nothing is lost by the page's <c>AutoScrollToSelected</c> no
    /// longer being nudged on arrival.
    ///
    /// The rows stop lands on the chosen card, and because the cards are the FIRST stop that landing
    /// also needs <c>SetStart</c>: the first seating is the start node's, not
    /// <see cref="InitialFocusStop"/>'s.
    ///
    /// The win-condition toggles and the layout dropdown are the SELECTED card's, as they were for
    /// the widget screen: the other three cards draw their own copies, but they are the settings of
    /// maps the player has not chosen. They read after the cards, which is where the selected card
    /// draws them. The dropdown is a real <c>UITextMeshDropdown</c>, so it is a combo box opening
    /// <see cref="DropListScreen"/> over the game's own popup.
    ///
    /// Three REGIONS, one per band. The page draws no caption over any of them, so each is named
    /// with the game's own word for what it holds where the game has one - <c>Common/Players</c>
    /// ("Players") and <c>Campaign/MapSelect/InformationView/WinConditionsHeader</c> ("Win
    /// Conditions"), both read out of the live localization table on 2026-09-06 - and with the mod's
    /// existing <c>Screens.Layout</c> where it has none.
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
        private const string PlayersRegion = "random-layout-players";
        private const string WinConditionsRegion = "random-layout-win-conditions";
        private const string LayoutRegion = "random-layout-variant";

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
            ControlId landing = null;
            builder.PushContext(GameText.Get("Common/Players", "Players"));
            builder.SetRegion(PlayersRegion);
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
                ControlId id = ControlId.For(subject, "random-layout:card/" + layout.Id);
                builder.AddItem(new DrawnNode(id, vtable, subject));
                if (layout.IsSelected)
                {
                    landing = id;
                }
            }

            builder.SetRegion(null);
            builder.PopContext();

            // The card in force, so Tab into the page - and the very first seating, which is the
            // start node's business rather than the stop's because the cards are the first stop -
            // both land on the choice the player would otherwise have to walk the row to find.
            builder.LandStopOn(landing);
            if (landing != null)
            {
                builder.SetStart(landing);
            }
        }

        private void BuildSelectedLayoutSettings(GraphBuilder builder)
        {
            AdventureLobbyRandomLayoutAdapter.RandomLayoutItem selected = _adapter.SelectedLayout;
            if (selected == null)
            {
                return;
            }

            BuildWinConditions(builder, selected);
            BuildLayoutDropdown(builder, selected);
        }

        private void BuildWinConditions(GraphBuilder builder, AdventureLobbyRandomLayoutAdapter.RandomLayoutItem selected)
        {
            IReadOnlyList<AdventureLobbyRandomLayoutAdapter.WinConditionToggleItem> toggles =
                selected.GetWinConditionToggles();
            bool opened = false;
            for (int i = 0; i < toggles.Count; i++)
            {
                AdventureLobbyRandomLayoutAdapter.WinConditionToggleItem toggle = toggles[i];
                Component subject = toggle != null ? toggle.Subject : null;
                if (subject == null || !toggle.IsVisible)
                {
                    continue;
                }

                if (!opened)
                {
                    builder.PushContext(GameText.Get(
                        "Campaign/MapSelect/InformationView/WinConditionsHeader",
                        "Win Conditions"));
                    builder.SetRegion(WinConditionsRegion);
                    opened = true;
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

            if (opened)
            {
                builder.SetRegion(null);
                builder.PopContext();
            }
        }

        private void BuildLayoutDropdown(GraphBuilder builder, AdventureLobbyRandomLayoutAdapter.RandomLayoutItem selected)
        {
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
            builder.PushContext(label());
            builder.SetRegion(LayoutRegion);
            builder.AddItem(new DrawnNode(
                ControlId.For(dropdownSubject, "random-layout:" + dropdown.Id),
                combo,
                dropdownSubject));
            builder.SetRegion(null);
            builder.PopContext();
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
