using System;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SongsOfConquestAccess.UI
{
    public sealed class TimeInputTextWidget : Widget
    {
        private enum ActiveField
        {
            None,
            Minutes,
            Seconds
        }

        private readonly string _label;
        private readonly Func<IUITimeInputField> _getField;
        private readonly Func<IUITextMeshInputField> _getMinutesField;
        private readonly Func<IUITextMeshInputField> _getSecondsField;
        private readonly Action _onFocus;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isVisible;
        private readonly Func<Tooltip> _getTooltip;
        private readonly TextInputEchoHelper _echo = new TextInputEchoHelper();
        private ActiveField _activeField;
        private TMP_InputField _nativeInputField;
        private string _lastText;
        private int _lastCaret;
        private int _lastAnchor;

        public TimeInputTextWidget(
            string id,
            string label,
            Func<IUITimeInputField> getField,
            Func<IUITextMeshInputField> getMinutesField,
            Func<IUITextMeshInputField> getSecondsField,
            Action onFocus,
            Func<bool> isEnabled,
            Func<bool> isVisible,
            Func<Tooltip> getTooltip = null)
            : base(id)
        {
            _label = label ?? string.Empty;
            _getField = getField;
            _getMinutesField = getMinutesField;
            _getSecondsField = getSecondsField;
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
            string value = GetActiveFieldValueText();

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

        public override void Update()
        {
            SynchronizeActiveFieldFromNativeFocus();
            HandleBoundaryNavigation();
            _echo.Update();
            CaptureBaseline();
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
            ActivateField(ActiveField.Minutes, null);
        }

        protected override void OnUnfocus()
        {
            _echo.Stop();
            DeactivateNativeField();
            _activeField = ActiveField.None;
            _nativeInputField = null;
            _lastText = null;
            _lastCaret = 0;
            _lastAnchor = 0;
        }

        private bool IsEnabled()
        {
            IUITimeInputField field = _getField != null ? _getField() : null;
            if (field != null && !field.Interactable)
            {
                return false;
            }

            return _isEnabled == null || _isEnabled();
        }

        private void HandleBoundaryNavigation()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _nativeInputField == null || !_nativeInputField.isFocused)
            {
                return;
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame
                && _activeField == ActiveField.Minutes
                && IsPreviousCaretAtEnd())
            {
                ActivateField(ActiveField.Seconds, 0);
                AnnounceCurrentField();
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame
                && _activeField == ActiveField.Seconds
                && IsPreviousCaretAtStart())
            {
                IUITextMeshInputField minutes = GetChildField(ActiveField.Minutes);
                TMP_InputField nativeMinutes = GetNativeInputField(minutes);
                int end = nativeMinutes != null && nativeMinutes.text != null ? nativeMinutes.text.Length : 0;
                ActivateField(ActiveField.Minutes, end);
                AnnounceCurrentField();
            }
        }

        private bool IsPreviousCaretAtEnd()
        {
            if (_lastAnchor != _lastCaret)
            {
                return false;
            }

            string text = _lastText ?? string.Empty;
            return _lastCaret >= text.Length;
        }

        private bool IsPreviousCaretAtStart()
        {
            return _lastAnchor == _lastCaret && _lastCaret <= 0;
        }

        private void SynchronizeActiveFieldFromNativeFocus()
        {
            IUITextMeshInputField minutes = GetChildField(ActiveField.Minutes);
            IUITextMeshInputField seconds = GetChildField(ActiveField.Seconds);
            TMP_InputField nativeMinutes = GetNativeInputField(minutes);
            TMP_InputField nativeSeconds = GetNativeInputField(seconds);

            if (nativeMinutes != null && nativeMinutes.isFocused && _activeField != ActiveField.Minutes)
            {
                BeginTracking(ActiveField.Minutes, minutes, nativeMinutes);
                return;
            }

            if (nativeSeconds != null && nativeSeconds.isFocused && _activeField != ActiveField.Seconds)
            {
                BeginTracking(ActiveField.Seconds, seconds, nativeSeconds);
            }
        }

        private void ActivateField(ActiveField field, int? caretPosition)
        {
            IUITextMeshInputField child = GetChildField(field);
            TMP_InputField inputField = GetNativeInputField(child);
            if (child == null || inputField == null || !child.Interactable)
            {
                return;
            }

            if (_activeField != field)
            {
                DeactivateNativeField();
            }

            child.Select();
            child.ActivateInputField();
            if (caretPosition.HasValue)
            {
                SetCaret(inputField, caretPosition.Value);
            }

            BeginTracking(field, child, inputField);
        }

        private void BeginTracking(ActiveField field, IUITextMeshInputField child, TMP_InputField inputField)
        {
            _activeField = field;
            _nativeInputField = inputField;
            _echo.Begin(child);
            CaptureBaseline();
        }

        private void DeactivateNativeField()
        {
            if (_nativeInputField != null)
            {
                _nativeInputField.DeactivateInputField();
            }
        }

        private void CaptureBaseline()
        {
            if (_nativeInputField == null)
            {
                _lastText = null;
                _lastCaret = 0;
                _lastAnchor = 0;
                return;
            }

            _lastText = _nativeInputField.text ?? string.Empty;
            _lastCaret = _nativeInputField.caretPosition;
            _lastAnchor = _nativeInputField.selectionAnchorPosition;
        }

        private void AnnounceCurrentField()
        {
            SpeechPipeline.Output(new SpeechRequest(GetLabel(), interrupt: true));
        }

        private string GetActiveFieldValueText()
        {
            string rawValue = GetActiveRawValue();
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                int numericValue;
                if (!int.TryParse(rawValue, out numericValue))
                {
                    numericValue = 0;
                }

                if (_activeField == ActiveField.Minutes)
                {
                    string fallback = numericValue == 1 ? "{0} minute" : "{0} minutes";
                    return GameText.Get("Adventure/PostGameMenu/TotalPlayTime/Minutes", fallback, numericValue);
                }

                if (_activeField == ActiveField.Seconds)
                {
                    string fallback = numericValue == 1 ? "{0} second" : "{0} seconds";
                    return GameText.Get("Adventure/PostGameMenu/TotalPlayTime/Seconds", fallback, numericValue);
                }
            }

            if (_activeField == ActiveField.None)
            {
                IUITimeInputField field = _getField != null ? _getField() : null;
                return field != null
                    ? field.MinutesValue + ":" + field.SecondsValue.ToString("D2")
                    : string.Empty;
            }

            return rawValue;
        }

        private string GetActiveRawValue()
        {
            IUITextMeshInputField activeChild = GetChildField(_activeField);
            string value = activeChild != null ? activeChild.InputFieldValue : string.Empty;
            if (string.IsNullOrWhiteSpace(value) && _nativeInputField != null)
            {
                value = _nativeInputField.text ?? string.Empty;
            }

            return value;
        }

        private IUITextMeshInputField GetChildField(ActiveField field)
        {
            if (field == ActiveField.Minutes)
            {
                return _getMinutesField != null ? _getMinutesField() : null;
            }

            if (field == ActiveField.Seconds)
            {
                return _getSecondsField != null ? _getSecondsField() : null;
            }

            return null;
        }

        private static TMP_InputField GetNativeInputField(IUITextMeshInputField field)
        {
            UITextMeshInputField concrete = field as UITextMeshInputField;
            return concrete != null ? concrete.GetInputField() : null;
        }

        private static void SetCaret(TMP_InputField inputField, int position)
        {
            if (inputField == null)
            {
                return;
            }

            string text = inputField.text ?? string.Empty;
            if (position < 0)
            {
                position = 0;
            }
            else if (position > text.Length)
            {
                position = text.Length;
            }

            inputField.caretPosition = position;
            inputField.selectionAnchorPosition = position;
            inputField.selectionFocusPosition = position;
        }
    }
}
