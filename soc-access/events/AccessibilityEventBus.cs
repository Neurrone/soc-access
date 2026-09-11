using System;

namespace SongsOfConquestAccess.Events
{
    public static class AccessibilityEventBus
    {
        private static event Action<IAccessibilityEvent> Published;

        private static IAccessibilityEvent _composedEvent;
        private static string _composedText;

        /// <summary>
        /// The speech text of one published event, composed once. Both the announcer and the review
        /// buffer read every event, and <see cref="IAccessibilityEvent.GetSpeechText"/> composes a
        /// fresh string out of live game state, so asking twice costs twice and the two answers can
        /// drift apart. Publication is synchronous on the main thread, so one slot keyed on the
        /// event's identity serves every subscriber of that event.
        /// </summary>
        public static string TextOf(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null)
            {
                return string.Empty;
            }

            if (!ReferenceEquals(accessibilityEvent, _composedEvent))
            {
                _composedText = accessibilityEvent.GetSpeechText() ?? string.Empty;
                _composedEvent = accessibilityEvent;
            }

            return _composedText;
        }

        public static void Publish(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null)
            {
                SocAccessMod.Instance?.LogWarning("AccessibilityEventBus dropped null event");
                return;
            }

            Published?.Invoke(accessibilityEvent);
        }

        public static void Subscribe(Action<IAccessibilityEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            Published -= handler;
            Published += handler;
        }

        public static void Unsubscribe(Action<IAccessibilityEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            Published -= handler;
        }

        public static void Reset()
        {
            Published = null;
            _composedEvent = null;
            _composedText = null;
        }
    }
}
