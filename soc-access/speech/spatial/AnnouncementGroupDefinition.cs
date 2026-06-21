using System.Collections.Generic;
using System.Linq;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class AnnouncementGroupDefinition
    {
        private readonly Dictionary<string, AnnouncementElementDefinition> _elementsByKey;

        public AnnouncementGroupDefinition(string key, string configSection, ModString label, params AnnouncementElementDefinition[] elements)
        {
            Key = key ?? string.Empty;
            ConfigSection = configSection ?? string.Empty;
            Label = label;
            Elements = elements ?? new AnnouncementElementDefinition[0];
            _elementsByKey = Elements.ToDictionary(element => element.Key, element => element);
        }

        public string Key { get; private set; }

        public string ConfigSection { get; private set; }

        public ModString Label { get; private set; }

        public IReadOnlyList<AnnouncementElementDefinition> Elements { get; private set; }

        public AnnouncementElementDefinition GetElement(string key)
        {
            AnnouncementElementDefinition element;
            return !string.IsNullOrWhiteSpace(key) && _elementsByKey.TryGetValue(key, out element)
                ? element
                : null;
        }

        public string DefaultOrderCsv
        {
            get { return string.Join(",", Elements.Select(element => element.Key).ToArray()); }
        }
    }
}
