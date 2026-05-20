namespace SongsOfConquestAccess.Localization
{
    internal readonly struct ModString
    {
        public ModString(string key, string text)
        {
            Key = key;
            Text = text;
        }

        public string Key { get; }
        public string Text { get; }
    }
}
