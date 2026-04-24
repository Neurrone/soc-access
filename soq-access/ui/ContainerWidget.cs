using System.Collections.Generic;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.UI
{
    internal class ContainerWidget : Widget
    {
        private readonly List<Widget> _children = new List<Widget>();
        private int _focusedIndex = -1;

        public ContainerWidget(string id, string label)
            : base(id)
        {
            Label = label ?? string.Empty;
        }

        public string Label { get; set; }

        public override bool AnnounceName { get; } = false;

        public Widget FocusedChild
        {
            get
            {
                if (_focusedIndex < 0 || _focusedIndex >= _children.Count)
                {
                    return null;
                }

                return _children[_focusedIndex];
            }
        }

        public override string GetLabel()
        {
            return Label;
        }

        public void AddChild(Widget child)
        {
            if (child == null)
            {
                return;
            }

            child.Parent = this;
            _children.Add(child);
        }

        public bool SetFocusedChildById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                Widget child = _children[i];
                if (child != null && child.IsVisible && child.Id == id)
                {
                    _focusedIndex = i;
                    return true;
                }
            }

            return false;
        }

        protected override void OnFocus()
        {
            if (FocusedChild != null && FocusedChild.IsVisible)
            {
                FocusedChild.Focus();
                UIManager.SetFocusedWidget(FocusedChild.GetFocusedWidget());
                return;
            }

            SetFocus(FindFirstVisibleIndex());
        }

        public override Widget GetFocusedWidget()
        {
            return FocusedChild != null ? FocusedChild.GetFocusedWidget() : this;
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.NextWidget.Key
                || actionKey == AccessibilityActions.PreviousWidget.Key;
        }

        public override bool HasClaimInTree(string actionKey)
        {
            if (ClaimsAction(actionKey))
            {
                return true;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                Widget child = _children[i];
                if (child != null && child.IsVisible && child.HasClaimInTree(actionKey))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool HandleAction(InputAction action)
        {
            Widget focusedChild = FocusedChild;
            if (focusedChild != null && focusedChild.HandleAction(action))
            {
                return true;
            }

            if (action == null)
            {
                return false;
            }

            if (action.Key == AccessibilityActions.NextWidget.Key)
            {
                return MoveRelative(1);
            }

            if (action.Key == AccessibilityActions.PreviousWidget.Key)
            {
                return MoveRelative(-1);
            }

            return false;
        }

        private bool MoveRelative(int delta)
        {
            if (_children.Count == 0)
            {
                return false;
            }

            int nextIndex = _focusedIndex;
            if (nextIndex < 0)
            {
                nextIndex = delta > 0 ? -1 : _children.Count;
            }

            nextIndex += delta;
            while (nextIndex >= 0 && nextIndex < _children.Count)
            {
                if (_children[nextIndex].IsVisible)
                {
                    return SetFocus(nextIndex);
                }

                nextIndex += delta;
            }

            return false;
        }

        private int FindFirstVisibleIndex()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i].IsVisible)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool SetFocus(int index)
        {
            if (index < 0 || index >= _children.Count)
            {
                return false;
            }

            Widget next = _children[index];
            if (next == null || !next.IsVisible)
            {
                return false;
            }

            Widget previous = FocusedChild;
            if (previous != null && !ReferenceEquals(previous, next))
            {
                previous.Unfocus();
            }

            _focusedIndex = index;
            next.Focus();
            UIManager.SetFocusedWidget(next.GetFocusedWidget());
            return true;
        }
    }
}
