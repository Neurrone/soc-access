namespace SongsOfConquestAccess.Events
{
    internal interface IAccessibilityEvent
    {
        string Kind { get; }

        string GetSpeechText();
    }
}
