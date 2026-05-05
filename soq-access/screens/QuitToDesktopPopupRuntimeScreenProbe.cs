using System.Collections.Generic;
using System;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class QuitToDesktopPopupRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(QuitToDesktopPopupInstaller), "Container");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            QuitToDesktopPopupInstaller[] installers = Resources.FindObjectsOfTypeAll<QuitToDesktopPopupInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                QuitToDesktopPopupInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                QuitToDesktopPopup popup = TryResolvePopup(installer);
                if (popup == null)
                {
                    continue;
                }

                QuitToDesktopPopupAdapter adapter = new QuitToDesktopPopupAdapter(popup);
                if (adapter.IsPresent())
                {
                    screens.Add(new QuitToDesktopPopupScreen(adapter));
                    return;
                }
            }
        }

        private static bool IsLiveSceneInstaller(QuitToDesktopPopupInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static QuitToDesktopPopup TryResolvePopup(QuitToDesktopPopupInstaller installer)
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
                return container.Resolve<QuitToDesktopPopup>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
