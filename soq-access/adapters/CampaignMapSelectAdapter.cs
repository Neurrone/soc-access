using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CampaignMapSelectAdapter
    {
        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, CampaignMapSelectMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, CampaignMapSelectMenu.Settings>("_settings");
        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, ICampaignDefinition> CampaignDefinitionRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, ICampaignDefinition>("_campaignDefinition");
        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, CampaignState> CampaignStateRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, CampaignState>("_campaignState");
        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, List<CampaignMapButton>> MapButtonsRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, List<CampaignMapButton>>("_mapButtons");
        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, CampaignMapButton> SelectedButtonRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, CampaignMapButton>("_selectedButton");
        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private readonly CampaignMapSelectMenu _menu;
        private readonly CampaignMapSelectMenu.Settings _settings;
        private readonly List<CampaignMapButtonAdapter> _missions = new List<CampaignMapButtonAdapter>();

        public CampaignMapSelectAdapter(CampaignMapSelectMenu menu, CampaignMapSelectedInformationView informationView)
        {
            _menu = menu;
            _settings = menu != null ? SettingsRef(menu) : null;
            Information = new CampaignMapSelectedInformationAdapter(informationView);
            BuildMissions();

            MainMenuManager.Settings mainMenuSettings = GetMainMenuSettings();
            BackButton = mainMenuSettings != null ? new StandardMenuButtonAdapter(mainMenuSettings.BackButton) : null;
            OptionsButton = mainMenuSettings != null ? new OptionsMenuButtonAdapter(mainMenuSettings.OptionsButton) : null;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IReadOnlyList<CampaignMapButtonAdapter> Missions
        {
            get { return _missions; }
        }

        public CampaignMapSelectedInformationAdapter Information { get; private set; }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && IsLiveSceneObject(GetMapContainerGameObject())
                && IsGameObjectActive(GetMapContainerGameObject())
                && Information != null
                && Information.IsPresent()
                && HasVisibleMission();
        }

        public int SelectedMissionIndex
        {
            get
            {
                CampaignMapButton selected = _menu != null ? SelectedButtonRef(_menu) : null;
                for (int i = 0; i < _missions.Count; i++)
                {
                    if (ReferenceEquals(_missions[i].Source, selected))
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public string GetCampaignTitle()
        {
            ICampaignDefinition definition = _menu != null ? CampaignDefinitionRef(_menu) : null;
            if (definition == null || GlobalLocalizationVariables.LocalizationHandler == null)
            {
                return string.Empty;
            }

            return MenuButtonTextUtility.JoinParts(
                GlobalLocalizationVariables.LocalizationHandler.TryGetText(definition.Title, definition.Title),
                GlobalLocalizationVariables.LocalizationHandler.TryGetText(definition.SubTitle, definition.SubTitle));
        }

        private void BuildMissions()
        {
            List<CampaignMapButton> buttons = _menu != null ? MapButtonsRef(_menu) : null;
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                CampaignMapButtonAdapter adapter = new CampaignMapButtonAdapter(buttons[i]);
                if (adapter.IsVisible())
                {
                    _missions.Add(adapter);
                }
            }
        }

        private bool HasVisibleMission()
        {
            for (int i = 0; i < _missions.Count; i++)
            {
                if (_missions[i] != null && _missions[i].IsVisible())
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject GetMapContainerGameObject()
        {
            return _settings != null && _settings.MapContainerCanvasGroup != null
                ? ((Component)_settings.MapContainerCanvasGroup).gameObject
                : null;
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = _menu != null ? ManagerContainerRef(_menu) : null;
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
