using System;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MessageDialogScreen : Screen
    {
        private static readonly AccessTools.FieldRef<PopupMenu, PopupMenu.Settings> PopupSettingsRef =
            AccessTools.FieldRefAccess<PopupMenu, PopupMenu.Settings>("_settings");
        private static readonly System.Reflection.PropertyInfo PopupInstallerContainerProperty =
            AccessTools.Property(typeof(PopupMenuInstaller), "Container");
        private static readonly System.Reflection.PropertyInfo RandomEventInstallerContainerProperty =
            AccessTools.Property(typeof(RandomEventMenuInstaller), "Container");

        private readonly IMessageDialogAdapter _adapter;
        private readonly IInputDialogAdapter _inputAdapter;
        private readonly Action<IUITextMeshInputField, string> _inputSubmitHandler;

        public MessageDialogScreen(IMessageDialogAdapter adapter)
            : base(BuildRootWidget(adapter))
        {
            _adapter = adapter;
            _inputAdapter = adapter as IInputDialogAdapter;
            if (_inputAdapter != null)
            {
                _inputSubmitHandler = HandleInputSubmit;
                _inputAdapter.AttachInputSubmit(_inputSubmitHandler);
            }
        }

        public static Screen TryBuildActiveMapMessagePopupScreen()
        {
            MapMessagePopup[] popups = Resources.FindObjectsOfTypeAll<MapMessagePopup>();
            for (int i = 0; i < popups.Length; i++)
            {
                MapMessagePopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                MapMessagePopupAdapter adapter = new MapMessagePopupAdapter(popup);
                if (adapter.IsPresent())
                {
                    return new MessageDialogScreen(adapter);
                }
            }

            return null;
        }

        public static Screen TryBuildActiveRandomEventMenuScreen()
        {
            RandomEventMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<RandomEventMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                RandomEventMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                RandomEventMenu menu = TryResolveRandomEventMenu(installer);
                if (menu == null)
                {
                    continue;
                }

                RandomEventMenuAdapter adapter = new RandomEventMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new MessageDialogScreen(adapter);
                }
            }

            return null;
        }

        public static Screen TryBuildActivePopupMenuScreen()
        {
            PopupMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<PopupMenuInstaller>();
            PopupMenuAdapter bestAdapter = null;
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
                    settings = PopupSettingsRef(popupMenu);
                }
                catch (Exception)
                {
                    settings = null;
                }

                if (settings == null)
                {
                    continue;
                }

                PopupMenuAdapter adapter = new PopupMenuAdapter(popupMenu, settings);
                if (!adapter.IsPresent())
                {
                    continue;
                }

                int siblingIndex = GetPopupSiblingIndex(settings);
                if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                {
                    bestAdapter = adapter;
                    bestSiblingIndex = siblingIndex;
                }
            }

            return bestAdapter != null ? new MessageDialogScreen(bestAdapter) : null;
        }

        public static Screen TryBuildActiveConfirmPopupScreen()
        {
            ConfirmPopup[] popups = Resources.FindObjectsOfTypeAll<ConfirmPopup>();
            ConfirmPopupAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < popups.Length; i++)
            {
                ConfirmPopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                ConfirmPopupAdapter adapter = new ConfirmPopupAdapter(popup);
                if (!adapter.IsPresent())
                {
                    continue;
                }

                int siblingIndex = popup.transform != null ? popup.transform.GetSiblingIndex() : 0;
                if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                {
                    bestAdapter = adapter;
                    bestSiblingIndex = siblingIndex;
                }
            }

            return bestAdapter != null ? new MessageDialogScreen(bestAdapter) : null;
        }

        public static Screen TryBuildActiveSystemPopupScreen()
        {
            SystemPopup[] popups = Resources.FindObjectsOfTypeAll<SystemPopup>();
            SystemPopupAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < popups.Length; i++)
            {
                SystemPopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                SystemPopupAdapter adapter = new SystemPopupAdapter(popup);
                if (!adapter.IsPresent())
                {
                    continue;
                }

                int siblingIndex = popup.transform != null ? popup.transform.GetSiblingIndex() : 0;
                if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                {
                    bestAdapter = adapter;
                    bestSiblingIndex = siblingIndex;
                }
            }

            return bestAdapter != null ? new MessageDialogScreen(bestAdapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool HasClaimed(string actionKey)
        {
            if (actionKey == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.IsNegativeActionEnabled;
            }

            return base.HasClaimed(actionKey);
        }

        public override void OnPop()
        {
            if (_inputAdapter != null && _inputSubmitHandler != null)
            {
                _inputAdapter.DetachInputSubmit(_inputSubmitHandler);
            }
        }

        public object SourceKey
        {
            get { return _adapter != null ? _adapter.SourceKey : null; }
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.IsNegativeActionEnabled && _adapter.ActivateAction(DialogAction.Negative);
            }

            return base.OnActionJustPressed(action);
        }

        private void HandleInputSubmit(IUITextMeshInputField inputField, string text)
        {
            if (_adapter != null && _adapter.IsPositiveActionEnabled)
            {
                _adapter.ActivateAction(DialogAction.Positive);
            }
        }

        private static ContainerWidget BuildRootWidget(IMessageDialogAdapter adapter)
        {
            string title = adapter != null ? adapter.Title : string.Empty;
            string dialogLabel = string.IsNullOrWhiteSpace(title) ? "dialog" : title + " dialog";
            ContainerWidget root = new ContainerWidget("message-dialog", dialogLabel);
            IInputDialogAdapter inputAdapter = adapter as IInputDialogAdapter;

            root.AddChild(new TextWidget(
                "body",
                () => adapter != null ? adapter.Body : string.Empty,
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(DialogAction.Body);
                    }
                },
                includeParentLabelInAnnouncement: true,
                isVisible: () => adapter != null && !string.IsNullOrWhiteSpace(adapter.Body)));

            root.AddChild(new TextInputWidget(
                "input",
                FirstNonEmpty(title, adapter != null ? adapter.Body : string.Empty),
                () => inputAdapter != null ? inputAdapter.InputField : null,
                null,
                null,
                () => inputAdapter != null && inputAdapter.HasInputField,
                () => inputAdapter != null && inputAdapter.HasInputField));

            root.AddChild(new ButtonWidget(
                "positive",
                adapter != null ? adapter.PositiveLabel : string.Empty,
                () => adapter != null && adapter.ActivateAction(DialogAction.Positive),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(DialogAction.Positive);
                    }
                },
                () => adapter != null && adapter.IsPositiveActionEnabled,
                () => adapter != null && adapter.HasPositiveAction));

            root.AddChild(new ButtonWidget(
                "negative",
                adapter != null ? adapter.NegativeLabel : string.Empty,
                () => adapter != null && adapter.ActivateAction(DialogAction.Negative),
                () =>
                {
                    if (adapter != null)
                    {
                        adapter.SyncNativeSelection(DialogAction.Negative);
                    }
                },
                () => adapter != null && adapter.IsNegativeActionEnabled,
                () => adapter != null && adapter.HasNegativeAction));

            return root;
        }

        private static bool IsLiveScenePopup(MapMessagePopup popup)
        {
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveScenePopup(ConfirmPopup popup)
        {
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLiveScenePopup(SystemPopup popup)
        {
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
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

        private static bool IsLiveSceneInstaller(RandomEventMenuInstaller installer)
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
            if (installer == null || PopupInstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = PopupInstallerContainerProperty.GetValue(installer, null) as DiContainer;
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

        private static RandomEventMenu TryResolveRandomEventMenu(RandomEventMenuInstaller installer)
        {
            if (installer == null || RandomEventInstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = RandomEventInstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<RandomEventMenu>();
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

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? second ?? string.Empty : first;
        }
    }
}
