using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class TutorialSimpleAdapter
    {
        private static readonly AccessTools.FieldRef<TutorialMenu, GameObject> TopContainerRef =
            AccessTools.FieldRefAccess<TutorialMenu, GameObject>("_topContainer");
        private static readonly AccessTools.FieldRef<TutorialMenu, RectTransform> PanelRectRef =
            AccessTools.FieldRefAccess<TutorialMenu, RectTransform>("_panelRect");
        private static readonly AccessTools.FieldRef<TutorialMenu, TutorialSimplePopup> SimplePopupRef =
            AccessTools.FieldRefAccess<TutorialMenu, TutorialSimplePopup>("_simplePopup");
        private static readonly AccessTools.FieldRef<TutorialSimplePopup, UITextMesh> TitleTextRef =
            AccessTools.FieldRefAccess<TutorialSimplePopup, UITextMesh>("_titleText");
        private static readonly AccessTools.FieldRef<TutorialSimplePopup, UITextMesh> BodyTextRef =
            AccessTools.FieldRefAccess<TutorialSimplePopup, UITextMesh>("_text");
        private static readonly AccessTools.FieldRef<TutorialSimplePopup, UIButton> OkButtonRef =
            AccessTools.FieldRefAccess<TutorialSimplePopup, UIButton>("_okButton");
        private static readonly AccessTools.FieldRef<TutorialSimplePopup, UIToggle> ToggleRef =
            AccessTools.FieldRefAccess<TutorialSimplePopup, UIToggle>("_uiToggle");

        private readonly TutorialMenu _menu;

        public TutorialSimpleAdapter(TutorialMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public bool IsPresent()
        {
            if (_menu == null)
            {
                return false;
            }

            GameObject top = TopContainerRef(_menu);
            RectTransform panel = PanelRectRef(_menu);
            TutorialSimplePopup simplePopup = SimplePopup;
            return top != null
                && top.activeInHierarchy
                && simplePopup != null
                && simplePopup.gameObject.activeInHierarchy
                && (panel == null || !panel.gameObject.activeInHierarchy);
        }

        public string Header
        {
            get { return Normalize(UITextMeshTextUtility.GetEffectiveText(TitleTextRef(SimplePopup))); }
        }

        public string Description
        {
            get { return Normalize(UITextMeshTextUtility.GetEffectiveText(BodyTextRef(SimplePopup))); }
        }

        public string TutorialsToggleLabel
        {
            get { return GetLocalizedText("Tutorial/TutorialPopup/ShowTutorialCheckbox", "Show tutorials"); }
        }

        /// <summary>The component the game draws the OK button with, or null where the popup has
        /// none.</summary>
        public Component OkButton
        {
            get { return OkButtonRef(SimplePopup); }
        }

        /// <summary>The component the game draws the show-tutorials checkbox with, or null.</summary>
        public Component TutorialsToggle
        {
            get { return ToggleRef(SimplePopup); }
        }

        /// <summary>Whether the game is drawing the OK button at all.</summary>
        public bool IsOkVisible()
        {
            UIButton button = OkButtonRef(SimplePopup);
            return button != null && button.Active;
        }

        /// <summary>Whether the game is taking a press of the OK button.</summary>
        public bool IsOkEnabled()
        {
            UIButton button = OkButtonRef(SimplePopup);
            return button != null && button.Interactable;
        }

        public bool IsTutorialsChecked()
        {
            UIToggle toggle = ToggleRef(SimplePopup);
            return toggle != null && toggle.ToggleValue;
        }

        public bool ActivateOk()
        {
            return InvokeButton(OkButtonRef(SimplePopup));
        }

        public void ToggleTutorials()
        {
            UIToggle toggle = ToggleRef(SimplePopup);
            if (toggle != null)
            {
                toggle.ToggleValue = !toggle.ToggleValue;
            }
        }

        private TutorialSimplePopup SimplePopup
        {
            get { return _menu != null ? SimplePopupRef(_menu) : null; }
        }

        private static bool InvokeButton(UIButton button)
        {
            if (!IsButtonAvailable(button))
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static bool IsButtonAvailable(UIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }

        private static string Normalize(string value)
        {
            return SpeechTextSanitizer.Normalize(value);
        }

        private static string GetLocalizedText(string key, string fallback)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(key, fallback ?? string.Empty));
        }
    }
}
