using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

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

        public void Refresh(bool focusAfterRefresh)
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            string gridSlotId = GetFocusedGridSlotId();

            RootWidget = BuildRoot(_adapter);
            RestoreGridFocus(gridSlotId);

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

            bool focusAfterRefresh = ReferenceEquals(SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen, this);
            Refresh(focusAfterRefresh);
        }

        private string GetFocusedGridSlotId()
        {
            ArmyExchangeGridWidget grid = RootWidget != null
                ? RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget
                : null;
            return grid != null ? grid.FocusedSlotId : null;
        }

        private void RestoreGridFocus(string gridSlotId)
        {
            if (string.IsNullOrWhiteSpace(gridSlotId) || RootWidget == null)
            {
                return;
            }

            ArmyExchangeGridWidget grid = RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget;
            grid?.SetFocusedSlotById(gridSlotId);
        }

        private static ContainerWidget BuildRoot(TownInteractionMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("settlement", adapter != null ? adapter.Title : "Settlement");
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

            root.AddChild(adapter.BuildArmyExchangeGrid());
            root.AddChild(BuildDefenseMenu("settlement-garrison", "Garrison", adapter.GetGarrisonSlots()));
            root.AddChild(BuildDefenseMenu("settlement-ballista", "Ballista", adapter.GetBallistaSlots()));

            root.AddChild(new ButtonWidget(
                "settlement-close",
                () => adapter.CloseLabel,
                adapter.Close,
                adapter.HideNativeTooltip,
                () => adapter.IsTopLevelPresent()));

            return root;
        }

        private static MenuWidget BuildDefenseMenu(
            string id,
            string label,
            IReadOnlyList<TownInteractionMenuAdapter.DefenseSlot> slots)
        {
            MenuWidget menu = new MenuWidget(id, label);
            if (slots == null || slots.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    id + "-none",
                    () => "No slots",
                    null,
                    () => false,
                    null,
                    () => true));
                return menu;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                TownInteractionMenuAdapter.DefenseSlot slot = slots[i];
                menu.AddItem(new MenuItemWidget(
                    slot.Id,
                    () => slot.Label,
                    null,
                    null,
                    slot.Focus,
                    () => true,
                    () => slot.Tooltip));
            }

            return menu;
        }
    }
}
