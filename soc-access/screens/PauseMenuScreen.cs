using System;
using System.Collections.Generic;
using System.Reflection;
using _8_UILayer.ClientView.Menu.Paus;
using HarmonyLib;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PauseMenuScreen : Screen
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(PauseMenuInstaller), "Container");

        private readonly PauseMenuAdapter _adapter;

        public PauseMenuScreen(PauseMenuAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PauseMenu pauseMenu = FindActivePauseMenu();
            if (pauseMenu == null)
            {
                return null;
            }

            PauseMenuAdapter adapter = new PauseMenuAdapter(pauseMenu);
            return adapter.IsPresent() ? new PauseMenuScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        private static ContainerWidget BuildRootWidget(PauseMenuAdapter adapter)
        {
            string title = adapter != null && !string.IsNullOrWhiteSpace(adapter.Title)
                ? adapter.Title
                : string.Empty;
            ContainerWidget root = new ContainerWidget("pause-menu-screen", title);
            MenuWidget menu = new MenuWidget("pause-menu", title);

            if (adapter != null)
            {
                AddItems(menu, adapter.Items);
            }

            root.AddChild(menu);
            return root;
        }

        private static void AddItems(MenuWidget menu, IReadOnlyList<PauseMenuAdapter.Item> items)
        {
            if (menu == null || items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PauseMenuAdapter.Item item = items[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    item.Id,
                    item.GetLabel,
                    item.GetStatus,
                    item.Activate,
                    item.Select,
                    item.IsVisible,
                    (Tooltip)null));
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
