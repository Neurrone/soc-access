using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ResearchScreen : Screen
    {
        private const string BuildingsMenuId = "research-buildings";
        private const string FactionsMenuId = "research-factions";

        private readonly ResearchMenuAdapter _adapter;
        private Action<OnResearchPurchasedPayload> _researchPurchasedHandler;

        public ResearchScreen(ResearchMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            ResearchMenu[] menus = Resources.FindObjectsOfTypeAll<ResearchMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                ResearchMenuAdapter adapter = new ResearchMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new ResearchScreen(adapter);
                }
            }

            return null;
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

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            string focusedChildId = GetFocusedChildId(focusedIndex);
            int focusedMenuIndex = GetFocusedMenuIndex(focusedIndex);
            string focusedMenuItemId = GetFocusedMenuItemId(focusedIndex);

            RootWidget = BuildRoot(_adapter);
            if (string.IsNullOrWhiteSpace(focusedChildId) || !RootWidget.SetFocusedChildById(focusedChildId))
            {
                RootWidget?.SetFocusByIndexSilently(focusedIndex);
            }

            MenuWidget menu = RootWidget?.FocusedChild as MenuWidget;
            if (menu != null && menu.Id != BuildingsMenuId)
            {
                if (string.IsNullOrWhiteSpace(focusedMenuItemId) || !menu.SetFocusedItemById(focusedMenuItemId))
                {
                    menu.SetFocusByIndexSilently(focusedMenuIndex);
                }
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
            if (!ReferenceEquals(SocAccessMod.Instance?.ScreenManager?.CurrentScreen, this))
            {
                return;
            }

            Refresh();
        }

        private int GetFocusedMenuIndex(int focusedIndex)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(focusedIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private string GetFocusedChildId(int focusedIndex)
        {
            Widget widget = RootWidget != null ? RootWidget.GetChildAt(focusedIndex) : null;
            return widget != null ? widget.Id : null;
        }

        private string GetFocusedMenuItemId(int focusedIndex)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(focusedIndex) as MenuWidget : null;
            return menu != null && menu.FocusedItem != null ? menu.FocusedItem.Id : null;
        }

        private static ContainerWidget BuildRoot(ResearchMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("research", GameText.Get("Adventure/KingdomResearchOverview/Header", string.Empty));
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

            if (adapter.HasFactionSelector())
            {
                root.AddChild(BuildFactionMenu(adapter));
            }

            root.AddChild(BuildBuildingMenu(adapter));
            AddCategoryMenus(root, adapter);
            return root;
        }

        private static MenuWidget BuildFactionMenu(ResearchMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(FactionsMenuId, ModText.Get(ModStrings.UI.ColumnFaction));
            IReadOnlyList<ResearchMenuAdapter.FactionItem> factions = adapter.GetFactions();
            for (int i = 0; i < factions.Count; i++)
            {
                ResearchMenuAdapter.FactionItem faction = factions[i];
                menu.AddItem(new MenuItemWidget(
                    "research-faction-" + faction.FactionIndex,
                    () => faction.Label,
                    () => faction.IsSelected ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    faction.Activate,
                    () => faction.Focus(),
                    () => true));
            }

            if (factions.Count > 0)
            {
                menu.SetFocusByIndexSilently(adapter.SelectedFactionMenuIndex);
            }

            return menu;
        }

        private static MenuWidget BuildBuildingMenu(ResearchMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(BuildingsMenuId, string.Empty);
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
                return menu;
            }

            menu.SetFocusByIndexSilently(adapter.SelectedBuildingIndex);

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
                return ModText.Get(ModStrings.Screens.DescriptionWithMissingBuilding, building.Description);
            }

            if (building.MissingBuilding)
            {
                return ModText.Get(ModStrings.Screens.MissingBuilding);
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

            string tierHeader = string.IsNullOrWhiteSpace(item.TierHeader) ? ModText.Get(ModStrings.Screens.Tier) : item.TierHeader;
            return ModText.Get(ModStrings.Screens.ResearchTier, item.Label, tierHeader, item.OwnedTier);
        }
    }
}
