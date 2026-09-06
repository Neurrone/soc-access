using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquestAccess.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommunityMapsSearchResultsAdapter
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

        private readonly SearchResults _results;
        private readonly string _backLabel;
        private readonly string _title;
        private readonly string _summaryText;
        private readonly string _footerText;
        private readonly Selectable _refineFilter;
        private readonly string _refineFilterLabel;
        private readonly SortDropdown _sort;
        private readonly IReadOnlyList<ResultItem> _resultsSnapshot;
        private readonly object _activeOverlay;
        private readonly bool _hasSelectedResult;
        private readonly string _subscribeLabel;
        private readonly string _moreOptionsLabel;

        private CommunityMapsSearchResultsAdapter(SearchResults results)
        {
            _results = results;
            _backLabel = FindTopBarText("Back / Exit");
            if (string.IsNullOrWhiteSpace(_backLabel))
            {
                _backLabel = ModText.Get(ModStrings.Screens.Back);
            }

            _title = FindPanelTitle();
            _summaryText = BuildSummaryText();
            _footerText = BuildFooterText();
            _refineFilter = GetField<Selectable>(RefineFilterField);
            _refineFilterLabel = GetSelectableLabel(_refineFilter);
            _sort = new SortDropdown(GetField<TMP_Dropdown>(SortDropdownField));
            _resultsSnapshot = BuildResults();
            _activeOverlay = FindActiveOverlay();
            _hasSelectedResult = GetOverlayItem(_activeOverlay) != null;
            _subscribeLabel = GetOverlaySubscribeLabel(_activeOverlay);
            _moreOptionsLabel = GetOverlayMoreOptionsLabel(_activeOverlay, _subscribeLabel);
        }

        public static CommunityMapsSearchResultsAdapter TryCreate()
        {
            SearchResults[] results = Resources.FindObjectsOfTypeAll<SearchResults>();
            for (int i = 0; i < results.Length; i++)
            {
                CommunityMapsSearchResultsAdapter adapter = new CommunityMapsSearchResultsAdapter(results[i]);
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
                && _results != null
                && _results.SearchResultsPanel != null
                && _results.SearchResultsPanel.activeInHierarchy;
        }

        public string Title
        {
            get { return _title; }
        }

        public string SummaryText
        {
            get { return _summaryText; }
        }

        public string FooterText
        {
            get { return _footerText; }
        }

        public string RefineFilterLabel
        {
            get { return _refineFilterLabel; }
        }

        public bool HasRefineFilter
        {
            get
            {
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
            get { return _sort; }
        }

        public IReadOnlyList<ResultItem> Results
        {
            get { return _resultsSnapshot; }
        }

        public IReadOnlyList<ResultItem> BuildResults()
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

                string label = GetText(GetField<TMP_Text>(component, "title"));
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                result.Add(new ResultItem(result.Count, BuildResultId(component, result.Count), label, component, listItem));
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
            get { return _subscribeLabel; }
        }

        public bool HasSelectedResult
        {
            get { return _hasSelectedResult; }
        }

        public bool HasSubscribeAction
        {
            get { return _hasSelectedResult && !string.IsNullOrWhiteSpace(_subscribeLabel); }
        }

        public bool HasMoreOptionsAction
        {
            get { return _hasSelectedResult && !string.IsNullOrWhiteSpace(_moreOptionsLabel); }
        }

        public bool SubscribeSelected()
        {
            if (_activeOverlay == null || OverlaySubscribeMethod == null)
            {
                return false;
            }

            OverlaySubscribeMethod.Invoke(_activeOverlay, null);
            return true;
        }

        public string MoreOptionsLabel
        {
            get { return _moreOptionsLabel; }
        }

        public bool OpenSelectedOptions()
        {
            if (_activeOverlay == null || OverlayMoreOptionsMethod == null)
            {
                return false;
            }

            OverlayMoreOptionsMethod.Invoke(_activeOverlay, null);
            return true;
        }

        public string BackLabel
        {
            get { return _backLabel; }
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
            return _title;
        }

        private string BuildFooterText()
        {
            GameObject noResults = GetField<GameObject>(NoResultsField);
            if (noResults != null && noResults.activeInHierarchy)
            {
                return JoinVisibleText(noResults);
            }

            GameObject endOfResults = GetField<GameObject>(EndOfResultsField);
            if (endOfResults != null && endOfResults.activeInHierarchy)
            {
                List<string> lines = new List<string>();
                AddIfNotEmpty(lines, GetText(GetField<TMP_Text>(EndOfResultsHeaderField)));
                AddIfNotEmpty(lines, GetText(GetField<TMP_Text>(EndOfResultsTextField)));
                return string.Join("\n", lines.ToArray());
            }

            return string.Empty;
        }

        private string GetActiveFilterText()
        {
            GameObject mainTag = GetField<GameObject>(MainTagField);
            if (mainTag == null || !mainTag.activeInHierarchy)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddIfNotEmpty(parts, GetCategoryLabel(GetText(GetField<TMP_Text>(MainTagCategoryNameField))));
            AddIfNotEmpty(parts, GetTagLabel(GetText(GetField<TMP_Text>(MainTagNameField))));
            AddIfNotEmpty(parts, GetText(GetField<TMP_Text>(OtherTagsTextField)));
            return string.Join(" ", parts.ToArray());
        }

        private string GetSearchPhraseText()
        {
            GameObject phrase = GetField<GameObject>(SearchPhraseField);
            return phrase != null && phrase.activeInHierarchy
                ? GetText(GetField<TMP_Text>(SearchPhraseTextField))
                : string.Empty;
        }

        private object FindActiveOverlay()
        {
            if (SearchResultOverlayType == null)
            {
                return null;
            }

            UnityEngine.Object[] overlays = Resources.FindObjectsOfTypeAll(SearchResultOverlayType);
            for (int i = 0; i < overlays.Length; i++)
            {
                Component component = overlays[i] as Component;
                if (component != null && component.gameObject.activeInHierarchy)
                {
                    return overlays[i];
                }
            }

            return null;
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
            return GetText(text);
        }

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

        private T GetField<T>(FieldInfo field)
        {
            return field != null && _results != null ? (T)field.GetValue(_results) : default(T);
        }

        private static T GetField<T>(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return default(T);
            }

            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            object value = field != null ? field.GetValue(instance) : null;
            return value is T ? (T)value : default(T);
        }

        /// <summary>The mod's own id, which is what tells one result from another. <c>ModId</c> is a
        /// struct wrapping a long with no <c>ToString</c> of its own, so reading it as an object and
        /// printing it answered the type name for every row - which is why every result used to carry
        /// the same identity.</summary>
        private static string BuildResultId(Component component, int index)
        {
            object profile = GetField<object>(component, "profile");
            object idValue = profile != null ? GetField<object>(profile, "id") : null;
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

            TMP_Text foundText = GetField<TMP_Text>(FoundTextField);
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

                string value = GetText(text);
                if (!string.IsNullOrWhiteSpace(value) && text.fontSize >= 30f)
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private bool IsSearchResultsStatusText(TMP_Text text)
        {
            GameObject noResults = GetField<GameObject>(NoResultsField);
            GameObject endOfResults = GetField<GameObject>(EndOfResultsField);
            GameObject mainTag = GetField<GameObject>(MainTagField);
            GameObject searchPhrase = GetField<GameObject>(SearchPhraseField);
            return IsChildOf(text, noResults)
                || IsChildOf(text, endOfResults)
                || IsChildOf(text, mainTag)
                || IsChildOf(text, searchPhrase)
                || text == GetField<TMP_Text>(MainTagNameField)
                || text == GetField<TMP_Text>(MainTagCategoryNameField)
                || text == GetField<TMP_Text>(OtherTagsTextField)
                || text == GetField<TMP_Text>(SearchPhraseTextField);
        }

        private bool IsSearchResultsControlText(TMP_Text text)
        {
            TMP_Dropdown sortDropdown = GetField<TMP_Dropdown>(SortDropdownField);
            Selectable refineFilter = GetField<Selectable>(RefineFilterField);
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

        private static string GetSelectableLabel(Selectable selectable)
        {
            TMP_Text text = selectable != null ? selectable.GetComponentInChildren<TMP_Text>(false) : null;
            return GetText(text);
        }

        private static string GetButtonLabel(Button button)
        {
            TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(false) : null;
            return GetText(text);
        }

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
                AddIfNotEmpty(lines, GetText(texts[i]));
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string GetText(TMP_Text text)
        {
            return text != null && text.gameObject.activeInHierarchy
                ? StripTmpMarkup(text.text)
                : string.Empty;
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

        private static string FindTopBarText(string transformName)
        {
            if (string.IsNullOrWhiteSpace(transformName))
            {
                return string.Empty;
            }

            NavBar[] navBars = Resources.FindObjectsOfTypeAll<NavBar>();
            for (int navIndex = 0; navIndex < navBars.Length; navIndex++)
            {
                TMP_Text[] texts = navBars[navIndex] != null ? navBars[navIndex].GetComponentsInChildren<TMP_Text>(false) : null;
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

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
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
                    result.Add(StripTmpMarkup(option != null ? option.text : string.Empty));
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
                string value = GetText(label);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                return GetText(dropdown.captionText);
            }
        }

        public sealed class ResultItem
        {
            public ResultItem(int index, string id, string label, Component nativeComponent, ListItem nativeListItem)
            {
                DisplayIndex = index;
                Id = id ?? index.ToString();
                Label = label ?? string.Empty;
                NativeComponent = nativeComponent;
                NativeListItem = nativeListItem;
            }

            public int DisplayIndex { get; set; }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public Component NativeComponent { get; private set; }

            public ListItem NativeListItem { get; private set; }
        }
    }
}
