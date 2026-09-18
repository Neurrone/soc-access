using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIO;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommunityMapsHomeAdapter : IPresent
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
        // The browser's own list item and progress tab are named by fields rather than by properties,
        // and every mod of every band is read through several of them on every build. The handle for
        // each (type, name) is resolved once, misses among them.
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> FieldHandles =
            new Dictionary<Type, Dictionary<string, FieldInfo>>();

        private readonly Home _home;
        private ListItem _selectedItem;
        private ListItem _lastScrolledItem;
        private int _selectedRowIndex = -1;
        // mod.io's own words for its controls, read once rather than once per row per frame. The
        // browser is instantiated once for the session and only hidden when it closes, so this
        // adapter outlives an options visit; mod.io re-translates its own UI when the language
        // changes under an open browser, so these are read again when it does (SyncLabelLanguage).
        private ILanguageDefinition _labelLanguage;
        private string _browseLabel;
        private string _collectionLabel;
        private string _featuredLabel;
        private string _searchFilterLabel;
        private string _moreOptionsLabel;
        private string _subscribeLabel;
        private string _unsubscribeLabel;
        private string _loadingLabel;
        private string _errorLabel;
        private string _downloadsLabel;

        // mod.io's own subscription check, and the collection object it is asked on: both were looked
        // up afresh for every mod whose Subscribe label was read.
        private static readonly MethodInfo CollectionIsSubscribedMethod =
            AccessTools.Method(typeof(Collection), "IsSubscribed", new[] { typeof(ModId) });
        private Collection _collection;

        // Which mesh each band's caption was found on. The search for it is up to three walks of the
        // band and of its neighbours; the words are still read off the mesh live. Only a HIT is kept:
        // the walks see switched-on meshes only, so a band looked at while it was not drawn has no
        // caption to find, and keeping that answer left the band nameless for as long as the browser
        // lived. A miss is stamped with whether the band was drawn when it was looked at and looked
        // for again when that changes - one bool read per nameless band per frame, and no walk
        // (AGENTS.md, Performance).
        private readonly Dictionary<ModListRow, TMP_Text> _rowLabelTexts = new Dictionary<ModListRow, TMP_Text>();
        private readonly Dictionary<ModListRow, bool> _rowLabelMisses = new Dictionary<ModListRow, bool>();

        public CommunityMapsHomeAdapter(Home home)
        {
            _home = home;
            SyncLabelLanguage();
        }

        private void ReadLabels()
        {
            _browseLabel = CommunityMapsText.Translate("Browse");
            _collectionLabel = CommunityMapsText.Translate("Collection");
            _featuredLabel = CommunityMapsText.Translate("Featured maps & mods");
            _searchFilterLabel = CommunityMapsText.FindTopBar("Search & filter");
            _moreOptionsLabel = CommunityMapsText.Translate("More options");
            _subscribeLabel = CommunityMapsText.Translate("Subscribe");
            _unsubscribeLabel = CommunityMapsText.Translate("Unsubscribe");
            _loadingLabel = CommunityMapsText.Translate("Loading");
            _errorLabel = CommunityMapsText.Translate("Error");
            _downloadsLabel = CommunityMapsText.Translate("Downloads");
        }

        /// <summary>Read the words above again where the game has changed language since. Asked from
        /// <see cref="IsPresent"/>, which the screen asks every frame before it reads anything.
        /// </summary>
        private void SyncLabelLanguage()
        {
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            ILanguageDefinition language = localization != null ? localization.CurrentLanguage : null;
            if (ReferenceEquals(language, _labelLanguage))
            {
                return;
            }

            _labelLanguage = language;
            ReadLabels();
        }

        public bool IsPresent()
        {
            SyncLabelLanguage();
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
            get { return CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_home, FeaturedNameField)); }
        }

        public string FeaturedSubscribeLabel
        {
            get
            {
                string text = CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_home, FeaturedSubscribeTextField));
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

        /// <summary>The bands the page is drawing and the maps in them, built only when the page has
        /// redrawn them. There are about eighty items across the bands and each one was a new object
        /// per frame; the key is what mod.io's own pools hold - how many bands are drawn, how many
        /// real items they hold between them, and which the first and last of those are - all read
        /// from the game on every call (AGENTS.md, Performance). Nothing that MOVES is part of the
        /// snapshot: a band's caption and state and an item's name and download progress are read off
        /// the game when they are asked for.</summary>
        public IReadOnlyList<RowItem> GetRows()
        {
            ModListRow[] nativeRows = Reflect.Cast<ModListRow[]>(_home, RowsField);
            int drawnRows = 0;
            int drawnItems = 0;
            object firstItem = null;
            object lastItem = null;
            for (int i = 0; nativeRows != null && i < nativeRows.Length; i++)
            {
                ModListRow row = nativeRows[i];
                if (row == null || !row.gameObject.activeInHierarchy)
                {
                    continue;
                }

                drawnRows++;
                IList items = RowItemsField != null ? RowItemsField.GetValue(row) as IList : null;
                for (int item = 0; items != null && item < items.Count; item++)
                {
                    ListItem listItem = items[item] as ListItem;
                    if (listItem == null || !listItem.gameObject.activeInHierarchy || listItem.isPlaceholder)
                    {
                        continue;
                    }

                    drawnItems++;
                    firstItem = firstItem ?? listItem;
                    lastItem = listItem;
                }
            }

            if (_rows != null
                && drawnRows == _rowCount
                && drawnItems == _itemCount
                && ReferenceEquals(firstItem, _firstItem)
                && ReferenceEquals(lastItem, _lastItem))
            {
                return _rows;
            }

            _rowCount = drawnRows;
            _itemCount = drawnItems;
            _firstItem = firstItem;
            _lastItem = lastItem;
            _rows = ReadRows(nativeRows);
            return _rows;
        }

        private IReadOnlyList<RowItem> ReadRows(ModListRow[] nativeRows)
        {
            List<RowItem> rows = new List<RowItem>();
            for (int i = 0; nativeRows != null && i < nativeRows.Length; i++)
            {
                ModListRow row = nativeRows[i];
                if (row == null || !row.gameObject.activeInHierarchy)
                {
                    continue;
                }

                rows.Add(new RowItem(this, i, row, GetRowItems(i, row)));
            }

            return rows;
        }

        private IReadOnlyList<RowItem> _rows;
        private int _rowCount = -1;
        private int _itemCount = -1;
        private object _firstItem;
        private object _lastItem;

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

        /// <summary>The word mod.io's overlay would draw on its subscribe button for one item -
        /// "Unsubscribe" for a mod already taken, "Subscribe" otherwise.</summary>
        public string GetItemSubscribeLabel(ModItem item)
        {
            ModProfile? profile = item != null ? GetProfile(item.NativeItem) : null;
            return profile.HasValue && IsSubscribed(profile.Value.id) ? _unsubscribeLabel : _subscribeLabel;
        }

        /// <summary>
        /// Press the overlay's subscribe button for one item.
        ///
        /// There is ONE overlay object (<c>SelectionOverlayHandler.homeModListItemOverlay</c>) and
        /// mod.io moves it onto whichever list item is selected - the item's own <c>OnSelect</c> calls
        /// <c>MoveSelection(this)</c>, which is what sets the overlay's <c>listItemToReplicate</c> -
        /// so acting on a particular item means selecting it first and then pressing the one button.
        /// </summary>
        public bool SubscribeItem(ModItem item)
        {
            return FocusItem(item) && SubscribeSelectedItem();
        }

        /// <summary>Open the overlay's "more options" menu for one item, selected the same way
        /// <see cref="SubscribeItem"/> selects it.</summary>
        public bool OpenItemOptions(ModItem item)
        {
            return FocusItem(item) && OpenSelectedItemOptions();
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

            Collection collection = CommunityMapsSources.Collection;
            if (collection == null || collection.CollectionPanel == null)
            {
                return false;
            }

            collection.Open();
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

        /// <summary>mod.io's own word for the download queue, as the collection page reads it.
        /// </summary>
        public string DownloadsLabel
        {
            get { return _downloadsLabel; }
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

                if (string.IsNullOrWhiteSpace(GetItemLabel(item)))
                {
                    continue;
                }

                result.Add(new ModItem(this, rowIndex, i, item));
            }

            return result;
        }

        private string FindRowLabel(ModListRow row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            TMP_Text kept;
            if (_rowLabelTexts.TryGetValue(row, out kept) && kept != null)
            {
                return CommunityMapsText.Of(kept);
            }

            bool drawn = row.gameObject.activeInHierarchy;
            bool missDrawn;
            if (_rowLabelMisses.TryGetValue(row, out missDrawn) && missDrawn == drawn)
            {
                return string.Empty;
            }

            TMP_Text found = FindRowLabelText(row);
            if (found == null)
            {
                _rowLabelMisses[row] = drawn;
                return string.Empty;
            }

            _rowLabelTexts[row] = found;
            _rowLabelMisses.Remove(row);
            return CommunityMapsText.Of(found);
        }

        private static TMP_Text FindRowLabelText(ModListRow row)
        {
            if (row == null)
            {
                return null;
            }

            Transform itemContainer = RowContainerField != null ? RowContainerField.GetValue(row) as Transform : null;
            TMP_Text ancestorHeader = FindAncestorRowHeader(row, itemContainer);
            if (ancestorHeader != null)
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

                if (!string.IsNullOrWhiteSpace(CommunityMapsText.Of(text)))
                {
                    return text;
                }
            }

            Transform rowTransform = row.transform;
            Transform parent = rowTransform.parent;
            if (parent == null)
            {
                return null;
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
                    if (!string.IsNullOrWhiteSpace(CommunityMapsText.Of(siblingTexts[textIndex])))
                    {
                        return siblingTexts[textIndex];
                    }
                }
            }

            return null;
        }

        private static TMP_Text FindAncestorRowHeader(ModListRow row, Transform itemContainer)
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

                        if (!string.IsNullOrWhiteSpace(CommunityMapsText.Of(text)))
                        {
                            return text;
                        }
                    }

                    return null;
                }

                current = current.parent;
            }

            return null;
        }

        private string GetRowStatus(ModListRow row)
        {
            GameObject loading = RowLoadingPanelField != null ? RowLoadingPanelField.GetValue(row) as GameObject : null;
            if (loading != null && loading.activeInHierarchy)
            {
                return _loadingLabel;
            }

            GameObject error = RowErrorPanelField != null ? RowErrorPanelField.GetValue(row) as GameObject : null;
            if (error != null && error.activeInHierarchy)
            {
                return _errorLabel;
            }

            return string.Empty;
        }

        private string GetItemLabel(ListItem item)
        {
            TMP_Text title = Reflect.Cast<TMP_Text>(item, "title");
            return CommunityMapsText.Of(title);
        }

        private string GetProgressText(ListItem item)
        {
            object progressTab = Reflect.Cast<object>(item, "progressTab");
            if (progressTab == null)
            {
                return string.Empty;
            }

            TMP_Text text = Reflect.Cast<TMP_Text>(progressTab, "progressBarText");
            return CommunityMapsText.Of(text);
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
            if (scrollRect == null)
            {
                return;
            }

            if (item != null)
            {
                _lastScrolledItem = item;
            }

            ScrollView.Reveal(scrollRect, source);
        }

        private static bool SelectViaModIoNavigation(Selectable selectable)
        {
            if (selectable == null)
            {
                return false;
            }

            object navigation = CommunityMapsSources.InputNavigation;
            if (navigation != null && InputNavigationSelectMethod != null)
            {
                InputNavigationSelectMethod.Invoke(navigation, new object[] { selectable, true });
                return true;
            }

            selectable.Select();
            return true;
        }

        private bool IsSubscribed(ModId id)
        {
            Collection collection = GetCollection();
            if (collection == null || CollectionIsSubscribedMethod == null)
            {
                return false;
            }

            object result = CollectionIsSubscribedMethod.Invoke(collection, new object[] { id });
            return result is bool && (bool)result;
        }

        private Collection GetCollection()
        {
            if (_collection != null)
            {
                return _collection;
            }

            // No probed flag: a miss here is mod.io's singleton not being up yet, and the read behind
            // it is a static field access, so asking again costs nothing.
            _collection = CommunityMapsSources.Collection;
            return _collection;
        }

        private ModProfile[] GetFeaturedProfiles()
        {
            return Reflect.Cast<ModProfile[]>(_home, FeaturedProfilesField);
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

            object value = Reflect.Cast<object>(item, "profile");
            if (value is ModProfile)
            {
                return (ModProfile)value;
            }

            return null;
        }

        /// <summary>The handle for one named field of one runtime type, resolved once. The field is
        /// looked up on the object's own type, as the browser's items are subclasses of what its
        /// public surface names.</summary>
        private static FieldInfo ResolveField(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Dictionary<string, FieldInfo> byName;
            if (!FieldHandles.TryGetValue(type, out byName))
            {
                byName = new Dictionary<string, FieldInfo>();
                FieldHandles[type] = byName;
            }

            FieldInfo field;
            if (!byName.TryGetValue(name, out field))
            {
                field = AccessTools.Field(type, name);
                byName[name] = field;
            }

            return field;
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

        public sealed class RowItem
        {
            private readonly CommunityMapsHomeAdapter _adapter;
            private readonly ModListRow _row;

            public RowItem(CommunityMapsHomeAdapter adapter, int index, ModListRow row, IReadOnlyList<ModItem> items)
            {
                _adapter = adapter;
                _row = row;
                Index = index;
                Items = items ?? new ModItem[0];
            }

            public int Index { get; private set; }

            /// <summary>The band's own caption, read off the mesh it was found on when it is asked
            /// for.</summary>
            public string Label
            {
                get { return _adapter != null ? _adapter.FindRowLabel(_row) ?? string.Empty : string.Empty; }
            }

            /// <summary>Whether the band is loading or failed, which changes while the page is
            /// open.</summary>
            public string Status
            {
                get { return _adapter != null ? _adapter.GetRowStatus(_row) ?? string.Empty : string.Empty; }
            }

            public IReadOnlyList<ModItem> Items { get; private set; }
        }

        public sealed class FeaturedItem
        {
            public FeaturedItem(int index, string label)
            {
                Index = index;
                Label = label ?? string.Empty;
            }

            public int Index { get; private set; }
            public string Label { get; private set; }
        }

        public sealed class TabItem
        {
            private readonly Func<bool> _isSelected;
            private readonly Func<bool> _select;

            public TabItem(string key, string label, Func<bool> isSelected, Func<bool> select)
            {
                Key = key ?? string.Empty;
                Label = label ?? string.Empty;
                _isSelected = isSelected;
                _select = select;
            }

            public string Key { get; private set; }
            public string Label { get; private set; }
            public bool IsSelected { get { return _isSelected != null && _isSelected(); } }
            public bool Select() { return _select != null && _select(); }
        }

        public sealed class ModItem
        {
            private readonly CommunityMapsHomeAdapter _adapter;

            public ModItem(CommunityMapsHomeAdapter adapter, int rowIndex, int index, ListItem nativeItem)
            {
                _adapter = adapter;
                RowIndex = rowIndex;
                Index = index;
                NativeItem = nativeItem;
            }

            public int RowIndex { get; private set; }
            public int Index { get; private set; }

            /// <summary>The map's own name, read off the game when it is asked for - so the eighty
            /// items of a page cost one read each, for the one the cursor is on.</summary>
            public string Label
            {
                get { return _adapter != null ? _adapter.GetItemLabel(NativeItem) ?? string.Empty : string.Empty; }
            }

            /// <summary>How far a download of it has got, which moves while the page is open.</summary>
            public string Status
            {
                get { return _adapter != null ? _adapter.GetProgressText(NativeItem) ?? string.Empty : string.Empty; }
            }

            public ListItem NativeItem { get; private set; }
        }
    }
}
