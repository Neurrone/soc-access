using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class AdventureLobbyPlayerSettingsScreen : Screen
    {
        private readonly AdventureLobbyPlayerSettingsAdapter _adapter;

        public AdventureLobbyPlayerSettingsScreen(AdventureLobbyPlayerSettingsAdapter adapter)
            : base(BuildRoot(adapter))
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

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot(_adapter);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
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

        private static ContainerWidget BuildRoot(AdventureLobbyPlayerSettingsAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "adventure-lobby-player-settings",
                adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            AddContent(root, adapter);

            AdventureLobbyPlayerSettingsAdapter.ButtonItem cancelButton = adapter.GetCancelButton();
            if (cancelButton != null)
            {
                root.AddChild(BuildButton(cancelButton));
            }

            AdventureLobbyPlayerSettingsAdapter.ButtonItem confirmButton = adapter.GetConfirmButton();
            if (confirmButton != null)
            {
                root.AddChild(BuildButton(confirmButton));
            }

            return root;
        }

        private static void AddContent(ContainerWidget root, AdventureLobbyPlayerSettingsAdapter adapter)
        {
            IReadOnlyList<AdventureLobbyPlayerSettingsAdapter.ControlItem> controls = adapter.GetContentControls();
            for (int i = 0; i < controls.Count; i++)
            {
                AdventureLobbyPlayerSettingsAdapter.ControlItem control = controls[i];
                object item = control != null ? control.Item : null;

                AdventureLobbyPlayerSettingsAdapter.TextItem text = item as AdventureLobbyPlayerSettingsAdapter.TextItem;
                if (text != null)
                {
                    root.AddChild(new TextWidget(
                        text.Id,
                        text.GetText,
                        null,
                        includeParentLabelInAnnouncement: false,
                        isVisible: () => text.IsVisible() && !string.IsNullOrWhiteSpace(text.GetText())));
                    continue;
                }

                AdventureLobbyPlayerSettingsAdapter.ToggleItem toggle = item as AdventureLobbyPlayerSettingsAdapter.ToggleItem;
                if (toggle != null)
                {
                    root.AddChild(new CheckboxWidget(
                        toggle.Id,
                        toggle.GetLabel,
                        toggle.Toggle,
                        toggle.IsChecked,
                        toggle.IsVisible,
                        toggle.IsEnabled,
                        toggle.GetTooltip));
                    continue;
                }

                AdventureLobbyPlayerSettingsAdapter.SliderItem slider = item as AdventureLobbyPlayerSettingsAdapter.SliderItem;
                if (slider != null)
                {
                    root.AddChild(new SliderWidget(
                        slider.Id,
                        slider.GetLabel,
                        slider.GetValueText,
                        slider.GetValue,
                        slider.GetMinimumValue,
                        slider.GetMaximumValue,
                        slider.GetStep,
                        slider.SetValue,
                        slider.IsEnabled,
                        slider.IsVisible,
                        slider.GetTooltip));
                    continue;
                }

                AdventureLobbyPlayerSettingsAdapter.ButtonItem button = item as AdventureLobbyPlayerSettingsAdapter.ButtonItem;
                if (button != null)
                {
                    root.AddChild(BuildButton(button));
                }
            }
        }

        private static ButtonWidget BuildButton(AdventureLobbyPlayerSettingsAdapter.ButtonItem button)
        {
            return new ButtonWidget(
                button.Id,
                button.GetLabel,
                button.Activate,
                button.Focus,
                button.IsEnabled,
                button.IsVisible,
                button.GetTooltip);
        }
    }
}
