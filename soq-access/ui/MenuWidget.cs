using System.Collections.Generic;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.UI
{
    internal sealed class MenuWidget : Widget
    {
        private readonly List<MenuItemWidget> _items = new List<MenuItemWidget>();
        private int _focusedIndex = -1;

        public MenuWidget(string id, string label)
            : base(id)
        {
            Label = label ?? string.Empty;
        }

        public string Label { get; private set; }

        public MenuItemWidget FocusedItem
        {
            get
            {
                if (_focusedIndex < 0 || _focusedIndex >= _items.Count)
                {
                    return null;
                }

                return _items[_focusedIndex];
            }
        }

        public override string GetLabel()
        {
            return Label;
        }

        public override bool AnnounceName
        {
            get { return true; }
        }

        public override string GetRole()
        {
            return "menu";
        }

        public void AddItem(MenuItemWidget item)
        {
            if (item == null)
            {
                return;
            }

            item.Parent = this;
            _items.Add(item);
        }

        public bool SetFocusedItemById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                MenuItemWidget item = _items[i];
                if (item != null && item.IsVisible && item.Id == id)
                {
                    _focusedIndex = i;
                    return true;
                }
            }

            return false;
        }

        public override Widget GetFocusedWidget()
        {
            return FocusedItem != null ? FocusedItem.GetFocusedWidget() : this;
        }

        protected override void OnFocus()
        {
            if (FocusedItem != null && FocusedItem.IsVisible)
            {
                SoqAccessPlugin.Instance?.LogInfo("MenuWidget.OnFocus reusing focused item " + FocusedItem.Id);
                FocusedItem.Focus();
                UIManager.SetFocusedWidget(FocusedItem.GetFocusedWidget());
                return;
            }

            SoqAccessPlugin.Instance?.LogInfo("MenuWidget.OnFocus selecting first visible item");
            SetFocus(FindFirstVisibleIndex());
        }

        protected override void OnUnfocus()
        {
            FocusedItem?.Unfocus();
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.NextMenuItem.Key
                || actionKey == AccessibilityActions.PreviousMenuItem.Key
                || actionKey == AccessibilityActions.FirstMenuItem.Key
                || actionKey == AccessibilityActions.LastMenuItem.Key;
        }

        public override bool HasClaimInTree(string actionKey)
        {
            if (ClaimsAction(actionKey))
            {
                return true;
            }

            MenuItemWidget focusedItem = FocusedItem;
            return focusedItem != null && focusedItem.IsVisible && focusedItem.HasClaimInTree(actionKey);
        }

        public override bool HandleAction(InputAction action)
        {
            MenuItemWidget focusedItem = FocusedItem;
            if (focusedItem != null && focusedItem.HandleAction(action))
            {
                return true;
            }

            if (action == null)
            {
                return false;
            }

            switch (action.Key)
            {
                case "next_menu_item":
                    return MoveRelative(1);
                case "previous_menu_item":
                    return MoveRelative(-1);
                case "first_menu_item":
                    return SetFocus(FindFirstVisibleIndex());
                case "last_menu_item":
                    return SetFocus(FindLastVisibleIndex());
                default:
                    return false;
            }
        }

        private bool MoveRelative(int delta)
        {
            if (_items.Count == 0)
            {
                SoqAccessPlugin.Instance?.LogInfo("MenuWidget.MoveRelative ignored because there are no items");
                return false;
            }

            int nextIndex = _focusedIndex;
            if (nextIndex < 0)
            {
                nextIndex = delta > 0 ? -1 : _items.Count;
            }

            nextIndex += delta;
            while (nextIndex >= 0 && nextIndex < _items.Count)
            {
                if (_items[nextIndex].IsVisible)
                {
                    SoqAccessPlugin.Instance?.LogInfo("MenuWidget.MoveRelative moving focus to index " + nextIndex + " (" + _items[nextIndex].Id + ")");
                    return SetFocus(nextIndex);
                }

                nextIndex += delta;
            }

            return false;
        }

        private int FindFirstVisibleIndex()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsVisible)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindLastVisibleIndex()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].IsVisible)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool SetFocus(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                SoqAccessPlugin.Instance?.LogInfo("MenuWidget.SetFocus rejected invalid index " + index);
                return false;
            }

            MenuItemWidget next = _items[index];
            if (next == null || !next.IsVisible)
            {
                SoqAccessPlugin.Instance?.LogInfo("MenuWidget.SetFocus rejected index " + index + " because the item is not visible");
                return false;
            }

            MenuItemWidget previous = FocusedItem;
            if (previous != null && !ReferenceEquals(previous, next))
            {
                previous.Unfocus();
            }

            _focusedIndex = index;
            SoqAccessPlugin.Instance?.LogInfo("MenuWidget.SetFocus focused index " + index + " (" + next.Id + ")");
            next.Focus();
            UIManager.SetFocusedWidget(next.GetFocusedWidget());
            return true;
        }
    }
}
