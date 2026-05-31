using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SendResourcePopupScreen : Screen
    {
        private readonly SendResourcePopupAdapter _adapter;

        public SendResourcePopupScreen(SendResourcePopupAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            SendResourcePopup[] popups = Resources.FindObjectsOfTypeAll<SendResourcePopup>();
            for (int i = 0; i < popups.Length; i++)
            {
                SendResourcePopupAdapter adapter = new SendResourcePopupAdapter(popups[i]);
                if (adapter.IsPresent())
                {
                    return new SendResourcePopupScreen(adapter);
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

        private static ContainerWidget BuildRoot(SendResourcePopupAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("send-resource-popup", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildResourceMenu("send-resource-popup-send", adapter.SendHeader, adapter.GetSendResources));
            root.AddChild(BuildResourceMenu(
                "send-resource-popup-request",
                adapter.RequestHeader,
                adapter.GetRequestResources,
                adapter.IsRequestMenuVisible));
            root.AddChild(new ButtonWidget(
                "send-resource-popup-close",
                () => ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.FocusClose,
                adapter.CanClose,
                getTooltip: () => adapter.CloseTooltip));
            return root;
        }

        private static MenuWidget BuildResourceMenu(
            string id,
            string label,
            System.Func<IReadOnlyList<SendResourcePopupAdapter.ResourceItem>> getItems,
            System.Func<bool> isVisible = null)
        {
            MenuWidget menu = new MenuWidget(id, label, isVisible);
            IReadOnlyList<SendResourcePopupAdapter.ResourceItem> items = getItems != null
                ? getItems()
                : new SendResourcePopupAdapter.ResourceItem[0];
            for (int i = 0; i < items.Count; i++)
            {
                SendResourcePopupAdapter.ResourceItem item = items[i];
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
