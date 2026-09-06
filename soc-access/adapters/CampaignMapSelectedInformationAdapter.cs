using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Campaign;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CampaignMapSelectedInformationAdapter
    {
        private const string MissionCounterLocalizationKey = "Campaign/MapSelect/InformationView/MissionCounter";

        private static readonly AccessTools.FieldRef<CampaignMapSelectedInformationView, CampaignMapSelectedInformationView.Settings> SettingsRef =
            AccessTools.FieldRefAccess<CampaignMapSelectedInformationView, CampaignMapSelectedInformationView.Settings>("_settings");
        private static readonly AccessTools.FieldRef<CampaignMapSelectedInformationView, ILocalizationHandler> LocalizationRef =
            AccessTools.FieldRefAccess<CampaignMapSelectedInformationView, ILocalizationHandler>("_localizationHandler");
        private static readonly AccessTools.FieldRef<CampaignMapSelectedInformationView, CampaignState> CampaignStateRef =
            AccessTools.FieldRefAccess<CampaignMapSelectedInformationView, CampaignState>("_campaignState");
        private static readonly AccessTools.FieldRef<CampaignMapSelectedInformationView, MapFormat> MapRef =
            AccessTools.FieldRefAccess<CampaignMapSelectedInformationView, MapFormat>("_map");
        private static readonly AccessTools.FieldRef<CampaignMapSelectedInformationView, ICampaignMapDefinition> MapDefinitionRef =
            AccessTools.FieldRefAccess<CampaignMapSelectedInformationView, ICampaignMapDefinition>("_mapDefinition");
        private static readonly AccessTools.FieldRef<CampaignMapSelectedInformationView, CampaignDifficulty[]> CurrentDifficultiesRef =
            AccessTools.FieldRefAccess<CampaignMapSelectedInformationView, CampaignDifficulty[]>("_currentDifficulties");

        private readonly CampaignMapSelectedInformationView _view;
        private readonly CampaignMapSelectedInformationView.Settings _settings;

        public CampaignMapSelectedInformationAdapter(CampaignMapSelectedInformationView view)
        {
            _view = view;
            _settings = view != null ? SettingsRef(view) : null;
            StartButton = _settings != null ? new StandardMenuButtonAdapter(_settings.StartGameButton) : null;
            ReplayButton = _settings != null ? new StandardMenuButtonAdapter(_settings.ReplayOutroButton) : null;
            Difficulty = new DifficultyDropList(this);
        }

        /// <summary>The difficulty control, as a drop list: the page draws a real
        /// <c>UITextMeshDropdown</c> for it, so it answers the questions every drop list answers and
        /// the mod's own list screen opens over the game's popup.</summary>
        public DifficultyDropList Difficulty { get; private set; }

        public object SourceKey
        {
            get { return _view; }
        }

        public IMenuButtonAdapter StartButton { get; private set; }

        public IMenuButtonAdapter ReplayButton { get; private set; }

        public bool IsPresent()
        {
            return _view != null
                && _settings != null
                && IsLiveSceneObject(GetRootGameObject())
                && IsGameObjectActive(GetRootGameObject())
                && MapDefinition != null
                && Map != null;
        }

        public ICampaignMapDefinition MapDefinition
        {
            get { return _view != null ? MapDefinitionRef(_view) : null; }
        }

        public MapFormat Map
        {
            get { return _view != null ? MapRef(_view) : null; }
        }

        public IReadOnlyList<CampaignDifficulty> CurrentDifficulties
        {
            get { return CurrentDifficultiesRef(_view) ?? Array.Empty<CampaignDifficulty>(); }
        }

        public CampaignDifficulty CurrentDifficulty
        {
            get
            {
                CampaignState state = _view != null ? CampaignStateRef(_view) : null;
                return state != null ? state.Difficulty : CampaignDifficulty.Easy;
            }
        }

        public bool HasDifficultyMenu()
        {
            return _settings != null
                && _settings.DifficultyDropdown != null
                && _settings.DifficultyDropdown.Active
                && CurrentDifficulties.Count > 1;
        }

        public string GetTitle()
        {
            ILocalizationHandler localization = GetLocalization();
            MapFormat map = Map;
            if (localization == null || map == null || map.Metadata == null)
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(localization.GetText(map.Metadata.Name));
        }

        public string GetDescription()
        {
            ILocalizationHandler localization = GetLocalization();
            MapFormat map = Map;
            if (localization == null || map == null || map.Metadata == null)
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(localization.GetText(map.Metadata.Description));
        }

        public string GetMissionCounter()
        {
            return GetText(_settings != null ? _settings.MissionCounterText : null);
        }

        public string GetMissionCounter(string displayName)
        {
            ILocalizationHandler localization = GetLocalization();
            if (localization == null || string.IsNullOrWhiteSpace(displayName))
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(localization.GetText(MissionCounterLocalizationKey, displayName));
        }

        public string GetCompletedStatus()
        {
            if (_settings == null || _settings.CompletedContainer == null || !_settings.CompletedContainer.activeInHierarchy)
            {
                return string.Empty;
            }

            return GetText(_settings.CompletedText);
        }

        public string GetWinConditions()
        {
            ILocalizationHandler localization = GetLocalization();
            MapFormat map = Map;
            if (localization == null || map == null || map.Metadata == null || map.Metadata.WinConditions == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < map.Metadata.WinConditions.Length; i++)
            {
                string text = localization.GetText("GameModes/" + map.Metadata.WinConditions[i] + "/Name");
                text = SpeechTextSanitizer.Normalize(text);
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                {
                    parts.Add(text);
                }
            }

            return parts.Count == 0 ? string.Empty : string.Join(". ", parts.ToArray());
        }

        public string GetDifficultyLabel(CampaignDifficulty difficulty)
        {
            ILocalizationHandler localization = GetLocalization();
            if (localization == null)
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(localization.GetText("Campaign/Difficulty/" + difficulty));
        }

        public bool SelectDifficulty(CampaignDifficulty difficulty)
        {
            if (!HasDifficultyMenu())
            {
                return false;
            }

            IReadOnlyList<CampaignDifficulty> difficulties = CurrentDifficulties;
            for (int i = 0; i < difficulties.Count; i++)
            {
                if (difficulties[i] != difficulty)
                {
                    continue;
                }

                _settings.DifficultyDropdown.DropdownValue = i;
                if (CurrentDifficulty != difficulty && _settings.DifficultyDropdown.OnDropdownValueChanged != null)
                {
                    _settings.DifficultyDropdown.OnDropdownValueChanged.Invoke();
                }

                return true;
            }

            return false;
        }

        public void FocusDifficultyDropdown()
        {
            if (_settings == null || _settings.DifficultyDropdown == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(((Component)_settings.DifficultyDropdown).gameObject);
        }

        public void FocusButton(UIButton button)
        {
            if (button == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(((Component)button).gameObject);
        }

        private ILocalizationHandler GetLocalization()
        {
            return _view != null ? LocalizationRef(_view) : null;
        }

        private GameObject GetRootGameObject()
        {
            return _settings != null && _settings.MainTransform != null
                ? ((Component)_settings.MainTransform).gameObject
                : null;
        }

        private static string GetText(UITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        /// <summary>The difficulty dropdown, answering the questions every drop list answers so the
        /// mod's own list screen can be opened over the game's popup. What TAKING an entry means is
        /// the page's, and it hands that over when it opens the list.</summary>
        public sealed class DifficultyDropList : IDropList
        {
            private readonly CampaignMapSelectedInformationAdapter _adapter;

            public DifficultyDropList(CampaignMapSelectedInformationAdapter adapter)
            {
                _adapter = adapter;
                GetOptions = ReadOptions;
                GetValue = ReadValue;
                IsEnabled = () => _adapter != null && _adapter.HasDifficultyMenu();
                IsVisible = () => _adapter != null && _adapter.HasDifficultyMenu();
                OpenPopup = () => DropdownPopup.Show(Dropdown);
                ClosePopup = () => DropdownPopup.Hide(Dropdown);
                IsPopupOpen = () => DropdownPopup.IsOpen(Dropdown);
                FocusOption = index => DropdownPopup.FocusOption(Dropdown, index);
            }

            public string Id
            {
                get { return "campaign-map-difficulty"; }
            }

            /// <summary>The drawn dropdown itself, so a caller can key a control on it.</summary>
            public Component Subject
            {
                get { return Dropdown as Component; }
            }

            public Func<IReadOnlyList<string>> GetOptions { get; private set; }
            public Func<int> GetValue { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<bool> OpenPopup { get; private set; }
            public Func<bool> ClosePopup { get; private set; }
            public Func<bool> IsPopupOpen { get; private set; }
            public Func<int, bool> FocusOption { get; private set; }

            /// <summary>The difficulty the campaign is set to, in the game's own words.</summary>
            public string CurrentLabel
            {
                get
                {
                    return _adapter != null ? _adapter.GetDifficultyLabel(_adapter.CurrentDifficulty) : string.Empty;
                }
            }

            public bool SetValue(int value)
            {
                IReadOnlyList<CampaignDifficulty> difficulties = _adapter != null
                    ? _adapter.CurrentDifficulties
                    : new CampaignDifficulty[0];
                return value >= 0
                    && value < difficulties.Count
                    && _adapter.SelectDifficulty(difficulties[value]);
            }

            public void Focus()
            {
                if (_adapter != null)
                {
                    _adapter.FocusDifficultyDropdown();
                }
            }

            private UITextMeshDropdown Dropdown
            {
                get
                {
                    return _adapter != null && _adapter._settings != null
                        ? _adapter._settings.DifficultyDropdown
                        : null;
                }
            }

            private IReadOnlyList<string> ReadOptions()
            {
                List<string> labels = new List<string>();
                IReadOnlyList<CampaignDifficulty> difficulties = _adapter != null
                    ? _adapter.CurrentDifficulties
                    : new CampaignDifficulty[0];
                for (int i = 0; i < difficulties.Count; i++)
                {
                    labels.Add(_adapter.GetDifficultyLabel(difficulties[i]));
                }

                return labels;
            }

            private int ReadValue()
            {
                IReadOnlyList<CampaignDifficulty> difficulties = _adapter != null
                    ? _adapter.CurrentDifficulties
                    : new CampaignDifficulty[0];
                CampaignDifficulty current = _adapter != null ? _adapter.CurrentDifficulty : default(CampaignDifficulty);
                for (int i = 0; i < difficulties.Count; i++)
                {
                    if (difficulties[i] == current)
                    {
                        return i;
                    }
                }

                return 0;
            }
        }
    }
}
