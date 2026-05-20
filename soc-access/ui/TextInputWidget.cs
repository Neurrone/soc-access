using System;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TextInputWidget : Widget
    {
        private readonly string _label;
        private readonly Func<IUITextMeshInputField> _getField;
        private readonly Func<bool> _activate;
        private readonly Action _onFocus;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isVisible;

        public TextInputWidget(
            string id,
            string label,
            Func<IUITextMeshInputField> getField,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isEnabled,
            Func<bool> isVisible)
            : base(id)
        {
            _label = label ?? string.Empty;
            _getField = getField;
            _activate = activate;
            _onFocus = onFocus;
            _isEnabled = isEnabled;
            _isVisible = isVisible;
        }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        public override string GetLabel()
        {
            IUITextMeshInputField field = _getField != null ? _getField() : null;
            string value = field != null ? field.InputFieldValue : string.Empty;
            return string.IsNullOrWhiteSpace(value)
                ? _label
                : ModText.Get(ModStrings.Common.ListSeparator, _label, value);
        }

        public override string GetRole()
        {
            return ModText.Get(ModStrings.UI.RoleEdit);
        }

        public override string GetStatus()
        {
            return IsEnabled() ? string.Empty : ModText.Get(ModStrings.UI.StatusDisabled);
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

        private bool IsEnabled()
        {
            return _isEnabled == null || _isEnabled();
        }
    }
}
