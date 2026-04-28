using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MoveTroopPopupScreen : Screen
    {
        private readonly MoveTroopPopupAdapter _adapter;

        public MoveTroopPopupScreen(MoveTroopPopupAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        private static ContainerWidget BuildRoot(MoveTroopPopupAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("move-troop-popup", "Move troops");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "move-troop-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "move-troop-max-size",
                () => adapter.MaxTroopSize,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(adapter.BuildMoveAllLeftButton());
            root.AddChild(adapter.BuildSplitButton());
            root.AddChild(adapter.BuildMoveAllRightButton());

            // Known minor issue: native TroopHUDEntryMovable stores SliderValue as the
            // right-side balance size, then remaps visible left/right amounts based on
            // drag direction. When moving troops right-to-left, keyboard right-arrow
            // increases the native value but can visually move the slider left. We keep
            // this native behavior for now because speech reports the resulting
            // "Left: X, right: Y" distribution.
            root.AddChild(adapter.BuildDistributionSlider());

            root.AddChild(new ButtonWidget(
                "move-troop-ok",
                "OK",
                adapter.Confirm,
                adapter.HideNativeTooltip,
                () => adapter.IsPresent()));

            root.AddChild(new ButtonWidget(
                "move-troop-cancel",
                "Cancel",
                adapter.Cancel,
                adapter.HideNativeTooltip,
                () => adapter.IsPresent()));

            return root;
        }
    }
}
