using BepInEx;
using UnityEngine.SceneManagement;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SoqAccessPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "songs.of.conquest.access";
        public const string PluginName = "Songs of Conquest Access";
        public const string PluginVersion = "0.1.0";

        private SpeechService _speechService;
        private bool _announcedReady;

        private void Awake()
        {
            _speechService = new SpeechService(Logger);
            _speechService.Initialize();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            TryAnnounceReady();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _speechService?.Dispose();
            _speechService = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            TryAnnounceReady();
        }

        private void TryAnnounceReady()
        {
            if (_announcedReady || _speechService == null)
            {
                return;
            }

            string message = "Songs of Conquest Access v0.1 ready";
            _speechService.Speak(message, interrupt: true);
            _announcedReady = true;
            Logger.LogInfo(message);
        }
    }
}
