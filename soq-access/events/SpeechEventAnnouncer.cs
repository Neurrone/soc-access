using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Events
{
    internal sealed class SpeechEventAnnouncer
    {
        private bool _attached;

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            AccessibilityEventBus.Subscribe(OnAccessibilityEvent);
            _attached = true;
        }

        public void Detach()
        {
            if (!_attached)
            {
                return;
            }

            AccessibilityEventBus.Unsubscribe(OnAccessibilityEvent);
            _attached = false;
        }

        private static void OnAccessibilityEvent(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null)
            {
                return;
            }

            string text = accessibilityEvent.GetSpeechText();
            if (string.IsNullOrWhiteSpace(text))
            {
                SoqAccessPlugin.Instance?.LogWarning(
                    "SpeechEventAnnouncer dropped event with empty text: " + accessibilityEvent.Kind);
                return;
            }

            SpeechPipeline.Output(new SpeechRequest(text, false));
        }
    }
}
