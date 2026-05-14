using System;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class QuitToDesktopPopupScreen : Screen
    {
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(QuitToDesktopPopupInstaller), "Container");

        private readonly QuitToDesktopPopupAdapter _adapter;

        public QuitToDesktopPopupScreen(QuitToDesktopPopupAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
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
                    return new QuitToDesktopPopupScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.HasCancel && _adapter.ActivateCancel();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(QuitToDesktopPopupAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            ContainerWidget root = new ContainerWidget("quit-to-desktop-popup", title);

            root.AddChild(new TextWidget(
                "follow-title",
                () => adapter != null ? adapter.FollowTitle : string.Empty,
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SelectBody();
                    }
                },
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter != null && adapter.HasSteamFollow));

            root.AddChild(new ButtonWidget(
                "follow-steam",
                adapter != null ? adapter.SteamFollowLabel : string.Empty,
                () => adapter != null && adapter.ActivateSteamFollow(),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SelectSteamFollow();
                    }
                },
                () => adapter != null && adapter.HasSteamFollow,
                () => adapter != null && adapter.HasSteamFollow));

            root.AddChild(new TextWidget(
                "description",
                () => adapter != null ? adapter.Description : string.Empty,
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SelectBody();
                    }
                },
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "confirm",
                adapter != null ? adapter.ConfirmLabel : string.Empty,
                () => adapter != null && adapter.ActivateConfirm(),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SelectConfirm();
                    }
                },
                () => adapter != null && adapter.HasConfirm,
                () => adapter != null && adapter.HasConfirm));

            root.AddChild(new ButtonWidget(
                "cancel",
                adapter != null ? adapter.CancelLabel : string.Empty,
                () => adapter != null && adapter.ActivateCancel(),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SelectCancel();
                    }
                },
                () => adapter != null && adapter.HasCancel,
                () => adapter != null && adapter.HasCancel));

            return root;
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
