using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommunityMapsSearchFilterAdapter : IPresent
    {
        private static readonly Type SearchPanelType = AccessTools.TypeByName("ModIOBrowser.Implementation.SearchPanel");
        private static readonly FieldInfo PanelField = AccessTools.Field(SearchPanelType, "SearchPanelGameObject");
        private static readonly FieldInfo SearchFieldInfo = AccessTools.Field(SearchPanelType, "SearchPanelField");
        private static readonly FieldInfo TagsField = AccessTools.Field(SearchPanelType, "tags");
        private static readonly FieldInfo SelectedTagsField = AccessTools.Field(SearchPanelType, "searchFilterTags");
        private static readonly MethodInfo ApplyFilterMethod = AccessTools.Method(SearchPanelType, "ApplyFilter");
        private static readonly MethodInfo ClearFilterMethod = AccessTools.Method(SearchPanelType, "ClearFilter");
        private static readonly MethodInfo CloseMethod = AccessTools.Method(SearchPanelType, "Close");
        private static readonly FieldInfo TagParentField = AccessTools.Field(SearchPanelType, "SearchPanelTagParent");

        private readonly object _searchPanel;

        // mod.io's own word for this panel, read once rather than per frame. The browser is
        // instantiated once for the session and only hidden when it closes, so this adapter outlives
        // an options visit; mod.io re-translates its own UI when the language changes under an open
        // browser, so the title is read again when it does (SyncLabelLanguage).
        private ILanguageDefinition _labelLanguage;
        private string _title;

        public CommunityMapsSearchFilterAdapter(object searchPanel)
        {
            _searchPanel = searchPanel;
            SyncLabelLanguage();
        }

        /// <summary>Read the title above again where the game has changed language since. Asked from
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
            _title = CommunityMapsText.FindTopBar("Search & filter");
        }

        public bool IsPresent()
        {
            SyncLabelLanguage();
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
                return CommunityMapsText.Of(placeholder);
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
                if (category == null || Reflect.Typed<bool>(category, "hidden"))
                {
                    continue;
                }

                string categoryName = Reflect.Typed<string>(category, "name");
                Array tags = Reflect.Typed<Array>(category, "tags");
                if (string.IsNullOrWhiteSpace(categoryName) || tags == null || tags.Length == 0)
                {
                    continue;
                }

                List<TagItem> tagItems = new List<TagItem>();
                for (int tagIndex = 0; tagIndex < tags.Length; tagIndex++)
                {
                    object tag = tags.GetValue(tagIndex);
                    string tagName = Reflect.Typed<string>(tag, "name");
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

        /// <summary>The panel's three commands, listed at most once a frame. The walk for the panel's
        /// buttons is a frame sweep, so naming the three of them costs one walk and not four
        /// (AGENTS.md, Performance).</summary>
        public IReadOnlyList<ActionItem> GetActions()
        {
            int frame = Time.frameCount;
            if (_actions != null && _actionsFrame == frame)
            {
                return _actions;
            }

            _actionsFrame = frame;
            List<ActionItem> actions = new List<ActionItem>();
            List<Button> buttons = FindActionButtons();
            AddAction(actions, "search", ApplyFilterMethod, buttons, 0);
            AddAction(actions, "clear", ClearFilterMethod, buttons, 1);
            AddAction(actions, "cancel", CloseMethod, buttons, 2);
            _actions = actions;
            return _actions;
        }

        private IReadOnlyList<ActionItem> _actions;
        private int _actionsFrame = -1;

        // The panel's own buttons, walked once a frame however many of them are named.
        private static readonly FrameSweep<Button> PanelButtons =
            new FrameSweep<Button>("community maps filter panel", inactiveToo: false);

        private Button[] ButtonsOfPanel()
        {
            GameObject panel = Panel;
            return panel != null ? PanelButtons.Under(panel.transform) : null;
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
            string key,
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

            actions.Add(new ActionItem(actions.Count, key, label, button, () => Invoke(method)));
        }

        private Button FindButtonInvoking(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            Button[] buttons = ButtonsOfPanel();
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
            Button[] buttons = ButtonsOfPanel();
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

        /// <summary>Whether the panel is filtering on this tag. The chosen tags are gathered once a
        /// frame: every drawn chip asks, and the answer was a walk of the whole chosen set each time
        /// (AGENTS.md, Performance).</summary>
        private bool IsTagSelected(string category, string name)
        {
            int frame = Time.frameCount;
            if (_selectedTags == null || _selectedTagsFrame != frame)
            {
                _selectedTagsFrame = frame;
                _selectedTags = ReadSelectedTags();
            }

            return _selectedTags.Contains(TagKey(category, name));
        }

        private HashSet<string> _selectedTags;
        private int _selectedTagsFrame = -1;

        private static HashSet<string> ReadSelectedTags()
        {
            HashSet<string> keys = new HashSet<string>();
            IEnumerable selectedTags = SelectedTagsField != null ? SelectedTagsField.GetValue(null) as IEnumerable : null;
            if (selectedTags == null)
            {
                return keys;
            }

            foreach (object tag in selectedTags)
            {
                keys.Add(TagKey(Reflect.Typed<string>(tag, "category"), Reflect.Typed<string>(tag, "name")));
            }

            return keys;
        }

        private static string TagKey(string category, string name)
        {
            return (category ?? string.Empty) + "\u0001" + (name ?? string.Empty);
        }

        // LAZY: a click. The walk under the tag chip is paid when the player toggles it.
        private bool ToggleTag(string category, string name)
        {
            TagListItem item = FindNativeTag(category, name);
            Toggle toggle = item != null ? item.GetComponentInChildren<Toggle>(false) : null;
            if (toggle == null || !toggle.gameObject.activeInHierarchy || !toggle.interactable)
            {
                return false;
            }

            toggle.isOn = !toggle.isOn;

            // mod.io changes its chosen set in the toggle's handler, within this frame, and the
            // state is read back for the announcement before the frame ends: the once-a-frame
            // gathering above would answer with the set as it stood before the press.
            _selectedTags = null;
            return true;
        }

        // LAZY: a focus move. The walk under the tag chip is paid when the cursor arrives on it.
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

        // The chips the panel draws, which the game pools under SearchPanelTagParent and nowhere else
        // (decompiled SearchPanel.CreateTagListItems). Walking that subtree is what replaced a
        // scene-wide scan on the focus path, which measured about 19 ms per cursor move.
        private static readonly FrameSweep<TagListItem> TagChips =
            new FrameSweep<TagListItem>("community maps filter tags", inactiveToo: true);

        private TagListItem FindNativeTag(string category, string name)
        {
            Transform parent = Reflect.Get<Transform>(_searchPanel, TagParentField);
            TagListItem[] items = parent != null ? TagChips.Under(parent) : new TagListItem[0];
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

        // FindActionButtons walks the panel for its buttons and then asked each of them for its
        // label, so the filter panel paid one subtree walk per button per build. Keyed on the
        // frame, the walk under a button is paid once however often the label is asked for.
        private static readonly FrameSweep<TMP_Text> ButtonTexts =
            new FrameSweep<TMP_Text>("community maps filter button", inactiveToo: false);

        private static string GetButtonLabel(Button button)
        {
            TMP_Text[] texts = button != null ? ButtonTexts.Under(button.transform) : null;
            return CommunityMapsText.Of(texts != null && texts.Length > 0 ? texts[0] : null);
        }

        public sealed class CategoryItem
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

        public sealed class TagItem
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

        public sealed class ActionItem
        {
            private readonly Func<bool> _activate;

            public ActionItem(int index, string key, string label, Button button, Func<bool> activate)
            {
                Index = index;
                Key = key ?? string.Empty;
                Label = label ?? string.Empty;
                Button = button;
                _activate = activate;
            }

            public int Index { get; private set; }

            public string Key { get; private set; }

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
