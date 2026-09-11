using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SongsOfConquestAccess.Localization
{
    /// <summary>One entry of a .po file: the key it is found by (the msgctxt, or the msgid where
    /// there is none), the source text it was translated from, and the translation itself.</summary>
    public sealed class PoEntry
    {
        public PoEntry(string key, string id, string value)
        {
            Key = key;
            Id = id;
            Value = value;
        }

        public string Key { get; private set; }
        public string Id { get; private set; }
        public string Value { get; private set; }
    }

    /// <summary>
    /// The one .po reader. The mod reads a translation file through it at runtime and the
    /// localization tool validates the same files through it (linked into
    /// <c>tools/Localization</c>), so what the validator checks is exactly what the game resolves.
    /// </summary>
    public sealed class PoTranslationCatalog
    {
        private readonly Dictionary<string, PoEntry> _entries;
        private readonly List<string> _duplicateKeys;

        private PoTranslationCatalog(Dictionary<string, PoEntry> entries, List<string> duplicateKeys)
        {
            _entries = entries;
            _duplicateKeys = duplicateKeys;
        }

        /// <summary>Every entry the file holds, by key. A key that appears twice is kept at its LAST
        /// occurrence, which is the one a reader resolves, and is also listed in
        /// <see cref="DuplicateKeys"/>.</summary>
        public IReadOnlyDictionary<string, PoEntry> Entries
        {
            get { return _entries; }
        }

        /// <summary>Every key the file declared more than once, in the order the repeats were met.
        /// </summary>
        public IReadOnlyList<string> DuplicateKeys
        {
            get { return _duplicateKeys; }
        }

        public static PoTranslationCatalog Load(string path)
        {
            Dictionary<string, PoEntry> entries = new Dictionary<string, PoEntry>(StringComparer.Ordinal);
            List<string> duplicateKeys = new List<string>();
            PoEntryBuilder entry = new PoEntryBuilder();

            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    AddEntry(entries, duplicateKeys, entry);
                    entry.Reset();
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("msgctxt ", StringComparison.Ordinal))
                {
                    entry.ActiveField = PoField.Context;
                    entry.Context = ParsePoString(line.Substring("msgctxt ".Length));
                    continue;
                }

                if (line.StartsWith("msgid ", StringComparison.Ordinal))
                {
                    entry.ActiveField = PoField.Id;
                    entry.Id = ParsePoString(line.Substring("msgid ".Length));
                    continue;
                }

                if (line.StartsWith("msgstr ", StringComparison.Ordinal))
                {
                    entry.ActiveField = PoField.String;
                    entry.Value = ParsePoString(line.Substring("msgstr ".Length));
                    continue;
                }

                if (line.StartsWith("\"", StringComparison.Ordinal))
                {
                    entry.Append(ParsePoString(line));
                }
            }

            AddEntry(entries, duplicateKeys, entry);
            return new PoTranslationCatalog(entries, duplicateKeys);
        }

        /// <summary>The translation for a key, where the file holds one that is not blank. A blank
        /// msgstr is not a translation: the caller falls back to the English source.</summary>
        public bool TryGetText(string key, out string text)
        {
            text = string.Empty;
            if (key == null || !_entries.ContainsKey(key))
            {
                return false;
            }

            PoEntry entry = _entries[key];
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                return false;
            }

            text = entry.Value;
            return true;
        }

        private static void AddEntry(Dictionary<string, PoEntry> entries, List<string> duplicateKeys, PoEntryBuilder entry)
        {
            if (entry == null)
            {
                return;
            }

            string key = !string.IsNullOrWhiteSpace(entry.Context) ? entry.Context : entry.Id;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (entries.ContainsKey(key))
            {
                duplicateKeys.Add(key);
            }

            entries[key] = new PoEntry(key, entry.Id ?? string.Empty, entry.Value ?? string.Empty);
        }

        private static string ParsePoString(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2);
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(c);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case '"':
                        builder.Append('"');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    default:
                        builder.Append(value[i]);
                        break;
                }
            }

            return builder.ToString();
        }

        private enum PoField
        {
            None,
            Context,
            Id,
            String
        }

        private sealed class PoEntryBuilder
        {
            public string Context = string.Empty;
            public string Id = string.Empty;
            public string Value = string.Empty;
            public PoField ActiveField;

            public void Append(string value)
            {
                switch (ActiveField)
                {
                    case PoField.Context:
                        Context += value;
                        break;
                    case PoField.Id:
                        Id += value;
                        break;
                    case PoField.String:
                        Value += value;
                        break;
                }
            }

            public void Reset()
            {
                Context = string.Empty;
                Id = string.Empty;
                Value = string.Empty;
                ActiveField = PoField.None;
            }
        }
    }
}
