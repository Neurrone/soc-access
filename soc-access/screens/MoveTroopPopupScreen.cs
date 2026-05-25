using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MoveTroopPopupScreen : Screen
    {
        private readonly MoveTroopPopupAdapter _adapter;

        public MoveTroopPopupScreen(MoveTroopPopupAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            TroopHUDEntryMovable[] movables = Resources.FindObjectsOfTypeAll<TroopHUDEntryMovable>();
            for (int i = 0; i < movables.Length; i++)
            {
                TroopHUDEntryMovable movable = movables[i];
                MoveTroopPopupAdapter adapter = new MoveTroopPopupAdapter(movable);
                if (adapter.IsPresent())
                {
                    return new MoveTroopPopupScreen(adapter);
                }
            }

            return null;
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

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool HasFocusedWidgetClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasFocusedWidgetClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.CanCancel() && _adapter.Cancel();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(MoveTroopPopupAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("move-troop-popup", adapter != null ? adapter.Title : string.Empty);
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
                () => BuildMaxTroopSize(adapter),
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "move-troop-move-all-left",
                () => ModText.Get(ModStrings.Screens.MoveAllLeft),
                adapter.MoveAllLeft,
                adapter.HideNativeTooltip,
                adapter.IsMoveAllLeftEnabled,
                getTooltip: () => adapter.MoveAllLeftTooltip));

            root.AddChild(new ButtonWidget(
                "move-troop-split-equal",
                () => adapter.SplitEqualLabel,
                adapter.SplitEqual,
                adapter.HideNativeTooltip,
                adapter.IsSplitEqualEnabled,
                getTooltip: () => adapter.SplitEqualTooltip));

            root.AddChild(new ButtonWidget(
                "move-troop-move-all-right",
                () => ModText.Get(ModStrings.Screens.MoveAllRight),
                adapter.MoveAllRight,
                adapter.HideNativeTooltip,
                adapter.IsMoveAllRightEnabled,
                getTooltip: () => adapter.MoveAllRightTooltip));

            // Known minor issue: native TroopHUDEntryMovable stores SliderValue as the
            // right-side balance size, then remaps visible left/right amounts based on
            // drag direction. When moving troops right-to-left, keyboard right-arrow
            // increases the native value but can visually move the slider left. We keep
            // this native behavior for now because speech reports the resulting
            // "Left: X, right: Y" distribution.
            root.AddChild(new SliderWidget(
                "move-troop-distribution",
                ModText.Get(ModStrings.Screens.TroopDistribution),
                () => BuildDistributionText(adapter),
                adapter.GetSliderValue,
                adapter.GetSliderMinimum,
                adapter.GetSliderMaximum,
                adapter.GetSliderStep,
                adapter.SetSliderValue,
                adapter.IsSliderEnabled));

            root.AddChild(new ButtonWidget(
                "move-troop-ok",
                ModText.Get(ModStrings.Screens.Ok),
                adapter.Confirm,
                adapter.HideNativeTooltip,
                adapter.CanConfirm));

            root.AddChild(new ButtonWidget(
                "move-troop-cancel",
                ModText.Get(ModStrings.Actions.Cancel),
                adapter.Cancel,
                adapter.HideNativeTooltip,
                adapter.CanCancel));

            return root;
        }

        private static string BuildMaxTroopSize(MoveTroopPopupAdapter adapter)
        {
            string amount = adapter != null ? adapter.MaxTroopSizeAmount : string.Empty;
            return string.IsNullOrWhiteSpace(amount)
                ? string.Empty
                : ModText.Get(ModStrings.Screens.MaxTroopSize, amount);
        }

        private static string BuildDistributionText(MoveTroopPopupAdapter adapter)
        {
            string left = adapter != null ? adapter.LeftAmount : string.Empty;
            string right = adapter != null ? adapter.RightAmount : string.Empty;
            return ModText.Get(
                ModStrings.Screens.LeftRightDistribution,
                string.IsNullOrWhiteSpace(left) ? "0" : left,
                string.IsNullOrWhiteSpace(right) ? "0" : right);
        }
    }
}
