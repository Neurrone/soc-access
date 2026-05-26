using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureLobbyGameSettingsScreen : Screen
    {
        private readonly AdventureLobbyGameSettingsAdapter _adapter;

        public AdventureLobbyGameSettingsScreen(AdventureLobbyGameSettingsAdapter adapter)
            : base(BuildRoot(adapter))
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

        internal static LobbyMapSettingsMenu FindActiveMenu(LobbyMapSettingsMenu targetMenu)
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

        private static ContainerWidget BuildRoot(AdventureLobbyGameSettingsAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "adventure-lobby-game-settings",
                adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            AddContent(root, adapter);

            AdventureLobbyGameSettingsAdapter.ButtonItem cancelButton = adapter.GetCancelButton();
            if (cancelButton != null)
            {
                root.AddChild(BuildButton(cancelButton));
            }

            AdventureLobbyGameSettingsAdapter.ButtonItem applyButton = adapter.GetApplyButton();
            if (applyButton != null)
            {
                root.AddChild(BuildButton(applyButton));
            }

            return root;
        }

        private static void AddContent(ContainerWidget root, AdventureLobbyGameSettingsAdapter adapter)
        {
            IReadOnlyList<AdventureLobbyGameSettingsAdapter.ControlItem> controls = adapter.GetContentControls();
            for (int i = 0; i < controls.Count; i++)
            {
                AdventureLobbyGameSettingsAdapter.ControlItem control = controls[i];
                object item = control != null ? control.Item : null;

                AdventureLobbyGameSettingsAdapter.TextItem text = item as AdventureLobbyGameSettingsAdapter.TextItem;
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

                AdventureLobbyGameSettingsAdapter.DropdownItem dropdown = item as AdventureLobbyGameSettingsAdapter.DropdownItem;
                if (dropdown != null)
                {
                    root.AddChild(BuildDropdown(dropdown));
                    continue;
                }

                AdventureLobbyGameSettingsAdapter.ToggleItem toggle = item as AdventureLobbyGameSettingsAdapter.ToggleItem;
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

                AdventureLobbyGameSettingsAdapter.TextInputItem input = item as AdventureLobbyGameSettingsAdapter.TextInputItem;
                if (input != null)
                {
                    root.AddChild(new TextInputWidget(
                        input.Id,
                        input.GetLabel(),
                        input.GetField,
                        null,
                        input.Focus,
                        input.IsEnabled,
                        input.IsVisible,
                        input.GetTooltip));
                    continue;
                }

                AdventureLobbyGameSettingsAdapter.TimeInputItem timeInput = item as AdventureLobbyGameSettingsAdapter.TimeInputItem;
                if (timeInput != null)
                {
                    root.AddChild(new TimeInputTextWidget(
                        timeInput.Id,
                        timeInput.GetLabel(),
                        timeInput.GetField,
                        timeInput.GetMinutesField,
                        timeInput.GetSecondsField,
                        timeInput.Focus,
                        timeInput.IsEnabled,
                        timeInput.IsVisible,
                        timeInput.GetTooltip));
                    continue;
                }

                AdventureLobbyGameSettingsAdapter.ButtonItem button = item as AdventureLobbyGameSettingsAdapter.ButtonItem;
                if (button != null)
                {
                    root.AddChild(BuildButton(button));
                }
            }
        }

        private static MenuWidget BuildDropdown(AdventureLobbyGameSettingsAdapter.DropdownItem dropdown)
        {
            MenuWidget menu = new MenuWidget(dropdown.Id, dropdown.GetLabel(), dropdown.IsVisible);
            IReadOnlyList<string> options = dropdown.GetOptions();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    dropdown.Id + "-option-" + index,
                    () => options[index],
                    () => dropdown.GetValue() == index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () => dropdown.SetValue(index),
                    () =>
                    {
                        dropdown.Focus?.Invoke();
                        if (dropdown.GetValue() != index)
                        {
                            dropdown.SetValue(index);
                        }
                    },
                    () => true,
                    dropdown.GetTooltip,
                    null,
                    dropdown.IsEnabled));
            }

            menu.SetFocusedItemById(dropdown.Id + "-option-" + dropdown.GetValue());
            return menu;
        }

        private static ButtonWidget BuildButton(AdventureLobbyGameSettingsAdapter.ButtonItem button)
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
