using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal sealed class CheckboxWidget : Widget
    {
        private readonly Func<string> _getLabel;
        private readonly Action _toggle;
        private readonly Func<bool> _isChecked;
        private readonly Func<bool> _isVisible;
        private readonly Func<bool> _isEnabled;
        private readonly Func<Tooltip> _getTooltip;

        public CheckboxWidget(string id, string label, Action toggle, Func<bool> isChecked, Func<bool> isVisible = null)
            : this(id, () => label, toggle, isChecked, isVisible, null)
        {
        }

        public CheckboxWidget(string id, string label, Action toggle, Func<bool> isChecked, Func<bool> isVisible, Func<bool> isEnabled)
            : this(id, () => label, toggle, isChecked, isVisible, isEnabled, null)
        {
        }

        public CheckboxWidget(
            string id,
            Func<string> getLabel,
            Action toggle,
            Func<bool> isChecked,
            Func<bool> isVisible,
            Func<bool> isEnabled,
            Func<Tooltip> getTooltip = null)
            : base(id)
        {
            _getLabel = getLabel;
            _toggle = toggle;
            _isChecked = isChecked;
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
            return _getLabel != null ? (_getLabel() ?? string.Empty) : string.Empty;
        }

        public override string GetRole()
        {
            return "check box";
        }

        public override string GetStatus()
        {
            if (!IsEnabled())
            {
                return "disabled";
            }

            return IsChecked() ? "checked" : "unchecked";
        }

        public override Tooltip GetTooltip()
        {
            return _getTooltip != null ? _getTooltip() : null;
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

            if (_toggle == null)
            {
                return false;
            }

            _toggle();
            SpeechPipeline.Output(new SpeechRequest(GetStatus(), interrupt: false));
            return true;
        }

        private bool IsChecked()
        {
            return _isChecked != null && _isChecked();
        }

        private bool IsEnabled()
        {
            return _isEnabled == null || _isEnabled();
        }
    }
}
