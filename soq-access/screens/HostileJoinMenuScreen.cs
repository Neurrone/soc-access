using System;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class HostileJoinMenuScreen : Screen
    {
        private const int ArmyExchangeGridIndex = 2;

        private readonly HostileJoinMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;

        public HostileJoinMenuScreen(HostileJoinMenuAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRoot(adapter))
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
            _adapter?.Dispose();
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

            if (payload.ParentId != _adapter.AttackingCommanderId && payload.ParentId != _adapter.JoiningCommanderId)
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

        private static ContainerWidget BuildRoot(HostileJoinMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("hostile-join-menu", "Troops want to join");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "hostile-join-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "hostile-join-instructions",
                () => adapter.Instructions,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(adapter.BuildArmyExchangeGrid());

            root.AddChild(new ButtonWidget(
                "hostile-join-discard",
                adapter.DiscardLabel,
                adapter.ActivateDiscard,
                adapter.HideNativeTooltip,
                adapter.IsDiscardEnabled));

            root.AddChild(new ButtonWidget(
                "hostile-join-mass-move",
                adapter.MassMoveLabel,
                adapter.ActivateMassMove,
                adapter.FocusMassMove,
                adapter.IsMassMoveEnabled,
                tooltip: adapter.MassMoveTooltip));

            return root;
        }
    }
}
