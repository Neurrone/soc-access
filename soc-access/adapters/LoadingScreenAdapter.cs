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
