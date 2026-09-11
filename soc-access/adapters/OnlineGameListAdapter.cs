using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Online;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class OnlineGameListAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<GameListMenu, GameListMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<GameListMenu, GameListMenu.Settings>("_settings");
        private static readonly AccessTools.FieldRef<GameListMenu, ILocalizationHandler> LocalizationRef =
            AccessTools.FieldRefAccess<GameListMenu, ILocalizationHandler>("_localizationHandler");
        private static readonly AccessTools.FieldRef<GameListMenu, MainMenuManagerContainer> MainMenuContainerRef =
            AccessTools.FieldRefAccess<GameListMenu, MainMenuManagerContainer>("_mainMenuContainer");
        private static readonly AccessTools.FieldRef<GameListMenu, List<GameListEntry>> ActiveEntriesRef =
            AccessTools.FieldRefAccess<GameListMenu, List<GameListEntry>>("_activeEntries");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");
        private static readonly MethodInfo DropdownGetTextMethod =
            AccessTools.Method(typeof(UITextMeshDropdown), "GetText");

        private readonly GameListMenu _menu;
        private readonly GameListMenu.Settings _settings;
        private readonly ILocalizationHandler _localization;

        // The region dropdown and the TMP_Dropdown under it are the same two objects for the life of
        // the page; the walk that finds the second is probed once, the miss kept with the hit.
        private RegionDropList _region;
        private TMP_Dropdown _nativeRegionDropdown;
        private bool _nativeRegionDropdownProbed;

        public OnlineGameListAdapter(GameListMenu menu)
        {
            _menu = menu;
            GameListMenu.Settings settings = menu != null ? SettingsRef(menu) : null;
            _settings = settings;
            _localization = (menu != null ? LocalizationRef(menu) : null) ?? GlobalLocalizationVariables.LocalizationHandler;

            HostGameButton = CreateButton(settings != null ? settings.HostGameButton : null);
            HostSavedGameButton = CreateButton(settings != null ? settings.HostSavedGameButton : null);
            JoinWithCodeButton = CreateButton(settings != null ? settings.JoinWithNameButton : null);
            JoinSelectedButton = CreateButton(settings != null ? settings.JoinSelectedButton : null);
            BackButton = CreateBackButton();
            OptionsButton = CreateOptionsButton();
        }

        public IMenuButtonAdapter HostGameButton { get; private set; }

        public IMenuButtonAdapter HostSavedGameButton { get; private set; }

        public IMenuButtonAdapter JoinWithCodeButton { get; private set; }

        public IMenuButtonAdapter JoinSelectedButton { get; private set; }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public bool IsPresent()
        {
            return _settings != null
                && IsLoadedMainMenuScene(MainMenuSceneType.OnlineGameList)
                && _settings.EntryParent != null
                && IsLiveSceneObject(((Component)_settings.EntryParent).gameObject)
                && ((Component)_settings.EntryParent).gameObject.activeInHierarchy;
        }

        public string Title
        {
            get
            {
                MainMenuManager.Settings settings = GetMainMenuSettings();
                string title = GetText(settings != null ? settings.TitleText : null);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = GameText.Get(_localization, "Lobby/GameList/Title", "Game List");
                }

                return title;
            }
        }

        public string GamesLabel
        {
            get { return ModText.Get(ModStrings.Common.ListSeparator, Title, ModText.Get(ModStrings.UI.RoleGrid)); }
        }

        public string RegionLabel
        {
            get { return GetDropdownLabel(_settings != null ? _settings.RegionDropdown : null); }
        }

        public string StatusText
        {
            get { return GetText(_settings != null ? _settings.BufferText : null); }
        }

        public string SelectedEntryText
        {
            get { return GetText(_settings != null ? _settings.SelectedEntryText : null); }
        }

        public bool IsStatusVisible
        {
            get
            {
                return _settings != null
                    && _settings.BufferVisuals != null
                    && _settings.BufferVisuals.activeInHierarchy
                    && !string.IsNullOrWhiteSpace(StatusText);
            }
        }

        public bool IsSelectedEntryTextVisible
        {
            get { return !string.IsNullOrWhiteSpace(SelectedEntryText); }
        }

        /// <summary>The region dropdown as the mod's drop list screen needs it.</summary>
        public RegionDropList Region
        {
            get { return _region ?? (_region = new RegionDropList(this, _settings != null ? _settings.RegionDropdown : null)); }
        }

        public IReadOnlyList<DropdownOption> GetRegionOptions()
        {
            List<DropdownOption> options = new List<DropdownOption>();
            UITextMeshDropdown dropdown = _settings != null ? _settings.RegionDropdown : null;
            TMP_Dropdown nativeDropdown = GetNativeRegionDropdown(dropdown);
            if (dropdown == null || nativeDropdown == null || nativeDropdown.options == null)
            {
                return options;
            }

            int current = GetRegionValue();
            for (int i = 0; i < nativeDropdown.options.Count; i++)
            {
                string text = nativeDropdown.options[i] != null ? nativeDropdown.options[i].text : string.Empty;
                options.Add(new DropdownOption(this, i, text, i == current));
            }

            return options;
        }

        public int GetRegionValue()
        {
            return MenuRows.DropdownValue(_settings != null ? _settings.RegionDropdown : null);
        }

        public bool SetRegionValue(int value)
        {
            return MenuRows.SetDropdownValue(_settings != null ? _settings.RegionDropdown : null, value);
        }

        public void FocusRegion()
        {
            UITextMeshDropdown dropdown = _settings != null ? _settings.RegionDropdown : null;
            if (dropdown != null)
            {
                NativeSelectionUtility.Select(dropdown.GetSelectable());
            }
        }

        /// <summary>The games the list is drawing, read from the menu's own <c>_activeEntries</c> every
        /// call. Nothing is cached here, so nothing has to be dropped: the detector's
        /// <c>OnOnlineGameListChanged</c> pulse, which fired on every list update, selection and region
        /// change, existed only to replace an adapter built over the installer's settings with one
        /// built over the menu, and the source hands the menu itself now.</summary>
        public IReadOnlyList<GameRow> GetRows()
        {
            List<GameListEntry> entries = GetVisibleEntries();
            List<GameRow> rows = new List<GameRow>();
            for (int i = 0; i < entries.Count; i++)
            {
                rows.Add(new GameRow(this, entries[i], i));
            }

            return rows;
        }

        public Tooltip GetButtonTooltip(IMenuButtonAdapter button)
        {
            return button != null ? Tooltip.ForComponent(button.Button as Component, _localization) : null;
        }

        private List<GameListEntry> GetVisibleEntries()
        {
            List<GameListEntry> result = new List<GameListEntry>();
            List<GameListEntry> activeEntries = _menu != null ? ActiveEntriesRef(_menu) : null;
            for (int i = 0; activeEntries != null && i < activeEntries.Count; i++)
            {
                AddIfVisible(result, activeEntries[i]);
            }

            result.Sort(CompareVisualOrder);
            return result;
        }

        private static void AddIfVisible(List<GameListEntry> result, GameListEntry entry)
        {
            if (result == null || entry == null)
            {
                return;
            }

            GameObject gameObject = ((Component)entry).gameObject;
            if (gameObject != null && gameObject.activeInHierarchy && IsLiveSceneObject(gameObject))
            {
                result.Add(entry);
            }
        }

        private static int CompareVisualOrder(GameListEntry left, GameListEntry right)
        {
            int leftIndex = left != null ? ((Component)left).transform.GetSiblingIndex() : int.MaxValue;
            int rightIndex = right != null ? ((Component)right).transform.GetSiblingIndex() : int.MaxValue;
            return leftIndex.CompareTo(rightIndex);
        }

        private IMenuButtonAdapter CreateBackButton()
        {
            MainMenuManager.Settings settings = GetMainMenuSettings();
            UIBackButton button = settings != null ? settings.BackButton : null;
            return button != null
                ? new StandardMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
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

        private static IMenuButtonAdapter CreateButton(UIButton button)
        {
            return button != null
                ? new StandardMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = _menu != null ? MainMenuContainerRef(_menu) : null;
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private TMP_Dropdown GetNativeRegionDropdown(UITextMeshDropdown dropdown)
        {
            if (_nativeRegionDropdownProbed)
            {
                return _nativeRegionDropdown;
            }

            _nativeRegionDropdownProbed = true;
            _nativeRegionDropdown = GetNativeDropdown(dropdown);
            return _nativeRegionDropdown;
        }

        // Once per adapter: the caller keeps the answer in _nativeRegionDropdown, and the region
        // dropdown is instantiated with the list and never replaced under it.
        private static TMP_Dropdown GetNativeDropdown(UITextMeshDropdown dropdown)
        {
            Component component = dropdown as Component;
            return component != null ? component.GetComponentInChildren<TMP_Dropdown>(true) : null;
        }

        private static string GetDropdownLabel(UITextMeshDropdown dropdown)
        {
            if (dropdown == null)
            {
                return string.Empty;
            }

            if (DropdownGetTextMethod != null)
            {
                IUITextMesh textMesh = DropdownGetTextMethod.Invoke(dropdown, new object[0]) as IUITextMesh;
                string text = UITextMeshTextUtility.GetEffectiveText(textMesh);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return dropdown.Text ?? string.Empty;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }

        private static bool IsLoadedMainMenuScene(MainMenuSceneType sceneType)
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == sceneType;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        /// <summary>The page's one dropdown, answering the questions every drop list answers so the
        /// mod's own list screen can be opened over the game's popup.</summary>
        public sealed class RegionDropList : IDropList
        {
            private readonly OnlineGameListAdapter _adapter;
            private readonly UITextMeshDropdown _dropdown;

            public RegionDropList(OnlineGameListAdapter adapter, UITextMeshDropdown dropdown)
            {
                _adapter = adapter;
                _dropdown = dropdown;
                GetOptions = ReadOptions;
                GetValue = () => _adapter != null ? _adapter.GetRegionValue() : 0;
                IsEnabled = () => _dropdown != null && _dropdown.Active && _dropdown.Interactable;
                IsVisible = () => _dropdown != null && ((Component)_dropdown).gameObject.activeInHierarchy;
                OpenPopup = () => DropdownPopup.Show(_dropdown);
                ClosePopup = () => DropdownPopup.Hide(_dropdown);
                IsPopupOpen = () => DropdownPopup.IsOpen(_dropdown);
                FocusOption = index => DropdownPopup.FocusOption(_dropdown, index);
            }

            public string Id
            {
                get { return "online-game-list-region"; }
            }

            /// <summary>The drawn dropdown itself, so a caller can key a control on it.</summary>
            public Component Subject
            {
                get { return _dropdown; }
            }

            public Func<IReadOnlyList<string>> GetOptions { get; private set; }
            public Func<int> GetValue { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<bool> OpenPopup { get; private set; }
            public Func<bool> ClosePopup { get; private set; }
            public Func<bool> IsPopupOpen { get; private set; }
            public Func<int, bool> FocusOption { get; private set; }

            public bool SetValue(int value)
            {
                return _adapter != null && _adapter.SetRegionValue(value);
            }

            public void Focus()
            {
                if (_adapter != null)
                {
                    _adapter.FocusRegion();
                }
            }

            private IReadOnlyList<string> ReadOptions()
            {
                List<string> labels = new List<string>();
                IReadOnlyList<DropdownOption> options = _adapter != null
                    ? _adapter.GetRegionOptions()
                    : new DropdownOption[0];
                for (int i = 0; i < options.Count; i++)
                {
                    labels.Add(options[i].Label);
                }

                return labels;
            }
        }

        public sealed class DropdownOption
        {
            private readonly OnlineGameListAdapter _adapter;

            public DropdownOption(OnlineGameListAdapter adapter, int index, string label, bool isSelected)
            {
                _adapter = adapter;
                Index = index;
                Label = label ?? string.Empty;
                IsSelected = isSelected;
            }

            public int Index { get; private set; }

            public string Label { get; private set; }

            public bool IsSelected { get; private set; }

            public bool Activate()
            {
                return _adapter != null && _adapter.SetRegionValue(Index);
            }
        }

        public sealed class GameRow
        {
            private static readonly AccessTools.FieldRef<GameListEntry, UITextMesh> NameTextRef =
                AccessTools.FieldRefAccess<GameListEntry, UITextMesh>("_nameText");
            private static readonly AccessTools.FieldRef<GameListEntry, UITextMesh> PlayerInfoTextRef =
                AccessTools.FieldRefAccess<GameListEntry, UITextMesh>("_playerInfoText");
            private static readonly AccessTools.FieldRef<GameListEntry, UIButton> ButtonRef =
                AccessTools.FieldRefAccess<GameListEntry, UIButton>("_button");
            private static readonly AccessTools.FieldRef<GameListEntry, UIImage> LockedGameIconRef =
                AccessTools.FieldRefAccess<GameListEntry, UIImage>("_lockedGameIcon");
            private static readonly AccessTools.FieldRef<GameListEntry, UIImage> PlayableGameIconRef =
                AccessTools.FieldRefAccess<GameListEntry, UIImage>("_playableGameIcon");
            private static readonly AccessTools.FieldRef<GameListEntry, UIImage> VersionMismatchIconRef =
                AccessTools.FieldRefAccess<GameListEntry, UIImage>("_versionMismatchIcon");
            private static readonly AccessTools.FieldRef<GameListEntry, string> GameIdRef =
                AccessTools.FieldRefAccess<GameListEntry, string>("_gameId");

            private readonly OnlineGameListAdapter _adapter;
            private readonly GameListEntry _entry;
            private readonly int _index;

            public GameRow(OnlineGameListAdapter adapter, GameListEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                _index = index;
            }

            public object NativeKey
            {
                get { return _entry; }
            }

            public string Id
            {
                get
                {
                    string gameId = _entry != null ? GameIdRef(_entry) : string.Empty;
                    return "online-game-row-" + _index + (string.IsNullOrWhiteSpace(gameId) ? string.Empty : "-" + SanitizeId(gameId));
                }
            }

            public string Label
            {
                get { return Name; }
            }

            /// <summary>The row the game draws this game as.</summary>
            public Component Entry
            {
                get { return _entry; }
            }

            public string Name
            {
                get { return GetText(NameTextRef(_entry)); }
            }

            public string Players
            {
                get { return GetText(PlayerInfoTextRef(_entry)); }
            }

            public string Status
            {
                get
                {
                    if (_entry == null)
                    {
                        return string.Empty;
                    }

                    if (_entry.Open && _entry.MatchingVersions)
                    {
                        return _adapter != null && _adapter.JoinSelectedButton != null
                            ? _adapter.JoinSelectedButton.GetLabel()
                            : string.Empty;
                    }

                    string tooltip = TooltipLines.First(GetStatusTooltip());
                    return !string.IsNullOrWhiteSpace(tooltip)
                        ? tooltip
                        : ModText.Get(ModStrings.UI.StatusUnavailable);
                }
            }

            public void FocusNative()
            {
                UIButton button = GetButton();
                if (button == null)
                {
                    return;
                }

                NativeSelectionUtility.Select(button.GetSelectable());
                NativeSelectionUtility.Click(button);
                AutoScrollToSelected autoScroller = _adapter != null && _adapter._settings != null
                    ? _adapter._settings.AutoScroller
                    : null;
                if (autoScroller != null && ((Behaviour)autoScroller).isActiveAndEnabled)
                {
                    autoScroller.ForceFocusOn(button.GetSelectable());
                }
            }

            public bool Activate()
            {
                UIButton button = GetButton();
                return button != null && NativeSelectionUtility.Click(button);
            }

            public Tooltip GetCellTooltip(string columnId)
            {
                if (columnId == "status")
                {
                    return GetStatusTooltip();
                }

                return null;
            }

            private UIButton GetButton()
            {
                return _entry != null ? ButtonRef(_entry) : null;
            }

            private Tooltip GetStatusTooltip()
            {
                Component component = null;
                UIImage versionMismatch = _entry != null ? VersionMismatchIconRef(_entry) : null;
                UIImage locked = _entry != null ? LockedGameIconRef(_entry) : null;
                UIImage playable = _entry != null ? PlayableGameIconRef(_entry) : null;

                if (versionMismatch != null && versionMismatch.Active)
                {
                    component = versionMismatch;
                }
                else if (locked != null && locked.Active)
                {
                    component = locked;
                }
                else if (playable != null && playable.Active)
                {
                    component = playable;
                }

                return component != null && _adapter != null
                    ? Tooltip.ForComponent(component, _adapter._localization)
                    : null;
            }

            private static string SanitizeId(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                char[] chars = value.ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    char c = chars[i];
                    if (!char.IsLetterOrDigit(c))
                    {
                        chars[i] = '-';
                    }
                }

                return new string(chars).Trim('-').ToLowerInvariant();
            }
        }
    }
}
