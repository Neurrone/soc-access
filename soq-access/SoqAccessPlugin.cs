using BepInEx;
using HarmonyLib;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SoqAccessPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "songs.of.conquest.access";
        public const string PluginName = "Songs of Conquest Access";
        public const string PluginVersion = "0.1.0";

        internal static SoqAccessPlugin Instance { get; private set; }

        private Harmony _harmony;
        private SpeechService _speechService;
        private ScreenManager _screenManager;
        private ScreenDetector _screenDetector;
        private AccessibilityInputRouter _inputRouter;
        private bool _announcedReady;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("Accessibility plugin Awake");
            _speechService = new SpeechService(Logger);
            bool speechInitialized = _speechService.Initialize();
            Logger.LogInfo("Speech initialization result: " + speechInitialized);
            SpeechPipeline.Initialize(_speechService);
            _screenManager = new ScreenManager();
            _screenDetector = new ScreenDetector(_screenManager);
            _inputRouter = new AccessibilityInputRouter(_screenManager);
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PopupMenuPatches).Assembly);
            Logger.LogInfo("Harmony patches applied");
        }

        private void Start()
        {
            Logger.LogInfo("Accessibility plugin Start");
            TryAnnounceReady();
            _screenDetector?.ResyncFromRuntimeState();
        }

        private void OnDestroy()
        {
            Logger.LogInfo("Accessibility plugin OnDestroy");
            _harmony?.UnpatchSelf();
            _harmony = null;
            _inputRouter = null;
            _screenDetector = null;
            _screenManager = null;
            UIManager.Reset();
            SpeechPipeline.Shutdown();
            _speechService?.Dispose();
            _speechService = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            UIManager.Update();
        }

        internal ScreenDetector ScreenDetector
        {
            get { return _screenDetector; }
        }

        internal AccessibilityInputRouter InputRouter
        {
            get { return _inputRouter; }
        }

        internal ScreenManager ScreenManager
        {
            get { return _screenManager; }
        }

        internal void LogInfo(string message)
        {
            Logger.LogInfo(message);
        }

        internal void LogWarning(string message)
        {
            Logger.LogWarning(message);
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
