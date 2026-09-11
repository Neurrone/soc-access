using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
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
    public sealed class AdventureLobbyRandomLayoutAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<LobbyRandomMapSelectionMenu, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<LobbyRandomMapSelectionMenu, CanvasGroup>("_canvasGroup");
        private static readonly AccessTools.FieldRef<LobbyRandomMapSelectionMenu, UIButton> ConfirmButtonRef =
            AccessTools.FieldRefAccess<LobbyRandomMapSelectionMenu, UIButton>("_confirmButton");
        private static readonly AccessTools.FieldRef<LobbyRandomMapSelectionMenu, List<LobbyRandomMapPreviewEntry>> EntriesRef =
            AccessTools.FieldRefAccess<LobbyRandomMapSelectionMenu, List<LobbyRandomMapPreviewEntry>>("_entries");
        private static readonly AccessTools.FieldRef<LobbyRandomMapSelectionMenu, LobbyRandomMapPreviewEntry> SelectedEntryRef =
            AccessTools.FieldRefAccess<LobbyRandomMapSelectionMenu, LobbyRandomMapPreviewEntry>("_selectedEntry");
        private static readonly AccessTools.FieldRef<LobbyRandomMapSelectionMenu, ILocalizationHandler> LocalizationRef =
            AccessTools.FieldRefAccess<LobbyRandomMapSelectionMenu, ILocalizationHandler>("_localizationHandler");
        private static readonly AccessTools.FieldRef<LobbyNavigation, UIBackButton> CommonBackButtonRef =
            AccessTools.FieldRefAccess<LobbyNavigation, UIBackButton>("_commonBackButton");
        private static readonly AccessTools.FieldRef<LobbyNavigation, MainMenuManagerContainer> NavigationManagerContainerRef =
            AccessTools.FieldRefAccess<LobbyNavigation, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");
        private static readonly FieldInfo EntryTitleField =
            AccessTools.Field(typeof(LobbyRandomMapPreviewEntry), "_title");
        private static readonly FieldInfo EntryDescriptionField =
            AccessTools.Field(typeof(LobbyRandomMapPreviewEntry), "_description");
        private static readonly FieldInfo EntryButtonField =
            AccessTools.Field(typeof(LobbyRandomMapPreviewEntry), "_button");
        private static readonly FieldInfo EntryLayoutDropdownField =
            AccessTools.Field(typeof(LobbyRandomMapPreviewEntry), "_layoutDropdown");
        private static readonly FieldInfo EntryKingToggleField =
            AccessTools.Field(typeof(LobbyRandomMapPreviewEntry), "_kingToggle");
        private static readonly FieldInfo EntryBeaconToggleField =
            AccessTools.Field(typeof(LobbyRandomMapPreviewEntry), "_beaconToggle");
        private static readonly FieldInfo EntryArtifactToggleField =
            AccessTools.Field(typeof(LobbyRandomMapPreviewEntry), "_artifactToggle");
        private static readonly MethodInfo SetSelectedEntryMethod =
            AccessTools.Method(typeof(LobbyRandomMapSelectionMenu), "SetSelectedEntry");
        private static readonly MethodInfo DropdownGetTextMethod =
            AccessTools.Method(typeof(UITextMeshDropdown), "GetText");

        private static readonly LobbyRandomMapPreviewEntry[] NoEntries = new LobbyRandomMapPreviewEntry[0];

        private readonly LobbyRandomMapSelectionMenu _menu;
        private readonly LobbyNavigation _navigation;
        private readonly ILocalizationHandler _localization;

        // One reader per drawn card, kept while the menu's list of cards is the one they were made
        // for. The card's own facts - its title, its description, which card is chosen - are read
        // off the entry every time they are asked for; what is kept is the reflection behind them,
        // which a Build would otherwise pay four times a frame for the cards and again for the
        // selected card's three toggles and its dropdown. The list is compared by its count and the
        // identity of its first and last entry, all read from the game each frame, so a page that
        // redraws its cards gets new readers.
        private readonly Dictionary<LobbyRandomMapPreviewEntry, RandomLayoutItem> _items =
            new Dictionary<LobbyRandomMapPreviewEntry, RandomLayoutItem>();
        private int _entryCount = -1;
        private LobbyRandomMapPreviewEntry _firstEntry;
        private LobbyRandomMapPreviewEntry _lastEntry;

        public AdventureLobbyRandomLayoutAdapter(LobbyRandomMapSelectionMenu menu, LobbyNavigation navigation)
        {
            _menu = menu;
            _navigation = navigation;
            _localization = menu != null ? LocalizationRef(menu) : GlobalLocalizationVariables.LocalizationHandler;

            ConfirmButton = CreateConfirmButton();
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
                && SelectedEntryRef(_menu) != null
                && GetEntries().Count > 0;
        }

        /// <summary>The page's drawn title. <c>LobbyNavigation.ShowSubmenu</c> (decompiled, line 350)
        /// sets it from <c>Lobby/RandomMapPopup/Header</c> ("Select layout") on the SHARED main-menu
        /// header, not on the menu's own canvas, which is why searching the menu's children for it
        /// answered nothing.</summary>
        public string Title
        {
            get { return GameText.Get(_localization, "Lobby/RandomMapPopup/Header", string.Empty); }
        }

        public RandomLayoutItem SelectedLayout
        {
            get
            {
                LobbyRandomMapPreviewEntry selected = _menu != null ? SelectedEntryRef(_menu) : null;
                return selected != null ? ItemFor(selected) : null;
            }
        }

        public IReadOnlyList<RandomLayoutItem> GetLayouts()
        {
            List<RandomLayoutItem> items = new List<RandomLayoutItem>();
            IReadOnlyList<LobbyRandomMapPreviewEntry> entries = GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                LobbyRandomMapPreviewEntry entry = entries[i];
                if (entry != null && IsVisible((Component)entry))
                {
                    items.Add(ItemFor(entry));
                }
            }

            items.Sort(CompareVisualOrder);
            return items;
        }

        public void SelectLayout(LobbyRandomMapPreviewEntry entry)
        {
            if (_menu == null || entry == null)
            {
                return;
            }

            if (!ReferenceEquals(SelectedEntryRef(_menu), entry) && SetSelectedEntryMethod != null)
            {
                SetSelectedEntryMethod.Invoke(_menu, new object[] { entry });
            }

            NativeSelectionUtility.Select(entry.GetSelectable());
        }

        public bool ActivateLayout(LobbyRandomMapPreviewEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            SelectLayout(entry);
            return true;
        }

        private IReadOnlyList<LobbyRandomMapPreviewEntry> GetEntries()
        {
            List<LobbyRandomMapPreviewEntry> entries = _menu != null ? EntriesRef(_menu) : null;
            if (entries == null)
            {
                return NoEntries;
            }

            int count = entries.Count;
            LobbyRandomMapPreviewEntry first = count > 0 ? entries[0] : null;
            LobbyRandomMapPreviewEntry last = count > 0 ? entries[count - 1] : null;
            if (count != _entryCount || !ReferenceEquals(first, _firstEntry) || !ReferenceEquals(last, _lastEntry))
            {
                _items.Clear();
                _entryCount = count;
                _firstEntry = first;
                _lastEntry = last;
            }

            return entries;
        }

        private RandomLayoutItem ItemFor(LobbyRandomMapPreviewEntry entry)
        {
            RandomLayoutItem item;
            if (!_items.TryGetValue(entry, out item))
            {
                item = new RandomLayoutItem(this, entry, _localization);
                _items.Add(entry, item);
            }

            return item;
        }

        private IMenuButtonAdapter CreateConfirmButton()
        {
            UIButton button = _menu != null ? ConfirmButtonRef(_menu) : null;
            return button != null
                ? new StandardMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
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

        private static int CompareVisualOrder(RandomLayoutItem left, RandomLayoutItem right)
        {
            int result = GetSiblingIndex(left).CompareTo(GetSiblingIndex(right));
            if (result != 0)
            {
                return result;
            }

            return string.CompareOrdinal(left != null ? left.Title : string.Empty, right != null ? right.Title : string.Empty);
        }

        private static int GetSiblingIndex(RandomLayoutItem item)
        {
            return item != null && item.Entry != null ? ((Component)item.Entry).transform.GetSiblingIndex() : int.MaxValue;
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

        private static bool IsVisible(Component component)
        {
            return component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        // A text mesh the game may have written more than one paragraph into.
        private static IList<string> GetLines(IUITextMesh textMesh)
        {
            return SpokenLines.Of(new[] { UITextMeshTextUtility.GetEffectiveText(textMesh) });
        }

        private static T GetEntryField<T>(LobbyRandomMapPreviewEntry entry, FieldInfo field) where T : class
        {
            return entry != null && field != null ? field.GetValue(entry) as T : null;
        }

        private static Component GetDropdownTooltipComponent(IUITextMeshDropdown dropdown)
        {
            UITextMeshDropdown concrete = dropdown as UITextMeshDropdown;
            if (concrete == null || DropdownGetTextMethod == null)
            {
                return null;
            }

            return DropdownGetTextMethod.Invoke(concrete, new object[0]) as Component;
        }

        public sealed class RandomLayoutItem
        {
            private readonly AdventureLobbyRandomLayoutAdapter _owner;
            private readonly ILocalizationHandler _localization;

            public RandomLayoutItem(AdventureLobbyRandomLayoutAdapter owner, LobbyRandomMapPreviewEntry entry, ILocalizationHandler localization)
            {
                _owner = owner;
                Entry = entry;
                _localization = localization;
            }

            public LobbyRandomMapPreviewEntry Entry { get; private set; }

            public string Id
            {
                get
                {
                    string name = Entry != null && Entry.MapProviderData.name != null ? Entry.MapProviderData.name : Title;
                    return string.IsNullOrWhiteSpace(name) ? "layout" : SanitizeId(name);
                }
            }

            public string Title
            {
                get { return GetText(GetEntryField<UITextMesh>(Entry, EntryTitleField)); }
            }

            /// <summary>The paragraphs the card draws under its title, kept apart rather than
            /// collapsed.</summary>
            public IList<string> DescriptionLines
            {
                get { return GetLines(GetEntryField<UITextMesh>(Entry, EntryDescriptionField)); }
            }

            public bool IsSelected
            {
                get { return _owner != null && ReferenceEquals(SelectedEntryRef(_owner._menu), Entry); }
            }

            public bool Activate()
            {
                return _owner != null && _owner.ActivateLayout(Entry);
            }

            // The card's toggles and its dropdown are serialized fields of the entry: the game sets
            // them when it instantiates the card and never again, so they are resolved once per card
            // rather than once per Build.
            private IReadOnlyList<WinConditionToggleItem> _winConditions;
            private LayoutDropdownItem _layoutDropdown;

            public IReadOnlyList<WinConditionToggleItem> GetWinConditionToggles()
            {
                return _winConditions ?? (_winConditions = new[]
                {
                    new WinConditionToggleItem(GetEntryField<UIToggle>(Entry, EntryKingToggleField), AdventureWinCondition.LastTeamStanding, _localization),
                    new WinConditionToggleItem(GetEntryField<UIToggle>(Entry, EntryBeaconToggleField), AdventureWinCondition.Beacons, _localization),
                    new WinConditionToggleItem(GetEntryField<UIToggle>(Entry, EntryArtifactToggleField), AdventureWinCondition.FindTheEntity, _localization)
                });
            }

            public LayoutDropdownItem GetLayoutDropdown()
            {
                return _layoutDropdown ?? (_layoutDropdown = new LayoutDropdownItem(
                    GetEntryField<UITextMeshDropdown>(Entry, EntryLayoutDropdownField),
                    _localization));
            }

            private static string SanitizeId(string value)
            {
                char[] chars = value.ToLowerInvariant().ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    char c = chars[i];
                    if (!char.IsLetterOrDigit(c))
                    {
                        chars[i] = '-';
                    }
                }

                return new string(chars);
            }
        }

        public sealed class WinConditionToggleItem
        {
            private readonly UIToggle _toggle;
            private readonly ILocalizationHandler _localization;

            public WinConditionToggleItem(UIToggle toggle, AdventureWinCondition condition, ILocalizationHandler localization)
            {
                _toggle = toggle;
                Condition = condition;
                _localization = localization;
            }

            public AdventureWinCondition Condition { get; private set; }

            /// <summary>The drawn toggle itself, so a caller can key a control on the object the game
            /// destroys when the card goes away.</summary>
            public Component Subject
            {
                get { return _toggle; }
            }

            public string Id
            {
                get { return Condition.ToString().ToLowerInvariant(); }
            }

            public string Label
            {
                get
                {
                    string text = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(_toggle != null ? _toggle.GetTextMesh() : null));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }

                    return SpokenLines.Clean(GameText.Get(_localization, "GameModes/" + Condition + "/Name", Condition.ToString()));
                }
            }

            public bool IsVisible
            {
                get { return IsVisibleComponent(_toggle); }
            }

            public bool IsEnabled
            {
                get { return _toggle != null && _toggle.Interactable; }
            }

            public bool IsChecked
            {
                get { return _toggle != null && _toggle.ToggleValue; }
            }

            public void Toggle()
            {
                if (_toggle != null && _toggle.Interactable)
                {
                    _toggle.ToggleValue = !_toggle.ToggleValue;
                }
            }

            /// <summary>The game's own selection on the toggle, which is what its scroll rect
            /// follows.</summary>
            public void Focus()
            {
                if (_toggle != null)
                {
                    NativeSelectionUtility.Select(_toggle.GetSelectable());
                }
            }

            public Tooltip GetTooltip()
            {
                Component component = _toggle != null ? _toggle.GetTextMesh() as Component : null;
                return component != null ? Tooltip.ForComponent(component, _localization) : null;
            }

            private static bool IsVisibleComponent(Component component)
            {
                return component != null
                    && component.gameObject != null
                    && component.gameObject.activeInHierarchy;
            }
        }

        /// <summary>The layout dropdown the selected card draws ("Quad", "Corridor", "Random"), in
        /// the shape every drop list in the mod answers: the options, the one in force, and the
        /// game's own popup underneath.</summary>
        public sealed class LayoutDropdownItem : IDropList
        {
            private readonly UITextMeshDropdown _dropdown;
            private readonly ILocalizationHandler _localization;

            public LayoutDropdownItem(UITextMeshDropdown dropdown, ILocalizationHandler localization)
            {
                _dropdown = dropdown;
                _localization = localization;
                GetOptions = () => MenuRows.DropdownOptions(_dropdown);
                GetValue = ReadValue;
                IsEnabled = () => _dropdown != null && _dropdown.Active && _dropdown.Interactable;
                IsVisible = () => _dropdown != null && ((Component)_dropdown).gameObject.activeInHierarchy;
                OpenPopup = () => DropdownPopup.Show(_dropdown);
                ClosePopup = () => DropdownPopup.Hide(_dropdown);
                IsPopupOpen = () => DropdownPopup.IsOpen(_dropdown);
                FocusOption = index => DropdownPopup.FocusOption(_dropdown, index);
            }

            public string Id
            {
                get { return "random-layout-variant"; }
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
                return MenuRows.SetDropdownValue(_dropdown, value);
            }

            public void Focus()
            {
                if (_dropdown != null)
                {
                    NativeSelectionUtility.Select(_dropdown.GetSelectable());
                }
            }

            public Tooltip GetTooltip()
            {
                Component component = GetDropdownTooltipComponent(_dropdown) ?? _dropdown as Component;
                return component != null ? Tooltip.ForComponent(component, _localization) : null;
            }

            private int ReadValue()
            {
                return MenuRows.DropdownValue(_dropdown);
            }
        }
    }
}
