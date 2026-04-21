using System;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.UI
{
    internal sealed class ButtonWidget : Widget
    {
        private readonly Func<bool> _activate;
        private readonly Action _onFocus;
        private readonly Func<bool> _isEnabled;
        private readonly string _label;

        public ButtonWidget(string id, string label, Func<bool> activate, Action onFocus, Func<bool> isEnabled)
            : base(id)
        {
            _label = label ?? string.Empty;
            _activate = activate;
            _onFocus = onFocus;
            _isEnabled = isEnabled;
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
