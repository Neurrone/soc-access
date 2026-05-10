using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CodexMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(CodexMenu), "_settings");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(CodexMenu), "_localizationHandler");
        private static readonly FieldInfo ShowAsyncField = AccessTools.Field(typeof(CodexMenu), "_showAsync");
        private static readonly FieldInfo ProvidersField = AccessTools.Field(typeof(CodexMenu), "_allCodexProviders");
        private static readonly FieldInfo CurrentProviderField = AccessTools.Field(typeof(CodexMenu), "_currentCodexProvider");
        private static readonly FieldInfo CategoryTabPoolField = AccessTools.Field(typeof(CodexMenu), "_categoryTabPool");
        private static readonly FieldInfo CategorySectionPoolField = AccessTools.Field(typeof(CodexMenu), "_categorySectionPool");
        private static readonly MethodInfo HideMethod = AccessTools.Method(typeof(CodexMenu), "Hide");

        private static readonly FieldInfo CategoryTabButtonField = AccessTools.Field(typeof(CodexCategoryTabButton), "_button");
        private static readonly FieldInfo CategorySectionTextField = AccessTools.Field(typeof(CodexCategorySection), "_categoryText");
        private static readonly FieldInfo ContentButtonTextField = AccessTools.Field(typeof(CodexContentButton), "_text");
        private static readonly FieldInfo ContentButtonButtonField = AccessTools.Field(typeof(CodexContentButton), "_button");
        private static readonly FieldInfo ContentButtonDefinitionField = AccessTools.Field(typeof(CodexContentButton), "_definition");

        private static readonly FieldInfo WielderNameTextField = AccessTools.Field(typeof(WielderCodexContent), "_nameText");
        private static readonly FieldInfo WielderClassTextField = AccessTools.Field(typeof(WielderCodexContent), "_classText");
        private static readonly FieldInfo WielderDescriptionTextField = AccessTools.Field(typeof(WielderCodexContent), "_descriptionText");
        private static readonly FieldInfo WielderOffenseStatTextField = AccessTools.Field(typeof(WielderCodexContent), "_offenseStatText");
        private static readonly FieldInfo WielderDefenceStatTextField = AccessTools.Field(typeof(WielderCodexContent), "_defenceStatText");
        private static readonly FieldInfo WielderMovementStatTextField = AccessTools.Field(typeof(WielderCodexContent), "_movementStatText");
        private static readonly FieldInfo WielderViewRadiusStatTextField = AccessTools.Field(typeof(WielderCodexContent), "_viewRadiusStatText");
        private static readonly FieldInfo WielderCommandStatTextField = AccessTools.Field(typeof(WielderCodexContent), "_commandStatText");
        private static readonly FieldInfo WielderStartingTroopsField = AccessTools.Field(typeof(WielderCodexContent), "_startingTroops");
        private static readonly FieldInfo WielderSkillsField = AccessTools.Field(typeof(WielderCodexContent), "_skills");
        private static readonly FieldInfo WielderSpecializationField = AccessTools.Field(typeof(WielderCodexContent), "_specialization");
        private static readonly FieldInfo WielderInfoHeaderField = AccessTools.Field(typeof(WielderCodexContentInfoSection), "_header");
        private static readonly FieldInfo WielderInfoDescriptionField = AccessTools.Field(typeof(WielderCodexContentInfoSection), "_description");
        private static readonly FieldInfo UnitInfoDescriptionField = AccessTools.Field(typeof(UnitCodexContentInfoSection), "_description");

        private static readonly FieldInfo TutorialToggleField = AccessTools.Field(typeof(CodexTutorialSettings), "_tutorialsToggle");
        private static readonly FieldInfo ResetButtonField = AccessTools.Field(typeof(CodexTutorialSettings), "_resetButton");

        private readonly CodexMenu _menu;
        private readonly ILocalizationHandler _localization;

        public CodexMenuAdapter(CodexMenu menu)
        {
            _menu = menu;
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public bool IsPresent()
        {
            return _menu != null && ShowAsyncField != null && ShowAsyncField.GetValue(_menu) != null && IsWindowActive();
        }

        public bool Close()
        {
            if (_menu == null || HideMethod == null)
            {
                return false;
            }

            HideMethod.Invoke(_menu, new object[0]);
            return true;
        }

        public IReadOnlyList<TabItem> GetTabs()
        {
            List<TabItem> items = new List<TabItem>();
            ICodexProvider[] providers = GetProviders();
            int activeIndex = GetActiveTabIndex();
            for (int i = 0; i < providers.Length; i++)
            {
                ICodexProvider provider = providers[i];
                string label = GetLocalizedText(provider != null ? provider.NameKey : null, provider != null ? provider.NameKey : "Tab " + (i + 1));
                items.Add(new TabItem("codex-tab-" + i, label, i, i == activeIndex));
            }

            return items;
        }

        public bool FocusTab(int index)
        {
            if (_menu == null)
            {
                return false;
            }

            CodexCategoryTabButton tabButton = GetActivePoolEntry(CategoryTabPoolField, index) as CodexCategoryTabButton;
            Component tabComponent = tabButton as Component;
            NativeSelectionUtility.Select(tabComponent);

            if (index == GetActiveTabIndex())
            {
                return true;
            }

            UIButton button = GetField<UIButton>(tabButton, CategoryTabButtonField);
            if (button != null)
            {
                return NativeSelectionUtility.Click(button);
            }

            return NativeSelectionUtility.PointerClick(tabComponent);
        }

        public IReadOnlyList<ArticleItem> GetArticles()
        {
            List<ArticleItem> items = new List<ArticleItem>();
            IList sections = GetActivePoolEntries(CategorySectionPoolField);
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int articleIndex = 0;
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                CodexCategorySection section = sections[sectionIndex] as CodexCategorySection;
                if (section == null || !((Component)section).gameObject.activeInHierarchy)
                {
                    continue;
                }

                string sectionLabel = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(GetField<UITextMesh>(section, CategorySectionTextField)));
                List<CodexContentButton> buttons = section.Buttons;
                bool announcedSection = false;
                for (int buttonIndex = 0; buttonIndex < buttons.Count; buttonIndex++)
                {
                    CodexContentButton button = buttons[buttonIndex];
                    if (button == null || !((Component)button).gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    UITextMesh textMesh = GetField<UITextMesh>(button, ContentButtonTextField);
                    string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    label = FormatContentButtonLabel(label, button);
                    string fullLabel = !announcedSection && !string.IsNullOrWhiteSpace(sectionLabel)
                        ? sectionLabel + ", " + label
                        : label;
                    announcedSection = true;
                    bool isSelected = selected != null && selected == ((Component)button).gameObject;
                    items.Add(new ArticleItem("codex-article-" + articleIndex, fullLabel, button, isSelected));
                    articleIndex++;
                }
            }

            return items;
        }

        public bool FocusArticle(ArticleItem item)
        {
            if (item == null || item.Button == null)
            {
                return false;
            }

            return NativeSelectionUtility.Select(item.Button as Component);
        }

        public bool ActivateArticle(ArticleItem item)
        {
            if (item == null || item.Button == null)
            {
                return false;
            }

            UIButton button = GetField<UIButton>(item.Button, ContentButtonButtonField);
            if (button != null)
            {
                return NativeSelectionUtility.Click(button);
            }

            return NativeSelectionUtility.PointerClick(item.Button as Component);
        }

        private static string FormatContentButtonLabel(string label, CodexContentButton button)
        {
            CodexCategoryContentDefinition definition = GetField<CodexCategoryContentDefinition>(button, ContentButtonDefinitionField);
            return definition != null && definition.HasContentColor
                ? ArtifactSpeechFormatter.FormatName(label, definition.ContentColor)
                : label;
        }

        public IReadOnlyList<CodexContentItem> GetContentItems()
        {
            List<CodexContentItem> items = new List<CodexContentItem>();
            Transform contentParent = GetSettingsField<Transform>("ContentParent");
            if (contentParent == null)
            {
                return items;
            }

            if (TryAddWielderContentItems(contentParent, items))
            {
                return items;
            }

            UITextMesh[] textMeshes = contentParent.GetComponentsInChildren<UITextMesh>(false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                RectTransform sourceTransform = ((Component)textMesh).GetComponent<RectTransform>();
                if (IsZeroSize(sourceTransform))
                {
                    continue;
                }

                string rawText = UITextMeshTextUtility.GetEffectiveText(textMesh);
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    continue;
                }

                CodexContentItemKind kind = items.Count == 0 || IsHeadingTextMesh(textMesh)
                    ? CodexContentItemKind.Heading
                    : CodexContentItemKind.Text;
                AddTextParts(items, kind, rawText, sourceTransform);
            }

            return items;
        }

        public void ScrollContentItemIntoView(CodexContentItem item)
        {
            if (item == null || item.SourceTransform == null)
            {
                return;
            }

            ScrollRect scrollRect = GetSettingsField<ScrollRect>("ContentParentScrollRect");
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

            Canvas.ForceUpdateCanvases();

            Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item.SourceTransform);
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

        public bool IsTutorialSettingsVisible()
        {
            GameObject settings = GetSettingsField<GameObject>("TutorialSettings");
            return settings != null && settings.activeInHierarchy;
        }

        public string TutorialsToggleLabel
        {
            get { return GetLocalizedText("Tutorial/TutorialPopup/ShowTutorialCheckbox", "Show tutorials"); }
        }

        public bool IsTutorialsChecked()
        {
            UIToggle toggle = GetTutorialToggle();
            return toggle != null && toggle.ToggleValue;
        }

        public void ToggleTutorials()
        {
            UIToggle toggle = GetTutorialToggle();
            if (toggle != null)
            {
                toggle.ToggleValue = !toggle.ToggleValue;
            }
        }

        public bool ResetTutorials()
        {
            UIButton button = GetResetButton();
            return button != null && NativeSelectionUtility.Click(button);
        }

        private bool IsWindowActive()
        {
            object window = GetSettingsField<object>("WindowTransform");
            if (window == null)
            {
                return false;
            }

            PropertyInfo activeProperty = window.GetType().GetProperty("Active");
            if (activeProperty != null && activeProperty.PropertyType == typeof(bool))
            {
                return (bool)activeProperty.GetValue(window, null);
            }

            Component component = window as Component;
            return component != null && component.gameObject.activeInHierarchy;
        }

        private ICodexProvider[] GetProviders()
        {
            return ProvidersField != null && _menu != null
                ? ProvidersField.GetValue(_menu) as ICodexProvider[] ?? new ICodexProvider[0]
                : new ICodexProvider[0];
        }

        private int GetActiveTabIndex()
        {
            ICodexProvider current = CurrentProviderField != null && _menu != null
                ? CurrentProviderField.GetValue(_menu) as ICodexProvider
                : null;
            ICodexProvider[] providers = GetProviders();
            for (int i = 0; i < providers.Length; i++)
            {
                if (object.ReferenceEquals(providers[i], current))
                {
                    return i;
                }
            }

            return 0;
        }

        private UIToggle GetTutorialToggle()
        {
            CodexTutorialSettings settings = GetTutorialSettings();
            return GetField<UIToggle>(settings, TutorialToggleField);
        }

        private UIButton GetResetButton()
        {
            CodexTutorialSettings settings = GetTutorialSettings();
            return GetField<UIButton>(settings, ResetButtonField);
        }

        private CodexTutorialSettings GetTutorialSettings()
        {
            GameObject settings = GetSettingsField<GameObject>("TutorialSettings");
            return settings != null ? settings.GetComponent<CodexTutorialSettings>() : null;
        }

        private T GetSettingsField<T>(string fieldName) where T : class
        {
            object settings = SettingsField != null && _menu != null ? SettingsField.GetValue(_menu) : null;
            if (settings == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(settings.GetType(), fieldName);
            return field != null ? field.GetValue(settings) as T : null;
        }

        private IList GetActivePoolEntries(FieldInfo poolField)
        {
            object pool = poolField != null && _menu != null ? poolField.GetValue(_menu) : null;
            if (pool == null)
            {
                return new object[0];
            }

            PropertyInfo activeEntriesProperty = pool.GetType().GetProperty("ActiveEntries");
            IList entries = activeEntriesProperty != null ? activeEntriesProperty.GetValue(pool, null) as IList : null;
            return entries ?? new object[0];
        }

        private object GetActivePoolEntry(FieldInfo poolField, int index)
        {
            IList entries = GetActivePoolEntries(poolField);
            return index >= 0 && index < entries.Count ? entries[index] : null;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            if (_localization == null || string.IsNullOrWhiteSpace(key))
            {
                return SpeechTextSanitizer.Normalize(fallback);
            }

            string text = _localization.GetText(key);
            return SpeechTextSanitizer.Normalize(string.IsNullOrWhiteSpace(text) || text == key ? fallback : text);
        }

        private static bool IsHeadingTextMesh(UITextMesh textMesh)
        {
            if (textMesh == null)
            {
                return false;
            }

            Transform current = ((Component)textMesh).transform;
            while (current != null)
            {
                Component[] components = current.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null || component is UITextMesh)
                    {
                        continue;
                    }

                    if (component is UnitCodexContentInfoSection
                        && FieldReferencesText(component, "_header", textMesh)
                        && string.IsNullOrWhiteSpace(GetVisibleText(GetField<UITextMesh>(component, UnitInfoDescriptionField))))
                    {
                        return false;
                    }

                    if (FieldReferencesText(component, "_header", textMesh)
                        || FieldReferencesText(component, "_nameText", textMesh))
                    {
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        private bool TryAddWielderContentItems(Transform contentParent, List<CodexContentItem> items)
        {
            WielderCodexContent content = contentParent.GetComponentInChildren<WielderCodexContent>(false);
            if (content == null)
            {
                return false;
            }

            AddTextMeshItems(items, CodexContentItemKind.Heading, GetField<UITextMesh>(content, WielderNameTextField));
            AddTextMeshItems(items, CodexContentItemKind.Text, GetField<UITextMesh>(content, WielderClassTextField));
            AddTextMeshItems(items, CodexContentItemKind.Text, GetField<UITextMesh>(content, WielderDescriptionTextField));
            AddStatItem(items, "Commanders/Tooltip/Offense", "Offence", GetField<UITextMesh>(content, WielderOffenseStatTextField));
            AddStatItem(items, "Commanders/Tooltip/Defense", "Defence", GetField<UITextMesh>(content, WielderDefenceStatTextField));
            AddStatItem(items, "Commanders/Tooltip/Movement", "Movement", GetField<UITextMesh>(content, WielderMovementStatTextField));
            AddStatItem(items, "Commanders/Tooltip/ViewRadius", "View radius", GetField<UITextMesh>(content, WielderViewRadiusStatTextField));
            AddStatItem(items, "Commanders/Tooltip/Command", "Command", GetField<UITextMesh>(content, WielderCommandStatTextField));
            AddWielderInfoSection(items, GetField<WielderCodexContentInfoSection>(content, WielderStartingTroopsField));
            AddWielderInfoSection(items, GetField<WielderCodexContentInfoSection>(content, WielderSkillsField));
            AddWielderInfoSection(items, GetField<WielderCodexContentInfoSection>(content, WielderSpecializationField));
            return true;
        }

        private void AddStatItem(List<CodexContentItem> items, string labelKey, string fallbackLabel, UITextMesh valueText)
        {
            string value = GetVisibleText(valueText);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string label = GetLocalizedText(labelKey, fallbackLabel);
            RectTransform sourceTransform = ((Component)valueText).GetComponent<RectTransform>();
            items.Add(new CodexContentItem(CodexContentItemKind.Text, label + ": " + value, sourceTransform));
        }

        private static void AddWielderInfoSection(List<CodexContentItem> items, WielderCodexContentInfoSection section)
        {
            if (section == null || !((Component)section).gameObject.activeInHierarchy)
            {
                return;
            }

            AddTextMeshItems(items, CodexContentItemKind.Heading, GetField<UITextMesh>(section, WielderInfoHeaderField));
            AddTextMeshItems(items, CodexContentItemKind.Text, GetField<UITextMesh>(section, WielderInfoDescriptionField));
        }

        private static void AddTextMeshItems(List<CodexContentItem> items, CodexContentItemKind kind, UITextMesh textMesh)
        {
            string text = GetVisibleText(textMesh);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            RectTransform sourceTransform = ((Component)textMesh).GetComponent<RectTransform>();
            AddTextParts(items, kind, text, sourceTransform);
        }

        private static string GetVisibleText(UITextMesh textMesh)
        {
            if (textMesh == null)
            {
                return string.Empty;
            }

            Component component = textMesh as Component;
            RectTransform rect = component != null ? component.GetComponent<RectTransform>() : null;
            if (component == null || !component.gameObject.activeInHierarchy || IsZeroSize(rect))
            {
                return string.Empty;
            }

            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }

        private static void AddTextParts(
            List<CodexContentItem> items,
            CodexContentItemKind kind,
            string rawText,
            RectTransform sourceTransform)
        {
            string[] parts = rawText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                string text = SpeechTextSanitizer.Normalize(parts[i]);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                items.Add(new CodexContentItem(kind, text, sourceTransform));
                if (kind == CodexContentItemKind.Heading)
                {
                    kind = CodexContentItemKind.Text;
                }
            }
        }

        private static bool FieldReferencesText(object owner, string fieldName, UITextMesh textMesh)
        {
            FieldInfo field = owner != null
                ? owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                : null;
            return field != null && object.ReferenceEquals(field.GetValue(owner), textMesh);
        }

        private static bool IsZeroSize(RectTransform rect)
        {
            return rect != null && (rect.rect.width <= 0.1f || rect.rect.height <= 0.1f);
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class TabItem
        {
            public TabItem(string id, string label, int index, bool isActive)
            {
                Id = id;
                Label = label;
                Index = index;
                IsActive = isActive;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public int Index { get; private set; }
            public bool IsActive { get; private set; }
        }

        internal sealed class ArticleItem
        {
            public ArticleItem(string id, string label, CodexContentButton button, bool isSelected)
            {
                Id = id;
                Label = label;
                Button = button;
                IsSelected = isSelected;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public CodexContentButton Button { get; private set; }
            public bool IsSelected { get; private set; }
        }
    }
}
