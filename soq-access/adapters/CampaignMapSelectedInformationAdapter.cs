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
    internal sealed class CampaignMapSelectedInformationAdapter
    {
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
            StartButton = _settings != null ? new StandardMenuButtonAdapter("start-mission", _settings.StartGameButton) : null;
            ReplayButton = _settings != null ? new StandardMenuButtonAdapter("replay-cutscene", _settings.ReplayOutroButton) : null;
        }

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
    }
}
