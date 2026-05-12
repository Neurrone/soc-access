using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Client.Deployment;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PreBattleMenuScreen : Screen
    {
        private readonly PreBattleMenuAdapter _adapter;
        private readonly TroopPlacementHexGrid _hexGrid;
        private System.Action<OnChangedPayload> _deploymentChangedHandler;

        public PreBattleMenuScreen(PreBattleMenuAdapter adapter)
            : this(adapter, new TroopPlacementHexGrid(adapter))
        {
        }

        private PreBattleMenuScreen(PreBattleMenuAdapter adapter, TroopPlacementHexGrid hexGrid)
            : base(BuildRoot(adapter, hexGrid))
        {
            _adapter = adapter;
            _hexGrid = hexGrid;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnPush()
        {
            _deploymentChangedHandler = HandleDeploymentChanged;
            _adapter?.AddDeploymentChangedHandler(_deploymentChangedHandler);
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            if (_deploymentChangedHandler != null)
            {
                _adapter?.RemoveDeploymentChangedHandler(_deploymentChangedHandler);
                _deploymentChangedHandler = null;
            }

            _adapter?.HideNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        private void HandleDeploymentChanged(OnChangedPayload payload)
        {
            _hexGrid?.RebuildAfterPlacementChanged();
        }

        private static ContainerWidget BuildRoot(PreBattleMenuAdapter adapter, TroopPlacementHexGrid hexGrid)
        {
            ContainerWidget root = new ContainerWidget("pre-battle-menu", "Troop placement");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(Portrait.Static(
                "pre-battle-our-wielder",
                () => adapter.OurWielderText,
                adapter.FocusOurWielder,
                () => adapter.OurWielderTooltip));

            root.AddChild(Portrait.Static(
                "pre-battle-opponent",
                () => adapter.OpponentText,
                adapter.FocusOpponent,
                getTooltip: null));

            root.AddChild(new TextWidget(
                "pre-battle-instructions",
                () => adapter.InstructionText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(hexGrid);
            root.AddChild(new ButtonWidget(
                "pre-battle-withdraw",
                adapter.WithdrawButtonLabel,
                adapter.Withdraw,
                adapter.FocusWithdrawButton,
                adapter.IsWithdrawButtonEnabled,
                adapter.IsWithdrawButtonVisible,
                adapter.WithdrawButtonTooltip));

            root.AddChild(new ButtonWidget(
                "pre-battle-manual-battle",
                adapter.ManualBattleButtonLabel,
                adapter.ManualBattle,
                adapter.FocusManualBattleButton,
                adapter.IsManualBattleButtonEnabled,
                adapter.IsManualBattleButtonVisible,
                adapter.ManualBattleButtonTooltip));

            root.AddChild(new ButtonWidget(
                "pre-battle-quick-battle",
                adapter.QuickBattleButtonLabel,
                adapter.QuickBattle,
                adapter.FocusQuickBattleButton,
                adapter.IsQuickBattleButtonEnabled,
                adapter.IsQuickBattleButtonVisible,
                adapter.QuickBattleButtonTooltip));

            root.AddChild(new ButtonWidget(
                "pre-battle-ready",
                adapter.ReadyButtonLabel,
                adapter.Ready,
                adapter.FocusReadyButton,
                adapter.IsReadyButtonEnabled,
                adapter.IsReadyButtonVisible,
                adapter.ReadyButtonTooltip));
            return root;
        }
    }
}
