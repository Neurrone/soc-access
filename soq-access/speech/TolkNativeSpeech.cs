using System;
using System.IO;
using System.Runtime.InteropServices;
namespace Tolk
{
    public sealed class TolkNativeSpeech : IDisposable
    {
        public sealed class Options
        {
            public string TolkPath { get; set; }
            public string NvdaPath { get; set; }
            public bool TrySapi { get; set; } = true;
            public bool PreferNvdaIfRunning { get; set; } = true;
            public Action<string> Log { get; set; }
            public Action<string> Warn { get; set; }
            public Action<string> Error { get; set; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpLibFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hLibModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Tolk_LoadDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Tolk_UnloadDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool Tolk_IsLoadedDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate bool Tolk_OutputDelegate([MarshalAs(UnmanagedType.LPWStr)] string str, bool interrupt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate IntPtr Tolk_DetectScreenReaderDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool Tolk_HasSpeechDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool Tolk_HasBrailleDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Tolk_TrySAPIDelegate(bool trySAPI);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int nvdaController_testIfRunningDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate int nvdaController_speakTextDelegate([MarshalAs(UnmanagedType.LPWStr)] string text);

        private IntPtr tolkHandle = IntPtr.Zero;
        private IntPtr nvdaHandle = IntPtr.Zero;

        private Tolk_LoadDelegate Tolk_Load;
        private Tolk_UnloadDelegate Tolk_Unload;
        private Tolk_IsLoadedDelegate Tolk_IsLoaded;
        private Tolk_OutputDelegate Tolk_Output;
        private Tolk_DetectScreenReaderDelegate Tolk_DetectScreenReader;
        private Tolk_HasSpeechDelegate Tolk_HasSpeech;
        private Tolk_HasBrailleDelegate Tolk_HasBraille;
        private Tolk_TrySAPIDelegate Tolk_TrySAPI;
        private nvdaController_testIfRunningDelegate nvdaController_testIfRunning;
        private nvdaController_speakTextDelegate nvdaController_speakText;

        private bool isInitialized;
        private bool useDirectNvda;
        private readonly object tolkLock = new object();
        private Options options;

        public bool Initialize(Options options)
        {
            if (isInitialized)
            {
                return IsActive;
            }

            if (options == null)
            {
                return false;
            }

            this.options = options;

            if (string.IsNullOrEmpty(options.TolkPath) || !File.Exists(options.TolkPath))
            {
                options.Error?.Invoke($"Tolk.dll not found at: {options.TolkPath}");
                return false;
            }

            try
            {
                LoadNvda(options.NvdaPath);
                LoadTolk(options.TolkPath);
                BindTolkFunctions();

                bool nvdaRunning = TestNvda();

                Tolk_Load();
                if (options.TrySapi)
                {
                    Tolk_TrySAPI(true);
                }

                isInitialized = true;

                if (IsActive)
                {
                    string readerName = DetectScreenReaderName();
                    options.Log?.Invoke($"Tolk initialized (native). Detected: {readerName}");
                    options.Log?.Invoke($"Speech: {SafeCallBool(() => Tolk_HasSpeech())}; Braille: {SafeCallBool(() => Tolk_HasBraille())}");

                    if (options.PreferNvdaIfRunning && readerName == "SAPI" && nvdaRunning)
                    {
                        options.Warn?.Invoke("Tolk fell back to SAPI while NVDA is running. Using direct NVDA output.");
                        useDirectNvda = true;
                    }
                }
                else
                {
                    options.Warn?.Invoke("Tolk initialized but no screen reader detected.");
                }
            }
            catch (Exception ex)
            {
                options.Error?.Invoke($"Failed to initialize Tolk (native): {ex.Message}");
                Shutdown();
                return false;
            }

            return IsActive;
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            try
            {
                Tolk_Unload?.Invoke();
            }
            catch (Exception ex)
            {
                options?.Error?.Invoke($"Error unloading Tolk (native): {ex.Message}");
            }
            finally
            {
                isInitialized = false;
                useDirectNvda = false;
                FreeLibraries();
                ClearDelegates();
            }
        }

        public bool IsActive
        {
            get
            {
                if (!isInitialized || Tolk_IsLoaded == null)
                {
                    return false;
                }

                try
                {
                    return Tolk_IsLoaded();
                }
                catch (Exception ex)
                {
                    options?.Error?.Invoke($"Error checking Tolk status (native): {ex.Message}");
                    return false;
                }
            }
        }

        public void Speak(string text, SpeechPriority priority = SpeechPriority.Normal)
        {
            bool interrupt = priority == SpeechPriority.High;
            Speak(text, interrupt);
        }

        public void Speak(string text, bool interrupt)
        {
            if (string.IsNullOrEmpty(text) || !IsActive)
            {
                return;
            }

            try
            {
                lock (tolkLock)
                {
                    if (useDirectNvda && nvdaController_speakText != null)
                    {
                        int result = nvdaController_speakText(text);
                        if (result == 0)
                        {
                            return;
                        }

                        options?.Warn?.Invoke("Direct NVDA speech failed; falling back to Tolk output.");
                        useDirectNvda = false;
                    }

                    Tolk_Output?.Invoke(text, interrupt);
                }
            }
            catch (Exception ex)
            {
                options?.Error?.Invoke($"Error speaking text (native): {ex.Message}");
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        private void LoadNvda(string nvdaPath)
        {
            if (string.IsNullOrEmpty(nvdaPath) || !File.Exists(nvdaPath))
            {
                return;
            }

            nvdaHandle = LoadLibraryW(nvdaPath);
            if (nvdaHandle == IntPtr.Zero)
            {
                options.Warn?.Invoke("Failed to load nvdaControllerClient64.dll");
                return;
            }

            nvdaController_testIfRunning = GetFunction<nvdaController_testIfRunningDelegate>(nvdaHandle, "nvdaController_testIfRunning");
            nvdaController_speakText = GetFunction<nvdaController_speakTextDelegate>(nvdaHandle, "nvdaController_speakText");
        }

        private void LoadTolk(string tolkPath)
        {
            tolkHandle = LoadLibraryW(tolkPath);
            if (tolkHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new DllNotFoundException($"Failed to load Tolk.dll (error code: {error})");
            }
        }

        private void BindTolkFunctions()
        {
            Tolk_Load = GetFunction<Tolk_LoadDelegate>(tolkHandle, "Tolk_Load");
            Tolk_Unload = GetFunction<Tolk_UnloadDelegate>(tolkHandle, "Tolk_Unload");
            Tolk_IsLoaded = GetFunction<Tolk_IsLoadedDelegate>(tolkHandle, "Tolk_IsLoaded");
            Tolk_Output = GetFunction<Tolk_OutputDelegate>(tolkHandle, "Tolk_Output");
            Tolk_DetectScreenReader = GetFunction<Tolk_DetectScreenReaderDelegate>(tolkHandle, "Tolk_DetectScreenReader");
            Tolk_HasSpeech = GetFunction<Tolk_HasSpeechDelegate>(tolkHandle, "Tolk_HasSpeech");
            Tolk_HasBraille = GetFunction<Tolk_HasBrailleDelegate>(tolkHandle, "Tolk_HasBraille");
            Tolk_TrySAPI = GetFunction<Tolk_TrySAPIDelegate>(tolkHandle, "Tolk_TrySAPI");
        }

        private bool TestNvda()
        {
            if (nvdaController_testIfRunning == null)
            {
                return false;
            }

            try
            {
                return nvdaController_testIfRunning() == 0;
            }
            catch
            {
                return false;
            }
        }

        private string DetectScreenReaderName()
        {
            try
            {
                IntPtr namePtr = Tolk_DetectScreenReader?.Invoke() ?? IntPtr.Zero;
                return namePtr != IntPtr.Zero ? Marshal.PtrToStringUni(namePtr) : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private bool SafeCallBool(Func<bool> call)
        {
            if (call == null)
            {
                return false;
            }

            try
            {
                return call();
            }
            catch
            {
                return false;
            }
        }

        private void FreeLibraries()
        {
            if (tolkHandle != IntPtr.Zero)
            {
                FreeLibrary(tolkHandle);
                tolkHandle = IntPtr.Zero;
            }

            if (nvdaHandle != IntPtr.Zero)
            {
                FreeLibrary(nvdaHandle);
                nvdaHandle = IntPtr.Zero;
            }
        }

        private void ClearDelegates()
        {
            Tolk_Load = null;
            Tolk_Unload = null;
            Tolk_IsLoaded = null;
            Tolk_Output = null;
            Tolk_DetectScreenReader = null;
            Tolk_HasSpeech = null;
            Tolk_HasBraille = null;
            Tolk_TrySAPI = null;
            nvdaController_testIfRunning = null;
            nvdaController_speakText = null;
        }

        private static T GetFunction<T>(IntPtr library, string functionName) where T : Delegate
        {
            IntPtr procAddress = GetProcAddress(library, functionName);
            if (procAddress == IntPtr.Zero)
            {
                throw new Exception($"Could not find function '{functionName}' in library");
            }

            return (T)Marshal.GetDelegateForFunctionPointer(procAddress, typeof(T));
        }

    }
}
