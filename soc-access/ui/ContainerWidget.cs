using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.UI
{
    public class ContainerWidget : Widget
    {
        private const string FocusWrapCueKey = "Common_ClickUnfold";

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

        public int FocusedIndex
        {
            get { return _focusedIndex; }
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

        public override IEnumerable<Widget> EnumerateChildren()
        {
            return _children;
        }

        public Widget GetChildAt(int index)
        {
            if (index < 0 || index >= _children.Count)
            {
                return null;
            }

            return _children[index];
        }

        public Widget GetChildById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                Widget child = _children[i];
                if (child != null && child.Id == id)
                {
                    return child;
                }
            }

            return null;
        }

        public bool ReplaceChildAt(int index, Widget child)
        {
            if (child == null || index < 0 || index >= _children.Count)
            {
                return false;
            }

            Widget previous = _children[index];
            bool wasFocused = _focusedIndex == index;
            if (wasFocused)
            {
                previous?.Unfocus();
            }

            if (previous != null)
            {
                previous.Parent = null;
            }

            child.Parent = this;
            _children[index] = child;

            return true;
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

        public bool SetFocusByIndex(int index)
        {
            return SetFocus(ClampToVisibleIndex(index), announce: true);
        }

        public bool SetFocusByIndexSilently(int index)
        {
            return SetFocus(ClampToVisibleIndex(index), announce: false);
        }

        protected override void OnFocus()
        {
            EnsureFocus();
        }

        protected override void OnUnfocus()
        {
            FocusedChild?.Unfocus();
        }

        public override Widget GetFocusedWidget()
        {
            return FocusedChild != null ? FocusedChild.GetFocusedWidget() : this;
        }

        public override bool EnsureFocus()
        {
            if (FocusedChild == null || !FocusedChild.IsVisible)
            {
                _focusedIndex = FindFirstVisibleIndex();
            }

            Widget focusedChild = FocusedChild;
            return focusedChild != null && focusedChild.EnsureFocus();
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

        public override bool HasFocusedClaimInTree(string actionKey)
        {
            if (ClaimsAction(actionKey))
            {
                return true;
            }

            Widget focusedChild = FocusedChild;
            return focusedChild != null
                && focusedChild.IsVisible
                && focusedChild.HasFocusedClaimInTree(actionKey);
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

        public override void Update()
        {
            Widget focusedChild = FocusedChild;
            if (focusedChild != null && focusedChild.IsVisible)
            {
                focusedChild.Update();
            }
        }

        private bool MoveRelative(int delta)
        {
            if (_children.Count == 0)
            {
                return false;
            }

            Widget focusedChild = FocusedChild;
            if (focusedChild != null && !focusedChild.IsVisible)
            {
                return SetFocus(ClampToVisibleIndex(_focusedIndex), announce: true);
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
                    return SetFocus(nextIndex, announce: true);
                }

                nextIndex += delta;
            }

            if (CountVisibleChildren() <= 1)
            {
                return false;
            }

            int wrappedIndex = delta > 0 ? FindFirstVisibleIndex() : FindLastVisibleIndex();
            if (SetFocus(wrappedIndex, announce: true))
            {
                NativeSoundUtility.PostEvent(FocusWrapCueKey);
                return true;
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

        private int FindLastVisibleIndex()
        {
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                if (_children[i].IsVisible)
                {
                    return i;
                }
            }

            return -1;
        }

        private int CountVisibleChildren()
        {
            int count = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i] != null && _children[i].IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private int ClampToVisibleIndex(int index)
        {
            if (_children.Count == 0)
            {
                return -1;
            }

            if (index < 0)
            {
                return FindFirstVisibleIndex();
            }

            if (index >= _children.Count)
            {
                return FindLastVisibleIndex();
            }

            if (_children[index].IsVisible)
            {
                return index;
            }

            for (int i = index; i < _children.Count; i++)
            {
                if (_children[i].IsVisible)
                {
                    return i;
                }
            }

            for (int i = index - 1; i >= 0; i--)
            {
                if (_children[i].IsVisible)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool SetFocus(int index, bool announce)
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

            _focusedIndex = index;
            next.EnsureFocus();
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
