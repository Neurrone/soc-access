using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class HostileJoinMenuRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(HostileJoinMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            HostileJoinMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<HostileJoinMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                HostileJoinMenu menu = TryResolveHostileJoinMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    screens.Add(new HostileJoinMenuScreen(adapter));
                }
                else
                {
                    adapter.Dispose();
                }
            }
        }

        private static HostileJoinMenu TryResolveHostileJoinMenu(HostileJoinMenuInstaller installer)
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
                return container.Resolve<HostileJoinMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(HostileJoinMenuInstaller installer)
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
