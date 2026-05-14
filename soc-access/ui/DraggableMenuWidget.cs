using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal sealed class DraggableMenuWidget : MenuWidget
    {
        private readonly Func<DraggableMenuItemWidget, MenuItemWidget, bool> _drop;
        private DraggableMenuItemWidget _dragSource;

        public DraggableMenuWidget(
            string id,
            string label,
            Func<DraggableMenuItemWidget, MenuItemWidget, bool> drop)
            : this(id, label, null, drop)
        {
        }

        public DraggableMenuWidget(
            string id,
            string label,
            Func<bool> isVisible,
            Func<DraggableMenuItemWidget, MenuItemWidget, bool> drop)
            : base(id, label, isVisible)
        {
            _drop = drop;
        }

        public DraggableMenuItemWidget DragSource
        {
            get { return _dragSource; }
        }

        protected override bool ClaimsFocusedItemAction(string actionKey)
        {
            return actionKey == AccessibilityActions.StartDrag.Key
                || actionKey == AccessibilityActions.Activate.Key
                || (_dragSource != null && actionKey == AccessibilityActions.Cancel.Key)
                || base.ClaimsFocusedItemAction(actionKey);
        }

        protected override bool HandleFocusedItemAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (action.Key == AccessibilityActions.StartDrag.Key)
            {
                return StartDrag();
            }

            if (action.Key == AccessibilityActions.Activate.Key && _dragSource != null)
            {
                return CompleteDrag();
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                DraggableMenuItemWidget item = FocusedItemOrNull as DraggableMenuItemWidget;
                if (item != null && item.CanDrag && !item.HasActivateBehavior)
                {
                    Speak("Press space to drag.");
                    return true;
                }
            }

            if (action.Key == AccessibilityActions.Cancel.Key && _dragSource != null)
            {
                return CancelDrag();
            }

            return base.HandleFocusedItemAction(action);
        }

        protected override void OnUnfocus()
        {
            ClearDrag();
            base.OnUnfocus();
        }

        private bool StartDrag()
        {
            DraggableMenuItemWidget source = FocusedItemOrNull as DraggableMenuItemWidget;
            if (source == null || !source.CanDrag)
            {
                return true;
            }

            _dragSource = source;
            Speak("Started drag. Move to destination and press enter to drop.");
            return true;
        }

        private bool CompleteDrag()
        {
            if (_dragSource == null)
            {
                Speak("Press space to select an item to drag.");
                return true;
            }

            MenuItemWidget target = FocusedItemOrNull;
            if (target == null)
            {
                Speak("Invalid destination.");
                return true;
            }

            if (ReferenceEquals(target, _dragSource))
            {
                return true;
            }

            DraggableMenuItemWidget source = _dragSource;
            if (_drop != null && _drop(source, target))
            {
                _dragSource = null;
            }

            return true;
        }

        private bool CancelDrag()
        {
            if (_dragSource == null)
            {
                return false;
            }

            ClearDrag();
            Speak("Drag cancelled.");
            return true;
        }

        private void ClearDrag()
        {
            _dragSource = null;
        }

        private static void Speak(string text)
        {
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
        }
    }
}
