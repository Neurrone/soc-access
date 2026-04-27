using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class QuestionDialogAdapter
    {
        private readonly object _sourceKey;
        private readonly IUITransform _containerTransform;
        private readonly UITextMeshInputField _inputField;
        private readonly IUIButton _positiveButton;
        private readonly IUIButton _negativeButton;
        private readonly string _title;
        private readonly string _body;
        private readonly string[] _actionLabels;

        private QuestionDialogAdapter(
            object sourceKey,
            IUITransform containerTransform,
            UITextMeshInputField inputField,
            IUIButton positiveButton,
            IUIButton negativeButton,
            string title,
            string body,
            string positiveLabel,
            string negativeLabel)
        {
            _sourceKey = sourceKey;
            _containerTransform = containerTransform;
            _inputField = inputField;
            _positiveButton = positiveButton;
            _negativeButton = negativeButton;
            _title = SpeechTextSanitizer.Normalize(title);
            _body = SpeechTextSanitizer.Normalize(body);
            _actionLabels = new[] { SpeechTextSanitizer.Normalize(positiveLabel), SpeechTextSanitizer.Normalize(negativeLabel) };
        }

        public QuestionDialogAdapter(object sourceKey, PopupMenu.Settings settings)
            : this(
                sourceKey,
                settings != null ? settings.ContainerTransform : null,
                settings != null ? settings.InputField : null,
                settings != null ? settings.PositiveButton : null,
                settings != null ? settings.NegativeButton : null,
                // Read popup text through UITextMeshTextUtility rather than the raw public
                // Text properties. On hot reload, PopupMenu can remain visible while those
                // public values revert to prefab placeholder content.
                GetText(settings != null ? settings.HeaderText : null),
                GetText(settings != null ? settings.MessageText : null),
                GetButtonText(settings != null ? settings.PositiveButton : null),
                GetButtonText(settings != null ? settings.NegativeButton : null))
        {
        }

        public object SourceKey
        {
            get { return _sourceKey; }
        }

        public string Title
        {
            get { return _title; }
        }

        public string Body
        {
            get { return _body; }
        }

        public string PositiveLabel
        {
            get { return GetActionLabel(0); }
        }

        public string NegativeLabel
        {
            get { return GetActionLabel(1); }
        }

        public bool IsPresent()
        {
            if (_containerTransform == null)
            {
                return false;
            }

            return _containerTransform.Active
                && IsButtonActive(_positiveButton)
                && IsButtonActive(_negativeButton)
                && _inputField != null
                && !_inputField.Active;
        }

        public void SyncNativeSelection(int focusIndex)
        {
            if (focusIndex <= 0)
            {
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                return;
            }

            Selectable selectable = null;
            switch (focusIndex)
            {
                case 1:
                    selectable = GetSelectable(_positiveButton);
                    break;
                case 2:
                    selectable = GetSelectable(_negativeButton);
                    break;
            }

            if (selectable == null)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                return;
            }

            selectable.Select();
        }

        public bool ActivateAction(int focusIndex)
        {
            switch (focusIndex)
            {
                case 1:
                    return InvokeButton(_positiveButton);
                case 2:
                    return InvokeButton(_negativeButton);
                default:
                    return false;
            }
        }

        private static bool IsButtonActive(IUIButton button)
        {
            return button != null && button.Active;
        }

        private static Selectable GetSelectable(IUIButton button)
        {
            if (button == null)
            {
                return null;
            }

            IUISelectableHolder holder = button;
            return holder.GetSelectable();
        }

        private string GetActionLabel(int index)
        {
            if (_actionLabels == null || index < 0 || index >= _actionLabels.Length)
            {
                return string.Empty;
            }

            return _actionLabels[index] ?? string.Empty;
        }

        private static bool InvokeButton(IUIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static string GetButtonText(IUIButton button)
        {
            return UITextMeshTextUtility.GetEffectiveButtonText(button);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }

    }
}
