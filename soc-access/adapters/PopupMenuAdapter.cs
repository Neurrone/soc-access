using System;
using System.Collections.Generic;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PopupMenuAdapter : IMessageDialogAdapter, IInputDialogAdapter, IDisposable
    {
        private readonly object _sourceKey;
        private readonly PopupMenu.Settings _settings;
        private Action<IUITextMeshInputField, string> _attachedSubmit;

        /// <summary>The ONE popup object the game reuses for every message it shows, so nothing is
        /// read at construction: the heading, the body and the button labels are whatever the popup
        /// is drawing when they are asked for.</summary>
        public PopupMenuAdapter(object sourceKey, PopupMenu.Settings settings)
        {
            _sourceKey = sourceKey;
            _settings = settings;
        }

        public object SourceKey
        {
            get { return _sourceKey; }
        }

        /// <summary>Read through <c>UITextMeshTextUtility</c> rather than the raw public Text
        /// properties: on a hot reload the popup can stay visible while those revert to the prefab's
        /// placeholder content.</summary>
        public string Title
        {
            get { return SpokenLines.Clean(GetActiveText(Header)); }
        }

        public string Body
        {
            get { return string.Join(" ", BodyLines); }
        }

        /// <summary>The paragraphs the game broke the message into, kept apart rather than collapsed:
        /// the popup reads a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get { return SpokenLines.Of(new[] { GetActiveText(Message) }); }
        }

        public string PositiveLabel
        {
            get { return SpokenLines.Clean(GetButtonText(PositiveButton)); }
        }

        public string NegativeLabel
        {
            get { return SpokenLines.Clean(GetButtonText(NegativeButton)); }
        }

        private IUITextMesh Header
        {
            get { return _settings != null ? _settings.HeaderText : null; }
        }

        private IUITextMesh Message
        {
            get { return _settings != null ? _settings.MessageText : null; }
        }

        private IUITransform ContainerTransform
        {
            get { return _settings != null ? _settings.ContainerTransform : null; }
        }

        private UITextMeshInputField Field
        {
            get { return _settings != null ? _settings.InputField : null; }
        }

        private IUIButton PositiveButton
        {
            get { return _settings != null ? _settings.PositiveButton : null; }
        }

        private IUIButton NegativeButton
        {
            get { return _settings != null ? _settings.NegativeButton : null; }
        }

        public bool HasPositiveAction
        {
            get { return IsButtonActive(PositiveButton); }
        }

        public bool HasNegativeAction
        {
            get { return IsButtonActive(NegativeButton); }
        }

        public bool IsPositiveActionEnabled
        {
            get { return MenuButtonAdapterBase.IsButtonEnabled(PositiveButton); }
        }

        public bool IsNegativeActionEnabled
        {
            get { return MenuButtonAdapterBase.IsButtonEnabled(NegativeButton); }
        }

        public bool HasInputField
        {
            get
            {
                UITextMeshInputField field = Field;
                return field != null && field.Active && field.Interactable;
            }
        }

        public IUITextMeshInputField InputField
        {
            get { return HasInputField ? Field : null; }
        }

        /// <summary>True: every <c>PopupMenu.Show</c> overload registers
        /// <c>InputActions.UI.ExitMenu</c> on <c>HandleNegativeButtonClicked</c> in its non-gamepad
        /// branch, so Escape already presses the negative button.</summary>
        public bool GameHandlesEscape
        {
            get { return true; }
        }

        public Component ButtonOf(DialogAction action)
        {
            switch (action)
            {
                case DialogAction.Positive:
                    return ComponentOf(PositiveButton);
                case DialogAction.Negative:
                    return ComponentOf(NegativeButton);
                default:
                    return null;
            }
        }

        public bool IsPresent()
        {
            IUITransform container = ContainerTransform;
            if (container == null)
            {
                return false;
            }

            return container.Active
                && (HasPositiveAction || HasNegativeAction);
        }

        public void AttachInputSubmit(Action<IUITextMeshInputField, string> handler)
        {
            UITextMeshInputField field = Field;
            if (field != null && handler != null)
            {
                field.OnSubmit = (Action<IUITextMeshInputField, string>)Delegate.Combine(field.OnSubmit, handler);
                _attachedSubmit = handler;
            }
        }

        public void DetachInputSubmit(Action<IUITextMeshInputField, string> handler)
        {
            UITextMeshInputField field = Field;
            if (field != null && handler != null)
            {
                field.OnSubmit = (Action<IUITextMeshInputField, string>)Delegate.Remove(field.OnSubmit, handler);
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

            Selectable selectable = null;
            switch (action)
            {
                case DialogAction.Positive:
                    selectable = GetSelectable(PositiveButton);
                    break;
                case DialogAction.Negative:
                    selectable = GetSelectable(NegativeButton);
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

        public bool ActivateAction(DialogAction action)
        {
            switch (action)
            {
                case DialogAction.Positive:
                    return NativeSelectionUtility.Click(PositiveButton);
                case DialogAction.Negative:
                    return NativeSelectionUtility.Click(NegativeButton);
                default:
                    return false;
            }
        }

        private static Component ComponentOf(IUIButton button)
        {
            return button == null ? null : button.MonoTransform;
        }

        /// <summary>The button's own active flag is the whole answer here, unlike the other popup
        /// adapters, which also ask whether the object is drawn: the game sets Active per button as it
        /// composes each popup (one-button popups switch the negative button off), and the popup's own
        /// container carries the shown/hidden state, which <see cref="IsPresent"/> reads.</summary>
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

        private static string GetButtonText(IUIButton button)
        {
            return UITextMeshTextUtility.GetEffectiveButtonText(button);
        }

        private static string GetActiveText(IUITextMesh textMesh)
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
