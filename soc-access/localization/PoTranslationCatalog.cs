using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SongsOfConquestAccess.Localization
{
    public sealed class PoTranslationCatalog
    {
        private readonly Dictionary<string, string> _entries;

        private PoTranslationCatalog(Dictionary<string, string> entries)
        {
            _entries = entries;
        }

        public static PoTranslationCatalog Load(string path)
        {
            Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);
            PoEntryBuilder entry = new PoEntryBuilder();

            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    AddEntry(entries, entry);
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

            AddEntry(entries, entry);
            return new PoTranslationCatalog(entries);
        }

        public bool TryGetText(string key, out string text)
        {
            return _entries.TryGetValue(key, out text) && !string.IsNullOrWhiteSpace(text);
        }

        private static void AddEntry(Dictionary<string, string> entries, PoEntryBuilder entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
            {
                return;
            }

            string key = !string.IsNullOrWhiteSpace(entry.Context) ? entry.Context : entry.Id;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            entries[key] = entry.Value;
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
            public string Context;
            public string Id;
            public string Value;
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
                Context = null;
                Id = null;
                Value = null;
                ActiveField = PoField.None;
            }
        }
    }
}
