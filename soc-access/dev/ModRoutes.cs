using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Loader;
using SongsOfConquestAccess.Loader.Dev;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

// Spelled out once: UnityEngine has a Screen of its own, and every use here is the mod's.
using Screen = SongsOfConquestAccess.Screens.Screen;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// The mod's half of the dev server: what it said, what state it is in, and the two ways to make
    /// it do something. Registered with the loader on Start and taken back down on Stop, so an
    /// unloaded or broken mod answers 404 here instead of reporting state that no longer exists.
    ///
    ///   GET  /status            mod version, speech backend, last spoken line, screen stack,
    ///                           focused widget and scene size
    ///   GET  /speech?since=N&amp;wait=MS
    ///                           lines spoken after sequence N, plus the next cursor; with wait, hold
    ///                           the connection open until there is one
    ///   GET  /gui/widgets?buffers=1&amp;flat=1
    ///                           the accessible tree of the top screen (see <see cref="WidgetDump"/>)
    ///   POST /input             body = an action key; run it as a keypress would
    ///   POST /key?hold=MS&amp;gap=MS&amp;text=1
    ///                           body = a key sequence (or, with text=1, characters); pressed as REAL
    ///                           OS key events at the game's window (see RawKeyboard)
    ///   POST /loadsave          body = a save name, or empty for the most recent save
    ///
    /// /speech reads the thread-safe buffer straight from the HTTP thread; /status and /gui/widgets
    /// touch the scene, so they go through the main-thread queue and answer 503 if the game is
    /// wedged. /input, /loadsave and /speech?wait block the HTTP thread and never the main one: the
    /// game has to keep running frames for any of them to be answered at all.
    /// </summary>
    internal sealed class ModRoutes
    {
        // Long enough for a frame that is doing real work (a screen rebuild, a window animating in),
        // short enough that a wedged game answers rather than hanging the caller.
        private const int InjectionTimeoutMilliseconds = 5000;

        private const int SettleMilliseconds = 400;
        private const int MaxSpeechWaitMilliseconds = 3000;
        private const int SpeechPollMilliseconds = 25;

        /// <summary>The ceiling on /speech?wait, so a caller cannot pin an HTTP thread indefinitely.
        /// </summary>
        private const int MaxWaitMilliseconds = 30000;

        // Longer than the two menu waits together, so a load that is merely slow is reported by the
        // coroutine rather than as a timeout with nothing said about why.
        private const int LoadTimeoutMilliseconds = 15000;
        private const float MenuWaitSeconds = 10f;
        private const float RefusalGraceSeconds = 2f;

        // How long the coroutine keeps watching for the "press any key to continue" screen after
        // the load button was pressed. A large map takes a couple of minutes to build here.
        private const float LoadingScreenWaitSeconds = 300f;

        /// <summary>MainMenu's own menu system - the field HandleLoadClicked calls ShowLoadGame on.
        /// </summary>
        private static readonly AccessTools.FieldRef<MainMenu, IMenuSystem> MainMenuSystemRef =
            AccessTools.FieldRefAccess<MainMenu, IMenuSystem>("_menuSystem");

        private readonly ModHost _host;
        private readonly ScreenManager _screens;
        private readonly AccessibilityInputRouter _input;
        private readonly SocAccessMod _mod;
        private readonly SpeechLog _speech = new SpeechLog();
        private LoadAttempt _load;

        public ModRoutes(
            ModHost host,
            ScreenManager screens,
            AccessibilityInputRouter input,
            SocAccessMod mod)
        {
            _host = host;
            _screens = screens;
            _input = input;
            _mod = mod;
        }

        public void Register()
        {
            SpeechPipeline.Observer = Spoken;
            // Each route names the query parameters it understands; the loader answers 400 for
            // anything else before the handler runs, so a mistyped parameter is never ignored.
            _host.RegisterRoute("GET", "/status", Status);
            _host.RegisterRoute("GET", "/speech", Speech, "since", "wait");
            _host.RegisterRoute("GET", "/gui/widgets", Widgets, "buffers", "flat");
            _host.RegisterRoute(
                "GET",
                "/gui/unity",
                request => UnityDump.Route(request, _host),
                "path",
                "depth",
                "visibleOnly",
                "fields");
            _host.RegisterRoute("POST", "/input", Input);
            _host.RegisterRoute("POST", "/key", Key, "hold", "gap", "text");
            _host.RegisterRoute("POST", "/loadsave", LoadSave);
        }

        /// <summary>The routes themselves are dropped by the loader; this releases the tap on the
        /// speech chokepoint, which is the mod's own static state, and lets go of anyone waiting on
        /// this buffer for a line that is never coming now.</summary>
        public void Unregister()
        {
            SpeechPipeline.Observer = null;
            _speech.Close();
        }

        // The mod's own buffer is what /speech serves; the loader's outlives a hot reload, which is
        // what lets POST /eval report the speech an evaluated call provoked.
        private void Spoken(string text)
        {
            _speech.Add(text);
            _host.NotifySpoken(text);
        }

        private DevResponse Status(DevRequest request)
        {
            return DevResponse.Json(
                (string)
                    _host.MainThread.Run(() =>
                    {
                        Widget focused = UIManager.CurrentWidget;
                        Screen top = _screens == null ? null : _screens.CurrentScreen;
                        int gameObjectCount = UnityEngine
                            .Object.FindObjectsOfType(typeof(GameObject))
                            .Length;
                        long cursor = _speech.Cursor;
                        long next;
                        List<SpeechLog.Entry> last = _speech.Since(cursor - 1, out next);
                        return DevJson.Write(json =>
                        {
                            json.WriteStartObject();
                            json.WritePropertyName("version");
                            json.WriteValue(SocAccessMod.PluginVersion);
                            // Which LOAD is answering. /loader/status reports the same name from the
                            // outside; read here it is the mod's own assembly saying so, which is
                            // what catches a reload that half-happened.
                            json.WritePropertyName("modAssemblyName");
                            json.WriteValue(typeof(ModEntry).Assembly.GetName().Name);
                            json.WritePropertyName("speechAvailable");
                            json.WriteValue(_mod != null && _mod.SpeechAvailable);
                            json.WritePropertyName("speechMuted");
                            json.WriteValue(SpeechPipeline.Muted);
                            json.WritePropertyName("lastSpoken");
                            json.WriteValue(last.Count > 0 ? last[last.Count - 1].Text : null);
                            json.WritePropertyName("screenStack");
                            json.WriteStartArray();
                            if (_screens != null)
                            {
                                IReadOnlyList<Screen> stack = _screens.Stack;
                                for (int i = 0; i < stack.Count; i++)
                                {
                                    json.WriteValue(stack[i].GetType().Name);
                                }
                            }

                            json.WriteEndArray();
                            json.WritePropertyName("topScreen");
                            json.WriteValue(top == null ? null : top.GetType().Name);
                            json.WritePropertyName("focusedWidgetId");
                            json.WriteValue(focused == null ? null : focused.Id);
                            json.WritePropertyName("focusedWidgetType");
                            json.WriteValue(focused == null ? null : focused.GetType().Name);
                            json.WritePropertyName("gameObjectCount");
                            json.WriteValue(gameObjectCount);
                            json.WriteEndObject();
                        });
                    })
            );
        }

        /// <summary>The accessible tree of the top screen, as text: every line of it is a sentence
        /// meant to be read. Side-effect free, so two calls answer identically.</summary>
        private DevResponse Widgets(DevRequest request)
        {
            bool buffers;
            bool flat;
            DevResponse badBuffers = Flag(request, "buffers", out buffers);
            DevResponse badFlat = Flag(request, "flat", out flat);
            DevResponse bad = badBuffers ?? badFlat;
            if (bad != null)
            {
                return bad;
            }

            return Plain(
                (string)_host.MainThread.Run(() => WidgetDump.Dump(_screens, buffers, flat))
            );
        }

        /// <summary>
        /// Run one of the mod's actions as though its key had been pressed, and report what became of
        /// it - who consumed it, or why nobody could - together with what it made the mod say.
        ///
        /// It goes through the PRODUCTION path: the same drain point in the frame, the same claim
        /// check, the same silence, the same dispatch. So a screen that answers over /eval and not
        /// over /input is a screen whose keys do not reach it, which is the bug /eval cannot see.
        /// </summary>
        private DevResponse Input(DevRequest request)
        {
            string key = (request.Body ?? string.Empty).Trim();
            InputAction action = AccessibilityActions.FindByKey(key);
            if (action == null)
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error(
                        "no action named '"
                            + key
                            + "'; the registered actions are: "
                            + AccessibilityActions.AllKeys()
                    )
                );
            }

            long spokenBefore = _speech.Cursor;
            AccessibilityInputRouter router = _input;
            if (router == null)
            {
                return DevResponse.Json(503, DevJson.Error("the mod's input router is not up"));
            }

            AccessibilityInputRouter.Injection injection =
                (AccessibilityInputRouter.Injection)_host.MainThread.Run(() => router.Inject(action));
            if (!injection.Done.WaitOne(InjectionTimeoutMilliseconds, false))
            {
                return DevResponse.Json(
                    503,
                    DevJson.Error(
                        "the game did not run '"
                            + key
                            + "' within "
                            + InjectionTimeoutMilliseconds
                            + " ms"
                    )
                );
            }

            List<SpeechLog.Entry> spoken = Settled(spokenBefore);
            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("ok");
                    json.WriteValue(true);
                    json.WritePropertyName("action");
                    json.WriteValue(key);
                    json.WritePropertyName("outcome");
                    json.WriteValue(injection.Outcome);
                    json.WritePropertyName("speech");
                    json.WriteStartArray();
                    foreach (SpeechLog.Entry entry in spoken)
                    {
                        json.WriteValue(entry.Text);
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                })
            );
        }

        /// <summary>
        /// Press keys the way a hand presses them - real OS key events at the game's window
        /// (<see cref="RawKeyboard"/>) - and report what the mod said about them.
        ///
        /// The one route that is NOT a shortcut into the mod: /input runs an action with no key
        /// physically down, and everything that branches on a key being down (the router's raw
        /// InputSystem subscription and its release debounce, the game's own reading of the same
        /// key) is invisible to it. This is how those are tested.
        ///
        /// Never on the main thread: a sequence holds keys down across frames, so the game has to keep
        /// running while it is sent.
        /// </summary>
        private DevResponse Key(DevRequest request)
        {
            int hold = request.QueryInt("hold", RawKeyboard.DefaultHoldMilliseconds);
            int gap = request.QueryInt("gap", RawKeyboard.DefaultGapMilliseconds);
            if (hold < 0 || gap < 0)
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("hold= and gap= are milliseconds, and cannot be negative")
                );
            }

            bool asText;
            DevResponse bad = Flag(request, "text", out asText);
            if (bad != null)
            {
                return bad;
            }

            string body = request.Body ?? string.Empty;
            long spokenBefore = _speech.Cursor;
            RawKeyboard.Result result = asText
                ? RawKeyboard.Type(body, gap)
                : RawKeyboard.Send(body, hold, gap);
            if (!result.Ok)
            {
                // 409 for "the game does not have the foreground" - the caller can fix that one and
                // ask again; 400 for a key name or a body the route cannot make sense of.
                return DevResponse.Json(result.Refused ? 409 : 400, DevJson.Error(result.Error));
            }

            List<SpeechLog.Entry> spoken = Settled(spokenBefore);
            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("ok");
                    json.WriteValue(true);
                    json.WritePropertyName("sent");
                    json.WriteStartArray();
                    foreach (string step in result.Sent)
                    {
                        json.WriteValue(step);
                    }

                    json.WriteEndArray();
                    json.WritePropertyName("speech");
                    json.WriteStartArray();
                    foreach (SpeechLog.Entry entry in spoken)
                    {
                        json.WriteValue(entry.Text);
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                })
            );
        }

        /// <summary>
        /// Boot straight into a saved game, so one command goes from a cold launch to in-game.
        ///
        /// It drives the game's OWN load, never the loader service behind it: the load menu is opened
        /// the way the game opens it, the save entry is clicked, the menu's asynchronous validation is
        /// waited out, and the native load button is pressed once it is interactable. A save the game
        /// would refuse therefore fails here too, which is the point - a direct call to
        /// <c>IGameLoader.Load</c> would load saves the player cannot.
        ///
        /// The answer comes back at the load button click; the coroutine then keeps watching, for up
        /// to <see cref="LoadingScreenWaitSeconds"/>, for the "press any key to continue" screen the
        /// game ends every load on, and presses it (<see cref="DevProbe.PressContinue"/>), so a
        /// launcher can go straight to waiting for <c>ingame</c>.
        ///
        /// A caller that arrives too early gets 503 and a "[not ready]" message rather than a failure,
        /// because "too early" is the normal case - the dev server answers while the game is still
        /// building its main menu - and the answer is to ask again in a second.
        /// </summary>
        private DevResponse LoadSave(DevRequest request)
        {
            string name = (request.Body ?? string.Empty).Trim();
            LoadAttempt running = _load;
            if (running != null && !running.Finished)
            {
                return DevResponse.Json(
                    409,
                    DevJson.Error("a load started by /loadsave is still in progress")
                );
            }

            object started = _host.MainThread.Run(() => BeginLoad(name));
            DevResponse refused = started as DevResponse;
            if (refused != null)
            {
                return refused;
            }

            LoadAttempt attempt = (LoadAttempt)started;
            if (!attempt.Done.WaitOne(LoadTimeoutMilliseconds, false))
            {
                return NotReady(
                    "the load menu did not answer within " + LoadTimeoutMilliseconds + " ms"
                );
            }

            if (attempt.Status != 200)
            {
                return DevResponse.Json(
                    attempt.Status,
                    attempt.Status == 503 ? DevJson.Error("[not ready] " + attempt.Error + "; retry")
                        : DevJson.Error(attempt.Error)
                );
            }

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("result");
                    json.WriteValue("loading '" + attempt.Loaded + "'");
                    json.WriteEndObject();
                })
            );
        }

        // Main thread: everything here is game state. Answers the 503/404 itself, or hands back the
        // attempt the coroutine will finish.
        private object BeginLoad(string name)
        {
            ScreenManager screens = _screens;
            if (screens == null || screens.CurrentScreen == null)
            {
                return NotReady("the mod has no screen yet");
            }

            IReadOnlyList<Screen> stack = screens.Stack;
            string topName = screens.CurrentScreen.GetType().Name;
            if (Present(stack, "LoadingCompleteScreen"))
            {
                return NotReady("a loading screen is up");
            }

            if (Present(stack, "CombatScreen"))
            {
                return NotReady("a battle is in progress");
            }

            if (topName == "MessageDialogScreen"
                || topName == "TooltipActionsMenuScreen"
                || topName.EndsWith("PopupScreen", StringComparison.Ordinal))
            {
                return NotReady("a dialog is open");
            }

            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i].GetType().Name.StartsWith("AdventureLobby", StringComparison.Ordinal))
                {
                    return NotReady("the lobby is open");
                }
            }

            // Already there (a previous call, or the player) - use the menu that is up.
            if (!Present(stack, "SaveLoadGameScreen"))
            {
                DevResponse refused = OpenLoadMenu(stack);
                if (refused != null)
                {
                    return refused;
                }
            }

            LoadAttempt attempt = new LoadAttempt { Name = name };
            _load = attempt;
            SocAccessMod.Instance?.StartCoroutine(DriveLoad(attempt));
            return attempt;
        }

        /// <summary>Open the load menu the way the game does - from the main menu, what
        /// <c>MainMenu.HandleLoadClicked</c> calls; in a session, what the pause menu's
        /// <c>OpenLoadGameMenu</c> response makes the menu system do. Null when it was opened.</summary>
        private static DevResponse OpenLoadMenu(IReadOnlyList<Screen> stack)
        {
            MainMenu mainMenu = UnityEngine.Object.FindObjectOfType<MainMenu>();
            if (mainMenu != null)
            {
                // The mod pushes MainMenuScreen only once the menu's button container is active, so
                // its presence is the proof that the menu can be clicked.
                if (!Present(stack, "MainMenuScreen"))
                {
                    return NotReady("the main menu is not interactable yet");
                }

                IMenuSystem fromMenu = MainMenuSystemRef != null ? MainMenuSystemRef(mainMenu) : null;
                if (fromMenu == null)
                {
                    return NotReady("the main menu has no menu system yet");
                }

                fromMenu.ShowLoadGame();
                return null;
            }

            if (!Present(stack, "AdventureMapScreen"))
            {
                return NotReady("neither the main menu nor a running session can start a load yet");
            }

            IMenuSystemManager manager = ProjectContext.Instance == null
                ? null
                : ProjectContext.Instance.Container.TryResolve<IMenuSystemManager>();
            IMenuSystem system = manager == null ? null : manager.Main;
            if (system == null)
            {
                return NotReady("the session's menu system is not up yet");
            }

            AdventureGameMode gameMode;
            if (!system.TryGetGameModeForCurrentGame(out gameMode))
            {
                return NotReady("the running game has no game mode yet");
            }

            system.ShowLoadGame(gameMode);
            return null;
        }

        /// <summary>Wait for the menu's entries, click the save, wait for the menu's own validation to
        /// enable the load button, and press it. Every wait is bounded, and every exit sets the event
        /// the HTTP thread is holding.</summary>
        private IEnumerator DriveLoad(LoadAttempt attempt)
        {
            SaveLoadGameMenuAdapter menu = null;
            IReadOnlyList<SaveLoadGameMenuAdapter.SaveEntry> entries = null;
            float deadline = Time.realtimeSinceStartup + MenuWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                menu = FindLoadMenu();
                if (menu != null)
                {
                    entries = menu.GetEntries();
                    if (entries.Count > 0)
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (menu == null)
            {
                attempt.Finish(503, "the load menu did not open");
                yield break;
            }

            if (entries == null || entries.Count == 0)
            {
                attempt.Finish(404, "the load menu listed no saves");
                yield break;
            }

            SaveLoadGameMenuAdapter.SaveEntry chosen = Choose(entries, attempt.Name);
            if (chosen == null)
            {
                attempt.Finish(
                    404,
                    attempt.Name.Length == 0
                        ? "there is no save that can be loaded"
                        : "no save named '" + attempt.Name + "'"
                );
                yield break;
            }

            string chosenName = chosen.SaveName;
            if (!chosen.Select())
            {
                attempt.Finish(503, "the save entry '" + chosenName + "' could not be clicked");
                yield break;
            }

            // SetupSelectedSave reads the save off disk before it enables the button; until it has,
            // pressing Load does nothing at all. A save it rejects (wrong version, content the
            // game does not have) leaves the button off for good, and the menu's details text says
            // why; the refusal is read only after a grace period because the menu's state field
            // starts out reading as rejected before the first validation has run.
            float started = Time.realtimeSinceStartup;
            deadline = started + MenuWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup - started > RefusalGraceSeconds
                    && !menu.LoadButton.IsEnabled()
                    && menu.IsSelectedSaveRefused())
                {
                    attempt.Finish(
                        422,
                        "the game refuses to load '" + chosenName + "': " + menu.GetDetailsText()
                    );
                    yield break;
                }

                if (menu.LoadButton.IsEnabled())
                {
                    if (!menu.LoadButton.Activate())
                    {
                        attempt.Finish(503, "the load button refused the click");
                        yield break;
                    }

                    attempt.Loaded = chosenName;
                    attempt.Finish(200, null);
                    break;
                }

                yield return null;
            }

            if (!attempt.Finished)
            {
                attempt.Finish(
                    503,
                    "the game did not accept '" + chosenName + "' as loadable within "
                        + MenuWaitSeconds + " s"
                );
                yield break;
            }

            // The HTTP caller has its answer; what follows is the key press the game waits for at
            // the end of the load, so the caller can wait for ingame instead of for loading.
            deadline = Time.realtimeSinceStartup + LoadingScreenWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (DevProbe.PressContinue())
                {
                    _host.LogInfo("/loadsave: continued past the loading-complete screen");
                    yield break;
                }

                yield return null;
            }

            _host.LogWarning(
                "/loadsave: the loading-complete screen did not appear within "
                    + LoadingScreenWaitSeconds + " s; the load may still be running"
            );
        }

        /// <summary>The save named, matched case-insensitively; otherwise the most recently written
        /// save the game does not read as corrupt.</summary>
        private static SaveLoadGameMenuAdapter.SaveEntry Choose(
            IReadOnlyList<SaveLoadGameMenuAdapter.SaveEntry> entries,
            string name
        )
        {
            SaveLoadGameMenuAdapter.SaveEntry newest = null;
            for (int i = 0; i < entries.Count; i++)
            {
                SaveLoadGameMenuAdapter.SaveEntry entry = entries[i];
                if (name.Length > 0)
                {
                    if (string.Compare(entry.SaveName, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return entry;
                    }

                    continue;
                }

                if (entry.IsCorrupt)
                {
                    continue;
                }

                if (newest == null || entry.LastWriteTime > newest.LastWriteTime)
                {
                    newest = entry;
                }
            }

            return newest;
        }

        // The mod's own screen for the menu already holds the instance the patches handed it, so the
        // route reads it from there rather than reaching into Zenject for a menu it cannot resolve.
        private SaveLoadGameMenuAdapter FindLoadMenu()
        {
            SaveLoadGameScreen screen = _screens == null ? null : _screens.Get<SaveLoadGameScreen>();
            SaveLoadGameMenu menu = screen == null ? null : screen.SourceKey as SaveLoadGameMenu;
            return menu == null ? null : new SaveLoadGameMenuAdapter(menu);
        }

        private static bool Present(IReadOnlyList<Screen> stack, string typeName)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static DevResponse NotReady(string why)
        {
            return DevResponse.Json(503, DevJson.Error("[not ready] " + why + "; retry"));
        }

        /// <summary>One /loadsave in flight: what it was asked for, what it did, and the event the
        /// waiting HTTP thread is released by.</summary>
        private sealed class LoadAttempt
        {
            public readonly System.Threading.ManualResetEvent Done =
                new System.Threading.ManualResetEvent(false);

            public string Name = string.Empty;
            public string Loaded;
            public string Error;
            public int Status;
            public volatile bool Finished;

            public void Finish(int status, string error)
            {
                Status = status;
                Error = error;
                Finished = true;
                Done.Set();
            }
        }

        /// <summary>
        /// What the mod has said since sequence N. With <c>wait=MS</c> the answer is held open until
        /// there is something newer, up to that many milliseconds - so a caller can ask "what does it
        /// say next" and be answered on the frame it is said, instead of polling and having to guess a
        /// sleep long enough not to miss it and short enough not to waste the test's time.
        ///
        /// The wait blocks this HTTP thread only. The main thread is what produces speech, so a route
        /// that waited there would be waiting for itself.
        /// </summary>
        private DevResponse Speech(DevRequest request)
        {
            long since = request.QueryLong("since", 0);
            int wait = request.QueryInt("wait", 0);
            if (wait > 0)
            {
                _speech.WaitForNewer(since, wait > MaxWaitMilliseconds ? MaxWaitMilliseconds : wait);
            }

            long next;
            List<SpeechLog.Entry> entries = _speech.Since(since, out next);

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("entries");
                    json.WriteStartArray();
                    foreach (SpeechLog.Entry entry in entries)
                    {
                        json.WriteStartObject();
                        json.WritePropertyName("seq");
                        json.WriteValue(entry.Seq);
                        json.WritePropertyName("text");
                        json.WriteValue(entry.Text);
                        json.WriteEndObject();
                    }

                    json.WriteEndArray();
                    json.WritePropertyName("next");
                    json.WriteValue(next);
                    json.WriteEndObject();
                })
            );
        }

        // The speech an action provoked usually lands a frame or two later, so the answer waits for
        // quiet the way POST /eval does (the same poll, on the same ring) - from this thread, never
        // the main one, since the game has to keep running frames for anything to be said at all.
        private List<SpeechLog.Entry> Settled(long since)
        {
            return _speech.Settled(
                since,
                SettleMilliseconds,
                SpeechPollMilliseconds,
                MaxSpeechWaitMilliseconds
            );
        }

        /// <summary>One off-by-default query flag, or the 400 that says the value made no sense.
        /// Null means it parsed.</summary>
        private static DevResponse Flag(DevRequest request, string name, out bool value)
        {
            string text = request.QueryValue(name);
            if (ParseFlag(text, false, out value))
            {
                return null;
            }

            return DevResponse.Json(
                400,
                DevJson.Error(name + "= expects 1/0 or true/false, not '" + text + "'")
            );
        }

        /// <summary>A query flag written either way callers write one: 1/0 or true/false. False for
        /// a value that is neither, so the route can say so rather than quietly using its default.
        /// </summary>
        internal static bool ParseFlag(string text, bool fallback, out bool value)
        {
            value = fallback;
            if (text == null)
            {
                return true;
            }

            if (text == "1" || string.Compare(text, "true", StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = true;
                return true;
            }

            if (
                text == "0"
                || string.Compare(text, "false", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                value = false;
                return true;
            }

            return false;
        }

        private static DevResponse Plain(string text)
        {
            return new DevResponse
            {
                ContentType = "text/plain; charset=utf-8",
                Body = Encoding.UTF8.GetBytes(text),
            };
        }
    }
}
