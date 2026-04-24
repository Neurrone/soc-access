using System.Collections.Generic;

namespace SongsOfConquestAccess.UI
{
    internal sealed class FocusContext
    {
        private readonly List<Widget> _lastPath = new List<Widget>();

        public string BuildAnnouncement(Widget widget)
        {
            if (widget == null)
            {
                return string.Empty;
            }

            List<Widget> newPath = BuildPath(widget);
            int divergenceIndex = FindDivergenceIndex(_lastPath, newPath);
            _lastPath.Clear();
            _lastPath.AddRange(newPath);

            List<string> parts = new List<string>();
            for (int i = divergenceIndex; i < newPath.Count; i++)
            {
                Widget context = newPath[i];
                if (context != null && context.AnnounceName)
                {
                    AddIfNotEmpty(parts, context.GetLabel());
                }
            }

            AddIfNotEmpty(parts, widget.GetFocusMessage());
            return string.Join(". ", parts.ToArray());
        }

        public void Reset()
        {
            _lastPath.Clear();
        }

        private static List<Widget> BuildPath(Widget widget)
        {
            List<Widget> path = new List<Widget>();
            Widget current = widget.Parent;
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

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }
    }
}
