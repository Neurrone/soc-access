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
    }
}
