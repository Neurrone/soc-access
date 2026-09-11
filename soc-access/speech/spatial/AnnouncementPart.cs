using System.Collections.Generic;

namespace SongsOfConquestAccess.Speech.Spatial
{
    public sealed class AnnouncementPart
    {
        public AnnouncementPart(string key, string text)
        {
            Key = key ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string Key { get; private set; }

        public string Text { get; private set; }

        /// <summary>Adds a part for text there is something to say; blank text is no part at all.</summary>
        public static void AddIfPresent(List<AnnouncementPart> parts, string key, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            parts.Add(new AnnouncementPart(key, text));
        }
    }
}
