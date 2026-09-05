using System.Collections.Generic;
using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    internal class MenuWidget : Widget
    {
        private readonly List<MenuItemWidget> _items = new List<MenuItemWidget>();
        private readonly Func<bool> _isVisible;
        private readonly Action _onFocus;
        private readonly Action _onUnfocus;
        private int _focusedIndex = -1;

        public MenuWidget(string id, string label)
            : this(id, label, null)
        {
        }

        public MenuWidget(string id, string label, Func<bool> isVisible)
            : this(id, label, isVisible, null, null)
        {
        }

        public MenuWidget(string id, string label, Func<bool> isVisible, Action onFocus, Action onUnfocus)
            : base(id)
        {
            Label = label ?? string.Empty;
            _isVisible = isVisible;
            _onFocus = onFocus;
            _onUnfocus = onUnfocus;
        }

        public string Label { get; private set; }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

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

        public int FocusedIndex
        {
            get { return _focusedIndex; }
        }

        protected MenuItemWidget FocusedItemOrNull
        {
            get { return FocusedItem; }
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
            return ModText.Get(ModStrings.UI.RoleMenu);
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

        internal override IEnumerable<Widget> EnumerateChildren()
        {
            return _items;
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

        public bool SetFocusByIndex(int index)
        {
            return SetFocus(ClampToVisibleIndex(index), announce: true);
        }

        public bool SetFocusByIndexSilently(int index)
        {
            index = ClampToVisibleIndex(index);
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            MenuItemWidget item = _items[index];
            if (item == null || !item.IsVisible)
            {
                return false;
            }

            _focusedIndex = index;
            return true;
        }

        public override Widget GetFocusedWidget()
        {
            return FocusedItem != null ? FocusedItem.GetFocusedWidget() : this;
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
            EnsureFocus();
        }

        protected override void OnUnfocus()
        {
            FocusedItem?.Unfocus();
            _onUnfocus?.Invoke();
        }

        public override bool ClaimsAction(string actionKey)
        {
            return ClaimsMenuNavigationAction(actionKey);
        }

        public override bool EnsureFocus()
        {
            if (FocusedItem == null || !FocusedItem.IsVisible)
            {
                _focusedIndex = FindFirstVisibleIndex();
            }

            return FocusedItem != null;
        }

        public override bool HasClaimInTree(string actionKey)
        {
            if (ClaimsAction(actionKey))
            {
                return true;
            }

            return ClaimsFocusedItemAction(actionKey);
        }

        public override bool HasFocusedClaimInTree(string actionKey)
        {
            return HasClaimInTree(actionKey);
        }

        public override bool HandleAction(InputAction action)
        {
            if (HandleFocusedItemAction(action))
            {
                return true;
            }

            if (action == null)
            {
                return false;
            }

            return HandleMenuNavigationAction(action);
        }

        protected virtual bool ClaimsFocusedItemAction(string actionKey)
        {
            MenuItemWidget focusedItem = FocusedItem;
            return focusedItem != null && focusedItem.IsVisible && focusedItem.HasClaimInTree(actionKey);
        }

        protected virtual bool HandleFocusedItemAction(InputAction action)
        {
            MenuItemWidget focusedItem = FocusedItem;
            return focusedItem != null && focusedItem.HandleAction(action);
        }

        protected virtual bool ClaimsMenuNavigationAction(string actionKey)
        {
            return actionKey == AccessibilityActions.NextMenuItem.Key
                || actionKey == AccessibilityActions.PreviousMenuItem.Key
                || actionKey == AccessibilityActions.FirstMenuItem.Key
                || actionKey == AccessibilityActions.LastMenuItem.Key;
        }

        protected virtual bool HandleMenuNavigationAction(InputAction action)
        {
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
                    return SetFocus(FindFirstVisibleIndex(), announce: true);
                case "last_menu_item":
                    return SetFocus(FindLastVisibleIndex(), announce: true);
                default:
                    return false;
            }
        }

        protected bool MoveRelative(int delta)
        {
            if (_items.Count == 0)
            {
                return false;
            }

            MenuItemWidget focusedItem = FocusedItem;
            if (focusedItem != null && !focusedItem.IsVisible)
            {
                return SetFocus(ClampToVisibleIndex(_focusedIndex), announce: true);
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
                    return SetFocus(nextIndex, announce: true);
                }

                nextIndex += delta;
            }

            return false;
        }

        protected int FindFirstVisibleIndex()
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

        protected int FindLastVisibleIndex()
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

        protected int ClampToVisibleIndex(int index)
        {
            if (_items.Count == 0)
            {
                return -1;
            }

            if (index < 0)
            {
                return FindFirstVisibleIndex();
            }

            if (index >= _items.Count)
            {
                return FindLastVisibleIndex();
            }

            if (_items[index].IsVisible)
            {
                return index;
            }

            for (int i = index; i < _items.Count; i++)
            {
                if (_items[i].IsVisible)
                {
                    return i;
                }
            }

            for (int i = index - 1; i >= 0; i--)
            {
                if (_items[i].IsVisible)
                {
                    return i;
                }
            }

            return -1;
        }

        protected bool SetFocus(int index, bool announce)
        {
            if (index < 0 || index >= _items.Count)
            {
                return false;
            }

            MenuItemWidget next = _items[index];
            if (next == null || !next.IsVisible)
            {
                return false;
            }

            _focusedIndex = index;
            if (announce)
            {
                UIManager.RequestFocus(next);
            }
            else
            {
                UIManager.RequestFocusSilently(next);
            }

            return true;
        }
    }
}
