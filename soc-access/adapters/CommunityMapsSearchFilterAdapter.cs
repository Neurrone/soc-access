using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquestAccess.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CommunityMapsSearchFilterAdapter
    {
        private static readonly Type SearchPanelType = AccessTools.TypeByName("ModIOBrowser.Implementation.SearchPanel");
        private static readonly FieldInfo PanelField = AccessTools.Field(SearchPanelType, "SearchPanelGameObject");
        private static readonly FieldInfo SearchFieldInfo = AccessTools.Field(SearchPanelType, "SearchPanelField");
        private static readonly FieldInfo TagsField = AccessTools.Field(SearchPanelType, "tags");
        private static readonly FieldInfo SelectedTagsField = AccessTools.Field(SearchPanelType, "searchFilterTags");
        private static readonly MethodInfo ApplyFilterMethod = AccessTools.Method(SearchPanelType, "ApplyFilter");
        private static readonly MethodInfo ClearFilterMethod = AccessTools.Method(SearchPanelType, "ClearFilter");
        private static readonly MethodInfo CloseMethod = AccessTools.Method(SearchPanelType, "Close");

        private readonly object _searchPanel;
        private readonly string _title;

        private CommunityMapsSearchFilterAdapter(object searchPanel)
        {
            _searchPanel = searchPanel;
            _title = FindTopBarText("Search & filter");
        }

        public static CommunityMapsSearchFilterAdapter TryCreate()
        {
            if (SearchPanelType == null)
            {
                return null;
            }

            UnityEngine.Object[] panels = Resources.FindObjectsOfTypeAll(SearchPanelType);
            for (int i = 0; i < panels.Length; i++)
            {
                CommunityMapsSearchFilterAdapter adapter = new CommunityMapsSearchFilterAdapter(panels[i]);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public bool IsPresent()
        {
            GameObject panel = Panel;
            return Browser.IsOpen && panel != null && panel.activeInHierarchy;
        }

        public string Title
        {
            get { return _title; }
        }

        public TMP_InputField SearchField
        {
            get { return SearchFieldInfo != null ? SearchFieldInfo.GetValue(_searchPanel) as TMP_InputField : null; }
        }

        public string SearchFieldLabel
        {
            get
            {
                TMP_InputField field = SearchField;
                TMP_Text placeholder = field != null ? field.placeholder as TMP_Text : null;
                return GetText(placeholder);
            }
        }

        public IReadOnlyList<CategoryItem> GetCategories()
        {
            List<CategoryItem> result = new List<CategoryItem>();
            Array categories = TagsField != null ? TagsField.GetValue(_searchPanel) as Array : null;
            if (categories == null)
            {
                return result;
            }

            for (int i = 0; i < categories.Length; i++)
            {
                object category = categories.GetValue(i);
                if (category == null || GetField<bool>(category, "hidden"))
                {
                    continue;
                }

                string categoryName = GetField<string>(category, "name");
                Array tags = GetField<Array>(category, "tags");
                if (string.IsNullOrWhiteSpace(categoryName) || tags == null || tags.Length == 0)
                {
                    continue;
                }

                List<TagItem> tagItems = new List<TagItem>();
                for (int tagIndex = 0; tagIndex < tags.Length; tagIndex++)
                {
                    object tag = tags.GetValue(tagIndex);
                    string tagName = GetField<string>(tag, "name");
                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        tagItems.Add(new TagItem(this, categoryName, tagName, GetTagLabel(tagName), tagIndex));
                    }
                }

                if (tagItems.Count > 0)
                {
                    result.Add(new CategoryItem(i, GetCategoryLabel(categoryName), tagItems));
                }
            }

            return result;
        }

        public IReadOnlyList<ActionItem> GetActions()
        {
            List<ActionItem> actions = new List<ActionItem>();
            List<Button> buttons = FindActionButtons();
            AddAction(actions, "search", ApplyFilterMethod, buttons, 0);
            AddAction(actions, "clear", ClearFilterMethod, buttons, 1);
            AddAction(actions, "cancel", CloseMethod, buttons, 2);
            return actions;
        }

        public bool ApplyFilter()
        {
            return Invoke(ApplyFilterMethod);
        }

        public bool ClearFilter()
        {
            return Invoke(ClearFilterMethod);
        }

        public bool Close()
        {
            return Invoke(CloseMethod);
        }

        private GameObject Panel
        {
            get { return PanelField != null ? PanelField.GetValue(_searchPanel) as GameObject : null; }
        }

        private void AddAction(
            List<ActionItem> actions,
            string id,
            MethodInfo method,
            IReadOnlyList<Button> buttons,
            int spatialIndex)
        {
            if (method == null)
            {
                return;
            }

            Button button = FindButtonInvoking(method.Name);
            if (button == null && buttons != null && spatialIndex >= 0 && spatialIndex < buttons.Count)
            {
                button = buttons[spatialIndex];
            }

            string label = GetButtonLabel(button);
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            actions.Add(new ActionItem(actions.Count, id, label, button, () => Invoke(method)));
        }

        private Button FindButtonInvoking(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            GameObject panel = Panel;
            Button[] buttons = panel != null ? panel.GetComponentsInChildren<Button>(false) : null;
            if (buttons == null)
            {
                return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                UnityEvent onClick = button.onClick;
                int count = onClick != null ? onClick.GetPersistentEventCount() : 0;
                for (int eventIndex = 0; eventIndex < count; eventIndex++)
                {
                    if (onClick.GetPersistentMethodName(eventIndex) == methodName)
                    {
                        return button;
                    }
                }
            }

            return null;
        }

        private List<Button> FindActionButtons()
        {
            List<Button> result = new List<Button>();
            GameObject panel = Panel;
            Button[] buttons = panel != null ? panel.GetComponentsInChildren<Button>(false) : null;
            if (buttons == null)
            {
                return result;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null
                    && button.gameObject.activeInHierarchy
                    && !string.IsNullOrWhiteSpace(GetButtonLabel(button)))
                {
                    result.Add(button);
                }
            }

            result.Sort(CompareButtonPosition);
            if (result.Count > 3)
            {
                result.RemoveRange(3, result.Count - 3);
            }

            result.Sort(CompareButtonX);
            return result;
        }

        private static int CompareButtonPosition(Button a, Button b)
        {
            Vector3 aPosition = a != null ? a.transform.position : Vector3.zero;
            Vector3 bPosition = b != null ? b.transform.position : Vector3.zero;
            int y = aPosition.y.CompareTo(bPosition.y);
            return y != 0 ? y : aPosition.x.CompareTo(bPosition.x);
        }

        private static int CompareButtonX(Button a, Button b)
        {
            Vector3 aPosition = a != null ? a.transform.position : Vector3.zero;
            Vector3 bPosition = b != null ? b.transform.position : Vector3.zero;
            return aPosition.x.CompareTo(bPosition.x);
        }

        private bool Invoke(MethodInfo method)
        {
            if (method == null || _searchPanel == null)
            {
                return false;
            }

            method.Invoke(_searchPanel, null);
            return true;
        }

        private bool IsTagSelected(string category, string name)
        {
            IEnumerable selectedTags = SelectedTagsField != null ? SelectedTagsField.GetValue(null) as IEnumerable : null;
            if (selectedTags == null)
            {
                return false;
            }

            foreach (object tag in selectedTags)
            {
                if (GetField<string>(tag, "category") == category && GetField<string>(tag, "name") == name)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ToggleTag(string category, string name)
        {
            TagListItem item = FindNativeTag(category, name);
            Toggle toggle = item != null ? item.GetComponentInChildren<Toggle>(false) : null;
            if (toggle == null || !toggle.gameObject.activeInHierarchy || !toggle.interactable)
            {
                return false;
            }

            toggle.isOn = !toggle.isOn;
            return true;
        }

        private void FocusTag(string category, string name)
        {
            TagListItem item = FindNativeTag(category, name);
            Toggle toggle = item != null ? item.GetComponentInChildren<Toggle>(false) : null;
            if (toggle != null)
            {
                NativeSelectionUtility.Select(toggle);
            }
            else if (item != null)
            {
                NativeSelectionUtility.Select(item);
            }
        }

        private static TagListItem FindNativeTag(string category, string name)
        {
            TagListItem[] items = Resources.FindObjectsOfTypeAll<TagListItem>();
            for (int i = 0; i < items.Length; i++)
            {
                TagListItem item = items[i];
                if (item != null
                    && item.gameObject.activeInHierarchy
                    && item.tagCategory == category
                    && item.tagName == name)
                {
                    return item;
                }
            }

            return null;
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

        private static string GetButtonLabel(Button button)
        {
            TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(false) : null;
            return GetText(text);
        }

        private static string GetText(TMP_Text text)
        {
            return text != null && text.gameObject.activeInHierarchy
                ? StripTmpMarkup(text.text)
                : string.Empty;
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

        internal sealed class CategoryItem
        {
            public CategoryItem(int index, string label, IReadOnlyList<TagItem> tags)
            {
                Index = index;
                Label = label ?? string.Empty;
                Tags = tags ?? new TagItem[0];
            }

            public int Index { get; private set; }

            public string Label { get; private set; }

            public IReadOnlyList<TagItem> Tags { get; private set; }
        }

        internal sealed class TagItem
        {
            private readonly CommunityMapsSearchFilterAdapter _adapter;
            private readonly string _category;
            private readonly string _name;
            private readonly string _label;

            public TagItem(CommunityMapsSearchFilterAdapter adapter, string category, string name, string label, int index)
            {
                _adapter = adapter;
                _category = category ?? string.Empty;
                _name = name ?? string.Empty;
                _label = label ?? string.Empty;
                Index = index;
            }

            public int Index { get; private set; }

            public string Label
            {
                get { return _label; }
            }

            public bool IsSelected
            {
                get { return _adapter != null && _adapter.IsTagSelected(_category, _name); }
            }

            public bool Toggle()
            {
                return _adapter != null && _adapter.ToggleTag(_category, _name);
            }

            public void Focus()
            {
                _adapter?.FocusTag(_category, _name);
            }
        }

        internal sealed class ActionItem
        {
            private readonly Func<bool> _activate;

            public ActionItem(int index, string id, string label, Button button, Func<bool> activate)
            {
                Index = index;
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Button = button;
                _activate = activate;
            }

            public int Index { get; private set; }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public Button Button { get; private set; }

            public bool IsEnabled
            {
                get { return Button == null || Button.interactable; }
            }

            public void Focus()
            {
                if (Button != null)
                {
                    NativeSelectionUtility.Select(Button);
                }
            }

            public bool Activate()
            {
                return _activate != null && _activate();
            }
        }
    }
}
