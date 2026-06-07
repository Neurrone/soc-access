using System;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;

namespace SongsOfConquestAccess.Speech
{
    internal sealed class SpeechService : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private readonly ManualLogSource _logger;
        private IntPtr _context = IntPtr.Zero;
        private IntPtr _backend = IntPtr.Zero;
        private IntPtr _prismLibrary = IntPtr.Zero;
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
                if (_prismLibrary == IntPtr.Zero)
                {
                    _prismLibrary = LoadLibrary(prismPath);
                    if (_prismLibrary == IntPtr.Zero)
                    {
                        _logger.LogError("Failed to preload Prism.dll from: " + prismPath + " (Win32 error " + Marshal.GetLastWin32Error() + ")");
                        return false;
                    }
                }

                PrismNative.PrismConfig config = PrismNative.ConfigInit();
                _context = PrismNative.Init(ref config);
                if (_context == IntPtr.Zero)
                {
                    _logger.LogError("Prism initialization failed: prism_init returned null");
                    Dispose();
                    return false;
                }

                _backend = PrismNative.RegistryAcquireBest(_context);
                if (_backend == IntPtr.Zero)
                {
                    _logger.LogError("Prism initialization failed: no available speech backend");
                    Dispose();
                    return false;
                }

                _backendFeatures = (PrismNative.BackendFeatures)PrismNative.BackendGetFeatures(_backend);
                _initialized = true;

                string backendName = PrismNative.BackendName(_backend) ?? "<unknown>";
                _logger.LogInfo("Prism initialized. Backend: " + backendName + " (features=0x" + ((ulong)_backendFeatures).ToString("X") + ")");
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

            if (_prismLibrary != IntPtr.Zero)
            {
                try
                {
                    if (!FreeLibrary(_prismLibrary))
                    {
                        _logger.LogWarning("Prism FreeLibrary failed with Win32 error " + Marshal.GetLastWin32Error());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Prism FreeLibrary failed: " + ex.Message);
                }
                finally
                {
                    _prismLibrary = IntPtr.Zero;
                }
            }
        }

        private static string FormatError(PrismNative.PrismError error)
        {
            return error + ": " + PrismNative.ErrorString(error);
        }
    }
}
