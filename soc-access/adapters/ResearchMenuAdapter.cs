using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.GameActions;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Research;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class ResearchMenuAdapter
    {
        private static readonly TooltipAnchor[] ResearchTooltipAnchors =
        {
            TooltipAnchor.RightCenter,
            TooltipAnchor.LeftCenter,
            TooltipAnchor.TopCenter,
            TooltipAnchor.BottomCenter
        };

        private static readonly FieldInfo ContainerField = AccessTools.Field(typeof(ResearchMenu), "_container");
        private static readonly FieldInfo TutorialButtonField = AccessTools.Field(typeof(ResearchMenu), "_tutorialButton");
        private static readonly FieldInfo AllTabButtonsField = AccessTools.Field(typeof(ResearchMenu), "_allTabButtons");
        private static readonly FieldInfo BuildingsTabGroupField = AccessTools.Field(typeof(ResearchMenu), "_buildingsTabGroup");
        private static readonly FieldInfo CategoriesField = AccessTools.Field(typeof(ResearchMenu), "_categories");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(ResearchMenu), "_facade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(ResearchMenu), "_localizationHandler");
        private static readonly FieldInfo BuildingTabNameField = AccessTools.Field(typeof(ResearchMenuBuildingTabButton), "_name");
        private static readonly FieldInfo BuildingTabDescriptionField = AccessTools.Field(typeof(ResearchMenuBuildingTabButton), "_description");
        private static readonly FieldInfo BuildingTabMapEntityIdField = AccessTools.Field(typeof(ResearchMenuBuildingTabButton), "_mapEntityId");
        private static readonly FieldInfo CategoryNameField = AccessTools.Field(typeof(ResearchMenuCategory), "_name");
        private static readonly FieldInfo CategoryButtonsContainerField = AccessTools.Field(typeof(ResearchMenuCategory), "_buttonsContainer");
        private static readonly FieldInfo ResearchStackField = AccessTools.Field(typeof(ResearchMenuStackButton), "_researchStack");

        private readonly ResearchMenu _menu;

        public ResearchMenuAdapter(ResearchMenu menu)
        {
            _menu = menu;
        }

        public IClientAdventureFacade Facade
        {
            get { return GetField<IClientAdventureFacade>(_menu, FacadeField); }
        }

        public bool IsPresent()
        {
            GameObject container = GetField<GameObject>(_menu, ContainerField);
            return _menu != null
                && container != null
                && container.activeInHierarchy
                && _menu.HasContent();
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = GetTutorialButton();
            return IsVisible(button as Component);
        }

        public string GetTutorialButtonLabel()
        {
            string label = MenuButtonTextUtility.GetAllVisibleText(GetTutorialButton());
            return string.IsNullOrWhiteSpace(label)
                ? GameText.Get(GetLocalization(), "Tutorial/CodexCategory/Tutorials", "Tutorials")
                : label;
        }

        public bool ActivateTutorial()
        {
            return NativeSelectionUtility.Click(GetTutorialButton());
        }

        public IReadOnlyList<BuildingItem> GetBuildings()
        {
            List<BuildingItem> items = new List<BuildingItem>();
            IReadOnlyList<ResearchMenuBuildingTabButton> tabs = GetTabButtons();
            for (int i = 0; i < tabs.Count; i++)
            {
                ResearchMenuBuildingTabButton tab = tabs[i];
                if (!IsVisible(tab as Component))
                {
                    continue;
                }

                int index = i;
                UIButton button = GetButton(tab);
                string label = GetBuildingLabel(tab, index);
                string description = GetBuildingDescription(tab);
                int mapEntityId = BuildingTabMapEntityIdField != null ? (int)BuildingTabMapEntityIdField.GetValue(tab) : 0;
                items.Add(new BuildingItem(
                    label,
                    description,
                    mapEntityId < 0,
                    () => FocusBuilding(index, button),
                    () => ActivateBuilding(button)));
            }

            return items;
        }

        public int SelectedBuildingIndex
        {
            get
            {
                DynamicUITabGroup tabGroup = GetBuildingsTabGroup();
                return tabGroup != null ? tabGroup.CurrentTab : 0;
            }
        }

        public IReadOnlyList<CategoryItem> GetCategories()
        {
            List<CategoryItem> items = new List<CategoryItem>();
            IReadOnlyList<ResearchMenuCategory> categories = GetNativeCategories();
            for (int i = 0; i < categories.Count; i++)
            {
                ResearchMenuCategory category = categories[i];
                if (!IsVisible(category as Component))
                {
                    continue;
                }

                List<ResearchItem> researchItems = new List<ResearchItem>();
                ResearchMenuStackButton[] buttons = GetResearchButtons(category);
                for (int j = 0; j < buttons.Length; j++)
                {
                    ResearchMenuStackButton stackButton = buttons[j];
                    if (!IsVisible(stackButton as Component))
                    {
                        continue;
                    }

                    ResearchStack stack = ResearchStackField != null
                        ? ResearchStackField.GetValue(stackButton) as ResearchStack
                        : null;
                    UIButton button = stackButton.Button;
                    int itemIndex = j;
                    Func<bool> activate = () => NativeSelectionUtility.Click(button);
                    string name = Localize(stack != null ? stack.NameKey : null, "Research " + (itemIndex + 1));
                    researchItems.Add(new ResearchItem(
                        name,
                        GetOwnedTier(stack),
                        GetTierHeader(),
                        () => FocusResearch(button),
                        activate,
                        BuildResearchTooltip(button, activate)));
                }

                if (researchItems.Count > 0)
                {
                    items.Add(new CategoryItem(
                        GetCategoryLabel(category, i),
                        researchItems));
                }
            }

            return items;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private bool FocusBuilding(int index, UIButton button)
        {
            HideNativeTooltip();
            NativeSelectionUtility.Select(button as Component);
            if (SelectedBuildingIndex == index)
            {
                return true;
            }

            return ClickBuilding(button);
        }

        private bool ActivateBuilding(UIButton button)
        {
            return ClickBuilding(button);
        }

        private static bool ClickBuilding(UIButton button)
        {
            return NativeSelectionUtility.PointerClick(button as Component);
        }

        private bool FocusResearch(UIButton button)
        {
            if (button == null)
            {
                HideNativeTooltip();
                return false;
            }

            NativeTooltipUtility.ShowTooltipForComponent(button as Component);
            return true;
        }

        private string GetBuildingLabel(ResearchMenuBuildingTabButton tab, int index)
        {
            UITextMesh name = GetField<UITextMesh>(tab, BuildingTabNameField);
            string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(name));
            return string.IsNullOrWhiteSpace(label) ? "Building " + (index + 1) : label;
        }

        private string GetBuildingDescription(ResearchMenuBuildingTabButton tab)
        {
            UITextMesh description = GetField<UITextMesh>(tab, BuildingTabDescriptionField);
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(description));
        }

        private string GetCategoryLabel(ResearchMenuCategory category, int index)
        {
            UITextMesh name = GetField<UITextMesh>(category, CategoryNameField);
            string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(name));
            return string.IsNullOrWhiteSpace(label) ? "Research category " + (index + 1) : label;
        }

        private int GetOwnedTier(ResearchStack stack)
        {
            if (stack == null)
            {
                return 0;
            }

            IClientAdventureFacade facade = Facade;
            if (facade == null || facade.Research == null || facade.Teams == null)
            {
                return 0;
            }

            int teamId = facade.Teams.LocalTeamInControlId;
            List<ResearchDefinition> definitions = stack.Definitions;
            int ownedTier = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                ResearchDefinition definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                if (facade.Research.HasGlobalResearch(definition.ResearchType, teamId, includeDisabled: true))
                {
                    ownedTier = i + 1;
                }
            }

            return ownedTier;
        }

        private string GetTierHeader()
        {
            return Localize("Adventure/KingdomInformationHUD/ResearchTierHeader", "Tier");
        }

        private Tooltip BuildResearchTooltip(UIButton button, Func<bool> activate)
        {
            Component component = button as Component;
            if (component == null)
            {
                return null;
            }

            return new Tooltip(
                () => CaptureResearchTooltip(component).TextLines,
                VisualTooltipMetadata.ForComponent(component, component.GetComponent<RectTransform>(), ResearchTooltipAnchors),
                GetResearchTooltipActions(component, activate));
        }

        private IReadOnlyList<TooltipAction> GetResearchTooltipActions(Component component, Func<bool> activate)
        {
            DetailsTextUtility capture = CaptureResearchTooltip(component);
            List<TooltipAction> actions = new List<TooltipAction>();
            for (int i = 0; i < capture.InstructionRows.Count; i++)
            {
                TooltipInstructionRow row = capture.InstructionRows[i];
                string label = SpeechTextSanitizer.Normalize(row != null ? row.Text : null);
                if (string.IsNullOrWhiteSpace(label) || row.InputType == InputType.NoInput)
                {
                    continue;
                }

                actions.Add(new TooltipAction(label, activate));
            }

            return actions;
        }

        private DetailsTextUtility CaptureResearchTooltip(Component component)
        {
            IDetails details;
            ILocalizationHandler localization = GetLocalization();
            return NativeTooltipUtility.TryGetUiDetails(component, out details)
                ? DetailsTextUtility.Capture(details, localization)
                : new DetailsTextUtility();
        }

        private ResearchMenuStackButton[] GetResearchButtons(ResearchMenuCategory category)
        {
            UITransform container = GetField<UITransform>(category, CategoryButtonsContainerField);
            Component component = container as Component;
            return component != null
                ? component.GetComponentsInChildren<ResearchMenuStackButton>(false)
                : new ResearchMenuStackButton[0];
        }

        private IReadOnlyList<ResearchMenuBuildingTabButton> GetTabButtons()
        {
            return GetField<List<ResearchMenuBuildingTabButton>>(_menu, AllTabButtonsField)
                ?? new List<ResearchMenuBuildingTabButton>();
        }

        private IReadOnlyList<ResearchMenuCategory> GetNativeCategories()
        {
            return GetField<List<ResearchMenuCategory>>(_menu, CategoriesField)
                ?? new List<ResearchMenuCategory>();
        }

        private DynamicUITabGroup GetBuildingsTabGroup()
        {
            return GetField<DynamicUITabGroup>(_menu, BuildingsTabGroupField);
        }

        private UIButton GetTutorialButton()
        {
            return GetField<UIButton>(_menu, TutorialButtonField);
        }

        private static UIButton GetButton(ResearchMenuBuildingTabButton tab)
        {
            return tab != null ? ((Component)tab).GetComponent<UIButton>() : null;
        }

        private ILocalizationHandler GetLocalization()
        {
            return GetField<ILocalizationHandler>(_menu, LocalizationField);
        }

        private string Localize(string key, string fallback)
        {
            ILocalizationHandler localization = GetLocalization();
            return SpeechTextSanitizer.Normalize(GameText.Get(localization, key, fallback ?? string.Empty));
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class BuildingItem
        {
            public BuildingItem(string label, string description, bool missingBuilding, Func<bool> focus, Func<bool> activate)
            {
                Label = label ?? string.Empty;
                Description = description ?? string.Empty;
                MissingBuilding = missingBuilding;
                Focus = focus;
                Activate = activate;
            }

            public string Label { get; private set; }
            public string Description { get; private set; }
            public bool MissingBuilding { get; private set; }
            public Func<bool> Focus { get; private set; }
            public Func<bool> Activate { get; private set; }
        }

        internal sealed class CategoryItem
        {
            public CategoryItem(string label, IReadOnlyList<ResearchItem> items)
            {
                Label = label ?? string.Empty;
                Items = items ?? new ResearchItem[0];
            }

            public string Label { get; private set; }
            public IReadOnlyList<ResearchItem> Items { get; private set; }
        }

        internal sealed class ResearchItem
        {
            public ResearchItem(string label, int ownedTier, string tierHeader, Func<bool> focus, Func<bool> activate, Tooltip tooltip)
            {
                Label = label ?? string.Empty;
                OwnedTier = ownedTier;
                TierHeader = tierHeader ?? string.Empty;
                Focus = focus;
                Activate = activate;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public int OwnedTier { get; private set; }
            public string TierHeader { get; private set; }
            public Func<bool> Focus { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }
    }
}
