using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class GiftTownPopupScreen : Screen
    {
        private readonly GiftTownPopupAdapter _adapter;

        public GiftTownPopupScreen(GiftTownPopupAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            GiftTownPopup[] popups = Resources.FindObjectsOfTypeAll<GiftTownPopup>();
            for (int i = 0; i < popups.Length; i++)
            {
                GiftTownPopupAdapter adapter = new GiftTownPopupAdapter(popups[i]);
                if (adapter.IsPresent())
                {
                    return new GiftTownPopupScreen(adapter);
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

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
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
                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(GiftTownPopupAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("gift-town-popup", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildTownMenu("gift-town-popup-gift", adapter.GiftHeader, adapter.GetGiftTowns));
            root.AddChild(BuildTownMenu(
                "gift-town-popup-request",
                adapter.RequestHeader,
                adapter.GetRequestTowns,
                adapter.IsRequestMenuVisible));
            root.AddChild(new ButtonWidget(
                "gift-town-popup-close",
                () => ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.FocusClose,
                adapter.CanClose,
                getTooltip: () => adapter.CloseTooltip));
            return root;
        }

        private static MenuWidget BuildTownMenu(
            string id,
            string label,
            System.Func<IReadOnlyList<GiftTownPopupAdapter.TownItem>> getItems,
            System.Func<bool> isVisible = null)
        {
            MenuWidget menu = new MenuWidget(id, label, isVisible);
            IReadOnlyList<GiftTownPopupAdapter.TownItem> items = getItems != null
                ? getItems()
                : new GiftTownPopupAdapter.TownItem[0];
            for (int i = 0; i < items.Count; i++)
            {
                GiftTownPopupAdapter.TownItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    item.Id,
                    () => item.Label,
                    null,
                    item.Activate,
                    item.Focus,
                    () => item.IsVisible,
                    () => item.Tooltip,
                    null,
                    () => item.IsEnabled));
            }

            return menu;
        }
    }
}
