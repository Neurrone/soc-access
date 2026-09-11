using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest;
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
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class ResearchMenuAdapter : IPresent
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

        // A category's stack buttons, walked at most once a frame per category: the build asks for
        // every category and every row on every frame, and each category was a fresh subtree walk.
        private readonly FrameSweep<ResearchMenuStackButton> _categoryButtons =
            new FrameSweep<ResearchMenuStackButton>("research menu category", inactiveToo: false);

        public ResearchMenuAdapter(ResearchMenu menu)
        {
            _menu = menu;
        }

        /// <summary>Drop what is kept for the current frame only. Switching a building tab or a
        /// faction respawns the categories and re-pools their stack buttons, so a read later in the
        /// same frame must not see the page the build read before the click.</summary>
        public void InvalidateFrameSnapshots()
        {
            _categoryButtons.Invalidate();
        }

        public IClientAdventureFacade Facade
        {
            get { return Reflect.Get<IClientAdventureFacade>(_menu, FacadeField); }
        }

        public bool IsPresent()
        {
            GameObject container = Reflect.Get<GameObject>(_menu, ContainerField);
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
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, HeaderTextField)); }
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
            return IsVisible(Reflect.Get<RectTransform>(_menu, MixedFactionsContainerField) as Component)
                && GetFactions().Count > 0;
        }

        public IReadOnlyList<FactionItem> GetFactions()
        {
            List<FactionItem> items = new List<FactionItem>();
            UIButton[] buttons = GetFactionButtons();
            IFactionLookup factionLookup = Reflect.Get<IFactionLookup>(_menu, FactionLookupField);
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
                string label = SpokenText.Get(GetLocalization(), faction != null ? faction.NameKey : null, string.Empty);
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
            // One localization lookup for the whole page rather than one per row.
            string tierHeader = GetTierHeader();
            HashSet<ResearchTypes> owned = GetOwnedGlobalResearch();
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
                    string name = SpokenText.Get(GetLocalization(), stack != null ? stack.NameKey : null, "Research " + (itemIndex + 1));
                    researchItems.Add(new ResearchItem(
                        name,
                        GetOwnedTier(stack, owned),
                        tierHeader,
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
            bool clicked = ClickBuilding(button);
            InvalidateFrameSnapshots();
            return clicked;
        }

        private bool SelectFaction(UIButton button)
        {
            return NativeSelectionUtility.Select(button as Component);
        }

        private bool ActivateFaction(int factionIndex, UIButton button)
        {
            HideNativeTooltip();
            NativeSelectionUtility.Select(button as Component);
            if (SelectedFactionIndex == factionIndex)
            {
                return true;
            }

            bool clicked = NativeSelectionUtility.Click(button);
            InvalidateFrameSnapshots();
            return clicked;
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

        private string GetBuildingLabel(ResearchMenuBuildingTabButton tab, int index)
        {
            UITextMesh name = Reflect.Get<UITextMesh>(tab, BuildingTabNameField);
            string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(name));
            return string.IsNullOrWhiteSpace(label) ? "Building " + (index + 1) : label;
        }

        // The paragraphs the game wrote the tab's description in, kept apart rather than collapsed.
        private IList<string> GetBuildingDescription(ResearchMenuBuildingTabButton tab)
        {
            UITextMesh description = Reflect.Get<UITextMesh>(tab, BuildingTabDescriptionField);
            return SpokenLines.Of(new[] { UITextMeshTextUtility.GetEffectiveText(description) });
        }

        private string GetCategoryLabel(ResearchMenuCategory category, int index)
        {
            UITextMesh name = Reflect.Get<UITextMesh>(category, CategoryNameField);
            string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(name));
            return string.IsNullOrWhiteSpace(label) ? "Research category " + (index + 1) : label;
        }

        /// <summary>The local team's global research, asked for once per page. The game answers
        /// <c>HasGlobalResearch</c> with a scan of every research state, and the page used to ask it
        /// once per tier of every row; <c>GetGlobal</c> with disabled states included is the same
        /// set in one scan.</summary>
        private HashSet<ResearchTypes> GetOwnedGlobalResearch()
        {
            IClientAdventureFacade facade = Facade;
            if (facade == null || facade.Research == null || facade.Teams == null)
            {
                return null;
            }

            IResearchState[] states = facade.Research.GetGlobal(facade.Teams.LocalTeamInControlId, includeDisabled: true);
            HashSet<ResearchTypes> owned = new HashSet<ResearchTypes>();
            for (int i = 0; states != null && i < states.Length; i++)
            {
                if (states[i] != null)
                {
                    owned.Add(states[i].Type);
                }
            }

            return owned;
        }

        private static int GetOwnedTier(ResearchStack stack, HashSet<ResearchTypes> owned)
        {
            if (stack == null || owned == null)
            {
                return 0;
            }

            List<ResearchDefinition> definitions = stack.Definitions;
            int ownedTier = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                ResearchDefinition definition = definitions[i];
                if (definition != null && owned.Contains(definition.ResearchType))
                {
                    ownedTier = i + 1;
                }
            }

            return ownedTier;
        }

        private string GetTierHeader()
        {
            return SpokenText.Get(GetLocalization(), "Adventure/KingdomInformationHUD/ResearchTierHeader", "Tier");
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
                VisualTooltipMetadata.ForComponent(component, component.GetComponent<RectTransform>(), ResearchTooltipAnchors),
                isLong: () => NativeTooltipUtility.IsLongForComponent(component));
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
            if (category == null)
            {
                return new ResearchMenuStackButton[0];
            }

            UITransform container = Reflect.Get<UITransform>(category, CategoryButtonsContainerField);
            return _categoryButtons.Under(container as Component);
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
            return Reflect.Get<UIButton[]>(_menu, MixedFactionButtonsField) ?? new UIButton[0];
        }

        private IReadOnlyList<ResearchMenuBuildingTabButton> GetTabButtons()
        {
            return Reflect.Get<List<ResearchMenuBuildingTabButton>>(_menu, AllTabButtonsField)
                ?? new List<ResearchMenuBuildingTabButton>();
        }

        private IReadOnlyList<ResearchMenuCategory> GetNativeCategories()
        {
            return Reflect.Get<List<ResearchMenuCategory>>(_menu, CategoriesField)
                ?? new List<ResearchMenuCategory>();
        }

        private DynamicUITabGroup GetBuildingsTabGroup()
        {
            return Reflect.Get<DynamicUITabGroup>(_menu, BuildingsTabGroupField);
        }

        private UIButton GetTutorialButton()
        {
            return Reflect.Get<UIButton>(_menu, TutorialButtonField);
        }

        private static UIButton GetButton(ResearchMenuBuildingTabButton tab)
        {
            return tab != null ? ((Component)tab).GetComponent<UIButton>() : null;
        }

        private ILocalizationHandler GetLocalization()
        {
            return Reflect.Get<ILocalizationHandler>(_menu, LocalizationField);
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
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
