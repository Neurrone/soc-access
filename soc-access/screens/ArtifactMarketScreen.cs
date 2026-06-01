using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ArtifactMarketScreen : Screen
    {
        private const string CategoriesMenuId = "artifact-market-categories";
        private const string OffersMenuId = "artifact-market-offers";
        private const string InventoryGridId = "artifact-market-inventory-grid";
        private const string WielderArmyMenuId = "artifact-market-wielder-army";

        private readonly ArtifactMarketMenuAdapter _adapter;
        private Action<int, bool> _artifactChangedHandler;
        private Action<ArtifactMarketUpdatedPayoad> _artifactMarketUpdatedHandler;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;

        public ArtifactMarketScreen(ArtifactMarketMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            ArtifactMarketMenu[] menus = Resources.FindObjectsOfTypeAll<ArtifactMarketMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                ArtifactMarketMenuAdapter adapter = new ArtifactMarketMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new ArtifactMarketScreen(adapter);
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

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int categoryFocusedIndex = GetFocusedMenuIndex(CategoriesMenuId);
            int offerFocusedIndex = GetFocusedMenuIndex(OffersMenuId);
            int armyFocusedIndex = GetFocusedMenuIndex(WielderArmyMenuId);
            InventoryGridWidget.FocusState inventoryFocus = CaptureInventoryGridFocus();
            RootWidget = BuildRoot(_adapter);
            RestoreMenuFocus(CategoriesMenuId, categoryFocusedIndex);
            RestoreMenuFocus(OffersMenuId, offerFocusedIndex);
            RestoreMenuFocus(WielderArmyMenuId, armyFocusedIndex);
            RestoreInventoryGridFocus(inventoryFocus);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            _artifactChangedHandler = HandleArtifactChanged;
            _artifactMarketUpdatedHandler = HandleArtifactMarketUpdated;
            _troopsUpdatedHandler = HandleTroopsUpdated;
            commands.OnArtifactChanged = (Action<int, bool>)Delegate.Combine(commands.OnArtifactChanged, _artifactChangedHandler);
            commands.OnArtifactMarketUpdated = (Action<ArtifactMarketUpdatedPayoad>)Delegate.Combine(commands.OnArtifactMarketUpdated, _artifactMarketUpdatedHandler);
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Combine(commands.OnTroopsUpdated, _troopsUpdatedHandler);
        }

        private void DetachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            if (_artifactChangedHandler != null)
            {
                commands.OnArtifactChanged = (Action<int, bool>)Delegate.Remove(commands.OnArtifactChanged, _artifactChangedHandler);
                _artifactChangedHandler = null;
            }

            if (_artifactMarketUpdatedHandler != null)
            {
                commands.OnArtifactMarketUpdated = (Action<ArtifactMarketUpdatedPayoad>)Delegate.Remove(commands.OnArtifactMarketUpdated, _artifactMarketUpdatedHandler);
                _artifactMarketUpdatedHandler = null;
            }

            if (_troopsUpdatedHandler != null)
            {
                commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Remove(commands.OnTroopsUpdated, _troopsUpdatedHandler);
                _troopsUpdatedHandler = null;
            }
        }

        private void HandleArtifactChanged(int artifactId, bool isNewArtifact)
        {
            RequestDetectorRefresh();
        }

        private void HandleArtifactMarketUpdated(ArtifactMarketUpdatedPayoad payload)
        {
            if (payload == null || _adapter == null || payload.InteractingCommanderId == _adapter.CommanderId)
            {
                RequestDetectorRefresh();
            }
        }

        private void HandleTroopsUpdated(OnTroopsUpdatedPayload payload)
        {
            if (payload == null || _adapter == null || payload.ParentId == _adapter.CommanderId)
            {
                RequestDetectorRefresh();
            }
        }

        private void RequestDetectorRefresh()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnArtifactMarketChanged();
        }

        private int GetFocusedMenuIndex(string id)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildById(id) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private void RestoreMenuFocus(string id, int focusedIndex)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildById(id) as MenuWidget : null;
            menu?.SetFocusByIndexSilently(focusedIndex);
        }

        private InventoryGridWidget.FocusState CaptureInventoryGridFocus()
        {
            InventoryGridWidget grid = RootWidget != null ? RootWidget.GetChildById(InventoryGridId) as InventoryGridWidget : null;
            return grid != null ? grid.CaptureFocusState() : null;
        }

        private void RestoreInventoryGridFocus(InventoryGridWidget.FocusState focus)
        {
            if (focus == null || RootWidget == null)
            {
                return;
            }

            InventoryGridWidget grid = RootWidget.GetChildById(InventoryGridId) as InventoryGridWidget;
            grid?.RestoreFocusState(focus);
        }

        private static ContainerWidget BuildRoot(ArtifactMarketMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("artifact-market", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "artifact-market-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.Title)));

            root.AddChild(Portrait.StaticNative(
                "artifact-market-wielder-portrait",
                () => adapter.CommanderName,
                () => adapter.WielderPortraitTarget,
                adapter.Localization,
                null,
                () => adapter.WielderPortraitTarget != null));

            root.AddChild(TroopHudMenu.Build(
                WielderArmyMenuId,
                GameText.Get(adapter.Localization, "Commanders/Tooltip/Troops", "Troops"),
                adapter.Troops,
                adapter.IsArmyVisible));

            root.AddChild(new TextWidget(
                "artifact-market-description",
                () => adapter.Description,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.Description)));

            root.AddChild(BuildCategoriesMenu(adapter));
            root.AddChild(BuildOffersMenu(adapter));
            root.AddChild(new ButtonWidget(
                "artifact-market-buy-selected",
                () => adapter.SelectedBuyButtonLabel,
                adapter.BuySelectedMarketArtifact,
                adapter.HideNativeTooltip,
                adapter.CanBuySelectedArtifact));
            root.AddChild(new InventoryGridWidget(
                InventoryGridId,
                BuildInventoryGridColumns(adapter),
                adapter.DropInventoryArtifact));

            root.AddChild(new ButtonWidget(
                "artifact-market-close",
                ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static MenuWidget BuildCategoriesMenu(ArtifactMarketMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(CategoriesMenuId, ModText.Get(ModStrings.Screens.Categories));
            IReadOnlyList<ArtifactMarketMenuAdapter.CategoryItem> categories = adapter.GetCategories();
            string activeId = null;
            for (int i = 0; i < categories.Count; i++)
            {
                ArtifactMarketMenuAdapter.CategoryItem category = categories[i];
                ArtifactMarketMenuAdapter.CategoryItem captured = category;
                if (captured.Index == adapter.ActiveCategoryIndex)
                {
                    activeId = captured.Id;
                }

                menu.AddItem(new MenuItemWidget(
                    captured.Id,
                    () => captured.Label,
                    null,
                    () => SelectCategory(adapter, captured.Index),
                    () => SelectCategory(adapter, captured.Index),
                    () => true));
            }

            menu.SetFocusedItemById(activeId);
            return menu;
        }

        private static bool SelectCategory(ArtifactMarketMenuAdapter adapter, int categoryIndex)
        {
            if (adapter == null)
            {
                return false;
            }

            int previousCategoryIndex = adapter.ActiveCategoryIndex;
            bool result = adapter.SelectCategory(categoryIndex);
            if (result && previousCategoryIndex != adapter.ActiveCategoryIndex)
            {
                SocAccessPlugin.Instance?.ScreenDetector?.OnArtifactMarketChanged();
            }

            return result;
        }

        private static MenuWidget BuildOffersMenu(ArtifactMarketMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(OffersMenuId, adapter.Title);
            IReadOnlyList<ArtifactMarketMenuAdapter.MarketArtifactItem> items = adapter.GetMarketArtifacts();
            if (items.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "artifact-market-offers-none",
                    () => ModText.Get(ModStrings.Screens.None),
                    null,
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true));
                return menu;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ArtifactMarketMenuAdapter.MarketArtifactItem item = items[i];
                ArtifactMarketMenuAdapter.MarketArtifactItem captured = item;
                menu.AddItem(new MenuItemWidget(
                    captured.Id,
                    () => MenuButtonTextUtility.JoinParts(captured.Label, captured.CostLabel),
                    null,
                    captured.Activate,
                    captured.OnFocus,
                    () => true,
                    captured.GetTooltip));
            }

            return menu;
        }

        private static IReadOnlyList<InventoryGridWidget.Column> BuildInventoryGridColumns(ArtifactMarketMenuAdapter adapter)
        {
            return new[]
            {
                new InventoryGridWidget.Column(
                    "artifact-market-equipped",
                    adapter.EquipmentLabel,
                    BuildInventoryCells("artifact-market-equipped", adapter.GetEquipmentSlots())),
                new InventoryGridWidget.Column(
                    "artifact-market-inventory",
                    adapter.InventoryLabel,
                    BuildInventoryCells("artifact-market-inventory", adapter.GetBackpackSlots()))
            };
        }

        private static IReadOnlyList<InventoryGridWidget.Cell> BuildInventoryCells(
            string idPrefix,
            IReadOnlyList<InventorySlotInfo> slots)
        {
            List<InventoryGridWidget.Cell> cells = new List<InventoryGridWidget.Cell>();
            if (slots == null)
            {
                return cells;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotInfo slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                cells.Add(new InventoryGridWidget.Cell(
                    idPrefix + "-" + i,
                    BuildInventorySlotLabel(slot),
                    slot));
            }

            return cells;
        }

        private static string BuildInventorySlotLabel(InventorySlotInfo slot)
        {
            string name = !slot.IsEmpty ? slot.ArtifactName : ModText.Get(ModStrings.Screens.Empty);
            string location = slot.IsBackpackSlot
                ? ModText.Get(ModStrings.UI.SlotInGroup, slot.InventoryName, slot.PositionIndex + 1)
                : slot.SlotName;
            return MenuButtonTextUtility.JoinParts(name, location);
        }
    }
}
