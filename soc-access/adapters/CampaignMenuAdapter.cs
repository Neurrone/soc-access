using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CampaignMenuAdapter
    {
        private static readonly AccessTools.FieldRef<CampaignMenu, GameObject> CampaignButtonContainerRef =
            AccessTools.FieldRefAccess<CampaignMenu, GameObject>("_campaignButtonContainer");
        private static readonly AccessTools.FieldRef<CampaignMenu, GameObject> CustomCampaignButtonRef =
            AccessTools.FieldRefAccess<CampaignMenu, GameObject>("_customCampaignButton");
        private static readonly AccessTools.FieldRef<CampaignMenu, UIButton> TalesButtonRef =
            AccessTools.FieldRefAccess<CampaignMenu, UIButton>("_talesButton");
        private static readonly AccessTools.FieldRef<CampaignMenu, CampaignButton[]> CampaignButtonsRef =
            AccessTools.FieldRefAccess<CampaignMenu, CampaignButton[]>("_campaignButtons");
        private static readonly AccessTools.FieldRef<CampaignMenu, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<CampaignMenu, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private readonly CampaignMenu _campaignMenu;
        private readonly List<CampaignButtonAdapter> _campaignButtons = new List<CampaignButtonAdapter>();

        public CampaignMenuAdapter(CampaignMenu campaignMenu)
        {
            _campaignMenu = campaignMenu;
            CampaignButton[] campaignButtons = campaignMenu != null ? CampaignButtonsRef(campaignMenu) : null;
            if (campaignButtons != null)
            {
                for (int i = 0; i < campaignButtons.Length; i++)
                {
                    CampaignButtonAdapter adapter = new CampaignButtonAdapter(campaignButtons[i], i + 1);
                    if (adapter.IsVisible())
                    {
                        _campaignButtons.Add(adapter);
                    }
                }
            }

            CustomCampaignButton = CreateOptionalButton(
                "Custom campaigns",
                campaignMenu != null ? CustomCampaignButtonRef(campaignMenu) : null,
                includeAllVisibleText: false);
            UIButton talesButton = campaignMenu != null ? TalesButtonRef(campaignMenu) : null;
            TalesButton = CreateOptionalButton(
                "Tales",
                talesButton != null ? ((Component)talesButton).gameObject : null,
                includeAllVisibleText: true);
            MainMenuManager.Settings settings = GetMainMenuSettings();
            BackButton = settings != null ? new OptionalMenuButtonAdapter(
                settings.BackButton,
                "Back",
                () => settings.BackButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.BackButton),
                null,
                includeAllVisibleText: false) : null;
            OptionsButton = settings != null ? new OptionsMenuButtonAdapter(
                settings.OptionsButton,
                () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                null) : null;
        }

        public object SourceKey
        {
            get { return _campaignMenu; }
        }

        public IReadOnlyList<CampaignButtonAdapter> CampaignButtons
        {
            get { return _campaignButtons; }
        }

        public IMenuButtonAdapter CustomCampaignButton { get; private set; }

        public IMenuButtonAdapter TalesButton { get; private set; }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public string GetTitle()
        {
            if (GlobalLocalizationVariables.LocalizationHandler == null)
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(
                GlobalLocalizationVariables.LocalizationHandler.GetText("Campaign/CampaignSelect/Header"));
        }

        public bool IsPresent()
        {
            return _campaignMenu != null
                && IsLoadedMainMenuScene(MainMenuSceneType.Campaign)
                && IsLiveSceneObject(_campaignMenu.gameObject)
                && IsGameObjectActive(GetCampaignButtonContainer())
                && HasVisibleCampaignButton();
        }

        public bool HasOptionalControls()
        {
            return IsOptionalButtonUsable(CustomCampaignButton) || IsOptionalButtonUsable(TalesButton);
        }

        private bool HasVisibleCampaignButton()
        {
            for (int i = 0; i < _campaignButtons.Count; i++)
            {
                if (_campaignButtons[i] != null && _campaignButtons[i].IsVisible())
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject GetCampaignButtonContainer()
        {
            return _campaignMenu != null ? CampaignButtonContainerRef(_campaignMenu) : null;
        }

        private static IMenuButtonAdapter CreateOptionalButton(string fallbackLabel, GameObject root, bool includeAllVisibleText)
        {
            if (!IsGameObjectActive(root))
            {
                return null;
            }

            UIButton button = root.GetComponent<UIButton>() ?? root.GetComponentInChildren<UIButton>(includeInactive: false);
            if (!MenuButtonAdapterBase.IsButtonVisible(button))
            {
                return null;
            }

            return new OptionalMenuButtonAdapter(
                button,
                fallbackLabel,
                () => IsGameObjectActive(root) && MenuButtonAdapterBase.IsButtonVisible(button),
                null,
                includeAllVisibleText);
        }

        private static bool IsOptionalButtonUsable(IMenuButtonAdapter button)
        {
            return button != null && button.IsVisible();
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLoadedMainMenuScene(MainMenuSceneType sceneType)
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == sceneType;
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = _campaignMenu != null ? ManagerContainerRef(_campaignMenu) : null;
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private sealed class OptionalMenuButtonAdapter : MenuButtonAdapterBase
        {
            private readonly string _fallbackLabel;
            private readonly bool _includeAllVisibleText;

            public OptionalMenuButtonAdapter(
                UIButton button,
                string fallbackLabel,
                System.Func<bool> isVisible,
                System.Func<bool> activate,
                bool includeAllVisibleText)
                : base(button, isVisible, activate)
            {
                _fallbackLabel = fallbackLabel ?? string.Empty;
                _includeAllVisibleText = includeAllVisibleText;
            }

            protected override string BuildLabel()
            {
                string label = _includeAllVisibleText
                    ? MenuButtonTextUtility.GetAllVisibleText(Button)
                    : MenuButtonTextUtility.GetStandardButtonLabel(Button);
                return string.IsNullOrWhiteSpace(label) ? _fallbackLabel : label;
            }
        }
    }
}
