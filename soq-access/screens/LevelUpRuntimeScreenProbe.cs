using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class LevelUpRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(CommanderLevelUpMenuInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            CommanderLevelUpMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CommanderLevelUpMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CommanderLevelUpMenu menu = TryResolveLevelUpMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                LevelUpMenuAdapter adapter = new LevelUpMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    screens.Add(new LevelUpScreen(adapter));
                }
            }
        }

        private static CommanderLevelUpMenu TryResolveLevelUpMenu(CommanderLevelUpMenuInstaller installer)
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
                return container.Resolve<CommanderLevelUpMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(CommanderLevelUpMenuInstaller installer)
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
