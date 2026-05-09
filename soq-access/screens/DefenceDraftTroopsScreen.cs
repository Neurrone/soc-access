using System.Collections.Generic;
using SongsOfConquest.Client;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate.Faction;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class DefenceDraftTroopsScreen : Screen
    {
        private readonly DefenceMenuAdapter _adapter;

        public DefenceDraftTroopsScreen(DefenceMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsDraftPresent();
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
            ContainerWidget root = new ContainerWidget("defences-draft-troops", "Draft defending troops");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                "defences-draft-tutorial",
                adapter.GetTutorialButtonLabel(),
                adapter.ActivateTutorial,
                adapter.HideNativeTooltip,
                adapter.IsTutorialButtonVisible,
                adapter.IsTutorialButtonVisible));

            root.AddChild(new TextWidget(
                "defences-draft-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            PurchaseTroopsSubMenuAdapter subMenu = adapter.PurchaseTroops;
            IReadOnlyList<PurchaseTroopsSubMenuAdapter.RecruitEntry> entries = subMenu.GetRecruitEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                AddRecruitWidgets(root, entries[i], adapter);
            }

            root.AddChild(new ButtonWidget(
                "defences-draft-back",
                "Back",
                adapter.BackToTop,
                adapter.HideNativeTooltip,
                () => adapter.IsDraftPresent()));

            root.AddChild(new ButtonWidget(
                "defences-draft-close",
                () => adapter.CloseLabel,
                adapter.Close,
                adapter.HideNativeTooltip,
                () => adapter.IsDraftPresent()));

            return root;
        }

        private static void AddRecruitWidgets(
            ContainerWidget root,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            DefenceMenuAdapter adapter)
        {
            if (root == null || entry == null)
            {
                return;
            }

            root.AddChild(new TextWidget(
                entry.IdPrefix + "-name",
                () => entry.TroopName,
                entry.Focus,
                includeParentLabelInAnnouncement: false,
                () => entry.Tooltip));

            if (entry.IsEssenceMenuVisible)
            {
                root.AddChild(BuildEssenceMenu(entry));
            }

            root.AddChild(new TextWidget(
                entry.IdPrefix + "-no-troops",
                () => entry.NoTroopsText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => entry.IsNoTroopsVisible));

            root.AddChild(new SliderWidget(
                entry.IdPrefix + "-quantity",
                "quantity",
                () => entry.SliderLabel,
                () => entry.SliderValue,
                () => entry.SliderMinimum,
                () => entry.SliderMaximum,
                () => 1,
                entry.SetSliderValue,
                () => entry.IsSliderEnabled,
                () => entry.IsSliderVisible));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-purchase",
                () => BuildPurchaseLabel(entry.PurchaseCosts),
                entry.Purchase,
                entry.Focus,
                () => entry.IsPurchaseEnabled,
                () => entry.IsPurchaseVisible,
                () => entry.PurchaseTooltip));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-upgrade-in-pool",
                () => "Upgrade available troops",
                entry.UpgradeInPool,
                entry.Focus,
                () => entry.IsUpgradeInPoolEnabled,
                () => entry.IsUpgradeInPoolVisible,
                () => entry.UpgradeInPoolTooltip));
        }

        private static MenuWidget BuildEssenceMenu(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            MenuWidget menu = new MenuWidget(entry.IdPrefix + "-essence", "Essence variants");
            AddEssenceItem(menu, entry, TroopUpgradeType.ArcanaUpgraded, "Arcana");
            AddEssenceItem(menu, entry, TroopUpgradeType.CreationUpgraded, "Creation");
            AddEssenceItem(menu, entry, TroopUpgradeType.OrderUpgraded, "Order");
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
                        parts.Add(cost.Amount + " " + GetResourceName(cost.ResourceType));
                    }
                }
            }

            return parts.Count == 0 ? "Purchase" : "Purchase for " + JoinWithAnd(parts);
        }

        private static string GetResourceName(ResourceType resourceType)
        {
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            string fallback = FormatEnumName(resourceType.ToString());
            if (localization == null)
            {
                return fallback;
            }

            string key = "Common/Resource/" + resourceType;
            string text = localization.GetText(key);
            return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
        }

        private static string JoinWithAnd(List<string> parts)
        {
            if (parts.Count == 1)
            {
                return parts[0];
            }

            if (parts.Count == 2)
            {
                return parts[0] + " and " + parts[1];
            }

            return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + ", and " + parts[parts.Count - 1];
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
