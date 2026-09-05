using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>
    /// In-process dev driver, on unless SOCACCESS_NO_DEV=1. It exists so a developer or an AI
    /// agent who can neither see the screen nor hear the screen reader can still observe and
    /// steer the game: dump the live UI hierarchy, grab the frame the game is rendering, run C#
    /// against the running process, swap in a freshly built mod, and shut the game down. Bound to
    /// 127.0.0.1 only, so it is reachable from this machine alone.
    ///
    /// The loader owns these routes, so they keep answering when the mod is broken or unloaded:
    ///
    ///   GET  /gui/game?path=&amp;depth=    live Unity hierarchy as JSON (see GuiDump)
    ///   GET  /screenshot                the rendered frame as image/png
    ///   GET  /log?since=&amp;grep=         everything BepInEx logged, cursor-polled (no since = tail)
    ///   GET  /loader/status             loader version, whether the mod is up, reload history
    ///   POST /reload                    rebuild-and-swap the mod assembly on the next frame
    ///   POST /eval?settle=&amp;speech=     run the C# in the body, and report what it made the mod say
    ///   POST /wait?timeout=            block until the boolean expression in the body holds
    ///   POST /quit                      exit the game
    ///
    /// Everything else comes from the route registry the mod fills in through
    /// <see cref="ModHost"/> (/status, /speech). Those answer 404 while the mod is down, which is
    /// the honest answer: there is nothing to report.
    ///
    /// Every route, builtin or mod-registered, declares the query parameters it understands; a
    /// request naming any other is answered 400 (see <see cref="DevRoute"/>) rather than served an
    /// answer that quietly ignored what was asked for.
    ///
    /// Requests arrive on the HTTP thread; anything that touches Unity is queued onto the main
    /// thread and waited for (503 when the game does not get to it). Not shipped to players.
    /// </summary>
    internal sealed class DevServer
    {
        public const string DisableEnv = "SOCACCESS_NO_DEV";
        public const string PortEnv = "SOCACCESS_DEV_PORT";

        private const int DefaultPort = 8772;
        private const int ScreenshotTimeoutMilliseconds = 5000;

        private const int DefaultWaitMilliseconds = 5000;
        private const int MaxWaitMilliseconds = 60000;

        // A wait ends itself on the frame its deadline passes; this is only the backstop for a
        // game that has stopped producing frames at all, so the HTTP thread is never stuck.
        private const int WaitBackstopMilliseconds = 2000;

        private const int DefaultSettleMilliseconds = 700;
        private const int MaxSettleMilliseconds = 3000;
        private const int MaxSpeechWaitMilliseconds = 3000;
        private const int SpeechPollMilliseconds = 25;

        private const int SpokenCapacity = 200;
        private const int LogCapacity = 2000;

        // What GET /log answers when no cursor was given: the tail, not the whole ring.
        private const int DefaultLogEntries = 100;

        // Long enough for the response to reach the client before the process goes away.
        private const float QuitDelaySeconds = 0.25f;

        private readonly LoaderPlugin _plugin;
        private readonly MainThreadQueue _mainThread = new MainThreadQueue();
        private readonly PredicateWaits _waits = new PredicateWaits();
        private readonly SeqLog _spoken = new SeqLog(SpokenCapacity);
        private readonly SeqLog _log = new SeqLog(LogCapacity);
        private readonly object _routeLock = new object();
        private readonly Dictionary<string, DevRoute> _modRoutes =
            new Dictionary<string, DevRoute>();
        private readonly Dictionary<string, DevRoute> _builtins;

        private DevHttpServer _http;
        private BepInExLogTap _logTap;
        private CSharpEvaluator _evaluator;

        public DevServer(LoaderPlugin plugin)
        {
            _plugin = plugin;

            // The routes the loader owns, each with the query parameters it understands: anything
            // else is a 400 rather than a silent no-op (see DevRoute).
            _builtins = new Dictionary<string, DevRoute>
            {
                { Key("GET", "/gui/game"), new DevRoute(Gui, "path", "depth") },
                { Key("GET", "/screenshot"), new DevRoute(request => Screenshot()) },
                { Key("GET", "/log"), new DevRoute(Log, "since", "grep") },
                { Key("GET", "/loader/status"), new DevRoute(request => LoaderStatus()) },
                { Key("POST", "/reload"), new DevRoute(request => Reload()) },
                { Key("POST", "/eval"), new DevRoute(Eval, "settle", "speech") },
                { Key("POST", "/wait"), new DevRoute(Wait, "timeout") },
                { Key("POST", "/quit"), new DevRoute(request => Quit()) },
            };
        }

        /// <summary>The mod lifecycle the /loader/status and /reload routes drive. Set once, by
        /// the plugin, before <see cref="Start"/>.</summary>
        public ModLoader Mods;

        public MainThreadQueue MainThread
        {
            get { return _mainThread; }
        }

        /// <summary>Bring up the HTTP front end. The queue and the registry work either way, so
        /// leaving the server off only takes away the remote control, not the mod. Off unless the
        /// devServer config setting opts in; SOCACCESS_NO_DEV=1 forces it off regardless.</summary>
        public void Start(bool enabled)
        {
            if (!enabled)
            {
                LoaderLog.Info("Dev server disabled (devServer = false in config)");
                return;
            }

            if (Environment.GetEnvironmentVariable(DisableEnv) == "1")
            {
                LoaderLog.Info("Dev server disabled (" + DisableEnv + "=1)");
                return;
            }

            // An unattended test run drives the game from another process, so the window never
            // has focus; without this Unity would stop simulating and every wait would time out.
            Application.runInBackground = true;

            _logTap = new BepInExLogTap(_log);
            BepInEx.Logging.Logger.Listeners.Add(_logTap);

            int port = DefaultPort;
            string configuredPort = Environment.GetEnvironmentVariable(PortEnv);
            if (!string.IsNullOrEmpty(configuredPort))
            {
                int.TryParse(configuredPort, out port);
            }

            try
            {
                _http = new DevHttpServer(port, Handle);
                _http.Start();
                LoaderLog.Info("Dev server listening on " + _http.Address);
            }
            catch (Exception e)
            {
                LoaderLog.Error("Dev server failed to start: " + e);
                _http = null;
            }
        }

        /// <summary>Whether the listener is up, which is the one place the dev server's config
        /// setting and its environment override have both already been resolved to a yes or a no.
        /// </summary>
        public bool Listening
        {
            get { return _http != null; }
        }

        /// <summary>Run the work HTTP requests queued for the main thread, then ask every
        /// outstanding /wait whether it is done. Call once per frame.</summary>
        public void Tick()
        {
            _mainThread.Drain();
            _waits.Tick();
        }

        public void Stop()
        {
            if (_http != null)
            {
                _http.Stop();
                _http = null;
            }

            // Nothing new can arrive now, but a request already inside is parked on work only a
            // FRAME retires - a main-thread job, or a predicate - and no frame is coming. Both are
            // ended here, or shutdown sits out every one of their timeouts.
            _mainThread.Drain();
            _waits.AbandonAll("the dev server stopped before this wait was satisfied");

            if (_logTap != null)
            {
                BepInEx.Logging.Logger.Listeners.Remove(_logTap);
                _logTap.Dispose();
                _logTap = null;
            }
        }

        /// <summary>Record a line the mod spoke, so POST /eval can report what it provoked. Kept
        /// here rather than in the mod because it has to outlive a hot reload.</summary>
        public void NotifySpoken(string text)
        {
            _spoken.Add(text);
        }

        public void RegisterModRoute(
            string method,
            string path,
            DevRouteHandler handler,
            string[] allowedQueryParameters
        )
        {
            lock (_routeLock)
            {
                _modRoutes[Key(method, path)] = new DevRoute(handler, allowedQueryParameters);
            }
        }

        public void UnregisterModRoutes()
        {
            _waits.AbandonAll("the mod was unloaded while this wait was pending");
            GuiDump.ClearReflectionCache();
            lock (_routeLock)
            {
                _modRoutes.Clear();
            }
        }

        /// <summary>
        /// Point the REPL at the mod assembly that is now current, throwing away the evaluator
        /// that was bound to the previous one.
        ///
        /// Every load takes a fresh identity (SongsOfConquest.Access-r1, -r2, ...) and Mono cannot unload, so
        /// after a reload the process holds several assemblies that all declare
        /// SongsOfConquestAccess.ModEntry. Mono.CSharp's importer caches the namespaces and types it has taken
        /// from the assemblies it references, and the first registration of a name wins: merely
        /// referencing the new assembly leaves ModEntry bound to the *oldest* copy, so eval keeps
        /// driving code that stopped running several builds ago, silently and without an error.
        /// Only an evaluator that has never seen the older copies binds the name correctly.
        ///
        /// The price is the REPL session - variables and usings declared before a reload are gone,
        /// the same way the speech ring resets. Rebuilding here rather than on the next request
        /// keeps Mono.CSharp's start-up cost out of the /eval main-thread job, which would
        /// otherwise be liable to blow its timeout and answer 503.
        ///
        /// Main thread, like every other use of the evaluator: the mod lifecycle runs there too,
        /// so no request can be inside the evaluator while it is being swapped. No-op until
        /// someone has actually used /eval - there is nothing bound yet to be wrong.
        /// </summary>
        public void RebindModAssembly(Assembly assembly)
        {
            if (_evaluator == null)
            {
                return;
            }

            try
            {
                _evaluator = NewEvaluator(assembly);
            }
            catch (Exception e)
            {
                // Better no evaluator than one bound to the assembly that just went away; the
                // next /eval builds one against whatever is loaded by then.
                _evaluator = null;
                LoaderLog.Warn("eval: could not rebuild the REPL for the new mod: " + e.Message);
            }
        }

        // Runs on an HTTP pool thread, one per request and possibly several at once.
        private DevResponse Handle(DevRequest request)
        {
            try
            {
                string key = Key(request.Method, request.Path);
                DevRoute route;
                if (!_builtins.TryGetValue(key, out route))
                {
                    lock (_routeLock)
                    {
                        _modRoutes.TryGetValue(key, out route);
                    }
                }

                if (route == null)
                {
                    return DevResponse.Json(
                        404,
                        DevJson.Error("no route for " + request.Method + " " + request.Path)
                    );
                }

                // One chokepoint for every route, the mod's included: a parameter the route does
                // not declare is answered, never dropped.
                DevResponse rejected = route.Reject(request);
                return rejected ?? route.Handler(request);
            }
            catch (MainThreadTimeoutException e)
            {
                return DevResponse.Json(503, DevJson.Error(e.Message));
            }
            catch (Exception e)
            {
                return DevResponse.Json(500, DevJson.Error(e.Message));
            }
        }

        private DevResponse LoaderStatus()
        {
            DateTime? loaded = Mods.ModFileWrittenUtc;
            DateTime? onDisk = Mods.ModFileOnDiskWrittenUtc;
            bool stale = loaded.HasValue && onDisk.HasValue && onDisk.Value > loaded.Value;

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("loaderVersion");
                    json.WriteValue(LoaderPlugin.PluginVersion);
                    json.WritePropertyName("modLoaded");
                    json.WriteValue(Mods.ModLoaded);
                    // Renamed per load, so a changed value here is proof the swap reached Mono.
                    json.WritePropertyName("modAssemblyName");
                    json.WriteValue(Mods.ModAssemblyName);
                    json.WritePropertyName("reloadCount");
                    json.WriteValue(Mods.ReloadCount);
                    json.WritePropertyName("failedReloadCount");
                    json.WriteValue(Mods.FailedReloadCount);
                    json.WritePropertyName("lastReloadError");
                    json.WriteValue(Mods.LastReloadError);
                    json.WritePropertyName("modFileWrittenUtc");
                    json.WriteValue(Iso(loaded));
                    json.WritePropertyName("modFileOnDiskWrittenUtc");
                    json.WriteValue(Iso(onDisk));
                    json.WritePropertyName("staleBuild");
                    json.WriteValue(stale);
                    json.WriteEndObject();
                })
            );
        }

        private static string Iso(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : null;
        }

        // Answers before the swap so the client is not holding a socket open across a reload that
        // may itself throw; the outcome shows up in /loader/status and the game log.
        private DevResponse Reload()
        {
            _mainThread.Post(() => Mods.Reload());
            return DevResponse.Json(DevJson.Ok());
        }

        /// <summary>
        /// Run C# against the game and report both what it returned and what it made the mod say.
        /// Most of what evaluated code is worth doing here is provoking an announcement, and the
        /// speech it provokes usually lands a frame or two later, so by default the answer is held
        /// until speech has gone quiet for a settle window. ?speech=0 drops that wait, and the
        /// speech field with it, when the caller only wants the return value.
        ///
        /// The session - variables, usings, anything a request declared - lasts as long as the
        /// mod load it was made against. A hot reload starts a fresh one, so that mod type names
        /// resolve to the build now running; see <see cref="RebindModAssembly"/>.
        /// </summary>
        private DevResponse Eval(DevRequest request)
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("POST /eval expects C# source as the request body")
                );
            }

            bool wantSpeech = request.QueryInt("speech", 1) != 0;
            int settle = Clamp(
                request.QueryInt("settle", DefaultSettleMilliseconds),
                0,
                MaxSettleMilliseconds
            );
            long spokenBefore = _spoken.Cursor;

            CSharpEvaluator.Result result = (CSharpEvaluator.Result)
                _mainThread.Run(() => Evaluate(request.Body));

            // Settling polls the ring from this thread, never the main one: the game has to keep
            // running frames for speech to arrive at all.
            List<SeqLog.Entry> spoken = wantSpeech
                ? _spoken.Settled(
                    spokenBefore,
                    settle,
                    SpeechPollMilliseconds,
                    MaxSpeechWaitMilliseconds
                )
                : null;

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("ok");
                    json.WriteValue(result.Ok);
                    json.WritePropertyName("result");
                    json.WriteValue(result.Value);
                    json.WritePropertyName("error");
                    json.WriteValue(result.Error);
                    if (spoken != null)
                    {
                        json.WritePropertyName("speech");
                        json.WriteStartArray();
                        foreach (SeqLog.Entry entry in spoken)
                        {
                            json.WriteValue(entry.Text);
                        }

                        json.WriteEndArray();
                    }

                    json.WriteEndObject();
                })
            );
        }

        // Main thread: the point of the REPL is reaching game state, which is only legal here.
        private CSharpEvaluator.Result Evaluate(string source)
        {
            try
            {
                return Evaluator().Evaluate(source);
            }
            catch (Exception e)
            {
                return CSharpEvaluator.Result.Failed(e.ToString());
            }
        }

        /// <summary>
        /// Block until the boolean expression in the body is true, checking it on every frame
        /// rather than on every poll. A condition that holds for a single frame - a transition
        /// announced and then replaced, a panel that opens and closes - is invisible to a caller
        /// sampling from outside the process, and this is how it is caught.
        /// </summary>
        private DevResponse Wait(DevRequest request)
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("POST /wait expects a C# boolean expression as the request body")
                );
            }

            int timeout = Clamp(
                request.QueryInt("timeout", DefaultWaitMilliseconds),
                0,
                MaxWaitMilliseconds
            );

            object watched = _mainThread.Run(() => Watch(request.Body, timeout));

            string compileError = watched as string;
            if (compileError != null)
            {
                return DevResponse.Json(WaitJson(false, 0, 0, compileError));
            }

            PredicateWait wait = (PredicateWait)watched;
            if (!wait.Done.WaitOne(timeout + WaitBackstopMilliseconds, false))
            {
                _waits.Remove(wait);
                wait.Abandon("the game stopped producing frames while the wait was pending");
            }

            return DevResponse.Json(
                WaitJson(wait.Satisfied, wait.Frames, wait.ElapsedMilliseconds, wait.Error)
            );
        }

        // Main thread: compiling shares the REPL session, so the expression can name whatever
        // earlier /eval requests declared. Returns the wait, or the compile error as a string.
        private object Watch(string expression, int timeoutMilliseconds)
        {
            CompiledPredicate predicate;
            try
            {
                predicate = Evaluator().CompilePredicate(expression);
            }
            catch (Exception e)
            {
                return e.ToString();
            }

            if (predicate.Error != null)
            {
                return predicate.Error;
            }

            PredicateWait wait = new PredicateWait(predicate, timeoutMilliseconds);
            _waits.Add(wait);
            return wait;
        }

        private static string WaitJson(
            bool satisfied,
            int frames,
            int elapsedMilliseconds,
            string error
        )
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("ok");
                json.WriteValue(error == null);
                json.WritePropertyName("satisfied");
                json.WriteValue(satisfied);
                json.WritePropertyName("frames");
                json.WriteValue(frames);
                json.WritePropertyName("elapsedMs");
                json.WriteValue(elapsedMilliseconds);
                json.WritePropertyName("error");
                json.WriteValue(error);
                json.WriteEndObject();
            });
        }

        /// <summary>
        /// The log, cursor-polled. A caller that passes <c>since</c> is following the log and gets
        /// exactly what is newer; a caller that passes none is opening it for the first time and
        /// wants the end of it, not two thousand lines of boot - so that answer is capped at the
        /// newest <see cref="DefaultLogEntries"/> and says so. <c>grep</c> is applied to the whole
        /// ring first, so a search still reaches back through the history the cap hides, and
        /// <c>next</c> is the ring's own cursor either way: polling with it resumes correctly
        /// whatever the answer dropped.
        /// </summary>
        private DevResponse Log(DevRequest request)
        {
            long next;
            List<SeqLog.Entry> entries = SeqLog.Matching(
                _log.Since(request.QueryLong("since", 0), out next),
                request.QueryValue("grep")
            );

            bool capped =
                request.QueryValue("since") == null && entries.Count > DefaultLogEntries;
            if (capped)
            {
                entries = entries.GetRange(
                    entries.Count - DefaultLogEntries,
                    DefaultLogEntries
                );
            }

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("capped");
                    json.WriteValue(capped);
                    json.WritePropertyName("entries");
                    json.WriteStartArray();
                    foreach (SeqLog.Entry entry in entries)
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

        private CSharpEvaluator Evaluator()
        {
            if (_evaluator == null)
            {
                _evaluator = NewEvaluator(Mods.ModAssembly);
            }

            return _evaluator;
        }

        // The mod assembly is passed in rather than read from the loader: a rebind happens while
        // the load it belongs to is still going up, before the loader has adopted it.
        private static CSharpEvaluator NewEvaluator(Assembly modAssembly)
        {
            CSharpEvaluator evaluator = new CSharpEvaluator();
            if (modAssembly != null)
            {
                evaluator.Reference(modAssembly);
            }

            return evaluator;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private DevResponse Gui(DevRequest request)
        {
            string path = request.QueryValue("path");
            int depth = request.QueryInt("depth", GuiDump.DefaultDepth);
            return DevResponse.Json((string)_mainThread.Run(() => GuiDump.Dump(path, depth)));
        }

        private DevResponse Screenshot()
        {
            FrameCapture capture = new FrameCapture();
            _mainThread.Run(() =>
            {
                _plugin.StartCoroutine(capture.Run());
                return null;
            });

            if (!capture.Done.WaitOne(ScreenshotTimeoutMilliseconds, false))
            {
                return DevResponse.Json(
                    503,
                    DevJson.Error(
                        "the game did not render a frame within "
                            + ScreenshotTimeoutMilliseconds
                            + " ms"
                    )
                );
            }

            if (capture.Failure != null)
            {
                return DevResponse.Json(500, DevJson.Error(capture.Failure));
            }

            return DevResponse.Png(capture.Png);
        }

        private DevResponse Quit()
        {
            _mainThread.Post(() => _plugin.StartCoroutine(QuitAfterAnswering()));
            return DevResponse.Json(DevJson.Ok());
        }

        private static IEnumerator QuitAfterAnswering()
        {
            yield return new WaitForSeconds(QuitDelaySeconds);
            Application.Quit();
        }

        private static string Key(string method, string path)
        {
            return method + " " + path;
        }

        // Reads the framebuffer, which is only legal once the frame has finished rendering, so it
        // has to run as a coroutine; the requesting HTTP thread waits on Done for the PNG.
        private sealed class FrameCapture
        {
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public byte[] Png;
            public string Failure;

            public IEnumerator Run()
            {
                yield return new WaitForEndOfFrame();

                Texture2D frame = null;
                try
                {
                    frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    frame.Apply();
                    Png = frame.EncodeToPNG();
                }
                catch (Exception e)
                {
                    Failure = e.Message;
                }
                finally
                {
                    if (frame != null)
                    {
                        UnityEngine.Object.Destroy(frame);
                    }

                    Done.Set();
                }
            }
        }
    }
}
