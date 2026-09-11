using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class LoadingScreenAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<LoadingScreenMenu, LoadingScreenMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<LoadingScreenMenu, LoadingScreenMenu.Settings>("_settings");

        private static readonly AccessTools.FieldRef<LoadingBarVisuals, UITextMesh> LoadingBarTextRef =
            AccessTools.FieldRefAccess<LoadingBarVisuals, UITextMesh>("_loadingText");

        private static readonly AccessTools.FieldRef<LoadingScreenMenu, ISceneLoader> SceneLoaderRef =
            AccessTools.FieldRefAccess<LoadingScreenMenu, ISceneLoader>("_sceneLoader");

        private static readonly AccessTools.FieldRef<LoadingScreenMenu, bool> IsFinalizingRef =
            AccessTools.FieldRefAccess<LoadingScreenMenu, bool>("_isFinalizing");

        private static readonly AccessTools.FieldRef<LoadingBarVisuals, float> ProgressRef =
            AccessTools.FieldRefAccess<LoadingBarVisuals, float>("_progress");

        private static readonly MethodInfo FinalizeMethod =
            AccessTools.Method(typeof(LoadingScreenMenu), "FinalizeLoadingScreen");

        private readonly LoadingScreenMenu _menu;

        // The two latches the page's whole life is read through, both derived from a per-frame read
        // and never from a hook (AGENTS.md, Screen Resolution). This adapter lives exactly as long as
        // the one menu instance a load puts up, so neither latch needs a reset.
        private bool _shown;
        private bool _dismissed;

        public LoadingScreenAdapter(LoadingScreenMenu menu)
        {
            _menu = menu;
        }

        public LoadingScreenMenu Source
        {
            get { return _menu; }
        }

        public string PromptText
        {
            get
            {
                UITextMesh promptText = GetPromptTextMesh();
                if (promptText == null || !promptText.Active || !((Component)promptText).gameObject.activeInHierarchy)
                {
                    return string.Empty;
                }

                return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(promptText));
            }
        }

        /// <summary>The tip the page draws above the prompt ("Most troop buildings of Yulan can be
        /// upgraded into..."). The menu picks one at random into <c>_settings.TipText</c> and switches
        /// <c>_settings.TipContainer</c> off for a loading screen definition that shows no tips
        /// (<c>LoadingScreenMenu.SetupTip</c>), so an empty answer means the page is drawing none.
        ///
        /// Read as the LAID-OUT text rather than as the string the menu assigned: a tip carries the
        /// game's own action tokens ("Hold &lt;action name=ToggleHexTargetingMode&gt; to target the
        /// ground"), which <c>UITextMesh.UpdateText</c> rewrites into the key the action is bound to as
        /// it draws. <c>GetParsedText</c> is that finished line, so the player hears "Hold Ctrl" the way
        /// a sighted player reads it.
        /// </summary>
        public string TipText
        {
            get { return string.Join(" ", TipLines); }
        }

        /// <summary>The tip's own lines, kept apart rather than run together: the menu writes some
        /// tips in more than one.</summary>
        public IList<string> TipLines
        {
            get
            {
                UITextMesh tip = GetTipTextMesh();
                if (tip == null || !tip.Active || !((Component)tip).gameObject.activeInHierarchy)
                {
                    return new List<string>();
                }

                return SpokenLines.Of(new[] { tip.GetParsedText() });
            }
        }

        /// <summary>The game has WRITTEN a tip, whether or not TextMeshPro has laid it out yet.
        ///
        /// The two answers differ for exactly one frame. <c>SetupTip</c> assigns the localized tip into
        /// <c>UITextMesh.Text</c>, which lands in the mesh's own string builder at once, while
        /// <see cref="TipLines"/> reads <c>GetParsedText</c>, which TextMeshPro fills when it lays the
        /// text out at the end of that frame. A caller that must not treat "not laid out yet" as "no
        /// tip" asks this first.</summary>
        public bool HasTip
        {
            get
            {
                UITextMesh tip = GetTipTextMesh();
                if (tip == null || !tip.Active || !((Component)tip).gameObject.activeInHierarchy)
                {
                    return false;
                }

                return !string.IsNullOrWhiteSpace(UITextMeshTextUtility.GetStringBuilderText(tip));
            }
        }

        /// <summary>The label the tip is drawn on, for a caller that needs something the game paints to
        /// key its own reading of the page on.</summary>
        public Component TipLabel
        {
            get { return GetTipTextMesh() as Component; }
        }

        /// <summary>The label the "press any key to continue" prompt is drawn on.</summary>
        public Component PromptLabel
        {
            get { return GetPromptTextMesh() as Component; }
        }

        /// <summary>How far the whole load has come, 0 to 1. Read off <c>LoadingBarVisuals._progress</c>
        /// rather than off the mask's fill, because <c>UpdateProgress</c> stops writing the fill once
        /// the value reaches 1. One value spans the load: the async phase writes 0 to 0.75
        /// (<c>HandleAsyncOperationProgress</c>), the scene load 0.75 to 1 (<c>LoadingScreenMenu.Tick</c>),
        /// and entering the wait for a key writes 1.</summary>
        public float Progress
        {
            get
            {
                LoadingBarVisuals bar = GetLoadingBar();
                return bar != null && ProgressRef != null ? ProgressRef(bar) : 0f;
            }
        }

        /// <summary>Whether the page draws a loading bar at all: a loading screen definition can switch
        /// <c>_settings.LoadingbarContainer</c> off (<c>ShowLoadingBar</c>).</summary>
        public bool HasProgressBar
        {
            get
            {
                LoadingScreenMenu.Settings settings = GetSettings();
                GameObject container = settings != null ? settings.LoadingbarContainer : null;
                return container != null && container.activeInHierarchy;
            }
        }

        /// <summary>The bar the progress is drawn on, for a caller that needs something the game paints
        /// to key its own reading of the page on.</summary>
        public Component ProgressLabel
        {
            get { return GetLoadingBar(); }
        }

        /// <summary>The status line above the bar: the scene loader's own messages, "Loading scene", or
        /// a progress payload's text. Empty once the menu clears it, which is what entering the wait
        /// for a key does.
        ///
        /// Read as what the game WROTE and never as the mesh's own text: <c>UITextMesh.Text</c> assigns
        /// through <c>TMP_Text.SetText</c>, which leaves <c>m_text</c> untouched, so the prefab's
        /// placeholder "LOADING" is still what the mesh answers with after the menu has cleared the
        /// line - and the page draws nothing there (screenshot, 2026-09-10).</summary>
        public string StatusText
        {
            get { return string.Join(" ", StatusLines); }
        }

        /// <summary>The status line's own lines, kept apart the way the tip's are.</summary>
        public IList<string> StatusLines
        {
            get
            {
                LoadingScreenMenu.Settings settings = GetSettings();
                UITextMesh status = settings != null ? settings.LoadingText : null;
                if (status == null || !status.Active || !((Component)status).gameObject.activeInHierarchy)
                {
                    return new List<string>();
                }

                return SpokenLines.Of(new[] { UITextMeshTextUtility.GetStringBuilderText(status) });
            }
        }

        /// <summary>The label the status line is drawn on.</summary>
        public Component StatusLabel
        {
            get
            {
                LoadingScreenMenu.Settings settings = GetSettings();
                return settings != null ? settings.LoadingText as Component : null;
            }
        }

        // What a key press does on the "press any key to continue" screen: LoadingScreenMenu.Tick
        // calls FinalizeLoadingScreen once any input is held while the scene loader is waiting
        // for finalization. Invoking the same method is the native path minus the key.
        //
        // Guarded on the WAIT and not on presence: the page is present for the whole load now, and
        // finalizing mid-load would fade the canvas out while the load still ran.
        public bool Continue()
        {
            if (!IsWaitingForKey() || FinalizeMethod == null)
            {
                return false;
            }

            FinalizeMethod.Invoke(_menu, null);
            return true;
        }

        /// <summary>The page for the WHOLE load, from the moment the game has finished fading it in to
        /// the moment it begins fading it out.
        ///
        /// Arrival is the end state of the menu's own fade - <c>SetLoadingScreenDefinition</c> puts the
        /// canvas alpha at 0 and tweens it to 1 over 0.3 s - latched, because a progress payload
        /// carrying another loading screen definition re-runs that fade mid-load (a save load does),
        /// and the latch is what keeps the page from dropping out from under the cursor when it does.
        ///
        /// Departure is <c>_isFinalizing</c>, latched for the opposite reason:
        /// <c>FinalizeLoadingScreen</c> sets it for the half-second fade to 0 and its completion
        /// callback clears it again just before the scene unloads, so only a latch tells "not begun"
        /// from "just finished".
        ///
        /// The scene loader having a state at all (anything but <c>None</c>) is what says a load is
        /// running; the rest is the same drawn check the page has always used.</summary>
        public bool IsPresent()
        {
            if (_menu == null || _dismissed)
            {
                return false;
            }

            if (IsFinalizingRef(_menu))
            {
                _dismissed = true;
                return false;
            }

            if (!_shown && IsFadedIn())
            {
                _shown = true;
            }

            if (!_shown || !_menu.Active)
            {
                return false;
            }

            ISceneLoader sceneLoader = SceneLoaderRef(_menu);
            if (sceneLoader == null || sceneLoader.State == SceneLoaderState.None)
            {
                return false;
            }

            GameObject gameObject = GetMenuGameObject();
            return GameObjects.IsLiveSceneObject(gameObject) && GameObjects.IsLive(gameObject);
        }

        /// <summary>The arrival fade has finished: the canvas group the menu tweens is opaque.</summary>
        private bool IsFadedIn()
        {
            LoadingScreenMenu.Settings settings = GetSettings();
            CanvasGroup canvasGroup = settings != null ? settings.CanvasGroup : null;
            return canvasGroup == null || canvasGroup.alpha >= 0.99f;
        }

        /// <summary>The one state a key press means anything in, and so the only one
        /// <see cref="Continue"/> may act on: the scene loader is at <c>WaitingForFinalization</c> and
        /// the menu has not begun dismissing itself.</summary>
        private bool IsWaitingForKey()
        {
            if (_menu == null || IsFinalizingRef(_menu))
            {
                return false;
            }

            ISceneLoader sceneLoader = SceneLoaderRef(_menu);
            return sceneLoader != null && sceneLoader.State == SceneLoaderState.WaitingForFinalization;
        }

        private UITextMesh GetPromptTextMesh()
        {
            LoadingBarVisuals bar = GetLoadingBar();
            return bar != null ? LoadingBarTextRef(bar) : null;
        }

        private LoadingBarVisuals GetLoadingBar()
        {
            LoadingScreenMenu.Settings settings = GetSettings();
            return settings != null ? settings.MainLoadingBar : null;
        }

        private UITextMesh GetTipTextMesh()
        {
            LoadingScreenMenu.Settings settings = GetSettings();
            return settings != null ? settings.TipText : null;
        }

        private LoadingScreenMenu.Settings GetSettings()
        {
            return _menu != null ? SettingsRef(_menu) : null;
        }

        private GameObject GetMenuGameObject()
        {
            LoadingScreenMenu.Settings settings = GetSettings();
            if (settings == null || settings.MenuTransform == null)
            {
                return null;
            }

            return ((Component)settings.MenuTransform).gameObject;
        }
    }
}
