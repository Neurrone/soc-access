using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ModIO;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommunityMapsCollectionAdapter
    {
        private static readonly FieldInfo TitleField = AccessTools.Field(typeof(Collection), "CollectionPanelTitle");
        private static readonly FieldInfo SearchFieldInfo = AccessTools.Field(typeof(Collection), "CollectionPanelSearchField");
        private static readonly FieldInfo ListItemParentField = AccessTools.Field(typeof(Collection), "CollectionPanelModListItemParent");
        private static readonly FieldInfo CheckForUpdatesTextField = AccessTools.Field(typeof(Collection), "CollectionPanelCheckForUpdatesText");
        private static readonly FieldInfo CheckForUpdatesButtonField = AccessTools.Field(typeof(Collection), "CollectionPanelCheckForUpdatesButton");
        private static readonly FieldInfo FilterDropdownField = AccessTools.Field(typeof(Collection), "CollectionPanelFirstDropDownFilter");
        private static readonly FieldInfo SortDropdownField = AccessTools.Field(typeof(Collection), "CollectionPanelSecondDropDownFilter");
        private static readonly MethodInfo CheckForUpdatesMethod = AccessTools.Method(typeof(Collection), "CheckForUpdates");
        private static readonly MethodInfo RefreshListMethod = AccessTools.Method(typeof(Collection), "RefreshList");
        private static readonly Type InputNavigationType = AccessTools.TypeByName("ModIOBrowser.InputNavigation");
        private static readonly MethodInfo InputNavigationSelectMethod =
            InputNavigationType != null ? AccessTools.Method(InputNavigationType, "Select", new[] { typeof(Selectable), typeof(bool) }) : null;

        private readonly Collection _collection;
        private readonly string _browseLabel;
        private readonly string _collectionLabel;
        private readonly string _searchFilterLabel;
        private readonly string _downloadsLabel;
        private readonly string _moreOptionsLabel;
        private readonly string _unsubscribeLabel;
        private CollectionItem _selectedItem;

        private CommunityMapsCollectionAdapter(Collection collection)
        {
            _collection = collection;
            _browseLabel = Translate("Browse");
            _collectionLabel = Translate("Collection");
            _searchFilterLabel = FindTopBarText("Search & filter");
            _downloadsLabel = Translate("Downloads");
            _moreOptionsLabel = Translate("More options");
            _unsubscribeLabel = Translate("Unsubscribe");
        }

        public static CommunityMapsCollectionAdapter TryCreate()
        {
            Collection[] collections = Resources.FindObjectsOfTypeAll<Collection>();
            for (int i = 0; i < collections.Length; i++)
            {
                CommunityMapsCollectionAdapter adapter = new CommunityMapsCollectionAdapter(collections[i]);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public bool IsPresent()
        {
            return Browser.IsOpen
                && _collection != null
                && _collection.CollectionPanel != null
                && _collection.CollectionPanel.activeInHierarchy;
        }

        public string Title
        {
            get
            {
                string title = GetText(GetField<TMP_Text>(TitleField));
                return !string.IsNullOrWhiteSpace(title) ? title : _collectionLabel;
            }
        }

        public IReadOnlyList<TabItem> GetTabs()
        {
            return new[]
            {
                new TabItem("browse", _browseLabel, false, OpenBrowse),
                new TabItem("collection", _collectionLabel, true, OpenCollection)
            };
        }

        public string SearchFilterLabel
        {
            get { return _searchFilterLabel; }
        }

        public bool HasSearchFilter
        {
            get { return !string.IsNullOrWhiteSpace(_searchFilterLabel); }
        }

        public string DownloadsLabel
        {
            get { return _downloadsLabel; }
        }

        public bool HasDownloadsMenu
        {
            get { return Browser.IsOpen; }
        }

        public TMP_InputField SearchField
        {
            get { return GetField<TMP_InputField>(SearchFieldInfo); }
        }

        public string SearchFieldLabel
        {
            get
            {
                TMP_InputField field = SearchField;
                TMP_Text placeholder = field != null ? field.placeholder as TMP_Text : null;
                string label = GetText(placeholder);
                return !string.IsNullOrWhiteSpace(label) ? label : string.Empty;
            }
        }

        public ButtonAction CheckForUpdatesAction
        {
            get
            {
                Button button = GetField<Button>(CheckForUpdatesButtonField);
                return new ButtonAction(
                    "check-updates",
                    GetText(GetField<TMP_Text>(CheckForUpdatesTextField)),
                    FocusCheckForUpdates,
                    CheckForUpdates,
                    () => button != null && button.gameObject.activeInHierarchy && button.interactable,
                    () => button != null && button.gameObject.activeInHierarchy);
            }
        }

        public DropdownItem FilterDropdown
        {
            get { return BuildDropdown("filter", GetField<MultiTargetDropdown>(FilterDropdownField)); }
        }

        public DropdownItem SortDropdown
        {
            get { return BuildDropdown("sort", GetField<MultiTargetDropdown>(SortDropdownField)); }
        }

        public string ItemsLabel
        {
            get { return Title; }
        }

        public IReadOnlyList<CollectionItem> GetItems()
        {
            List<CollectionItem> result = new List<CollectionItem>();
            Transform parent = GetField<Transform>(ListItemParentField);
            if (parent == null)
            {
                return result;
            }

            ListItem[] nativeItems = parent.GetComponentsInChildren<ListItem>(false);
            for (int i = 0; i < nativeItems.Length; i++)
            {
                ListItem item = nativeItems[i];
                if (!IsCollectionListItem(item))
                {
                    continue;
                }

                CollectionItem collectionItem = BuildCollectionItem(result.Count, item);
                if (collectionItem != null)
                {
                    result.Add(collectionItem);
                }
            }

            return result;
        }

        public bool HasSelectedItem
        {
            get { return _selectedItem != null && _selectedItem.IsVisible; }
        }

        public string SelectedItemLabel
        {
            get { return _selectedItem != null ? _selectedItem.Label : string.Empty; }
        }

        public bool IsSelectedItemToggleVisible()
        {
            return HasSelectedItem && _selectedItem.Toggle != null && _selectedItem.Toggle.gameObject.activeInHierarchy;
        }

        public bool IsSelectedItemToggleEnabled()
        {
            return IsSelectedItemToggleVisible() && _selectedItem.Toggle.interactable;
        }

        public bool IsSelectedItemEnabled()
        {
            return IsSelectedItemToggleVisible() && _selectedItem.Toggle.isOn;
        }

        public bool ToggleSelectedItemEnabled()
        {
            if (!IsSelectedItemToggleEnabled())
            {
                return false;
            }

            FocusSelectedItemToggle();
            _selectedItem.Toggle.isOn = !_selectedItem.Toggle.isOn;
            return true;
        }

        public void FocusSelectedItemToggle()
        {
            if (IsSelectedItemToggleVisible())
            {
                SelectViaModIoNavigation(_selectedItem.Toggle);
                _selectedItem.Toggle.GetComponent<ViewportRestraint>()?.CheckSelectionVerticalVisibility();
            }
        }

        public string UnsubscribeLabel
        {
            get { return _unsubscribeLabel; }
        }

        public bool IsUnsubscribeVisible()
        {
            return HasSelectedItem
                && _selectedItem.UnsubscribeButton != null
                && _selectedItem.UnsubscribeButton.gameObject.activeInHierarchy;
        }

        public bool IsUnsubscribeEnabled()
        {
            return IsUnsubscribeVisible() && _selectedItem.UnsubscribeButton.interactable;
        }

        public bool UnsubscribeSelectedItem()
        {
            if (!IsUnsubscribeEnabled())
            {
                return false;
            }

            FocusSelectedItem();

            MethodInfo method = AccessTools.Method(_selectedItem.NativeItem.GetType(), "UnsubscribeButton");
            if (method == null)
            {
                return false;
            }

            method.Invoke(_selectedItem.NativeItem, null);
            return true;
        }

        public string MoreOptionsLabel
        {
            get { return _moreOptionsLabel; }
        }

        public bool IsMoreOptionsVisible()
        {
            return HasSelectedItem
                && _selectedItem.MoreOptionsButton != null
                && _selectedItem.MoreOptionsButton.gameObject.activeInHierarchy;
        }

        public bool IsMoreOptionsEnabled()
        {
            return IsMoreOptionsVisible() && _selectedItem.MoreOptionsButton.interactable;
        }

        public bool OpenSelectedItemOptions()
        {
            if (!IsMoreOptionsEnabled())
            {
                return false;
            }

            FocusSelectedItem();
            MethodInfo method = AccessTools.Method(_selectedItem.NativeItem.GetType(), "ShowMoreOptions");
            if (method == null)
            {
                return false;
            }

            method.Invoke(_selectedItem.NativeItem, null);
            return true;
        }

        public bool OpenSearchFilter()
        {
            if (!HasSearchFilter)
            {
                return false;
            }

            InputReceiver.OnSearch();
            return true;
        }

        public bool OpenDownloadsMenu()
        {
            if (!HasDownloadsMenu)
            {
                return false;
            }

            InputReceiver.OnMenu();
            return true;
        }

        public bool Close()
        {
            Browser.Close();
            return true;
        }

        private bool OpenBrowse()
        {
            Home[] homes = Resources.FindObjectsOfTypeAll<Home>();
            for (int i = 0; i < homes.Length; i++)
            {
                if (homes[i] != null && homes[i].BrowserPanel != null)
                {
                    homes[i].Open();
                    SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsChanged();
                    return true;
                }
            }

            return false;
        }

        private bool OpenCollection()
        {
            return true;
        }

        private bool FocusCheckForUpdates()
        {
            Button button = GetField<Button>(CheckForUpdatesButtonField);
            return SelectViaModIoNavigation(button);
        }

        private bool CheckForUpdates()
        {
            Button button = GetField<Button>(CheckForUpdatesButtonField);
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                button.onClick.Invoke();
                return true;
            }

            if (CheckForUpdatesMethod == null)
            {
                return false;
            }

            CheckForUpdatesMethod.Invoke(_collection, null);
            return true;
        }

        private DropdownItem BuildDropdown(string id, MultiTargetDropdown dropdown)
        {
            return new DropdownItem(
                id,
                FindDropdownLabel(dropdown),
                dropdown,
                SetDropdownValue,
                FocusDropdown);
        }

        private bool SetDropdownValue(MultiTargetDropdown dropdown, int value)
        {
            if (dropdown == null || value < 0 || value >= dropdown.options.Count)
            {
                return false;
            }

            FocusDropdown(dropdown);
            bool hasNativeChangeListener = dropdown.onValueChanged != null
                && dropdown.onValueChanged.GetPersistentEventCount() > 0;
            dropdown.value = value;
            dropdown.RefreshShownValue();
            if (!hasNativeChangeListener)
            {
                RefreshListMethod?.Invoke(_collection, null);
            }

            return true;
        }

        private bool FocusDropdown(MultiTargetDropdown dropdown)
        {
            return SelectViaModIoNavigation(dropdown);
        }

        private CollectionItem BuildCollectionItem(int index, ListItem item)
        {
            string title = GetText(GetField<TMP_Text>(item, "title"));
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            List<string> statusParts = new List<string>();
            AddIfNotEmpty(statusParts, GetText(GetField<TMP_Text>(item, "subscriptionStatus")));
            AddIfNotEmpty(statusParts, GetText(GetField<TMP_Text>(item, "installStatus")));
            AddIfNotEmpty(statusParts, GetText(GetField<TMP_Text>(item, "errorInstallingText")));
            AddProgress(statusParts, item);
            AddIfNotEmpty(statusParts, GetText(GetField<TMP_Text>(item, "fileSize")));
            AddIfNotEmpty(statusParts, GetText(GetField<TMP_Text>(item, "otherSubscribersText")));

            return new CollectionItem(
                index,
                title,
                string.Join(". ", statusParts.ToArray()),
                item,
                GetField<MultiTargetToggle>(item, "enabledOrDisabledToggle"),
                GetField<Button>(item, "unsubscribeButton"),
                GetField<Button>(item, "moreOptionsButton"));
        }

        private static void AddProgress(List<string> parts, ListItem item)
        {
            GameObject progressBar = GetField<GameObject>(item, "progressBar");
            if (progressBar == null || !progressBar.activeInHierarchy)
            {
                return;
            }

            string text = GetText(GetField<TMP_Text>(item, "progressBarText"));
            string percent = GetText(GetField<TMP_Text>(item, "progressBarPercentageText"));
            if (string.IsNullOrWhiteSpace(text))
            {
                AddIfNotEmpty(parts, percent);
            }
            else if (string.IsNullOrWhiteSpace(percent))
            {
                parts.Add(text);
            }
            else
            {
                parts.Add(text + " " + percent);
            }
        }

        public bool FocusItem(CollectionItem item)
        {
            if (item == null || item.NativeItem == null)
            {
                return false;
            }

            _selectedItem = item;
            SelectViaModIoNavigation(item.NativeItem.selectable);
            item.NativeItem.viewportRestraint?.CheckSelectionVerticalVisibility();
            return true;
        }

        public bool ActivateItem(CollectionItem item)
        {
            if (item == null || item.NativeItem == null)
            {
                return false;
            }

            FocusItem(item);
            MethodInfo method = AccessTools.Method(item.NativeItem.GetType(), "OpenModDetailsForThisProfile");
            if (method == null)
            {
                return false;
            }

            method.Invoke(item.NativeItem, null);
            return true;
        }

        private void FocusSelectedItem()
        {
            if (_selectedItem != null)
            {
                FocusItem(_selectedItem);
            }
        }

        private T GetField<T>(FieldInfo field)
        {
            return field != null && _collection != null ? (T)field.GetValue(_collection) : default(T);
        }

        private static T GetField<T>(object instance, string name)
        {
            if (instance == null)
            {
                return default(T);
            }

            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            return field != null ? (T)field.GetValue(instance) : default(T);
        }

        private static bool IsCollectionListItem(ListItem item)
        {
            return item != null
                && item.gameObject.activeInHierarchy
                && !item.isPlaceholder
                && item.GetType().FullName == "ModIOBrowser.Implementation.CollectionModListItem";
        }

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        private static string FindDropdownLabel(MultiTargetDropdown dropdown)
        {
            if (dropdown == null)
            {
                return string.Empty;
            }

            string caption = GetText(dropdown.captionText);
            TMP_Text[] texts = dropdown.GetComponentsInChildren<TMP_Text>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || text == dropdown.captionText || text == dropdown.itemText)
                {
                    continue;
                }

                string value = GetText(text);
                if (!string.IsNullOrWhiteSpace(value) && value != caption)
                {
                    return value;
                }
            }

            return caption;
        }

        private static string FindTopBarText(string transformName)
        {
            if (string.IsNullOrWhiteSpace(transformName))
            {
                return string.Empty;
            }

            NavBar[] navBars = Resources.FindObjectsOfTypeAll<NavBar>();
            for (int navIndex = 0; navIndex < navBars.Length; navIndex++)
            {
                NavBar navBar = navBars[navIndex];
                TMP_Text[] texts = navBar != null ? navBar.GetComponentsInChildren<TMP_Text>(false) : null;
                if (texts == null)
                {
                    continue;
                }

                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    if (text == null || text.transform.parent == null || text.transform.parent.name != transformName)
                    {
                        continue;
                    }

                    string value = GetText(text);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return string.Empty;
        }

        private static string Translate(string key)
        {
            TranslationManager[] managers = Resources.FindObjectsOfTypeAll<TranslationManager>();
            if (managers.Length == 0 || string.IsNullOrWhiteSpace(key))
            {
                return key ?? string.Empty;
            }

            return StripTmpMarkup(managers[0].Get(key));
        }

        private static string GetText(TMP_Text text)
        {
            return text != null && text.gameObject.activeInHierarchy
                ? StripTmpMarkup(text.text)
                : string.Empty;
        }

        private static string StripTmpMarkup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>')
                {
                    inTag = false;
                    continue;
                }

                if (!inTag)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Trim();
        }

        private static bool SelectViaModIoNavigation(Selectable selectable)
        {
            if (selectable == null)
            {
                return false;
            }

            if (InputNavigationType != null && InputNavigationSelectMethod != null)
            {
                UnityEngine.Object[] instances = Resources.FindObjectsOfTypeAll(InputNavigationType);
                if (instances.Length > 0)
                {
                    InputNavigationSelectMethod.Invoke(instances[0], new object[] { selectable, true });
                    return true;
                }
            }

            selectable.Select();
            return true;
        }

        public sealed class TabItem
        {
            private readonly Func<bool> _select;

            public TabItem(string id, string label, bool isSelected, Func<bool> select)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                IsSelected = isSelected;
                _select = select;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public bool IsSelected { get; private set; }

            public bool Select()
            {
                return _select != null && _select();
            }
        }

        public sealed class ButtonAction
        {
            private readonly Func<bool> _focus;
            private readonly Func<bool> _activate;
            private readonly Func<bool> _isEnabled;
            private readonly Func<bool> _isVisible;

            public ButtonAction(
                string id,
                string label,
                Func<bool> focus,
                Func<bool> activate,
                Func<bool> isEnabled,
                Func<bool> isVisible)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                _focus = focus;
                _activate = activate;
                _isEnabled = isEnabled;
                _isVisible = isVisible;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public bool Focus()
            {
                return _focus != null && _focus();
            }

            public bool Activate()
            {
                return _activate != null && _activate();
            }

            public bool IsEnabled()
            {
                return _isEnabled == null || _isEnabled();
            }

            public bool IsVisible()
            {
                return _isVisible == null || _isVisible();
            }
        }

        public sealed class DropdownItem
        {
            private readonly MultiTargetDropdown _dropdown;
            private readonly Func<MultiTargetDropdown, int, bool> _setValue;
            private readonly Func<MultiTargetDropdown, bool> _focus;

            public DropdownItem(
                string id,
                string label,
                MultiTargetDropdown dropdown,
                Func<MultiTargetDropdown, int, bool> setValue,
                Func<MultiTargetDropdown, bool> focus)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                _dropdown = dropdown;
                _setValue = setValue;
                _focus = focus;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public bool IsVisible
            {
                get { return _dropdown != null && _dropdown.gameObject.activeInHierarchy; }
            }

            public int Value
            {
                get { return _dropdown != null ? _dropdown.value : -1; }
            }

            public IReadOnlyList<string> GetOptions()
            {
                List<string> result = new List<string>();
                if (_dropdown == null || _dropdown.options == null)
                {
                    return result;
                }

                for (int i = 0; i < _dropdown.options.Count; i++)
                {
                    TMP_Dropdown.OptionData option = _dropdown.options[i];
                    result.Add(StripTmpMarkup(option != null ? option.text : string.Empty));
                }

                return result;
            }

            public bool Focus()
            {
                return _focus != null && _focus(_dropdown);
            }

            public bool SetValue(int value)
            {
                return _setValue != null && _setValue(_dropdown, value);
            }
        }

        public sealed class CollectionItem
        {
            public CollectionItem(
                int index,
                string label,
                string status,
                ListItem nativeItem,
                MultiTargetToggle toggle,
                Button unsubscribeButton,
                Button moreOptionsButton)
            {
                Index = index;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                NativeItem = nativeItem;
                Toggle = toggle;
                UnsubscribeButton = unsubscribeButton;
                MoreOptionsButton = moreOptionsButton;
            }

            public int Index { get; private set; }

            public string Label { get; private set; }

            public string Status { get; private set; }

            public ListItem NativeItem { get; private set; }

            public MultiTargetToggle Toggle { get; private set; }

            public Button UnsubscribeButton { get; private set; }

            public Button MoreOptionsButton { get; private set; }

            public bool IsVisible
            {
                get { return NativeItem != null && NativeItem.gameObject.activeInHierarchy; }
            }
        }
    }
}
