using System;
using SongsOfConquest.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public abstract class MenuButtonAdapterBase : IMenuButtonAdapter
    {
        private readonly Func<bool> _isVisible;
        private readonly Func<bool> _activate;

        protected MenuButtonAdapterBase(
            UIButton button,
            Func<bool> isVisible,
            Func<bool> activate)
        {
            Button = button;
            _isVisible = isVisible;
            _activate = activate;
        }

        public UIButton Button { get; private set; }

        public string GetLabel()
        {
            return BuildLabel();
        }

        public virtual string GetStatus()
        {
            return string.Empty;
        }

        public bool IsVisible()
        {
            return (_isVisible == null || _isVisible()) && IsButtonVisible(Button);
        }

        public bool IsEnabled()
        {
            return Button != null && Button.Interactable;
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

            return NativeSelectionUtility.Click(Button);
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

        /// <summary>The button's object is drawn. <c>IUIButton.Active</c> is the object's OWN active
        /// flag, so a button under a hidden parent still reports true for it; this is the question
        /// that asks the hierarchy.</summary>
        public static bool IsButtonDrawn(UIButton button)
        {
            return GameObjects.IsLive(button as Component);
        }

        /// <summary>The game says the button can be pressed. It says nothing about the button being
        /// drawn: see <see cref="IsButtonDrawn"/> and <see cref="IsButtonVisible"/>.</summary>
        public static bool IsButtonEnabled(IUIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }

        /// <summary>The button can be pressed and its object is drawn.</summary>
        public static bool IsButtonEnabledAndDrawn(UIButton button)
        {
            return IsButtonEnabled(button) && IsButtonDrawn(button);
        }

        /// <summary>The button can be pressed and is visible by the stricter rule of
        /// <see cref="IsButtonVisible"/>: the selectable behind it is active and enabled too.</summary>
        public static bool IsButtonEnabledAndVisible(UIButton button)
        {
            return IsButtonVisible(button) && button.Interactable;
        }

        protected abstract string BuildLabel();
    }
}
