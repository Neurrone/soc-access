using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class DefenceMenuScreen : Screen
    {
        private readonly DefenceMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;

        public DefenceMenuScreen(DefenceMenuAdapter adapter)
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
            RootWidget = BuildRoot(_adapter);
            if (focusAfterRefresh)
            {
                RootWidget?.SetFocusByIndex(focusedIndex);
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

            int storedWielderId = _adapter.DefendingWielder.StoredWielderId;
            if (payload.ParentId != _adapter.MapEntityId && payload.ParentId != storedWielderId)
            {
                return;
            }

            bool focusAfterRefresh = ReferenceEquals(SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen, this);
            Refresh(focusAfterRefresh);
        }

        private static ContainerWidget BuildRoot(DefenceMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("defences", adapter != null ? adapter.Title : "Defences");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                "defences-tutorial",
                adapter.GetTutorialButtonLabel(),
                adapter.ActivateTutorial,
                adapter.HideNativeTooltip,
                adapter.IsTutorialButtonVisible,
                adapter.IsTutorialButtonVisible));

            root.AddChild(new TextWidget(
                "defences-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "defences-subtitle",
                () => adapter.Subtitle,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.Subtitle)));

            DefencePanelWielderAdapter defendingWielder = adapter.DefendingWielder;
            root.AddChild(new TextWidget(
                "defences-defending-wielder-status",
                () => defendingWielder.Status,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(defendingWielder.Status)));

            root.AddChild(Portrait.Static(
                "defences-defending-wielder",
                () => defendingWielder.StoredWielderName,
                defendingWielder.FocusPortrait,
                () => defendingWielder.PortraitTooltip,
                () => defendingWielder.IsStoredWielderVisible));

            root.AddChild(new ButtonWidget(
                "defences-eject-wielder",
                () => defendingWielder.EjectLabel,
                defendingWielder.ActivateEject,
                defendingWielder.FocusEject,
                defendingWielder.IsEjectEnabled,
                defendingWielder.IsEjectVisible,
                () => defendingWielder.EjectTooltip));

            root.AddChild(new ButtonWidget(
                "defences-trade-wielder",
                () => defendingWielder.TradeLabel,
                defendingWielder.ActivateTrade,
                defendingWielder.FocusTrade,
                defendingWielder.IsTradeEnabled,
                defendingWielder.IsTradeVisible,
                () => defendingWielder.TradeTooltip));

            if (defendingWielder.IsStoredWielderVisible)
            {
                root.AddChild(BuildArmyExchangeGrid(
                    "defences-army-exchange-grid",
                    defendingWielder.Troops,
                    adapter.SettlementTroops));
            }
            else
            {
                root.AddChild(TroopHudMenu.Build(
                    "defences-settlement-troops",
                    adapter.DefendingTroopsLabel,
                    adapter.SettlementTroops,
                    adapter.IsSettlementTroopsVisible));
            }

            root.AddChild(new ButtonWidget(
                "defences-draft-troops",
                () => adapter.DraftLabel,
                adapter.ActivateDraft,
                adapter.FocusDraft,
                adapter.IsDraftEnabled,
                adapter.IsDraftVisible,
                () => adapter.DraftTooltip));

            root.AddChild(new ButtonWidget(
                "defences-upgrade-troops",
                () => adapter.UpgradeLabel,
                adapter.ActivateUpgrade,
                adapter.FocusUpgrade,
                adapter.IsUpgradeEnabled,
                adapter.IsUpgradeVisible,
                () => adapter.UpgradeTooltip));

            root.AddChild(new TextWidget(
                "defences-tower-summary",
                () => adapter.TowerSummary,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.HasTowerSummary));

            root.AddChild(BuildTowerMenu(adapter));
            root.AddChild(BuildDefenseMenu("defences-garrison", "Garrison", adapter.GetGarrisonSlots()));
            root.AddChild(BuildDefenseMenu("defences-ballista", "Ballista", adapter.GetBallistaSlots()));

            root.AddChild(new ButtonWidget(
                "defences-close",
                () => adapter.CloseLabel,
                adapter.Close,
                adapter.HideNativeTooltip,
                () => adapter.IsTopLevelPresent()));

            return root;
        }

        private static ArmyExchangeGridWidget BuildArmyExchangeGrid(
            string id,
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
                GetArmyLabel(leftSlots),
                leftSlots,
                rightSlots,
                DropArmySlot);
        }

        private static string GetArmyLabel(IReadOnlyList<TroopHudAdapter.SlotItem> slots)
        {
            return slots != null && slots.Count > 0 ? slots[0].ArmyLabel : string.Empty;
        }

        private static bool DropArmySlot(TroopHudAdapter.SlotItem source, TroopHudAdapter.SlotItem target)
        {
            return source != null && source.DropTo(target);
        }

        private static MenuWidget BuildTowerMenu(DefenceMenuAdapter adapter)
        {
            IReadOnlyList<DefenceMenuAdapter.TowerItem> towers = adapter.GetTowerItems();
            MenuWidget menu = new MenuWidget("defences-towers", "Towers", () => towers.Count > 0);
            for (int i = 0; i < towers.Count; i++)
            {
                DefenceMenuAdapter.TowerItem tower = towers[i];
                menu.AddItem(new MenuItemWidget(
                    tower.Id,
                    () => tower.Label,
                    null,
                    null,
                    tower.Focus,
                    () => true,
                    () => tower.Tooltip));
            }

            return menu;
        }

        private static MenuWidget BuildDefenseMenu(
            string id,
            string label,
            IReadOnlyList<DefenceSlotListAdapter.Slot> slots)
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

            Tooltip tooltip = slot.Tooltip;
            if (tooltip != null && tooltip.TextLines != null && tooltip.TextLines.Count > 0)
            {
                return SpeechTextSanitizer.Normalize(string.Join(". ", tooltip.TextLines));
            }

            return "Empty, slot " + slot.SlotNumber;
        }
    }
}
