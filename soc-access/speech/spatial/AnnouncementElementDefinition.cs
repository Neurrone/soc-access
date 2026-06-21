using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class AnnouncementElementDefinition
    {
        public AnnouncementElementDefinition(string key, ModString label, bool defaultEnabled = true, bool defaultSuffix = true)
        {
            Key = key ?? string.Empty;
            Label = label;
            DefaultEnabled = defaultEnabled;
            DefaultSuffix = defaultSuffix;
        }

        public string Key { get; private set; }

        public ModString Label { get; private set; }

        public bool DefaultEnabled { get; private set; }

        public bool DefaultSuffix { get; private set; }
    }
}
