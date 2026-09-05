namespace SongsOfConquestAccess.Localization
{
    public readonly struct ModPluralString
    {
        public ModPluralString(string key, params string[] forms)
        {
            Key = key;
            Forms = forms ?? new string[0];
        }

        public string Key { get; }
        public string[] Forms { get; }

        public string GetFallback(int count)
        {
            if (Forms == null || Forms.Length == 0)
            {
                return string.Empty;
            }

            int form = count == 1 ? 0 : 1;
            if (form >= Forms.Length)
            {
                form = Forms.Length - 1;
            }

            return Forms[form] ?? string.Empty;
        }
    }
}
