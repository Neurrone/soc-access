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
    public sealed class CampaignMapSelectAdapter
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
        // The two buttons a mission card draws, read here as well as in CampaignMapButtonAdapter so
        // that counting the drawn missions every frame costs two field reads and allocates nothing.
        private static readonly AccessTools.FieldRef<CampaignMapButton, UIButton> UnplayedButtonRef =
            AccessTools.FieldRefAccess<CampaignMapButton, UIButton>("_unplayedButton");
        private static readonly AccessTools.FieldRef<CampaignMapButton, UIButton> PlayedBeforeButtonRef =
            AccessTools.FieldRefAccess<CampaignMapButton, UIButton>("_playedBeforeButton");
        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private readonly CampaignMapSelectMenu _menu;
        private readonly CampaignMapSelectMenu.Settings _settings;
        private readonly List<CampaignMapButtonAdapter> _missions = new List<CampaignMapButtonAdapter>();

        // How many of the menu's own mission buttons were drawn when the list above was built. The
        // menu instantiates one button per mission after the scene is up, and animates the last one
        // in, so the adapter can be made before there is a button to list; the list is rebuilt
        // whenever the count read from the game differs from this. The SELECTED mission is not
        // cached at all - it is read off the menu's _selectedButton every time it is asked for.
        private int _missionsKey = -1;
        private bool _headerFound;
        private IMenuButtonAdapter _backButton;
        private IMenuButtonAdapter _optionsButton;

        public CampaignMapSelectAdapter(CampaignMapSelectMenu menu, CampaignMapSelectedInformationView informationView)
        {
            _menu = menu;
            _settings = menu != null ? SettingsRef(menu) : null;
            Information = new CampaignMapSelectedInformationAdapter(informationView);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        /// <summary>The missions, in the menu's own order. Rebuilt only when the number of drawn
        /// mission buttons changes, which is one walk of a list of four per read.</summary>
        public IReadOnlyList<CampaignMapButtonAdapter> Missions
        {
            get
            {
                int key = CountVisibleMissions();
                if (key != _missionsKey)
                {
                    _missionsKey = key;
                    BuildMissions();
                }

                return _missions;
            }
        }

        public CampaignMapSelectedInformationAdapter Information { get; private set; }

        /// <summary>The main menu's own header band over the page. Built the first time the manager
        /// answers with its settings, which it may not do on the frame the menu is first found, and
        /// kept once it has.</summary>
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
            _backButton = new StandardMenuButtonAdapter(settings.BackButton);
            _optionsButton = new OptionsMenuButtonAdapter(settings.OptionsButton);
        }

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

        /// <summary>Which mission the page is describing, read off the menu's own selected button
        /// every time rather than remembered: taking a mission or a difficulty changes it, and
        /// neither is an event this adapter is told about.</summary>
        public int SelectedMissionIndex
        {
            get
            {
                CampaignMapButton selected = _menu != null ? SelectedButtonRef(_menu) : null;
                IReadOnlyList<CampaignMapButtonAdapter> missions = Missions;
                for (int i = 0; i < missions.Count; i++)
                {
                    if (ReferenceEquals(missions[i].Source, selected))
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
            _missions.Clear();
            List<CampaignMapButton> buttons = _menu != null ? MapButtonsRef(_menu) : null;
            for (int i = 0; buttons != null && i < buttons.Count; i++)
            {
                CampaignMapButtonAdapter adapter = new CampaignMapButtonAdapter(buttons[i]);
                if (adapter.IsVisible())
                {
                    _missions.Add(adapter);
                }
            }
        }

        /// <summary>The menu's own mission buttons that are drawn now. Both the readiness gate and
        /// the list's key, so the count is what the list is keyed on.</summary>
        private int CountVisibleMissions()
        {
            List<CampaignMapButton> buttons = _menu != null ? MapButtonsRef(_menu) : null;
            int count = 0;
            for (int i = 0; buttons != null && i < buttons.Count; i++)
            {
                if (IsVisibleMissionButton(buttons[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsVisibleMissionButton(CampaignMapButton button)
        {
            if (button == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)button).gameObject;
            return gameObject != null
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded
                && (MenuButtonAdapterBase.IsButtonVisible(UnplayedButtonRef(button))
                    || MenuButtonAdapterBase.IsButtonVisible(PlayedBeforeButtonRef(button)));
        }

        private bool HasVisibleMission()
        {
            return CountVisibleMissions() > 0;
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
