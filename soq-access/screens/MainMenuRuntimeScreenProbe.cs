using System.Collections.Generic;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MainMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            MainMenuAdapter adapter = FindActiveMainMenu();
            if (adapter == null)
            {
                return;
            }

            screens.Add(new MainMenuScreen(adapter));
            if (adapter.ExtrasFoldout != null && adapter.ExtrasFoldout.IsOpen())
            {
                screens.Add(new FoldoutMenuScreen(adapter, adapter.ExtrasFoldout));
                return;
            }

            if (adapter.MultiplayerFoldout != null && adapter.MultiplayerFoldout.IsOpen())
            {
                screens.Add(new FoldoutMenuScreen(adapter, adapter.MultiplayerFoldout));
            }
        }

        private static MainMenuAdapter FindActiveMainMenu()
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

        private static bool IsLiveSceneMainMenu(MainMenu mainMenu)
        {
            if (mainMenu == null)
            {
                return false;
            }

            GameObject gameObject = mainMenu.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
