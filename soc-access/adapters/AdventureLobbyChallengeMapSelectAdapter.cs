using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.Lobby;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class AdventureLobbyChallengeMapSelectAdapter
    {
        private static readonly AccessTools.FieldRef<ChallengeMapsMenu, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<ChallengeMapsMenu, CanvasGroup>("_canvasGroup");
        private static readonly AccessTools.FieldRef<ChallengeMapsMenu, UIButton> ConfirmButtonRef =
            AccessTools.FieldRefAccess<ChallengeMapsMenu, UIButton>("_confirmButton");
        private static readonly AccessTools.FieldRef<ChallengeMapsMenu, AutoScrollToSelected> AutoScrollerRef =
            AccessTools.FieldRefAccess<ChallengeMapsMenu, AutoScrollToSelected>("_autoScroller");
        private static readonly AccessTools.FieldRef<ChallengeMapsMenu, LobbyMapPreview> PreviewRef =
            AccessTools.FieldRefAccess<ChallengeMapsMenu, LobbyMapPreview>("_preview");
        private static readonly AccessTools.FieldRef<ChallengeMapsMenu, LobbyChallengeMapEntry> SelectedEntryRef =
            AccessTools.FieldRefAccess<ChallengeMapsMenu, LobbyChallengeMapEntry>("_selectedEntry");
        private static readonly AccessTools.FieldRef<ChallengeMapsMenu, List<LobbyChallengeMapEntry>> EntriesRef =
            AccessTools.FieldRefAccess<ChallengeMapsMenu, List<LobbyChallengeMapEntry>>("_entries");
        private static readonly AccessTools.FieldRef<ChallengeMapsMenu, ILocalizationHandler> LocalizationRef =
            AccessTools.FieldRefAccess<ChallengeMapsMenu, ILocalizationHandler>("_localizationHandler");
        private static readonly AccessTools.FieldRef<LobbyNavigation, UIBackButton> CommonBackButtonRef =
            AccessTools.FieldRefAccess<LobbyNavigation, UIBackButton>("_commonBackButton");
        private static readonly AccessTools.FieldRef<LobbyNavigation, MainMenuManagerContainer> NavigationManagerContainerRef =
            AccessTools.FieldRefAccess<LobbyNavigation, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");
        private static readonly MethodInfo SetSelectedEntryMethod =
            AccessTools.Method(typeof(ChallengeMapsMenu), "SetSelectedEntry");

        private readonly ChallengeMapsMenu _menu;
        private readonly LobbyNavigation _navigation;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyChallengeMapSelectAdapter(ChallengeMapsMenu menu)
        {
            _menu = menu;
            _navigation = FindNavigationFor(menu);
            _localization = menu != null ? LocalizationRef(menu) : GlobalLocalizationVariables.LocalizationHandler;

            ConfirmButton = new StandardMenuButtonAdapter(ConfirmButtonRef(menu));
            BackButton = CreateBackButton();
            OptionsButton = CreateOptionsButton();
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IMenuButtonAdapter ConfirmButton { get; private set; }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public bool IsPresent()
        {
            CanvasGroup canvasGroup = _menu != null ? CanvasGroupRef(_menu) : null;
            GameObject gameObject = _menu != null ? ((Component)_menu).gameObject : null;
            return _menu != null
                && IsLoadedMainMenuScene(MainMenuSceneType.AdventureLobby)
                && IsLiveSceneObject(gameObject)
                && gameObject.activeInHierarchy
                && canvasGroup != null
                && (canvasGroup.blocksRaycasts || canvasGroup.alpha > 0.5f)
                && GetEntries().Count > 0
                && SelectedEntryRef(_menu) != null;
        }

        public string Title
        {
            get { return GetLocalizedText("Lobby/ChallengeMapMenu/Title", "Challenge Maps"); }
        }

        public string MapsLabel
        {
            get { return ModText.Get(ModStrings.Common.ListSeparator, Title, ModText.Get(ModStrings.UI.RoleGrid)); }
        }

        public string NameColumnLabel
        {
            get { return GetLocalizedText("Common/Name", "Name"); }
        }

        public string WinConditionColumnLabel
        {
            get { return GetLocalizedText("Lobby/GameMode", "Win condition"); }
        }

        public string CompletedColumnLabel
        {
            get { return GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/Completed", "Completed"); }
        }

        /// <summary>The map name the preview panel draws for the selected challenge.</summary>
        public string PreviewTitle
        {
            get { return _menu != null ? LobbyMapPreviewText.GetTitle(PreviewRef(_menu)) : string.Empty; }
        }

        /// <summary>Whether this entry is the one the menu currently has selected - the challenge the
        /// preview panel is showing and Confirm would take.</summary>
        public bool IsSelectedEntry(LobbyChallengeMapEntry entry)
        {
            return _menu != null && entry != null && ReferenceEquals(SelectedEntryRef(_menu), entry);
        }

        public AdventureLobbyChallengeMapRowAdapter SelectedRow
        {
            get
            {
                LobbyChallengeMapEntry selected = _menu != null ? SelectedEntryRef(_menu) : null;
                return selected != null ? new AdventureLobbyChallengeMapRowAdapter(this, selected, _localization) : null;
            }
        }

        public IReadOnlyList<AdventureLobbyChallengeMapRowAdapter> GetVisibleRows()
        {
            List<AdventureLobbyChallengeMapRowAdapter> rows = new List<AdventureLobbyChallengeMapRowAdapter>();
            List<LobbyChallengeMapEntry> visibleEntries = new List<LobbyChallengeMapEntry>();
            IReadOnlyList<LobbyChallengeMapEntry> entries = GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                LobbyChallengeMapEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                GameObject gameObject = ((Component)entry).gameObject;
                if (gameObject != null && gameObject.activeInHierarchy)
                {
                    visibleEntries.Add(entry);
                }
            }

            visibleEntries.Sort(CompareVisualOrder);
            for (int i = 0; i < visibleEntries.Count; i++)
            {
                rows.Add(new AdventureLobbyChallengeMapRowAdapter(this, visibleEntries[i], _localization));
            }

            return rows;
        }

        public void FocusEntry(LobbyChallengeMapEntry entry)
        {
            if (_menu == null || entry == null)
            {
                return;
            }

            if (!ReferenceEquals(SelectedEntryRef(_menu), entry) && SetSelectedEntryMethod != null)
            {
                SetSelectedEntryMethod.Invoke(_menu, new object[] { entry });
            }

            Selectable selectable = entry.Button != null ? entry.Button.GetSelectable() : null;
            NativeSelectionUtility.Select(selectable);
            AutoScrollToSelected autoScroller = AutoScrollerRef(_menu);
            if (autoScroller != null && ((Behaviour)autoScroller).isActiveAndEnabled && selectable != null)
            {
                autoScroller.ForceFocusOn(selectable);
            }
        }

        public bool SelectEntry(LobbyChallengeMapEntry entry)
        {
            if (_menu == null || entry == null)
            {
                return false;
            }

            FocusEntry(entry);
            return true;
        }

        public string GetMapInfoText(LobbyChallengeMapEntry entry)
        {
            if (_menu == null || entry == null || !ReferenceEquals(SelectedEntryRef(_menu), entry))
            {
                return string.Empty;
            }

            return LobbyMapPreviewText.GetInfo(PreviewRef(_menu));
        }

        private IReadOnlyList<LobbyChallengeMapEntry> GetEntries()
        {
            return _menu != null ? EntriesRef(_menu) ?? new List<LobbyChallengeMapEntry>() : new List<LobbyChallengeMapEntry>();
        }

        private IMenuButtonAdapter CreateBackButton()
        {
            UIBackButton backButton = _navigation != null ? CommonBackButtonRef(_navigation) : null;
            return backButton != null
                ? new StandardMenuButtonAdapter(backButton, () => MenuButtonAdapterBase.IsButtonVisible(backButton), () => NativeSelectionUtility.Click(backButton))
                : null;
        }

        private IMenuButtonAdapter CreateOptionsButton()
        {
            MainMenuManager.Settings settings = GetMainMenuSettings();
            UIButton button = settings != null ? settings.OptionsButton : null;
            return button != null
                ? new OptionsMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = _navigation != null ? NavigationManagerContainerRef(_navigation) : null;
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback ?? string.Empty));
        }

        private static int CompareVisualOrder(LobbyChallengeMapEntry left, LobbyChallengeMapEntry right)
        {
            int result = GetSiblingIndex(left).CompareTo(GetSiblingIndex(right));
            if (result != 0)
            {
                return result;
            }

            return string.CompareOrdinal(GetEntryName(left), GetEntryName(right));
        }

        private static int GetSiblingIndex(LobbyChallengeMapEntry entry)
        {
            return entry != null ? ((Component)entry).transform.GetSiblingIndex() : int.MaxValue;
        }

        private static string GetEntryName(LobbyChallengeMapEntry entry)
        {
            return entry != null ? entry.LocalizedMapName ?? string.Empty : string.Empty;
        }

        private static LobbyNavigation FindNavigationFor(ChallengeMapsMenu menu)
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
    }

    public sealed class AdventureLobbyChallengeMapRowAdapter
    {
        private static readonly AccessTools.FieldRef<LobbyChallengeMapEntry, GameObject> PlayedContainerRef =
            AccessTools.FieldRefAccess<LobbyChallengeMapEntry, GameObject>("_playedContainer");
        private static readonly AccessTools.FieldRef<LobbyChallengeMapEntry, UIImage[]> WinConditionIconsRef =
            AccessTools.FieldRefAccess<LobbyChallengeMapEntry, UIImage[]>("_winconditionIcons");

        private readonly AdventureLobbyChallengeMapSelectAdapter _owner;
        private readonly LobbyChallengeMapEntry _entry;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyChallengeMapRowAdapter(
            AdventureLobbyChallengeMapSelectAdapter owner,
            LobbyChallengeMapEntry entry,
            ILocalizationHandler localization)
        {
            _owner = owner;
            _entry = entry;
            _localization = localization;
        }

        public string NativeKey
        {
            get
            {
                string path = _entry != null && _entry.MapMetadata != null ? _entry.MapMetadata.PathName : null;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = _entry != null ? _entry.MapData.path : null;
                }

                return string.IsNullOrWhiteSpace(path) ? Name : path;
            }
        }

        public string Name
        {
            get { return SpeechTextSanitizer.Normalize(_entry != null ? _entry.LocalizedMapName : string.Empty); }
        }

        public IReadOnlyList<string> WinConditionLabels
        {
            get { return GetWinConditionLabels(); }
        }

        /// <summary>One tooltip per drawn win-condition icon, in the order of
        /// <see cref="WinConditionLabels"/>; a null entry is an icon the game drew without one.
        /// </summary>
        public IReadOnlyList<Tooltip> WinConditionTooltips
        {
            get
            {
                IReadOnlyList<string> labels = GetWinConditionLabels();
                UIImage[] icons = _entry != null ? WinConditionIconsRef(_entry) : null;
                List<Tooltip> tooltips = new List<Tooltip>(labels.Count);
                for (int i = 0; i < labels.Count; i++)
                {
                    UIImage icon = icons != null && i < icons.Length ? icons[i] : null;
                    tooltips.Add(HasTooltip(icon) ? Tooltip.ForComponent(icon, _localization) : null);
                }

                return tooltips;
            }
        }

        /// <summary>The row the game draws this challenge as.</summary>
        public Component Entry
        {
            get { return _entry; }
        }

        /// <summary>Whether this is the challenge the menu has selected.</summary>
        public bool IsSelected
        {
            get { return _owner != null && _owner.IsSelectedEntry(_entry); }
        }

        public bool IsCompleted
        {
            get
            {
                GameObject playedContainer = _entry != null ? PlayedContainerRef(_entry) : null;
                return playedContainer != null && playedContainer.activeInHierarchy;
            }
        }

        public string CompletedLabel
        {
            get { return GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/Completed", "Completed"); }
        }

        public string NotCompletedLabel
        {
            get { return GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/NotCompleted", "Not completed"); }
        }

        public string Description
        {
            get { return _owner != null ? _owner.GetMapInfoText(_entry) : string.Empty; }
        }

        public void FocusNative()
        {
            _owner?.FocusEntry(_entry);
        }

        public bool Select()
        {
            return _owner != null && _owner.SelectEntry(_entry);
        }

        public Tooltip GetCellTooltip(string columnId)
        {
            if (columnId != "win-condition")
            {
                return null;
            }

            return GetWinConditionTooltip();
        }

        private IReadOnlyList<string> GetWinConditionLabels()
        {
            MapFormat.AdventureMapMetadata metadata = _entry != null ? _entry.MapMetadata : null;
            if (metadata == null || metadata.WinConditions == null)
            {
                return new string[0];
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < metadata.WinConditions.Length; i++)
            {
                AdventureWinCondition condition = metadata.WinConditions[i];
                AddIfNotEmpty(parts, GetLocalizedText("GameModes/" + condition + "/Name", condition.ToString()));
            }

            return parts;
        }

        private Tooltip GetWinConditionTooltip()
        {
            UIImage[] icons = _entry != null ? WinConditionIconsRef(_entry) : null;
            if (icons == null || icons.Length == 0)
            {
                return null;
            }

            List<Component> components = new List<Component>();
            for (int i = 0; i < icons.Length; i++)
            {
                UIImage icon = icons[i];
                if (HasTooltip(icon))
                {
                    components.Add(icon);
                }
            }

            if (components.Count == 0)
            {
                return null;
            }

            return new Tooltip(
                () => GetCombinedTooltipLines(components),
                VisualTooltipMetadata.ForComponent(components[0]));
        }

        private bool HasTooltip(Component component)
        {
            return IsVisible(component)
                && NativeTooltipUtility.GetTooltipLinesForComponent(component, _localization).Count > 0;
        }

        private IReadOnlyList<string> GetCombinedTooltipLines(IReadOnlyList<Component> components)
        {
            List<string> lines = new List<string>();
            if (components == null)
            {
                return lines;
            }

            for (int i = 0; i < components.Count; i++)
            {
                IReadOnlyList<string> componentLines = NativeTooltipUtility.GetTooltipLinesForComponent(components[i], _localization);
                for (int j = 0; j < componentLines.Count; j++)
                {
                    AddIfNotDuplicate(lines, componentLines[j]);
                }
            }

            return lines;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback ?? string.Empty));
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        private static void AddIfNotDuplicate(List<string> lines, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i], line, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            lines.Add(line);
        }
    }
}
