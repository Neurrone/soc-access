using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class WorldConfirmMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(WorldConfirmMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            WorldConfirmMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<WorldConfirmMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                WorldConfirmMenu menu = TryResolveWorldConfirmMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                WorldConfirmMenuAdapter adapter = new WorldConfirmMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    screens.Add(new WorldConfirmMenuScreen(adapter));
                }
            }
        }

        private static WorldConfirmMenu TryResolveWorldConfirmMenu(WorldConfirmMenuInstaller installer)
        {
            if (!IsLiveSceneInstaller(installer) || InstallerContainerProperty == null)
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
                return container.Resolve<WorldConfirmMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(WorldConfirmMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
