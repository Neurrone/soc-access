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
        private int _maxFrameRate;

        /// <summary>Whether this launch was asked to keep the screen reader silent. Held here
        /// rather than re-read by the mod so a hot reload stays muted.</summary>
        internal bool MuteSpeech { get; private set; }

        private void Awake()
        {
            LoaderLog.Install(Logger.LogInfo, Logger.LogWarning, Logger.LogError);
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var devServer = Config.Bind("Dev", "devServer", false,
                "Enable the loopback developer HTTP server on http://127.0.0.1:8772. Development only; leave off to play.");
            var maxFrameRate = Config.Bind("Performance", "maxFrameRate", 0,
                "Development only: cap the frame rate to this many frames per second, to cut the CPU "
                    + "the game burns in a virtual machine with no GPU acceleration. 0 disables the "
                    + "cap and leaves the game's own frame rate and vertical sync settings untouched.");
            _maxFrameRate = maxFrameRate.Value;
            var resolution = Config.Bind("Performance", "resolution", "",
                "Development only: force this render resolution as WIDTHxHEIGHT (for example "
                    + "1280x800), regardless of what the display driver advertises, to cut the CPU a "
                    + "software renderer burns per pixel. Empty leaves the game's own resolution "
                    + "handling untouched.");
            ParseForcedResolution(resolution.Value);
            // One-shot on purpose: a persistent mute once silenced the owner's own launch after a
            // development run, so the switch covers only the launch that reads it.
            var muteSpeech = Config.Bind("Dev", "muteSpeech", false,
                "Development only: mute the screen reader for the NEXT launch only, while the dev "
                    + "server still captures every line. Written back to false as soon as it is "
                    + "read. run-game.ps1 -NoSpeech sets it.");
            MuteSpeech = muteSpeech.Value;
            if (muteSpeech.Value) muteSpeech.Value = false;
            _dev = new DevServer(this);
            _mods = new ModLoader(this, _dev, pluginDirectory);
            _dev.Mods = _mods;
            _dev.Start(devServer.Value);
            _mods.Load();
        }

        private void Update()
        {
            ApplyFrameRateCap();
            ApplyForcedResolution();
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

        private int _forcedWidth;
        private int _forcedHeight;

        private void ParseForcedResolution(string setting)
        {
            string value = setting == null ? "" : setting.Trim();
            if (value.Length == 0) return;

            string[] parts = value.Split('x', 'X');
            int width;
            int height;
            if (parts.Length != 2
                || !int.TryParse(parts[0].Trim(), out width)
                || !int.TryParse(parts[1].Trim(), out height)
                || width <= 0
                || height <= 0)
            {
                LoaderLog.Warn("Performance/resolution is not a WIDTHxHEIGHT size and was ignored: " + value);
                return;
            }

            _forcedWidth = width;
            _forcedHeight = height;
        }

        private void ApplyForcedResolution()
        {
            if (_forcedWidth <= 0) return;

            // Same re-assert story as the frame cap, and for the same reason: the game's video
            // settings push the player's stored resolution back through Screen.SetResolution
            // when they are applied. SetResolution lands a frame or more later, so the comparison
            // retries until it takes and then stays quiet.
            if (Screen.width == _forcedWidth && Screen.height == _forcedHeight) return;

            Screen.SetResolution(_forcedWidth, _forcedHeight, Screen.fullScreen);
        }

        private void ApplyFrameRateCap()
        {
            if (_maxFrameRate <= 0) return;

            // The game's own FrameRateManager writes both of these whenever the video settings
            // are applied (SetRefreshRateDivider turns vertical sync on and resets the target
            // frame rate), long after this plugin wakes. So the cap is re-asserted every frame,
            // not set once. Application.targetFrameRate is ignored while vertical sync is on.
            if (QualitySettings.vSyncCount != 0) QualitySettings.vSyncCount = 0;
            if (Application.targetFrameRate != _maxFrameRate) Application.targetFrameRate = _maxFrameRate;
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
