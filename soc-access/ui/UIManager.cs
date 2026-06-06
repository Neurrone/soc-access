using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal static class UIManager
    {
        private static Widget _currentWidget;
        private static Widget _lastFocusedWidget;
        private static string _lastAnnouncement;
        private static Widget _requestedWidget;
        private static bool _requestedAnnounce;
        private static bool _isCommittingFocus;
        private static Widget _committingWidget;
        private static bool _dirty;
        private static readonly List<Widget> FocusPath = new List<Widget>();
        private static readonly FocusContext FocusContext = new FocusContext();

        public static Widget CurrentWidget
        {
            get { return _currentWidget; }
        }

        public static void SetFocusedWidget(Widget widget)
        {
            RequestFocus(widget);
        }

        public static void RequestFocus(Widget widget)
        {
            RequestFocus(widget, announce: true);
        }

        public static void RequestFocusSilently(Widget widget)
        {
            RequestFocus(widget, announce: false);
        }

        private static void RequestFocus(Widget widget, bool announce)
        {
            Widget target = ResolveFocusedWidget(widget);
            if (target == null)
            {
                return;
            }

            if (_isCommittingFocus && ReferenceEquals(target, _committingWidget))
            {
                return;
            }

            _requestedWidget = target;
            _requestedAnnounce = _requestedAnnounce || announce;
            _dirty = true;
        }

        public static void Reset()
        {
            UnfocusPathFrom(0);
            FocusPath.Clear();
            _currentWidget = null;
            _lastFocusedWidget = null;
            _lastAnnouncement = null;
            _requestedWidget = null;
            _requestedAnnounce = false;
            _dirty = false;
            FocusContext.Reset();
            NativeTooltipUtility.HideTooltip();
        }

        public static void Update()
        {
            if (!_dirty || _requestedWidget == null)
            {
                return;
            }

            Widget target = _requestedWidget;
            bool announce = _requestedAnnounce;
            _requestedWidget = null;
            _requestedAnnounce = false;
            _dirty = false;

            CommitFocus(target);
            _currentWidget = target;

            string announcement = BuildAnnouncement(target);
            if (string.IsNullOrWhiteSpace(announcement))
            {
                NativeTooltipUtility.HideTooltip();
                return;
            }

            bool didFocusChange = !ReferenceEquals(target, _lastFocusedWidget);
            if (!announce)
            {
                _lastFocusedWidget = target;
                _lastAnnouncement = announcement;
                PopulateUiReviewBuffer(target);
                ShowNativeTooltip(target);
                return;
            }

            if (!didFocusChange && announcement == _lastAnnouncement)
            {
                ShowNativeTooltip(target);
                return;
            }

            _lastFocusedWidget = target;
            _lastAnnouncement = announcement;
            SocAccessPlugin.Instance?.LogInfo("UIManager speaking focused widget: \"" + announcement + "\"");
            SpeechPipeline.Output(new SpeechRequest(announcement, interrupt: false));

            PopulateUiReviewBuffer(target);
            ShowNativeTooltip(target);
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

        private static void ShowNativeTooltip(Widget widget)
        {
            Tooltip tooltip = widget != null ? widget.GetTooltip() : null;
            NativeTooltipUtility.ShowVisualTooltip(tooltip != null ? tooltip.VisualMetadata : null);
        }

        private static void PopulateUiReviewBuffer(Widget widget)
        {
            ReviewBufferManager buffers = SocAccessPlugin.Instance?.ReviewBuffers;
            if (buffers == null)
            {
                return;
            }

            List<string> lines = new List<string>();
            if (widget != null)
            {
                string rawLabel = widget.GetLabel();
                string label = SpeechTextSanitizer.Normalize(rawLabel);
                AddNormalizedTextLines(lines, rawLabel);
                AddIfNotEmpty(lines, widget.GetStatus());

                IReadOnlyList<string> tooltipLines = widget.GetTooltipTextLines();
                if (tooltipLines != null)
                {
                    for (int i = 0; i < tooltipLines.Count; i++)
                    {
                        string tooltipLine = SpeechTextSanitizer.Normalize(tooltipLines[i]);
                        // Many native tooltips repeat the focused control name
                        // as their first line. Suppress only exact duplicates so
                        // shorter tooltip headings still get announced when the
                        // focused label contains additional context.
                        if (i == 0
                            && !string.IsNullOrWhiteSpace(label)
                            && !string.IsNullOrWhiteSpace(tooltipLine)
                            && string.Equals(label, tooltipLine, System.StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        AddNormalizedTextLines(lines, tooltipLines[i]);
                    }
                }

                AddIfNotEmpty(lines, widget.GetTooltipActionsText());
            }

            buffers.ReplaceLines(ReviewBufferKind.Ui, lines);
            buffers.SetCurrentBuffer(ReviewBufferKind.Ui);
        }

        private static Widget ResolveFocusedWidget(Widget widget)
        {
            if (widget == null || !widget.IsVisible)
            {
                return null;
            }

            widget.EnsureFocus();
            Widget focusedWidget = widget.GetFocusedWidget();
            if (focusedWidget == null || !focusedWidget.IsVisible)
            {
                return widget;
            }

            return focusedWidget;
        }

        private static void AddNormalizedTextLines(List<string> lines, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string[] rawLines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                AddIfNotEmpty(lines, SpeechTextSanitizer.Normalize(rawLines[i]));
            }
        }

        private static void CommitFocus(Widget widget)
        {
            List<Widget> newPath = BuildPathIncludingSelf(widget);
            int divergenceIndex = FindDivergenceIndex(FocusPath, newPath);

            _isCommittingFocus = true;
            _committingWidget = widget;
            try
            {
                UnfocusPathFrom(divergenceIndex);

                for (int i = divergenceIndex; i < newPath.Count; i++)
                {
                    newPath[i]?.Focus();
                }
            }
            finally
            {
                _committingWidget = null;
                _isCommittingFocus = false;
            }

            FocusPath.Clear();
            FocusPath.AddRange(newPath);
        }

        private static void UnfocusPathFrom(int index)
        {
            for (int i = FocusPath.Count - 1; i >= index; i--)
            {
                FocusPath[i]?.Unfocus();
            }
        }

        private static List<Widget> BuildPathIncludingSelf(Widget widget)
        {
            List<Widget> path = new List<Widget>();
            Widget current = widget;
            while (current != null)
            {
                path.Add(current);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        private static int FindDivergenceIndex(List<Widget> oldPath, List<Widget> newPath)
        {
            int sharedCount = System.Math.Min(oldPath.Count, newPath.Count);
            for (int i = 0; i < sharedCount; i++)
            {
                if (!ReferenceEquals(oldPath[i], newPath[i]))
                {
                    return i;
                }
            }

            return newPath.Count > oldPath.Count ? oldPath.Count : newPath.Count;
        }
    }
}
