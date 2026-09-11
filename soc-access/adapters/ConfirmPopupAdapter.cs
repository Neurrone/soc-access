using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class ConfirmPopupAdapter : IMessageDialogAdapter
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
            get { return UITextMeshTextUtility.Spoken(GetTitle()); }
        }

        public string Body
        {
            get { return _body.Joined(RawBody); }
        }

        /// <summary>The paragraphs the game broke the description into, kept apart rather than
        /// collapsed: the dialog reads a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get { return _body.Lines(RawBody); }
        }

        // The body, split at most once a frame: the guard, the node and the screen's own name all
        // ask for it (AGENTS.md, Performance).
        private readonly BodyText _body = new BodyText();

        private string RawBody
        {
            get { return UITextMeshTextUtility.GetEffectiveText(GetDescription()); }
        }

        public string PositiveLabel
        {
            get { return SpokenText.Get(GetLocalizationHandler(), "Common/Confirm", string.Empty); }
        }

        public string NegativeLabel
        {
            get { return SpokenText.Get(GetLocalizationHandler(), "Common/Cancel", string.Empty); }
        }

        public bool HasPositiveAction
        {
            get { return MenuButtonAdapterBase.IsButtonVisible(GetYesButton()); }
        }

        public bool HasNegativeAction
        {
            get { return MenuButtonAdapterBase.IsButtonVisible(GetNoButton()); }
        }

        public bool IsPositiveActionEnabled
        {
            get { return MenuButtonAdapterBase.IsButtonEnabledAndVisible(GetYesButton()); }
        }

        public bool IsNegativeActionEnabled
        {
            get { return MenuButtonAdapterBase.IsButtonEnabledAndVisible(GetNoButton()); }
        }

        /// <summary>True: <c>ConfirmPopup.Show</c> registers <c>InputActions.UI.ExitMenu</c> on
        /// <c>HandleNoClicked</c> whenever the input mode is KeyboardMouse, so Escape already presses
        /// No.</summary>
        public bool GameHandlesEscape
        {
            get { return true; }
        }

        public Component ButtonOf(DialogAction action)
        {
            switch (action)
            {
                case DialogAction.Positive:
                    return GetYesButton();
                case DialogAction.Negative:
                    return GetNoButton();
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
                    return NativeSelectionUtility.Click(GetYesButton());
                case DialogAction.Negative:
                    return NativeSelectionUtility.Click(GetNoButton());
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
    }
}
