using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TradingScreen : Screen
    {
        private const int InventoryGridIndex = 4;
        private const int ArmyExchangeGridIndex = 5;

        private readonly TradingMenuAdapter _adapter;
        private Action<int, bool> _artifactChangedHandler;
        private Action<int> _statisticsChangedHandler;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;

        public TradingScreen(TradingMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public TradingMenuAdapter Adapter
        {
            get { return _adapter; }
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

        public void Refresh(bool focusAfterRefresh)
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            GridFocus inventoryFocus = CaptureInventoryGridFocus();
            GridFocus armyFocus = CaptureArmyGridFocus();
            RootWidget = BuildRoot(_adapter);
            RestoreInventoryGridFocus(inventoryFocus);
            RestoreArmyGridFocus(armyFocus);

            if (!focusAfterRefresh)
            {
                return;
            }

            if (RootWidget == null || !RootWidget.SetFocusByIndex(focusedIndex))
            {
                RootWidget?.Focus();
            }
        }

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            _artifactChangedHandler = HandleArtifactChanged;
            _statisticsChangedHandler = HandleStatisticsChanged;
            _troopsUpdatedHandler = HandleTroopsUpdated;
            commands.OnArtifactChanged = (Action<int, bool>)Delegate.Combine(commands.OnArtifactChanged, _artifactChangedHandler);
            commands.OnCommanderStatisticsChanged = (Action<int>)Delegate.Combine(commands.OnCommanderStatisticsChanged, _statisticsChangedHandler);
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

            if (_statisticsChangedHandler != null)
            {
                commands.OnCommanderStatisticsChanged = (Action<int>)Delegate.Remove(commands.OnCommanderStatisticsChanged, _statisticsChangedHandler);
                _statisticsChangedHandler = null;
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

        private void HandleStatisticsChanged(int commanderId)
        {
            if (_adapter != null && (commanderId == _adapter.LeftCommanderId || commanderId == _adapter.RightCommanderId))
            {
                RequestDetectorRefresh();
            }
        }

        private void HandleTroopsUpdated(OnTroopsUpdatedPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            if (payload.ParentId != _adapter.LeftCommanderId && payload.ParentId != _adapter.RightCommanderId)
            {
                return;
            }

            RequestDetectorRefresh();
        }

        private void RequestDetectorRefresh()
        {
            SoqAccessPlugin.Instance?.ScreenDetector?.OnTradingMenuChanged();
        }

        private GridFocus CaptureInventoryGridFocus()
        {
            InventoryGridWidget grid = RootWidget != null ? RootWidget.GetChildAt(InventoryGridIndex) as InventoryGridWidget : null;
            return grid != null ? new GridFocus(grid.FocusedColumnIndex, grid.FocusedRowIndex) : null;
        }

        private GridFocus CaptureArmyGridFocus()
        {
            ArmyExchangeGridWidget grid = RootWidget != null ? RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget : null;
            return grid != null ? new GridFocus(grid.FocusedColumnIndex, grid.FocusedRowIndex) : null;
        }

        private void RestoreInventoryGridFocus(GridFocus focus)
        {
            if (focus == null || RootWidget == null)
            {
                return;
            }

            InventoryGridWidget grid = RootWidget.GetChildAt(InventoryGridIndex) as InventoryGridWidget;
            grid?.SetFocusedCell(focus.ColumnIndex, focus.RowIndex);
        }

        private void RestoreArmyGridFocus(GridFocus focus)
        {
            if (focus == null || RootWidget == null)
            {
                return;
            }

            ArmyExchangeGridWidget grid = RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget;
            grid?.SetFocusedCell(focus.ColumnIndex, focus.RowIndex);
        }

        private static ContainerWidget BuildRoot(TradingMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("trade-screen", adapter != null ? adapter.Title : "Trade");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildPortrait(adapter, left: true));
            root.AddChild(BuildMenu("trade-left-stats", adapter.LeftCommanderName + "'s stats", GetItemsSafely("Left stats", () => adapter.GetStats(left: true)), adapter.HideNativeTooltip));
            root.AddChild(BuildModifierCategoryMenu(adapter, left: true));
            root.AddChild(BuildMenu("trade-left-active-modifiers", adapter.GetActiveModifierListLabel(left: true), GetItemsSafely("Left active modifiers", () => adapter.GetActiveModifiers(left: true)), adapter.HideNativeTooltip));

            root.AddChild(new InventoryGridWidget(
                "trade-inventory-grid",
                adapter.BuildInventoryGridColumns(),
                adapter.DropInventoryGridArtifact));

            root.AddChild(adapter.BuildArmyExchangeGrid());

            root.AddChild(BuildPortrait(adapter, left: false));
            root.AddChild(BuildMenu("trade-right-stats", adapter.RightCommanderName + "'s stats", GetItemsSafely("Right stats", () => adapter.GetStats(left: false)), adapter.HideNativeTooltip));
            root.AddChild(BuildModifierCategoryMenu(adapter, left: false));
            root.AddChild(BuildMenu("trade-right-active-modifiers", adapter.GetActiveModifierListLabel(left: false), GetItemsSafely("Right active modifiers", () => adapter.GetActiveModifiers(left: false)), adapter.HideNativeTooltip));

            root.AddChild(new ButtonWidget(
                "trade-close",
                "Close",
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static Widget BuildPortrait(TradingMenuAdapter adapter, bool left)
        {
            string side = left ? "left" : "right";
            return Portrait.StaticNative(
                "trade-" + side + "-portrait",
                () => adapter.GetPortraitLabel(left),
                () => adapter.GetPortraitTooltipTarget(left),
                adapter.Localization);
        }

        private static IReadOnlyList<TradingMenuAdapter.LabeledItem> GetItemsSafely(
            string section,
            Func<IReadOnlyList<TradingMenuAdapter.LabeledItem>> getter)
        {
            try
            {
                IReadOnlyList<TradingMenuAdapter.LabeledItem> items = getter != null ? getter() : null;
                return items ?? new TradingMenuAdapter.LabeledItem[0];
            }
            catch (Exception ex)
            {
                SoqAccessPlugin.Instance?.LogWarning("TradingScreen section " + section + " failed to build: " + ex);
                return new TradingMenuAdapter.LabeledItem[]
                {
                    new TradingMenuAdapter.LabeledItem(section.ToLowerInvariant() + "-error", "Unavailable")
                };
            }
        }

        private static MenuWidget BuildModifierCategoryMenu(TradingMenuAdapter adapter, bool left)
        {
            string side = left ? "left" : "right";
            MenuWidget menu = new MenuWidget(
                "trade-" + side + "-modifier-tabs",
                (left ? adapter.LeftCommanderName : adapter.RightCommanderName) + "'s modifier categories");
            string activeId = null;
            foreach (TradingMenuAdapter.ModifierCategory category in adapter.GetModifierCategories(left))
            {
                TradingMenuAdapter.ModifierCategory captured = category;
                if (captured.Index == adapter.GetActiveModifierCategoryIndex(left))
                {
                    activeId = captured.Id;
                }

                menu.AddItem(new MenuItemWidget(
                    captured.Id,
                    () => captured.Label,
                    null,
                    () => FocusModifierCategory(adapter, left, captured.Index),
                    () => FocusModifierCategory(adapter, left, captured.Index),
                    () => true,
                    captured.Tooltip));
            }

            menu.SetFocusedItemById(activeId);
            return menu;
        }

        private static bool FocusModifierCategory(TradingMenuAdapter adapter, bool left, int categoryIndex)
        {
            int previousCategoryIndex = adapter.GetActiveModifierCategoryIndex(left);
            bool result = adapter.FocusModifierCategory(left, categoryIndex);
            if (result && previousCategoryIndex != adapter.GetActiveModifierCategoryIndex(left))
            {
                SoqAccessPlugin.Instance?.ScreenDetector?.OnTradingMenuChanged();
            }

            return result;
        }

        private static MenuWidget BuildMenu(
            string id,
            string label,
            IReadOnlyList<TradingMenuAdapter.LabeledItem> items,
            Action emptyItemFocus = null)
        {
            MenuWidget menu = new MenuWidget(id, label);
            if (items == null || items.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    id + "-none",
                    () => "None",
                    null,
                    () => false,
                    emptyItemFocus,
                    () => true));
                return menu;
            }

            for (int i = 0; i < items.Count; i++)
            {
                TradingMenuAdapter.LabeledItem item = items[i];
                menu.AddItem(new MenuItemWidget(
                    item.Id,
                    () => item.Label,
                    () => item.Status,
                    item.Activate ?? (() => false),
                    item.OnFocus ?? emptyItemFocus,
                    () => true,
                    item.Tooltip));
            }

            return menu;
        }

        private sealed class GridFocus
        {
            public GridFocus(int columnIndex, int rowIndex)
            {
                ColumnIndex = columnIndex;
                RowIndex = rowIndex;
            }

            public int ColumnIndex { get; private set; }
            public int RowIndex { get; private set; }
        }
    }
}
