using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SongsOfConquestAccess.Speech
{
    internal static class PrismNative
    {
        private const string Dll = "prism";

        public enum PrismError
        {
            Ok = 0,
            NotInitialized = 1,
            InvalidParam = 2,
            NotImplemented = 3,
            NoVoices = 4,
            VoiceNotFound = 5,
            SpeakFailure = 6,
            MemoryFailure = 7,
            RangeOutOfBounds = 8,
            Internal = 9,
            NotSpeaking = 10,
            NotPaused = 11,
            AlreadyPaused = 12,
            InvalidUtf8 = 13,
            InvalidOperation = 14,
            AlreadyInitialized = 15,
            BackendNotAvailable = 16,
            Unknown = 17,
            InvalidAudioFormat = 18,
            InternalBackendLimitExceeded = 19,
            BackendEnteredUndefinedState = 20,
            Count = 21
        }

        [Flags]
        public enum BackendFeatures : ulong
        {
            SupportedAtRuntime = 1UL << 0,
            SupportsSpeak = 1UL << 2,
            SupportsSpeakToMemory = 1UL << 3,
            SupportsBraille = 1UL << 4,
            SupportsOutput = 1UL << 5,
            SupportsIsSpeaking = 1UL << 6,
            SupportsStop = 1UL << 7,
            SupportsPause = 1UL << 8,
            SupportsResume = 1UL << 9,
            SupportsSetVolume = 1UL << 10,
            SupportsGetVolume = 1UL << 11,
            SupportsSetRate = 1UL << 12,
            SupportsGetRate = 1UL << 13,
            SupportsSetPitch = 1UL << 14,
            SupportsGetPitch = 1UL << 15,
            SupportsRefreshVoices = 1UL << 16,
            SupportsCountVoices = 1UL << 17,
            SupportsGetVoiceName = 1UL << 18,
            SupportsGetVoiceLanguage = 1UL << 19,
            SupportsGetVoice = 1UL << 20,
            SupportsSetVoice = 1UL << 21,
            SupportsGetChannels = 1UL << 22,
            SupportsGetSampleRate = 1UL << 23,
            SupportsGetBitDepth = 1UL << 24,
            PerformsSilenceTrimmingOnSpeak = 1UL << 25,
            PerformsSilenceTrimmingOnSpeakToMemory = 1UL << 26,
            SupportsSpeakSsml = 1UL << 27,
            SupportsSpeakToMemorySsml = 1UL << 28
        }

        [DllImport(Dll, EntryPoint = "prism_init", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Init(IntPtr config);

        [DllImport(Dll, EntryPoint = "prism_shutdown", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Shutdown(IntPtr context);

        [DllImport(Dll, EntryPoint = "prism_registry_create_best", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr RegistryCreateBest(IntPtr context);

        [DllImport(Dll, EntryPoint = "prism_backend_free", CallingConvention = CallingConvention.Cdecl)]
        public static extern void BackendFree(IntPtr backend);

        [DllImport(Dll, EntryPoint = "prism_backend_name", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr BackendNameRaw(IntPtr backend);

        public static string BackendName(IntPtr backend)
        {
            return Utf8FromPtr(BackendNameRaw(backend));
        }

        [DllImport(Dll, EntryPoint = "prism_backend_get_features", CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong BackendGetFeatures(IntPtr backend);

        [DllImport(Dll, EntryPoint = "prism_backend_output", CallingConvention = CallingConvention.Cdecl)]
        private static extern PrismError BackendOutputRaw(
            IntPtr backend,
            byte[] textUtf8,
            [MarshalAs(UnmanagedType.I1)] bool interrupt);

        public static PrismError BackendOutput(IntPtr backend, string text, bool interrupt)
        {
            return BackendOutputRaw(backend, Utf8(text), interrupt);
        }

        [DllImport(Dll, EntryPoint = "prism_backend_speak", CallingConvention = CallingConvention.Cdecl)]
        private static extern PrismError BackendSpeakRaw(
            IntPtr backend,
            byte[] textUtf8,
            [MarshalAs(UnmanagedType.I1)] bool interrupt);

        public static PrismError BackendSpeak(IntPtr backend, string text, bool interrupt)
        {
            return BackendSpeakRaw(backend, Utf8(text), interrupt);
        }

        [DllImport(Dll, EntryPoint = "prism_backend_stop", CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError BackendStop(IntPtr backend);

        [DllImport(Dll, EntryPoint = "prism_error_string", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ErrorStringRaw(PrismError error);

        public static string ErrorString(PrismError error)
        {
            string message = Utf8FromPtr(ErrorStringRaw(error));
            return string.IsNullOrEmpty(message) ? error.ToString() : message;
        }

        private static byte[] Utf8(string text)
        {
            if (text == null)
            {
                text = string.Empty;
            }

            int length = Encoding.UTF8.GetByteCount(text);
            byte[] buffer = new byte[length + 1];
            Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
            return buffer;
        }

        private static string Utf8FromPtr(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            byte[] buffer = new byte[length];
            Marshal.Copy(ptr, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }
    }
}
