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

    public sealed class AdventureLobbyMapSelectAdapter : IPresent
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

        // Fixed for the adapter's lifetime: the seven sort buttons, their drawn captions, the filter
        // band and its clear button are serialized fields of the menu, and a new menu is a new adapter.
        private readonly MapSelectSortButtonAdapter[] _sortButtons;
        private readonly string[] _columnLabels = new string[7];
        private readonly bool[] _columnLabelResolved = new bool[7];
        private string[] _columnLabelList;
        private string _title;
        private List<MapSelectFilterAdapter> _filters;
        private IMenuButtonAdapter _clearFiltersButton;
        private bool _clearFiltersProbed;

        // The visible rows, kept while the table's membership and drawn order are unchanged: a row
        // adapter memoizes its labels and tooltips, which is what keeps the per-frame build cheap.
        private static readonly AdventureLobbyMapSelectRowAdapter[] NoRows = new AdventureLobbyMapSelectRowAdapter[0];

        private readonly List<LobbyMapSelectMenuEntry> _visibleScratch = new List<LobbyMapSelectMenuEntry>();
        private List<AdventureLobbyMapSelectRowAdapter> _rows;
        private int _rowsSignature;
        private LobbyMapSelectMenuEntry _selectedEntry;
        private AdventureLobbyMapSelectRowAdapter _selectedRow;

        public AdventureLobbyMapSelectAdapter(MapSelectMenu menu, LobbyNavigation navigation)
        {
            _menu = menu;
            _navigation = navigation;
            _localization = menu != null ? LocalizationRef(menu) : GlobalLocalizationVariables.LocalizationHandler;

            _sortButtons = menu != null
                ? new[]
                {
                    new MapSelectSortButtonAdapter(this, SortTypeButtonRef(menu)),
                    new MapSelectSortButtonAdapter(this, SortNameButtonRef(menu)),
                    new MapSelectSortButtonAdapter(this, SortTagButtonRef(menu)),
                    new MapSelectSortButtonAdapter(this, SortWinConditionButtonRef(menu)),
                    new MapSelectSortButtonAdapter(this, SortPlayersButtonRef(menu)),
                    new MapSelectSortButtonAdapter(this, SortSizeButtonRef(menu)),
                    new MapSelectSortButtonAdapter(this, SortCompletedButtonRef(menu))
                }
                : new MapSelectSortButtonAdapter[0];

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
            get { return _title ?? (_title = GetLocalizedText("Lobby/MapSelect/Title", "Select Map")); }
        }

        /// <summary>The map name the preview panel draws beside the table, which the game sets from
        /// the selected map's metadata rather than from the entry's own row text.</summary>
        public string PreviewTitle
        {
            get { return _menu != null ? LobbyMapPreviewText.GetTitle(PreviewRef(_menu)) : string.Empty; }
        }

        public AdventureLobbyMapSelectRowAdapter SelectedRow
        {
            get
            {
                LobbyMapSelectMenuEntry selected = _menu != null ? SelectedEntryRef(_menu) : null;
                if (selected == null)
                {
                    _selectedEntry = null;
                    _selectedRow = null;
                    return null;
                }

                if (!ReferenceEquals(_selectedEntry, selected))
                {
                    _selectedEntry = selected;
                    _selectedRow = new AdventureLobbyMapSelectRowAdapter(this, selected, _localization);
                }

                return _selectedRow;
            }
        }

        /// <summary>The drawn rows in drawn order. One cheap pass over the menu's own entry list says
        /// whether the table still holds the same rows in the same order; while it does, the kept row
        /// adapters - and everything they have already read off the game - are handed back unchanged.
        /// </summary>
        public IReadOnlyList<AdventureLobbyMapSelectRowAdapter> GetVisibleRows()
        {
            List<LobbyMapSelectMenuEntry> entries = _menu != null ? EntriesRef(_menu) : null;
            if (entries == null)
            {
                _rows = null;
                return NoRows;
            }

            _visibleScratch.Clear();
            int signature = 17;
            for (int i = 0; i < entries.Count; i++)
            {
                LobbyMapSelectMenuEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                GameObject gameObject = ((Component)entry).gameObject;
                if (gameObject == null || !gameObject.activeInHierarchy)
                {
                    continue;
                }

                _visibleScratch.Add(entry);
                unchecked
                {
                    signature = (signature * 31) + entry.GetInstanceID();
                    signature = (signature * 31) + ((Component)entry).transform.GetSiblingIndex();
                }
            }

            if (_rows != null && _rows.Count == _visibleScratch.Count && _rowsSignature == signature)
            {
                _visibleScratch.Clear();
                return _rows;
            }

            _visibleScratch.Sort(CompareVisualOrder);
            List<AdventureLobbyMapSelectRowAdapter> rows = new List<AdventureLobbyMapSelectRowAdapter>(_visibleScratch.Count);
            for (int i = 0; i < _visibleScratch.Count; i++)
            {
                rows.Add(new AdventureLobbyMapSelectRowAdapter(this, _visibleScratch[i], _localization));
            }

            _visibleScratch.Clear();
            _rows = rows;
            _rowsSignature = signature;
            return _rows;
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
            return _sortButtons;
        }

        public IReadOnlyList<MapSelectFilterAdapter> GetFilters()
        {
            LobbyMapFilters nativeFilters = _menu != null ? FiltersRef(_menu) : null;
            if (nativeFilters == null)
            {
                return new MapSelectFilterAdapter[0];
            }

            if (_filters != null)
            {
                return _filters;
            }

            LobbyMapFilters it = nativeFilters;
            List<MapSelectFilterAdapter> filters = new List<MapSelectFilterAdapter>();
            AddFilter(filters, nativeFilters, GetColumnLabel(0), FilterMapTypeDropdownField, null, GetMapTypeFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(2), FilterMapTagDropdownField, null, GetMapTagFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(3), FilterWinConditionDropdownField, null, GetWinConditionFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(4), FilterPlayersDropdownField, null, GetPlayersFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(5), FilterSizeDropdownField, null, GetSizeFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetColumnLabel(6), FilterPlayedDropdownField, null, GetCompletedFilterOptionLabel);
            AddFilter(filters, nativeFilters, GetLocalizedText("LevelEditor/ContentProfile/Name", "Content profile"), FilterContentProfileDropdownField, () => IsContentProfileFilterVisible(it), GetContentProfileFilterOptionLabel);
            _filters = filters;
            return _filters;
        }

        public IMenuButtonAdapter GetClearFiltersButton()
        {
            if (_clearFiltersProbed)
            {
                return _clearFiltersButton;
            }

            LobbyMapFilters nativeFilters = _menu != null ? FiltersRef(_menu) : null;
            UIButton button = nativeFilters != null && FilterClearButtonField != null
                ? FilterClearButtonField.GetValue(nativeFilters) as UIButton
                : null;

            _clearFiltersProbed = true;
            _clearFiltersButton = button != null
                ? new StandardMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
            return _clearFiltersButton;
        }

        public IReadOnlyList<string> GetColumnLabels()
        {
            return _columnLabelList ?? (_columnLabelList = new[]
            {
                GetColumnLabel(0),
                GetColumnLabel(1),
                GetColumnLabel(2),
                GetColumnLabel(3),
                GetColumnLabel(4),
                GetColumnLabel(5),
                GetColumnLabel(6)
            });
        }

        /// <summary>The caption the column's heading draws, read off the game once: a sort button's own
        /// label is a subtree walk, and the band is set up before the page is navigable.</summary>
        private string GetColumnLabel(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _columnLabels.Length)
            {
                return ResolveColumnLabel(columnIndex);
            }

            if (!_columnLabelResolved[columnIndex])
            {
                _columnLabels[columnIndex] = ResolveColumnLabel(columnIndex);
                _columnLabelResolved[columnIndex] = true;
            }

            return _columnLabels[columnIndex];
        }

        private string ResolveColumnLabel(int columnIndex)
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

        /// <summary>Whether this entry is the one the menu currently has selected - the map the
        /// preview panel is showing and Confirm would take.</summary>
        public bool IsSelectedEntry(LobbyMapSelectMenuEntry entry)
        {
            return _menu != null && entry != null && ReferenceEquals(SelectedEntryRef(_menu), entry);
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
            Func<bool> isVisible,
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

            return SpokenLines.Clean(GameText.Get(_localization, key, fallback ?? string.Empty));
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

        // Everything the row reads off the map's own metadata and off the game's tooltip data is fixed
        // for the life of the row, and a tooltip's existence can only be answered by capturing it, so
        // each is read once and kept. The live parts - the selection, the preview text - are not here.
        private string _name;
        private string _nativeKey;
        private string _typeLabel;
        private string _sizeLabel;
        private string _completedLabel;
        private string _notCompletedLabel;
        private IReadOnlyList<string> _tagLabels;
        private IReadOnlyList<string> _winConditionLabels;
        private IReadOnlyList<Tooltip> _winConditionTooltips;
        private Tooltip _typeTooltip;
        private bool _typeTooltipProbed;
        private Tooltip _tagTooltipCache;
        private bool _tagTooltipProbed;
        private Tooltip _winConditionTooltip;
        private bool _winConditionTooltipProbed;

        public AdventureLobbyMapSelectRowAdapter(AdventureLobbyMapSelectAdapter owner, LobbyMapSelectMenuEntry entry, ILocalizationHandler localization)
        {
            _owner = owner;
            _entry = entry;
            _localization = localization;
        }

        public string NativeKey
        {
            get { return _nativeKey ?? (_nativeKey = ResolveNativeKey()); }
        }

        private string ResolveNativeKey()
        {
            string path = _entry != null && _entry.Map != null && _entry.Map.Metadata != null ? _entry.Map.Metadata.PathName : null;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = _entry != null ? _entry.MapData.path : null;
            }

            return string.IsNullOrWhiteSpace(path) ? Name : path;
        }

        public string Name
        {
            get { return _name ?? (_name = SpokenLines.Clean(_entry != null ? _entry.PrettyMapName : string.Empty)); }
        }

        public MapFormat.AdventureMapMetadata Metadata
        {
            get { return _entry != null ? _entry.MetaData : null; }
        }

        /// <summary>The row the game draws this map as.</summary>
        public Component Entry
        {
            get { return _entry; }
        }

        /// <summary>Whether this is the map the menu has selected.</summary>
        public bool IsSelected
        {
            get { return _owner != null && _owner.IsSelectedEntry(_entry); }
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
                    if (!_typeTooltipProbed)
                    {
                        _typeTooltip = GetComponentTooltip(IconRef(_entry));
                        _typeTooltipProbed = true;
                    }

                    return _typeTooltip;
                case "tag":
                    if (!_tagTooltipProbed)
                    {
                        _tagTooltipCache = GetTagTooltip();
                        _tagTooltipProbed = true;
                    }

                    return _tagTooltipCache;
                case "win-condition":
                    if (!_winConditionTooltipProbed)
                    {
                        _winConditionTooltip = GetWinConditionTooltip();
                        _winConditionTooltipProbed = true;
                    }

                    return _winConditionTooltip;
                default:
                    return null;
            }
        }

        public string TypeLabel
        {
            get { return _typeLabel ?? (_typeLabel = GetTypeLabel()); }
        }

        public IReadOnlyList<string> TagLabels
        {
            get { return _tagLabels ?? (_tagLabels = GetTagLabels()); }
        }

        public IReadOnlyList<string> WinConditionLabels
        {
            get { return _winConditionLabels ?? (_winConditionLabels = GetWinConditionLabels()); }
        }

        /// <summary>One tooltip per drawn win-condition icon, in the order of
        /// <see cref="WinConditionLabels"/>: the game hangs a name and an objective on each icon
        /// (<c>LobbyMapSelectMenuEntry.Setup</c>), and a null entry is an icon it drew without one.
        /// </summary>
        public IReadOnlyList<Tooltip> WinConditionTooltips
        {
            get { return _winConditionTooltips ?? (_winConditionTooltips = BuildWinConditionTooltips()); }
        }

        private IReadOnlyList<Tooltip> BuildWinConditionTooltips()
        {
            IReadOnlyList<string> labels = WinConditionLabels;
            UIImage[] icons = _entry != null ? WinConditionIconsRef(_entry) : null;
            List<Tooltip> tooltips = new List<Tooltip>(labels.Count);
            for (int i = 0; i < labels.Count; i++)
            {
                UIImage icon = icons != null && i < icons.Length ? icons[i] : null;
                tooltips.Add(GetComponentTooltip(icon));
            }

            return tooltips;
        }

        public int Players
        {
            get { return _entry != null ? _entry.Players : 0; }
        }

        public string SizeLabel
        {
            get { return _sizeLabel ?? (_sizeLabel = GetSizeLabel()); }
        }

        public bool IsCompleted
        {
            get { return _entry != null && _entry.IsCompleted(); }
        }

        public string CompletedLabel
        {
            get { return _completedLabel ?? (_completedLabel = GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/Completed", "Completed")); }
        }

        public string NotCompletedLabel
        {
            get { return _notCompletedLabel ?? (_notCompletedLabel = GetLocalizedText("Lobby/MapSelect/Filter/FilterButton/NotCompleted", "Not completed")); }
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

            return SpokenLines.Clean(GameText.Get(_localization, key, fallback ?? string.Empty));
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

        /// <summary>The heading the game draws for this column.</summary>
        public Component Button
        {
            get { return _button; }
        }

        public string Label
        {
            get { return SpokenLines.Clean(MenuButtonTextUtility.GetStandardButtonLabel(_button)); }
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
        private readonly Func<bool> _isVisible;
        private List<Option> _options;

        public MapSelectFilterAdapter(string label, UIFilterDropdown dropdown, Func<bool> isVisible, Func<int, string> getOptionLabel)
        {
            Label = label ?? string.Empty;
            _dropdown = dropdown;
            _isVisible = isVisible;
            _getOptionLabel = getOptionLabel;
        }

        public string Label { get; private set; }

        /// <summary>Null means a filter the game always draws; the content-profile one is drawn only
        /// while its container is.</summary>
        public bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        /// <summary>The filter button the game draws in the header band.</summary>
        public Component Subject
        {
            get { return _dropdown; }
        }

        /// <summary>Whether the game is showing this filter's list of checkboxes right now.</summary>
        public bool IsOpen
        {
            get { return AdventureLobbyMapSelectAdapter.IsDropdownOpen(_dropdown); }
        }

        public void OpenNative()
        {
            AdventureLobbyMapSelectAdapter.OpenDropdown(_dropdown);
        }

        public void CloseNative()
        {
            AdventureLobbyMapSelectAdapter.CloseDropdown(_dropdown);
        }

        /// <summary>The checkboxes the dropdown holds. The list is a serialized field the game fills
        /// once, so the options are resolved once too.</summary>
        public IReadOnlyList<Option> GetOptions()
        {
            if (_options != null)
            {
                return _options;
            }

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

            _options = options;
            return _options;
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
                _label = SpokenLines.Clean(_toggle != null ? _toggle.Text : string.Empty);
                if (string.IsNullOrWhiteSpace(_label))
                {
                    _label = fallbackLabel ?? string.Empty;
                }
            }

            public int Index { get; private set; }

            /// <summary>The checkbox the game draws in the filter's list.</summary>
            public Component Subject
            {
                get { return _toggle; }
            }

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
