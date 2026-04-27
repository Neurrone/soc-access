using System;
using SongsOfConquest.Client.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal abstract class MenuButtonAdapterBase : IMenuButtonAdapter
    {
        private readonly Func<bool> _isVisible;
        private readonly Func<bool> _activate;

        protected MenuButtonAdapterBase(
            string id,
            UIButton button,
            Func<bool> isVisible,
            Func<bool> activate)
        {
            Id = id ?? string.Empty;
            Button = button;
            _isVisible = isVisible;
            _activate = activate;
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

            return NativeSelectionUtility.Click(button);
        }

        protected abstract string BuildLabel();
    }
}
