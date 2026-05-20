using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SettlementScreen : Screen
    {
        private const int ArmyExchangeGridIndex = 10;

        private readonly TownInteractionMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;

        public SettlementScreen(TownInteractionMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            TownInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<TownInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                TownInteractionMenuAdapter adapter = new TownInteractionMenuAdapter(menus[i]);
                if (adapter.IsTopLevelPresent())
                {
                    return new SettlementScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsTopLevelPresent();
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
            GridFocus gridFocus = CaptureArmyGridFocus();

            RootWidget = BuildRoot(_adapter);
            RestoreArmyGridFocus(gridFocus);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null)
            {
                return;
            }

            _troopsUpdatedHandler = HandleTroopsUpdated;
            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Combine(
                commands.OnTroopsUpdated,
                _troopsUpdatedHandler);
        }

        private void DetachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null || _troopsUpdatedHandler == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Remove(
                commands.OnTroopsUpdated,
                _troopsUpdatedHandler);
            _troopsUpdatedHandler = null;
        }

        private void HandleTroopsUpdated(OnTroopsUpdatedPayload payload)
        {
            if (payload == null || _adapter == null)
            {
                return;
            }

            if (payload.ParentId != _adapter.VisitingCommanderId && payload.ParentId != _adapter.SettlementMapEntityId)
            {
                return;
            }

            Refresh();
        }

        private GridFocus CaptureArmyGridFocus()
        {
            ArmyExchangeGridWidget grid = RootWidget != null
                ? RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget
                : null;
            return grid != null ? new GridFocus(grid.FocusedColumnIndex, grid.FocusedRowIndex) : null;
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

        private static ContainerWidget BuildRoot(TownInteractionMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("settlement", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                "settlement-tutorial",
                adapter.GetTutorialButtonLabel(),
                adapter.ActivateTutorial,
                adapter.HideNativeTooltip,
                adapter.IsTutorialButtonVisible,
                adapter.IsTutorialButtonVisible));

            root.AddChild(new TextWidget(
                "settlement-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "settlement-custom-name",
                () => adapter.CustomName,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsCustomNameVisible));

            root.AddChild(Portrait.Static(
                "settlement-visiting-wielder",
                () => adapter.VisitingWielderName,
                adapter.HideNativeTooltip,
                () => adapter.VisitingWielderTooltip));

            root.AddChild(new ButtonWidget(
                "settlement-draft-troops",
                () => adapter.DraftLabel,
                adapter.ActivateDraft,
                adapter.FocusDraft,
                adapter.IsDraftEnabled,
                getTooltip: () => adapter.DraftTooltip));

            root.AddChild(new ButtonWidget(
                "settlement-upgrade-troops",
                () => adapter.UpgradeLabel,
                adapter.ActivateUpgrade,
                adapter.FocusUpgrade,
                adapter.IsUpgradeEnabled,
                getTooltip: () => adapter.UpgradeTooltip));

            root.AddChild(new TextWidget(
                "settlement-defending-wielder-status",
                () => adapter.DefendingWielderStatus,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.DefendingWielderStatus)));

            root.AddChild(new ButtonWidget(
                "settlement-store-wielder",
                () => adapter.StoreLabel,
                adapter.ActivateStore,
                adapter.FocusStore,
                adapter.IsStoreEnabled,
                adapter.IsStoreVisible,
                () => adapter.StoreTooltip));

            root.AddChild(new ButtonWidget(
                "settlement-eject-wielder",
                () => adapter.EjectLabel,
                adapter.ActivateEject,
                adapter.FocusEject,
                adapter.IsEjectEnabled,
                adapter.IsEjectVisible,
                () => adapter.EjectTooltip));

            root.AddChild(new ButtonWidget(
                "settlement-trade-wielder",
                () => adapter.TradeLabel,
                adapter.ActivateTrade,
                adapter.FocusTrade,
                adapter.IsTradeEnabled,
                adapter.IsTradeVisible,
                () => adapter.TradeTooltip));

            root.AddChild(BuildArmyExchangeGrid(
                "settlement-army-exchange-grid",
                BuildVisitingArmyLabel(adapter),
                ModText.Get(ModStrings.Screens.SettlementTroops),
                adapter.VisitingTroops,
                adapter.SettlementTroops));
            root.AddChild(BuildDefenseMenu("settlement-garrison", GameText.Get("Adventure/BuildMenu/Garrison", string.Empty), adapter.GetGarrisonSlots()));
            root.AddChild(BuildDefenseMenu("settlement-ballista", ModText.Get(ModStrings.Screens.Ballista), adapter.GetBallistaSlots()));

            root.AddChild(new ButtonWidget(
                "settlement-close",
                () => adapter.CloseLabel,
                adapter.Close,
                adapter.HideNativeTooltip,
                () => adapter.IsTopLevelPresent()));

            return root;
        }

        private static ArmyExchangeGridWidget BuildArmyExchangeGrid(
            string id,
            string leftArmyLabel,
            string rightArmyLabel,
            TroopHudAdapter left,
            TroopHudAdapter right)
        {
            IReadOnlyList<TroopHudAdapter.SlotItem> leftSlots = left != null
                ? left.GetSlots()
                : new TroopHudAdapter.SlotItem[0];
            IReadOnlyList<TroopHudAdapter.SlotItem> rightSlots = right != null
                ? right.GetSlots()
                : new TroopHudAdapter.SlotItem[0];
            return new ArmyExchangeGridWidget(
                id,
                leftArmyLabel,
                rightArmyLabel,
                leftSlots,
                rightSlots,
                DropArmySlot);
        }

        private static string BuildVisitingArmyLabel(TownInteractionMenuAdapter adapter)
        {
            string name = adapter != null ? adapter.VisitingWielderName : string.Empty;
            return string.IsNullOrWhiteSpace(name)
                ? ModText.Get(ModStrings.Screens.VisitingWielderArmy)
                : ModText.Get(ModStrings.Screens.WielderArmyPossessive, name);
        }

        private static TroopHudAdapter.DropResult DropArmySlot(TroopHudAdapter.SlotItem source, TroopHudAdapter.SlotItem target)
        {
            return source != null ? source.CompleteDropTo(target) : TroopHudAdapter.DropResult.None;
        }

        private static MenuWidget BuildDefenseMenu(
            string id,
            string label,
            IReadOnlyList<DefenceSlotListAdapter.Slot> slots)
        {
            MenuWidget menu = new MenuWidget(id, label);
            if (slots == null || slots.Count == 0)
            {
                return menu;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                DefenceSlotListAdapter.Slot slot = slots[i];
                menu.AddItem(new MenuItemWidget(
                    id + "-slot-" + slot.SlotNumber,
                    () => BuildDefenseSlotLabel(slot),
                    null,
                    null,
                    slot.Focus,
                    () => true,
                    () => slot.Tooltip));
            }

            return menu;
        }

        private static string BuildDefenseSlotLabel(DefenceSlotListAdapter.Slot slot)
        {
            if (slot == null)
            {
                return string.Empty;
            }

            string slotLabel = ModText.Get(ModStrings.UI.Slot, slot.SlotNumber);
            if (!slot.IsOccupied)
            {
                return ModText.Get(ModStrings.UI.EmptyTroopSlot, slotLabel);
            }

            if (slot.CurrentSize > 0 && slot.MaxSize > 0)
            {
                return ModText.Get(ModStrings.UI.TroopSlotWithSize, slot.TroopName, slot.CurrentSize, slot.MaxSize, slotLabel);
            }

            return ModText.Get(ModStrings.UI.TroopSlot, slot.TroopName, slotLabel);
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
