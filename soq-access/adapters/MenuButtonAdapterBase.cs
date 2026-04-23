using System;
using SongsOfConquest.Client.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal abstract class MenuButtonAdapterBase : IMenuButtonAdapter
    {
        private readonly Func<bool> _isVisible;
        private readonly Func<bool> _activate;
        private readonly MenuButtonFocusMode _focusMode;

        protected MenuButtonAdapterBase(
            string id,
            UIButton button,
            Func<bool> isVisible,
            Func<bool> activate,
            MenuButtonFocusMode focusMode)
        {
            Id = id ?? string.Empty;
            Button = button;
            _isVisible = isVisible;
            _activate = activate;
            _focusMode = focusMode;
        }

        public string Id { get; private set; }

        public UIButton Button { get; private set; }

        public string GetLabel()
        {
            return BuildLabel();
        }

        public virtual string GetStatus()
        {
            return Button != null && !Button.Interactable ? "disabled" : string.Empty;
        }

        public bool IsVisible()
        {
            return (_isVisible == null || _isVisible()) && IsButtonVisible(Button);
        }

        public void Focus()
        {
            if (_focusMode == MenuButtonFocusMode.SemanticOnly)
            {
                return;
            }

            SelectButton(Button);
        }

        public bool Activate()
        {
            if (!IsVisible())
            {
                return false;
            }

            if (_activate != null)
            {
                return _activate();
            }

            return InvokeButton(Button);
        }

        public static bool IsButtonVisible(UIButton button)
        {
            if (button == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)button).gameObject;
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            Selectable selectable = button.GetSelectable();
            return selectable != null && selectable.isActiveAndEnabled;
        }

        protected static bool InvokeButton(UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            button.OnClicked?.Invoke();
            return true;
        }

        protected abstract string BuildLabel();

        private static void SelectButton(UIButton button)
        {
            if (button == null)
            {
                return;
            }

            Selectable selectable = button.GetSelectable();
            if (selectable == null)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                return;
            }

            selectable.Select();
        }
    }
}
