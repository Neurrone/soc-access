using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class SystemPopupAdapter : IQuestionDialogAdapter
    {
        private static readonly AccessTools.FieldRef<SystemPopup, UITextMesh> HeaderTextRef =
            AccessTools.FieldRefAccess<SystemPopup, UITextMesh>("_headerText");
        private static readonly AccessTools.FieldRef<SystemPopup, UITextMesh> MessageTextRef =
            AccessTools.FieldRefAccess<SystemPopup, UITextMesh>("_messageText");
        private static readonly AccessTools.FieldRef<SystemPopup, UITextMeshInputField> InputFieldRef =
            AccessTools.FieldRefAccess<SystemPopup, UITextMeshInputField>("_inputField");
        private static readonly AccessTools.FieldRef<SystemPopup, UIButton> ConfirmButtonRef =
            AccessTools.FieldRefAccess<SystemPopup, UIButton>("_confirmButton");
        private static readonly AccessTools.FieldRef<SystemPopup, UIButton> CancelButtonRef =
            AccessTools.FieldRefAccess<SystemPopup, UIButton>("_cancelButton");

        private readonly SystemPopup _popup;

        public SystemPopupAdapter(SystemPopup popup)
        {
            _popup = popup;
        }

        public object SourceKey
        {
            get { return _popup; }
        }

        public string Title
        {
            get { return GetActiveText(GetHeaderText()); }
        }

        public string Body
        {
            get { return GetActiveText(GetMessageText()); }
        }

        public string PositiveLabel
        {
            get { return GetButtonText(GetConfirmButton()); }
        }

        public string NegativeLabel
        {
            get { return GetButtonText(GetCancelButton()); }
        }

        public bool HasPositiveAction
        {
            get { return IsButtonActive(GetConfirmButton()); }
        }

        public bool HasNegativeAction
        {
            get { return IsButtonActive(GetCancelButton()); }
        }

        public bool IsPresent()
        {
            if (_popup == null)
            {
                return false;
            }

            GameObject gameObject = _popup.gameObject;
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            UITextMeshInputField inputField = GetInputField();
            return (inputField == null || !inputField.Active)
                && (HasPositiveAction || HasNegativeAction);
        }

        public void SyncNativeSelection(DialogAction action)
        {
            if (action == DialogAction.Body)
            {
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                return;
            }

            UIButton button = action == DialogAction.Positive ? GetConfirmButton() : GetCancelButton();
            Selectable selectable = button != null ? button.GetSelectable() : null;
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        public bool ActivateAction(DialogAction action)
        {
            switch (action)
            {
                case DialogAction.Positive:
                    return InvokeButton(GetConfirmButton());
                case DialogAction.Negative:
                    return InvokeButton(GetCancelButton());
                default:
                    return false;
            }
        }

        private UITextMesh GetHeaderText()
        {
            return _popup != null ? HeaderTextRef(_popup) : null;
        }

        private UITextMesh GetMessageText()
        {
            return _popup != null ? MessageTextRef(_popup) : null;
        }

        private UITextMeshInputField GetInputField()
        {
            return _popup != null ? InputFieldRef(_popup) : null;
        }

        private UIButton GetConfirmButton()
        {
            return _popup != null ? ConfirmButtonRef(_popup) : null;
        }

        private UIButton GetCancelButton()
        {
            return _popup != null ? CancelButtonRef(_popup) : null;
        }

        private static bool IsButtonActive(UIButton button)
        {
            return button != null && button.Active && MenuButtonAdapterBase.IsButtonVisible(button);
        }

        private static bool InvokeButton(UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static string GetButtonText(IUIButton button)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private static string GetActiveText(IUITextMesh textMesh)
        {
            IUITransform transform = textMesh as IUITransform;
            if (transform != null && !transform.Active)
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }
    }
}
