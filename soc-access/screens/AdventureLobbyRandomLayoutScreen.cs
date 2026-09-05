using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class AdventureLobbyRandomLayoutScreen : Screen
    {
        private const int LayoutMenuIndex = 0;
        private const int VariantMenuIndex = 4;

        private readonly AdventureLobbyRandomLayoutAdapter _adapter;

        public AdventureLobbyRandomLayoutScreen(AdventureLobbyRandomLayoutAdapter adapter)
            : base(BuildRootWidget(adapter, null))
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

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null
                    && _adapter.BackButton != null
                    && _adapter.BackButton.Activate();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh(bool announceFocus)
        {
            if (!IsPresent())
            {
                return;
            }

            FocusState focusState = CaptureFocusState();
            RootWidget = BuildRootWidget(_adapter, focusState);
            if (announceFocus)
            {
                UIManager.RequestFocus(RootWidget);
            }
            else
            {
                UIManager.RequestFocusSilently(RootWidget);
            }
        }

        private FocusState CaptureFocusState()
        {
            Widget focusedChild = RootWidget != null ? RootWidget.FocusedChild : null;
            MenuWidget menu = focusedChild as MenuWidget;
            MenuItemWidget item = menu != null ? menu.FocusedItem : null;
            return new FocusState(
                focusedChild != null ? focusedChild.Id : null,
                item != null ? item.Id : null);
        }

        private static ContainerWidget BuildRootWidget(AdventureLobbyRandomLayoutAdapter adapter, FocusState focusState)
        {
            ContainerWidget root = new ContainerWidget(
                "adventure-lobby-random-layout-screen",
                adapter != null ? adapter.Title : string.Empty);

            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildLayoutMenu(adapter, focusState));
            AddSelectedLayoutSettings(root, adapter, focusState);
            AddOptionalButton(root, "confirm", adapter.ConfirmButton);
            AddOptionalButton(root, "back", adapter.BackButton);
            AddOptionalButton(root, "options", adapter.OptionsButton);

            if (focusState != null && !string.IsNullOrWhiteSpace(focusState.RootChildId))
            {
                root.SetFocusedChildById(focusState.RootChildId);
            }

            return root;
        }

        private static MenuWidget BuildLayoutMenu(AdventureLobbyRandomLayoutAdapter adapter, FocusState focusState)
        {
            MenuWidget menu = new MenuWidget("random-layouts", adapter != null ? adapter.Title : string.Empty);
            IReadOnlyList<AdventureLobbyRandomLayoutAdapter.RandomLayoutItem> layouts = adapter != null
                ? adapter.GetLayouts()
                : new AdventureLobbyRandomLayoutAdapter.RandomLayoutItem[0];

            for (int i = 0; i < layouts.Count; i++)
            {
                AdventureLobbyRandomLayoutAdapter.RandomLayoutItem layout = layouts[i];
                if (layout == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    "random-layout-" + layout.Id,
                    () => BuildLayoutLabel(layout),
                    () => layout.IsSelected ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    layout.Activate,
                    layout.FocusNative,
                    () => true));
            }

            if (focusState != null
                && focusState.RootChildId == menu.Id
                && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
            {
                menu.SetFocusedItemById(focusState.MenuItemId);
            }
            else
            {
                SetFocusedSelectedLayout(menu, layouts);
            }

            return menu;
        }

        private static void SetFocusedSelectedLayout(
            MenuWidget menu,
            IReadOnlyList<AdventureLobbyRandomLayoutAdapter.RandomLayoutItem> layouts)
        {
            if (menu == null || layouts == null)
            {
                return;
            }

            for (int i = 0; i < layouts.Count; i++)
            {
                AdventureLobbyRandomLayoutAdapter.RandomLayoutItem layout = layouts[i];
                if (layout != null && layout.IsSelected)
                {
                    menu.SetFocusedItemById("random-layout-" + layout.Id);
                    return;
                }
            }
        }

        private static string BuildLayoutLabel(AdventureLobbyRandomLayoutAdapter.RandomLayoutItem layout)
        {
            if (layout == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddIfNotEmpty(parts, layout.Title);
            AddIfNotEmpty(parts, layout.Description);

            return ModText.JoinList(parts);
        }

        private static void AddSelectedLayoutSettings(
            ContainerWidget root,
            AdventureLobbyRandomLayoutAdapter adapter,
            FocusState focusState)
        {
            AdventureLobbyRandomLayoutAdapter.RandomLayoutItem selected = adapter != null ? adapter.SelectedLayout : null;
            if (root == null || selected == null)
            {
                return;
            }

            IReadOnlyList<AdventureLobbyRandomLayoutAdapter.WinConditionToggleItem> toggles = selected.GetWinConditionToggles();
            for (int i = 0; i < toggles.Count; i++)
            {
                AdventureLobbyRandomLayoutAdapter.WinConditionToggleItem toggle = toggles[i];
                if (toggle == null)
                {
                    continue;
                }

                root.AddChild(new CheckboxWidget(
                    "random-layout-win-condition-" + toggle.Id,
                    () => toggle.Label,
                    toggle.Toggle,
                    () => toggle.IsChecked,
                    () => toggle.IsVisible,
                    () => toggle.IsEnabled,
                    toggle.GetTooltip));
            }

            AdventureLobbyRandomLayoutAdapter.LayoutDropdownItem dropdown = selected.GetLayoutDropdown();
            if (dropdown != null)
            {
                root.AddChild(BuildVariantMenu(dropdown, focusState));
            }
        }

        private static MenuWidget BuildVariantMenu(
            AdventureLobbyRandomLayoutAdapter.LayoutDropdownItem dropdown,
            FocusState focusState)
        {
            IReadOnlyList<string> options = dropdown != null
                ? dropdown.GetOptions()
                : new string[0];
            int selectedValue = dropdown != null ? dropdown.Value : 0;
            MenuWidget menu = new MenuWidget("random-layout-variant", ModText.Get(ModStrings.Screens.Layout), () => dropdown != null && dropdown.IsVisible);

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    "random-layout-variant-" + index,
                    () => options[index],
                    () => dropdown != null && dropdown.Value == index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () => dropdown != null && dropdown.SetValue(index),
                    () =>
                    {
                        if (dropdown != null)
                        {
                            dropdown.Focus();
                            if (dropdown.Value != index)
                            {
                                dropdown.SetValue(index);
                            }
                        }
                    },
                    () => true,
                    dropdown != null ? (System.Func<Tooltip>)dropdown.GetTooltip : null,
                    null,
                    () => dropdown != null && dropdown.IsEnabled));
            }

            if (focusState != null
                && focusState.RootChildId == menu.Id
                && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
            {
                menu.SetFocusedItemById(focusState.MenuItemId);
            }
            else
            {
                menu.SetFocusedItemById("random-layout-variant-" + selectedValue);
            }

            return menu;
        }

        private static void AddOptionalButton(ContainerWidget root, string id, IMenuButtonAdapter button)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                id,
                button.GetLabel,
                button.Activate,
                () => FocusNativeButton(button.Button),
                button.IsEnabled,
                button.IsVisible));
        }

        private static void FocusNativeButton(UIButton button)
        {
            if (button == null)
            {
                return;
            }

            NativeSelectionUtility.Select(button);
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

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        private sealed class FocusState
        {
            public FocusState(string rootChildId, string menuItemId)
            {
                RootChildId = rootChildId;
                MenuItemId = menuItemId;
            }

            public string RootChildId { get; private set; }

            public string MenuItemId { get; private set; }
        }
    }
}
