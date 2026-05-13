using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class LoadingScreenAdapter
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
