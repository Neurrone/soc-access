using System;
using System.Collections.Generic;
using System.Reflection;
using _8_UILayer.ClientView.Menu.Paus;
using HarmonyLib;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PauseMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(PauseMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            PauseMenu pauseMenu = FindActivePauseMenu();
            if (pauseMenu == null)
            {
                return;
            }

            PauseMenuAdapter adapter = new PauseMenuAdapter(pauseMenu);
            if (adapter.IsPresent())
            {
                screens.Add(new PauseMenuScreen(adapter));
            }
        }

        private static PauseMenu FindActivePauseMenu()
        {
            PauseMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<PauseMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                PauseMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                PauseMenu pauseMenu = TryResolve<PauseMenu>(installer);
                if (pauseMenu == null)
                {
                    continue;
                }

                PauseMenuAdapter adapter = new PauseMenuAdapter(pauseMenu);
                if (adapter.IsPresent())
                {
                    return pauseMenu;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(PauseMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static T TryResolve<T>(PauseMenuInstaller installer) where T : class
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
