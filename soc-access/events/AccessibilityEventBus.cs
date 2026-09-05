using System;

namespace SongsOfConquestAccess.Events
{
    public static class AccessibilityEventBus
    {
        private static event Action<IAccessibilityEvent> Published;

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
        }
    }
}
