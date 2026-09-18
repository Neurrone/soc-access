using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CampaignMenuAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<CampaignMenu, GameObject> CampaignButtonContainerRef =
            AccessTools.FieldRefAccess<CampaignMenu, GameObject>("_campaignButtonContainer");
        private static readonly AccessTools.FieldRef<CampaignMenu, GameObject> CustomCampaignButtonRef =
            AccessTools.FieldRefAccess<CampaignMenu, GameObject>("_customCampaignButton");
        private static readonly AccessTools.FieldRef<CampaignMenu, UIButton> TalesButtonRef =
            AccessTools.FieldRefAccess<CampaignMenu, UIButton>("_talesButton");
        private static readonly AccessTools.FieldRef<CampaignMenu, CampaignButton[]> CampaignButtonsRef =
            AccessTools.FieldRefAccess<CampaignMenu, CampaignButton[]>("_campaignButtons");
        // The card's own clickable button, read here as well as in CampaignButtonAdapter so that
        // counting the drawn cards every frame costs a field read and allocates nothing.
        private static readonly AccessTools.FieldRef<CampaignButton, UIButton> CampaignButtonButtonRef =
            AccessTools.FieldRefAccess<CampaignButton, UIButton>("_button");
        private static readonly AccessTools.FieldRef<CampaignMenu, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<CampaignMenu, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private readonly CampaignMenu _campaignMenu;
        private readonly List<CampaignButtonAdapter> _campaignButtons = new List<CampaignButtonAdapter>();

        // How many of the menu's own campaign buttons were visible when the list above was built.
        // The menu enables its button container a frame into its Start coroutine, so the adapter can
        // be made before there is anything to list, and a campaign the game hides or shows changes
        // the count; the list is rebuilt whenever the count read from the game differs from this.
        private int _campaignButtonsKey = -1;
        private IMenuButtonAdapter _customCampaignButton;
        private IMenuButtonAdapter _talesButton;
        private bool _headerFound;
        private IMenuButtonAdapter _backButton;
        private IMenuButtonAdapter _optionsButton;

        public CampaignMenuAdapter(CampaignMenu campaignMenu)
        {
            _campaignMenu = campaignMenu;
        }

        public object SourceKey
        {
            get { return _campaignMenu; }
        }

        /// <summary>The campaign cards, numbered as the page numbers them. Rebuilt only when the
        /// number of visible buttons the menu holds changes, which is one walk of a serialized array
        /// of four per read and no allocation while nothing moves.</summary>
        public IReadOnlyList<CampaignButtonAdapter> CampaignButtons
        {
            get
            {
                int key = CountVisibleCampaignButtons();
                if (key != _campaignButtonsKey)
                {
                    _campaignButtonsKey = key;
                    BuildCampaignButtons();
                }

                return _campaignButtons;
            }
        }

        /// <summary>The Community Campaigns card, which the menu hides when mods are off and again
        /// once a campaign has been picked. Looked for until it is found and kept after that: the
        /// page's own Start coroutine draws it a frame after the scene is up, so the adapter can be
        /// made before there is a button to wrap, and whether it is DRAWN is asked of the adapter
        /// every frame anyway.</summary>
        public IMenuButtonAdapter CustomCampaignButton
        {
            get
            {
                if (_customCampaignButton == null)
                {
                    _customCampaignButton = CreateOptionalButton(
                        () => ModText.Get(ModStrings.Screens.CustomCampaigns),
                        _campaignMenu != null ? CustomCampaignButtonRef(_campaignMenu) : null,
                        includeAllVisibleText: false);
                }

                return _customCampaignButton;
            }
        }

        /// <summary>The Tales card, found the same way as the Community Campaigns card.</summary>
        public IMenuButtonAdapter TalesButton
        {
            get
            {
                if (_talesButton == null)
                {
                    UIButton talesButton = _campaignMenu != null ? TalesButtonRef(_campaignMenu) : null;
                    _talesButton = CreateOptionalButton(
                        () => ModText.Get(ModStrings.Screens.Tales),
                        talesButton != null ? ((Component)talesButton).gameObject : null,
                        includeAllVisibleText: true);
                }

                return _talesButton;
            }
        }

        /// <summary>The main menu's own header band, shared with the tale select page. Built the
        /// first time the manager answers with its settings and kept after that.</summary>
        public IMenuButtonAdapter BackButton
        {
            get
            {
                SyncHeader();
                return _backButton;
            }
        }

        public IMenuButtonAdapter OptionsButton
        {
            get
            {
                SyncHeader();
                return _optionsButton;
            }
        }

        private void SyncHeader()
        {
            if (_headerFound)
            {
                return;
            }

            MainMenuManager.Settings settings = GetMainMenuSettings();
            if (settings == null)
            {
                return;
            }

            _headerFound = true;
            _backButton = new OptionalMenuButtonAdapter(
                settings.BackButton,
                () => ModText.Get(ModStrings.Screens.Back),
                () => settings.BackButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.BackButton),
                null,
                includeAllVisibleText: false);
            _optionsButton = new OptionsMenuButtonAdapter(
                settings.OptionsButton,
                () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                null);
        }

        public string GetTitle()
        {
            if (GlobalLocalizationVariables.LocalizationHandler == null)
            {
                return string.Empty;
            }

            return SpokenLines.Clean(
                GlobalLocalizationVariables.LocalizationHandler.GetText("Campaign/CampaignSelect/Header"));
        }

        public bool IsPresent()
        {
            return _campaignMenu != null
                && IsLoadedMainMenuScene(MainMenuSceneType.Campaign)
                && GameObjects.IsLiveSceneObject(_campaignMenu.gameObject)
                && IsGameObjectActive(GetCampaignButtonContainer())
                && HasVisibleCampaignButton();
        }

        public bool HasOptionalControls()
        {
            return IsOptionalButtonUsable(CustomCampaignButton) || IsOptionalButtonUsable(TalesButton);
        }

        private bool HasVisibleCampaignButton()
        {
            return CountVisibleCampaignButtons() > 0;
        }

        /// <summary>The menu's own campaign buttons that are drawn now. Both the readiness gate and
        /// the list's key, so the count is what the list is keyed on.</summary>
        private int CountVisibleCampaignButtons()
        {
            CampaignButton[] campaignButtons = _campaignMenu != null ? CampaignButtonsRef(_campaignMenu) : null;
            int count = 0;
            for (int i = 0; campaignButtons != null && i < campaignButtons.Length; i++)
            {
                if (IsVisibleCampaignButton(campaignButtons[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private void BuildCampaignButtons()
        {
            _campaignButtons.Clear();
            CampaignButton[] campaignButtons = _campaignMenu != null ? CampaignButtonsRef(_campaignMenu) : null;
            for (int i = 0; campaignButtons != null && i < campaignButtons.Length; i++)
            {
                if (!IsVisibleCampaignButton(campaignButtons[i]))
                {
                    continue;
                }

                // Numbered by the button's place in the menu's own array, which is where the number
                // the card speaks ("Campaign 1") comes from.
                _campaignButtons.Add(new CampaignButtonAdapter(campaignButtons[i], i + 1));
            }
        }

        private static bool IsVisibleCampaignButton(CampaignButton campaignButton)
        {
            return campaignButton != null
                && MenuButtonAdapterBase.IsButtonVisible(CampaignButtonButtonRef(campaignButton));
        }

        private GameObject GetCampaignButtonContainer()
        {
            return _campaignMenu != null ? CampaignButtonContainerRef(_campaignMenu) : null;
        }

        private static IMenuButtonAdapter CreateOptionalButton(System.Func<string> fallbackLabel, GameObject root, bool includeAllVisibleText)
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
            // The fallback is asked for when the label is read, not when the wrapper is made: the
            // menu's own options page changes the language with this page still up and the wrapper
            // is kept for the page's life, so a word taken at construction would be the one the
            // player had when they arrived.
            private readonly System.Func<string> _fallbackLabel;
            private readonly bool _includeAllVisibleText;

            public OptionalMenuButtonAdapter(
                UIButton button,
                System.Func<string> fallbackLabel,
                System.Func<bool> isVisible,
                System.Func<bool> activate,
                bool includeAllVisibleText)
                : base(button, isVisible, activate)
            {
                _fallbackLabel = fallbackLabel;
                _includeAllVisibleText = includeAllVisibleText;
            }

            protected override string BuildLabel()
            {
                string label = _includeAllVisibleText
                    ? MenuButtonTextUtility.GetAllVisibleText(Button)
                    : MenuButtonTextUtility.GetStandardButtonLabel(Button);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }

                return _fallbackLabel != null ? _fallbackLabel() : string.Empty;
            }
        }
    }
}
