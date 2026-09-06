using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class MapMessagePopupAdapter : IMessageDialogAdapter
    {
        private static readonly AccessTools.FieldRef<MapMessagePopup, UITransform> ContainerRef =
            AccessTools.FieldRefAccess<MapMessagePopup, UITransform>("_container");
        private static readonly AccessTools.FieldRef<MapMessagePopup, UITextMesh> TextRef =
            AccessTools.FieldRefAccess<MapMessagePopup, UITextMesh>("_text");
        private static readonly AccessTools.FieldRef<MapMessagePopup, UITextMesh> TitleTextRef =
            AccessTools.FieldRefAccess<MapMessagePopup, UITextMesh>("_titleText");
        private static readonly AccessTools.FieldRef<MapMessagePopup, UIButton> OkButtonRef =
            AccessTools.FieldRefAccess<MapMessagePopup, UIButton>("_okButton");

        private readonly MapMessagePopup _popup;

        public MapMessagePopupAdapter(MapMessagePopup popup)
        {
            _popup = popup;
        }

        public object SourceKey
        {
            get { return _popup; }
        }

        public string Title
        {
            get { return GetText(GetTitleText()); }
        }

        public string Body
        {
            get { return GetText(GetBodyText()); }
        }

        public string PositiveLabel
        {
            get { return FirstNonEmpty(GetButtonText(GetOkButton()), "OK"); }
        }

        public string NegativeLabel
        {
            get { return string.Empty; }
        }

        public bool HasPositiveAction
        {
            get { return IsButtonActive(GetOkButton()); }
        }

        public bool HasNegativeAction
        {
            get { return false; }
        }

        public bool IsPositiveActionEnabled
        {
            get { return IsButtonEnabled(GetOkButton()); }
        }

        public bool IsNegativeActionEnabled
        {
            get { return false; }
        }

        /// <summary>True: <c>MapMessagePopup.Show</c> registers <c>InputActions.UI.ExitMenu</c> on
        /// <c>Hide</c> unconditionally, whatever the input mode.</summary>
        public bool GameHandlesEscape
        {
            get { return true; }
        }

        public Component ButtonOf(DialogAction action)
        {
            return action == DialogAction.Positive ? GetOkButton() : null;
        }

        public bool IsPresent()
        {
            if (_popup == null)
            {
                return false;
            }

            GameObject gameObject = _popup.gameObject;
            UITransform container = GetContainer();
            return gameObject != null
                && gameObject.activeInHierarchy
                && container != null
                && container.Active
                && HasPositiveAction;
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

            if (action != DialogAction.Positive)
            {
                return;
            }

            UIButton button = GetOkButton();
            Selectable selectable = button != null ? button.GetSelectable() : null;
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        public bool ActivateAction(DialogAction action)
        {
            return action == DialogAction.Positive && InvokeButton(GetOkButton());
        }

        private UITransform GetContainer()
        {
            return _popup != null ? ContainerRef(_popup) : null;
        }

        private UITextMesh GetTitleText()
        {
            return _popup != null ? TitleTextRef(_popup) : null;
        }

        private UITextMesh GetBodyText()
        {
            return _popup != null ? TextRef(_popup) : null;
        }

        private UIButton GetOkButton()
        {
            return _popup != null ? OkButtonRef(_popup) : null;
        }

        private static bool IsButtonActive(UIButton button)
        {
            return button != null && button.Active && MenuButtonAdapterBase.IsButtonVisible(button);
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return IsButtonActive(button) && button.Interactable;
        }

        private static bool InvokeButton(UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(IUIButton button)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private static string FirstNonEmpty(string first, string fallback)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : fallback;
        }
    }
}
