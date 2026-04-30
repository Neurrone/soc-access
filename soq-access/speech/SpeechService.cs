using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Tolk;

namespace SongsOfConquestAccess.Speech
{
    internal sealed class SpeechService : IDisposable
    {
        private readonly ManualLogSource _logger;
        private readonly TolkNativeSpeech _nativeSpeech;
        private bool _initialized;

        public SpeechService(ManualLogSource logger)
        {
            _logger = logger;
            _nativeSpeech = new TolkNativeSpeech();
        }

        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            string tolkPath = Path.Combine(Paths.GameRootPath, "Tolk.dll");
            string nvdaPath = Path.Combine(Paths.GameRootPath, "nvdaControllerClient64.dll");

            if (!File.Exists(tolkPath))
            {
                return false;
            }

            _initialized = _nativeSpeech.Initialize(new TolkNativeSpeech.Options
            {
                TolkPath = tolkPath,
                NvdaPath = File.Exists(nvdaPath) ? nvdaPath : null,
                TrySapi = true,
                PreferNvdaIfRunning = true,
                Log = message => _logger.LogInfo(message),
                Warn = message => _logger.LogWarning(message),
                Error = message => _logger.LogError(message)
            });

            return _initialized;
        }

        public void Speak(string text, bool interrupt)
        {
            if (!_initialized)
            {
                _logger.LogWarning("SpeechService dropped speech because it is not initialized");
                return;
            }

            if (!_nativeSpeech.IsActive)
            {
                _logger.LogWarning("SpeechService dropped speech because no active speech backend is available");
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("SpeechService dropped empty speech text");
                return;
            }

            _logger.LogInfo("SpeechService speaking: \"" + text + "\", interrupt=" + interrupt);
            _nativeSpeech.Speak(text, interrupt);
        }

        public void Silence()
        {
            if (!_initialized)
            {
                return;
            }

            if (!_nativeSpeech.IsActive)
            {
                return;
            }

            _logger.LogInfo("SpeechService silencing speech");
            _nativeSpeech.Silence();
        }

        public void Dispose()
        {
            _nativeSpeech.Dispose();
        }
    }
}
