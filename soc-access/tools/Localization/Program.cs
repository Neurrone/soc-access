using System.Text;
using System.Text.RegularExpressions;

namespace Localization;

internal static class Program
{
    private const int TemplatePluralFormCount = 3;

    private static readonly Regex ModStringRegex = new Regex(
        @"new\s+ModString\s*\(\s*(?<key>@?""(?:""""|\\.|[^""])*"")\s*,\s*(?<text>@?""(?:""""|\\.|[^""])*"")\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex ModPluralStringRegex = new Regex(
        @"new\s+ModPluralString\s*\(\s*(?<args>(?:@?""(?:""""|\\.|[^""])*""\s*,?\s*)+)\)",
        RegexOptions.Compiled);

    private static readonly Regex CSharpStringRegex = new Regex(
        @"@?""(?:""""|\\.|[^""])*""",
        RegexOptions.Compiled);

    private static readonly Regex PlaceholderRegex = new Regex(@"\{(?<index>\d+)(?:[^}]*)\}", RegexOptions.Compiled);

    public static int Main(string[] args)
    {
        if (args.Length != 1 || (args[0] != "update-pot" && args[0] != "validate"))
        {
            Console.Error.WriteLine("Usage: dotnet run --project soc-access/tools/Localization -- <update-pot|validate>");
            return 2;
        }

        string socAccessDirectory = FindSocAccessDirectory();
        string modStringsPath = Path.Combine(socAccessDirectory, "localization", "ModStrings.cs");
        string translationsDirectory = Path.Combine(socAccessDirectory, "translations");
        string potPath = Path.Combine(translationsDirectory, "strings_template.pot");

        SourceCatalog sourceStrings = ReadModStrings(modStringsPath);
        if (args[0] == "update-pot")
        {
            Directory.CreateDirectory(translationsDirectory);
            File.WriteAllText(potPath, BuildPot(sourceStrings), new UTF8Encoding(false));
            Console.WriteLine("Updated " + potPath);
            return 0;
        }

        return ValidateTranslations(sourceStrings, translationsDirectory, potPath);
    }

