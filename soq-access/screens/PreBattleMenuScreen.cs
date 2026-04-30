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
            : base(adapter != null ? adapter.SourceKey : null, BuildRoot(adapter, hexGrid))
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

            root.AddChild(new TextWidget(
                "pre-battle-our-wielder",
                () => adapter.OurWielderText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "pre-battle-opponent",
                () => adapter.OpponentText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "pre-battle-instructions",
                () => adapter.InstructionText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(hexGrid);
            root.AddChild(adapter.BuildWithdrawButton());
            root.AddChild(adapter.BuildManualBattleButton());
            root.AddChild(adapter.BuildQuickBattleButton());
            root.AddChild(adapter.BuildReadyButton());
            return root;
        }
    }
}
