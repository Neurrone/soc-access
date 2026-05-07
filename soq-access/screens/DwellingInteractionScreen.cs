using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class DwellingInteractionScreen : Screen
    {
        private readonly DwellingInteractionMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;
        private Action<ResourceUpdatedPayload> _resourceUpdatedHandler;
        private Action _recruitmentPoolUpdatedHandler;

        public DwellingInteractionScreen(DwellingInteractionMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnPush()
        {
            AttachListeners();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            DetachListeners();
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

                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh(bool focusAfterRefresh)
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot(_adapter);

            if (!focusAfterRefresh)
            {
                return;
            }

            if (RootWidget == null || !RootWidget.SetFocusByIndex(focusedIndex))
            {
                RootWidget?.Focus();
            }
        }

        private void AttachListeners()
        {
            IClientAdventureFacade facade = _adapter != null ? _adapter.Facade : null;
            if (facade == null || facade.Commands == null)
            {
                return;
            }

            _troopsUpdatedHandler = delegate(OnTroopsUpdatedPayload _) { RefreshIfTop(); };
            _resourceUpdatedHandler = delegate(ResourceUpdatedPayload _) { RefreshIfTop(); };
            _recruitmentPoolUpdatedHandler = RefreshIfTop;

            IClientCommandsFacade commands = facade.Commands;
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Combine(commands.OnTroopsUpdated, _troopsUpdatedHandler);
            commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Combine(commands.OnResourceUpdated, _resourceUpdatedHandler);
            commands.OnRecruitmentPoolUpdated = (Action)Delegate.Combine(commands.OnRecruitmentPoolUpdated, _recruitmentPoolUpdatedHandler);
        }

        private void DetachListeners()
        {
            IClientAdventureFacade facade = _adapter != null ? _adapter.Facade : null;
            if (facade == null || facade.Commands == null)
            {
                return;
            }

            IClientCommandsFacade commands = facade.Commands;
            if (_troopsUpdatedHandler != null)
            {
                commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Remove(commands.OnTroopsUpdated, _troopsUpdatedHandler);
                _troopsUpdatedHandler = null;
            }

            if (_resourceUpdatedHandler != null)
            {
                commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Remove(commands.OnResourceUpdated, _resourceUpdatedHandler);
                _resourceUpdatedHandler = null;
            }

            if (_recruitmentPoolUpdatedHandler != null)
            {
                commands.OnRecruitmentPoolUpdated = (Action)Delegate.Remove(commands.OnRecruitmentPoolUpdated, _recruitmentPoolUpdatedHandler);
                _recruitmentPoolUpdatedHandler = null;
            }
        }

        private void RefreshIfTop()
        {
            if (ReferenceEquals(SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen, this))
            {
                Refresh(true);
            }
        }

        private static ContainerWidget BuildRoot(DwellingInteractionMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("dwelling-interaction", adapter != null ? adapter.Title : "Dwelling");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "dwelling-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(Portrait.Static(
                "dwelling-wielder",
                () => adapter.WielderName,
                adapter.HideNativeTooltip,
                () => adapter.WielderTooltip));

            root.AddChild(TroopHudMenu.Build(
                "dwelling-troops",
                "Troops",
                adapter.Troops,
                () => true));

            IReadOnlyList<DwellingInteractionMenuAdapter.RecruitEntry> entries = adapter.GetRecruitEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                AddRecruitWidgets(root, entries[i], adapter);
            }

            root.AddChild(new ButtonWidget(
                "dwelling-close",
                "Close",
                adapter.Close,
                adapter.HideNativeTooltip,
                () => adapter.IsPresent()));

            return root;
        }

        private static void AddRecruitWidgets(
            ContainerWidget root,
            DwellingInteractionMenuAdapter.RecruitEntry entry,
            DwellingInteractionMenuAdapter adapter)
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
                getTooltip: () => entry.UpgradeInPoolTooltip));
        }

        private static MenuWidget BuildEssenceMenu(DwellingInteractionMenuAdapter.RecruitEntry entry)
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
            DwellingInteractionMenuAdapter.RecruitEntry entry,
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

        private static string BuildPurchaseLabel(IReadOnlyList<DwellingInteractionMenuAdapter.ResourceCostLine> costs)
        {
            List<string> parts = new List<string>();
            if (costs != null)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    DwellingInteractionMenuAdapter.ResourceCostLine cost = costs[i];
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
