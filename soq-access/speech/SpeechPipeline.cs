namespace SongsOfConquestAccess.Speech
{
    internal static class SpeechPipeline
    {
        private static SpeechService _speechService;

        public static void Initialize(SpeechService speechService)
        {
            _speechService = speechService;
        }

        public static void Shutdown()
        {
            _speechService = null;
        }

        public static void Output(SpeechRequest request)
        {
            if (request == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("SpeechPipeline dropped null request");
                return;
            }

            string text = request.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SoqAccessPlugin.Instance?.LogWarning("SpeechPipeline dropped empty text");
                return;
            }

            SoqAccessPlugin.Instance?.LogInfo("SpeechPipeline output: \"" + text + "\", interrupt=" + request.Interrupt);
            _speechService.Speak(text, request.Interrupt);
        }

        public static void Silence()
        {
            _speechService?.Silence();
        }
    }
}
