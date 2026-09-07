using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class TutorialSlideshowAdapter
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
        private static readonly AccessTools.FieldRef<TutorialMenu, int> CurrentPageRef =
            AccessTools.FieldRefAccess<TutorialMenu, int>("_currentPage");
        private static readonly AccessTools.FieldRef<TutorialMenu, ITutorialEntry> CurrentTutorialRef =
            AccessTools.FieldRefAccess<TutorialMenu, ITutorialEntry>("_currentTutorial");
        private static readonly MethodInfo UpdatePageMethod =
            AccessTools.Method(typeof(TutorialMenu), "UpdatePage");

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

        /// <summary>The text mesh the panel writes each page's description into - one viewer the game
        /// rewrites per page, and the only thing on screen that draws a page at all.</summary>
        public Component DescriptionBox
        {
            get { return _menu != null ? DescriptionTextRef(_menu) : null; }
        }

        /// <summary>The component the game draws the show-tutorials checkbox with, or null.</summary>
        public Component TutorialsToggle
        {
            get { return _menu != null ? TutorialsToggleRef(_menu) : null; }
        }

        /// <summary>The component the game draws the closing button with, or null.</summary>
        public Component CloseButton
        {
            get { return _menu != null ? CloseButtonRef(_menu) : null; }
        }

        /// <summary>What the game writes on the closing button ("Got it!").</summary>
        public string CloseLabel
        {
            get { return MenuButtonTextUtility.GetStandardButtonLabel(_menu != null ? CloseButtonRef(_menu) : null); }
        }

        /// <summary>How many pages the tutorial the panel is showing has.</summary>
        public int PageCount
        {
            get
            {
                ITutorialEntry tutorial = CurrentTutorial;
                return tutorial != null && tutorial.SlideShowPages != null ? tutorial.SlideShowPages.Count : 0;
            }
        }

        /// <summary>Which page the panel is showing, counted from zero.</summary>
        public int CurrentPage
        {
            get { return _menu != null ? CurrentPageRef(_menu) : 0; }
        }

        /// <summary>
        /// Turn the panel to <paramref name="page"/>. The menu offers no page-by-index call of its
        /// own - <c>HandleOnHorizontal</c> steps one page at a time off the arrow clicks - so this
        /// sets the page index the menu keeps and calls its own redraw, which is what the arrows do
        /// after they have moved the index (owner's choice 2026-09-07 over replaying arrow clicks).
        /// <c>UpdatePage</c> is also what records that the last page has been seen, so a turn to the
        /// end enables the closing button exactly as clicking through would.
        /// </summary>
        public bool ShowPage(int page)
        {
            if (_menu == null || UpdatePageMethod == null || page < 0 || page >= PageCount)
            {
                return false;
            }

            try
            {
                CurrentPageRef(_menu) = page;
                UpdatePageMethod.Invoke(_menu, null);
                return true;
            }
            catch (System.Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to turn the tutorial to page " + page + ": " + exception.Message);
                return false;
            }
        }

        public bool IsPreviousAvailable()
        {
            return IsButtonAvailable(PageLeftButtonRef(_menu));
        }

        public bool IsNextAvailable()
        {
            return IsButtonAvailable(PageRightButtonRef(_menu));
        }

        /// <summary>Whether the game is drawing the closing button at all.</summary>
        public bool IsCloseVisible()
        {
            UIButton button = _menu != null ? CloseButtonRef(_menu) : null;
            return button != null && button.Active;
        }

        /// <summary>Whether the game is taking a press of the closing button - it enables it once the
        /// last page has been shown.</summary>
        public bool IsCloseEnabled()
        {
            UIButton button = _menu != null ? CloseButtonRef(_menu) : null;
            return button != null && button.Interactable;
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

        private ITutorialEntry CurrentTutorial
        {
            get { return _menu != null ? CurrentTutorialRef(_menu) : null; }
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
