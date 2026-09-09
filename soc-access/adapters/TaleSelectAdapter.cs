using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class TaleSelectAdapter
    {
        private static readonly AccessTools.FieldRef<TaleButtonLayoutCoordinator, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<TaleButtonLayoutCoordinator, CanvasGroup>("_canvasGroup");
        private static readonly AccessTools.FieldRef<TaleButton, MainMenuManagerContainer> TaleButtonManagerContainerRef =
            AccessTools.FieldRefAccess<TaleButton, MainMenuManagerContainer>("_mainMenuManagerContainer");
        // The card's own clickable button, read here as well as in TaleButtonAdapter so that
        // counting the drawn cards every frame costs a field read and allocates nothing.
        private static readonly AccessTools.FieldRef<TaleButton, UIButton> TaleButtonMainButtonRef =
            AccessTools.FieldRefAccess<TaleButton, UIButton>("_mainButton");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private readonly TaleButtonLayoutCoordinator _coordinator;
        private readonly List<TaleButtonAdapter> _tales = new List<TaleButtonAdapter>();
        private readonly TaleButton[] _taleButtons;

        // How many of the coordinator's own tale buttons were drawn when the list above was built.
        // The coordinator fades its canvas group in at the end of a coroutine and a tale the account
        // cannot use hides itself in Awake, so the adapter can be made before the page's cards are
        // drawn; the list is rebuilt whenever the count read from the game differs from this.
        private int _talesKey = -1;
        private bool _headerFound;
        private IMenuButtonAdapter _backButton;
        private IMenuButtonAdapter _optionsButton;

        public TaleSelectAdapter(TaleButtonLayoutCoordinator coordinator)
        {
            _coordinator = coordinator;
            // Every tale button under the coordinator, drawn or not, walked once here and never
            // again: which of them are DRAWN is asked per frame below.
            _taleButtons = coordinator != null
                ? ((Component)coordinator).GetComponentsInChildren<TaleButton>(includeInactive: true)
                : null;
        }

        public object SourceKey
        {
            get { return _coordinator; }
        }

        /// <summary>The tale cards. Rebuilt only when the number of drawn tale buttons changes,
        /// which is one walk of an array of seven per read and no allocation while nothing moves.
        /// </summary>
        public IReadOnlyList<TaleButtonAdapter> Tales
        {
            get
            {
                int key = CountVisibleTales();
                if (key != _talesKey)
                {
                    _talesKey = key;
                    BuildTales();
                }

                return _tales;
            }
        }

        /// <summary>The main menu's own header band, shared with the campaign menu. Built the first
        /// time a tale button's injected manager answers with its settings, which it may not do on
        /// the frame the coordinator is first found, and kept once it has.</summary>
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

            MainMenuManager.Settings settings = GetMainMenuSettings(_taleButtons);
            if (settings == null)
            {
                return;
            }

            _headerFound = true;
            _backButton = new StandardMenuButtonAdapter(
                settings.BackButton,
                () => settings.BackButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.BackButton),
                null);
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
            return _coordinator != null
                && IsLoadedMainMenuScene(MainMenuSceneType.TaleSelect)
                && IsLiveSceneObject(((Component)_coordinator).gameObject)
                && IsReady()
                && HasVisibleTale();
        }

        private bool IsReady()
        {
            CanvasGroup canvasGroup = _coordinator != null ? CanvasGroupRef(_coordinator) : null;
            return canvasGroup == null || canvasGroup.alpha > 0.5f;
        }

        private bool HasVisibleTale()
        {
            return CountVisibleTales() > 0;
        }

        /// <summary>The coordinator's own tale buttons that are drawn now. Both the readiness gate
        /// and the list's key, so the count is what the list is keyed on.</summary>
        private int CountVisibleTales()
        {
            int count = 0;
            for (int i = 0; _taleButtons != null && i < _taleButtons.Length; i++)
            {
                if (IsVisibleTaleButton(_taleButtons[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private void BuildTales()
        {
            _tales.Clear();
            for (int i = 0; _taleButtons != null && i < _taleButtons.Length; i++)
            {
                if (IsVisibleTaleButton(_taleButtons[i]))
                {
                    _tales.Add(new TaleButtonAdapter(_taleButtons[i]));
                }
            }
        }

        private static bool IsVisibleTaleButton(TaleButton taleButton)
        {
            if (taleButton == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)taleButton).gameObject;
            return gameObject != null
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded
                && MenuButtonAdapterBase.IsButtonVisible(TaleButtonMainButtonRef(taleButton));
        }

        private static MainMenuManager.Settings GetMainMenuSettings(TaleButton[] taleButtons)
        {
            if (taleButtons == null)
            {
                return null;
            }

            for (int i = 0; i < taleButtons.Length; i++)
            {
                TaleButton taleButton = taleButtons[i];
                MainMenuManagerContainer container = taleButton != null ? TaleButtonManagerContainerRef(taleButton) : null;
                MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
                if (manager != null)
                {
                    return MainMenuSettingsRef(manager);
                }
            }

            return null;
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

    }
}
