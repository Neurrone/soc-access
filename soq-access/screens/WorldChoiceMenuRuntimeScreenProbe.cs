using System.Collections.Generic;
using System;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class WorldChoiceMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(WorldChoiceMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            WorldChoiceMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<WorldChoiceMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                WorldChoiceMenu menu = TryResolveWorldChoiceMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                WorldChoiceMenuAdapter adapter = new WorldChoiceMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    screens.Add(new WorldChoiceMenuScreen(adapter));
                }
            }
        }

        private static WorldChoiceMenu TryResolveWorldChoiceMenu(WorldChoiceMenuInstaller installer)
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
                return container.Resolve<WorldChoiceMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(WorldChoiceMenuInstaller installer)
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
