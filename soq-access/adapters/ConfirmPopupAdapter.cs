using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class ConfirmPopupAdapter : IQuestionDialogAdapter
    {
        private static readonly AccessTools.FieldRef<ConfirmPopup, UITextMesh> TitleRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UITextMesh>("_title");
        private static readonly AccessTools.FieldRef<ConfirmPopup, UITextMesh> DescriptionRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UITextMesh>("_description");
        private static readonly AccessTools.FieldRef<ConfirmPopup, UIButton> YesButtonRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UIButton>("_yesButton");
        private static readonly AccessTools.FieldRef<ConfirmPopup, UIButton> NoButtonRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UIButton>("_noButton");
        private static readonly AccessTools.FieldRef<ConfirmPopup, UITransform> ButtonContainerRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UITransform>("_buttonContainer");
        private static readonly AccessTools.FieldRef<ConfirmPopup, UITransform> MainContainerRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UITransform>("_mainContainer");
        private static readonly AccessTools.FieldRef<ConfirmPopup, ILocalizationHandler> LocalizationHandlerRef =
            AccessTools.FieldRefAccess<ConfirmPopup, ILocalizationHandler>("_localizationHandler");

        private readonly ConfirmPopup _popup;

        public ConfirmPopupAdapter(ConfirmPopup popup)
        {
            _popup = popup;
        }

        public object SourceKey
        {
            get { return _popup; }
        }

        public string Title
        {
            get { return GetText(GetTitle()); }
        }

        public string Body
        {
            get { return GetText(GetDescription()); }
        }

        public string PositiveLabel
        {
            get { return GetLocalizedText("Common/Confirm"); }
        }

        public string NegativeLabel
        {
            get { return GetLocalizedText("Common/Cancel"); }
        }

        public bool HasPositiveAction
        {
            get { return IsButtonActive(GetYesButton()); }
        }

        public bool HasNegativeAction
        {
            get { return IsButtonActive(GetNoButton()); }
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

            UITransform mainContainer = GetMainContainer();
            UITransform buttonContainer = GetButtonContainer();
            return mainContainer != null
                && mainContainer.Active
                && buttonContainer != null
                && buttonContainer.Active
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

            UIButton button = action == DialogAction.Positive ? GetYesButton() : GetNoButton();
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
                    return InvokeButton(GetYesButton());
                case DialogAction.Negative:
                    return InvokeButton(GetNoButton());
                default:
                    return false;
            }
        }

        private UITextMesh GetTitle()
        {
            return _popup != null ? TitleRef(_popup) : null;
        }

        private UITextMesh GetDescription()
        {
            return _popup != null ? DescriptionRef(_popup) : null;
        }

        private UIButton GetYesButton()
        {
            return _popup != null ? YesButtonRef(_popup) : null;
        }

        private UIButton GetNoButton()
        {
            return _popup != null ? NoButtonRef(_popup) : null;
        }

        private UITransform GetButtonContainer()
        {
            return _popup != null ? ButtonContainerRef(_popup) : null;
        }

        private UITransform GetMainContainer()
        {
            return _popup != null ? MainContainerRef(_popup) : null;
        }

        private ILocalizationHandler GetLocalizationHandler()
        {
            return _popup != null ? LocalizationHandlerRef(_popup) : null;
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

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private string GetLocalizedText(string key)
        {
            ILocalizationHandler localizationHandler = GetLocalizationHandler();
            return localizationHandler != null
                ? SpeechTextSanitizer.Normalize(localizationHandler.GetText(key))
                : string.Empty;
        }
    }
}
