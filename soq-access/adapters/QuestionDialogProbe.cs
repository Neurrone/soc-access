using System;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class QuestionDialogProbe
    {
        private static readonly AccessTools.FieldRef<PopupMenu, PopupMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<PopupMenu, PopupMenu.Settings>("_settings");
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(PopupMenuInstaller), "Container");

        public QuestionDialogAdapter FindActiveQuestionDialog()
        {
            PopupMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<PopupMenuInstaller>();
            QuestionDialogAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < installers.Length; i++)
            {
                PopupMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                PopupMenu popupMenu = TryResolvePopupMenu(installer);
                if (popupMenu == null)
                {
                    continue;
                }

                PopupMenu.Settings settings = null;
                try
                {
                    settings = SettingsRef(popupMenu);
                }
                catch (Exception)
                {
                    settings = null;
                }

                if (settings == null)
                {
                    continue;
                }

                // Rebuild directly from the live popup settings on every probe pass.
                // The dialog's effective text is recovered by QuestionDialogAdapter from
                // UITextMesh internals, so we do not need a separate cached-arguments or
                // persisted-snapshot workaround here anymore.
                QuestionDialogAdapter adapter = new QuestionDialogAdapter(popupMenu, settings);
                if (adapter.IsPresent())
                {
                    int siblingIndex = GetPopupSiblingIndex(settings);
                    if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                    {
                        bestAdapter = adapter;
                        bestSiblingIndex = siblingIndex;
                    }
                }
            }

            return bestAdapter;
        }

        private static bool IsLiveSceneInstaller(PopupMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static PopupMenu TryResolvePopupMenu(PopupMenuInstaller installer)
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
                return container.Resolve<PopupMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int GetPopupSiblingIndex(PopupMenu.Settings settings)
        {
            if (settings == null || settings.TopContainer == null)
            {
                return int.MinValue;
            }

            return settings.TopContainer.GetSiblingIndex();
        }
    }
}
