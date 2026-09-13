using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace SongsOfConquestAccess.Tests.Lint
{
    /// <summary>
    /// THE BATTLEFIELD TABLES ARE SPOKEN MOD TEXT, so the rule the .po files live under holds for
    /// them too: everything the mod authors is translated into every language the mod is translated
    /// into (AGENTS.md, Localization). The .po validator cannot see these - a description is not a
    /// <c>ModString</c>, it is authored under <c>battlefields/descriptions/&lt;lang&gt;/</c> and
    /// built by <c>build-battlefields.ps1</c> into <c>soc-access/battlefields/&lt;lang&gt;.json</c> -
    /// so this gate reads the BUILT tables, which are what ships and what the mod loads by the
    /// game's language code.
    ///
    /// English is the reference: it is what a language with no table of its own falls back to, so a
    /// translated table holding different layout keys is not a variant, it is a table that goes
    /// silent on the layouts it lacks. The placeholders are checked as a multiset because they are
    /// not decoration: <c>BattlefieldText</c> replaces each <c>{x,y}</c> with the name the ground at
    /// that cell has, so a dropped one loses a region from the sentence and an invented one names a
    /// region the sentence was not about. Word order is free - the counts are compared, never the
    /// positions.
    ///
    /// A field equal to its English word for word is the untranslated copy a half-finished pass
    /// leaves behind, and it is worth failing over even though it reads as valid text. The one
    /// exception is a field with no letters in it at all - coordinates and punctuation - which
    /// every language writes the same way.
    /// </summary>
    [TestClass]
    public class BattlefieldTableLintTests
    {
        private const string Reference = "en";

        private const string Rule =
            "A battlefield description is spoken mod text, so every language with a soc-access/translations/<code>.po has a soc-access/battlefields/<code>.json (AGENTS.md, Localization).\n"
            + "Each table holds exactly the layout keys en.json holds, all three fields non-empty, the same {x,y} placeholders as the English in each field, and no field left as the English text verbatim.";

        /// <summary>The sentence every gate closes with, in the shape the source lints use. This one
        /// has no allowlist to add a line to, so the note goes where the description is authored.</summary>
        private const string ExceptionSentence =
            "Making an exception means a why-comment beside the authored description under battlefields/descriptions/, and telling the owner in the handover before it merges. No silent exceptions.";

        /// <summary>The three fields a layout is described by, in the order the screen reads them.</summary>
        private static readonly string[] Fields = { "terrain", "attacker", "defender" };

        /// <summary>A region reference, exactly as <c>BattlefieldText</c>'s own placeholder pattern
        /// matches one - the whitespace it tolerates is tolerated here, so what this counts is what
        /// the mod would actually substitute.</summary>
        private static readonly Regex Placeholder = new Regex(@"\{\s*(\d+)\s*,\s*(\d+)\s*\}");

        [TestMethod]
        public void EveryTranslatedLanguageHasABattlefieldTable()
        {
            List<string> problems = new List<string>();
            foreach (string language in TranslatedLanguages())
            {
                if (!File.Exists(TablePath(language)))
                {
                    problems.Add(language + ": no soc-access/battlefields/" + language
                        + ".json, so this language hears the English table");
                }
            }

            Fail(problems);
        }

        [TestMethod]
        public void EveryBattlefieldTableSaysWhatTheEnglishOneSays()
        {
            Table english = Read(Reference);
            Assert.IsNotNull(
                english,
                "soc-access/battlefields/en.json is missing or is not a table; it is the reference every other table is read against.");

            List<string> problems = new List<string>();
            CheckFieldsArePresent(english, problems);

            foreach (string language in TranslatedLanguages())
            {
                Table table = Read(language);
                if (table == null)
                {
                    // An absent table is the other rule's finding; an unreadable one is this one's.
                    if (File.Exists(TablePath(language)))
                    {
                        problems.Add(language + ": soc-access/battlefields/" + language
                            + ".json is not a table");
                    }

                    continue;
                }

                Compare(english, table, problems);
            }

            Fail(problems);
        }

        /// <summary>An empty field is a silence where a sentence was meant to be, and that holds for
        /// English too.</summary>
        private static void CheckFieldsArePresent(Table table, List<string> problems)
        {
            foreach (string key in table.Keys)
            {
                foreach (string field in Fields)
                {
                    if (string.IsNullOrWhiteSpace(table.Text(key, field)))
                    {
                        problems.Add(table.Language + " | " + key + " | " + field + ": empty");
                    }
                }
            }
        }

        /// <summary>One translated table against the English one: the keys, then each field of each
        /// key they share.</summary>
        private static void Compare(Table english, Table table, List<string> problems)
        {
            foreach (string key in english.Keys)
            {
                if (!table.Has(key))
                {
                    problems.Add(table.Language + " | " + key + ": missing, en.json describes this layout");
                }
            }

            foreach (string key in table.Keys)
            {
                if (!english.Has(key))
                {
                    problems.Add(table.Language + " | " + key + ": not a layout en.json describes");
                    continue;
                }

                foreach (string field in Fields)
                {
                    string source = english.Text(key, field);
                    string translated = table.Text(key, field);
                    if (string.IsNullOrWhiteSpace(translated))
                    {
                        problems.Add(table.Language + " | " + key + " | " + field + ": empty");
                        continue;
                    }

                    string drift = PlaceholderDrift(source, translated);
                    if (drift != null)
                    {
                        problems.Add(table.Language + " | " + key + " | " + field + ": " + drift);
                    }

                    if (HasLetter(source)
                        && string.Equals(Trim(source), Trim(translated), StringComparison.Ordinal))
                    {
                        problems.Add(table.Language + " | " + key + " | " + field
                            + ": the English text, word for word");
                    }
                }
            }
        }

        /// <summary>How the two multisets differ, placeholder by placeholder, or null where they do
        /// not. Every difference is named: a translator fixing one wants to see the rest.</summary>
        private static string PlaceholderDrift(string source, string translated)
        {
            Dictionary<string, int> wanted = Placeholders(source);
            Dictionary<string, int> found = Placeholders(translated);
            SortedSet<string> all = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> entry in wanted)
            {
                all.Add(entry.Key);
            }

            foreach (KeyValuePair<string, int> entry in found)
            {
                all.Add(entry.Key);
            }

            List<string> differences = new List<string>();
            foreach (string placeholder in all)
            {
                int expected = Count(wanted, placeholder);
                int actual = Count(found, placeholder);
                if (expected != actual)
                {
                    differences.Add(placeholder + " "
                        + actual.ToString(CultureInfo.InvariantCulture) + " here, "
                        + expected.ToString(CultureInfo.InvariantCulture) + " in the English");
                }
            }

            return differences.Count == 0 ? null : string.Join("; ", differences.ToArray());
        }

        private static Dictionary<string, int> Placeholders(string text)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (text == null)
            {
                return counts;
            }

            foreach (Match match in Placeholder.Matches(text))
            {
                // The canonical spelling, so that "{1, 4}" and "{1,4}" are the one placeholder they
                // are to the mod.
                string placeholder = "{" + match.Groups[1].Value + "," + match.Groups[2].Value + "}";
                counts[placeholder] = Count(counts, placeholder) + 1;
            }

            return counts;
        }

        private static int Count(Dictionary<string, int> counts, string placeholder)
        {
            int count;
            return counts.TryGetValue(placeholder, out count) ? count : 0;
        }

        private static bool HasLetter(string text)
        {
            if (text == null)
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetter(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Trim(string text)
        {
            return text == null ? string.Empty : text.Trim();
        }

        /// <summary>The languages the mod is translated into, read off the .po files rather than a
        /// list written here: one place says which languages exist, and it is the one a new
        /// translation lands in.</summary>
        public static IList<string> TranslatedLanguages()
        {
            string directory = Path.Combine(LintSources.RepoRoot(), "soc-access", "translations");
            List<string> languages = new List<string>();
            foreach (string file in Directory.GetFiles(directory, "*.po"))
            {
                // GetFiles matches short names too, so a .pot file answers "*.po"; the extension says.
                if (string.Equals(Path.GetExtension(file), ".po", StringComparison.OrdinalIgnoreCase))
                {
                    languages.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            languages.Sort(StringComparer.Ordinal);
            Assert.AreNotEqual(0, languages.Count, "no .po files under soc-access/translations/");
            return languages;
        }

        private static string TablePath(string language)
        {
            return Path.Combine(LintSources.RepoRoot(), "soc-access", "battlefields", language + ".json");
        }

        /// <summary>One built table, or null where the file is absent or is not a table.</summary>
        private static Table Read(string language)
        {
            string path = TablePath(language);
            if (!File.Exists(path))
            {
                return null;
            }

            JObject layouts;
            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path));
                layouts = root["layouts"] as JObject;
            }
            catch (Exception)
            {
                return null;
            }

            if (layouts == null)
            {
                return null;
            }

            Table table = new Table(language);
            foreach (KeyValuePair<string, JToken> layout in layouts)
            {
                table.Add(layout.Key, layout.Value as JObject);
            }

            return table;
        }

        /// <summary>Every problem in ONE message, one line each, and the sentence closes it once -
        /// thirteen languages is thirteen lines and one sentence.</summary>
        private static void Fail(IList<string> problems)
        {
            if (problems.Count == 0)
            {
                return;
            }

            StringBuilder message = new StringBuilder(Rule);
            message.Append(Environment.NewLine);
            foreach (string problem in problems)
            {
                message.Append(Environment.NewLine + "  " + problem);
            }

            message.Append(Environment.NewLine + Environment.NewLine);
            message.Append(ExceptionSentence);
            Assert.Fail(message.ToString());
        }

        /// <summary>One language's table as this rule reads it: the layout keys in the order the
        /// file lists them, and the raw text of each field.</summary>
        private sealed class Table
        {
            private readonly List<string> _keys = new List<string>();

            private readonly Dictionary<string, JObject> _layouts =
                new Dictionary<string, JObject>(StringComparer.Ordinal);

            public Table(string language)
            {
                Language = language;
            }

            public string Language { get; private set; }

            public IList<string> Keys
            {
                get { return _keys; }
            }

            public void Add(string key, JObject layout)
            {
                if (string.IsNullOrEmpty(key) || _layouts.ContainsKey(key))
                {
                    return;
                }

                _keys.Add(key);
                _layouts[key] = layout;
            }

            public bool Has(string key)
            {
                return _layouts.ContainsKey(key);
            }

            public string Text(string key, string field)
            {
                JObject layout;
                if (!_layouts.TryGetValue(key, out layout) || layout == null)
                {
                    return null;
                }

                JToken value = layout[field];
                return value == null || value.Type != JTokenType.String ? null : value.Value<string>();
            }
        }
    }
}
