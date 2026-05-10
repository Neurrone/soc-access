using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SettlementUpgradeTroopsScreen : Screen
    {
        private readonly TownInteractionMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;
        private Action<ResourceUpdatedPayload> _resourceUpdatedHandler;

        public SettlementUpgradeTroopsScreen(TownInteractionMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsUpgradePresent();
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

                return _adapter != null && _adapter.BackToTop();
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
            _resourceUpdatedHandler = delegate(ResourceUpdatedPayload _) { RefreshIfTop(); };

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Combine(
                commands.OnTroopsUpdated,
                _troopsUpdatedHandler);
            commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Combine(
                commands.OnResourceUpdated,
                _resourceUpdatedHandler);
        }

        private void DetachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            if (_troopsUpdatedHandler != null)
            {
                commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Remove(
                    commands.OnTroopsUpdated,
                    _troopsUpdatedHandler);
                _troopsUpdatedHandler = null;
            }

            if (_resourceUpdatedHandler != null)
            {
                commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Remove(
                    commands.OnResourceUpdated,
                    _resourceUpdatedHandler);
                _resourceUpdatedHandler = null;
            }
        }

        private void HandleTroopsUpdated(OnTroopsUpdatedPayload payload)
        {
            if (payload == null || _adapter == null)
            {
                return;
            }

            if (payload.ParentType != TroopParentType.Commander || payload.ParentId != _adapter.VisitingCommanderId)
            {
                return;
            }

            RefreshIfTop();
        }

        private void RefreshIfTop()
        {
            if (ReferenceEquals(SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen, this))
            {
                Refresh(true);
            }
        }

        private static ContainerWidget BuildRoot(TownInteractionMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("settlement-upgrade-troops", "Upgrade troops");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                "settlement-upgrade-tutorial",
                adapter.GetTutorialButtonLabel(),
                adapter.ActivateTutorial,
                adapter.HideNativeTooltip,
                adapter.IsTutorialButtonVisible,
                adapter.IsTutorialButtonVisible));

            root.AddChild(new TextWidget(
                "settlement-upgrade-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            UpgradeTroopsSubMenuAdapter subMenu = adapter.UpgradeTroops;
            root.AddChild(new TextWidget(
                "settlement-upgrade-none",
                () => subMenu.NoUpgradableTroopsText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => subMenu.IsNoUpgradableTroopsVisible));

            IReadOnlyList<UpgradeTroopsSubMenuAdapter.UpgradeEntry> entries = subMenu.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                AddUpgradeWidgets(root, entries[i]);
            }

            root.AddChild(new ButtonWidget(
                "settlement-upgrade-back",
                "Back",
                adapter.BackToTop,
                adapter.HideNativeTooltip,
                () => adapter.IsUpgradePresent()));

            root.AddChild(new ButtonWidget(
                "settlement-upgrade-close",
                () => adapter.CloseLabel,
                adapter.Close,
                adapter.HideNativeTooltip,
                () => adapter.IsUpgradePresent()));

            return root;
        }

        private static void AddUpgradeWidgets(ContainerWidget root, UpgradeTroopsSubMenuAdapter.UpgradeEntry entry)
        {
            if (root == null || entry == null)
            {
                return;
            }

            root.AddChild(new TextWidget(
                entry.IdPrefix + "-current",
                () => entry.CurrentTroopText,
                entry.Focus,
                includeParentLabelInAnnouncement: false,
                () => entry.CurrentTooltip));

            root.AddChild(new TextWidget(
                entry.IdPrefix + "-target",
                () => entry.TargetTroopText,
                entry.Focus,
                includeParentLabelInAnnouncement: false,
                () => entry.TargetTooltip));

            root.AddChild(new SliderWidget(
                entry.IdPrefix + "-quantity",
                "quantity",
                () => entry.SliderLabel,
                () => entry.SliderValue,
                () => entry.SliderMinimum,
                () => entry.SliderMaximum,
                () => 1,
                entry.SetSliderValue,
                () => entry.IsSliderEnabled));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-upgrade",
                () => entry.UpgradeLabel,
                entry.Upgrade,
                entry.Focus,
                () => entry.IsUpgradeEnabled,
                () => entry.IsUpgradeVisible,
                () => entry.UpgradeTooltip));
        }
    }
}
