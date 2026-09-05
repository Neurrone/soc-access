using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    public sealed class FiveDigitCodeInputWidget : Widget
    {
        private readonly string _label;
        private readonly Func<string> _getValue;
        private readonly Action _onFocus;
        private readonly Func<bool> _activate;
        private readonly Func<bool> _isVisible;
        private string _lastValue;

        public FiveDigitCodeInputWidget(
            string id,
            string label,
            Func<string> getValue,
            Action onFocus,
            Func<bool> activate,
            Func<bool> isVisible)
            : base(id)
        {
            _label = label ?? string.Empty;
            _getValue = getValue;
            _onFocus = onFocus;
            _activate = activate;
            _isVisible = isVisible;
        }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        public override string GetLabel()
        {
            string value = GetValue();
            return string.IsNullOrWhiteSpace(value)
                ? _label
                : ModText.Get(ModStrings.Common.ListSeparator, _label, value);
        }

        public override string GetRole()
        {
            return ModText.Get(ModStrings.UI.RoleEdit);
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.Activate.Key;
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null || action.Key != AccessibilityActions.Activate.Key)
            {
                return false;
            }

            return _activate != null && _activate();
        }

        public override void Update()
        {
            string value = GetValue();
            if (value == _lastValue)
            {
                return;
            }

            if (_lastValue != null)
            {
                AnnounceChange(_lastValue, value);
            }

            _lastValue = value;
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
            _lastValue = GetValue();
        }

        protected override void OnUnfocus()
        {
            _lastValue = null;
        }

        private string GetValue()
        {
            return _getValue != null ? _getValue() ?? string.Empty : string.Empty;
        }

        private static void AnnounceChange(string previous, string current)
        {
            int prefix = CommonPrefixLength(previous ?? string.Empty, current ?? string.Empty);
            if (current != null && current.Length > prefix)
            {
                SpeechPipeline.Output(new SpeechRequest(current[prefix].ToString(), interrupt: true));
            }
            else
            {
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.Blank), interrupt: true));
            }
        }

        private static int CommonPrefixLength(string a, string b)
        {
            int length = Math.Min(a.Length, b.Length);
            int index = 0;
            while (index < length && a[index] == b[index])
            {
                index++;
            }

            return index;
        }
    }
}
