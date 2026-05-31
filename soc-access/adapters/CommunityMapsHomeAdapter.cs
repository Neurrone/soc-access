using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIO;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquestAccess.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CommunityMapsHomeAdapter
    {
        private static readonly FieldInfo RowsField = AccessTools.Field(typeof(Home), "BrowserPanelModListRows");
        private static readonly FieldInfo FeaturedNameField = AccessTools.Field(typeof(Home), "featuredSelectedName");
        private static readonly FieldInfo FeaturedSubscribeTextField = AccessTools.Field(typeof(Home), "featuredSelectedSubscribeButtonText");
        private static readonly FieldInfo FeaturedProfilesField = AccessTools.Field(typeof(Home), "featuredProfiles");
        private static readonly FieldInfo FeaturedIndexField = AccessTools.Field(typeof(Home), "featuredIndex");
        private static readonly MethodInfo ShowFeaturedHighlightMethod = AccessTools.Method(typeof(Home), "ShowFeaturedHighlight");
        private static readonly MethodInfo SelectFeaturedModMethod = AccessTools.Method(typeof(Home), "SelectFeaturedMod");
        private static readonly MethodInfo SubscribeFeaturedMethod = AccessTools.Method(typeof(Home), "SubscribeToFeaturedMod");
        private static readonly MethodInfo MoreOptionsFeaturedMethod = AccessTools.Method(typeof(Home), "OpenMoreOptionsForFeaturedSlot");
        private static readonly MethodInfo PageFeaturedRowMethod = AccessTools.Method(typeof(Home), "PageFeaturedRow");
        private static readonly Type InputNavigationType = AccessTools.TypeByName("ModIOBrowser.InputNavigation");
        private static readonly MethodInfo InputNavigationSelectMethod =
            InputNavigationType != null ? AccessTools.Method(InputNavigationType, "Select", new[] { typeof(Selectable), typeof(bool) }) : null;
        private static readonly FieldInfo HomeScrollRectField = AccessTools.Field(typeof(Home), "scrollRect");
        private static readonly FieldInfo RowItemsField = AccessTools.Field(typeof(ModListRow), "items");
        private static readonly FieldInfo RowContainerField = AccessTools.Field(typeof(ModListRow), "ModListItemContainer");
        private static readonly FieldInfo RowErrorPanelField = AccessTools.Field(typeof(ModListRow), "ErrorPanel");
        private static readonly FieldInfo RowLoadingPanelField = AccessTools.Field(typeof(ModListRow), "LoadingPanel");

        private readonly Home _home;
        private ListItem _selectedItem;
        private ListItem _lastScrolledItem;
        private int _selectedRowIndex = -1;
        private readonly string _browseLabel;
        private readonly string _collectionLabel;
        private readonly string _featuredLabel;
        private readonly string _searchFilterLabel;
        private readonly string _moreOptionsLabel;
        private readonly string _subscribeLabel;
        private readonly string _unsubscribeLabel;

        public CommunityMapsHomeAdapter(Home home)
        {
            _home = home;
            _browseLabel = Translate("Browse");
            _collectionLabel = Translate("Collection");
            _featuredLabel = Translate("Featured maps & mods");
            _searchFilterLabel = FindTopBarText("Search & filter");
            _moreOptionsLabel = Translate("More options");
            _subscribeLabel = Translate("Subscribe");
            _unsubscribeLabel = Translate("Unsubscribe");
        }

        public static CommunityMapsHomeAdapter TryCreate()
        {
            Home[] homes = Resources.FindObjectsOfTypeAll<Home>();
            for (int i = 0; i < homes.Length; i++)
            {
                CommunityMapsHomeAdapter adapter = new CommunityMapsHomeAdapter(homes[i]);
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
                && _home != null
                && _home.BrowserPanel != null
                && (_home.BrowserPanel.activeInHierarchy || Collection.IsOn());
        }

        public string Title
        {
            get { return _browseLabel; }
        }

        public string FeaturedLabel
        {
            get { return _featuredLabel; }
        }

        public bool IsBrowseSelected
        {
            get { return _home != null && _home.BrowserPanel != null && _home.BrowserPanel.activeInHierarchy; }
        }

        public bool IsCollectionSelected
        {
            get { return Collection.IsOn(); }
        }

        public IReadOnlyList<TabItem> GetTabs()
        {
            List<TabItem> tabs = new List<TabItem>();
            if (!string.IsNullOrWhiteSpace(_browseLabel))
            {
                tabs.Add(new TabItem("browse", _browseLabel, () => IsBrowseSelected, OpenBrowse));
            }

            if (!string.IsNullOrWhiteSpace(_collectionLabel))
            {
                tabs.Add(new TabItem("collection", _collectionLabel, () => IsCollectionSelected, OpenCollection));
            }

            return tabs;
        }

        public string SearchFilterLabel
        {
            get { return _searchFilterLabel; }
        }

        public bool HasSearchFilter
        {
            get { return !string.IsNullOrWhiteSpace(_searchFilterLabel); }
        }

        public string MoreOptionsLabel
        {
            get { return _moreOptionsLabel; }
        }

        public string FeaturedName
        {
            get { return GetText(GetField<TMP_Text>(FeaturedNameField)); }
        }

        public string FeaturedSubscribeLabel
        {
            get
            {
                string text = GetText(GetField<TMP_Text>(FeaturedSubscribeTextField));
                return !string.IsNullOrWhiteSpace(text) ? text : _subscribeLabel;
            }
        }

        public bool HasFeatured
        {
            get
            {
                ModProfile[] profiles = GetFeaturedProfiles();
                int index = GetFeaturedIndex();
                return profiles != null && index >= 0 && index < profiles.Length;
            }
        }

        public int FeaturedIndex
        {
            get { return GetFeaturedIndex(); }
        }

        public IReadOnlyList<FeaturedItem> GetFeaturedItems()
        {
            List<FeaturedItem> result = new List<FeaturedItem>();
            ModProfile[] profiles = GetFeaturedProfiles();
            if (profiles == null)
            {
                return result;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                string label = profiles[i].name;
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                result.Add(new FeaturedItem(i, label));
            }

            return result;
        }

        public bool FocusFeatured()
        {
            if (_home == null || ShowFeaturedHighlightMethod == null)
            {
                return false;
            }

            ShowFeaturedHighlightMethod.Invoke(_home, null);
            return true;
        }

        public bool ActivateFeatured()
        {
            if (_home == null || SelectFeaturedModMethod == null)
            {
                return false;
            }

            SelectFeaturedModMethod.Invoke(_home, null);
            return true;
        }

        public bool FocusFeaturedItem(FeaturedItem item)
        {
            return item != null && FocusFeaturedIndex(item.Index);
        }

        public bool ActivateFeaturedItem(FeaturedItem item)
        {
            return item != null && FocusFeaturedIndex(item.Index) && ActivateFeatured();
        }

        public bool SubscribeFeatured()
        {
            if (_home == null || SubscribeFeaturedMethod == null)
            {
                return false;
            }

            SubscribeFeaturedMethod.Invoke(_home, null);
            return true;
        }

        public bool OpenFeaturedOptions()
        {
            if (_home == null || MoreOptionsFeaturedMethod == null)
            {
                return false;
            }

            MoreOptionsFeaturedMethod.Invoke(_home, null);
            return true;
        }

        public bool PreviousFeatured()
        {
            return PageFeatured(right: false);
        }

        public bool NextFeatured()
        {
            return PageFeatured(right: true);
        }

        public IReadOnlyList<RowItem> GetRows()
        {
            List<RowItem> rows = new List<RowItem>();
            ModListRow[] nativeRows = GetField<ModListRow[]>(RowsField);
            if (nativeRows == null)
            {
                return rows;
            }

            for (int i = 0; i < nativeRows.Length; i++)
            {
                ModListRow row = nativeRows[i];
                if (row == null || !row.gameObject.activeInHierarchy)
                {
                    continue;
                }

                rows.Add(new RowItem(
                    i,
                    FindRowLabel(row),
                    GetRowStatus(row),
                    GetRowItems(i, row)));
            }

            return rows;
        }

        public bool FocusItem(ModItem item)
        {
            if (item == null || item.NativeItem == null || item.NativeItem.selectable == null)
            {
                return false;
            }

            _selectedItem = item.NativeItem;
            SelectViaModIoNavigation(item.NativeItem.selectable);
            item.NativeItem.viewportRestraint?.CheckSelectionHorizontalVisibility();
            _selectedRowIndex = item.RowIndex;
            ScrollIntoView(item.NativeItem.transform as RectTransform);
            return true;
        }

        public bool ActivateItem(ModItem item)
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

        public bool HasSelectedItem
        {
            get { return _selectedItem != null && _selectedItem.gameObject.activeInHierarchy; }
        }

        public bool IsSelectedItemInRow(int rowIndex)
        {
            return HasSelectedItem && _selectedRowIndex == rowIndex;
        }

        public string SelectedSubscribeLabel
        {
            get
            {
                ModProfile? profile = GetProfile(_selectedItem);
                if (profile.HasValue && IsSubscribed(profile.Value.id))
                {
                    return _unsubscribeLabel;
                }

                return _subscribeLabel;
            }
        }

        public bool SubscribeSelectedItem()
        {
            if (!HasSelectedItem)
            {
                return false;
            }

            SelectViaModIoNavigation(_selectedItem.selectable);
            _selectedItem.viewportRestraint?.CheckSelectionHorizontalVisibility();
            ScrollIntoView(_selectedItem.transform as RectTransform);
            return InvokeStaticBool("ModIOBrowser.Implementation.SelectionOverlayHandler", "TryAlternateForBrowserOverlayObject");
        }

        public bool OpenSelectedItemOptions()
        {
            if (!HasSelectedItem)
            {
                return false;
            }

            SelectViaModIoNavigation(_selectedItem.selectable);
            _selectedItem.viewportRestraint?.CheckSelectionHorizontalVisibility();
            ScrollIntoView(_selectedItem.transform as RectTransform);
            return InvokeStaticBool("ModIOBrowser.Implementation.SelectionOverlayHandler", "TryToOpenMoreOptionsForBrowserOverlayObject");
        }

        public bool Close()
        {
            Browser.Close();
            return true;
        }

        private bool OpenBrowse()
        {
            if (IsBrowseSelected)
            {
                return true;
            }

            if (_home == null)
            {
                return false;
            }

            _home.Open();
            return true;
        }

        private bool OpenCollection()
        {
            if (IsCollectionSelected)
            {
                return true;
            }

            Collection[] collections = Resources.FindObjectsOfTypeAll<Collection>();
            for (int i = 0; i < collections.Length; i++)
            {
                Collection collection = collections[i];
                if (collection == null || collection.CollectionPanel == null)
                {
                    continue;
                }

                collection.Open();
                return true;
            }

            return false;
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

        public bool HasDownloadsMenu
        {
            get { return Browser.IsOpen; }
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

        public string Translate(string key)
        {
            TranslationManager[] managers = Resources.FindObjectsOfTypeAll<TranslationManager>();
            if (managers.Length == 0 || string.IsNullOrWhiteSpace(key))
            {
                return key ?? string.Empty;
            }

            string translated = managers[0].Get(key);
            return StripTmpMarkup(translated);
        }

        private bool PageFeatured(bool right)
        {
            if (_home == null || PageFeaturedRowMethod == null)
            {
                return false;
            }

            PageFeaturedRowMethod.Invoke(_home, new object[] { right });
            return true;
        }

        private bool FocusFeaturedIndex(int targetIndex)
        {
            ModProfile[] profiles = GetFeaturedProfiles();
            if (profiles == null || targetIndex < 0 || targetIndex >= profiles.Length)
            {
                return false;
            }

            FocusFeatured();

            int currentIndex = GetFeaturedIndex();
            if (currentIndex == targetIndex)
            {
                return true;
            }

            int length = profiles.Length;
            int forward = (targetIndex - currentIndex + length) % length;
            int backward = (currentIndex - targetIndex + length) % length;
            bool right = forward <= backward;
            int steps = right ? forward : backward;
            for (int i = 0; i < steps; i++)
            {
                PageFeatured(right);
            }

            return true;
        }

        private IReadOnlyList<ModItem> GetRowItems(int rowIndex, ModListRow row)
        {
            List<ModItem> result = new List<ModItem>();
            IList items = RowItemsField != null ? RowItemsField.GetValue(row) as IList : null;
            if (items == null)
            {
                return result;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ListItem item = items[i] as ListItem;
                if (item == null || !item.gameObject.activeInHierarchy || item.isPlaceholder)
                {
                    continue;
                }

                string label = GetItemLabel(item);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                result.Add(new ModItem(rowIndex, i, label, GetProgressText(item), item));
            }

            return result;
        }

        private string FindRowLabel(ModListRow row)
        {
            Transform itemContainer = RowContainerField != null ? RowContainerField.GetValue(row) as Transform : null;
            string ancestorHeader = FindAncestorRowHeader(row, itemContainer);
            if (!string.IsNullOrWhiteSpace(ancestorHeader))
            {
                return ancestorHeader;
            }

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || itemContainer != null && text.transform.IsChildOf(itemContainer))
                {
                    continue;
                }

                string value = GetText(text);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            Transform rowTransform = row.transform;
            Transform parent = rowTransform.parent;
            if (parent == null)
            {
                return string.Empty;
            }

            int rowSiblingIndex = rowTransform.GetSiblingIndex();
            for (int i = rowSiblingIndex - 1; i >= 0; i--)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling == null || !sibling.gameObject.activeInHierarchy || sibling.GetComponent<ModListRow>() != null)
                {
                    continue;
                }

                TMP_Text[] siblingTexts = sibling.GetComponentsInChildren<TMP_Text>(false);
                for (int textIndex = 0; textIndex < siblingTexts.Length; textIndex++)
                {
                    string value = GetText(siblingTexts[textIndex]);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return string.Empty;
        }

        private static string FindAncestorRowHeader(ModListRow row, Transform itemContainer)
        {
            Transform current = row != null ? row.transform : null;
            while (current != null && current.parent != null)
            {
                if (current.name.StartsWith("ModRow_", StringComparison.OrdinalIgnoreCase))
                {
                    TMP_Text[] texts = current.GetComponentsInChildren<TMP_Text>(false);
                    for (int i = 0; i < texts.Length; i++)
                    {
                        TMP_Text text = texts[i];
                        if (text == null
                            || text.transform.IsChildOf(row.transform)
                            || itemContainer != null && text.transform.IsChildOf(itemContainer))
                        {
                            continue;
                        }

                        string value = GetText(text);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }

                    return string.Empty;
                }

                current = current.parent;
            }

            return string.Empty;
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

        private string GetRowStatus(ModListRow row)
        {
            GameObject loading = RowLoadingPanelField != null ? RowLoadingPanelField.GetValue(row) as GameObject : null;
            if (loading != null && loading.activeInHierarchy)
            {
                return Translate("Loading");
            }

            GameObject error = RowErrorPanelField != null ? RowErrorPanelField.GetValue(row) as GameObject : null;
            if (error != null && error.activeInHierarchy)
            {
                return Translate("Error");
            }

            return string.Empty;
        }

        private string GetItemLabel(ListItem item)
        {
            TMP_Text title = GetField<TMP_Text>(item, "title");
            return GetText(title);
        }

        private string GetProgressText(ListItem item)
        {
            object progressTab = GetField<object>(item, "progressTab");
            if (progressTab == null)
            {
                return string.Empty;
            }

            TMP_Text text = GetField<TMP_Text>(progressTab, "progressBarText");
            return GetText(text);
        }

        private void ScrollIntoView(RectTransform source)
        {
            if (source == null)
            {
                return;
            }

            ListItem item = source.GetComponent<ListItem>();
            if (item != null && ReferenceEquals(item, _lastScrolledItem))
            {
                return;
            }

            ScrollRect scrollRect = HomeScrollRectField != null ? HomeScrollRectField.GetValue(_home) as ScrollRect : null;
            if (scrollRect == null || scrollRect.content == null)
            {
                return;
            }

            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : ((Component)scrollRect).GetComponent<RectTransform>();
            if (viewport == null)
            {
                return;
            }

            if (item != null)
            {
                _lastScrolledItem = item;
            }

            Canvas.ForceUpdateCanvases();

            Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, source);
            Rect viewportRect = viewport.rect;
            float scrollableHeight = scrollRect.content.rect.height - viewportRect.height;
            if (scrollableHeight <= 0f)
            {
                return;
            }

            float normalized = scrollRect.verticalNormalizedPosition;
            if (itemBounds.max.y > viewportRect.max.y)
            {
                normalized += (itemBounds.max.y - viewportRect.max.y) / scrollableHeight;
            }
            else if (itemBounds.min.y < viewportRect.min.y)
            {
                normalized -= (viewportRect.min.y - itemBounds.min.y) / scrollableHeight;
            }
            else
            {
                return;
            }

            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
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

        private bool IsSubscribed(ModId id)
        {
            Collection[] collections = Resources.FindObjectsOfTypeAll<Collection>();
            if (collections.Length == 0 || collections[0] == null)
            {
                return false;
            }

            MethodInfo method = AccessTools.Method(typeof(Collection), "IsSubscribed", new[] { typeof(ModId) });
            object result = method != null ? method.Invoke(collections[0], new object[] { id }) : null;
            return result is bool && (bool)result;
        }

        private ModProfile[] GetFeaturedProfiles()
        {
            return GetField<ModProfile[]>(FeaturedProfilesField);
        }

        private int GetFeaturedIndex()
        {
            return FeaturedIndexField != null ? (int)FeaturedIndexField.GetValue(_home) : -1;
        }

        private ModProfile? GetProfile(ListItem item)
        {
            if (item == null)
            {
                return null;
            }

            object value = GetField<object>(item, "profile");
            if (value is ModProfile)
            {
                return (ModProfile)value;
            }

            return null;
        }

        private T GetField<T>(FieldInfo field)
        {
            return field != null && _home != null ? (T)field.GetValue(_home) : default(T);
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

        private static string GetText(TMP_Text text)
        {
            return text != null && text.gameObject.activeInHierarchy
                ? StripTmpMarkup(text.text)
                : string.Empty;
        }

        private static bool InvokeStaticBool(string typeName, string methodName)
        {
            Type type = AccessTools.TypeByName(typeName);
            MethodInfo method = type != null ? AccessTools.Method(type, methodName) : null;
            if (method == null)
            {
                return false;
            }

            object result = method.Invoke(null, null);
            return result is bool && (bool)result;
        }

        private static string StripTmpMarkup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Replace("<color=red>", string.Empty).Replace("</color>", string.Empty);
        }

        internal sealed class RowItem
        {
            public RowItem(int index, string label, string status, IReadOnlyList<ModItem> items)
            {
                Index = index;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                Items = items ?? new ModItem[0];
            }

            public int Index { get; private set; }
            public string Label { get; private set; }
            public string Status { get; private set; }
            public IReadOnlyList<ModItem> Items { get; private set; }
        }

        internal sealed class FeaturedItem
        {
            public FeaturedItem(int index, string label)
            {
                Index = index;
                Label = label ?? string.Empty;
            }

            public int Index { get; private set; }
            public string Label { get; private set; }
        }

        internal sealed class TabItem
        {
            private readonly Func<bool> _isSelected;
            private readonly Func<bool> _select;

            public TabItem(string id, string label, Func<bool> isSelected, Func<bool> select)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                _isSelected = isSelected;
                _select = select;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public bool IsSelected { get { return _isSelected != null && _isSelected(); } }
            public bool Select() { return _select != null && _select(); }
        }

        internal sealed class ModItem
        {
            public ModItem(int rowIndex, int index, string label, string status, ListItem nativeItem)
            {
                RowIndex = rowIndex;
                Index = index;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                NativeItem = nativeItem;
            }

            public int RowIndex { get; private set; }
            public int Index { get; private set; }
            public string Label { get; private set; }
            public string Status { get; private set; }
            public ListItem NativeItem { get; private set; }
        }
    }
}
