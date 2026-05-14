using System;

namespace SongsOfConquestAccess.Events
{
    internal static class AccessibilityEventBus
    {
        private static event Action<IAccessibilityEvent> Published;

        public static void Publish(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null)
            {
                SocAccessPlugin.Instance?.LogWarning("AccessibilityEventBus dropped null event");
                return;
            }

            Published?.Invoke(accessibilityEvent);
        }

        internal static void Subscribe(Action<IAccessibilityEvent> handler)
        {
            if (handler == null)
            {
                return;
            }

            Published -= handler;
            Published += handler;
        }

        internal static void Unsubscribe(Action<IAccessibilityEvent> handler)
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
        }
    }
}
