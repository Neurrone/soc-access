using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class UpgradeTroopsScreen : TroopManagementScreenBase
    {
        private string _selectedUpgradeId;

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
        protected override string ScreenTitle { get { return Host != null ? Host.UpgradeScreenTitle : string.Empty; } }
        protected override bool IsContentPresent() { return Host != null && Host.IsUpgradePresent(); }

        protected override void AddContentWidgets(ContainerWidget root)
        {
            if (root == null || Host == null || Host.UpgradeTroops == null)
            {
                return;
            }

            UpgradeTroopsSubMenuAdapter subMenu = Host.UpgradeTroops;
            IReadOnlyList<UpgradeTroopsSubMenuAdapter.UpgradeEntry> entries = subMenu.GetEntries();
            EnsureSelectedUpgrade(entries);

            root.AddChild(new TextWidget(
                Host.IdPrefix + "-upgrade-none",
                () => subMenu.NoUpgradableTroopsText,
                Host.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => subMenu.IsNoUpgradableTroopsVisible));

            root.AddChild(BuildTroopsMenu(entries));
            for (int i = 0; i < entries.Count; i++)
            {
                AddUpgradeWidgets(root, entries[i]);
            }
        }

        private MenuWidget BuildTroopsMenu(IReadOnlyList<UpgradeTroopsSubMenuAdapter.UpgradeEntry> entries)
        {
            string idPrefix = Host != null ? Host.IdPrefix + "-" + ScreenSuffix : "upgrade-troops";
            MenuWidget menu = new MenuWidget(
                idPrefix + "-troops",
                GameText.Get("Commanders/Tooltip/Troops", string.Empty),
                () => entries != null && entries.Count > 0);
            if (entries == null)
            {
                return menu;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                UpgradeTroopsSubMenuAdapter.UpgradeEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                UpgradeTroopsSubMenuAdapter.UpgradeEntry capturedEntry = entry;
                menu.AddItem(new MenuItemWidget(
                    capturedEntry.IdPrefix + "-menu-item",
                    () => BuildUpgradeChoiceLabel(capturedEntry),
                    () => BuildUpgradeStatus(capturedEntry),
                    () => SelectUpgrade(capturedEntry, playSound: true),
                    () => SelectUpgrade(capturedEntry, playSound: true),
                    () => true,
                    () => capturedEntry.CurrentTooltip));
            }

            menu.SetFocusedItemById(_selectedUpgradeId + "-menu-item");
            return menu;
        }

        private void AddUpgradeWidgets(ContainerWidget root, UpgradeTroopsSubMenuAdapter.UpgradeEntry entry)
        {
            if (root == null || entry == null)
            {
                return;
            }

            root.AddChild(new TextWidget(
                entry.IdPrefix + "-target",
                () => entry.TargetTroopText,
                entry.FocusTarget,
                includeParentLabelInAnnouncement: false,
                () => entry.TargetTooltip,
                () => IsSelectedUpgrade(entry)));

            root.AddChild(new SliderWidget(
                entry.IdPrefix + "-quantity",
                ModText.Get(ModStrings.Common.Quantity),
                () => ModText.Get(ModStrings.Screens.AmountToUpgrade, entry.SliderLabel),
                () => entry.SliderValue,
                () => entry.SliderMinimum,
                () => entry.SliderMaximum,
                () => 1,
                entry.SetSliderValue,
                () => entry.IsSliderEnabled,
                () => IsSelectedUpgrade(entry) && entry.IsSliderVisible));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-upgrade",
                () => entry.UpgradeLabel,
                entry.Upgrade,
                entry.Focus,
                () => entry.IsUpgradeEnabled,
                () => IsSelectedUpgrade(entry) && entry.IsUpgradeVisible,
                () => entry.UpgradeTooltip));
        }

        private void EnsureSelectedUpgrade(IReadOnlyList<UpgradeTroopsSubMenuAdapter.UpgradeEntry> entries)
        {
            if (FindUpgrade(entries, _selectedUpgradeId) != null)
            {
                return;
            }

            _selectedUpgradeId = entries != null && entries.Count > 0 && entries[0] != null
                ? entries[0].IdPrefix
                : null;
        }

        private static UpgradeTroopsSubMenuAdapter.UpgradeEntry FindUpgrade(
            IReadOnlyList<UpgradeTroopsSubMenuAdapter.UpgradeEntry> entries,
            string id)
        {
            if (entries == null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                UpgradeTroopsSubMenuAdapter.UpgradeEntry entry = entries[i];
                if (entry != null && entry.IdPrefix == id)
                {
                    return entry;
                }
            }

            return null;
        }

        private bool IsSelectedUpgrade(UpgradeTroopsSubMenuAdapter.UpgradeEntry entry)
        {
            return entry != null && entry.IdPrefix == _selectedUpgradeId;
        }

        private bool SelectUpgrade(UpgradeTroopsSubMenuAdapter.UpgradeEntry entry, bool playSound)
        {
            if (entry == null)
            {
                return false;
            }

            bool changed = _selectedUpgradeId != entry.IdPrefix;
            _selectedUpgradeId = entry.IdPrefix;
            entry.Focus();
            if (changed && playSound)
            {
                NativeSoundUtility.PostEvent("Common_DefaultClick");
            }

            return true;
        }

        private static string BuildUpgradeStatus(UpgradeTroopsSubMenuAdapter.UpgradeEntry entry)
        {
            return entry != null
                ? ModText.Plural(ModStrings.Draft.AvailableTroops, entry.AvailableTroops, entry.AvailableTroops)
                : string.Empty;
        }

        private static string BuildUpgradeChoiceLabel(UpgradeTroopsSubMenuAdapter.UpgradeEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            string current = entry.CurrentTroopName;
            string target = entry.TargetTroopName;
            if (string.IsNullOrWhiteSpace(target) || target == current)
            {
                return current;
            }

            return ModText.Get(ModStrings.Draft.UpgradeChoice, current, target);
        }
    }
}
