using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The .po reader the mod resolves all thirteen translations through, and the one the
    /// localization tool validates them with. What it decides here is what a player hears.
    /// </summary>
    [TestClass]
    public sealed class PoTranslationCatalogTests
    {
        private readonly List<string> _files = new List<string>();

        [TestCleanup]
        public void DeleteTemporaryFiles()
        {
            for (int i = 0; i < _files.Count; i++)
            {
                if (File.Exists(_files[i]))
                {
                    File.Delete(_files[i]);
                }
            }
        }

        /// <summary>The msgctxt is the key the mod looks a ModString up by; the msgid beside it is
        /// only the English it was translated from.</summary>
        [TestMethod]
        public void AnEntryIsFoundByItsContextRatherThanItsEnglish()
        {
            PoTranslationCatalog catalog = Load(
                "msgctxt \"Common.ListPair\"",
                "msgid \"{0} and {1}\"",
                "msgstr \"{0} und {1}\"");

            string text;
            Assert.IsTrue(catalog.TryGetText("Common.ListPair", out text));
            Assert.AreEqual("{0} und {1}", text);
            Assert.IsFalse(catalog.TryGetText("{0} and {1}", out text));
        }

        /// <summary>A file with no contexts is still readable: the English is then the key.</summary>
        [TestMethod]
        public void AnEntryWithoutAContextIsFoundByItsEnglish()
        {
            PoTranslationCatalog catalog = Load(
                "msgid \"Impassable\"",
                "msgstr \"Impraticable\"");

            string text;
            Assert.IsTrue(catalog.TryGetText("Impassable", out text));
            Assert.AreEqual("Impraticable", text);
        }

        /// <summary>The shape a translator's editor writes long strings in: an empty first line and
        /// the text on the continuation lines, which join with nothing between them.</summary>
        [TestMethod]
        public void ContinuationLinesAreJoinedOntoTheFieldTheyFollow()
        {
            PoTranslationCatalog catalog = Load(
                "msgctxt \"Screens.Long\"",
                "msgid \"\"",
                "\"first \"",
                "\"second\"",
                "msgstr \"\"",
                "\"erste \"",
                "\"zweite\"");

            string text;
            Assert.IsTrue(catalog.TryGetText("Screens.Long", out text));
            Assert.AreEqual("erste zweite", text);
            Assert.AreEqual("first second", catalog.Entries["Screens.Long"].Id);
        }

        [TestMethod]
        public void EscapeSequencesBecomeTheCharactersTheyStandFor()
        {
            PoTranslationCatalog catalog = Load(
                "msgctxt \"Screens.Escapes\"",
                "msgid \"x\"",
                "msgstr \"a\\nb\\tc\\\"d\\\\e\"");

            string text;
            Assert.IsTrue(catalog.TryGetText("Screens.Escapes", out text));
            Assert.AreEqual("a\nb\tc\"d\\e", text);
        }

        /// <summary>A key declared twice resolves to the LAST of them, and the repeat is reported so
        /// the validator can refuse the file.</summary>
        [TestMethod]
        public void ARepeatedKeyKeepsItsLastTranslationAndIsReported()
        {
            PoTranslationCatalog catalog = Load(
                "msgctxt \"Common.Quantity\"",
                "msgid \"quantity\"",
                "msgstr \"erste\"",
                string.Empty,
                "msgctxt \"Common.Quantity\"",
                "msgid \"quantity\"",
                "msgstr \"zweite\"");

            string text;
            Assert.IsTrue(catalog.TryGetText("Common.Quantity", out text));
            Assert.AreEqual("zweite", text);
            CollectionAssert.AreEqual(new[] { "Common.Quantity" }, new List<string>(catalog.DuplicateKeys));
        }

        /// <summary>An untranslated entry is not a translation: the caller falls back to the English
        /// source rather than speaking a blank.</summary>
        [TestMethod]
        public void AWhitespaceOnlyTranslationIsNotATranslation()
        {
            PoTranslationCatalog catalog = Load(
                "msgctxt \"Common.Quantity\"",
                "msgid \"quantity\"",
                "msgstr \"   \"");

            string text;
            Assert.IsFalse(catalog.TryGetText("Common.Quantity", out text));
            Assert.AreEqual(string.Empty, text);
            Assert.IsTrue(catalog.Entries.ContainsKey("Common.Quantity"));
        }

        /// <summary>Comment lines are the translator's notes and the tool's own markers.</summary>
        [TestMethod]
        public void CommentLinesAreSkipped()
        {
            PoTranslationCatalog catalog = Load(
                "# a translator note",
                "#: ModStrings.cs",
                "msgctxt \"Common.Quantity\"",
                "msgid \"quantity\"",
                "msgstr \"Menge\"");

            string text;
            Assert.IsTrue(catalog.TryGetText("Common.Quantity", out text));
            Assert.AreEqual("Menge", text);
        }

        private PoTranslationCatalog Load(params string[] lines)
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".po");
            _files.Add(path);
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return PoTranslationCatalog.Load(path);
        }
    }
}
