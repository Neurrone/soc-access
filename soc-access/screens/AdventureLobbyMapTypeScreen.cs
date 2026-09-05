using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class AdventureLobbyMapTypeScreen : Screen
    {
        private readonly AdventureLobbyMapTypeAdapter _adapter;

        public AdventureLobbyMapTypeScreen(AdventureLobbyMapTypeAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyMapTypeAdapter adapter = FindActiveMapTypeMenu();
            return adapter != null ? new AdventureLobbyMapTypeScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
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

        private static ContainerWidget BuildRootWidget(AdventureLobbyMapTypeAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "adventure-lobby-map-type-screen",
                adapter != null ? adapter.GetTitle() : string.Empty);
            MenuWidget menu = new MenuWidget("map-type-menu", adapter != null ? adapter.GetTitle() : string.Empty);
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddMenuItem(menu, "all-maps", adapter.AllMapsButton);
            AddMenuItem(menu, "challenge-maps", adapter.ChallengeMapsButton);
            AddMenuItem(menu, "random-maps", adapter.RandomMapsButton);
            root.AddChild(menu);
            AddOptionalButton(root, "options", adapter.OptionsButton);
            AddOptionalButton(root, "back", adapter.BackButton);
            return root;
        }

        private static void AddMenuItem(MenuWidget menu, string id, IMenuButtonAdapter button)
        {
            if (menu == null || button == null)
            {
                return;
            }

            menu.AddItem(new MenuItemWidget(
                id,
                button.GetLabel,
                () => BuildMenuButtonStatus(button),
                button.Activate,
                () => FocusNativeButton(button.Button),
                button.IsVisible));
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

        private static string BuildMenuButtonStatus(IMenuButtonAdapter item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string nativeStatus = item.GetStatus();
            if (item.IsEnabled())
            {
                return nativeStatus;
            }

            return string.IsNullOrWhiteSpace(nativeStatus)
                ? ModText.Get(ModStrings.UI.StatusDisabled)
                : ModText.Get(ModStrings.Screens.DisabledWithReason, ModText.Get(ModStrings.UI.StatusDisabled), nativeStatus);
        }

        private static AdventureLobbyMapTypeAdapter FindActiveMapTypeMenu()
        {
            MapTypeMenu[] menus = Resources.FindObjectsOfTypeAll<MapTypeMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapTypeMenu menu = menus[i];
                if (!IsLiveSceneMapTypeMenu(menu))
                {
                    continue;
                }

                AdventureLobbyMapTypeAdapter adapter = new AdventureLobbyMapTypeAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneMapTypeMenu(MapTypeMenu menu)
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
