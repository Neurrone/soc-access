using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MoveTroopPopupScreen : Screen
    {
        private static readonly System.Reflection.FieldInfo CurrentStateField =
            AccessTools.Field(typeof(TroopHUDEntryMovable), "_currentState");

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
                if (IsPresent(movable))
                {
                    return new MoveTroopPopupScreen(new MoveTroopPopupAdapter(movable));
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

        private static bool IsPresent(TroopHUDEntryMovable movable)
        {
            if (movable == null || !((Component)movable).gameObject.activeInHierarchy)
            {
                return false;
            }

            object value = CurrentStateField != null ? CurrentStateField.GetValue(movable) : null;
            return value != null && value.ToString() == "Deciding";
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
                () => adapter.MaxTroopSize,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "move-troop-move-all-left",
                adapter.MoveAllLeftLabel,
                adapter.MoveAllLeft,
                adapter.HideNativeTooltip,
                adapter.IsMoveAllLeftEnabled,
                tooltip: adapter.MoveAllLeftTooltip));

            root.AddChild(new ButtonWidget(
                "move-troop-split-equal",
                adapter.SplitEqualLabel,
                adapter.SplitEqual,
                adapter.HideNativeTooltip,
                adapter.IsSplitEqualEnabled,
                tooltip: adapter.SplitEqualTooltip));

            root.AddChild(new ButtonWidget(
                "move-troop-move-all-right",
                adapter.MoveAllRightLabel,
                adapter.MoveAllRight,
                adapter.HideNativeTooltip,
                adapter.IsMoveAllRightEnabled,
                tooltip: adapter.MoveAllRightTooltip));

            // Known minor issue: native TroopHUDEntryMovable stores SliderValue as the
            // right-side balance size, then remaps visible left/right amounts based on
            // drag direction. When moving troops right-to-left, keyboard right-arrow
            // increases the native value but can visually move the slider left. We keep
            // this native behavior for now because speech reports the resulting
            // "Left: X, right: Y" distribution.
            root.AddChild(new SliderWidget(
                "move-troop-distribution",
                ModText.Get(ModStrings.Screens.TroopDistribution),
                adapter.GetDistributionText,
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
                () => adapter.IsPresent()));

            root.AddChild(new ButtonWidget(
                "move-troop-cancel",
                ModText.Get(ModStrings.Actions.Cancel),
                adapter.Cancel,
                adapter.HideNativeTooltip,
                () => adapter.IsPresent()));

            return root;
        }
    }
}
