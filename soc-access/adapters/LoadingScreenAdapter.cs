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
    public sealed class LoadingScreenAdapter
    {
        private static readonly AccessTools.FieldRef<LoadingScreenMenu, LoadingScreenMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<LoadingScreenMenu, LoadingScreenMenu.Settings>("_settings");

        private static readonly AccessTools.FieldRef<LoadingBarVisuals, UITextMesh> LoadingBarTextRef =
            AccessTools.FieldRefAccess<LoadingBarVisuals, UITextMesh>("_loadingText");

        private static readonly AccessTools.FieldRef<LoadingScreenMenu, ISceneLoader> SceneLoaderRef =
            AccessTools.FieldRefAccess<LoadingScreenMenu, ISceneLoader>("_sceneLoader");

        private static readonly AccessTools.FieldRef<LoadingScreenMenu, bool> IsFinalizingRef =
            AccessTools.FieldRefAccess<LoadingScreenMenu, bool>("_isFinalizing");

        private static readonly MethodInfo FinalizeMethod =
            AccessTools.Method(typeof(LoadingScreenMenu), "FinalizeLoadingScreen");

        private readonly LoadingScreenMenu _menu;

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

        // What a key press does on the "press any key to continue" screen: LoadingScreenMenu.Tick
        // calls FinalizeLoadingScreen once any input is held while the scene loader is waiting
        // for finalization. Invoking the same method is the native path minus the key.
        public bool Continue()
        {
            if (!IsPresent() || FinalizeMethod == null)
            {
                return false;
            }

            FinalizeMethod.Invoke(_menu, null);
            return true;
        }

        /// <summary>The page as the game leaves it while it waits for a key, which is the only state
        /// this screen describes. The scene loader is at <c>WaitingForFinalization</c> and the menu
        /// has not begun dismissing itself, and the prompt the menu writes there
        /// ("Common/LoadingMenu/PressAnyKeyToContinue") is drawn - the multiplayer branch of
        /// <c>HandleWaitForFinalizationEntered</c> writes a waiting line instead and no prompt, so it
        /// reads as absent. Pressing continue sets <c>_isFinalizing</c> for the half-second fade
        /// before the scene unloads, which is what takes the page away here, as the deleted
        /// <c>FinalizeLoadingScreen</c> hook used to.</summary>
        public bool IsPresent()
        {
            if (_menu == null || !_menu.Active || !IsWaitingForKey())
            {
                return false;
            }

            GameObject gameObject = GetMenuGameObject();
            return gameObject != null
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded
                && gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(PromptText);
        }

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
            LoadingScreenMenu.Settings settings = GetSettings();
            if (settings == null || settings.MainLoadingBar == null)
            {
                return null;
            }

            return LoadingBarTextRef(settings.MainLoadingBar);
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
