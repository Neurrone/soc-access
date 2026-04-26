namespace SongsOfConquestAccess.Events
{
    internal interface IAccessibilityEvent
    {
        string Kind { get; }

        bool Interrupt { get; }

        string GetSpeechText();
    }
}
