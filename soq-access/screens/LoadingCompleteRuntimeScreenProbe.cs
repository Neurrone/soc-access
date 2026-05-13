using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class LoadingCompleteRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(LoadingScreenMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            LoadingCompleteScreen screen = FindActiveLoadingCompleteScreen();
            if (screen != null)
            {
                screens.Add(screen);
            }
        }

        private static LoadingCompleteScreen FindActiveLoadingCompleteScreen()
        {
            LoadingScreenMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<LoadingScreenMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                LoadingScreenMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                LoadingScreenMenu menu = TryResolve<LoadingScreenMenu>(GetContainer(installer));
                LoadingScreenAdapter adapter = new LoadingScreenAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new LoadingCompleteScreen(adapter);
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(LoadingScreenMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(LoadingScreenMenuInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static T TryResolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}
