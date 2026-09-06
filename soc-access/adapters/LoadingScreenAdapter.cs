using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class LoadingScreenAdapter
    {
        private static readonly AccessTools.FieldRef<LoadingScreenMenu, LoadingScreenMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<LoadingScreenMenu, LoadingScreenMenu.Settings>("_settings");

        private static readonly AccessTools.FieldRef<LoadingBarVisuals, UITextMesh> LoadingBarTextRef =
            AccessTools.FieldRefAccess<LoadingBarVisuals, UITextMesh>("_loadingText");

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

                return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(promptText));
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
            get
            {
                UITextMesh tip = GetTipTextMesh();
                if (tip == null || !tip.Active || !((Component)tip).gameObject.activeInHierarchy)
                {
                    return string.Empty;
                }

                string text = tip.GetParsedText();
                return text != null ? text.Trim() : string.Empty;
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
            if (!IsPresent())
            {
                return false;
            }

            object sceneLoader = AccessTools.Field(typeof(LoadingScreenMenu), "_sceneLoader")?.GetValue(_menu);
            object state = sceneLoader != null ? Traverse.Create(sceneLoader).Property("State").GetValue() : null;
            if (state == null || state.ToString() != "WaitingForFinalization")
            {
                return false;
            }

            System.Reflection.MethodInfo finalize = AccessTools.Method(typeof(LoadingScreenMenu), "FinalizeLoadingScreen");
            if (finalize == null)
            {
                return false;
            }

            finalize.Invoke(_menu, null);
            return true;
        }

        public bool IsPresent()
        {
            if (_menu == null || !_menu.Active)
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
