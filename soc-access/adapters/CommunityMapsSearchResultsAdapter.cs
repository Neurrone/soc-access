using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommunityMapsSearchResultsAdapter : IPresent
    {
        private static readonly Type SearchResultListItemType = AccessTools.TypeByName("ModIOBrowser.Implementation.SearchResultListItem");
        private static readonly Type SearchResultOverlayType = AccessTools.TypeByName("ModIOBrowser.Implementation.SearchResultListItem_Overlay");
        private static readonly FieldInfo FoundTextField = AccessTools.Field(typeof(SearchResults), "SearchResultsFoundText");
        private static readonly FieldInfo MainTagNameField = AccessTools.Field(typeof(SearchResults), "SearchResultsMainTagName");
        private static readonly FieldInfo MainTagCategoryNameField = AccessTools.Field(typeof(SearchResults), "SearchResultsMainTagCategoryName");
        private static readonly FieldInfo MainTagField = AccessTools.Field(typeof(SearchResults), "SearchResultsMainTag");
        private static readonly FieldInfo OtherTagsTextField = AccessTools.Field(typeof(SearchResults), "SearchResultsNumberOfOtherTags");
        private static readonly FieldInfo SearchPhraseField = AccessTools.Field(typeof(SearchResults), "SearchResultsSearchPhrase");
        private static readonly FieldInfo SearchPhraseTextField = AccessTools.Field(typeof(SearchResults), "SearchResultsSearchPhraseText");
        private static readonly FieldInfo SortDropdownField = AccessTools.Field(typeof(SearchResults), "SearchResultsSortByDropdown");
        private static readonly FieldInfo EndOfResultsField = AccessTools.Field(typeof(SearchResults), "SearchResultsEndOfResults");
        private static readonly FieldInfo NoResultsField = AccessTools.Field(typeof(SearchResults), "SearchResultsNoResultsText");
        private static readonly FieldInfo EndOfResultsHeaderField = AccessTools.Field(typeof(SearchResults), "SearchResultsEndOfResultsHeader");
        private static readonly FieldInfo EndOfResultsTextField = AccessTools.Field(typeof(SearchResults), "SearchResultsEndOfResultsText");
        private static readonly FieldInfo RefineFilterField = AccessTools.Field(typeof(SearchResults), "SearchResultsRefineFilter");
        private static readonly MethodInfo OpenDetailsMethod = AccessTools.Method(SearchResultListItemType, "OpenModDetailsForThisProfile");
        private static readonly MethodInfo OverlaySubscribeMethod = AccessTools.Method(SearchResultOverlayType, "SubscribeButton");
        private static readonly MethodInfo OverlayMoreOptionsMethod = AccessTools.Method(SearchResultOverlayType, "ShowMoreOptions");
        private static readonly FieldInfo OverlayListItemField = AccessTools.Field(SearchResultOverlayType, "listItemToReplicate");
        private static readonly FieldInfo OverlaySubscribeTextField = AccessTools.Field(SearchResultOverlayType, "subscribeButtonText");

        // WHAT THE PAGE IS KEYED ON. The four deleted hooks (SearchResults.Open,
        // OpenWithoutRefreshing, Refresh and Get) each reported one fetch, and all they did was drop
        // this snapshot. The panel says the same thing itself: how many rows its list parent holds,
        // the fetch status it keeps, and the phrase it last searched for. A fetch cannot change the
        // results without changing at least one of the three, and reading them costs three field
        // reads a frame.
        private static readonly FieldInfo StatusField = AccessTools.Field(typeof(SearchResults), "searchResultsStatus");
        private static readonly FieldInfo LastPhraseField = AccessTools.Field(typeof(SearchResults), "lastUsedSearchPhrase");

        private readonly SearchResults _results;
        private string _backLabel;
        private string _title;
        private Selectable _refineFilter;
        private string _refineFilterLabel;
        private SortDropdown _sort;

        // Whether the panel's own texts have been read since it was last shown. The adapter is built
        // as soon as mod.io's SearchResults singleton exists, which is when the browser OPENS and
        // long before this panel is drawn, and a walk of a hidden panel finds none of its texts. So
        // the walk is keyed on the panel being drawn, and repeated once each time it is drawn again -
        // which is exactly what the deleted SearchResults.Open hook used to be for.
        private bool _panelWasDrawn;
        private bool _labelsRead;

        private bool _snapshotTaken;
        private int _stampRowCount;
        private int _stampStatus;
        private string _stampPhrase;
        private string _summaryText;
        private string _footerText;
        private List<ResultItem> _resultItems = new List<ResultItem>();
        private bool _overlayRead;
        private object _activeOverlay;
        private Component _overlayItem;
        private bool _hasSelectedResult;
        private string _subscribeLabel;
        private string _moreOptionsLabel;

        public CommunityMapsSearchResultsAdapter(SearchResults results)
        {
            _results = results;
            _sort = new SortDropdown(null);
        }

        /// <summary>Read the panel's own texts once per showing, on the frame it becomes drawn.
        /// </summary>
        private void EnsureLabels()
        {
            bool drawn = IsPresent();
            if (drawn && !_panelWasDrawn)
            {
                _labelsRead = false;
            }

            _panelWasDrawn = drawn;
            if (_labelsRead || !drawn)
            {
                return;
            }

            _labelsRead = true;
            // The summary is one of the panel's texts, so the snapshot below is taken again with it.
            _snapshotTaken = false;
            _backLabel = CommunityMapsText.FindTopBar("Back / Exit");
            if (string.IsNullOrWhiteSpace(_backLabel))
            {
                _backLabel = ModText.Get(ModStrings.Screens.Back);
            }

            _title = FindPanelTitle();
            _refineFilter = Reflect.Cast<Selectable>(_results, RefineFilterField);
            _refineFilterLabel = GetSelectableLabel(_refineFilter);
            _sort = new SortDropdown(Reflect.Cast<TMP_Dropdown>(_results, SortDropdownField));
        }

        /// <summary>Take the page again when the panel has fetched since the last time.</summary>
        private void EnsureSnapshot()
        {
            int rowCount = _results != null && _results.SearchResultsListItemParent != null
                ? _results.SearchResultsListItemParent.childCount
                : 0;
            int status = ReadStatus();
            string phrase = ReadLastPhrase();
            if (_snapshotTaken && rowCount == _stampRowCount && status == _stampStatus && phrase == _stampPhrase)
            {
                return;
            }

            _snapshotTaken = true;
            _stampRowCount = rowCount;
            _stampStatus = status;
            _stampPhrase = phrase;
            _summaryText = BuildSummaryText();
            _footerText = BuildFooterText();
            _resultItems = ScanResults();
        }

        /// <summary>The selection overlay on its own key. Nothing the page snapshot watches moves
        /// when the player selects a different result, so the card is keyed on what mod.io's own
        /// handler says it is replicating: the overlay while it is drawn, and the row it was last
        /// set up over. Both are field reads.</summary>
        private void EnsureOverlay()
        {
            object overlay = CommunityMapsSources.SearchResultOverlay;
            Component item = GetOverlayItem(overlay);
            if (_overlayRead && ReferenceEquals(overlay, _activeOverlay) && ReferenceEquals(item, _overlayItem))
            {
                return;
            }

            _overlayRead = true;
            _activeOverlay = overlay;
            _overlayItem = item;
            _hasSelectedResult = item != null;
            _subscribeLabel = GetOverlaySubscribeLabel(overlay);
            _moreOptionsLabel = GetOverlayMoreOptionsLabel(overlay, _subscribeLabel);
        }

        private int ReadStatus()
        {
            if (_results == null || StatusField == null)
            {
                return -1;
            }

            object value = StatusField.GetValue(_results);
            return value == null ? -1 : (int)value;
        }

        private string ReadLastPhrase()
        {
            return _results != null && LastPhraseField != null
                ? LastPhraseField.GetValue(_results) as string
                : null;
        }

        public bool IsPresent()
        {
            return Browser.IsOpen
                && _results != null
                && _results.SearchResultsPanel != null
                && _results.SearchResultsPanel.activeInHierarchy;
        }

        public string Title
        {
            get { EnsureLabels(); return _title; }
        }

        public string SummaryText
        {
            get { EnsureSnapshot(); return _summaryText; }
        }

        public string FooterText
        {
            get { EnsureSnapshot(); return _footerText; }
        }

        public string RefineFilterLabel
        {
            get { EnsureLabels(); return _refineFilterLabel; }
        }

        public bool HasRefineFilter
        {
            get
            {
                EnsureLabels();
                return _refineFilter != null
                    && _refineFilter.gameObject.activeInHierarchy
                    && !string.IsNullOrWhiteSpace(RefineFilterLabel);
            }
        }

        public bool OpenRefineFilter()
        {
            InputReceiver.OnSearch();
            return true;
        }

        public SortDropdown Sort
        {
            get { EnsureLabels(); return _sort; }
        }

        /// <summary>The grid's rows, taken with the rest of the page's snapshot. A row is a child of
        /// the list parent with a title on it, and finding it is a walk of the hundred children with a
        /// <c>GetComponent</c> and three reflected field reads each - half a millisecond, which is not
        /// something a build can pay every frame. mod.io only ever APPENDS to this grid, and an append
        /// moves the list parent's <c>childCount</c>, which the stamp above already reads; a new search
        /// moves the phrase or the status. So the walk runs once per change and the rows are handed
        /// back unchanged in between.</summary>
        public IReadOnlyList<ResultItem> BuildResults()
        {
            EnsureSnapshot();
            return _resultItems;
        }

        private List<ResultItem> ScanResults()
        {
            List<ResultItem> result = new List<ResultItem>();
            if (SearchResultListItemType == null || _results == null || _results.SearchResultsListItemParent == null)
            {
                return result;
            }

            Transform parent = _results.SearchResultsListItemParent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Component component = child != null ? child.GetComponent(SearchResultListItemType) as Component : null;
                if (component == null || !component.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ListItem listItem = component as ListItem;
                if (listItem == null || listItem.isPlaceholder)
                {
                    continue;
                }

                TMP_Text title = Reflect.Typed<TMP_Text>(component, "title");
                if (string.IsNullOrWhiteSpace(CommunityMapsText.Of(title)))
                {
                    continue;
                }

                result.Add(new ResultItem(result.Count, BuildResultId(component, result.Count), title, component, listItem));
            }

            result.Sort(CompareResultPosition);
            for (int i = 0; i < result.Count; i++)
            {
                result[i].DisplayIndex = i;
            }

            return result;
        }

        public string SubscribeLabel
        {
            get { EnsureOverlay(); return _subscribeLabel; }
        }

        public bool HasSelectedResult
        {
            get { EnsureOverlay(); return _hasSelectedResult; }
        }

        public bool HasSubscribeAction
        {
            get { EnsureOverlay(); return _hasSelectedResult && !string.IsNullOrWhiteSpace(_subscribeLabel); }
        }

        public bool HasMoreOptionsAction
        {
            get { EnsureOverlay(); return _hasSelectedResult && !string.IsNullOrWhiteSpace(_moreOptionsLabel); }
        }

        public bool SubscribeSelected()
        {
            EnsureOverlay();
            if (_activeOverlay == null || OverlaySubscribeMethod == null)
            {
                return false;
            }

            OverlaySubscribeMethod.Invoke(_activeOverlay, null);
            return true;
        }

        public string MoreOptionsLabel
        {
            get { EnsureOverlay(); return _moreOptionsLabel; }
        }

        public bool OpenSelectedOptions()
        {
            EnsureOverlay();
            if (_activeOverlay == null || OverlayMoreOptionsMethod == null)
            {
                return false;
            }

            OverlayMoreOptionsMethod.Invoke(_activeOverlay, null);
            return true;
        }

        public string BackLabel
        {
            get { EnsureLabels(); return _backLabel; }
        }

        public bool Back()
        {
            InputReceiver.OnCancel();
            return true;
        }

        public void FocusResult(ResultItem item)
        {
            if (item == null || item.NativeListItem == null)
            {
                return;
            }

            Selectable selectable = item.NativeListItem.selectable;
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        public bool ActivateResult(ResultItem item)
        {
            if (item == null || item.NativeComponent == null || OpenDetailsMethod == null)
            {
                return false;
            }

            OpenDetailsMethod.Invoke(item.NativeComponent, null);
            return true;
        }

        private string BuildSummaryText()
        {
            EnsureLabels();
            return _title;
        }

        private string BuildFooterText()
        {
            GameObject noResults = Reflect.Cast<GameObject>(_results, NoResultsField);
            if (noResults != null && noResults.activeInHierarchy)
            {
                return JoinVisibleText(noResults);
            }

            GameObject endOfResults = Reflect.Cast<GameObject>(_results, EndOfResultsField);
            if (endOfResults != null && endOfResults.activeInHierarchy)
            {
                List<string> lines = new List<string>();
                AddIfNotEmpty(lines, CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_results, EndOfResultsHeaderField)));
                AddIfNotEmpty(lines, CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_results, EndOfResultsTextField)));
                return string.Join("\n", lines.ToArray());
            }

            return string.Empty;
        }

        private string GetActiveFilterText()
        {
            GameObject mainTag = Reflect.Cast<GameObject>(_results, MainTagField);
            if (mainTag == null || !mainTag.activeInHierarchy)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddIfNotEmpty(parts, GetCategoryLabel(CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_results, MainTagCategoryNameField))));
            AddIfNotEmpty(parts, GetTagLabel(CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_results, MainTagNameField))));
            AddIfNotEmpty(parts, CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_results, OtherTagsTextField)));
            return string.Join(" ", parts.ToArray());
        }

        private string GetSearchPhraseText()
        {
            GameObject phrase = Reflect.Cast<GameObject>(_results, SearchPhraseField);
            return phrase != null && phrase.activeInHierarchy
                ? CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_results, SearchPhraseTextField))
                : string.Empty;
        }

        private static Component GetOverlayItem(object overlay)
        {
            return overlay != null && OverlayListItemField != null
                ? OverlayListItemField.GetValue(overlay) as Component
                : null;
        }

        private static string GetOverlaySubscribeLabel(object overlay)
        {
            TMP_Text text = overlay != null && OverlaySubscribeTextField != null
                ? OverlaySubscribeTextField.GetValue(overlay) as TMP_Text
                : null;
            return CommunityMapsText.Of(text);
        }

        // Under EnsureOverlay, which re-reads only when the overlay card or the row it
        // replicates has moved - both read from the game.
        private static string GetOverlayMoreOptionsLabel(object overlay, string subscribeLabel)
        {
            Component component = overlay as Component;
            if (component == null || !component.gameObject.activeInHierarchy)
            {
                return string.Empty;
            }

            Button[] buttons = component.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                string label = GetButtonLabel(buttons[i]);
                if (!string.IsNullOrWhiteSpace(label) && label != subscribeLabel)
                {
                    return label;
                }
            }

            return string.Empty;
        }

        /// <summary>The mod's own id, which is what tells one result from another. <c>ModId</c> is a
        /// struct wrapping a long with no <c>ToString</c> of its own, so reading it as an object and
        /// printing it answered the type name for every row - which is why every result used to carry
        /// the same identity.</summary>
        private static string BuildResultId(Component component, int index)
        {
            object profile = Reflect.Typed<object>(component, "profile");
            object idValue = profile != null ? Reflect.Typed<object>(profile, "id") : null;
            string id = idValue is ModIO.ModId
                ? ((ModIO.ModId)idValue).id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (idValue != null ? idValue.ToString() : string.Empty);
            return !string.IsNullOrWhiteSpace(id) ? id : index.ToString();
        }

        private static int CompareResultPosition(ResultItem left, ResultItem right)
        {
            if (left == null || right == null)
            {
                return left == null ? 1 : -1;
            }

            Vector3 leftPosition = left.NativeComponent != null ? left.NativeComponent.transform.position : Vector3.zero;
            Vector3 rightPosition = right.NativeComponent != null ? right.NativeComponent.transform.position : Vector3.zero;
            int y = rightPosition.y.CompareTo(leftPosition.y);
            return y != 0 ? y : leftPosition.x.CompareTo(rightPosition.x);
        }

        private string FindPanelTitle()
        {
            if (_results == null || _results.SearchResultsPanel == null)
            {
                return string.Empty;
            }

            TMP_Text foundText = Reflect.Cast<TMP_Text>(_results, FoundTextField);
            TMP_Text[] texts = _results.SearchResultsPanel.GetComponentsInChildren<TMP_Text>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null
                    || text == foundText
                    || IsSearchResultsStatusText(text)
                    || IsSearchResultsControlText(text)
                    || IsSearchResultsItemText(text))
                {
                    continue;
                }

                string value = CommunityMapsText.Of(text);
                if (!string.IsNullOrWhiteSpace(value) && text.fontSize >= 30f)
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private bool IsSearchResultsStatusText(TMP_Text text)
        {
            GameObject noResults = Reflect.Cast<GameObject>(_results, NoResultsField);
            GameObject endOfResults = Reflect.Cast<GameObject>(_results, EndOfResultsField);
            GameObject mainTag = Reflect.Cast<GameObject>(_results, MainTagField);
            GameObject searchPhrase = Reflect.Cast<GameObject>(_results, SearchPhraseField);
            return IsChildOf(text, noResults)
                || IsChildOf(text, endOfResults)
                || IsChildOf(text, mainTag)
                || IsChildOf(text, searchPhrase)
                || text == Reflect.Cast<TMP_Text>(_results, MainTagNameField)
                || text == Reflect.Cast<TMP_Text>(_results, MainTagCategoryNameField)
                || text == Reflect.Cast<TMP_Text>(_results, OtherTagsTextField)
                || text == Reflect.Cast<TMP_Text>(_results, SearchPhraseTextField);
        }

        private bool IsSearchResultsControlText(TMP_Text text)
        {
            TMP_Dropdown sortDropdown = Reflect.Cast<TMP_Dropdown>(_results, SortDropdownField);
            Selectable refineFilter = Reflect.Cast<Selectable>(_results, RefineFilterField);
            return IsChildOf(text, sortDropdown != null ? sortDropdown.gameObject : null)
                || IsChildOf(text, refineFilter != null ? refineFilter.gameObject : null);
        }

        private bool IsSearchResultsItemText(TMP_Text text)
        {
            return IsChildOf(text, _results != null && _results.SearchResultsListItemParent != null
                ? _results.SearchResultsListItemParent.gameObject
                : null);
        }

        private static bool IsChildOf(Component child, GameObject parent)
        {
            if (child == null || parent == null)
            {
                return false;
            }

            return child.transform == parent.transform || child.transform.IsChildOf(parent.transform);
        }

        // Under EnsureLabels, once per adapter: the browser's fixed chrome labels do not change
        // while the panel is up.
        private static string GetSelectableLabel(Selectable selectable)
        {
            TMP_Text text = selectable != null ? selectable.GetComponentInChildren<TMP_Text>(false) : null;
            return CommunityMapsText.Of(text);
        }

        // Under EnsureOverlay, which re-reads only when the overlay card or the row it
        // replicates has moved - both read from the game.
        private static string GetButtonLabel(Button button)
        {
            TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(false) : null;
            return CommunityMapsText.Of(text);
        }

        // Under EnsureSnapshot as well, for the footer the browser draws under the results.
        private static string JoinVisibleText(GameObject parent)
        {
            if (parent == null || !parent.activeInHierarchy)
            {
                return string.Empty;
            }

            List<string> lines = new List<string>();
            TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                AddIfNotEmpty(lines, CommunityMapsText.Of(texts[i]));
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string GetCategoryLabel(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return string.Empty;
            }

            string key = "ModBrowser/TagCategory/" + categoryName.Replace(" ", string.Empty);
            return GameText.Get(key, categoryName);
        }

        private static string GetTagLabel(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return string.Empty;
            }

            string key = "ModBrowser/Tag/" + tagName.Replace(" ", string.Empty);
            return GameText.Get(key, tagName);
        }

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        /// <summary>The results page's sort dropdown, answering the questions every drop list answers
        /// so the mod's own list screen can be opened over mod.io's popup. What TAKING an entry means
        /// is the page's, and it hands that over when it opens the list.</summary>
        public sealed class SortDropdown : IDropList
        {
            private readonly TMP_Dropdown _dropdown;
            private readonly string _label;

            public SortDropdown(TMP_Dropdown dropdown)
            {
                _dropdown = dropdown;
                _label = FindDropdownLabel(dropdown);
                GetOptions = () => BuildOptions(_dropdown);
                GetValue = () => _dropdown != null ? _dropdown.value : -1;
                IsEnabled = () => _dropdown != null
                    && _dropdown.gameObject.activeInHierarchy
                    && _dropdown.interactable;
                IsVisible = () => _dropdown != null && _dropdown.gameObject.activeInHierarchy;
                OpenPopup = () => DropdownPopup.Show(_dropdown);
                ClosePopup = () => DropdownPopup.Hide(_dropdown);
                IsPopupOpen = () => DropdownPopup.IsOpen(_dropdown);
                FocusOption = index => DropdownPopup.FocusOption(_dropdown, index);
            }

            public string Id
            {
                get { return "sort"; }
            }

            /// <summary>What the dropdown is choosing, in mod.io's own words ("Sort by:").</summary>
            public string Label
            {
                get { return _label; }
            }

            /// <summary>The drawn control itself, so a caller can key a node on it.</summary>
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

            /// <summary>The entry the dropdown is on, in mod.io's own words.</summary>
            public string CurrentLabel
            {
                get
                {
                    IReadOnlyList<string> options = GetOptions();
                    int value = GetValue();
                    return value >= 0 && value < options.Count ? options[value] : string.Empty;
                }
            }

            private static IReadOnlyList<string> BuildOptions(TMP_Dropdown dropdown)
            {
                List<string> result = new List<string>();
                if (dropdown == null || dropdown.options == null)
                {
                    return result;
                }

                for (int i = 0; i < dropdown.options.Count; i++)
                {
                    TMP_Dropdown.OptionData option = dropdown.options[i];
                    result.Add(SpokenLines.Clean(option != null ? option.text : string.Empty));
                }

                return result;
            }

            public void Focus()
            {
                if (_dropdown != null)
                {
                    NativeSelectionUtility.Select(_dropdown);
                }
            }

            public bool SetValue(int value)
            {
                if (_dropdown == null || value < 0 || value >= _dropdown.options.Count)
                {
                    return false;
                }

                _dropdown.value = value;
                _dropdown.RefreshShownValue();
                return true;
            }

            private static string FindDropdownLabel(TMP_Dropdown dropdown)
            {
                if (dropdown == null)
                {
                    return string.Empty;
                }

                Transform textLayout = dropdown.transform.Find("Text Layout");
                Transform labelTransform = textLayout != null ? textLayout.Find("Sort by:") : null;
                TMP_Text label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
                string value = CommunityMapsText.Of(label);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                return CommunityMapsText.Of(dropdown.captionText);
            }
        }

        public sealed class ResultItem
        {
            private readonly TMP_Text _title;

            public ResultItem(int index, string id, TMP_Text title, Component nativeComponent, ListItem nativeListItem)
            {
                DisplayIndex = index;
                Id = id ?? index.ToString();
                _title = title;
                NativeComponent = nativeComponent;
                NativeListItem = nativeListItem;
            }

            public int DisplayIndex { get; set; }

            public string Id { get; private set; }

            /// <summary>The row's drawn title, read off the game's own text when the node is READ
            /// rather than when the row was found: the rows are a snapshot, and mod.io refills a
            /// pooled row's text without the walk being made again.</summary>
            public string Label
            {
                get { return CommunityMapsText.Of(_title); }
            }

            public Component NativeComponent { get; private set; }

            public ListItem NativeListItem { get; private set; }
        }
    }
}
