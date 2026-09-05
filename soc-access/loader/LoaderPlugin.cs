using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using SongsOfConquestAccess.Loader.Dev;
using UnityEngine;

namespace SongsOfConquestAccess.Loader
{
    /// <summary>The stable BepInEx plugin that owns the dev server and reloadable mod.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class LoaderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "songs.of.conquest.access";
        public const string PluginName = "Songs of Conquest Access";
        public const string PluginVersion = "1.0.0";

        private readonly List<Coroutine> _modCoroutines = new List<Coroutine>();
        private DevServer _dev;
        private ModLoader _mods;
        private Action _modUpdate;

        private void Awake()
        {
            LoaderLog.Install(Logger.LogInfo, Logger.LogWarning, Logger.LogError);
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var devServer = Config.Bind("Dev", "devServer", false,
                "Enable the loopback developer HTTP server on http://127.0.0.1:8772. Development only; leave off to play.");
            _dev = new DevServer(this);
            _mods = new ModLoader(this, _dev, pluginDirectory);
            _dev.Mods = _mods;
            _dev.Start(devServer.Value);
            _mods.Load();
        }

        private void Update()
        {
            _dev.Tick();
            Action update = _modUpdate;
            if (update == null) return;
            try { update(); }
            catch (Exception e)
            {
                _modUpdate = null;
                LoaderLog.Error("Mod update handler threw and was switched off; POST /reload to restore it: " + e);
            }
        }

        private void OnDestroy()
        {
            if (_mods != null) _mods.Unload();
            if (_dev != null) _dev.Stop();
        }

        internal void SetModUpdateHandler(Action update) { _modUpdate = update; }

        internal Coroutine StartModCoroutine(IEnumerator routine)
        {
            Coroutine coroutine = StartCoroutine(routine);
            _modCoroutines.Add(coroutine);
            return coroutine;
        }

        internal void StopModCoroutines()
        {
            foreach (Coroutine coroutine in _modCoroutines) StopCoroutine(coroutine);
            _modCoroutines.Clear();
        }
    }
}
