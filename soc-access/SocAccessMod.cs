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

        /// <summary>Whether the loader is running the dev server, for the dev-only patches.</summary>
        public bool DevServerUp
        {
            get { return _host != null && _host.DevServerUp; }
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
        private GraphNavigator _navigator;
        private AccessibilityInputRouter _inputRouter;
        private ILocalizationHandler _localizationHandler;
        private ModRoutes _modRoutes;
        private bool _speechAvailable;
        private bool _announcedReady;
        private bool _reportedLocalizationUnavailable;
        // Mod-owned state that outlives every menu: whether the game was at the main menu on the
        // previous frame, so the adventure's buffers are cleared once on arrival there.
        private readonly Adapters.MainMenuArrival _mainMenuArrival = new Adapters.MainMenuArrival();

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
            GraphNavigator.InstallWiring();
            _navigator = new GraphNavigator();
            _screenManager = new ScreenManager(_navigator, _reviewBufferManager, _reviewBufferController);
            RegisterScreens(_screenManager);
            // One door for every drawn entry; the manager itself gains nothing.
            Adapters.ModOptionsEntries.Open = OpenModOptions;
            _inputRouter = new AccessibilityInputRouter(_screenManager);
            _navigator.TypedCharacters = _inputRouter.TakeTypedCharacters;
            // Warn, both ways, when a mod gesture and a game hotkey land on the same chord. Reads the
            // game's own input manager; a Stop step drops the subscription.
            ModKeybindConflicts.Start();
            // Before the ready line, so /speech carries it: the routes install the speech tap.
            _modRoutes = new ModRoutes(_host, _screenManager, _inputRouter, this);
            _modRoutes.Register();
            _harmony = new Harmony(PluginGuid + "." + Guid.NewGuid());
            try
            {
                Logger.LogInfo("Applying Harmony patches");
                _harmony.PatchAll(typeof(ChatPatches).Assembly);
                Logger.LogInfo("Harmony patches applied");
            }
            catch (System.Exception exception)
            {
                Logger.LogError("Harmony patching failed: " + exception);
                throw;
            }
            AttachLocalizationHandler();
            TryAnnounceReady();
            // The mod's drawn entries are put back before the first tick: a hot reload has just
            // destroyed the old load's, and a screen that lists them builds on the next frame.
            Adapters.ModOptionsEntries.Tick();
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
            Step("mod options entries", Adapters.ModOptionsEntries.Remove);
            Step("mod dialogs", UI.ModDialog.CloseAll);
            Step("screens", () => _screenManager?.Shutdown());
            Step("drop list", Screens.DropListScreen.Reset);
            Step("button text", Adapters.MenuButtonTextUtility.Reset);
            Step("graph navigator", () =>
            {
                _navigator?.Attach(null);
                GraphNavigator.ResetWiring();
            });
            _navigator = null;
            Step("pointer hover", UI.PointerHover.Release);
            Step("beacon audio", AdventureBeaconAudio.DisposeAll);
            Step("synth audio", SynthCuePlayer.DisposeAll);
            Step("sweep audio", SweepPlayer.DisposeAll);
            Step("Harmony", () => _harmony?.UnpatchSelf());
            _harmony = null;
            Step("input", () => _inputRouter?.Dispose());
            _inputRouter = null;
            Step("keybind conflicts", ModKeybindConflicts.Stop);
            Step("localization events", () =>
            {
                if (_localizationHandler != null)
                    _localizationHandler.OnLanguageChanged -= HandleLanguageChanged;
            });
            _localizationHandler = null;
            Step("translations", ModTranslationLoader.Reset);
            _screenManager = null;
            Step("story camera", StoryCameraFocusPatches.Reset);
            Step("combat", CombatPatches.Reset);
            Step("attack preview", Adapters.CombatAdapter.Reset);
            Step("chat", Screens.ChatSource.Reset);
            Step("tooltips", TooltipPatches.Reset);
            Step("community maps keys", CommunityMapsFiveDigitInputDuplicateKeyPatches.Reset);
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
            // Logged before it runs: a quit that stops responding leaves the step it stalled in as
            // the last line of the log (2026-09-07, a hang on quit with no stack to take).
            _host.LogInfo("Mod stop: " + name);
            try { action(); }
            catch (Exception exception)
            {
                _host.LogError("Mod stop: " + name + " failed: " + exception);
            }
        }

        /// <summary>
        /// EVERY SCREEN THE MOD KNOWS ABOUT, registered once and polled from here on
        /// (<see cref="ScreenManager"/>). Order matters only within a layer: a screen registered
        /// later covers one registered earlier on the same number, which is how combat sits above the
        /// adventure map. The layers themselves are in <c>screens/README.md</c>.
        /// </summary>
        private static void RegisterScreens(ScreenManager screens)
        {
            screens.Register(new MainMenuScreen());
            screens.Register(new CampaignMenuScreen());
            screens.Register(new TaleSelectScreen());
            screens.Register(new CustomCampaignSelectScreen());
            screens.Register(new CampaignMapSelectScreen());
            screens.Register(new OnlineGameListScreen());
            screens.Register(new OnlineHostGameScreen());
            screens.Register(new CommunityMapsHomeScreen());
            screens.Register(new CommunityMapsCollectionScreen());
            screens.Register(new CommunityMapsDetailsScreen());
            screens.Register(new CommunityMapsSearchResultsScreen());
            screens.Register(new CommunityMapsSearchFilterScreen());
            screens.Register(new CommunityMapsModalScreen());
            screens.Register(new AdventureLobbyMapTypeScreen());
            screens.Register(new AdventureLobbyMapSelectScreen());
            screens.Register(new AdventureLobbyChallengeMapSelectScreen());
            screens.Register(new AdventureLobbyRandomLayoutScreen());
            screens.Register(new AdventureLobbyPlayersScreen());
            screens.Register(new AdventureLobbyGameSettingsScreen());
            screens.Register(new AdventureLobbyPlayerSettingsScreen());
            screens.Register(new AdventureLobbyInviteProvidersScreen());
            screens.Register(new AdventureLobbyIconDropdownScreen());
            screens.Register(new PlatformUserMenuScreen());

            screens.Register(new AdventureMapScreen());
            // After the map, so the battlefield covers it on the layer they share.
            screens.Register(new CombatScreen());
            screens.Register(new PreBattleMenuScreen());

            screens.Register(new MapEntityMiniMenuScreen());
            screens.Register(new AdventurePlayerMenuScreen());
            screens.Register(new OwnedEntitiesScreen());
            screens.Register(new TroopOverviewScreen());
            screens.Register(new MarketplaceScreen());
            screens.Register(new ArtifactMarketScreen());
            screens.Register(new TradingScreen());
            screens.Register(new SettlementScreen());
            screens.Register(new DefenceMenuScreen());
            screens.Register(new GiftTownPopupScreen());
            screens.Register(new SendResourcePopupScreen());
            screens.Register(new DraftTroopsScreen());
            screens.Register(new UpgradeTroopsScreen());
            screens.Register(new RallyPointScreen());
            screens.Register(new BuildMenuScreen());
            screens.Register(new ResearchScreen());
            screens.Register(new PurchaseWielderScreen());
            screens.Register(new CommanderSheetScreen());
            screens.Register(new SpellbookScreen());
            screens.Register(new PostBattleResultScreen());
            screens.Register(new ClaimMenuScreen());
            screens.Register(new WorldChoiceMenuScreen());
            screens.Register(new WorldConfirmMenuScreen());
            screens.Register(new HostileJoinMenuScreen());
            screens.Register(new LevelUpScreen());
            screens.Register(new PlayerStatsScreen());
            screens.Register(new PostAdventureResultScreen());
            screens.Register(new PostAdventureStatsScreen());
            screens.Register(new MoveTroopPopupScreen());
            screens.Register(new TutorialSlideshowScreen());
            screens.Register(new TutorialSimpleScreen());
            screens.Register(new ChatScreen());

            screens.Register(new PauseMenuScreen());
            screens.Register(new OptionsScreen());
            screens.Register(new SaveLoadGameScreen());
            screens.Register(new CodexScreen());

            screens.Register(new MessageDialogScreen());
            screens.Register(new BugReportScreen());
            screens.Register(new QuitToDesktopPopupScreen());
            screens.Register(new StoryTextScreen());
            screens.Register(new LoadingCompleteScreen());

            // Children: pushed by the page that opens them rather than polled, and registered so the
            // dev server can name them and so their state survives the mod's load.
            screens.Register(new ModOptionsScreen());
            screens.Register(new DropListScreen());
        }

        /// <summary>Open the mod's own options. The drawn entries of
        /// <see cref="Adapters.ModOptionsEntries"/> come here, so one place decides what "mod
        /// options" means.</summary>
        public bool OpenModOptions()
        {
            return ModOptionsScreen.Open();
        }

        private void Update()
        {
            AttachLocalizationHandler();
            Adapters.ModOptionsEntries.Tick();
            ClearAdventureStateOnMainMenuArrival();
            // Who the player is on, then the keys: every screen resolves its own menu from the game
            // inside the tick, so a page the game has just put up is focused before any key reaches
            // it.
            _screenManager?.Tick();
            _inputRouter?.Update();
        }

        /// <summary>The adventure map's notification review buffer and the scanner's state describe
        /// a game that is over once the main menu is up, and nothing in the game clears them. The
        /// arrival is read from the game's own scene loader rather than from a hook.</summary>
        private void ClearAdventureStateOnMainMenuArrival()
        {
            if (!_mainMenuArrival.Arrived())
            {
                return;
            }

            _reviewBufferManager?.Clear(ReviewBufferKind.AdventureMapNotifications);
            _adventureMapScannerState?.Clear();
        }

        /// <summary>Whether the speech backend came up. Reported by GET /status, where a silent
        /// run is otherwise indistinguishable from a mod that has nothing to say.</summary>
        public bool SpeechAvailable
        {
            get { return _speechAvailable; }
        }

        public AccessibilityInputRouter InputRouter
        {
            get { return _inputRouter; }
        }

        public ScreenManager ScreenManager
        {
            get { return _screenManager; }
        }

        /// <summary>The one navigator every graph screen is driven by (screens/GraphScreen.cs).</summary>
        public GraphNavigator Navigator
        {
            get { return _navigator; }
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
