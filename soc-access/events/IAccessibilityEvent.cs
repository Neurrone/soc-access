namespace SongsOfConquestAccess.Events
{
    public interface IAccessibilityEvent
    {
        string Kind { get; }

        string GetSpeechText();
    }
}
