using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    internal sealed class ButtonWidget : Widget
    {
        private readonly Func<bool> _activate;
        private readonly Action _onFocus;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isVisible;
        private readonly Func<string> _getLabel;
        private readonly Func<Tooltip> _getTooltip;

        public ButtonWidget(string id, string label, Func<bool> activate, Action onFocus, Func<bool> isEnabled, Func<bool> isVisible = null, Tooltip tooltip = null)
            : this(id, () => label, activate, onFocus, isEnabled, isVisible, () => tooltip)
        {
        }

        public ButtonWidget(
            string id,
            Func<string> getLabel,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isEnabled,
            Func<bool> isVisible = null,
            Func<Tooltip> getTooltip = null)
            : base(id)
        {
            _getLabel = getLabel;
            _activate = activate;
            _onFocus = onFocus;
            _isEnabled = isEnabled;
            _isVisible = isVisible;
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

        public override string GetRole()
        {
            return ModText.Get(ModStrings.UI.RoleButton);
        }

        public override string GetStatus()
        {
            return IsEnabled() ? string.Empty : ModText.Get(ModStrings.UI.StatusDisabled);
        }

        public override Tooltip GetTooltip()
        {
            return _getTooltip != null ? _getTooltip() : null;
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
