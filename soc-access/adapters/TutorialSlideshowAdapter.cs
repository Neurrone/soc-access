using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TutorialSlideshowAdapter
    {
        private static readonly AccessTools.FieldRef<TutorialMenu, GameObject> TopContainerRef =
            AccessTools.FieldRefAccess<TutorialMenu, GameObject>("_topContainer");
        private static readonly AccessTools.FieldRef<TutorialMenu, RectTransform> PanelRectRef =
            AccessTools.FieldRefAccess<TutorialMenu, RectTransform>("_panelRect");
        private static readonly AccessTools.FieldRef<TutorialMenu, UITextMesh> HeaderTextRef =
            AccessTools.FieldRefAccess<TutorialMenu, UITextMesh>("_tutorialEntryHeaderText");
        private static readonly AccessTools.FieldRef<TutorialMenu, UITextMesh> DescriptionTextRef =
            AccessTools.FieldRefAccess<TutorialMenu, UITextMesh>("_tutorialEntryDescriptionText");
        private static readonly AccessTools.FieldRef<TutorialMenu, UIButton> PageLeftButtonRef =
            AccessTools.FieldRefAccess<TutorialMenu, UIButton>("_pageLeftButton");
        private static readonly AccessTools.FieldRef<TutorialMenu, UIButton> PageRightButtonRef =
            AccessTools.FieldRefAccess<TutorialMenu, UIButton>("_pageRightButton");
        private static readonly AccessTools.FieldRef<TutorialMenu, UIToggle> TutorialsToggleRef =
            AccessTools.FieldRefAccess<TutorialMenu, UIToggle>("_tutorialsToggle");
        private static readonly AccessTools.FieldRef<TutorialMenu, UIButton> CloseButtonRef =
            AccessTools.FieldRefAccess<TutorialMenu, UIButton>("_closeButton");
        private static readonly AccessTools.FieldRef<TutorialMenu, TutorialSimplePopup> SimplePopupRef =
            AccessTools.FieldRefAccess<TutorialMenu, TutorialSimplePopup>("_simplePopup");

        private readonly TutorialMenu _menu;

        public TutorialSlideshowAdapter(TutorialMenu menu)
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
            TutorialSimplePopup simplePopup = SimplePopupRef(_menu);
            return IsActive(top)
                && panel != null
                && panel.gameObject != null
                && panel.gameObject.activeInHierarchy
                && (simplePopup == null || !simplePopup.gameObject.activeInHierarchy);
        }

        public string Header
        {
            get { return Normalize(UITextMeshTextUtility.GetEffectiveText(HeaderTextRef(_menu))); }
        }

        public string Description
        {
            get { return Normalize(UITextMeshTextUtility.GetEffectiveText(DescriptionTextRef(_menu))); }
        }

        public string TutorialsToggleLabel
        {
            get { return GetLocalizedText("Tutorial/TutorialPopup/ShowTutorialCheckbox", "Show tutorials"); }
        }

        public bool IsPreviousAvailable()
        {
            return IsButtonAvailable(PageLeftButtonRef(_menu));
        }

        public bool IsNextAvailable()
        {
            return IsButtonAvailable(PageRightButtonRef(_menu));
        }

        public bool IsCloseAvailable()
        {
            return IsButtonAvailable(CloseButtonRef(_menu));
        }

        public bool IsTutorialsChecked()
        {
            UIToggle toggle = TutorialsToggleRef(_menu);
            return toggle != null && toggle.ToggleValue;
        }

        public bool ActivatePrevious()
        {
            return InvokeButton(PageLeftButtonRef(_menu));
        }

        public bool ActivateNext()
        {
            return InvokeButton(PageRightButtonRef(_menu));
        }

        public bool ActivateClose()
        {
            return InvokeButton(CloseButtonRef(_menu));
        }

        public void ToggleTutorials()
        {
            UIToggle toggle = TutorialsToggleRef(_menu);
            if (toggle != null)
            {
                toggle.ToggleValue = !toggle.ToggleValue;
            }
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

        private static bool IsActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
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
