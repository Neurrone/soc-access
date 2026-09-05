using System;
using System.Collections.Generic;
using System.Globalization;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Economy;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class MarketplaceScreen : Screen
    {
        private const int ResourcesMenuIndex = 1;

        private readonly MarketplaceMenuAdapter _adapter;
        private Action<ResourceUpdatedPayload> _resourceUpdatedHandler;

        public MarketplaceScreen(MarketplaceMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            MarketplaceMenu[] menus = Resources.FindObjectsOfTypeAll<MarketplaceMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MarketplaceMenuAdapter adapter = new MarketplaceMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new MarketplaceScreen(adapter);
                }
            }

            return null;
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

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int resourceFocusedIndex = GetFocusedResourceIndex();
            RootWidget = BuildRoot(_adapter);
            RestoreResourceFocus(resourceFocusedIndex);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null || _resourceUpdatedHandler != null)
            {
                return;
            }

            _resourceUpdatedHandler = HandleResourceUpdated;
            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Combine(commands.OnResourceUpdated, _resourceUpdatedHandler);
        }

        private void DetachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null || _resourceUpdatedHandler == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Remove(commands.OnResourceUpdated, _resourceUpdatedHandler);
            _resourceUpdatedHandler = null;
        }

        private void HandleResourceUpdated(ResourceUpdatedPayload payload)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnMarketplaceChanged();
        }

        private int GetFocusedResourceIndex()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(ResourcesMenuIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private void RestoreResourceFocus(int resourceFocusedIndex)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(ResourcesMenuIndex) as MenuWidget : null;
            menu?.SetFocusByIndexSilently(resourceFocusedIndex);
        }

        private static ContainerWidget BuildRoot(MarketplaceMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("marketplace", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "marketplace-summary",
                () => adapter.Summary,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.Summary)));

            root.AddChild(BuildResourcesMenu(adapter));
            AddTradeButton(root, adapter.GetTradeAction(isBuyButton: false, amount: 1));
            AddTradeButton(root, adapter.GetTradeAction(isBuyButton: false, amount: 5));
            AddTradeButton(root, adapter.GetTradeAction(isBuyButton: true, amount: 1));
            AddTradeButton(root, adapter.GetTradeAction(isBuyButton: true, amount: 5));
            root.AddChild(new TextWidget(
                "marketplace-tip",
                () => adapter.TipText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(adapter.TipText)));

            root.AddChild(new ButtonWidget(
                "marketplace-close",
                ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static MenuWidget BuildResourcesMenu(MarketplaceMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("marketplace-resources", ModText.Get(ModStrings.Screens.Resources));
            IReadOnlyList<MarketplaceMenuAdapter.ResourceItem> resources = adapter.GetResources();
            for (int i = 0; i < resources.Count; i++)
            {
                MarketplaceMenuAdapter.ResourceItem resource = resources[i];
                ResourceType capturedType = resource.ResourceType;
                menu.AddItem(new MenuItemWidget(
                    "marketplace-resource-" + capturedType.ToString().ToLowerInvariant(),
                    () => BuildResourceLabel(resource),
                    null,
                    () => false,
                    () => adapter.SelectResource(capturedType),
                    () => true));
            }

            return menu;
        }

        private static void AddTradeButton(ContainerWidget root, MarketplaceMenuAdapter.TradeActionItem action)
        {
            root.AddChild(new ButtonWidget(
                BuildTradeActionId(action),
                () => BuildTradeActionLabel(action),
                action.Activate,
                action.Focus,
                action.IsEnabled,
                action.IsVisible));
        }

        private static string BuildResourceLabel(MarketplaceMenuAdapter.ResourceItem resource)
        {
            if (resource == null)
            {
                return string.Empty;
            }

            return ModText.Get(ModStrings.UI.LabelValue, resource.ResourceName, FormatAmount(resource.Amount));
        }

        private static string BuildTradeActionId(MarketplaceMenuAdapter.TradeActionItem action)
        {
            string operation = action != null && action.IsBuyButton ? "buy" : "sell";
            int amount = action != null ? action.Amount : 0;
            return "marketplace-" + operation + "-" + amount;
        }

        private static string BuildTradeActionLabel(MarketplaceMenuAdapter.TradeActionItem action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            if (action.ResourceType == ResourceType.Gold || action.IsVisible == null || !action.IsVisible())
            {
                return string.Empty;
            }

            string resourceAmount = ModText.Get(
                ModStrings.Common.ResourceAmount,
                FormatAmount(action.Amount),
                action.ResourceName);
            string goldAmount = ModText.Get(
                ModStrings.Common.ResourceAmount,
                FormatAmount(action.GoldAmount),
                action.GoldResourceName);

            return action.IsBuyButton
                ? ModText.Get(ModStrings.Screens.BuyResourceForGold, resourceAmount, goldAmount)
                : ModText.Get(ModStrings.Screens.SellResourceForGold, resourceAmount, goldAmount);
        }

        private static string FormatAmount(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
