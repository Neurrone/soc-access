using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class SystemPopupAdapter : IMessageDialogAdapter, IInputDialogAdapter, IDisposable
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
        private Action<IUITextMeshInputField, string> _attachedSubmit;

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
            get { return string.Join(" ", BodyLines); }
        }

        /// <summary>The paragraphs the game broke the message into, kept apart rather than collapsed:
        /// the popup reads a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get { return SpokenLines.Of(new[] { GetActiveRawText(GetMessageText()) }); }
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
            get { return MenuButtonAdapterBase.IsButtonVisible(GetConfirmButton()); }
        }

        public bool HasNegativeAction
        {
            get { return MenuButtonAdapterBase.IsButtonVisible(GetCancelButton()); }
        }

        public bool IsPositiveActionEnabled
        {
            get { return MenuButtonAdapterBase.IsButtonEnabledAndVisible(GetConfirmButton()); }
        }

        public bool IsNegativeActionEnabled
        {
            get { return MenuButtonAdapterBase.IsButtonEnabledAndVisible(GetCancelButton()); }
        }

        public bool HasInputField
        {
            get
            {
                UITextMeshInputField inputField = GetInputField();
                return inputField != null && inputField.Active && inputField.Interactable;
            }
        }

        public IUITextMeshInputField InputField
        {
            get { return HasInputField ? GetInputField() : null; }
        }

        /// <summary>False: <c>SystemPopup</c> touches the input manager nowhere at all, so nothing in
        /// the game answers Escape while it is up.</summary>
        public bool GameHandlesEscape
        {
            get { return false; }
        }

        public Component ButtonOf(DialogAction action)
        {
            switch (action)
            {
                case DialogAction.Positive:
                    return GetConfirmButton();
                case DialogAction.Negative:
                    return GetCancelButton();
                default:
                    return null;
            }
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

            return HasPositiveAction || HasNegativeAction;
        }

        public void AttachInputSubmit(Action<IUITextMeshInputField, string> handler)
        {
            UITextMeshInputField inputField = GetInputField();
            if (inputField != null && handler != null)
            {
                inputField.OnSubmit = (Action<IUITextMeshInputField, string>)Delegate.Combine(inputField.OnSubmit, handler);
                _attachedSubmit = handler;
            }
        }

        public void DetachInputSubmit(Action<IUITextMeshInputField, string> handler)
        {
            UITextMeshInputField inputField = GetInputField();
            if (inputField != null && handler != null)
            {
                inputField.OnSubmit = (Action<IUITextMeshInputField, string>)Delegate.Remove(inputField.OnSubmit, handler);
            }

            if (ReferenceEquals(_attachedSubmit, handler))
            {
                _attachedSubmit = null;
            }
        }

        /// <summary>The slot has let this adapter go: the game's field must not be left holding a
        /// handler of ours (AGENTS.md, "Screen Resolution").</summary>
        public void Dispose()
        {
            DetachInputSubmit(_attachedSubmit);
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
                    return NativeSelectionUtility.Click(GetConfirmButton());
                case DialogAction.Negative:
                    return NativeSelectionUtility.Click(GetCancelButton());
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

        private static string GetButtonText(IUIButton button)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private static string GetActiveText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(GetActiveRawText(textMesh));
        }

        // The text as the game wrote it, line breaks and all; a mesh the game has hidden says nothing.
        private static string GetActiveRawText(IUITextMesh textMesh)
        {
            IUITransform transform = textMesh as IUITransform;
            if (transform != null && !transform.Active)
            {
                return string.Empty;
            }

            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }
    }
}
