using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ResearchScreen : Screen
    {
        private readonly ResearchMenuAdapter _adapter;
        private Action<OnResearchPurchasedPayload> _researchPurchasedHandler;

        public ResearchScreen(ResearchMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnPush()
        {
            AttachListeners();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            DetachListeners();
            _adapter?.HideNativeTooltip();
        }

        public void Refresh(bool focusAfterRefresh)
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int focusedMenuIndex = GetFocusedMenuIndex(focusedIndex);

            RootWidget = BuildRoot(_adapter);

            if (!focusAfterRefresh)
            {
                return;
            }

            if (RootWidget == null || !RootWidget.SetFocusByIndex(focusedIndex))
            {
                RootWidget?.Focus();
                return;
            }

            MenuWidget menu = RootWidget.GetChildAt(focusedIndex) as MenuWidget;
            if (menu != null && menu.Id != "research-buildings" && focusedMenuIndex >= 0)
            {
                menu.SetFocusByIndex(focusedMenuIndex);
            }
        }

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null || _researchPurchasedHandler != null)
            {
                return;
            }

            _researchPurchasedHandler = HandleResearchPurchased;
            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnResearchPurchased = (Action<OnResearchPurchasedPayload>)Delegate.Combine(
                commands.OnResearchPurchased,
                _researchPurchasedHandler);
        }

        private void DetachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null || _researchPurchasedHandler == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnResearchPurchased = (Action<OnResearchPurchasedPayload>)Delegate.Remove(
                commands.OnResearchPurchased,
                _researchPurchasedHandler);
            _researchPurchasedHandler = null;
        }

        private void HandleResearchPurchased(OnResearchPurchasedPayload payload)
        {
            if (!ReferenceEquals(SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen, this))
            {
                return;
            }

            Refresh(focusAfterRefresh: true);
        }

        private int GetFocusedMenuIndex(int focusedIndex)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(focusedIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private static ContainerWidget BuildRoot(ResearchMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("research", "Research");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                "research-tutorial",
                adapter.GetTutorialButtonLabel,
                adapter.ActivateTutorial,
                adapter.HideNativeTooltip,
                adapter.IsTutorialButtonVisible,
                adapter.IsTutorialButtonVisible));

            root.AddChild(BuildBuildingMenu(adapter));
            AddCategoryMenus(root, adapter);
            return root;
        }

        private static MenuWidget BuildBuildingMenu(ResearchMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("research-buildings", "Research building");
            IReadOnlyList<ResearchMenuAdapter.BuildingItem> buildings = adapter.GetBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                ResearchMenuAdapter.BuildingItem building = buildings[i];
                menu.AddItem(new MenuItemWidget(
                    "research-building-" + i,
                    () => building.Label,
                    () => BuildBuildingStatus(building),
                    building.Activate,
                    () => building.Focus(),
                    () => true));
            }

            if (buildings.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "research-buildings-none",
                    () => "No research buildings",
                    null,
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true));
            }
            else
            {
                menu.SetFocusByIndex(adapter.SelectedBuildingIndex);
            }

            return menu;
        }

        private static void AddCategoryMenus(ContainerWidget root, ResearchMenuAdapter adapter)
        {
            IReadOnlyList<ResearchMenuAdapter.CategoryItem> categories = adapter.GetCategories();
            for (int i = 0; i < categories.Count; i++)
            {
                ResearchMenuAdapter.CategoryItem category = categories[i];
                MenuWidget menu = new MenuWidget("research-category-" + i, category.Label);
                for (int j = 0; j < category.Items.Count; j++)
                {
                    ResearchMenuAdapter.ResearchItem item = category.Items[j];
                    menu.AddItem(new MenuItemWidget(
                        "research-item-" + i + "-" + j,
                        () => BuildResearchLabel(item),
                        null,
                        item.Activate,
                        () => item.Focus(),
                        () => true,
                        item.Tooltip));
                }

                root.AddChild(menu);
            }
        }

        private static string BuildBuildingStatus(ResearchMenuAdapter.BuildingItem building)
        {
            if (building == null)
            {
                return string.Empty;
            }

            if (building.MissingBuilding && !string.IsNullOrWhiteSpace(building.Description))
            {
                return building.Description + ". missing building";
            }

            if (building.MissingBuilding)
            {
                return "missing building";
            }

            return building.Description;
        }

        private static string BuildResearchLabel(ResearchMenuAdapter.ResearchItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.OwnedTier <= 0)
            {
                return item.Label;
            }

            string tierHeader = string.IsNullOrWhiteSpace(item.TierHeader) ? "Tier" : item.TierHeader;
            return item.Label + " (" + tierHeader + " " + item.OwnedTier + ")";
        }
    }
}
