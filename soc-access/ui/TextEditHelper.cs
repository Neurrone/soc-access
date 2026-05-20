using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using TMPro;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TextEditHelper
    {
        private IUITextMeshInputField _field;
        private TMP_InputField _inputField;
        private string _cachedValue;
        private string _lastText;
        private int _lastCaret;
        private int _lastAnchor;
        private bool _suppressNextDiff;

        public bool IsEditing { get; private set; }

        public bool Begin(IUITextMeshInputField field)
        {
            if (field == null || !field.Interactable)
            {
                return false;
            }

            TMP_InputField inputField = GetNativeInputField(field);
            if (inputField == null)
            {
                return false;
            }

            _field = field;
            _inputField = inputField;
            _cachedValue = field.InputFieldValue ?? string.Empty;
            IsEditing = true;
            field.Select();
            field.ActivateInputField();
            ResetBaseline();
            Speak(ModText.Get(ModStrings.UI.Editing, FormatValue(_cachedValue)));
            return true;
        }

        public bool HandleAction(Input.InputAction action)
        {
            if (!IsEditing || action == null)
            {
                return false;
            }

            if (action.Key == Input.AccessibilityActions.Activate.Key)
            {
                Confirm();
                return true;
            }

            if (action.Key == Input.AccessibilityActions.Cancel.Key)
            {
                Cancel();
                return true;
            }

            return false;
        }

        public void Update()
        {
            if (!IsEditing)
            {
                return;
            }

            if (_field == null || _inputField == null)
            {
                IsEditing = false;
                return;
            }

            if (_suppressNextDiff)
            {
                _suppressNextDiff = false;
                CaptureBaseline();
                return;
            }

            DiffAndAnnounce();
        }

        public void Confirm()
        {
            if (!IsEditing)
            {
                return;
            }

            IsEditing = false;
            if (_inputField != null)
            {
                _inputField.DeactivateInputField();
            }

            Speak(ModText.Get(ModStrings.UI.DoneEditing, FormatValue(_field != null ? _field.InputFieldValue : null)));
            Clear();
        }

        public void Cancel()
        {
            if (!IsEditing)
            {
                return;
            }

            IsEditing = false;
            if (_field != null)
            {
                _field.InputFieldValue = _cachedValue ?? string.Empty;
            }

            if (_inputField != null)
            {
                _inputField.DeactivateInputField();
            }

            Speak(ModText.Get(ModStrings.UI.CancelledEditing, FormatValue(_cachedValue)));
            Clear();
        }

        private static TMP_InputField GetNativeInputField(IUITextMeshInputField field)
        {
            UITextMeshInputField concrete = field as UITextMeshInputField;
            return concrete != null ? concrete.GetInputField() : null;
        }

        private void ResetBaseline()
        {
            CaptureBaseline();
            _suppressNextDiff = true;
        }

        private void CaptureBaseline()
        {
            _lastText = _inputField != null ? _inputField.text ?? string.Empty : string.Empty;
            _lastCaret = _inputField != null ? _inputField.caretPosition : 0;
            _lastAnchor = _inputField != null ? _inputField.selectionAnchorPosition : 0;
        }

        private void DiffAndAnnounce()
        {
            string text = _inputField.text ?? string.Empty;
            int caret = _inputField.caretPosition;
            int anchor = _inputField.selectionAnchorPosition;

            if (text == _lastText && caret == _lastCaret && anchor == _lastAnchor)
            {
                return;
            }

            if (text != _lastText)
            {
                AnnounceTextChange(_lastText ?? string.Empty, text);
            }
            else
            {
                bool wasSelecting = _lastAnchor != _lastCaret;
                bool isSelecting = anchor != caret;
                if (isSelecting)
                {
                    AnnounceSelection(anchor, caret, text);
                }
                else if (wasSelecting)
                {
                    AnnounceCharacterAtCaret(caret, text);
                }
                else
                {
                    AnnounceCharacterAtCaret(caret, text);
                }
            }

            _lastText = text;
            _lastCaret = caret;
            _lastAnchor = anchor;
        }

        private static void AnnounceTextChange(string previous, string current)
        {
            int prefix = CommonPrefixLength(previous, current);
            int maxSuffix = System.Math.Min(previous.Length - prefix, current.Length - prefix);
            int suffix = CommonSuffixLength(previous, current, maxSuffix);
            int removedLength = previous.Length - prefix - suffix;

            if (removedLength > 0)
            {
                Speak(FormatSubstring(previous.Substring(prefix, removedLength)));
            }
        }

        private static void AnnounceSelection(int anchor, int caret, string text)
        {
            int start = System.Math.Min(anchor, caret);
            int length = System.Math.Abs(anchor - caret);
            if (length <= 0 || start < 0 || start >= text.Length)
            {
                return;
            }

            if (start + length > text.Length)
            {
                length = text.Length - start;
            }

            Speak(FormatSubstring(text.Substring(start, length)));
        }

        private static void AnnounceCharacterAtCaret(int caret, string text)
        {
            if (string.IsNullOrEmpty(text) || caret < 0 || caret >= text.Length)
            {
                Speak(ModText.Get(ModStrings.UI.Blank));
                return;
            }

            Speak(FormatCharacter(text[caret]));
        }

        private static int CommonPrefixLength(string a, string b)
        {
            int length = System.Math.Min(a.Length, b.Length);
            int index = 0;
            while (index < length && a[index] == b[index])
            {
                index++;
            }

            return index;
        }

        private static int CommonSuffixLength(string a, string b, int maxLength)
        {
            int index = 0;
            while (index < maxLength && a[a.Length - 1 - index] == b[b.Length - 1 - index])
            {
                index++;
            }

            return index;
        }

        private static string FormatValue(string value)
        {
            return string.IsNullOrEmpty(value) ? ModText.Get(ModStrings.UI.Blank) : value;
        }

        private static string FormatSubstring(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return ModText.Get(ModStrings.UI.Blank);
            }

            return value.Length == 1 ? FormatCharacter(value[0]) : value;
        }

        private static string FormatCharacter(char value)
        {
            switch (value)
            {
                case ' ':
                    return ModText.Get(ModStrings.UI.CharacterSpace);
                case '\t':
                    return ModText.Get(ModStrings.UI.CharacterTab);
                case '_':
                    return ModText.Get(ModStrings.UI.CharacterUnderscore);
                case '-':
                    return ModText.Get(ModStrings.UI.CharacterDash);
                case '.':
                    return ModText.Get(ModStrings.UI.CharacterDot);
                case ',':
                    return ModText.Get(ModStrings.UI.CharacterComma);
                case ':':
                    return ModText.Get(ModStrings.UI.CharacterColon);
                case ';':
                    return ModText.Get(ModStrings.UI.CharacterSemicolon);
                default:
                    return value.ToString();
            }
        }

        private static void Speak(string value)
        {
            SpeechPipeline.Output(new SpeechRequest(value, interrupt: true));
        }

        private void Clear()
        {
            _field = null;
            _inputField = null;
            _cachedValue = null;
            _lastText = null;
            _lastCaret = 0;
            _lastAnchor = 0;
            _suppressNextDiff = false;
        }
    }
}
