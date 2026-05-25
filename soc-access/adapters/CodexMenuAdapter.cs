using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Unit;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
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
        private static readonly FieldInfo UnitContentSectionHeaderField = AccessTools.Field(typeof(UnitCodexContentSection), "_header");
        private static readonly FieldInfo UnitContentSectionDescriptionField = AccessTools.Field(typeof(UnitCodexContentSection), "_description");
        private static readonly FieldInfo UnitContentSectionEssenceControllerField = AccessTools.Field(typeof(UnitCodexContentSection), "_essenceController");
        private static readonly FieldInfo UnitInfoHeaderField = AccessTools.Field(typeof(UnitCodexContentInfoSection), "_header");
        private static readonly FieldInfo UnitInfoDescriptionField = AccessTools.Field(typeof(UnitCodexContentInfoSection), "_description");

        private static readonly FieldInfo TutorialToggleField = AccessTools.Field(typeof(CodexTutorialSettings), "_tutorialsToggle");
        private static readonly FieldInfo ResetButtonField = AccessTools.Field(typeof(CodexTutorialSettings), "_resetButton");

        private readonly CodexMenu _menu;
        private readonly ILocalizationHandler _localization;
        private int _focusedCategoryIndex = -1;

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
                items.Add(new TabItem(label, i, i == activeIndex));
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

        public int FocusedCategoryIndex
        {
            get { return _focusedCategoryIndex; }
        }

        public void FocusCategory(int index)
        {
            IReadOnlyList<ArticleGroupItem> groups = GetArticleGroups();
            if (groups.Count == 0)
            {
                _focusedCategoryIndex = -1;
                return;
            }

            if (index < 0)
            {
                index = 0;
            }
            else if (index >= groups.Count)
            {
                index = groups.Count - 1;
            }

            _focusedCategoryIndex = index;
        }

        public void EnsureFocusedCategory()
        {
            IReadOnlyList<ArticleGroupItem> groups = GetArticleGroups();
            if (groups.Count == 0)
            {
                _focusedCategoryIndex = -1;
                return;
            }

            if (_focusedCategoryIndex >= 0 && _focusedCategoryIndex < groups.Count)
            {
                return;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].ContainsSelectedArticle)
                {
                    _focusedCategoryIndex = i;
                    return;
                }
            }

            _focusedCategoryIndex = 0;
        }

        public IReadOnlyList<ArticleGroupItem> GetArticleGroups()
        {
            List<ArticleGroupItem> groups = new List<ArticleGroupItem>();
            IList sections = GetActivePoolEntries(CategorySectionPoolField);
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                CodexCategorySection section = sections[sectionIndex] as CodexCategorySection;
                if (section == null || !((Component)section).gameObject.activeInHierarchy)
                {
                    continue;
                }

                string sectionLabel = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(GetField<UITextMesh>(section, CategorySectionTextField)));
                List<CodexContentButton> buttons = section.Buttons;
                List<ArticleItem> articles = new List<ArticleItem>();
                bool containsSelectedArticle = false;
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
                    bool isSelected = selected != null && selected == ((Component)button).gameObject;
                    containsSelectedArticle = containsSelectedArticle || isSelected;
                    articles.Add(new ArticleItem(label, button, isSelected, groups.Count, articles.Count));
                }

                if (articles.Count > 0)
                {
                    groups.Add(new ArticleGroupItem(
                        string.IsNullOrWhiteSpace(sectionLabel) ? "Category " + (groups.Count + 1) : sectionLabel,
                        groups.Count,
                        articles,
                        containsSelectedArticle));
                }
            }

            return groups;
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

        private string FormatContentButtonLabel(string label, CodexContentButton button)
        {
            CodexCategoryContentDefinition definition = GetField<CodexCategoryContentDefinition>(button, ContentButtonDefinitionField);
            return definition != null && definition.HasContentColor
                ? ArtifactSpeechFormatter.FormatName(_localization, label, definition.ContentColor)
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

            if (TryAddUnitContentItems(contentParent, items))
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

        private string GetLocalizedText(string key, string fallback, params object[] parameters)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback, parameters));
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

        private bool TryAddUnitContentItems(Transform contentParent, List<CodexContentItem> items)
        {
            UnitCodexContent content = contentParent.GetComponentInChildren<UnitCodexContent>(false);
            if (content == null)
            {
                return false;
            }

            UnitCodexContentSection[] sections = content.GetComponentsInChildren<UnitCodexContentSection>(false);
            if (sections == null || sections.Length == 0)
            {
                return false;
            }

            IReadOnlyList<IUnitDefinition> definitions = GetCurrentUnitDefinitions();
            for (int i = 0; i < sections.Length; i++)
            {
                UnitCodexContentSection section = sections[i];
                if (section == null || !((Component)section).gameObject.activeInHierarchy)
                {
                    continue;
                }

                AddTextMeshItems(items, CodexContentItemKind.Heading, GetField<UITextMesh>(section, UnitContentSectionHeaderField));
                if (i < definitions.Count)
                {
                    AddUnitEssenceItem(items, section, definitions[i]);
                }

                AddTextMeshItems(items, CodexContentItemKind.Text, GetField<UITextMesh>(section, UnitContentSectionDescriptionField));

                UnitCodexContentInfoSection[] infoSections = section.GetComponentsInChildren<UnitCodexContentInfoSection>(false);
                for (int j = 0; j < infoSections.Length; j++)
                {
                    AddUnitInfoSectionItems(items, infoSections[j]);
                }
            }

            return items.Count > 0;
        }

        private IReadOnlyList<IUnitDefinition> GetCurrentUnitDefinitions()
        {
            List<IUnitDefinition> definitions = new List<IUnitDefinition>();
            CodexCategoryContentDefinition selectedDefinition = GetSelectedArticleDefinition();
            if (selectedDefinition == null || !(selectedDefinition.UniqueIdentifier is TroopReference))
            {
                return definitions;
            }

            IFactionLookup factionLookup = GetCurrentProviderField<IFactionLookup>("_factionLookup");
            if (factionLookup == null)
            {
                return definitions;
            }

            TroopReference selectedTroop = (TroopReference)selectedDefinition.UniqueIdentifier;
            TroopUpgradeType[] upgradeTypes = (TroopUpgradeType[])Enum.GetValues(typeof(TroopUpgradeType));
            for (int i = 0; i < upgradeTypes.Length; i++)
            {
                TroopReference troop = new TroopReference(selectedTroop.FactionIndex, selectedTroop.UnitIndex, upgradeTypes[i]);
                bool isFallback;
                IUnitDefinition unit = factionLookup.GetUnit(troop, out isFallback);
                if (unit != null && !isFallback)
                {
                    definitions.Add(unit);
                }
            }

            return definitions;
        }

        private CodexCategoryContentDefinition GetSelectedArticleDefinition()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            CodexContentButton button = selected != null ? selected.GetComponent<CodexContentButton>() : null;
            return GetField<CodexCategoryContentDefinition>(button, ContentButtonDefinitionField);
        }

        private void AddUnitEssenceItem(List<CodexContentItem> items, UnitCodexContentSection section, IUnitDefinition definition)
        {
            if (definition == null || definition.Stats == null || definition.Stats.Essences == null)
            {
                return;
            }

            List<ValueTuple<EssenceType, int>> allEssences = definition.Stats.Essences.GetAllEssences();
            if (allEssences == null || allEssences.Count == 0)
            {
                return;
            }

            TroopViewEssenceController essenceController = GetField<TroopViewEssenceController>(section, UnitContentSectionEssenceControllerField);
            RectTransform sourceTransform = essenceController != null
                ? ((Component)essenceController).GetComponent<RectTransform>()
                : ((Component)section).GetComponent<RectTransform>();
            List<EssenceAmount> amounts = new List<EssenceAmount>();
            for (int i = 0; i < allEssences.Count; i++)
            {
                amounts.Add(new EssenceAmount(GetEssenceAmountText(allEssences[i].Item1, allEssences[i].Item2)));
            }

            items.Add(new CodexContentItem(GetLocalizedText("Units/Types/EssenceIntro", "Essence"), amounts, sourceTransform));
        }

        private static void AddUnitInfoSectionItems(List<CodexContentItem> items, UnitCodexContentInfoSection section)
        {
            if (section == null || !((Component)section).gameObject.activeInHierarchy)
            {
                return;
            }

            UITextMesh header = GetField<UITextMesh>(section, UnitInfoHeaderField);
            UITextMesh description = GetField<UITextMesh>(section, UnitInfoDescriptionField);
            string descriptionText = GetVisibleText(description);
            AddTextMeshItems(
                items,
                string.IsNullOrWhiteSpace(descriptionText) ? CodexContentItemKind.Text : CodexContentItemKind.Heading,
                header);
            AddTextMeshItems(items, CodexContentItemKind.Text, description);
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

        private string GetEssenceName(EssenceType essenceType)
        {
            return GetLocalizedText("Units/Types/" + essenceType, FormatEnumName(essenceType.ToString()));
        }

        private string GetEssenceAmountText(EssenceType essenceType, int count)
        {
            if (count <= 1)
            {
                return GetEssenceName(essenceType);
            }

            string essenceName = GetEssenceName(essenceType);
            return GetLocalizedText("Units/Types/" + essenceType + "Multiple", count + " " + essenceName, count);
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c))
                {
                    chars.Add(' ');
                }

                chars.Add(c);
            }

            return new string(chars.ToArray());
        }

        private T GetCurrentProviderField<T>(string fieldName) where T : class
        {
            ICodexProvider provider = CurrentProviderField != null && _menu != null
                ? CurrentProviderField.GetValue(_menu) as ICodexProvider
                : null;
            FieldInfo field = provider != null ? AccessTools.Field(provider.GetType(), fieldName) : null;
            return field != null ? field.GetValue(provider) as T : null;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class TabItem
        {
            public TabItem(string label, int index, bool isActive)
            {
                Label = label;
                Index = index;
                IsActive = isActive;
            }

            public string Label { get; private set; }
            public int Index { get; private set; }
            public bool IsActive { get; private set; }
        }

        internal sealed class ArticleGroupItem
        {
            public ArticleGroupItem(string label, int index, IReadOnlyList<ArticleItem> articles, bool containsSelectedArticle)
            {
                Label = label ?? string.Empty;
                Index = index;
                Articles = articles ?? new ArticleItem[0];
                ContainsSelectedArticle = containsSelectedArticle;
            }

            public string Label { get; private set; }
            public int Index { get; private set; }
            public IReadOnlyList<ArticleItem> Articles { get; private set; }
            public bool ContainsSelectedArticle { get; private set; }
        }

        internal sealed class ArticleItem
        {
            public ArticleItem(string label, CodexContentButton button, bool isSelected, int categoryIndex, int articleIndex)
            {
                Label = label;
                Button = button;
                IsSelected = isSelected;
                CategoryIndex = categoryIndex;
                ArticleIndex = articleIndex;
            }

            public string Label { get; private set; }
            public CodexContentButton Button { get; private set; }
            public bool IsSelected { get; private set; }
            public int CategoryIndex { get; private set; }
            public int ArticleIndex { get; private set; }
        }

        internal enum CodexContentItemKind
        {
            Heading,
            Text,
            Essence
        }

        internal sealed class EssenceAmount
        {
            public EssenceAmount(string text)
            {
                Text = text ?? string.Empty;
            }

            public string Text { get; private set; }
        }

        internal sealed class CodexContentItem
        {
            public CodexContentItem(CodexContentItemKind kind, string text, RectTransform sourceTransform = null)
            {
                Kind = kind;
                Text = text ?? string.Empty;
                SourceTransform = sourceTransform;
                Essences = new EssenceAmount[0];
            }

            public CodexContentItem(string essenceLabel, IReadOnlyList<EssenceAmount> essences, RectTransform sourceTransform = null)
            {
                Kind = CodexContentItemKind.Essence;
                Text = essenceLabel ?? string.Empty;
                SourceTransform = sourceTransform;
                Essences = essences ?? new EssenceAmount[0];
            }

            public CodexContentItemKind Kind { get; private set; }
            public string Text { get; private set; }
            public RectTransform SourceTransform { get; private set; }
            public IReadOnlyList<EssenceAmount> Essences { get; private set; }
        }
    }
}
