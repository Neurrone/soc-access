using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Addons;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Lobby;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquest.Server.Adventure.Map.Provider;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public enum MapSelectSortDirection
    {
        None,
        Ascending,
        Descending
    }

    public sealed class AdventureLobbyMapSelectAdapter
    {
        private static readonly AccessTools.FieldRef<MapSelectMenu, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<MapSelectMenu, CanvasGroup>("_canvasGroup");
        private static readonly AccessTools.FieldRef<MapSelectMenu, UIButton> SelectButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, UIButton>("_selectButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, AutoScrollToSelected> AutoScrollerRef =
            AccessTools.FieldRefAccess<MapSelectMenu, AutoScrollToSelected>("_autoScroller");
        private static readonly AccessTools.FieldRef<MapSelectMenu, LobbyMapPreview> PreviewRef =
            AccessTools.FieldRefAccess<MapSelectMenu, LobbyMapPreview>("_preview");
        private static readonly AccessTools.FieldRef<MapSelectMenu, LobbyMapFilters> FiltersRef =
            AccessTools.FieldRefAccess<MapSelectMenu, LobbyMapFilters>("_filters");
        private static readonly AccessTools.FieldRef<MapSelectMenu, TableSortUIButton> SortSizeButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, TableSortUIButton>("_sortSizeButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, TableSortUIButton> SortPlayersButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, TableSortUIButton>("_sortPlayersButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, TableSortUIButton> SortNameButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, TableSortUIButton>("_sortNameButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, TableSortUIButton> SortTagButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, TableSortUIButton>("_sortTagButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, TableSortUIButton> SortWinConditionButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, TableSortUIButton>("_sortWinConditionButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, TableSortUIButton> SortTypeButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, TableSortUIButton>("_sortTypeButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, TableSortUIButton> SortCompletedButtonRef =
            AccessTools.FieldRefAccess<MapSelectMenu, TableSortUIButton>("_sortCompletedButton");
        private static readonly AccessTools.FieldRef<MapSelectMenu, LobbyMapSelectMenuEntry> SelectedEntryRef =
            AccessTools.FieldRefAccess<MapSelectMenu, LobbyMapSelectMenuEntry>("_selectedEntry");
        private static readonly AccessTools.FieldRef<MapSelectMenu, List<LobbyMapSelectMenuEntry>> EntriesRef =
            AccessTools.FieldRefAccess<MapSelectMenu, List<LobbyMapSelectMenuEntry>>("_entries");
        private static readonly AccessTools.FieldRef<MapSelectMenu, bool> EntriesLoadedRef =
            AccessTools.FieldRefAccess<MapSelectMenu, bool>("_entriesLoaded");
        private static readonly AccessTools.FieldRef<MapSelectMenu, ILocalizationHandler> LocalizationRef =
            AccessTools.FieldRefAccess<MapSelectMenu, ILocalizationHandler>("_localizationHandler");
        private static readonly AccessTools.FieldRef<MapSelectMenu, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<MapSelectMenu, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<LobbyNavigation, UIBackButton> CommonBackButtonRef =
            AccessTools.FieldRefAccess<LobbyNavigation, UIBackButton>("_commonBackButton");
        private static readonly AccessTools.FieldRef<LobbyNavigation, MainMenuManagerContainer> NavigationManagerContainerRef =
            AccessTools.FieldRefAccess<LobbyNavigation, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");
        private static readonly FieldInfo FilterContentProfileContainerField =
            AccessTools.Field(typeof(LobbyMapFilters), "_contentProfileContainer");
        private static readonly FieldInfo FilterWinConditionDropdownField =
            AccessTools.Field(typeof(LobbyMapFilters), "_winConditionDropdown");
        private static readonly FieldInfo FilterMapTagDropdownField =
            AccessTools.Field(typeof(LobbyMapFilters), "_mapTagDropdown");
        private static readonly FieldInfo FilterMapTypeDropdownField =
            AccessTools.Field(typeof(LobbyMapFilters), "_mapTypeDropdown");
        private static readonly FieldInfo FilterPlayersDropdownField =
            AccessTools.Field(typeof(LobbyMapFilters), "_playersDropdown");
        private static readonly FieldInfo FilterSizeDropdownField =
            AccessTools.Field(typeof(LobbyMapFilters), "_sizeDropdown");
        private static readonly FieldInfo FilterPlayedDropdownField =
            AccessTools.Field(typeof(LobbyMapFilters), "_playedDropdown");
        private static readonly FieldInfo FilterContentProfileDropdownField =
            AccessTools.Field(typeof(LobbyMapFilters), "_contentProfileDrowdown");
        private static readonly FieldInfo FilterClearButtonField =
            AccessTools.Field(typeof(LobbyMapFilters), "_clearButton");
        private static readonly FieldInfo FilterDropdownTogglesField =
            AccessTools.Field(typeof(UIFilterDropdown), "_toggles");
        private static readonly FieldInfo FilterDropdownToggleContainerField =
            AccessTools.Field(typeof(UIFilterDropdown), "_toggleContainer");
        private static readonly MethodInfo SetSelectedEntryMethod =
            AccessTools.Method(typeof(MapSelectMenu), "SetSelectedEntry");
        private static readonly MethodInfo SortSiblingsMethod =
            AccessTools.Method(typeof(MapSelectMenu), "SortSiblings");
        private static readonly MethodInfo FilterDropdownShowMethod =
            AccessTools.Method(typeof(UIFilterDropdown), "Show");
        private static readonly MethodInfo FilterDropdownCloseMethod =
            AccessTools.Method(typeof(UIFilterDropdown), "Close");

        private readonly MapSelectMenu _menu;
        private readonly LobbyNavigation _navigation;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyMapSelectAdapter(MapSelectMenu menu)
        {
            _menu = menu;
            _navigation = FindNavigationFor(menu);
            _localization = menu != null ? LocalizationRef(menu) : GlobalLocalizationVariables.LocalizationHandler;

            SelectButton = new StandardMenuButtonAdapter(SelectButtonRef(menu));
            BackButton = CreateBackButton();
            OptionsButton = CreateOptionsButton();
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IMenuButtonAdapter SelectButton { get; private set; }

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
                && EntriesLoadedRef(_menu);
        }

        public string Title
        {
            get { return GetLocalizedText("Lobby/MapSelect/Title", "Select Map"); }
        }

        public string MapsLabel
        {
            get { return ModText.Get(ModStrings.Common.ListSeparator, Title, ModText.Get(ModStrings.UI.RoleGrid)); }
        }

        public AdventureLobbyMapSelectRowAdapter SelectedRow
        {
            get
            {
                LobbyMapSelectMenuEntry selected = _menu != null ? SelectedEntryRef(_menu) : null;
                return selected != null ? new AdventureLobbyMapSelectRowAdapter(this, selected, _localization) : null;
            }
        }

        public IReadOnlyList<AdventureLobbyMapSelectRowAdapter> GetVisibleRows()
        {
            List<AdventureLobbyMapSelectRowAdapter> rows = new List<AdventureLobbyMapSelectRowAdapter>();
            List<LobbyMapSelectMenuEntry> visibleEntries = new List<LobbyMapSelectMenuEntry>();
            List<LobbyMapSelectMenuEntry> entries = _menu != null ? EntriesRef(_menu) : null;
            if (entries == null)
            {
                return rows;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                LobbyMapSelectMenuEntry entry = entries[i];
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
                rows.Add(new AdventureLobbyMapSelectRowAdapter(this, visibleEntries[i], _localization));
            }

            return rows;
        }

        private static int CompareVisualOrder(LobbyMapSelectMenuEntry left, LobbyMapSelectMenuEntry right)
        {
            int result = GetSiblingIndex(left).CompareTo(GetSiblingIndex(right));
            if (result != 0)
            {
                return result;
            }

            return string.CompareOrdinal(GetEntryName(left), GetEntryName(right));
        }

        private static int GetSiblingIndex(LobbyMapSelectMenuEntry entry)
        {
            return entry != null ? ((Component)entry).transform.GetSiblingIndex() : int.MaxValue;
        }

        private static string GetEntryName(LobbyMapSelectMenuEntry entry)
        {
            return entry != null ? entry.PrettyMapName ?? string.Empty : string.Empty;
        }

        public IReadOnlyList<MapSelectSortButtonAdapter> GetSortButtons()
        {
            return new[]
            {
                new MapSelectSortButtonAdapter(this, SortTypeButtonRef(_menu)),
                new MapSelectSortButtonAdapter(this, SortNameButtonRef(_menu)),
                new MapSelectSortButtonAdapter(this, SortTagButtonRef(_menu)),
                new MapSelectSortButtonAdapter(this, SortWinConditionButtonRef(_menu)),
                new MapSelectSortButtonAdapter(this, SortPlayersButtonRef(_menu)),
                new MapSelectSortButtonAdapter(this, SortSizeButtonRef(_menu)),
                new MapSelectSortButtonAdapter(this, SortCompletedButtonRef(_menu))
            };
        }

        public IReadOnlyList<MapSelectFilterAdapter> GetFilters()
        {
            List<MapSelectFilterAdapter> filters = new List<MapSelectFilterAdapter>();
            LobbyMapFilters nativeFilters = _menu != null ? FiltersRef(_menu) : null;
            if (nativeFilters == null)
            {
                return filters;
            }

            AddFilter(filters, nativeFilters, GetColumnLabel(0), FilterMapTypeDropdownField, true, GetMapTypeFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(2), FilterMapTagDropdownField, true, GetMapTagFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(3), FilterWinConditionDropdownField, true, GetWinConditionFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(4), FilterPlayersDropdownField, true, GetPlayersFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(5), FilterSizeDropdownField, true, GetSizeFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(6), FilterPlayedDropdownField, true, GetCompletedFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetLocalizedText("LevelEditor/ContentProfile/Name", "Content profile"), FilterContentProfileDropdownField, IsContentProfileFilterVisible(nativeFilters), GetContentProfileFilterOptionLabel);
            return filters;
        }

        public IMenuButtonAdapter GetClearFiltersButton()
        {
            LobbyMapFilters nativeFilters = _menu != null ? FiltersRef(_menu) : null;
            UIButton button = nativeFilters != null && FilterClearButtonField != null
                ? FilterClearButtonField.GetValue(nativeFilters) as UIButton
                : null;

            return button != null
                ? new StandardMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
        }

        public IReadOnlyList<string> GetColumnLabels()
        {
            return new[]
            {
                GetColumnLabel(0),
                GetColumnLabel(1),
                GetColumnLabel(2),
                GetColumnLabel(3),
                GetColumnLabel(4),
                GetColumnLabel(5),
                GetColumnLabel(6)
            };
        }

        private string GetColumnLabel(int columnIndex)
        {
            MapSelectSortButtonAdapter button = GetSortButton(columnIndex);
            if (button != null && !string.IsNullOrWhiteSpace(button.Label))
            {
                return button.Label;
            }

            switch (columnIndex)
            {
                case 0:
                    return GetLocalizedText("Lobby/MapSelect/Filter/MapType", "Type");
                case 1:
                    return GetLocalizedText("Common/Name", "Name");
                case 2:
                    return GetLocalizedText("LevelEditor/MapSettings/Tags", "Tag");
                case 3:
                    return GetLocalizedText("Lobby/GameMode", "Win condition");
                case 4:
                    return GetLocalizedText("Common/Players", "Players");
                case 5:
                    return GetLocalizedText("Common/Size", "Size");
                case 6:
                    return GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/Completed", "Completed");
                default:
                    return string.Empty;
            }
        }

        public void FocusEntry(LobbyMapSelectMenuEntry entry)
        {
            if (_menu == null || entry == null)
            {
                return;
            }

            if (!ReferenceEquals(SelectedEntryRef(_menu), entry) && SetSelectedEntryMethod != null)
            {
                SetSelectedEntryMethod.Invoke(_menu, new object[] { entry });
            }

            Selectable selectable = entry.GetSelectable();
            NativeSelectionUtility.Select(selectable);
            AutoScrollToSelected autoScroller = AutoScrollerRef(_menu);
            if (autoScroller != null && ((Behaviour)autoScroller).isActiveAndEnabled)
            {
                autoScroller.ForceFocusOn(selectable);
            }
        }

        public bool ActivateEntry(LobbyMapSelectMenuEntry entry)
        {
            if (entry == null || entry.Button == null)
            {
                return false;
            }

            return NativeSelectionUtility.Click(entry.Button);
        }

        public string GetMapInfoText(LobbyMapSelectMenuEntry entry)
        {
            if (_menu == null || entry == null || !ReferenceEquals(SelectedEntryRef(_menu), entry))
            {
                return string.Empty;
            }

            return LobbyMapPreviewText.GetInfo(PreviewRef(_menu));
        }

        private MapSelectSortButtonAdapter GetSortButton(int columnIndex)
        {
            IReadOnlyList<MapSelectSortButtonAdapter> buttons = GetSortButtons();
            return columnIndex >= 0 && columnIndex < buttons.Count ? buttons[columnIndex] : null;
        }

        public void SortSiblings()
        {
            if (_menu != null && SortSiblingsMethod != null)
            {
                SortSiblingsMethod.Invoke(_menu, null);
            }
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
            if (container == null && _menu != null)
            {
                container = ManagerContainerRef(_menu);
            }

            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private void AddFilter(
            List<MapSelectFilterAdapter> filters,
            LobbyMapFilters nativeFilters,
            string label,
            FieldInfo dropdownField,
            bool isVisible,
            Func<int, string> getOptionLabel)
        {
            UIFilterDropdown dropdown = nativeFilters != null && dropdownField != null
                ? dropdownField.GetValue(nativeFilters) as UIFilterDropdown
                : null;
            if (dropdown != null)
            {
                filters.Add(new MapSelectFilterAdapter(label, dropdown, isVisible, getOptionLabel));
            }
        }

        private string GetMapTypeFilterOptionLabel(int index)
        {
            List<MapProviderType> values = new List<MapProviderType>();
            foreach (MapProviderType type in Enum.GetValues(typeof(MapProviderType)))
            {
                if (type != MapProviderType.Random)
                {
                    values.Add(type);
                }
            }

            return index >= 0 && index < values.Count
                ? GetLocalizedText("DataTypes/MapProviderType/" + values[index], values[index].ToString())
                : string.Empty;
        }

        private string GetMapTagFilterOptionLabel(int index)
        {
            List<MapTag> values = new List<MapTag>();
            foreach (MapTag tag in Enum.GetValues(typeof(MapTag)))
            {
                if (tag != MapTag.Challenge)
                {
                    values.Add(tag);
                }
            }

            return index >= 0 && index < values.Count
                ? GetLocalizedText("DataTypes/MapTag/" + values[index], values[index].ToString())
                : string.Empty;
        }

        private string GetWinConditionFilterOptionLabel(int index)
        {
            AdventureWinCondition[] values = (AdventureWinCondition[])Enum.GetValues(typeof(AdventureWinCondition));
            return index >= 0 && index < values.Length
                ? GetLocalizedText("GameModes/" + values[index] + "/Name", values[index].ToString())
                : string.Empty;
        }

        private string GetPlayersFilterOptionLabel(int index)
        {
            return index >= 0 && index < 8 ? (index + 1).ToString() : string.Empty;
        }

        private string GetSizeFilterOptionLabel(int index)
        {
            MapSize[] values = (MapSize[])Enum.GetValues(typeof(MapSize));
            return index >= 0 && index < values.Length
                ? GetLocalizedText("Adventure/MapSize/" + values[index], values[index].ToString())
                : string.Empty;
        }

        private string GetCompletedFilterOptionLabel(int index)
        {
            if (index == 0)
            {
                return GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/Completed", "Completed");
            }

            return index == 1
                ? GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/NotCompleted", "Not completed")
                : string.Empty;
        }

        private string GetContentProfileFilterOptionLabel(int index)
        {
            ContentProfileType[] values = (ContentProfileType[])Enum.GetValues(typeof(ContentProfileType));
            return index >= 0 && index < values.Length ? values[index].ToString() : string.Empty;
        }

        private bool IsContentProfileFilterVisible(LobbyMapFilters nativeFilters)
        {
            GameObject container = nativeFilters != null && FilterContentProfileContainerField != null
                ? FilterContentProfileContainerField.GetValue(nativeFilters) as GameObject
                : null;
            return container != null && container.activeInHierarchy;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback ?? string.Empty));
        }

        private static LobbyNavigation FindNavigationFor(MapSelectMenu menu)
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

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        public static IReadOnlyList<UIToggle> GetDropdownToggles(UIFilterDropdown dropdown)
        {
            return dropdown != null && FilterDropdownTogglesField != null
                ? FilterDropdownTogglesField.GetValue(dropdown) as IReadOnlyList<UIToggle> ?? new UIToggle[0]
                : new UIToggle[0];
        }

        public static bool IsDropdownOpen(UIFilterDropdown dropdown)
        {
            UITransform container = dropdown != null && FilterDropdownToggleContainerField != null
                ? FilterDropdownToggleContainerField.GetValue(dropdown) as UITransform
                : null;
            return container != null && container.Active;
        }

        public static void OpenDropdown(UIFilterDropdown dropdown)
        {
            if (dropdown != null && !IsDropdownOpen(dropdown) && FilterDropdownShowMethod != null)
            {
                FilterDropdownShowMethod.Invoke(dropdown, null);
            }
        }

        public static void CloseDropdown(UIFilterDropdown dropdown)
        {
            if (dropdown != null && IsDropdownOpen(dropdown) && FilterDropdownCloseMethod != null)
            {
                FilterDropdownCloseMethod.Invoke(dropdown, null);
            }
        }
    }

    public sealed class AdventureLobbyMapSelectRowAdapter
    {
        private static readonly AccessTools.FieldRef<LobbyMapSelectMenuEntry, UIImage> IconRef =
            AccessTools.FieldRefAccess<LobbyMapSelectMenuEntry, UIImage>("_icon");
        private static readonly AccessTools.FieldRef<LobbyMapSelectMenuEntry, UIImage> TagTooltipImageRef =
            AccessTools.FieldRefAccess<LobbyMapSelectMenuEntry, UIImage>("_tagTooltipImage");
        private static readonly AccessTools.FieldRef<LobbyMapSelectMenuEntry, UITextMesh> TagTextRef =
            AccessTools.FieldRefAccess<LobbyMapSelectMenuEntry, UITextMesh>("_tagText");
        private static readonly AccessTools.FieldRef<LobbyMapSelectMenuEntry, UIImage[]> WinConditionIconsRef =
            AccessTools.FieldRefAccess<LobbyMapSelectMenuEntry, UIImage[]>("_winconditionIcons");

        private readonly AdventureLobbyMapSelectAdapter _owner;
        private readonly LobbyMapSelectMenuEntry _entry;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyMapSelectRowAdapter(AdventureLobbyMapSelectAdapter owner, LobbyMapSelectMenuEntry entry, ILocalizationHandler localization)
        {
            _owner = owner;
            _entry = entry;
            _localization = localization;
        }

        public string NativeKey
        {
            get
            {
                string path = _entry != null && _entry.Map != null && _entry.Map.Metadata != null ? _entry.Map.Metadata.PathName : null;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = _entry != null ? _entry.MapData.path : null;
                }

                return string.IsNullOrWhiteSpace(path) ? Name : path;
            }
        }

        public string Name
        {
            get { return SpeechTextSanitizer.Normalize(_entry != null ? _entry.PrettyMapName : string.Empty); }
        }

        public MapFormat.AdventureMapMetadata Metadata
        {
            get { return _entry != null ? _entry.MetaData : null; }
        }

        public void FocusNative()
        {
            _owner?.FocusEntry(_entry);
        }

        public bool Activate()
        {
            return _owner != null && _owner.ActivateEntry(_entry);
        }

        public Tooltip GetCellTooltip(string columnId)
        {
            if (_entry == null)
            {
                return null;
            }

            switch (columnId)
            {
                case "type":
                    return GetComponentTooltip(IconRef(_entry));
                case "tag":
                    return GetTagTooltip();
                case "win-condition":
                    return GetWinConditionTooltip();
                default:
                    return null;
            }
        }

        public string TypeLabel
        {
            get { return GetTypeLabel(); }
        }

        public IReadOnlyList<string> TagLabels
        {
            get { return GetTagLabels(); }
        }

        public IReadOnlyList<string> WinConditionLabels
        {
            get { return GetWinConditionLabels(); }
        }

        public int Players
        {
            get { return _entry != null ? _entry.Players : 0; }
        }

        public string SizeLabel
        {
            get { return GetSizeLabel(); }
        }

        public bool IsCompleted
        {
            get { return _entry != null && _entry.IsCompleted(); }
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

        private string GetTypeLabel()
        {
            if (_entry == null)
            {
                return string.Empty;
            }

            MapProviderData data = _entry.MapData;
            if (data.exclusiveAddon == Addon.Vanir || data.name == "BarrenFrontier")
            {
                return GetLocalizedText("MainMenu/VanirDLC/Title", "Vanir");
            }

            if (data.exclusiveAddon == Addon.Roots || data.name == "Invasive")
            {
                return GetLocalizedText("MainMenu/RootsDLC/Title", "Roots");
            }

            if (data.exclusiveAddon == Addon.Yulan || data.name == "FreeYulan")
            {
                return GetLocalizedText("MainMenu/YulanDLC/Title", "Yulan");
            }

            return GetLocalizedText("DataTypes/MapProviderType/" + data.type, data.type.ToString());
        }

        private IReadOnlyList<string> GetTagLabels()
        {
            MapFormat.AdventureMapMetadata metadata = Metadata;
            if (metadata == null || metadata.MapTags == null)
            {
                return new string[0];
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < metadata.MapTags.Length; i++)
            {
                MapTag tag = metadata.MapTags[i];
                if (tag != MapTag.Uncategorized)
                {
                    AddIfNotEmpty(parts, GetLocalizedText("DataTypes/MapTag/" + tag, tag.ToString()));
                }
            }

            return parts;
        }

        private IReadOnlyList<string> GetWinConditionLabels()
        {
            MapFormat.AdventureMapMetadata metadata = Metadata;
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

        private string GetSizeLabel()
        {
            MapFormat.AdventureMapMetadata metadata = Metadata;
            if (metadata == null)
            {
                return string.Empty;
            }

            return metadata.Size.x != 0 ? metadata.Size.x + " x " + metadata.Size.y : string.Empty;
        }

        private Tooltip GetTagTooltip()
        {
            Tooltip tooltip = GetComponentTooltip(TagTooltipImageRef(_entry));
            return tooltip ?? GetComponentTooltip(TagTextRef(_entry));
        }

        private Tooltip GetWinConditionTooltip()
        {
            UIImage[] icons = WinConditionIconsRef(_entry);
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

        private Tooltip GetComponentTooltip(Component component)
        {
            return HasTooltip(component) ? Tooltip.ForComponent(component, _localization) : null;
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

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        private static bool IsVisible(Component component)
        {
            return component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy;
        }

        private static void AddIfNotDuplicate(List<string> parts, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalized = value.Trim();
            for (int i = 0; i < parts.Count; i++)
            {
                if (string.Equals(parts[i]?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            parts.Add(value);
        }
    }

    public sealed class MapSelectSortButtonAdapter
    {
        private readonly AdventureLobbyMapSelectAdapter _owner;
        private readonly TableSortUIButton _button;

        public MapSelectSortButtonAdapter(AdventureLobbyMapSelectAdapter owner, TableSortUIButton button)
        {
            _owner = owner;
            _button = button;
        }

        public string Label
        {
            get { return SpeechTextSanitizer.Normalize(MenuButtonTextUtility.GetStandardButtonLabel(_button)); }
        }

        public MapSelectSortDirection Direction
        {
            get
            {
                if (_button == null || _button.CurrentSortMode == TableSortUIButton.SortMode.None)
                {
                    return MapSelectSortDirection.None;
                }

                // MapSelectMenu.SortSiblings calls SetAsFirstSibling for each
                // sorted entry, so the visible row order is the reverse of the
                // native TableSortUIButton mode.
                return _button.CurrentSortMode == TableSortUIButton.SortMode.Ascending
                    ? MapSelectSortDirection.Descending
                    : MapSelectSortDirection.Ascending;
            }
        }

        public bool Activate()
        {
            if (_button == null)
            {
                return false;
            }

            if (_button.CurrentSortMode == TableSortUIButton.SortMode.Ascending)
            {
                _button.Reset();
                Action<IUIButton> clicked = _button.OnClickedSelf;
                if (clicked != null)
                {
                    clicked(_button);
                }
                else
                {
                    _owner?.SortSiblings();
                }

                return true;
            }

            return NativeSelectionUtility.Click(_button);
        }
    }

    public sealed class MapSelectFilterAdapter
    {
        private readonly UIFilterDropdown _dropdown;
        private readonly Func<int, string> _getOptionLabel;

        public MapSelectFilterAdapter(string label, UIFilterDropdown dropdown, bool isVisible, Func<int, string> getOptionLabel)
        {
            Label = label ?? string.Empty;
            _dropdown = dropdown;
            IsVisible = isVisible;
            _getOptionLabel = getOptionLabel;
        }

        public string Label { get; private set; }

        public bool IsVisible { get; private set; }

        public void OpenNative()
        {
            AdventureLobbyMapSelectAdapter.OpenDropdown(_dropdown);
        }

        public void CloseNative()
        {
            AdventureLobbyMapSelectAdapter.CloseDropdown(_dropdown);
        }

        public IReadOnlyList<Option> GetOptions()
        {
            List<Option> options = new List<Option>();
            IReadOnlyList<UIToggle> toggles = AdventureLobbyMapSelectAdapter.GetDropdownToggles(_dropdown);
            for (int i = 0; i < toggles.Count; i++)
            {
                UIToggle toggle = toggles[i];
                if (toggle != null)
                {
                    options.Add(new Option(this, i, toggle, GetOptionLabel(i)));
                }
            }

            return options;
        }

        private string GetOptionLabel(int index)
        {
            return _getOptionLabel != null ? _getOptionLabel(index) ?? string.Empty : string.Empty;
        }

        public sealed class Option
        {
            private readonly UIToggle _toggle;
            private readonly MapSelectFilterAdapter _owner;
            private readonly string _label;

            public Option(MapSelectFilterAdapter owner, int index, UIToggle toggle, string fallbackLabel)
            {
                _owner = owner;
                Index = index;
                _toggle = toggle;
                _label = SpeechTextSanitizer.Normalize(_toggle != null ? _toggle.Text : string.Empty);
                if (string.IsNullOrWhiteSpace(_label))
                {
                    _label = fallbackLabel ?? string.Empty;
                }
            }

            public int Index { get; private set; }

            public string Label
            {
                get { return _label; }
            }

            public bool IsChecked
            {
                get { return _toggle != null && _toggle.ToggleValue; }
            }

            public bool IsEnabled
            {
                get { return _toggle == null || _toggle.Interactable; }
            }

            public bool IsVisible
            {
                get
                {
                    return _toggle != null;
                }
            }

            public void Toggle()
            {
                if (_toggle != null && _toggle.Interactable)
                {
                    _toggle.ToggleValue = !_toggle.ToggleValue;
                }
            }

            public void FocusNative()
            {
                if (_toggle != null)
                {
                    _owner?.OpenNative();
                    NativeSelectionUtility.Select(_toggle.GetSelectable());
                }
            }

            public Tooltip GetTooltip()
            {
                Component component = _toggle != null ? _toggle.GetTextMesh() : null;
                if (component == null
                    || component.gameObject == null
                    || !component.gameObject.activeInHierarchy
                    || NativeTooltipUtility.GetTooltipLinesForComponent(component, null).Count == 0)
                {
                    return null;
                }

                return Tooltip.ForComponent(component, null);
            }
        }
    }
}
