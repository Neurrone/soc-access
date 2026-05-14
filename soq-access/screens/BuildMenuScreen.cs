using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class BuildMenuScreen : Screen
    {
        private readonly BuildMenuAdapter _adapter;

        public BuildMenuScreen(BuildMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            BuildMenu[] menus = Resources.FindObjectsOfTypeAll<BuildMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                BuildMenuAdapter adapter = new BuildMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new BuildMenuScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (RootWidget != null && RootWidget.HandleAction(action))
                {
                    return true;
                }

                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public BuildMenuScreen Rebuild()
        {
            return new BuildMenuScreen(_adapter);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot(_adapter);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private static ContainerWidget BuildRoot(BuildMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("build-menu", "Build");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                "build-tutorial",
                adapter.GetTutorialButtonLabel,
                adapter.ActivateTutorial,
                adapter.HideNativeTooltip,
                adapter.IsTutorialButtonVisible,
                adapter.IsTutorialButtonVisible));

            root.AddChild(new TextWidget(
                "build-current-site",
                () => adapter.BuildSiteSummary,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "build-previous-site",
                () => adapter.PreviousBuildSiteButtonLabel,
                adapter.ActivatePreviousBuildSite,
                adapter.HideNativeTooltip,
                adapter.IsPreviousBuildSiteEnabled));

            root.AddChild(new ButtonWidget(
                "build-next-site",
                () => adapter.NextBuildSiteButtonLabel,
                adapter.ActivateNextBuildSite,
                adapter.HideNativeTooltip,
                adapter.IsNextBuildSiteEnabled));

            root.AddChild(new CheckboxWidget(
                "build-auto-select-site",
                "Autoselect buildsite",
                adapter.ToggleAutoSelect,
                adapter.IsAutoSelectChecked,
                adapter.IsAutoSelectVisible));

            root.AddChild(BuildCategoryMenu(adapter));
            root.AddChild(BuildBuildingMenu(adapter));
            root.AddChild(new TextWidget(
                "build-summary",
                () => adapter.SelectedBuildingSummary,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.HasSelectedBuildingSummary));
            root.AddChild(BuildTierMenu(adapter));
            root.AddChild(BuildAvailableResearchMenu(adapter));
            AddIncomeAndGarrisonMenus(root, adapter);
            root.AddChild(BuildRequirementsMenu(adapter));
            root.AddChild(new TextWidget(
                "build-cost",
                () => adapter.CurrentTierCostText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.HasCurrentTierCost));
            root.AddChild(new TextWidget(
                "build-warning",
                () => adapter.CannotBuyText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.HasWarning));

            root.AddChild(new ButtonWidget(
                "build-purchase",
                () => adapter.BuildButtonLabel,
                adapter.ActivateBuild,
                adapter.FocusBuildButton,
                adapter.IsBuildButtonEnabled,
                adapter.IsBuildButtonVisible));

            root.AddChild(new ButtonWidget(
                "build-close",
                "Close",
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static MenuWidget BuildTierMenu(BuildMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("build-tiers", "Tier", () => adapter.GetTiers().Count > 0);
            IReadOnlyList<BuildMenuAdapter.TierItem> tiers = adapter.GetTiers();
            for (int i = 0; i < tiers.Count; i++)
            {
                BuildMenuAdapter.TierItem tier = tiers[i];
                menu.AddItem(new MenuItemWidget(
                    BuildTierId(tier),
                    () => tier.Label,
                    null,
                    tier.Focus,
                    () => tier.Focus(),
                    () => true,
                    tier.Tooltip));
            }

            menu.SetFocusedItemById("build-tier-" + adapter.SelectedTier);
            return menu;
        }

        private static MenuWidget BuildAvailableResearchMenu(BuildMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("build-available-research", adapter.AvailableResearchHeader, adapter.HasAvailableResearch);
            IReadOnlyList<BuildMenuAdapter.SectionItem> items = adapter.GetAvailableResearchItems();
            for (int i = 0; i < items.Count; i++)
            {
                BuildMenuAdapter.SectionItem item = items[i];
                menu.AddItem(new MenuItemWidget(
                    "build-available-research-" + i,
                    () => item.Label,
                    null,
                    () => false,
                    item.Focus,
                    () => true,
                    item.Tooltip));
            }

            return menu;
        }

        private static void AddIncomeAndGarrisonMenus(ContainerWidget root, BuildMenuAdapter adapter)
        {
            IReadOnlyList<BuildMenuAdapter.SectionMenu> menus = adapter.GetIncomeAndGarrisonMenus();
            for (int i = 0; i < menus.Count; i++)
            {
                BuildMenuAdapter.SectionMenu section = menus[i];
                string sectionId = "build-info-section-" + i;
                MenuWidget menu = new MenuWidget(sectionId, section.Label);
                for (int j = 0; j < section.Items.Count; j++)
                {
                    BuildMenuAdapter.SectionItem item = section.Items[j];
                    menu.AddItem(new MenuItemWidget(
                        sectionId + "-" + j,
                        () => item.Label,
                        null,
                        () => false,
                        item.Focus,
                        () => true,
                        item.Tooltip));
                }

                root.AddChild(menu);
            }
        }

        private static MenuWidget BuildRequirementsMenu(BuildMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("build-requirements", adapter.RequirementsHeader, adapter.HasRequirements);
            IReadOnlyList<BuildMenuAdapter.RequirementItem> requirements = adapter.GetRequirements();
            for (int i = 0; i < requirements.Count; i++)
            {
                BuildMenuAdapter.RequirementItem requirement = requirements[i];
                menu.AddItem(new MenuItemWidget(
                    "build-requirement-" + i,
                    () => BuildRequirementLabel(requirement),
                    () => requirement.IsMet ? string.Empty : "missing",
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true,
                    requirement.Tooltip));
            }

            return menu;
        }

        private static MenuWidget BuildCategoryMenu(BuildMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("build-size-tabs", "Building size");
            IReadOnlyList<BuildMenuAdapter.CategoryItem> categories = adapter.GetCategories();
            string activeId = null;
            for (int i = 0; i < categories.Count; i++)
            {
                BuildMenuAdapter.CategoryItem category = categories[i];
                BuildMenuAdapter.CategoryItem captured = category;
                string id = BuildCategoryId(captured);
                if (captured.Index == adapter.SelectedCategoryIndex)
                {
                    activeId = id;
                }

                menu.AddItem(new MenuItemWidget(
                    id,
                    () => captured.Label,
                    () => captured.Enabled ? string.Empty : "unavailable",
                    () => captured.Enabled && adapter.FocusCategory(captured.Size),
                    () =>
                    {
                        if (captured.Enabled)
                        {
                            adapter.FocusCategory(captured.Size);
                        }
                    },
                    () => true,
                    (SongsOfConquestAccess.Adapters.Tooltip)null,
                    null,
                    () => captured.Enabled));
            }

            menu.SetFocusedItemById(activeId);
            return menu;
        }

        private static MenuWidget BuildBuildingMenu(BuildMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("build-buildings", adapter.BuildSizeHeader);
            IReadOnlyList<BuildMenuAdapter.BuildingItem> buildings = adapter.GetBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildMenuAdapter.BuildingItem building = buildings[i];
                menu.AddItem(new MenuItemWidget(
                    "build-building-" + i,
                    () => building.Label,
                    () => building.IsAvailable ? string.Empty : "unavailable",
                    building.Focus,
                    () => building.Focus(),
                    () => true,
                    building.Tooltip));
            }

            if (buildings.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "build-buildings-none",
                    () => "No buildings",
                    null,
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true));
            }
            else
            {
                menu.SetFocusedItemById("build-building-" + adapter.SelectedBuildingIndex);
            }

            return menu;
        }

        private static string BuildTierId(BuildMenuAdapter.TierItem tier)
        {
            return tier != null ? "build-tier-" + tier.Level : "build-tier";
        }

        private static string BuildCategoryId(BuildMenuAdapter.CategoryItem category)
        {
            return category != null ? "build-category-" + category.Index : "build-category";
        }

        private static string BuildRequirementLabel(BuildMenuAdapter.RequirementItem requirement)
        {
            if (requirement == null)
            {
                return string.Empty;
            }

            return requirement.IsMet ? requirement.Label : "Missing " + requirement.Label;
        }

    }
}
