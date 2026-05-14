using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Buffers
{
    internal sealed class BufferEventRecorder
    {
        private readonly ReviewBufferManager _buffers;
        private bool _attached;

        public BufferEventRecorder(ReviewBufferManager buffers)
        {
            _buffers = buffers;
        }

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

        private void OnAccessibilityEvent(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null || _buffers == null)
            {
                return;
            }

            string text = SpeechTextSanitizer.Normalize(accessibilityEvent.GetSpeechText());
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (IsNotification(accessibilityEvent.Kind))
            {
                _buffers.AppendLine(ReviewBufferKind.AdventureMapNotifications, text);
                return;
            }

            if (IsCombatEvent(accessibilityEvent.Kind))
            {
                _buffers.AppendLine(ReviewBufferKind.CombatEvents, text);
            }
        }

        private static bool IsNotification(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind)
                && kind.StartsWith("notification.");
        }

        private static bool IsCombatEvent(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind)
                && kind.StartsWith("combat.");
        }
    }
}
