using HarmonyLib;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureLobbyMapTypeAdapter
    {
        private static readonly AccessTools.FieldRef<MapTypeMenu, UIButton> AllMapsButtonRef =
            AccessTools.FieldRefAccess<MapTypeMenu, UIButton>("_allMapsButton");
        private static readonly AccessTools.FieldRef<MapTypeMenu, UIButton> RandomMapsButtonRef =
            AccessTools.FieldRefAccess<MapTypeMenu, UIButton>("_randomMapsButton");
        private static readonly AccessTools.FieldRef<MapTypeMenu, UIButton> ChallengeMapsButtonRef =
            AccessTools.FieldRefAccess<MapTypeMenu, UIButton>("_challengeMapsButton");
        private static readonly AccessTools.FieldRef<MapTypeMenu, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<MapTypeMenu, CanvasGroup>("_canvasGroup");
        private static readonly AccessTools.FieldRef<LobbyNavigation, UIBackButton> CommonBackButtonRef =
            AccessTools.FieldRefAccess<LobbyNavigation, UIBackButton>("_commonBackButton");
        private static readonly AccessTools.FieldRef<LobbyNavigation, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<LobbyNavigation, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private readonly MapTypeMenu _menu;
        private readonly LobbyNavigation _navigation;

        public AdventureLobbyMapTypeAdapter(MapTypeMenu menu)
        {
            _menu = menu;
            _navigation = FindNavigationFor(menu);

            AllMapsButton = CreateButton(menu != null ? AllMapsButtonRef(menu) : null);
            RandomMapsButton = CreateButton(menu != null ? RandomMapsButtonRef(menu) : null);
            ChallengeMapsButton = CreateButton(menu != null ? ChallengeMapsButtonRef(menu) : null);

            UIBackButton backButton = _navigation != null ? CommonBackButtonRef(_navigation) : null;
            BackButton = backButton != null ? new StandardMenuButtonAdapter(
                backButton,
                () => MenuButtonAdapterBase.IsButtonVisible(backButton),
                () => NativeSelectionUtility.Click(backButton)) : null;

            MainMenuManager.Settings settings = GetMainMenuSettings();
            OptionsButton = settings != null ? new OptionsMenuButtonAdapter(
                settings.OptionsButton,
                () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                () => NativeSelectionUtility.Click(settings.OptionsButton)) : null;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IMenuButtonAdapter AllMapsButton { get; private set; }

        public IMenuButtonAdapter RandomMapsButton { get; private set; }

        public IMenuButtonAdapter ChallengeMapsButton { get; private set; }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public string GetTitle()
        {
            if (GlobalLocalizationVariables.LocalizationHandler == null)
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(
                GlobalLocalizationVariables.LocalizationHandler.GetText("Lobby/MapTypeMenu/Title"));
        }

        public bool IsPresent()
        {
            return _menu != null
                && IsLoadedMainMenuScene(MainMenuSceneType.AdventureLobby)
                && IsLiveSceneObject(((Component)_menu).gameObject)
                && ((Component)_menu).gameObject.activeInHierarchy
                && IsReady()
                && HasVisibleMapTypeButton();
        }

        private bool IsReady()
        {
            CanvasGroup canvasGroup = _menu != null ? CanvasGroupRef(_menu) : null;
            return canvasGroup == null || canvasGroup.blocksRaycasts || canvasGroup.alpha > 0.5f;
        }

        private bool HasVisibleMapTypeButton()
        {
            return IsButtonVisible(AllMapsButton)
                || IsButtonVisible(RandomMapsButton)
                || IsButtonVisible(ChallengeMapsButton);
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = _navigation != null ? ManagerContainerRef(_navigation) : null;
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private static IMenuButtonAdapter CreateButton(UIButton button)
        {
            return new MapTypeMenuButtonAdapter(
                button,
                () => MenuButtonAdapterBase.IsButtonVisible(button),
                () => NativeSelectionUtility.Click(button));
        }

        private static bool IsButtonVisible(IMenuButtonAdapter button)
        {
            return button != null && button.IsVisible();
        }

        private static LobbyNavigation FindNavigationFor(MapTypeMenu menu)
        {
            if (menu == null)
            {
                return null;
            }

            GameObject menuObject = ((Component)menu).gameObject;
            LobbyNavigation[] navigations = Resources.FindObjectsOfTypeAll<LobbyNavigation>();
            for (int i = 0; i < navigations.Length; i++)
            {
                LobbyNavigation navigation = navigations[i];
                if (navigation == null)
                {
                    continue;
                }

                GameObject navigationObject = ((Component)navigation).gameObject;
                if (IsLiveSceneObject(navigationObject) && navigationObject.scene == menuObject.scene)
                {
                    return navigation;
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

        private sealed class MapTypeMenuButtonAdapter : MenuButtonAdapterBase
        {
            public MapTypeMenuButtonAdapter(
                UIButton button,
                System.Func<bool> isVisible,
                System.Func<bool> activate)
                : base(button, isVisible, activate)
            {
            }

            protected override string BuildLabel()
            {
                List<string> visibleParts = MenuButtonTextUtility.GetAllVisibleTextParts(Button);
                if (visibleParts.Count == 3)
                {
                    return string.Join(". ", new string[]
                    {
                        visibleParts[1],
                        visibleParts[0],
                        visibleParts[2]
                    });
                }

                return string.Join(". ", visibleParts.ToArray());
            }
        }
    }
}
