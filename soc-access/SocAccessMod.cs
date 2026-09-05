using BepInEx;
using System;
using System.Collections;
using BepInEx.Logging;
using SongsOfConquestAccess.Loader;
using HarmonyLib;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Dev;
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
    public sealed class SocAccessMod
    {
        public const string PluginGuid = "songs.of.conquest.access";
        public const string PluginName = "Songs of Conquest Access";

        public static SocAccessMod Instance { get; private set; }

        private readonly ModHost _host;
        private ManualLogSource Logger;

        public SocAccessMod(ModHost host)
        {
            _host = host;
        }

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            return _host.StartCoroutine(routine);
        }

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
        private ModRoutes _modRoutes;
        private bool _speechAvailable;
        private bool _announcedReady;
        private bool _reportedLocalizationUnavailable;

        public void Start()
        {
            Instance = this;
            Logger = BepInEx.Logging.Logger.CreateLogSource("SongsOfConquestAccess");
            Logger.LogInfo("Accessibility mod starting");
            ModSettings.Bind(_host.Config);
            _speechService = new SpeechService(Logger);
            _speechAvailable = _speechService.Initialize();
            Logger.LogInfo("Speech initialization result: " + _speechAvailable);
            SpeechPipeline.Initialize(_speechService, _host.MuteSpeech);
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
            // Before the ready line, so /speech carries it: the routes install the speech tap.
            _modRoutes = new ModRoutes(_host, _screenManager, _inputRouter, this);
            _modRoutes.Register();
            _harmony = new Harmony(PluginGuid + "." + Guid.NewGuid());
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
            AttachLocalizationHandler();
            TryAnnounceReady();
            _screenDetector?.ResyncFromRuntimeState();
            _host.SetUpdateHandler(Update);
        }

        public void Stop()
        {
            // First, so a mod being torn down stops answering for state that is going away.
            Step("dev routes", () => _modRoutes?.Unregister());
            _modRoutes = null;
            Step("update handler", () => _host.SetUpdateHandler(null));
            Step("routes", _host.UnregisterAllModRoutes);
            Step("coroutines", _host.StopAllCoroutines);
            Step("main menu waits", MainMenuPatches.Reset);
            Step("screens", () => _screenManager?.Clear());
            Step("beacon audio", AdventureBeaconAudio.DisposeAll);
            Step("synth audio", SynthCuePlayer.DisposeAll);
            Step("sweep audio", SweepPlayer.DisposeAll);
            Step("campaign notifiers", CampaignMenuLifetimeNotifier.DetachAll);
            Step("tale notifiers", Adapters.TaleSelectLifetimeNotifier.DetachAll);
            Step("Harmony", () => _harmony?.UnpatchSelf());
            _harmony = null;
            Step("input", () => _inputRouter?.Dispose());
            _inputRouter = null;
            Step("localization events", () =>
            {
                if (_localizationHandler != null)
                    _localizationHandler.OnLanguageChanged -= HandleLanguageChanged;
            });
            _localizationHandler = null;
            Step("translations", ModTranslationLoader.Reset);
            _screenDetector = null;
            _screenManager = null;
            Step("story camera", StoryCameraFocusPatches.ResetDedupe);
            Step("combat", CombatPatches.Reset);
            Step("chat", ChatPatches.Reset);
            Step("tooltips", TooltipPatches.Reset);
            Step("UI", UIManager.Reset);
            Step("buffer recorder", () => _bufferEventRecorder?.Detach());
            _bufferEventRecorder = null;
            Step("speech announcer", () => _speechEventAnnouncer?.Detach());
            _speechEventAnnouncer = null;
            Step("event bus", AccessibilityEventBus.Reset);
            _reviewBufferController = null;
            _reviewBufferManager = null;
            _adventureMapScannerState = null;
            Step("speech pipeline", SpeechPipeline.Shutdown);
            Step("speech service", () => _speechService?.Dispose());
            _speechService = null;
            Step("settings", ModSettings.Reset);
            if (Instance == this)
            {
                Instance = null;
            }
            Step("log source", () =>
            {
                if (Logger == null) return;
                BepInEx.Logging.Logger.Sources.Remove(Logger);
                Logger.Dispose();
            });
            Logger = null;
        }

        private void Step(string name, Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                _host.LogError("Mod stop: " + name + " failed: " + exception);
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

        /// <summary>Whether the speech backend came up. Reported by GET /status, where a silent
        /// run is otherwise indistinguishable from a mod that has nothing to say.</summary>
        public bool SpeechAvailable
        {
            get { return _speechAvailable; }
        }

        public ScreenDetector ScreenDetector
        {
            get { return _screenDetector; }
        }

        public AccessibilityInputRouter InputRouter
        {
            get { return _inputRouter; }
        }

        public ScreenManager ScreenManager
        {
            get { return _screenManager; }
        }

        public ReviewBufferManager ReviewBuffers
        {
            get { return _reviewBufferManager; }
        }

        public AdventureMapScannerState AdventureMapScannerState
        {
            get { return _adventureMapScannerState; }
        }

        public void LogInfo(string message)
        {
            Logger.LogInfo(message);
        }

        public void LogWarning(string message)
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

            string message = PluginName + " v" + ModEntry.ModVersion + " ready";
            SpeechPipeline.Output(new SpeechRequest(message, interrupt: true));
            _announcedReady = true;
            Logger.LogInfo(message);
        }

    }
}
