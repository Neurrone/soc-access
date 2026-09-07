using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.GameActions;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Research;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class ResearchMenuAdapter
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
        private static readonly FieldInfo FactionLookupField = AccessTools.Field(typeof(ResearchMenu), "_factionLookup");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(ResearchMenu), "_localizationHandler");
        private static readonly FieldInfo MixedFactionsContainerField = AccessTools.Field(typeof(ResearchMenu), "_mixedFactionsContainer");
        private static readonly FieldInfo MixedFactionButtonsField = AccessTools.Field(typeof(ResearchMenu), "_mixedFactionButtons");
        private static readonly FieldInfo SelectedFactionIndexField = AccessTools.Field(typeof(ResearchMenu), "_selectedFactionIndex");
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(ResearchMenu), "_headerText");
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

        /// <summary>The header the menu draws over the page. <c>ResearchMenu.SetHeaderText</c> writes
        /// "Research" into it, and appends the faction on a mixed-factions map, so it is the one place
        /// that says which faction's research is showing.</summary>
        public string HeaderText
        {
            get { return GetText(GetField<UITextMesh>(_menu, HeaderTextField)); }
        }

        /// <summary>The tutorial button the menu draws at the top left.</summary>
        public Component TutorialButton
        {
            get { return GetTutorialButton() as Component; }
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

        public bool HasFactionSelector()
        {
            return IsVisible(GetField<RectTransform>(_menu, MixedFactionsContainerField) as Component)
                && GetFactions().Count > 0;
        }

        public IReadOnlyList<FactionItem> GetFactions()
        {
            List<FactionItem> items = new List<FactionItem>();
            UIButton[] buttons = GetFactionButtons();
            IFactionLookup factionLookup = GetField<IFactionLookup>(_menu, FactionLookupField);
            int selectedFactionIndex = SelectedFactionIndex;
            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                if (!IsVisible(button as Component))
                {
                    continue;
                }

                int factionIndex = i + 1;
                IFactionDefinition faction = factionLookup != null ? factionLookup.GetFaction(factionIndex) : null;
                string label = Localize(faction != null ? faction.NameKey : null, string.Empty);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                items.Add(new FactionItem(
                    factionIndex,
                    label,
                    factionIndex == selectedFactionIndex,
                    button as Component,
                    () => SelectFaction(button),
                    () => ActivateFaction(factionIndex, button)));
            }

            return items;
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
                IList<string> description = GetBuildingDescription(tab);
                int mapEntityId = BuildingTabMapEntityIdField != null ? (int)BuildingTabMapEntityIdField.GetValue(tab) : 0;
                items.Add(new BuildingItem(
                    label,
                    description,
                    mapEntityId < 0,
                    index == SelectedBuildingIndex,
                    button as Component,
                    () => SelectBuilding(button),
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
                    string name = Localize(stack != null ? stack.NameKey : null, "Research " + (itemIndex + 1));
                    researchItems.Add(new ResearchItem(
                        name,
                        GetOwnedTier(stack),
                        GetTierHeader(),
                        button as Component,
                        () => button != null && button.Active && button.Interactable,
                        () => FocusResearch(button),
                        () => NativeSelectionUtility.Click(button),
                        BuildResearchTooltip(button)));
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

        /// <summary>Close the menu the way the game closes it: the blocker click and the gamepad
        /// cancel both go to <c>ResearchMenu.Hide</c>, which unregisters the menu's input, restores
        /// the HUD and completes the request.</summary>
        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        // Arriving at a tab must not switch to it: switching respawns the categories and their stack
        // buttons, so the page under the cursor would be replaced by merely walking the bar. Focus
        // therefore only moves the game's own selection; the click is Enter's.
        private bool SelectBuilding(UIButton button)
        {
            return NativeSelectionUtility.Select(button as Component);
        }

        private bool ActivateBuilding(UIButton button)
        {
            return ClickBuilding(button);
        }

        private bool SelectFaction(UIButton button)
        {
            return NativeSelectionUtility.Select(button as Component);
        }

        private bool ActivateFaction(int factionIndex, UIButton button)
        {
            HideNativeTooltip();
            NativeSelectionUtility.Select(button as Component);
            return SelectedFactionIndex == factionIndex || NativeSelectionUtility.Click(button);
        }

        private static bool ClickBuilding(UIButton button)
        {
            return NativeSelectionUtility.PointerClick(button as Component);
        }

        // The row's focus visual is the game's own selection; the tooltip beside it is drawn by the
        // navigator, off the tooltip the node points at.
        private bool FocusResearch(UIButton button)
        {
            return NativeSelectionUtility.Select(button as Component);
        }

        private static string GetText(UITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private string GetBuildingLabel(ResearchMenuBuildingTabButton tab, int index)
        {
            UITextMesh name = GetField<UITextMesh>(tab, BuildingTabNameField);
            string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(name));
            return string.IsNullOrWhiteSpace(label) ? "Building " + (index + 1) : label;
        }

        // The paragraphs the game wrote the tab's description in, kept apart rather than collapsed.
        private IList<string> GetBuildingDescription(ResearchMenuBuildingTabButton tab)
        {
            UITextMesh description = GetField<UITextMesh>(tab, BuildingTabDescriptionField);
            return SpokenLines.Of(new[] { UITextMeshTextUtility.GetEffectiveText(description) });
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

        private Tooltip BuildResearchTooltip(UIButton button)
        {
            Component component = button as Component;
            if (component == null)
            {
                return null;
            }

            // No structured actions: Enter on the row IS the buy, so a tooltip action naming it would
            // be a second door onto the same click.
            return new Tooltip(
                () => CaptureResearchTooltip(component).TextLines,
                VisualTooltipMetadata.ForComponent(component, component.GetComponent<RectTransform>(), ResearchTooltipAnchors));
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

        private int SelectedFactionIndex
        {
            get
            {
                return _menu != null && SelectedFactionIndexField != null
                    ? (int)SelectedFactionIndexField.GetValue(_menu)
                    : 0;
            }
        }

        private UIButton[] GetFactionButtons()
        {
            return GetField<UIButton[]>(_menu, MixedFactionButtonsField) ?? new UIButton[0];
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

        public sealed class FactionItem
        {
            public FactionItem(int factionIndex, string label, bool isSelected, Component button, Func<bool> focus, Func<bool> activate)
            {
                FactionIndex = factionIndex;
                Label = label ?? string.Empty;
                IsSelected = isSelected;
                Button = button;
                Focus = focus;
                Activate = activate;
            }

            public int FactionIndex { get; private set; }
            public string Label { get; private set; }
            public bool IsSelected { get; private set; }

            /// <summary>The faction's own button - what the strip is drawn by.</summary>
            public Component Button { get; private set; }

            /// <summary>Move the game's selection here, without switching to the faction.</summary>
            public Func<bool> Focus { get; private set; }

            public Func<bool> Activate { get; private set; }
        }

        public sealed class BuildingItem
        {
            public BuildingItem(
                string label,
                IList<string> description,
                bool missingBuilding,
                bool isSelected,
                Component button,
                Func<bool> focus,
                Func<bool> activate)
            {
                Label = label ?? string.Empty;
                DescriptionLines = description ?? new List<string>();
                MissingBuilding = missingBuilding;
                IsSelected = isSelected;
                Button = button;
                Focus = focus;
                Activate = activate;
            }

            public string Label { get; private set; }

            /// <summary>What the tab draws under its name, one line per paragraph.</summary>
            public IList<string> DescriptionLines { get; private set; }

            /// <summary>The team owns no building of this kind, so nothing under the tab can be
            /// bought; the menu draws its "missing building" label over the page.</summary>
            public bool MissingBuilding { get; private set; }

            /// <summary>The tab the menu is showing.</summary>
            public bool IsSelected { get; private set; }

            /// <summary>The tab's own button - what the tab is drawn by.</summary>
            public Component Button { get; private set; }

            /// <summary>Move the game's selection here, without switching to the tab.</summary>
            public Func<bool> Focus { get; private set; }

            public Func<bool> Activate { get; private set; }
        }

        public sealed class CategoryItem
        {
            public CategoryItem(string label, IReadOnlyList<ResearchItem> items)
            {
                Label = label ?? string.Empty;
                Items = items ?? new ResearchItem[0];
            }

            public string Label { get; private set; }
            public IReadOnlyList<ResearchItem> Items { get; private set; }
        }

        public sealed class ResearchItem
        {
            public ResearchItem(
                string label,
                int ownedTier,
                string tierHeader,
                Component button,
                Func<bool> isEnabled,
                Func<bool> focus,
                Func<bool> activate,
                Tooltip tooltip)
            {
                Label = label ?? string.Empty;
                OwnedTier = ownedTier;
                TierHeader = tierHeader ?? string.Empty;
                Button = button;
                IsEnabled = isEnabled;
                Focus = focus;
                Activate = activate;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public int OwnedTier { get; private set; }
            public string TierHeader { get; private set; }

            /// <summary>The stack's own button - what the row is drawn by.</summary>
            public Component Button { get; private set; }

            /// <summary>Whether the game would answer a click here: the menu turns a stack that
            /// cannot be bought non-interactable.</summary>
            public Func<bool> IsEnabled { get; private set; }

            public Func<bool> Focus { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }
    }
}
