using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using System.Reflection;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class OnlineHostGameAdapter
    {
        private static readonly AccessTools.FieldRef<GameListMenu, GameListMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<GameListMenu, GameListMenu.Settings>("_settings");
        private static readonly FieldInfo InstallerSettingsField =
            AccessTools.Field(typeof(GameListMenuInstaller), "_settings");

        private readonly GameListMenu _menu;
        private readonly GameListMenu.Settings _settings;

        public OnlineHostGameAdapter(GameListMenu menu)
            : this(menu, menu != null ? SettingsRef(menu) : null)
        {
        }

        private OnlineHostGameAdapter(GameListMenu menu, GameListMenu.Settings settings)
        {
            _menu = menu;
            _settings = settings;

            PositiveButton = CreateButton(settings != null ? settings.HostGamePositiveButton : null);
            NegativeButton = CreateButton(settings != null ? settings.HostGameNegativeButton : null);
        }

        public object SourceKey
        {
            get { return _menu ?? (object)_settings; }
        }

        public IMenuButtonAdapter PositiveButton { get; private set; }

        public IMenuButtonAdapter NegativeButton { get; private set; }

        public static OnlineHostGameAdapter TryCreateActive()
        {
            GameListMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<GameListMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                GameListMenuInstaller installer = installers[i];
                if (installer == null || !IsLiveSceneObject(((Component)installer).gameObject))
                {
                    continue;
                }

                GameListMenu.Settings settings = InstallerSettingsField != null
                    ? InstallerSettingsField.GetValue(installer) as GameListMenu.Settings
                    : null;
                OnlineHostGameAdapter adapter = new OnlineHostGameAdapter(null, settings);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public bool IsPresent()
        {
            return _settings != null
                && IsLoadedMainMenuScene(MainMenuSceneType.OnlineGameList)
                && _settings.HostGameMenu != null
                && _settings.HostGameMenu.Active;
        }

        public string Title
        {
            get { return GetText(_settings != null ? _settings.HostGameHeader : null); }
        }

        public string Description
        {
            get { return GetText(_settings != null ? _settings.HostGameSubheader : null); }
        }

        public string InviteOnlyLabel
        {
            get { return GetText(_settings != null ? _settings.HostGameToggleLabel : null); }
        }

        public IUITextMeshInputField InputField
        {
            get { return _settings != null ? _settings.HostGameInputField : null; }
        }

        public UIToggle InviteOnlyToggle
        {
            get { return _settings != null ? _settings.HostGameTogglePublicGame : null; }
        }

        public bool HasDescription
        {
            get { return !string.IsNullOrWhiteSpace(Description); }
        }

        public bool IsInputVisible()
        {
            IUITextMeshInputField field = InputField;
            return field != null && field.Active;
        }

        public bool IsInputEnabled()
        {
            IUITextMeshInputField field = InputField;
            return field != null && field.Active && field.Interactable;
        }

        public void FocusInput()
        {
            IUITextMeshInputField field = InputField;
            if (field != null)
            {
                field.Select();
            }
        }

        public bool IsInviteOnlyVisible()
        {
            UIToggle toggle = InviteOnlyToggle;
            return toggle != null && toggle.Active;
        }

        public bool IsInviteOnlyEnabled()
        {
            UIToggle toggle = InviteOnlyToggle;
            return toggle != null && toggle.Active && toggle.Interactable;
        }

        public bool IsInviteOnlyChecked()
        {
            UIToggle toggle = InviteOnlyToggle;
            return toggle != null && toggle.ToggleValue;
        }

        public void ToggleInviteOnly()
        {
            UIToggle toggle = InviteOnlyToggle;
            if (toggle != null && toggle.Active && toggle.Interactable)
            {
                toggle.ToggleValue = !toggle.ToggleValue;
            }
        }

        public Tooltip GetInputTooltip()
        {
            return Tooltip.ForComponent(InputField as Component, null);
        }

        public Tooltip GetInviteOnlyTooltip()
        {
            UIToggle toggle = InviteOnlyToggle;
            return Tooltip.ForComponent(toggle != null ? toggle.GetTextMesh() : null, null);
        }

        public Tooltip GetButtonTooltip(IMenuButtonAdapter button)
        {
            return button != null ? Tooltip.ForComponent(button.Button as Component, null) : null;
        }

        public bool Cancel()
        {
            return NegativeButton != null && NegativeButton.Activate();
        }

        private static IMenuButtonAdapter CreateButton(UIButton button)
        {
            return button != null
                ? new StandardMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }

        private static bool IsLoadedMainMenuScene(MainMenuSceneType sceneType)
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == sceneType;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
