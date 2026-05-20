using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TaleSelectAdapter
    {
        private static readonly AccessTools.FieldRef<TaleButtonLayoutCoordinator, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<TaleButtonLayoutCoordinator, CanvasGroup>("_canvasGroup");
        private static readonly AccessTools.FieldRef<TaleButton, MainMenuManagerContainer> TaleButtonManagerContainerRef =
            AccessTools.FieldRefAccess<TaleButton, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private readonly TaleButtonLayoutCoordinator _coordinator;
        private readonly List<TaleButtonAdapter> _tales = new List<TaleButtonAdapter>();
        private readonly TaleButton[] _taleButtons;

        public TaleSelectAdapter(TaleButtonLayoutCoordinator coordinator)
        {
            _coordinator = coordinator;
            _taleButtons = coordinator != null
                ? ((Component)coordinator).GetComponentsInChildren<TaleButton>(includeInactive: false)
                : null;
            if (_taleButtons != null)
            {
                for (int i = 0; i < _taleButtons.Length; i++)
                {
                    TaleButtonAdapter adapter = new TaleButtonAdapter(_taleButtons[i]);
                    if (adapter.IsVisible())
                    {
                        _tales.Add(adapter);
                    }
                }
            }

            MainMenuManager.Settings settings = GetMainMenuSettings(_taleButtons);
            BackButton = settings != null ? new StandardMenuButtonAdapter(
                settings.BackButton,
                () => settings.BackButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.BackButton),
                null) : null;
            OptionsButton = settings != null ? new OptionsMenuButtonAdapter(
                settings.OptionsButton,
                () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                null) : null;
        }

        public object SourceKey
        {
            get { return _coordinator; }
        }

        public IReadOnlyList<TaleButtonAdapter> Tales
        {
            get { return _tales; }
        }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public string GetTitle()
        {
            return string.Empty;
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
            for (int i = 0; i < _tales.Count; i++)
            {
                if (_tales[i] != null && _tales[i].IsVisible())
                {
                    return true;
                }
            }

            return false;
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
