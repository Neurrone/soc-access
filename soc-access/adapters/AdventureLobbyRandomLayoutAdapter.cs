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
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureLobbyRandomLayoutAdapter
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

        private readonly LobbyRandomMapSelectionMenu _menu;
        private readonly LobbyNavigation _navigation;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyRandomLayoutAdapter(LobbyRandomMapSelectionMenu menu)
        {
            _menu = menu;
            _navigation = FindNavigationFor(menu);
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

        public string Title
        {
            get { return GetVisibleTitleText(); }
        }

        public RandomLayoutItem SelectedLayout
        {
            get
            {
                LobbyRandomMapPreviewEntry selected = _menu != null ? SelectedEntryRef(_menu) : null;
                return selected != null ? new RandomLayoutItem(this, selected, _localization) : null;
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
                    items.Add(new RandomLayoutItem(this, entry, _localization));
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
            return entries ?? new List<LobbyRandomMapPreviewEntry>();
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

        private string GetVisibleTitleText()
        {
            if (_menu == null)
            {
                return string.Empty;
            }

            UITextMesh[] textMeshes = ((Component)_menu).GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null || IsInsideRandomLayoutEntry(textMesh.transform) || IsInsideButton(textMesh.transform))
                {
                    continue;
                }

                string text = GetText(textMesh);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static bool IsInsideRandomLayoutEntry(Transform transform)
        {
            while (transform != null)
            {
                if (transform.GetComponent<LobbyRandomMapPreviewEntry>() != null)
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static bool IsInsideButton(Transform transform)
        {
            while (transform != null)
            {
                if (transform.GetComponent<UIButton>() != null || transform.GetComponent<UIBackButton>() != null)
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
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

        private static LobbyNavigation FindNavigationFor(LobbyRandomMapSelectionMenu menu)
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
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
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

        private static IReadOnlyList<string> GetDropdownOptions(IUITextMeshDropdown dropdown)
        {
            Component component = dropdown as Component;
            TMP_Dropdown tmpDropdown = component != null ? component.GetComponentInChildren<TMP_Dropdown>(true) : null;
            if (tmpDropdown == null || tmpDropdown.options == null)
            {
                return new string[0];
            }

            List<string> options = new List<string>();
            for (int i = 0; i < tmpDropdown.options.Count; i++)
            {
                options.Add(SpeechTextSanitizer.Normalize(tmpDropdown.options[i].text));
            }

            return options;
        }

        internal sealed class RandomLayoutItem
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

            public string Description
            {
                get { return GetText(GetEntryField<UITextMesh>(Entry, EntryDescriptionField)); }
            }

            public bool IsSelected
            {
                get { return _owner != null && ReferenceEquals(SelectedEntryRef(_owner._menu), Entry); }
            }

            public void FocusNative()
            {
                _owner?.SelectLayout(Entry);
            }

            public bool Activate()
            {
                return _owner != null && _owner.ActivateLayout(Entry);
            }

            public IReadOnlyList<WinConditionToggleItem> GetWinConditionToggles()
            {
                return new[]
                {
                    new WinConditionToggleItem(GetEntryField<UIToggle>(Entry, EntryKingToggleField), AdventureWinCondition.LastTeamStanding, _localization),
                    new WinConditionToggleItem(GetEntryField<UIToggle>(Entry, EntryBeaconToggleField), AdventureWinCondition.Beacons, _localization),
                    new WinConditionToggleItem(GetEntryField<UIToggle>(Entry, EntryArtifactToggleField), AdventureWinCondition.FindTheEntity, _localization)
                };
            }

            public LayoutDropdownItem GetLayoutDropdown()
            {
                return new LayoutDropdownItem(GetEntryField<UITextMeshDropdown>(Entry, EntryLayoutDropdownField), _localization);
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

        internal sealed class WinConditionToggleItem
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

            public string Id
            {
                get { return Condition.ToString().ToLowerInvariant(); }
            }

            public string Label
            {
                get
                {
                    string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(_toggle != null ? _toggle.GetTextMesh() : null));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }

                    return SpeechTextSanitizer.Normalize(GameText.Get(_localization, "GameModes/" + Condition + "/Name", Condition.ToString()));
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

        internal sealed class LayoutDropdownItem
        {
            private readonly UITextMeshDropdown _dropdown;
            private readonly ILocalizationHandler _localization;

            public LayoutDropdownItem(UITextMeshDropdown dropdown, ILocalizationHandler localization)
            {
                _dropdown = dropdown;
                _localization = localization;
            }

            public bool IsVisible
            {
                get { return _dropdown != null && ((Component)_dropdown).gameObject.activeInHierarchy; }
            }

            public bool IsEnabled
            {
                get { return _dropdown != null && _dropdown.Active && _dropdown.Interactable; }
            }

            public int Value
            {
                get
                {
                    if (_dropdown == null || _dropdown.DropdownValueCount <= 0)
                    {
                        return 0;
                    }

                    int value = _dropdown.DropdownValue;
                    if (value < 0)
                    {
                        return 0;
                    }

                    return value >= _dropdown.DropdownValueCount ? _dropdown.DropdownValueCount - 1 : value;
                }
            }

            public IReadOnlyList<string> GetOptions()
            {
                return GetDropdownOptions(_dropdown);
            }

            public bool SetValue(int value)
            {
                if (_dropdown == null || !_dropdown.Active || !_dropdown.Interactable || _dropdown.DropdownValueCount <= 0)
                {
                    return false;
                }

                if (value < 0)
                {
                    value = 0;
                }
                else if (value >= _dropdown.DropdownValueCount)
                {
                    value = _dropdown.DropdownValueCount - 1;
                }

                _dropdown.DropdownValue = value;
                return true;
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
        }
    }
}
