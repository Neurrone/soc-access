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
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CodexMenuAdapter : IPresent
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

        public CodexMenuAdapter(CodexMenu menu)
        {
            _menu = menu;
            _localization = Reflect.Get<ILocalizationHandler>(menu, LocalizationField);
        }

        public bool IsPresent()
        {
            return _menu != null && ShowAsyncField != null && ShowAsyncField.GetValue(_menu) != null && IsWindowActive();
        }

        /// <summary>The window's own heading, drawn above the showing tab's name (the "SubHeader"
        /// text mesh of the window transform; the menu itself only ever writes the line under it).
        /// </summary>
        public string Title
        {
            get
            {
                Component window = GetSettingsField<object>("WindowTransform") as Component;
                if (!_titleProbed || !ReferenceEquals(window, _titleWindow))
                {
                    // Found once per window rather than per read: the screen names itself every
                    // frame and Find walks the window's children by name (AGENTS.md, Performance).
                    _titleWindow = window;
                    _titleProbed = true;
                    Transform header = window != null ? window.transform.Find("SubHeader") : null;
                    _titleTextMesh = header != null ? header.GetComponent<UITextMesh>() : null;
                }

                return _titleTextMesh != null ? UITextMeshTextUtility.GetEffectiveText(_titleTextMesh) : null;
            }
        }

        private Component _titleWindow;
        private UITextMesh _titleTextMesh;
        private bool _titleProbed;

        /// <summary>The handler the window itself localizes through, for the wording a screen
        /// composes from what this adapter reports.</summary>
        public ILocalizationHandler Localization
        {
            get { return _localization; }
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
                // The provider's own name, or nothing: what a tab with no name is CALLED is the
                // screen's wording, not the adapter's.
                string label = provider != null
                    ? SpokenText.Get(_localization, provider.NameKey, provider.NameKey)
                    : string.Empty;
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

            UIButton button = Reflect.Get<UIButton>(tabButton, CategoryTabButtonField);
            if (button != null)
            {
                return NativeSelectionUtility.Click(button);
            }

            return NativeSelectionUtility.PointerClick(tabComponent);
        }

        /// <summary>The drawn categories and the articles under them. Read once per redraw: pulling
        /// each article's label off its text mesh allocates a string per article, and the list only
        /// changes when the window re-spawns its category sections. The key is what the game's own
        /// section pool is drawing - how many sections, which the first and last are, how many
        /// buttons they draw between them and which tab they belong to - read from the game on every
        /// call (AGENTS.md,
        /// Performance). Which article is SELECTED is not part of it: each item answers that from
        /// the event system when it is asked. The language is, because the options menu re-localizes
        /// every drawn mesh where it stands and the pool is left alone.</summary>
        public IReadOnlyList<ArticleGroupItem> GetArticleGroups()
        {
            IList sections = GetActivePoolEntries(CategorySectionPoolField);
            int drawnSections = 0;
            int drawnButtons = 0;
            object first = null;
            object last = null;
            for (int i = 0; i < sections.Count; i++)
            {
                CodexCategorySection section = sections[i] as CodexCategorySection;
                if (section == null || !((Component)section).gameObject.activeInHierarchy)
                {
                    continue;
                }

                drawnSections++;
                first = first ?? section;
                last = section;
                List<CodexContentButton> buttons = section.Buttons;
                for (int b = 0; buttons != null && b < buttons.Count; b++)
                {
                    CodexContentButton button = buttons[b];
                    if (button != null && ((Component)button).gameObject.activeInHierarchy)
                    {
                        drawnButtons++;
                    }
                }
            }

            ILanguageDefinition language = _localization != null ? _localization.CurrentLanguage : null;
            // Which tab the window is showing, off its own current provider. The rest of this key
            // cannot tell two tabs apart where each draws ONE section with as many buttons: the pool
            // hands its entries back in reverse on a respawn, so the first and the last section are
            // the same object when there is only one of them.
            object provider = CurrentProviderField != null && _menu != null
                ? CurrentProviderField.GetValue(_menu)
                : null;
            if (_articleGroups != null
                && drawnSections == _articleSectionCount
                && drawnButtons == _articleButtonCount
                && ReferenceEquals(first, _firstArticleSection)
                && ReferenceEquals(last, _lastArticleSection)
                && ReferenceEquals(language, _articleLanguage)
                && ReferenceEquals(provider, _articleProvider))
            {
                return _articleGroups;
            }

            _articleSectionCount = drawnSections;
            _articleButtonCount = drawnButtons;
            _firstArticleSection = first;
            _lastArticleSection = last;
            _articleLanguage = language;
            _articleProvider = provider;
            _articleGroups = ReadArticleGroups(sections);
            return _articleGroups;
        }

        private IReadOnlyList<ArticleGroupItem> ReadArticleGroups(IList sections)
        {
            List<ArticleGroupItem> groups = new List<ArticleGroupItem>();
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                CodexCategorySection section = sections[sectionIndex] as CodexCategorySection;
                if (section == null || !((Component)section).gameObject.activeInHierarchy)
                {
                    continue;
                }

                string sectionLabel = CleanLabel(UITextMeshTextUtility.GetEffectiveText(Reflect.Get<UITextMesh>(section, CategorySectionTextField)));
                List<CodexContentButton> buttons = section.Buttons;
                List<ArticleItem> articles = new List<ArticleItem>();
                for (int buttonIndex = 0; buttons != null && buttonIndex < buttons.Count; buttonIndex++)
                {
                    CodexContentButton button = buttons[buttonIndex];
                    if (button == null || !((Component)button).gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    UITextMesh textMesh = Reflect.Get<UITextMesh>(button, ContentButtonTextField);
                    string label = CleanLabel(UITextMeshTextUtility.GetEffectiveText(textMesh));
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    CodexCategoryContentDefinition definition = Reflect.Get<CodexCategoryContentDefinition>(button, ContentButtonDefinitionField);
                    bool hasContentColor = definition != null && definition.HasContentColor;
                    articles.Add(new ArticleItem(
                        label,
                        button,
                        hasContentColor,
                        hasContentColor ? definition.ContentColor : default(Color),
                        groups.Count,
                        articles.Count));
                }

                if (articles.Count > 0)
                {
                    groups.Add(new ArticleGroupItem(sectionLabel, groups.Count, articles));
                }
            }

            return groups;
        }

        // The categories and their articles as they were last drawn, with the key that says the
        // window has redrawn them.
        private IReadOnlyList<ArticleGroupItem> _articleGroups;
        private int _articleSectionCount = -1;
        private int _articleButtonCount = -1;
        private object _firstArticleSection;
        private object _lastArticleSection;
        private ILanguageDefinition _articleLanguage;
        private object _articleProvider;

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

            UIButton button = Reflect.Get<UIButton>(item.Button, ContentButtonButtonField);
            if (button != null)
            {
                return NativeSelectionUtility.Click(button);
            }

            return NativeSelectionUtility.PointerClick(item.Button as Component);
        }

        public IReadOnlyList<CodexContentItem> GetContentItems()
        {
            Transform contentParent = GetSettingsField<Transform>("ContentParent");
            if (contentParent == null)
            {
                return new List<CodexContentItem>();
            }

            return _contentItems.Get(contentParent, () => ReadContentItems(contentParent));
        }

        private IReadOnlyList<CodexContentItem> ReadContentItems(Transform contentParent)
        {
            List<CodexContentItem> items = new List<CodexContentItem>();
            if (TryAddWielderContentItems(contentParent, items))
            {
                return items;
            }

            if (TryAddUnitContentItems(contentParent, items))
            {
                return items;
            }

            // Reached only when the key above says the body has been redrawn, so a still article
            // costs no walk.
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

            ScrollView.Reveal(GetSettingsField<ScrollRect>("ContentParentScrollRect"), item.SourceTransform);
        }

        public bool IsTutorialSettingsVisible()
        {
            GameObject settings = GetSettingsField<GameObject>("TutorialSettings");
            return settings != null && settings.activeInHierarchy;
        }

        public string TutorialsToggleLabel
        {
            get { return SpokenText.Get(_localization, "Tutorial/TutorialPopup/ShowTutorialCheckbox", string.Empty); }
        }

        /// <summary>The tutorials toggle the footer draws, while it draws one.</summary>
        public Component TutorialsToggle
        {
            get { return GetTutorialToggle() as Component; }
        }

        /// <summary>The reset button the footer draws, while it draws one.</summary>
        public Component ResetButton
        {
            get { return GetResetButton() as Component; }
        }

        /// <summary>What the reset button has written on it, through the menu's own key for it
        /// (<c>Options/ResetTutorials</c>, decompiled <c>CodexTutorialSettings.OnEnable</c>). Read
        /// off the KEY rather than off the button, whose assigned string is the unresolved
        /// localization token the renderer substitutes as it draws ("_Reset tutorials", measured
        /// 2026-09-06).</summary>
        public string ResetButtonLabel
        {
            get { return SpokenText.Get(_localization, "Options/ResetTutorials", string.Empty); }
        }

        /// <summary>The window's close button, while it is drawn (the game hides it in gamepad
        /// mode: <c>CodexMenu.HandleControlsChanged</c>).</summary>
        public Component CloseButton
        {
            get
            {
                Component button = GetSettingsField<object>("CloseButton") as Component;
                return button != null && button.gameObject.activeInHierarchy ? button : null;
            }
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

        /// <summary>Which tab the window is showing, by its place in the tab row.</summary>
        public int GetActiveTabIndex()
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
            return Reflect.Get<UIToggle>(settings, TutorialToggleField);
        }

        private UIButton GetResetButton()
        {
            CodexTutorialSettings settings = GetTutorialSettings();
            return Reflect.Get<UIButton>(settings, ResetButtonField);
        }

        private CodexTutorialSettings GetTutorialSettings()
        {
            GameObject settings = GetSettingsField<GameObject>("TutorialSettings");
            return settings != null ? settings.GetComponent<CodexTutorialSettings>() : null;
        }

        // The body the window last drew. Reading it walks every text mesh of the article and cleans
        // each one, which is the page's whole cost, and DrawContent destroys the body and builds a
        // new one, so the memo's key is what the content parent holds - read from the game each
        // build, not a generation a hook feeds (AGENTS.md, Screen Resolution).
        private readonly ContainerMemo<IReadOnlyList<CodexContentItem>> _contentItems =
            new ContainerMemo<IReadOnlyList<CodexContentItem>>();

        // The settings object's fields and each entry pool's ActiveEntries: one reflection lookup per
        // name rather than one per call.
        private readonly Dictionary<string, FieldInfo> _settingsFields = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<Type, PropertyInfo> ActiveEntriesProperties =
            new Dictionary<Type, PropertyInfo>();

        // The article labels the window draws, cleaned once per distinct line: cleaning runs three
        // regexes, and the list is read whole on every build.
        private readonly Dictionary<string, string> _cleanLabels = new Dictionary<string, string>();

        private string CleanLabel(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            string clean;
            if (!_cleanLabels.TryGetValue(raw, out clean))
            {
                clean = SpokenLines.Clean(raw);
                _cleanLabels[raw] = clean;
            }

            return clean;
        }

        private T GetSettingsField<T>(string fieldName) where T : class
        {
            object settings = SettingsField != null && _menu != null ? SettingsField.GetValue(_menu) : null;
            if (settings == null)
            {
                return null;
            }

            FieldInfo field;
            if (!_settingsFields.TryGetValue(fieldName, out field))
            {
                field = AccessTools.Field(settings.GetType(), fieldName);
                _settingsFields[fieldName] = field;
            }

            return field != null ? field.GetValue(settings) as T : null;
        }

        private IList GetActivePoolEntries(FieldInfo poolField)
        {
            object pool = poolField != null && _menu != null ? poolField.GetValue(_menu) : null;
            if (pool == null)
            {
                return new object[0];
            }

            Type poolType = pool.GetType();
            PropertyInfo activeEntriesProperty;
            if (!ActiveEntriesProperties.TryGetValue(poolType, out activeEntriesProperty))
            {
                activeEntriesProperty = poolType.GetProperty("ActiveEntries");
                ActiveEntriesProperties[poolType] = activeEntriesProperty;
            }

            IList entries = activeEntriesProperty != null ? activeEntriesProperty.GetValue(pool, null) as IList : null;
            return entries ?? new object[0];
        }

        private object GetActivePoolEntry(FieldInfo poolField, int index)
        {
            IList entries = GetActivePoolEntries(poolField);
            return index >= 0 && index < entries.Count ? entries[index] : null;
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
                        && string.IsNullOrWhiteSpace(GetVisibleText(Reflect.Get<UITextMesh>(component, UnitInfoDescriptionField))))
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

        // Under GetContentItems, which reads the body only when the game's own content parent has
        // been emptied and refilled - a key read from the game, not a hook. A redraw costs one walk
        // for the wielder card and one for the unit card; a still page costs none.
        private bool TryAddWielderContentItems(Transform contentParent, List<CodexContentItem> items)
        {
            WielderCodexContent content = contentParent.GetComponentInChildren<WielderCodexContent>(false);
            if (content == null)
            {
                return false;
            }

            AddTextMeshItems(items, CodexContentItemKind.Heading, Reflect.Get<UITextMesh>(content, WielderNameTextField));
            AddTextMeshItems(items, CodexContentItemKind.Text, Reflect.Get<UITextMesh>(content, WielderClassTextField));
            AddTextMeshItems(items, CodexContentItemKind.Text, Reflect.Get<UITextMesh>(content, WielderDescriptionTextField));
            AddStatItem(items, "Commanders/Tooltip/Offense", string.Empty, Reflect.Get<UITextMesh>(content, WielderOffenseStatTextField));
            AddStatItem(items, "Commanders/Tooltip/Defense", string.Empty, Reflect.Get<UITextMesh>(content, WielderDefenceStatTextField));
            AddStatItem(items, "Commanders/Tooltip/Movement", string.Empty, Reflect.Get<UITextMesh>(content, WielderMovementStatTextField));
            AddStatItem(items, "Commanders/Tooltip/ViewRadius", string.Empty, Reflect.Get<UITextMesh>(content, WielderViewRadiusStatTextField));
            AddStatItem(items, "Commanders/Tooltip/Command", "Command", Reflect.Get<UITextMesh>(content, WielderCommandStatTextField));
            AddWielderInfoSection(items, Reflect.Get<WielderCodexContentInfoSection>(content, WielderStartingTroopsField));
            AddWielderInfoSection(items, Reflect.Get<WielderCodexContentInfoSection>(content, WielderSkillsField));
            AddWielderInfoSection(items, Reflect.Get<WielderCodexContentInfoSection>(content, WielderSpecializationField));
            return true;
        }

        // As above: only on a redraw. The unit card's sections and each section's info sections are
        // walked in the same pass.
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

                AddTextMeshItems(items, CodexContentItemKind.Heading, Reflect.Get<UITextMesh>(section, UnitContentSectionHeaderField));
                if (i < definitions.Count)
                {
                    AddUnitEssenceItem(items, section, definitions[i]);
                }

                AddTextMeshItems(items, CodexContentItemKind.Text, Reflect.Get<UITextMesh>(section, UnitContentSectionDescriptionField));

                // Same pass, one level down: only on a redraw.
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
            return Reflect.Get<CodexCategoryContentDefinition>(button, ContentButtonDefinitionField);
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

            TroopViewEssenceController essenceController = Reflect.Get<TroopViewEssenceController>(section, UnitContentSectionEssenceControllerField);
            RectTransform sourceTransform = essenceController != null
                ? ((Component)essenceController).GetComponent<RectTransform>()
                : ((Component)section).GetComponent<RectTransform>();
            List<EssenceAmount> amounts = new List<EssenceAmount>();
            for (int i = 0; i < allEssences.Count; i++)
            {
                amounts.Add(new EssenceAmount(GetEssenceAmountText(allEssences[i].Item1, allEssences[i].Item2)));
            }

            items.Add(new CodexContentItem(SpokenText.Get(_localization, "Units/Types/EssenceIntro", string.Empty), amounts, sourceTransform));
        }

        private static void AddUnitInfoSectionItems(List<CodexContentItem> items, UnitCodexContentInfoSection section)
        {
            if (section == null || !((Component)section).gameObject.activeInHierarchy)
            {
                return;
            }

            UITextMesh header = Reflect.Get<UITextMesh>(section, UnitInfoHeaderField);
            UITextMesh description = Reflect.Get<UITextMesh>(section, UnitInfoDescriptionField);
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

            string label = SpokenText.Get(_localization, labelKey, fallbackLabel);
            RectTransform sourceTransform = ((Component)valueText).GetComponent<RectTransform>();
            // The stat's own name and its own amount; joining them is the screen's wording.
            items.Add(new CodexContentItem(CodexContentItemKind.Text, label, value, sourceTransform));
        }

        private static void AddWielderInfoSection(List<CodexContentItem> items, WielderCodexContentInfoSection section)
        {
            if (section == null || !((Component)section).gameObject.activeInHierarchy)
            {
                return;
            }

            AddTextMeshItems(items, CodexContentItemKind.Heading, Reflect.Get<UITextMesh>(section, WielderInfoHeaderField));
            AddTextMeshItems(items, CodexContentItemKind.Text, Reflect.Get<UITextMesh>(section, WielderInfoDescriptionField));
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

            return UITextMeshTextUtility.Spoken(textMesh);
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
                string text = SpokenLines.Clean(parts[i]);
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

        private string GetEssenceAmountText(EssenceType essenceType, int count)
        {
            return EssenceText.Amount(_localization, essenceType, count);
        }

        private T GetCurrentProviderField<T>(string fieldName) where T : class
        {
            ICodexProvider provider = CurrentProviderField != null && _menu != null
                ? CurrentProviderField.GetValue(_menu) as ICodexProvider
                : null;
            FieldInfo field = provider != null ? AccessTools.Field(provider.GetType(), fieldName) : null;
            return field != null ? field.GetValue(provider) as T : null;
        }

        public sealed class TabItem
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

        public sealed class ArticleGroupItem
        {
            public ArticleGroupItem(string label, int index, IReadOnlyList<ArticleItem> articles)
            {
                Label = label ?? string.Empty;
                Index = index;
                Articles = articles ?? new ArticleItem[0];
            }

            /// <summary>The category's own drawn name, empty where the game drew none.</summary>
            public string Label { get; private set; }
            public int Index { get; private set; }
            public IReadOnlyList<ArticleItem> Articles { get; private set; }
        }

        public sealed class ArticleItem
        {
            public ArticleItem(
                string label,
                CodexContentButton button,
                bool hasContentColor,
                Color contentColor,
                int categoryIndex,
                int articleIndex)
            {
                Label = label;
                Button = button;
                HasContentColor = hasContentColor;
                ContentColor = contentColor;
                CategoryIndex = categoryIndex;
                ArticleIndex = articleIndex;
            }

            public string Label { get; private set; }
            public CodexContentButton Button { get; private set; }

            /// <summary>The article's own power-level colour, where its definition carries one.</summary>
            public bool HasContentColor { get; private set; }
            public Color ContentColor { get; private set; }
            public int CategoryIndex { get; private set; }
            public int ArticleIndex { get; private set; }

            /// <summary>Whether the window is drawing THIS article, which is where the game's own
            /// selection sits. Read when asked, so the list above can outlive the frame.</summary>
            public bool IsSelected
            {
                get
                {
                    GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                    return selected != null && Button != null && selected == ((Component)Button).gameObject;
                }
            }
        }

        public enum CodexContentItemKind
        {
            Heading,
            Text,
            Essence
        }

        public sealed class EssenceAmount
        {
            public EssenceAmount(string text)
            {
                Text = text ?? string.Empty;
            }

            public string Text { get; private set; }
        }

        public sealed class CodexContentItem
        {
            public CodexContentItem(CodexContentItemKind kind, string text, RectTransform sourceTransform = null)
                : this(kind, text, null, sourceTransform)
            {
            }

            /// <summary>A line the game drew as a name and an amount side by side (a commander's
            /// stats), each reported as the game wrote it.</summary>
            public CodexContentItem(CodexContentItemKind kind, string text, string value, RectTransform sourceTransform)
            {
                Kind = kind;
                Text = text ?? string.Empty;
                Value = value ?? string.Empty;
                SourceTransform = sourceTransform;
                Essences = new EssenceAmount[0];
            }

            public CodexContentItem(string essenceLabel, IReadOnlyList<EssenceAmount> essences, RectTransform sourceTransform = null)
            {
                Kind = CodexContentItemKind.Essence;
                Text = essenceLabel ?? string.Empty;
                Value = string.Empty;
                SourceTransform = sourceTransform;
                Essences = essences ?? new EssenceAmount[0];
            }

            public CodexContentItemKind Kind { get; private set; }
            public string Text { get; private set; }

            /// <summary>The amount drawn beside <see cref="Text"/>, empty for a line that is only
            /// text.</summary>
            public string Value { get; private set; }
            public RectTransform SourceTransform { get; private set; }
            public IReadOnlyList<EssenceAmount> Essences { get; private set; }
        }
    }
}