    private static string FindSocAccessDirectory()
    {
        string? directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, "soc-access");
            if (File.Exists(Path.Combine(candidate, "localization", "ModStrings.cs")))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory, "localization", "ModStrings.cs"))
                && File.Exists(Path.Combine(directory, "soc-access.csproj")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not find soc-access/localization/ModStrings.cs from the current directory.");
    }

    private static SourceCatalog ReadModStrings(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("ModStrings.cs was not found.", path);
        }

        string source = File.ReadAllText(path, Encoding.UTF8);
        List<ModStringEntry> entries = new List<ModStringEntry>();
        List<ModPluralStringEntry> pluralEntries = new List<ModPluralStringEntry>();
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ModStringRegex.Matches(source))
        {
            string key = DecodeCSharpString(match.Groups["key"].Value);
            string text = DecodeCSharpString(match.Groups["text"].Value);
            if (!keys.Add(key))
            {
                throw new InvalidOperationException("Duplicate ModString key in ModStrings.cs: " + key);
            }

            entries.Add(new ModStringEntry(key, text));
        }

        foreach (Match match in ModPluralStringRegex.Matches(source))
        {
            List<string> values = CSharpStringRegex.Matches(match.Groups["args"].Value)
                .Cast<Match>()
                .Select(value => DecodeCSharpString(value.Value))
                .ToList();
            if (values.Count < 2)
            {
                throw new InvalidOperationException("ModPluralString requires a key and at least one fallback form.");
            }

            string key = values[0];
            if (!keys.Add(key))
            {
                throw new InvalidOperationException("Duplicate localization key in ModStrings.cs: " + key);
            }

            pluralEntries.Add(new ModPluralStringEntry(key, values.Skip(1).ToArray()));
        }

        if (entries.Count == 0 && pluralEntries.Count == 0)
        {
            throw new InvalidOperationException("No localization entries were found in " + path);
        }

        return new SourceCatalog(entries, pluralEntries);
    }

    private static string BuildPot(SourceCatalog sourceStrings)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("msgid \"\"");
        builder.AppendLine("msgstr \"\"");
        builder.AppendLine("\"Project-Id-Version: SongsOfConquestAccess\\n\"");
        builder.AppendLine("\"Content-Type: text/plain; charset=UTF-8\\n\"");
        builder.AppendLine("\"Content-Transfer-Encoding: 8bit\\n\"");
        builder.AppendLine();

        foreach (ModStringEntry entry in sourceStrings.Strings)
        {
            builder.AppendLine("#. " + entry.Key);
            builder.AppendLine("msgctxt \"" + EscapePoString(entry.Key) + "\"");
            builder.AppendLine("msgid \"" + EscapePoString(entry.Text) + "\"");
            builder.AppendLine("msgstr \"\"");
            builder.AppendLine();
        }

        foreach (ModPluralStringEntry entry in sourceStrings.Plurals)
        {
            for (int i = 0; i < TemplatePluralFormCount; i++)
            {
                string key = entry.GetKey(i);
                builder.AppendLine("#. " + key);
                builder.AppendLine("msgctxt \"" + EscapePoString(key) + "\"");
                builder.AppendLine("msgid \"" + EscapePoString(entry.GetFallback(i)) + "\"");
                builder.AppendLine("msgstr \"\"");
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static int ValidateTranslations(
        SourceCatalog sourceStrings,
        string translationsDirectory,
        string potPath)
    {
        List<string> failures = new List<string>();

        if (File.Exists(potPath))
        {
            string expectedPot = BuildPot(sourceStrings);
            string actualPot = File.ReadAllText(potPath, Encoding.UTF8);
            if (!string.Equals(NormalizeNewlines(actualPot), NormalizeNewlines(expectedPot), StringComparison.Ordinal))
            {
                failures.Add("strings_template.pot is stale. Run update-pot.");
            }
        }
        else
        {
            failures.Add("strings_template.pot is missing. Run update-pot.");
        }

        if (!Directory.Exists(translationsDirectory))
        {
            failures.Add("Translations directory is missing: " + translationsDirectory);
            return PrintValidationResult(failures);
        }

        string[] poFiles = Directory.GetFiles(translationsDirectory, "*.po")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (poFiles.Length == 0)
        {
            failures.Add("No .po files found in " + translationsDirectory);
            return PrintValidationResult(failures);
        }

        foreach (string poFile in poFiles)
        {
            List<ModStringEntry> expectedEntries = sourceStrings.GetEntriesForLanguage(Path.GetFileNameWithoutExtension(poFile));
            Dictionary<string, ModStringEntry> sourceByKey = expectedEntries.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
            PoCatalog catalog = PoCatalog.Load(poFile);
            foreach (string duplicate in catalog.DuplicateKeys)
            {
                failures.Add(Path.GetFileName(poFile) + ": duplicate key " + duplicate);
            }

            foreach (ModStringEntry source in expectedEntries)
            {
                if (!catalog.Entries.TryGetValue(source.Key, out PoEntry? translation))
                {
                    failures.Add(Path.GetFileName(poFile) + ": missing key " + source.Key);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(translation.Value))
                {
                    failures.Add(Path.GetFileName(poFile) + ": empty translation for " + source.Key);
                }

                if (!string.Equals(source.Text, translation.Id, StringComparison.Ordinal))
                {
                    failures.Add(Path.GetFileName(poFile) + ": msgid mismatch for " + source.Key);
                }

                if (!SamePlaceholders(source.Text, translation.Value))
                {
                    failures.Add(Path.GetFileName(poFile) + ": placeholder mismatch for " + source.Key);
                }
            }

            foreach (string key in catalog.Entries.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                if (!sourceByKey.ContainsKey(key))
                {
                    failures.Add(Path.GetFileName(poFile) + ": stale key " + key);
                }
            }
        }

        return PrintValidationResult(failures);
    }

    private static int PrintValidationResult(List<string> failures)
    {
        if (failures.Count == 0)
        {
            Console.WriteLine("Localization validation passed.");
            return 0;
        }

        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return 1;
    }

    private static bool SamePlaceholders(string source, string translation)
    {
        string[] sourcePlaceholders = PlaceholderRegex.Matches(source)
            .Cast<Match>()
            .Select(match => match.Groups["index"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] translatedPlaceholders = PlaceholderRegex.Matches(translation ?? string.Empty)
            .Cast<Match>()
            .Select(match => match.Groups["index"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return sourcePlaceholders.SequenceEqual(translatedPlaceholders);
    }

    private static string DecodeCSharpString(string literal)
    {
        if (literal.StartsWith("@\"", StringComparison.Ordinal))
        {
            return literal.Substring(2, literal.Length - 3).Replace("\"\"", "\"");
        }

        string value = literal.Substring(1, literal.Length - 2);
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

    private static string EscapePoString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
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

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private sealed record ModStringEntry(string Key, string Text);

    private sealed record ModPluralStringEntry(string Key, string[] Forms)
    {
        public string GetKey(int form)
        {
            return Key + "_" + form;
        }

        public string GetFallback(int form)
        {
            if (Forms.Length == 0)
            {
                return string.Empty;
            }

            int index = form >= Forms.Length ? Forms.Length - 1 : form;
            return Forms[index] ?? string.Empty;
        }
    }

    private sealed record SourceCatalog(
        List<ModStringEntry> Strings,
        List<ModPluralStringEntry> Plurals)
    {
        public List<ModStringEntry> GetEntriesForLanguage(string languageCode)
        {
            List<ModStringEntry> entries = new List<ModStringEntry>(Strings);
            int pluralFormCount = GetPluralFormCount(languageCode);
            foreach (ModPluralStringEntry plural in Plurals)
            {
                for (int i = 0; i < pluralFormCount; i++)
                {
                    entries.Add(new ModStringEntry(plural.GetKey(i), plural.GetFallback(i)));
                }
            }

            return entries;
        }

        private static int GetPluralFormCount(string languageCode)
        {
            string code = languageCode.ToLowerInvariant();
            if (code.StartsWith("zh", StringComparison.Ordinal)
                || code.StartsWith("ja", StringComparison.Ordinal)
                || code.StartsWith("ko", StringComparison.Ordinal)
                || code.StartsWith("tr", StringComparison.Ordinal))
            {
                return 1;
            }

            if (code.StartsWith("pl", StringComparison.Ordinal)
                || code.StartsWith("ru", StringComparison.Ordinal)
                || code.StartsWith("uk", StringComparison.Ordinal))
            {
                return 3;
            }

            return 2;
        }
    }

    private sealed record PoEntry(string Key, string Id, string Value);

    private sealed class PoCatalog
    {
        public Dictionary<string, PoEntry> Entries { get; } = new Dictionary<string, PoEntry>(StringComparer.Ordinal);
        public List<string> DuplicateKeys { get; } = new List<string>();

        public static PoCatalog Load(string path)
        {
            PoCatalog catalog = new PoCatalog();
            PoEntryBuilder entry = new PoEntryBuilder();

            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    catalog.AddEntry(entry);
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

            catalog.AddEntry(entry);
            return catalog;
        }

        private void AddEntry(PoEntryBuilder entry)
        {
            string? key = !string.IsNullOrWhiteSpace(entry.Context) ? entry.Context : entry.Id;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (Entries.ContainsKey(key))
            {
                DuplicateKeys.Add(key);
                return;
            }

            Entries[key] = new PoEntry(key, entry.Id ?? string.Empty, entry.Value ?? string.Empty);
        }
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
        public string? Context;
        public string? Id;
        public string? Value;
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
