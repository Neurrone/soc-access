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
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class AdventureLobbyChallengeMapSelectAdapter : IPresent
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

        // The visible rows, kept while the table's membership and drawn order are unchanged: a row
        // adapter memoizes its labels and tooltips, which is what keeps the per-frame build cheap.
        private readonly List<LobbyChallengeMapEntry> _visibleScratch = new List<LobbyChallengeMapEntry>();
        private List<AdventureLobbyChallengeMapRowAdapter> _rows;
        private int _rowsSignature;
        private LobbyChallengeMapEntry _selectedEntry;
        private AdventureLobbyChallengeMapRowAdapter _selectedRow;

        public AdventureLobbyChallengeMapSelectAdapter(ChallengeMapsMenu menu, LobbyNavigation navigation)
        {
            _menu = menu;
            _navigation = navigation;
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
                if (selected == null)
                {
                    _selectedEntry = null;
                    _selectedRow = null;
                    return null;
                }

                if (!ReferenceEquals(_selectedEntry, selected))
                {
                    _selectedEntry = selected;
                    _selectedRow = new AdventureLobbyChallengeMapRowAdapter(this, selected, _localization);
                }

                return _selectedRow;
            }
        }

        /// <summary>The drawn rows in drawn order. One cheap pass over the menu's own entry list says
        /// whether the table still holds the same rows in the same order; while it does, the kept row
        /// adapters - and everything they have already read off the game - are handed back unchanged.
        /// </summary>
        public IReadOnlyList<AdventureLobbyChallengeMapRowAdapter> GetVisibleRows()
        {
            IReadOnlyList<LobbyChallengeMapEntry> entries = GetEntries();
            _visibleScratch.Clear();
            int signature = 17;
            for (int i = 0; i < entries.Count; i++)
            {
                LobbyChallengeMapEntry entry = entries[i];
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
            List<AdventureLobbyChallengeMapRowAdapter> rows = new List<AdventureLobbyChallengeMapRowAdapter>(_visibleScratch.Count);
            for (int i = 0; i < _visibleScratch.Count; i++)
            {
                rows.Add(new AdventureLobbyChallengeMapRowAdapter(this, _visibleScratch[i], _localization));
            }

            _visibleScratch.Clear();
            _rows = rows;
            _rowsSignature = signature;
            return _rows;
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

            return SpokenText.Get(_localization, key, fallback);
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

    public sealed class AdventureLobbyChallengeMapRowAdapter : ILobbyMapRow
    {
        private static readonly AccessTools.FieldRef<LobbyChallengeMapEntry, GameObject> PlayedContainerRef =
            AccessTools.FieldRefAccess<LobbyChallengeMapEntry, GameObject>("_playedContainer");
        private static readonly AccessTools.FieldRef<LobbyChallengeMapEntry, UIImage[]> WinConditionIconsRef =
            AccessTools.FieldRefAccess<LobbyChallengeMapEntry, UIImage[]>("_winconditionIcons");

        private readonly AdventureLobbyChallengeMapSelectAdapter _owner;
        private readonly LobbyChallengeMapEntry _entry;
        private readonly ILocalizationHandler _localization;

        // Everything the row reads off the map's own metadata and off the game's tooltip data is fixed
        // for the life of the row, and a tooltip's existence can only be answered by capturing it, so
        // each is read once and kept. The live parts - the selection, the preview text - are not here.
        private string _name;
        private string _nativeKey;
        private string _completedLabel;
        private string _notCompletedLabel;
        private IReadOnlyList<string> _winConditionLabels;
        private IReadOnlyList<Tooltip> _winConditionTooltips;
        private Tooltip _winConditionTooltip;
        private bool _winConditionTooltipProbed;

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
            get { return _nativeKey ?? (_nativeKey = ResolveNativeKey()); }
        }

        private string ResolveNativeKey()
        {
            string path = _entry != null && _entry.MapMetadata != null ? _entry.MapMetadata.PathName : null;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = _entry != null ? _entry.MapData.path : null;
            }

            return string.IsNullOrWhiteSpace(path) ? Name : path;
        }

        public string Name
        {
            get { return _name ?? (_name = SpokenLines.Clean(_entry != null ? _entry.LocalizedMapName : string.Empty)); }
        }

        public IReadOnlyList<string> WinConditionLabels
        {
            get { return _winConditionLabels ?? (_winConditionLabels = GetWinConditionLabels()); }
        }

        /// <summary>One tooltip per drawn win-condition icon, in the order of
        /// <see cref="WinConditionLabels"/>; a null entry is an icon the game drew without one.
        /// </summary>
        public IReadOnlyList<Tooltip> WinConditionTooltips
        {
            get { return _winConditionTooltips ?? (_winConditionTooltips = BuildWinConditionTooltips()); }
        }

        private IReadOnlyList<Tooltip> BuildWinConditionTooltips()
        {
            return LobbyMapRow.WinConditionTooltips(
                WinConditionLabels,
                _entry != null ? WinConditionIconsRef(_entry) : null,
                _localization);
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

            if (!_winConditionTooltipProbed)
            {
                _winConditionTooltip = GetWinConditionTooltip();
                _winConditionTooltipProbed = true;
            }

            return _winConditionTooltip;
        }

        private IReadOnlyList<string> GetWinConditionLabels()
        {
            return LobbyMapRow.WinConditionLabels(_entry != null ? _entry.MapMetadata : null, _localization);
        }

        private Tooltip GetWinConditionTooltip()
        {
            return LobbyMapRow.WinConditionTooltip(
                _entry != null ? WinConditionIconsRef(_entry) : null,
                _localization);
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return LobbyMapRow.LocalizedText(_localization, key, fallback);
        }
    }
}
