namespace SongsOfConquestAccess.Speech
{
    public sealed class SpeechRequest
    {
        public SpeechRequest(string text, bool interrupt)
        {
            Text = text ?? string.Empty;
            Interrupt = interrupt;
        }

        public string Text { get; private set; }

        public bool Interrupt { get; private set; }
    }
}
