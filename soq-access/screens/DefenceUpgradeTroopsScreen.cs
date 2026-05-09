using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class DefenceUpgradeTroopsScreen : Screen
    {
        private readonly DefenceMenuAdapter _adapter;

        public DefenceUpgradeTroopsScreen(DefenceMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsUpgradePresent();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
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

        private static ContainerWidget BuildRoot(DefenceMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("defences-upgrade-troops", "Upgrade defending troops");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                "defences-upgrade-tutorial",
                adapter.GetTutorialButtonLabel(),
                adapter.ActivateTutorial,
                adapter.HideNativeTooltip,
                adapter.IsTutorialButtonVisible,
                adapter.IsTutorialButtonVisible));

            root.AddChild(new TextWidget(
                "defences-upgrade-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            UpgradeTroopsSubMenuAdapter subMenu = adapter.UpgradeTroops;
            root.AddChild(new TextWidget(
                "defences-upgrade-none",
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
                "defences-upgrade-back",
                "Back",
                adapter.BackToTop,
                adapter.HideNativeTooltip,
                () => adapter.IsUpgradePresent()));

            root.AddChild(new ButtonWidget(
                "defences-upgrade-close",
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
