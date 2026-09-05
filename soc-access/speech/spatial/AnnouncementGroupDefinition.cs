using System.Collections.Generic;
using System.Linq;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    public sealed class AnnouncementGroupDefinition
    {
        private readonly Dictionary<string, AnnouncementElementDefinition> _elementsByKey;

        public AnnouncementGroupDefinition(string key, string configSection, ModString label, params AnnouncementElementDefinition[] elements)
        {
            Key = key ?? string.Empty;
            ConfigSection = configSection ?? string.Empty;
            Label = label;
            Elements = elements ?? new AnnouncementElementDefinition[0];
            Version = 1;
            _elementsByKey = Elements.ToDictionary(element => element.Key, element => element);
        }

        /// <summary>
        /// Bump this whenever the element set changes meaning rather than merely
        /// gaining an element. Saved order and per-element settings are thrown
        /// away and rebuilt from the defaults when the stored version does not
        /// match, because a saved order full of retired keys would silently drop
        /// the parts of the announcement that matter.
        /// </summary>
        public int Version { get; private set; }

        public AnnouncementGroupDefinition WithVersion(int version)
        {
            Version = version;
            return this;
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
