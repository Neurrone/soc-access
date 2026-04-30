using System.Collections.Generic;

namespace SongsOfConquestAccess
{
    internal static class StoryMapSuppression
    {
        private static readonly HashSet<object> ActiveSources = new HashSet<object>();

        public static bool IsActive
        {
            get { return ActiveSources.Count > 0; }
        }

        public static void Activate(object source)
        {
            if (source != null)
            {
                ActiveSources.Add(source);
            }
        }

        public static void Clear(object source)
        {
            if (source != null)
            {
                ActiveSources.Remove(source);
            }
        }

        public static void Reset()
        {
            ActiveSources.Clear();
        }
    }
}
