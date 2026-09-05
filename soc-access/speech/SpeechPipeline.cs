using System;

namespace SongsOfConquestAccess.Speech
{
    public static class SpeechPipeline
    {
        private static SpeechService _speechService;
        private static bool _muted;

        // The dev server's tap on the one place every spoken line passes through, so
        // GET /speech reports exactly what a listener would have heard. Set while the mod's
        // routes are registered and cleared when they are taken down.
        public static Action<string> Observer;

        public static void Initialize(SpeechService speechService, bool muteFromConfig)
        {
            _speechService = speechService;
            // Read once: a test run decides before launch whether it wants to hear the mod, and a
            // value that could change mid-session would make two identical runs differ. The config
            // setting exists because the game relaunches itself through Steam, which drops the
            // environment a launcher script set.
            _muted = muteFromConfig || Environment.GetEnvironmentVariable("SOCACCESS_NO_SPEECH") == "1";
        }

        public static void Shutdown()
        {
            _speechService = null;
        }

        /// <summary>Whether SOCACCESS_NO_SPEECH is holding the backend silent. The lines are still
        /// logged and still reach the observer, so a muted run is fully readable over HTTP.</summary>
        public static bool Muted
        {
            get { return _muted; }
        }

        public static void Output(SpeechRequest request)
        {
            if (request == null)
            {
                SocAccessMod.Instance?.LogWarning("SpeechPipeline dropped null request");
                return;
            }

            string text = request.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SocAccessMod.Instance?.LogWarning("SpeechPipeline dropped empty text");
                return;
            }

            SocAccessMod.Instance?.LogInfo("SpeechPipeline output: \"" + text + "\", interrupt=" + request.Interrupt);
            Observer?.Invoke(text);
            if (_muted)
            {
                return;
            }

            _speechService.Speak(text, request.Interrupt);
        }

        public static void Silence()
        {
            // A muted run must leave the screen reader alone entirely: cutting it off on every
            // key press is as intrusive as speaking, and the owner may be using it meanwhile.
            if (_muted)
            {
                return;
            }

            _speechService?.Silence();
        }
    }
}
