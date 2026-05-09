using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.GameActions;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Research;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class BuildMenuAdapter
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(BuildMenu), "_async");
        private static readonly FieldInfo TutorialButtonField = AccessTools.Field(typeof(BuildMenu), "_tutorialButton");
        private static readonly FieldInfo LeftNavigationButtonField = AccessTools.Field(typeof(BuildMenu), "_leftNavigationButton");
        private static readonly FieldInfo RightNavigationButtonField = AccessTools.Field(typeof(BuildMenu), "_rightNavigationButton");
        private static readonly FieldInfo SmallBuildingsTabButtonField = AccessTools.Field(typeof(BuildMenu), "_smallBuildingsTabButton");
        private static readonly FieldInfo MediumBuildingsTabButtonField = AccessTools.Field(typeof(BuildMenu), "_mediumBuildingsTabButton");
        private static readonly FieldInfo LargeBuildingsTabButtonField = AccessTools.Field(typeof(BuildMenu), "_largeBuildingsTabButton");
        private static readonly FieldInfo AutoSelectBuildSiteToggleField = AccessTools.Field(typeof(BuildMenu), "_autoSelectBuildSiteToggle");
        private static readonly FieldInfo BuildMenuButtonPoolField = AccessTools.Field(typeof(BuildMenu), "_buildMenuButtonPool");
        private static readonly FieldInfo BuildMenuIncomePoolField = AccessTools.Field(typeof(BuildMenu), "_buildMenuIncomePool");
        private static readonly FieldInfo CurrentBuildSiteField = AccessTools.Field(typeof(BuildMenu), "_currentBuildSite");
        private static readonly FieldInfo SelectedCategoryField = AccessTools.Field(typeof(BuildMenu), "_selectedCategory");
        private static readonly FieldInfo SelectedActionField = AccessTools.Field(typeof(BuildMenu), "_selectedAction");
        private static readonly FieldInfo AllSiblingsField = AccessTools.Field(typeof(BuildMenu), "_allSiblings");
        private static readonly FieldInfo SiblingIndexField = AccessTools.Field(typeof(BuildMenu), "_siblingIndex");
        private static readonly FieldInfo BuildTimeTextField = AccessTools.Field(typeof(BuildMenu), "_buildTimeText");
        private static readonly FieldInfo BuildSizeHeaderField = AccessTools.Field(typeof(BuildMenu), "_buildSizeHeader");
        private static readonly FieldInfo HeaderSectionField = AccessTools.Field(typeof(BuildMenu), "_headerSection");
        private static readonly FieldInfo PurchaseButtonField = AccessTools.Field(typeof(BuildMenu), "_purchaseButton");
        private static readonly FieldInfo PurchaseButtonContainerField = AccessTools.Field(typeof(BuildMenu), "_purchaseButtonContainer");
        private static readonly FieldInfo CannotBuyContainerField = AccessTools.Field(typeof(BuildMenu), "_cannotBuyContainer");
        private static readonly FieldInfo CannotBuyTextField = AccessTools.Field(typeof(BuildMenu), "_cannotBuyText");
        private static readonly FieldInfo LargeCostSectionField = AccessTools.Field(typeof(BuildMenu), "_largeCostSection");
        private static readonly FieldInfo PurchaseAreaField = AccessTools.Field(typeof(BuildMenu), "_purchaseArea");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(BuildMenu), "_facade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(BuildMenu), "_localization");
        private static readonly FieldInfo SelectionHandlerField = AccessTools.Field(typeof(BuildMenu), "_selectionHandler");
        private static readonly FieldInfo DetailsField = AccessTools.Field(typeof(BuildMenu), "_details");
        private static readonly FieldInfo GameConfigField = AccessTools.Field(typeof(BuildMenu), "_gameConfig");
        private static readonly FieldInfo BuildingRequirementValidatorField = AccessTools.Field(typeof(BuildMenu), "_buildingRequirementValidator");
        private static readonly FieldInfo ResearchLookupField = AccessTools.Field(typeof(BuildMenu), "_researchLookup");
        private static readonly MethodInfo GetSelectedLevelMethod = AccessTools.Method(typeof(BuildMenu), "GetSelectedLevel");
        private static readonly MethodInfo HandleLeftNavigationButtonClickedMethod = AccessTools.Method(typeof(BuildMenu), "HandleLeftNavigationButtonClicked");
        private static readonly MethodInfo HandleRightNavigationButtonClickedMethod = AccessTools.Method(typeof(BuildMenu), "HandleRightNavigationButtonClicked");

        private static readonly FieldInfo BuildMenuButtonButtonField = AccessTools.Field(typeof(BuildMenuButton), "_button");
        private static readonly FieldInfo HeaderNameField = AccessTools.Field(typeof(BuildMenuHeaderSection), "_name");
        private static readonly FieldInfo HeaderDescriptionField = AccessTools.Field(typeof(BuildMenuHeaderSection), "_description");
        private static readonly FieldInfo HeaderLevelToButtonField = AccessTools.Field(typeof(BuildMenuHeaderSection), "_levelToButton");
        private static readonly FieldInfo DescriptionSectionHeaderField = AccessTools.Field(typeof(BuildMenuDescriptionSection), "_headerText");
        private static readonly FieldInfo DescriptionEntryTextField = AccessTools.Field(typeof(BuildMenuDescriptionEntry), "_text");
        private static readonly FieldInfo DescriptionEntryBackgroundField = AccessTools.Field(typeof(BuildMenuDescriptionEntry), "_background");
        private static readonly FieldInfo DescriptionEntryIconField = AccessTools.Field(typeof(BuildMenuDescriptionEntry), "_icon");

        private static readonly FieldInfo GoldCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_goldCostEntry");
        private static readonly FieldInfo StoneCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_stoneCostEntry");
        private static readonly FieldInfo WoodCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_woodCostEntry");
        private static readonly FieldInfo GlimmerWeaveCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_glimmerWeaveCostEntry");
        private static readonly FieldInfo AncientAmberCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_ancientAmberCostEntry");
        private static readonly FieldInfo CelestialOreCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_celestialOreCostEntry");
        private static readonly FieldInfo GoldAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_goldAmountText");
        private static readonly FieldInfo StoneAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_stoneAmountText");
        private static readonly FieldInfo WoodAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_woodAmountText");
        private static readonly FieldInfo GlimmerWeaveAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_glimmerWeaveAmountText");
        private static readonly FieldInfo AncientAmberAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_ancientAmberAmountText");
        private static readonly FieldInfo CelestialOreAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_celestialOreAmountText");

        private readonly BuildMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private readonly ISelectionHandler _selectionHandler;
        private readonly object _gameConfig;
        private readonly IBuildingRequirementValidator _buildingRequirementValidator;
        private readonly IResearchLookup _researchLookup;

        public BuildMenuAdapter(BuildMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(menu, FacadeField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
            _selectionHandler = GetField<ISelectionHandler>(menu, SelectionHandlerField);
            _gameConfig = GetField<object>(menu, GameConfigField);
            _buildingRequirementValidator = GetField<IBuildingRequirementValidator>(menu, BuildingRequirementValidatorField);
            _researchLookup = GetField<IResearchLookup>(menu, ResearchLookupField);
        }

        public bool IsPresent()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && _menu.IsOpen
                && GetField<object>(_menu, AsyncField) != null;
        }

        public string CurrentStateKey
        {
            get
            {
                IMapEntity site = CurrentBuildSite;
                BuildOnBuildSiteAction action = CurrentAction;
                return (site != null ? site.Id.ToString() : "none")
                    + "|"
                    + SelectedCategory
                    + "|"
                    + (action != null ? action.BuildingBlueprintId.ToString() : "none");
            }
        }

        public BuildSiteSize SelectedCategory
        {
            get
            {
                object value = SelectedCategoryField != null ? SelectedCategoryField.GetValue(_menu) : null;
                return value is BuildSiteSize ? (BuildSiteSize)value : BuildSiteSize.Small;
            }
        }

        public int SelectedCategoryIndex
        {
            get
            {
                switch (SelectedCategory)
                {
                    case BuildSiteSize.Medium:
                        return 1;
                    case BuildSiteSize.Large:
                        return 2;
                    default:
                        return 0;
                }
            }
        }

        public string BuildSiteSummary
        {
            get
            {
                IMapEntity site = CurrentBuildSite;
                if (site == null)
                {
                    return "No build site selected";
                }

                Vector2Int position = site.Position;
                string size = FormatSize(site.GetSize().Value);
                int index = SiblingIndex + 1;
                int count = SiblingCount;
                string ordinal = index > 0 && count > 0 ? ", " + index + " of " + count : string.Empty;
                return size + " build site" + ordinal + ", at " + position.x + ", " + position.y;
            }
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = GetTutorialButton();
            return IsVisible(button as Component);
        }

        public string GetTutorialButtonLabel()
        {
            string label = GetButtonLabel(GetTutorialButton());
            return string.IsNullOrWhiteSpace(label) ? "Tutorial available" : label;
        }

        public bool ActivateTutorial()
        {
            return NativeSelectionUtility.Click(GetTutorialButton());
        }

        public IReadOnlyList<CategoryItem> GetCategories()
        {
            return new[]
            {
                new CategoryItem("build-category-small", BuildCategoryLabel("Small", BuildSiteSize.Small), 0, BuildSiteSize.Small, IsButtonEnabled(GetSmallTabButton())),
                new CategoryItem("build-category-medium", BuildCategoryLabel("Medium", BuildSiteSize.Medium), 1, BuildSiteSize.Medium, IsButtonEnabled(GetMediumTabButton())),
                new CategoryItem("build-category-large", BuildCategoryLabel("Large", BuildSiteSize.Large), 2, BuildSiteSize.Large, IsButtonEnabled(GetLargeTabButton()))
            };
        }

        public bool FocusCategory(BuildSiteSize size)
        {
            UIButton button = GetCategoryButton(size);
            NativeSelectionUtility.Select(button as Component);
            if (SelectedCategory == size)
            {
                return true;
            }

            return NativeSelectionUtility.Click(button);
        }

        public string BuildTimeText
        {
            get { return GetText(GetField<UITextMesh>(_menu, BuildTimeTextField)); }
        }

        public string BuildSizeHeader
        {
            get { return GetText(GetField<UITextMesh>(_menu, BuildSizeHeaderField)); }
        }

        public IReadOnlyList<BuildingItem> GetBuildings()
        {
            List<BuildingItem> items = new List<BuildingItem>();
            IReadOnlyList<BuildMenuButton> buttons = GetActiveBuildButtons();
            for (int i = 0; i < buttons.Count; i++)
            {
                BuildMenuButton button = buttons[i];
                BuildOnBuildSiteAction action = button != null ? button.BuildAction : null;
                if (action == null)
                {
                    continue;
                }

                IMapEntityBlueprint blueprint = _facade != null ? _facade.MapEntities.GetBlueprint(action.BuildingBlueprintId) : null;
                string label = blueprint != null && _localization != null
                    ? SpeechTextSanitizer.Normalize(_localization.GetText(blueprint.NameKey))
                    : "Building " + (i + 1);
                BuildMenuButton captured = button;
                BuildOnBuildSiteAction capturedAction = action;
                int capturedIndex = i;
                items.Add(new BuildingItem(
                    "build-building-" + capturedIndex,
                    label,
                    () => capturedAction != null && !capturedAction.CanExecute() ? "unavailable" : string.Empty,
                    () => FocusBuilding(captured),
                    () => Tooltip.ForComponent(GetBuildButton(captured) as Component, _localization)));
            }

            return items;
        }

        public int SelectedBuildingIndex
        {
            get
            {
                BuildOnBuildSiteAction selected = CurrentAction;
                IReadOnlyList<BuildMenuButton> buttons = GetActiveBuildButtons();
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i] != null && ReferenceEquals(buttons[i].BuildAction, selected))
                    {
                        return i;
                    }
                }

                return 0;
            }
        }

        public string SelectedBuildingSummary
        {
            get
            {
                BuildMenuHeaderSection header = GetField<BuildMenuHeaderSection>(_menu, HeaderSectionField);
                string name = GetText(GetField<UITextMesh>(header, HeaderNameField));
                string description = GetText(GetField<UITextMesh>(header, HeaderDescriptionField));
                return JoinParts(name, description);
            }
        }

        public bool HasSelectedBuildingSummary()
        {
            return !string.IsNullOrWhiteSpace(SelectedBuildingSummary);
        }

        public int SelectedTier
        {
            get
            {
                if (_menu == null || GetSelectedLevelMethod == null)
                {
                    return 1;
                }

                object value = GetSelectedLevelMethod.Invoke(_menu, null);
                return value is int ? (int)value : 1;
            }
        }

        public IReadOnlyList<TierItem> GetTiers()
        {
            List<TierItem> items = new List<TierItem>();
            BuildMenuHeaderSection header = GetField<BuildMenuHeaderSection>(_menu, HeaderSectionField);
            IDictionary<int, UIButton> buttons = GetField<Dictionary<int, UIButton>>(header, HeaderLevelToButtonField);
            if (buttons == null || buttons.Count <= 1)
            {
                return items;
            }

            foreach (KeyValuePair<int, UIButton> pair in buttons.OrderBy(pair => pair.Key))
            {
                int level = pair.Key;
                UIButton button = pair.Value;
                string label = GetButtonLabel(button);
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = "Tier " + level;
                }

                items.Add(new TierItem(
                    "build-tier-" + level,
                    label,
                    level,
                    () => FocusTier(level),
                    () => Tooltip.ForComponent(button as Component, _localization)));
            }

            return items;
        }

        public bool FocusTier(int level)
        {
            UIButton button = GetTierButton(level);
            NativeSelectionUtility.Select(button as Component);
            if (SelectedTier == level)
            {
                return true;
            }

            return NativeSelectionUtility.Click(button);
        }

        public IReadOnlyList<SectionItem> GetAvailableResearchItems()
        {
            return GetVisibleSectionItems("Adventure/BuildMenu/AvailableResearch", "available-research");
        }

        public string AvailableResearchHeader
        {
            get { return GetLocalizedText("Adventure/BuildMenu/AvailableResearch", "Available Research"); }
        }

        public IReadOnlyList<SectionMenu> GetIncomeAndGarrisonMenus()
        {
            List<SectionMenu> menus = new List<SectionMenu>();
            IReadOnlyList<BuildMenuDescriptionSection> sections = GetActiveDescriptionSections();
            int index = 0;
            for (int i = 0; i < sections.Count; i++)
            {
                BuildMenuDescriptionSection section = sections[i];
                if (!IsVisible(section as Component))
                {
                    continue;
                }

                string header = GetSectionHeader(section);
                if (IsSectionHeader(header, "Adventure/BuildMenu/AvailableResearch", "Available Research")
                    || IsSectionHeader(header, "Adventure/BuildMenu/Requirements", "Requirements")
                    || IsSectionHeader(header, "Adventure/BuildMenu/Cost", "Cost"))
                {
                    continue;
                }

                IReadOnlyList<SectionItem> items = GetSectionItems(section, "build-info-section-" + index);
                if (items.Count == 0)
                {
                    continue;
                }

                menus.Add(new SectionMenu("build-info-section-" + index, header, items));
                index++;
            }

            return menus;
        }

        public IReadOnlyList<RequirementItem> GetRequirements()
        {
            List<RequirementItem> items = new List<RequirementItem>();
            AdventureMapEntityLevelDetails level = CurrentLevelDetails;
            IBuildingRequirements requirements = level != null && level.Requirements != null
                ? level.Requirements.Requirements
                : null;
            if (requirements == null)
            {
                return items;
            }

            int buildSiteId = CurrentBuildSite != null ? CurrentBuildSite.Id : -1;
            BuildMenuDescriptionEntry[] nativeEntries = GetSectionEntries("Adventure/BuildMenu/Requirements", "Requirements");
            int nativeIndex = 0;

            if (requirements.RequiredBuildings != null)
            {
                for (int i = 0; i < requirements.RequiredBuildings.Length; i++)
                {
                    RequiredBuilding requirement = requirements.RequiredBuildings[i];
                    string label = FormatRequiredBuilding(requirement);
                    bool met = buildSiteId != -1
                        && _buildingRequirementValidator != null
                        && _buildingRequirementValidator.Validate(requirement, buildSiteId);
                    items.Add(new RequirementItem(
                        "build-requirement-building-" + i,
                        PrefixMissing(label, met),
                        null));
                    nativeIndex++;
                }
            }

            if (requirements.RequiredResearch != null)
            {
                for (int i = 0; i < requirements.RequiredResearch.Length; i++)
                {
                    var requirement = requirements.RequiredResearch[i];
                    string label = FormatRequiredResearch(requirement);
                    bool met = buildSiteId != -1
                        && _buildingRequirementValidator != null
                        && _facade != null
                        && ValidateResearchRequirement(requirement, buildSiteId);
                    BuildMenuDescriptionEntry nativeEntry = nativeIndex >= 0 && nativeIndex < nativeEntries.Length ? nativeEntries[nativeIndex] : null;
                    items.Add(new RequirementItem(
                        "build-requirement-research-" + i,
                        PrefixMissing(label, met),
                        GetEntryTooltip(nativeEntry)));
                    nativeIndex++;
                }
            }

            return items;
        }

        public string RequirementsHeader
        {
            get { return GetLocalizedText("Adventure/BuildMenu/Requirements", "Requirements"); }
        }

        public bool HasRequirements()
        {
            return GetRequirements().Count > 0;
        }

        public bool HasAvailableResearch()
        {
            return GetAvailableResearchItems().Count > 0;
        }

        public string CurrentTierCostText
        {
            get
            {
                if (SelectedTier == 1 && IsVisible(GetField<UITransform>(_menu, PurchaseAreaField) as Component))
                {
                    string visibleCost = LargeCostText;
                    if (!string.IsNullOrWhiteSpace(visibleCost))
                    {
                        return FormatCostText(visibleCost);
                    }
                }

                if (SelectedTier > 1)
                {
                    string sectionCost = GetSectionBody("Adventure/BuildMenu/Cost", "Cost");
                    if (!string.IsNullOrWhiteSpace(sectionCost))
                    {
                        return FormatCostText(sectionCost);
                    }
                }

                return FormatCostText(BuildStructuredCostText());
            }
        }

        public bool HasCurrentTierCost()
        {
            return !string.IsNullOrWhiteSpace(CurrentTierCostText);
        }

        public bool HasWarning()
        {
            return !string.IsNullOrWhiteSpace(CannotBuyText);
        }

        public IReadOnlyList<DetailItem> GetDetailItems()
        {
            List<DetailItem> items = new List<DetailItem>();
            AddIfNotEmpty(items, "build-detail-summary", string.Empty, SelectedBuildingSummary);
            AddVisibleDescriptionSections(items);
            AddIfNotEmpty(items, "build-detail-cost", "Cost", CurrentTierCostText);
            AddIfNotEmpty(items, "build-detail-warning", "Warning", CannotBuyText);
            if (items.Count == 0)
            {
                items.Add(new DetailItem("build-detail-none", "Details", "No details"));
            }

            return items;
        }

        public string GetDetailsText()
        {
            IReadOnlyList<DetailItem> details = GetDetailItems();
            List<string> lines = new List<string>();
            for (int i = 0; i < details.Count; i++)
            {
                string label = details[i] != null ? details[i].Label : string.Empty;
                if (!string.IsNullOrWhiteSpace(label) && !lines.Contains(label))
                {
                    lines.Add(label);
                }
            }

            return string.Join("\n", lines.ToArray());
        }

        public string CannotBuyText
        {
            get
            {
                GameObject container = GetField<GameObject>(_menu, CannotBuyContainerField);
                if (!IsVisible(container))
                {
                    return string.Empty;
                }

                return GetText(GetField<UITextMesh>(_menu, CannotBuyTextField));
            }
        }

        public bool IsBuildButtonVisible()
        {
            return IsVisible(GetField<GameObject>(_menu, PurchaseButtonContainerField));
        }

        public bool IsBuildButtonEnabled()
        {
            return IsButtonEnabled(GetPurchaseButton()) && IsBuildButtonVisible();
        }

        public string BuildButtonLabel
        {
            get
            {
                string label = GetButtonLabel(GetPurchaseButton());
                return string.IsNullOrWhiteSpace(label) ? "Build" : label;
            }
        }

        public bool ActivateBuild()
        {
            return NativeSelectionUtility.Click(GetPurchaseButton());
        }

        public void FocusBuildButton()
        {
            NativeSelectionUtility.Select(GetPurchaseButton() as Component);
        }

        public bool ActivatePreviousBuildSite()
        {
            return ActivateBuildSiteNavigation(GetPreviousBuildSiteButton(), HandleLeftNavigationButtonClickedMethod);
        }

        public bool ActivateNextBuildSite()
        {
            return ActivateBuildSiteNavigation(GetNextBuildSiteButton(), HandleRightNavigationButtonClickedMethod);
        }

        public string PreviousBuildSiteButtonLabel
        {
            get
            {
                string label = GetButtonLabel(GetPreviousBuildSiteButton());
                return string.IsNullOrWhiteSpace(label) ? "Previous" : label;
            }
        }

        public string NextBuildSiteButtonLabel
        {
            get
            {
                string label = GetButtonLabel(GetNextBuildSiteButton());
                return string.IsNullOrWhiteSpace(label) ? "Next" : label;
            }
        }

        public bool IsPreviousBuildSiteEnabled()
        {
            return IsButtonEnabled(GetPreviousBuildSiteButton());
        }

        public bool IsNextBuildSiteEnabled()
        {
            return IsButtonEnabled(GetNextBuildSiteButton());
        }

        public bool IsAutoSelectVisible()
        {
            return IsVisible(GetAutoSelectToggle() as Component);
        }

        public bool IsAutoSelectChecked()
        {
            UIToggle toggle = GetAutoSelectToggle();
            return toggle != null && toggle.ToggleValue;
        }

        public void ToggleAutoSelect()
        {
            UIToggle toggle = GetAutoSelectToggle();
            if (toggle != null)
            {
                toggle.ToggleValue = !toggle.ToggleValue;
            }
        }

        public bool Close()
        {
            if (_menu == null || !_menu.IsOpen)
            {
                return false;
            }

            _menu.Close();
            return true;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private IMapEntity CurrentBuildSite
        {
            get { return GetField<IMapEntity>(_menu, CurrentBuildSiteField); }
        }

        private BuildOnBuildSiteAction CurrentAction
        {
            get { return GetField<BuildOnBuildSiteAction>(_menu, SelectedActionField); }
        }

        private int SiblingIndex
        {
            get
            {
                object value = SiblingIndexField != null ? SiblingIndexField.GetValue(_menu) : null;
                return value is int ? (int)value : -1;
            }
        }

        private int SiblingCount
        {
            get
            {
                IList siblings = GetField<IList>(_menu, AllSiblingsField);
                return siblings != null ? siblings.Count : 0;
            }
        }

        private AdventureMapEntityLevelDetails CurrentLevelDetails
        {
            get
            {
                object value = DetailsField != null ? DetailsField.GetValue(_menu) : null;
                if (!(value is AdventureMapEntityDetails))
                {
                    return null;
                }

                AdventureMapEntityDetails details = (AdventureMapEntityDetails)value;
                if (details.LevelDetails == null || details.LevelDetails.Count == 0)
                {
                    return null;
                }

                int index = Math.Max(0, Math.Min(SelectedTier - 1, details.LevelDetails.Count - 1));
                return details.LevelDetails[index];
            }
        }

        private string LargeCostText
        {
            get
            {
                LargeCostSection section = GetField<LargeCostSection>(_menu, LargeCostSectionField);
                if (section == null)
                {
                    return string.Empty;
                }

                List<string> parts = new List<string>();
                AddCostPart(parts, section, GoldCostEntryField, GoldAmountTextField, "gold");
                AddCostPart(parts, section, StoneCostEntryField, StoneAmountTextField, "stone");
                AddCostPart(parts, section, WoodCostEntryField, WoodAmountTextField, "wood");
                AddCostPart(parts, section, GlimmerWeaveCostEntryField, GlimmerWeaveAmountTextField, "glimmerweave");
                AddCostPart(parts, section, AncientAmberCostEntryField, AncientAmberAmountTextField, "ancient amber");
                AddCostPart(parts, section, CelestialOreCostEntryField, CelestialOreAmountTextField, "celestial ore");
                if (parts.Count > 0)
                {
                    return string.Join(", ", parts.ToArray());
                }

                return BuildStructuredCostText();
            }
        }

        private string BuildStructuredCostText()
        {
            AdventureMapEntityLevelDetails level = CurrentLevelDetails;
            Cost cost = level != null && level.Requirements != null && level.Requirements.Requirements != null
                ? level.Requirements.Requirements.Cost
                : null;
            if (cost == null || cost.SortedCostEntries == null || cost.SortedCostEntries.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < cost.SortedCostEntries.Count; i++)
            {
                Cost.CostEntry entry = cost.SortedCostEntries[i];
                if (entry.Amount <= 0)
                {
                    continue;
                }

                parts.Add(entry.Amount + " " + FormatResource(entry.Type));
            }

            return string.Join(", ", parts.ToArray());
        }

        private void AddVisibleDescriptionSections(List<DetailItem> items)
        {
            IReadOnlyList<BuildMenuDescriptionSection> sections = GetActiveDescriptionSections();
            int detailIndex = 0;
            for (int i = 0; i < sections.Count; i++)
            {
                BuildMenuDescriptionSection section = sections[i];
                if (!IsVisible(section as Component))
                {
                    continue;
                }

                string header = GetText(GetField<UITextMesh>(section, DescriptionSectionHeaderField));
                List<string> lines = new List<string>();
                BuildMenuDescriptionEntry[] entries = section.GetComponentsInChildren<BuildMenuDescriptionEntry>(false);
                for (int j = 0; j < entries.Length; j++)
                {
                    string text = GetText(GetField<UITextMesh>(entries[j], DescriptionEntryTextField));
                    if (!string.IsNullOrWhiteSpace(text) && !lines.Contains(text))
                    {
                        lines.Add(text);
                    }
                }

                string body = string.Join(". ", lines.ToArray());
                AddIfNotEmpty(items, "build-detail-section-" + detailIndex, header, body);
                detailIndex++;
            }
        }

        private IReadOnlyList<SectionItem> GetVisibleSectionItems(string localizationKey, string idPrefix)
        {
            BuildMenuDescriptionSection section = GetVisibleSection(localizationKey, null);
            return section != null ? GetSectionItems(section, idPrefix) : new SectionItem[0];
        }

        private IReadOnlyList<SectionItem> GetSectionItems(BuildMenuDescriptionSection section, string idPrefix)
        {
            List<SectionItem> items = new List<SectionItem>();
            if (section == null)
            {
                return items;
            }

            BuildMenuDescriptionEntry[] entries = section.GetComponentsInChildren<BuildMenuDescriptionEntry>(false);
            for (int i = 0; i < entries.Length; i++)
            {
                BuildMenuDescriptionEntry entry = entries[i];
                string text = GetText(GetField<UITextMesh>(entry, DescriptionEntryTextField));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                BuildMenuDescriptionEntry captured = entry;
                items.Add(new SectionItem(
                    idPrefix + "-" + items.Count,
                    text,
                    () => FocusEntry(captured),
                    () => GetEntryTooltip(captured)));
            }

            return items;
        }

        private BuildMenuDescriptionEntry[] GetSectionEntries(string localizationKey, string fallbackHeader)
        {
            BuildMenuDescriptionSection section = GetVisibleSection(localizationKey, fallbackHeader);
            return section != null ? section.GetComponentsInChildren<BuildMenuDescriptionEntry>(false) : new BuildMenuDescriptionEntry[0];
        }

        private string GetSectionBody(string localizationKey, string fallbackHeader)
        {
            BuildMenuDescriptionSection section = GetVisibleSection(localizationKey, fallbackHeader);
            if (section == null)
            {
                return string.Empty;
            }

            List<string> lines = new List<string>();
            BuildMenuDescriptionEntry[] entries = section.GetComponentsInChildren<BuildMenuDescriptionEntry>(false);
            for (int i = 0; i < entries.Length; i++)
            {
                string text = GetText(GetField<UITextMesh>(entries[i], DescriptionEntryTextField));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }

            return string.Join(". ", lines.ToArray());
        }

        private BuildMenuDescriptionSection GetVisibleSection(string localizationKey, string fallbackHeader)
        {
            IReadOnlyList<BuildMenuDescriptionSection> sections = GetActiveDescriptionSections();
            for (int i = 0; i < sections.Count; i++)
            {
                BuildMenuDescriptionSection section = sections[i];
                if (!IsVisible(section as Component))
                {
                    continue;
                }

                if (IsSectionHeader(GetSectionHeader(section), localizationKey, fallbackHeader))
                {
                    return section;
                }
            }

            return null;
        }

        private bool IsSectionHeader(string header, string localizationKey, string fallbackHeader)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return false;
            }

            string localized = GetLocalizedText(localizationKey, fallbackHeader);
            return string.Equals(header, localized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(header, fallbackHeader, StringComparison.OrdinalIgnoreCase);
        }

        private string GetSectionHeader(BuildMenuDescriptionSection section)
        {
            return GetText(GetField<UITextMesh>(section, DescriptionSectionHeaderField));
        }

        private void FocusEntry(BuildMenuDescriptionEntry entry)
        {
            Component target = GetEntryTooltipComponent(entry);
            if (target != null)
            {
                NativeSelectionUtility.Select(target);
                NativeTooltipUtility.ShowTooltipForComponent(target);
            }
        }

        private Tooltip GetEntryTooltip(BuildMenuDescriptionEntry entry)
        {
            return Tooltip.ForComponent(GetEntryTooltipComponent(entry), _localization);
        }

        private Component GetEntryTooltipComponent(BuildMenuDescriptionEntry entry)
        {
            Component background = GetField<UIImage>(entry, DescriptionEntryBackgroundField) as Component;
            if (background != null)
            {
                return background;
            }

            return GetField<UIImage>(entry, DescriptionEntryIconField) as Component;
        }

        private bool FocusBuilding(BuildMenuButton buildButton)
        {
            UIButton button = GetBuildButton(buildButton);
            NativeSelectionUtility.Select(button as Component);
            if (buildButton != null && ReferenceEquals(buildButton.BuildAction, CurrentAction))
            {
                return true;
            }

            return NativeSelectionUtility.Click(button);
        }

        private IReadOnlyList<BuildMenuButton> GetActiveBuildButtons()
        {
            List<BuildMenuButton> buttons = new List<BuildMenuButton>();
            foreach (object entry in GetActivePoolEntries(GetField<object>(_menu, BuildMenuButtonPoolField)))
            {
                BuildMenuButton button = entry as BuildMenuButton;
                if (button != null && IsVisible(button as Component))
                {
                    buttons.Add(button);
                }
            }

            return buttons;
        }

        private IReadOnlyList<BuildMenuDescriptionSection> GetActiveDescriptionSections()
        {
            List<BuildMenuDescriptionSection> sections = new List<BuildMenuDescriptionSection>();
            foreach (object entry in GetActivePoolEntries(GetField<object>(_menu, BuildMenuIncomePoolField)))
            {
                BuildMenuDescriptionSection section = entry as BuildMenuDescriptionSection;
                if (section != null)
                {
                    sections.Add(section);
                }
            }

            return sections;
        }

        private static IEnumerable<object> GetActivePoolEntries(object pool)
        {
            if (pool == null)
            {
                yield break;
            }

            PropertyInfo property = pool.GetType().GetProperty("ActiveEntries");
            IEnumerable entries = property != null ? property.GetValue(pool, null) as IEnumerable : null;
            if (entries == null)
            {
                yield break;
            }

            foreach (object entry in entries)
            {
                yield return entry;
            }
        }

        private UIButton GetBuildButton(BuildMenuButton buildButton)
        {
            return GetField<UIButton>(buildButton, BuildMenuButtonButtonField);
        }

        private UIButton GetTutorialButton()
        {
            return GetField<UIButton>(_menu, TutorialButtonField);
        }

        private UIButton GetPreviousBuildSiteButton()
        {
            return GetField<UIButton>(_menu, LeftNavigationButtonField);
        }

        private UIButton GetNextBuildSiteButton()
        {
            return GetField<UIButton>(_menu, RightNavigationButtonField);
        }

        private UIButton GetSmallTabButton()
        {
            return GetField<UIButton>(_menu, SmallBuildingsTabButtonField);
        }

        private UIButton GetMediumTabButton()
        {
            return GetField<UIButton>(_menu, MediumBuildingsTabButtonField);
        }

        private UIButton GetLargeTabButton()
        {
            return GetField<UIButton>(_menu, LargeBuildingsTabButtonField);
        }

        private UIButton GetCategoryButton(BuildSiteSize size)
        {
            switch (size)
            {
                case BuildSiteSize.Medium:
                    return GetMediumTabButton();
                case BuildSiteSize.Large:
                    return GetLargeTabButton();
                default:
                    return GetSmallTabButton();
            }
        }

        private UIButton GetTierButton(int level)
        {
            BuildMenuHeaderSection header = GetField<BuildMenuHeaderSection>(_menu, HeaderSectionField);
            IDictionary<int, UIButton> buttons = GetField<Dictionary<int, UIButton>>(header, HeaderLevelToButtonField);
            if (buttons == null || !buttons.ContainsKey(level))
            {
                return null;
            }

            return buttons[level];
        }

        private UIToggle GetAutoSelectToggle()
        {
            return GetField<UIToggle>(_menu, AutoSelectBuildSiteToggleField);
        }

        private UIButton GetPurchaseButton()
        {
            return GetField<UIButton>(_menu, PurchaseButtonField);
        }

        private static void AddCostPart(List<string> parts, LargeCostSection section, FieldInfo entryField, FieldInfo textField, string resourceName)
        {
            UITransform entry = GetField<UITransform>(section, entryField);
            if (entry == null || !entry.Active)
            {
                return;
            }

            string amount = GetText(GetField<UITextMesh>(section, textField));
            if (!string.IsNullOrWhiteSpace(amount))
            {
                parts.Add(amount + " " + resourceName);
            }
        }

        private string BuildCategoryLabel(string baseLabel, BuildSiteSize size)
        {
            string buildTime = BuildTimeForSize(size);
            return string.IsNullOrWhiteSpace(buildTime)
                ? baseLabel
                : baseLabel + ", " + buildTime;
        }

        private string FormatCostText(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            return GetLocalizedText("Adventure/BuildMenu/Cost", "Cost").TrimEnd(':') + ": " + body;
        }

        private string BuildTimeForSize(BuildSiteSize size)
        {
            int rounds;
            switch (size)
            {
                case BuildSiteSize.Medium:
                    rounds = GetGameConfigInt("towns.constructionRoundsForMediumBuildings", 2);
                    break;
                case BuildSiteSize.Large:
                    rounds = GetGameConfigInt("towns.constructionRoundsForLargeBuildings", 3);
                    break;
                default:
                    rounds = GetGameConfigInt("towns.constructionRoundsForSmallBuildings", 1);
                    break;
            }

            string label = GetLocalizedText("Adventure/Tooltips/Build/BuildTimeLabel", "Build time");
            string value = _localization != null
                ? SpeechTextSanitizer.Normalize(_localization.GetPluralText("Adventure/Tooltips/Build/BuildTime", rounds, rounds))
                : rounds + (rounds == 1 ? " round" : " rounds");
            return label + ": " + value;
        }

        private string FormatRequiredBuilding(RequiredBuilding building)
        {
            IMapEntityBlueprint blueprint = _facade != null ? _facade.MapEntities.GetBlueprint((ushort)building.entity) : null;
            string name = blueprint != null && _localization != null
                ? SpeechTextSanitizer.Normalize(_localization.GetText(blueprint.NameKey))
                : building.entity.ToString();

            if (_localization == null)
            {
                return name;
            }

            if (building.minLevel > 1)
            {
                var data = new
                {
                    mapEntityName = name,
                    amountNeeded = building.count.ToString(),
                    minLevel = building.minLevel.ToString()
                };
                return SpeechTextSanitizer.Normalize(_localization.GetPluralText("Common/Details/RequiredBuildings/EntryLevel", building.count, data));
            }

            var simpleData = new
            {
                mapEntityName = name,
                amountNeeded = building.count.ToString()
            };
            return SpeechTextSanitizer.Normalize(_localization.GetPluralText("Common/Details/RequiredBuildings/Entry", building.count, simpleData));
        }

        private string FormatRequiredResearch(object research)
        {
            ResearchDetails? details = GetResearchDetails(research);
            if (!details.HasValue || _localization == null)
            {
                return research != null ? research.ToString() : string.Empty;
            }

            ResearchDetails value = details.Value;
            string name = SpeechTextSanitizer.Normalize(_localization.GetText(value.Description.NameKey));
            string source = SpeechTextSanitizer.Normalize(_localization.GetText(value.ResearchMapEntityNameKey));
            return string.IsNullOrWhiteSpace(source) ? name : name + " (" + source + ")";
        }

        private bool ValidateResearchRequirement(object research, int buildSiteId)
        {
            if (_buildingRequirementValidator == null || research == null || _facade == null)
            {
                return false;
            }

            MethodInfo method = _buildingRequirementValidator.GetType().GetMethods()
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "Validate")
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length == 3
                        && parameters[0].ParameterType.IsInstanceOfType(research)
                        && parameters[1].ParameterType == typeof(int)
                        && parameters[2].ParameterType == typeof(int);
                });
            if (method == null)
            {
                return false;
            }

            object result = method.Invoke(_buildingRequirementValidator, new object[] { research, buildSiteId, _facade.Teams.LocalTeamInControlId });
            return result is bool && (bool)result;
        }

        private ResearchDetails? GetResearchDetails(object research)
        {
            if (_researchLookup == null || research == null)
            {
                return null;
            }

            MethodInfo method = _researchLookup.GetType().GetMethods()
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "GetDetails")
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(research);
                });
            if (method == null)
            {
                return null;
            }

            object result = method.Invoke(_researchLookup, new[] { research });
            return result is ResearchDetails ? (ResearchDetails)result : (ResearchDetails?)null;
        }

        private int GetGameConfigInt(string key, int fallback)
        {
            if (_gameConfig == null)
            {
                return fallback;
            }

            MethodInfo method = _gameConfig.GetType().GetMethods()
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "GetValue" || !candidate.IsGenericMethodDefinition)
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length == 2
                        && parameters[0].ParameterType == typeof(string);
                });
            if (method == null)
            {
                return fallback;
            }

            try
            {
                object value = method.MakeGenericMethod(typeof(int)).Invoke(_gameConfig, new object[] { key, fallback });
                return value is int ? (int)value : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string PrefixMissing(string label, bool met)
        {
            return met ? label : "Missing " + label;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            if (_localization == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            string text = SpeechTextSanitizer.Normalize(_localization.GetText(key));
            return string.IsNullOrWhiteSpace(text) ? fallback ?? string.Empty : text;
        }

        private static void AddIfNotEmpty(List<DetailItem> items, string id, string header, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            items.Add(new DetailItem(id, header, body));
        }

        private static string JoinParts(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first;
            }

            return first.TrimEnd('.') + ". " + second;
        }

        private static string FormatSize(BuildSiteSize size)
        {
            return size.ToString().ToLowerInvariant();
        }

        private static string FormatResource(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.AncientAmber:
                    return "ancient amber";
                case ResourceType.CelestialOre:
                    return "celestial ore";
                case ResourceType.Glimmerweave:
                    return "glimmerweave";
                default:
                    return type.ToString().ToLowerInvariant();
            }
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable && IsVisible(button as Component);
        }

        private bool InvokeNativeHandler(MethodInfo method)
        {
            if (_menu == null || method == null)
            {
                return false;
            }

            method.Invoke(_menu, null);
            return true;
        }

        private bool ActivateBuildSiteNavigation(UIButton button, MethodInfo method)
        {
            if (!IsButtonEnabled(button))
            {
                return false;
            }

            int before = CurrentBuildSite != null ? CurrentBuildSite.Id : -1;
            if (!InvokeNativeHandler(method))
            {
                return false;
            }

            int after = CurrentBuildSite != null ? CurrentBuildSite.Id : -1;
            return before != after;
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class CategoryItem
        {
            public CategoryItem(string id, string label, int index, BuildSiteSize size, bool enabled)
            {
                Id = id;
                Label = label;
                Index = index;
                Size = size;
                Enabled = enabled;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public int Index { get; private set; }
            public BuildSiteSize Size { get; private set; }
            public bool Enabled { get; private set; }
        }

        internal sealed class BuildingItem
        {
            public BuildingItem(string id, string label, Func<string> getStatus, Func<bool> focus, Func<Tooltip> tooltip)
            {
                Id = id;
                Label = label;
                GetStatus = getStatus;
                Focus = focus;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public Func<string> GetStatus { get; private set; }
            public Func<bool> Focus { get; private set; }
            public Func<Tooltip> Tooltip { get; private set; }
        }

        internal sealed class TierItem
        {
            public TierItem(string id, string label, int level, Func<bool> focus, Func<Tooltip> tooltip)
            {
                Id = id;
                Label = label;
                Level = level;
                Focus = focus;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public int Level { get; private set; }
            public Func<bool> Focus { get; private set; }
            public Func<Tooltip> Tooltip { get; private set; }
        }

        internal sealed class SectionMenu
        {
            public SectionMenu(string id, string label, IReadOnlyList<SectionItem> items)
            {
                Id = id;
                Label = label ?? string.Empty;
                Items = items ?? new SectionItem[0];
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public IReadOnlyList<SectionItem> Items { get; private set; }
        }

        internal sealed class SectionItem
        {
            public SectionItem(string id, string label, Action focus, Func<Tooltip> tooltip)
            {
                Id = id;
                Label = label ?? string.Empty;
                Focus = focus;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public Action Focus { get; private set; }
            public Func<Tooltip> Tooltip { get; private set; }
        }

        internal sealed class RequirementItem
        {
            public RequirementItem(string id, string label, Tooltip tooltip)
            {
                Id = id;
                Label = label ?? string.Empty;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

        internal sealed class DetailItem
        {
            public DetailItem(string id, string header, string body)
            {
                Id = id;
                Header = header ?? string.Empty;
                Body = body ?? string.Empty;
            }

            public string Id { get; private set; }
            public string Header { get; private set; }
            public string Body { get; private set; }

            public string Label
            {
                get { return JoinParts(Header, Body); }
            }
        }
    }
}
