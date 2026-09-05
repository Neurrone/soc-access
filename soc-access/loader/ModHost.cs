using System;
using System.Collections;
using BepInEx.Configuration;
using SongsOfConquestAccess.Loader.Dev;
using UnityEngine;

namespace SongsOfConquestAccess.Loader
{
    /// <summary>
    /// Everything the reloadable mod is allowed to reach in the loader, and the only thing it is
    /// handed at startup. The mod is loaded from bytes and dropped again on every reload, so it
    /// cannot own a BepInEx plugin, a MonoBehaviour, an HTTP listener or a file location of its
    /// own: it borrows all four from here.
    ///
    /// Everything the mod attaches through this object - routes, the per-frame handler,
    /// coroutines - the loader can also detach on its own, so a mod that fails halfway through
    /// Start or throws in Stop still leaves a clean loader behind.
    /// </summary>
    public sealed class ModHost
    {
        private readonly LoaderPlugin _plugin;
        private readonly DevServer _dev;

        internal ModHost(LoaderPlugin plugin, DevServer dev, string pluginDirectory)
        {
            _plugin = plugin;
            _dev = dev;
            PluginDirectory = pluginDirectory;
        }

        public ConfigFile Config
        {
            get { return _plugin.Config; }
        }

        /// <summary>Whether this launch was asked to keep the screen reader silent (the one-shot
        /// muteSpeech setting, already cleared in the file). Survives hot reloads.</summary>
        public bool MuteSpeech
        {
            get { return _plugin.MuteSpeech; }
        }

        public void LogInfo(string message)
        {
            LoaderLog.Info(message);
        }

        public void LogWarning(string message)
        {
            LoaderLog.Warn(message);
        }

        public void LogError(string message)
        {
            LoaderLog.Error(message);
        }

        /// <summary>The game install folder, where native libraries the mod ships live.</summary>
        public string GameRootPath
        {
            get { return BepInEx.Paths.GameRootPath; }
        }

        /// <summary>Where the plugin was deployed. A bytes-loaded assembly has no Location of its
        /// own, so the mod must take this rather than ask itself where it came from.</summary>
        public string PluginDirectory { get; private set; }

        /// <summary>Hands work from HTTP handler threads to the Unity main thread; the loader
        /// drains it once per frame. Route handlers that touch the game must go through it.</summary>
        public MainThreadQueue MainThread
        {
            get { return _dev.MainThread; }
        }

        /// <summary>
        /// Report a line the mod has just spoken. The loader keeps the last couple of hundred, so
        /// POST /eval can answer "and here is what that made it say" - the buffer has to live on
        /// this side because a hot reload takes the mod's own with it, and the point of the
        /// question is usually to check what a freshly reloaded mod does.
        /// </summary>
        public void NotifySpoken(string text)
        {
            _dev.NotifySpoken(text);
        }

        /// <summary>
        /// Answer <paramref name="method"/> <paramref name="path"/> on the dev server until the mod
        /// is unloaded. The loader's own routes (/gui/game, /screenshot, /log, /loader/status,
        /// /reload, /eval, /wait, /quit) are fixed and cannot be taken over.
        ///
        /// <paramref name="allowedQueryParameters"/> is the route's whole vocabulary: the server
        /// answers 400 for any other parameter before the handler runs, so a caller never gets a
        /// successful-looking answer that ignored what it asked for. Naming them here rather than
        /// checking inside the handler is what lets the loader hold one rule for its own routes and
        /// the mod's alike, without knowing anything about the mod.
        /// </summary>
        public void RegisterRoute(
            string method,
            string path,
            DevRouteHandler handler,
            params string[] allowedQueryParameters
        )
        {
            _dev.RegisterModRoute(method, path, handler, allowedQueryParameters);
        }

        public void UnregisterAllModRoutes()
        {
            _dev.UnregisterModRoutes();
        }

        /// <summary>Install the mod's per-frame pump, or null to remove it. Called after the
        /// loader's own ticking, so queued main-thread work has already run.</summary>
        public void SetUpdateHandler(Action update)
        {
            _plugin.SetModUpdateHandler(update);
        }

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            return _plugin.StartModCoroutine(routine);
        }

        /// <summary>Stop every coroutine the mod started. The loader's own coroutines are not
        /// touched, so this cannot take the dev server's screenshot or quit paths down with it.</summary>
        public void StopAllCoroutines()
        {
            _plugin.StopModCoroutines();
        }
    }
}
