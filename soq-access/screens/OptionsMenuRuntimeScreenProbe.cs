using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class OptionsMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(OptionsMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            OptionsMenu menu = FindActiveOptionsMenu();
            if (menu == null)
            {
                return;
            }

            OptionsMenuAdapter adapter = new OptionsMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                screens.Add(new OptionsScreen(adapter));
            }
        }

        private static OptionsMenu FindActiveOptionsMenu()
        {
            OptionsMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<OptionsMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                OptionsMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                OptionsMenu menu = TryResolve<OptionsMenu>(installer);
                if (menu == null)
                {
                    continue;
                }

                OptionsMenuAdapter adapter = new OptionsMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return menu;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(OptionsMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static T TryResolve<T>(OptionsMenuInstaller installer) where T : class
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
