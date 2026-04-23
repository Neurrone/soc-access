using System;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.UI
{
    internal sealed class MenuItemWidget : Widget
    {
        private readonly Func<string> _getLabel;
        private readonly Func<string> _getStatus;
        private readonly Func<bool> _activate;
        private readonly Action _onFocus;
        private readonly Func<bool> _isVisible;

        public MenuItemWidget(
            string id,
            Func<string> getLabel,
            Func<string> getStatus,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isVisible)
            : base(id)
        {
            _getLabel = getLabel;
            _getStatus = getStatus;
            _activate = activate;
            _onFocus = onFocus;
            _isVisible = isVisible;
        }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        public override string GetLabel()
        {
            return _getLabel != null ? _getLabel() ?? string.Empty : string.Empty;
        }

        public override string GetStatus()
        {
            return _getStatus != null ? _getStatus() ?? string.Empty : string.Empty;
        }

        public override bool ClaimsAction(string actionKey)
        {
            return IsVisible && actionKey == AccessibilityActions.Activate.Key;
        }

        public override bool HandleAction(InputAction action)
        {
            if (!IsVisible || action == null || action.Key != AccessibilityActions.Activate.Key)
            {
                return false;
            }

            return _activate != null && _activate();
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
        }
    }
}
