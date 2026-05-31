using SongsOfConquest.Client.Menu.Main;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MainMenuScreen : Screen
    {
        private static readonly string[] TopLevelItemIds =
        {
            "continue",
            "campaign",
            "skirmish",
            "load-game",
            // Map editor is not accessible yet, so do not expose it in the main menu.
            // "map-editor",
            "community-maps",
            "extras",
            "hotseat",
            "multiplayer",
            "quit"
        };

        private readonly MainMenuAdapter _adapter;

        public MainMenuScreen(MainMenuAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            MainMenuAdapter adapter = FindActiveMainMenu();
            return adapter != null ? new MainMenuScreen(adapter) : null;
        }

        internal static MainMenuAdapter FindActiveMainMenu()
        {
            MainMenu[] mainMenus = Resources.FindObjectsOfTypeAll<MainMenu>();
            for (int i = 0; i < mainMenus.Length; i++)
            {
                MainMenu mainMenu = mainMenus[i];
                if (!IsLiveSceneMainMenu(mainMenu))
                {
                    continue;
                }

                MainMenuAdapter adapter = new MainMenuAdapter(mainMenu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        private static ContainerWidget BuildRootWidget(MainMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("main-menu-screen", ModText.Get(ModStrings.Screens.MainMenu));
            MenuWidget menu = new MenuWidget("main-menu", ModText.Get(ModStrings.Screens.MainMenu));
            if (adapter == null)
            {
                root.AddChild(menu);
                return root;
            }

            AddItems(menu, adapter.TopLevelItems);
            root.AddChild(menu);
            AddOptionalButton(root, "options", adapter.OptionsButton);
            return root;
        }

        private static bool IsLiveSceneMainMenu(MainMenu mainMenu)
        {
            if (mainMenu == null)
            {
                return false;
            }

            GameObject gameObject = mainMenu.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static void AddItems(MenuWidget root, System.Collections.Generic.IReadOnlyList<IMenuButtonAdapter> items)
        {
            if (root == null || items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                IMenuButtonAdapter item = items[i];
                if (item == null)
                {
                    continue;
                }

                root.AddItem(new MenuItemWidget(
                    GetTopLevelItemId(i),
                    item.GetLabel,
                    () => BuildMenuButtonStatus(item),
                    item.Activate,
                    null,
                    item.IsVisible));
            }
        }

        private static string GetTopLevelItemId(int index)
        {
            return index >= 0 && index < TopLevelItemIds.Length
                ? TopLevelItemIds[index]
                : "main-menu-item-" + index;
        }

        private static void AddOptionalButton(ContainerWidget root, string id, IMenuButtonAdapter button)
        {
            if (root == null || button == null)
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                "main-menu-" + id,
                button.GetLabel,
                button.Activate,
                null,
                button.IsEnabled,
                button.IsVisible));
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
    }
}
