using BepInEx;
using HarmonyLib;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SocAccessPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "songs.of.conquest.access";
        public const string PluginName = "Songs of Conquest Access";
        public const string PluginVersion = "0.7.4";

        internal static SocAccessPlugin Instance { get; private set; }

        private Harmony _harmony;
        private SpeechService _speechService;
        private SpeechEventAnnouncer _speechEventAnnouncer;
        private BufferEventRecorder _bufferEventRecorder;
        private ReviewBufferManager _reviewBufferManager;
        private ReviewBufferController _reviewBufferController;
        private AdventureMapScannerState _adventureMapScannerState;
        private ScreenManager _screenManager;
        private ScreenDetector _screenDetector;
        private AccessibilityInputRouter _inputRouter;
        private ILocalizationHandler _localizationHandler;
        private bool _announcedReady;
        private bool _reportedLocalizationUnavailable;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("Accessibility plugin Awake");
            ModSettings.Bind(Config);
            _speechService = new SpeechService(Logger);
            bool speechInitialized = _speechService.Initialize();
            Logger.LogInfo("Speech initialization result: " + speechInitialized);
            SpeechPipeline.Initialize(_speechService);
            _reviewBufferManager = new ReviewBufferManager();
            _reviewBufferController = new ReviewBufferController(_reviewBufferManager);
            _adventureMapScannerState = new AdventureMapScannerState();
            _speechEventAnnouncer = new SpeechEventAnnouncer();
            _speechEventAnnouncer.Attach();
            _bufferEventRecorder = new BufferEventRecorder(_reviewBufferManager);
            _bufferEventRecorder.Attach();
            _screenManager = new ScreenManager(_reviewBufferManager, _reviewBufferController);
            _screenDetector = new ScreenDetector(_screenManager);
            _inputRouter = new AccessibilityInputRouter(_screenManager);
            _harmony = new Harmony(PluginGuid);
            try
            {
                Logger.LogInfo("Applying Harmony patches");
                _harmony.PatchAll(typeof(PopupMenuPatches).Assembly);
                Logger.LogInfo("Harmony patches applied");
            }
            catch (System.Exception exception)
            {
                Logger.LogError("Harmony patching failed: " + exception);
                throw;
            }
        }

        private void Start()
        {
            Logger.LogInfo("Accessibility plugin Start");
            AttachLocalizationHandler();
            TryAnnounceReady();
            _screenDetector?.ResyncFromRuntimeState();
        }

        private void OnDestroy()
        {
            Logger.LogInfo("Accessibility plugin OnDestroy");
            _screenManager?.Clear();
            AdventureBeaconAudio.DisposeAll();
            SynthCuePlayer.DisposeAll();
            _harmony?.UnpatchSelf();
            _harmony = null;
            _inputRouter?.Dispose();
            _inputRouter = null;
            if (_localizationHandler != null)
            {
                _localizationHandler.OnLanguageChanged -= HandleLanguageChanged;
                _localizationHandler = null;
            }
            ModTranslationLoader.Reset();
            _screenDetector = null;
            _screenManager = null;
            StoryCameraFocusPatches.ResetDedupe();
            CombatPatches.Reset();
            ChatPatches.Reset();
            TooltipPatches.Reset();
            UIManager.Reset();
            _bufferEventRecorder?.Detach();
            _bufferEventRecorder = null;
            _speechEventAnnouncer?.Detach();
            _speechEventAnnouncer = null;
            AccessibilityEventBus.Reset();
            _reviewBufferController = null;
            _reviewBufferManager = null;
            _adventureMapScannerState = null;
            SpeechPipeline.Shutdown();
            _speechService?.Dispose();
            _speechService = null;
            ModSettings.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            AttachLocalizationHandler();
            _screenDetector?.Update();
            _inputRouter?.Update();
            _screenManager?.Update();
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

        internal ReviewBufferManager ReviewBuffers
        {
            get { return _reviewBufferManager; }
        }

        internal AdventureMapScannerState AdventureMapScannerState
        {
            get { return _adventureMapScannerState; }
        }

        internal void LogInfo(string message)
        {
            Logger.LogInfo(message);
        }

        internal void LogWarning(string message)
        {
            Logger.LogWarning(message);
        }

        private void AttachLocalizationHandler()
        {
            ILocalizationHandler localizationHandler = GlobalLocalizationVariables.LocalizationHandler;
            if (ReferenceEquals(_localizationHandler, localizationHandler))
            {
                return;
            }

            if (_localizationHandler != null)
            {
                _localizationHandler.OnLanguageChanged -= HandleLanguageChanged;
                _localizationHandler = null;
            }

            if (localizationHandler == null)
            {
                if (!_reportedLocalizationUnavailable)
                {
                    Logger.LogWarning("Game localization handler is not available; using mod string fallbacks");
                    _reportedLocalizationUnavailable = true;
                }

                ModTranslationLoader.Reset();
                return;
            }

            _reportedLocalizationUnavailable = false;
            _localizationHandler = localizationHandler;
            ModTranslationLoader.LoadCurrentLanguage(_localizationHandler);
            _localizationHandler.OnLanguageChanged -= HandleLanguageChanged;
            _localizationHandler.OnLanguageChanged += HandleLanguageChanged;
        }

        private void HandleLanguageChanged()
        {
            ModTranslationLoader.LoadCurrentLanguage(_localizationHandler);
        }

        private void TryAnnounceReady()
        {
            if (_announcedReady || _speechService == null)
            {
                return;
            }

            string message = PluginName + " v" + PluginVersion + " ready";
            _speechService.Speak(message, interrupt: true);
            _announcedReady = true;
            Logger.LogInfo(message);
        }

    }
}
