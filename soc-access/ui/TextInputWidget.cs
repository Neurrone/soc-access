using System;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using TMPro;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TextInputWidget : Widget
    {
        private readonly string _label;
        private readonly Func<IUITextMeshInputField> _getField;
        private readonly Action _onFocus;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isVisible;
        private readonly Func<Tooltip> _getTooltip;
        private readonly TextInputEchoHelper _echo = new TextInputEchoHelper();

        public TextInputWidget(
            string id,
            string label,
            Func<IUITextMeshInputField> getField,
            Func<bool> activate,
            Action onFocus,
            Func<bool> isEnabled,
            Func<bool> isVisible,
            Func<Tooltip> getTooltip = null)
            : base(id)
        {
            _label = label ?? string.Empty;
            _getField = getField;
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

        public override Tooltip GetTooltip()
        {
            return _getTooltip != null ? _getTooltip() : null;
        }

        public override bool ClaimsAction(string actionKey)
        {
            return false;
        }

        public override bool HandleAction(InputAction action)
        {
            return false;
        }

        public override void Update()
        {
            _echo.Update();
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
            IUITextMeshInputField field = ActivateNativeField();
            _echo.Begin(field);
        }

        protected override void OnUnfocus()
        {
            _echo.Stop();
            DeactivateNativeField();
        }

        private bool IsEnabled()
        {
            return _isEnabled == null || _isEnabled();
        }

        private IUITextMeshInputField ActivateNativeField()
        {
            if (!IsVisible || !IsEnabled())
            {
                return null;
            }

            IUITextMeshInputField field = _getField != null ? _getField() : null;
            if (field == null || !field.Interactable)
            {
                return null;
            }

            field.Select();
            field.ActivateInputField();
            return field;
        }

        private void DeactivateNativeField()
        {
            IUITextMeshInputField field = _getField != null ? _getField() : null;
            UITextMeshInputField concrete = field as UITextMeshInputField;
            TMP_InputField inputField = concrete != null ? concrete.GetInputField() : null;
            if (inputField != null)
            {
                inputField.DeactivateInputField();
            }
        }
    }
}
