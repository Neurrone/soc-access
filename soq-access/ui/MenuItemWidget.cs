using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.UI
{
    internal class MenuItemWidget : Widget
    {
        private readonly Func<string> _getLabel;
        private readonly Func<string> _getStatus;
        private readonly Func<bool> _activate;
        private readonly Action _onFocus;
        private readonly Action _onUnfocus;
        private readonly Func<bool> _isVisible;
        private readonly Func<bool> _isEnabled;
        private readonly Func<Tooltip> _getTooltip;

        public MenuItemWidget(
            string id,
            Func<string> getLabel,
            Func<string> getStatus,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isVisible,
            Tooltip tooltip = null,
            Action onUnfocus = null,
            Func<bool> isEnabled = null)
            : this(id, getLabel, getStatus, activate, onFocus, isVisible, () => tooltip, onUnfocus, isEnabled)
        {
        }

        public MenuItemWidget(
            string id,
            Func<string> getLabel,
            Func<string> getStatus,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isVisible,
            Func<Tooltip> getTooltip,
            Action onUnfocus = null,
            Func<bool> isEnabled = null)
            : base(id)
        {
            _getLabel = getLabel;
            _getStatus = getStatus;
            _activate = activate;
            _onFocus = onFocus;
            _onUnfocus = onUnfocus;
            _isVisible = isVisible;
            _isEnabled = isEnabled;
            _getTooltip = getTooltip;
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
            if (!IsEnabled())
            {
                return "disabled";
            }

            return _getStatus != null ? _getStatus() ?? string.Empty : string.Empty;
        }

        public override Tooltip GetTooltip()
        {
            return _getTooltip != null ? _getTooltip() : null;
        }

        public bool HasActivateBehavior
        {
            get { return _activate != null; }
        }

        public override bool ClaimsAction(string actionKey)
        {
            return IsVisible && IsEnabled() && actionKey == AccessibilityActions.Activate.Key;
        }

        public override bool HandleAction(InputAction action)
        {
            if (!IsVisible || !IsEnabled() || action == null || action.Key != AccessibilityActions.Activate.Key)
            {
                return false;
            }

            return _activate != null && _activate();
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
        }

        protected override void OnUnfocus()
        {
            _onUnfocus?.Invoke();
        }

        protected bool IsEnabled()
        {
            return _isEnabled == null || _isEnabled();
        }
    }
}
