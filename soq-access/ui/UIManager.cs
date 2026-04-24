using System.Collections.Generic;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal static class UIManager
    {
        private static Widget _currentWidget;
        private static Widget _lastFocusedWidget;
        private static string _lastAnnouncement;
        private static bool _dirty;
        private static readonly FocusContext FocusContext = new FocusContext();

        public static void SetFocusedWidget(Widget widget)
        {
            _currentWidget = widget;
            _dirty = true;
        }

        public static void Reset()
        {
            _lastFocusedWidget?.Unfocus();
            _currentWidget = null;
            _lastFocusedWidget = null;
            _lastAnnouncement = null;
            _dirty = false;
            FocusContext.Reset();
        }

        public static void Update()
        {
            if (!_dirty || _currentWidget == null)
            {
                return;
            }

            _dirty = false;

            string announcement = BuildAnnouncement(_currentWidget);
            if (string.IsNullOrWhiteSpace(announcement))
            {
                return;
            }

            _lastFocusedWidget = _currentWidget;
            if (announcement == _lastAnnouncement)
            {
                return;
            }

            _lastAnnouncement = announcement;
            SoqAccessPlugin.Instance?.LogInfo("UIManager speaking focused widget: \"" + announcement + "\"");
            SpeechPipeline.Output(new SpeechRequest(announcement, interrupt: true));
        }

        private static string BuildAnnouncement(Widget widget)
        {
            List<string> parts = new List<string>(2);
            if (widget.IncludeParentLabelInAnnouncement && widget.Parent != null)
            {
                AddIfNotEmpty(parts, widget.Parent.GetLabel());
            }

            AddIfNotEmpty(parts, FocusContext.BuildAnnouncement(widget));
            return string.Join(". ", parts.ToArray());
        }

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }
    }
}
