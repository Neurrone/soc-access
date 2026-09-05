using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class DraftTroopsScreen : TroopManagementScreenBase
    {
        private string _selectedRecruitId;

        public DraftTroopsScreen(ITroopManagementHostAdapter host)
            : base(host)
        {
        }

        public static Screen TryBuildActiveDwellingScreen()
        {
            DwellingInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<DwellingInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DwellingInteractionMenuAdapter adapter = new DwellingInteractionMenuAdapter(menus[i]);
                if (adapter.IsDraftPresent())
                {
                    return new DraftTroopsScreen(new DwellingTroopManagementHostAdapter(adapter));
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
                if (adapter.IsDraftPresent())
                {
                    return new DraftTroopsScreen(new SettlementTroopManagementHostAdapter(adapter));
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
                if (adapter.IsDraftPresent())
                {
                    return new DraftTroopsScreen(new DefenceTroopManagementHostAdapter(adapter));
                }
            }

            return null;
        }

        protected override string ScreenSuffix { get { return "draft-troops"; } }
        protected override string ScreenTitle { get { return Host != null ? Host.DraftScreenTitle : string.Empty; } }
        protected override bool IsContentPresent() { return Host != null && Host.IsDraftPresent(); }

        protected override void AddContentWidgets(ContainerWidget root)
        {
            if (root == null || Host == null || Host.PurchaseTroops == null)
            {
                return;
            }

            IReadOnlyList<PurchaseTroopsSubMenuAdapter.RecruitEntry> entries = Host.PurchaseTroops.GetRecruitEntries();
            EnsureSelectedRecruit(entries);
            root.AddChild(BuildTroopsMenu(entries));

            for (int i = 0; i < entries.Count; i++)
            {
                AddRecruitWidgets(root, entries[i]);
            }
        }

        private MenuWidget BuildTroopsMenu(IReadOnlyList<PurchaseTroopsSubMenuAdapter.RecruitEntry> entries)
        {
            string idPrefix = Host != null ? Host.IdPrefix + "-" + ScreenSuffix : "draft-troops";
            MenuWidget menu = new MenuWidget(idPrefix + "-troops", GameText.Get("Commanders/Tooltip/Troops", string.Empty));
            if (entries == null)
            {
                return menu;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                PurchaseTroopsSubMenuAdapter.RecruitEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                string capturedId = entry.IdPrefix;
                PurchaseTroopsSubMenuAdapter.RecruitEntry capturedEntry = entry;
                menu.AddItem(new MenuItemWidget(
                    capturedId + "-menu-item",
                    () => capturedEntry.TroopName,
                    () => BuildRecruitStatus(capturedEntry),
                    () => SelectRecruit(capturedEntry, playSound: true),
                    () => SelectRecruit(capturedEntry, playSound: true),
                    () => true,
                    () => capturedEntry.Tooltip));
            }

            menu.SetFocusedItemById(_selectedRecruitId + "-menu-item");
            return menu;
        }

        private void AddRecruitWidgets(ContainerWidget root, PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            if (root == null || entry == null || Host == null)
            {
                return;
            }

            root.AddChild(BuildEssenceMenu(entry));

            root.AddChild(new SliderWidget(
                entry.IdPrefix + "-quantity",
                () => ModText.Get(ModStrings.Common.Quantity),
                () => entry.SliderLabel,
                () => entry.SliderValue,
                () => entry.SliderMinimum,
                () => entry.SliderMaximum,
                () => 1,
                entry.SetSliderValue,
                () => entry.IsSliderEnabled,
                () => IsSelectedRecruit(entry) && entry.IsSliderVisible));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-purchase",
                () => BuildPurchaseLabel(entry.PurchaseCosts),
                entry.Purchase,
                entry.Focus,
                () => entry.IsPurchaseEnabled,
                () => IsSelectedRecruit(entry) && entry.IsPurchaseVisible,
                () => entry.PurchaseTooltip));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-upgrade-in-pool",
                () => ModText.Get(ModStrings.Draft.UpgradeAvailableTroops),
                entry.UpgradeInPool,
                entry.Focus,
                () => entry.IsUpgradeInPoolEnabled,
                () => IsSelectedRecruit(entry) && entry.IsUpgradeInPoolVisible,
                () => entry.UpgradeInPoolTooltip));
        }

        private void EnsureSelectedRecruit(IReadOnlyList<PurchaseTroopsSubMenuAdapter.RecruitEntry> entries)
        {
            if (FindRecruit(entries, _selectedRecruitId) != null)
            {
                return;
            }

            _selectedRecruitId = entries != null && entries.Count > 0 && entries[0] != null
                ? entries[0].IdPrefix
                : null;
        }

        private static PurchaseTroopsSubMenuAdapter.RecruitEntry FindRecruit(
            IReadOnlyList<PurchaseTroopsSubMenuAdapter.RecruitEntry> entries,
            string id)
        {
            if (entries == null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                PurchaseTroopsSubMenuAdapter.RecruitEntry entry = entries[i];
                if (entry != null && entry.IdPrefix == id)
                {
                    return entry;
                }
            }

            return null;
        }

        private bool IsSelectedRecruit(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            return entry != null && entry.IdPrefix == _selectedRecruitId;
        }

        private bool SelectRecruit(PurchaseTroopsSubMenuAdapter.RecruitEntry entry, bool playSound)
        {
            if (entry == null)
            {
                return false;
            }

            bool changed = _selectedRecruitId != entry.IdPrefix;
            _selectedRecruitId = entry.IdPrefix;
            entry.Focus();
            if (changed && playSound)
            {
                NativeSoundUtility.PostEvent("Common_DefaultClick");
            }

            return true;
        }

        private static string BuildRecruitStatus(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return entry.IsSliderVisible
                ? ModText.Plural(ModStrings.Draft.AvailableTroops, entry.AvailableTroops, entry.AvailableTroops)
                : entry.NoTroopsText;
        }

        private MenuWidget BuildEssenceMenu(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            MenuWidget menu = new MenuWidget(
                entry.IdPrefix + "-essence",
                ModText.Get(ModStrings.Draft.EssenceVariants),
                () => IsSelectedRecruit(entry) && entry.IsEssenceMenuVisible);
            AddEssenceItem(menu, entry, TroopUpgradeType.ArcanaUpgraded, GameText.Get("Units/Types/Arcana", "Arcana"));
            AddEssenceItem(menu, entry, TroopUpgradeType.CreationUpgraded, GameText.Get("Units/Types/Creation", "Creation"));
            AddEssenceItem(menu, entry, TroopUpgradeType.OrderUpgraded, GameText.Get("Units/Types/Order", "Order"));
            menu.SetFocusedItemById(entry.IdPrefix + "-essence-" + entry.CurrentEssenceVariant.ToString().ToLowerInvariant());
            return menu;
        }

        private static void AddEssenceItem(
            MenuWidget menu,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            TroopUpgradeType upgradeType,
            string label)
        {
            menu.AddItem(new MenuItemWidget(
                entry.IdPrefix + "-essence-" + upgradeType.ToString().ToLowerInvariant(),
                () => label,
                null,
                () => entry.SelectEssenceVariant(upgradeType),
                () => entry.SelectEssenceVariant(upgradeType),
                () => true));
        }

        private static string BuildPurchaseLabel(IReadOnlyList<PurchaseTroopsSubMenuAdapter.ResourceCostLine> costs)
        {
            List<string> parts = new List<string>();
            if (costs != null)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    PurchaseTroopsSubMenuAdapter.ResourceCostLine cost = costs[i];
                    if (cost != null)
                    {
                        parts.Add(ModText.Get(
                            ModStrings.Common.ResourceAmount,
                            cost.Amount,
                            GetResourceName(cost.ResourceType)));
                    }
                }
            }

            return parts.Count == 0
                ? ModText.Get(ModStrings.Draft.Purchase)
                : ModText.Get(
                    ModStrings.Draft.PurchaseForResources,
                    ModText.JoinList(parts));
        }

        private static string GetResourceName(ResourceType resourceType)
        {
            string fallback = FormatEnumName(resourceType.ToString());
            return GameText.Get("Common/Resource/" + resourceType, fallback);
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(char.ToLowerInvariant(c));
            }

            return new string(chars.ToArray());
        }
    }
}
