using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;

namespace SongsOfConquestAccess.Speech
{
    internal sealed class SpeechService : IDisposable
    {
        private readonly ManualLogSource _logger;
        private IntPtr _context = IntPtr.Zero;
        private IntPtr _backend = IntPtr.Zero;
        private PrismNative.BackendFeatures _backendFeatures;
        private bool _initialized;

        public SpeechService(ManualLogSource logger)
        {
            _logger = logger;
        }

        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            string prismPath = Path.Combine(Paths.GameRootPath, "prism.dll");
            if (!File.Exists(prismPath))
            {
                _logger.LogError("Prism.dll not found at: " + prismPath);
                return false;
            }

            try
            {
                _context = PrismNative.Init(IntPtr.Zero);
                if (_context == IntPtr.Zero)
                {
                    _logger.LogError("Prism initialization failed: prism_init returned null");
                    return false;
                }

                List<BackendDiagnosticInfo> registryBackends = LogRegistryDiagnostics();
                _backend = PrismNative.RegistryCreateBest(_context);
                if (_backend == IntPtr.Zero)
                {
                    LogBoiyuInitializationProbe(null);
                    _logger.LogError("Prism initialization failed: no available speech backend");
                    Dispose();
                    return false;
                }

                _backendFeatures = (PrismNative.BackendFeatures)PrismNative.BackendGetFeatures(_backend);
                _initialized = true;

                string backendName = PrismNative.BackendName(_backend) ?? "<unknown>";
                BackendDiagnosticInfo selectedInfo = FindBackendByName(registryBackends, backendName);
                _logger.LogInfo(
                    "Prism initialized. Backend: "
                    + backendName
                    + " (id="
                    + FormatBackendId(selectedInfo != null ? selectedInfo.Id : 0)
                    + ", priority="
                    + FormatPriority(selectedInfo)
                    + ", features=0x"
                    + ((ulong)_backendFeatures).ToString("X")
                    + " ["
                    + FormatFeatures(_backendFeatures)
                    + "])");
                if (selectedInfo == null || selectedInfo.Id != PrismNative.BackendBoyPcReader)
                {
                    LogBoiyuInitializationProbe(selectedInfo);
                }
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError("Failed to load Prism.dll: " + ex.Message);
                Dispose();
            }
            catch (EntryPointNotFoundException ex)
            {
                _logger.LogError("Prism.dll does not expose the expected API: " + ex.Message);
                Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize Prism: " + ex);
                Dispose();
            }

            return _initialized;
        }

        public void Speak(string text, bool interrupt)
        {
            if (!_initialized)
            {
                _logger.LogWarning("SpeechService dropped speech because it is not initialized");
                return;
            }

            if (_backend == IntPtr.Zero)
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
            PrismNative.PrismError error = PrismNative.PrismError.NotImplemented;

            if ((_backendFeatures & PrismNative.BackendFeatures.SupportsOutput) != 0)
            {
                error = PrismNative.BackendOutput(_backend, text, interrupt);
                if (error == PrismNative.PrismError.Ok)
                {
                    return;
                }

                _logger.LogWarning("Prism output failed (" + FormatError(error) + "); falling back to speech");
            }

            if ((_backendFeatures & PrismNative.BackendFeatures.SupportsSpeak) == 0)
            {
                _logger.LogWarning("SpeechService dropped speech because Prism backend supports neither output nor speech");
                return;
            }

            error = PrismNative.BackendSpeak(_backend, text, interrupt);
            if (error != PrismNative.PrismError.Ok)
            {
                _logger.LogError("Prism speech failed: " + FormatError(error));
            }
        }

        public void Silence()
        {
            if (!_initialized || _backend == IntPtr.Zero)
            {
                return;
            }

            if ((_backendFeatures & PrismNative.BackendFeatures.SupportsStop) == 0)
            {
                return;
            }

            _logger.LogInfo("SpeechService silencing speech");
            PrismNative.PrismError error = PrismNative.BackendStop(_backend);
            if (error != PrismNative.PrismError.Ok && error != PrismNative.PrismError.NotSpeaking)
            {
                _logger.LogWarning("Prism stop failed: " + FormatError(error));
            }
        }

