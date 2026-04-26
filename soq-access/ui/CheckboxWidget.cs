using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal sealed class CheckboxWidget : Widget
    {
        private readonly string _label;
        private readonly Action _toggle;
        private readonly Func<bool> _isChecked;
        private readonly Func<bool> _isVisible;

        public CheckboxWidget(string id, string label, Action toggle, Func<bool> isChecked, Func<bool> isVisible = null)
            : base(id)
        {
            _label = label ?? string.Empty;
            _toggle = toggle;
            _isChecked = isChecked;
            _isVisible = isVisible;
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
            return "check box";
        }

        public override string GetStatus()
        {
            return IsChecked() ? "checked" : "unchecked";
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

            if (_toggle == null)
            {
                return false;
            }

            _toggle();
            SpeechPipeline.Output(new SpeechRequest(GetStatus(), interrupt: true));
            return true;
        }

        private bool IsChecked()
        {
            return _isChecked != null && _isChecked();
        }
    }
}
