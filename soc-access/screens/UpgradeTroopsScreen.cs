using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class UpgradeTroopsScreen : TroopManagementScreenBase
    {
        public UpgradeTroopsScreen(ITroopManagementHostAdapter host)
            : base(host)
        {
        }

        public static Screen TryBuildActiveDwellingScreen()
        {
            DwellingInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<DwellingInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DwellingInteractionMenuAdapter adapter = new DwellingInteractionMenuAdapter(menus[i]);
                if (adapter.IsUpgradePresent())
                {
                    return new UpgradeTroopsScreen(new DwellingTroopManagementHostAdapter(adapter));
                }
            }

            return null;
        }

        public static Screen TryBuildActiveSettlementScreen()
        {
            TownInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<TownInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                TownInteractionMenuAdapter adapter = new TownInteractionMenuAdapter(menus[i]);
                if (adapter.IsUpgradePresent())
                {
                    return new UpgradeTroopsScreen(new SettlementTroopManagementHostAdapter(adapter));
                }
            }

            return null;
        }

        public static Screen TryBuildActiveDefenceScreen()
        {
            DefenceMenu[] menus = Resources.FindObjectsOfTypeAll<DefenceMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DefenceMenuAdapter adapter = new DefenceMenuAdapter(menus[i]);
                if (adapter.IsUpgradePresent())
                {
                    return new UpgradeTroopsScreen(new DefenceTroopManagementHostAdapter(adapter));
                }
            }

            return null;
        }

        protected override string ScreenSuffix { get { return "upgrade-troops"; } }
        protected override string ScreenTitle { get { return Host != null ? Host.UpgradeScreenTitle : "Upgrade troops"; } }
        protected override bool IsContentPresent() { return Host != null && Host.IsUpgradePresent(); }

        protected override void AddContentWidgets(ContainerWidget root)
        {
            if (root == null || Host == null || Host.UpgradeTroops == null)
            {
                return;
            }

            UpgradeTroopsSubMenuAdapter subMenu = Host.UpgradeTroops;
            root.AddChild(new TextWidget(
                Host.IdPrefix + "-upgrade-none",
                () => subMenu.NoUpgradableTroopsText,
                Host.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => subMenu.IsNoUpgradableTroopsVisible));

            IReadOnlyList<UpgradeTroopsSubMenuAdapter.UpgradeEntry> entries = subMenu.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                AddUpgradeWidgets(root, entries[i]);
            }
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