        public void Dispose()
        {
            if (_backend != IntPtr.Zero)
            {
                try
                {
                    if ((_backendFeatures & PrismNative.BackendFeatures.SupportsStop) != 0)
                    {
                        PrismNative.BackendStop(_backend);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Prism stop during dispose failed: " + ex.Message);
                }

                try
                {
                    PrismNative.BackendFree(_backend);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Prism backend free failed: " + ex.Message);
                }
            }

            if (_context != IntPtr.Zero)
            {
                try
                {
                    PrismNative.Shutdown(_context);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Prism shutdown failed: " + ex.Message);
                }
            }

            _backend = IntPtr.Zero;
            _context = IntPtr.Zero;
            _backendFeatures = 0;
            _initialized = false;
        }

        private static string FormatError(PrismNative.PrismError error)
        {
            return error + ": " + PrismNative.ErrorString(error);
        }

        private List<BackendDiagnosticInfo> LogRegistryDiagnostics()
        {
            List<BackendDiagnosticInfo> backends = new List<BackendDiagnosticInfo>();
            try
            {
                UIntPtr countValue = PrismNative.RegistryCount(_context);
                ulong count = countValue.ToUInt64();
                _logger.LogInfo("Prism registry contains " + count + " backend(s)");

                for (ulong index = 0; index < count; index++)
                {
                    ulong id = PrismNative.RegistryIdAt(_context, new UIntPtr(index));
                    string name = PrismNative.RegistryName(_context, id) ?? "<unknown>";
                    int priority = PrismNative.RegistryPriority(_context, id);
                    bool exists = PrismNative.RegistryExists(_context, id);
                    BackendDiagnosticInfo info = new BackendDiagnosticInfo(id, name, priority, exists);
                    backends.Add(info);
                    _logger.LogInfo(
                        "Prism registry backend: id="
                        + FormatBackendId(id)
                        + ", name="
                        + name
                        + ", priority="
                        + priority);
                }

                LogBoiyuRegistryDiagnostics(backends);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Prism registry diagnostics failed: " + ex.Message);
            }

            return backends;
        }

        private void LogBoiyuRegistryDiagnostics(List<BackendDiagnosticInfo> backends)
        {
            BackendDiagnosticInfo boiyu = FindBackendById(backends, PrismNative.BackendBoyPcReader);
            if (boiyu != null)
            {
                _logger.LogInfo(
                    "Prism Boiyu backend registry entry: exists="
                    + boiyu.Exists
                    + ", id="
                    + FormatBackendId(boiyu.Id)
                    + ", name="
                    + boiyu.Name
                    + ", priority="
                    + boiyu.Priority);
                return;
            }

            bool exists = PrismNative.RegistryExists(_context, PrismNative.BackendBoyPcReader);
            string name = exists ? PrismNative.RegistryName(_context, PrismNative.BackendBoyPcReader) : null;
            string priority = exists ? PrismNative.RegistryPriority(_context, PrismNative.BackendBoyPcReader).ToString() : "<unavailable>";
            _logger.LogInfo(
                "Prism Boiyu backend registry entry: exists="
                + exists
                + ", id="
                + FormatBackendId(PrismNative.BackendBoyPcReader)
                + ", name="
                + (name ?? "<unavailable>")
                + ", priority="
                + priority);
        }

        private void LogBoiyuInitializationProbe(BackendDiagnosticInfo selectedInfo)
        {
            try
            {
                bool exists = PrismNative.RegistryExists(_context, PrismNative.BackendBoyPcReader);
                if (!exists)
                {
                    _logger.LogInfo("Prism Boiyu backend probe skipped because the backend is not registered");
                    return;
                }

                if (selectedInfo != null && selectedInfo.Id == PrismNative.BackendBoyPcReader)
                {
                    _logger.LogInfo("Prism Boiyu backend probe skipped because Boiyu is the selected backend");
                    return;
                }

                IntPtr backend = PrismNative.RegistryCreate(_context, PrismNative.BackendBoyPcReader);
                if (backend == IntPtr.Zero)
                {
                    _logger.LogWarning("Prism Boiyu backend probe failed: registry_create returned null");
                    return;
                }

                try
                {
                    PrismNative.PrismError error = PrismNative.BackendInitialize(backend);
                    PrismNative.BackendFeatures features = (PrismNative.BackendFeatures)PrismNative.BackendGetFeatures(backend);
                    _logger.LogInfo(
                        "Prism Boiyu backend probe: initialize="
                        + FormatError(error)
                        + ", features=0x"
                        + ((ulong)features).ToString("X")
                        + " ["
                        + FormatFeatures(features)
                        + "]");
                }
                finally
                {
                    PrismNative.BackendFree(backend);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Prism Boiyu backend probe failed: " + ex.Message);
            }
        }

        private static BackendDiagnosticInfo FindBackendById(List<BackendDiagnosticInfo> backends, ulong id)
        {
            if (backends == null)
            {
                return null;
            }

            for (int i = 0; i < backends.Count; i++)
            {
                if (backends[i] != null && backends[i].Id == id)
                {
                    return backends[i];
                }
            }

            return null;
        }

        private static BackendDiagnosticInfo FindBackendByName(List<BackendDiagnosticInfo> backends, string name)
        {
            if (backends == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            BackendDiagnosticInfo match = null;
            for (int i = 0; i < backends.Count; i++)
            {
                BackendDiagnosticInfo backend = backends[i];
                if (backend == null || !string.Equals(backend.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = backend;
            }

            return match;
        }

        private static string FormatBackendId(ulong id)
        {
            return id == 0 ? "<unknown>" : "0x" + id.ToString("X16");
        }

        private static string FormatPriority(BackendDiagnosticInfo info)
        {
            return info != null ? info.Priority.ToString() : "<unknown>";
        }

        private static string FormatFeatures(PrismNative.BackendFeatures features)
        {
            if (features == 0)
            {
                return "none";
            }

            List<string> names = new List<string>();
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportedAtRuntime, "SupportedAtRuntime");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSpeak, "SupportsSpeak");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSpeakToMemory, "SupportsSpeakToMemory");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsBraille, "SupportsBraille");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsOutput, "SupportsOutput");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsIsSpeaking, "SupportsIsSpeaking");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsStop, "SupportsStop");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsPause, "SupportsPause");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsResume, "SupportsResume");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSetVolume, "SupportsSetVolume");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetVolume, "SupportsGetVolume");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSetRate, "SupportsSetRate");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetRate, "SupportsGetRate");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSetPitch, "SupportsSetPitch");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetPitch, "SupportsGetPitch");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsRefreshVoices, "SupportsRefreshVoices");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsCountVoices, "SupportsCountVoices");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetVoiceName, "SupportsGetVoiceName");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetVoiceLanguage, "SupportsGetVoiceLanguage");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetVoice, "SupportsGetVoice");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSetVoice, "SupportsSetVoice");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetChannels, "SupportsGetChannels");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetSampleRate, "SupportsGetSampleRate");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsGetBitDepth, "SupportsGetBitDepth");
            AddFeatureName(names, features, PrismNative.BackendFeatures.PerformsSilenceTrimmingOnSpeak, "PerformsSilenceTrimmingOnSpeak");
            AddFeatureName(names, features, PrismNative.BackendFeatures.PerformsSilenceTrimmingOnSpeakToMemory, "PerformsSilenceTrimmingOnSpeakToMemory");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSpeakSsml, "SupportsSpeakSsml");
            AddFeatureName(names, features, PrismNative.BackendFeatures.SupportsSpeakToMemorySsml, "SupportsSpeakToMemorySsml");
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : "unknown";
        }

        private static void AddFeatureName(
            List<string> names,
            PrismNative.BackendFeatures features,
            PrismNative.BackendFeatures feature,
            string name)
        {
            if ((features & feature) != 0)
            {
                names.Add(name);
            }
        }

        private sealed class BackendDiagnosticInfo
        {
            public BackendDiagnosticInfo(ulong id, string name, int priority, bool exists)
            {
                Id = id;
                Name = name;
                Priority = priority;
                Exists = exists;
            }

            public ulong Id { get; private set; }

            public string Name { get; private set; }

            public int Priority { get; private set; }

            public bool Exists { get; private set; }
        }
    }
}
