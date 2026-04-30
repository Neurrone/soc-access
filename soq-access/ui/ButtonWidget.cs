using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.UI
{
    internal sealed class ButtonWidget : Widget
    {
        private readonly Func<bool> _activate;
        private readonly Action _onFocus;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isVisible;
        private readonly string _label;
        private readonly Tooltip _tooltip;

        public ButtonWidget(string id, string label, Func<bool> activate, Action onFocus, Func<bool> isEnabled, Func<bool> isVisible = null, Tooltip tooltip = null)
            : base(id)
        {
            _label = label ?? string.Empty;
            _activate = activate;
            _onFocus = onFocus;
            _isEnabled = isEnabled;
            _isVisible = isVisible;
            _tooltip = tooltip;
        }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        public override string GetLabel()
        {
            return _label;
        }

        public override string GetRole()
        {
            return "button";
        }

        public override string GetStatus()
        {
            return IsEnabled() ? string.Empty : "disabled";
        }

        public override Tooltip GetTooltip()
        {
            return _tooltip;
        }

        public override bool ClaimsAction(string actionKey)
        {
            return IsEnabled() && actionKey == AccessibilityActions.Activate.Key;
        }

        public override bool HandleAction(InputAction action)
        {
            if (!IsEnabled() || action == null || action.Key != AccessibilityActions.Activate.Key)
            {
                return false;
            }

            return _activate != null && _activate();
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
        }

        private bool IsEnabled()
        {
            return _isEnabled == null || _isEnabled();
        }
    }
}
