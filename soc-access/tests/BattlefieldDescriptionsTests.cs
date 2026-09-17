using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Battlefields;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The table <c>build-battlefields.ps1</c> ships and the mod reads: the layout key is the
    /// game's own "&lt;LevelType&gt;/&lt;PathName&gt;", a language with no table of its own falls
    /// back to English, and a file that is not a table answers nothing rather than throwing at
    /// whatever asked for a description.
    /// </summary>
    [TestClass]
    public sealed class BattlefieldDescriptionsTests
    {
        private const string Table = @"{
  ""version"": 1,
  ""language"": ""en"",
  ""layouts"": {
    ""Battle_Hills/Hills3"": {
      ""terrain"": ""Open ground with a ridge."",
      ""attacker"": ""Spawns down the left edge."",
      ""defender"": ""Spawns down the right edge.""
    }
  }
}";

        private string _directory;

        [TestInitialize]
        public void Setup()
        {
            _directory = Path.Combine(Path.GetTempPath(), "soc-access-battlefield-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (!string.IsNullOrWhiteSpace(_directory) && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [TestMethod]
        public void ReadsALayoutByItsGameKey()
        {
            BattlefieldDescriptionTable table = BattlefieldDescriptions.Parse(Table);

            Assert.AreEqual("en", table.Language);
            Assert.AreEqual(1, table.Count);

            BattlefieldDescription description;
            Assert.IsTrue(table.TryGet("Battle_Hills/Hills3", out description));
            Assert.AreEqual("Open ground with a ridge.", description.Terrain);
            Assert.AreEqual("Spawns down the left edge.", description.Attacker);
            Assert.AreEqual("Spawns down the right edge.", description.Defender);

            Assert.IsFalse(table.TryGet("Battle_Hills/Hills4", out description));
            Assert.IsNull(description);
        }

        /// <summary>A map whose authored type disagrees with the folder it was dumped from (the
        /// campaign battlefields say "Adventure") is still found by its path, which is unique.</summary>
        [TestMethod]
        public void ALayoutWhoseTypeDisagreesIsFoundByItsPath()
        {
            BattlefieldDescriptionTable table = BattlefieldDescriptions.Parse(Table);

            BattlefieldDescription description;
            Assert.IsTrue(table.TryGet("Adventure/Hills3", out description));
            Assert.AreEqual("Open ground with a ridge.", description.Terrain);

            Assert.IsFalse(table.TryGet("Hills3", out description));
            Assert.IsFalse(table.TryGet("Adventure/Hills4", out description));
        }

        [TestMethod]
        public void ALanguageWithNoTableOfItsOwnReadsTheEnglishOne()
        {
            File.WriteAllText(Path.Combine(_directory, "en.json"), Table);

            BattlefieldDescriptionTable fallback = BattlefieldDescriptions.Load(new[] { _directory }, "de");
            Assert.AreEqual("en", fallback.Language);
            Assert.AreEqual(1, fallback.Count);

            File.WriteAllText(Path.Combine(_directory, "de.json"), Table.Replace(@"""language"": ""en""", @"""language"": ""de"""));
            Assert.AreEqual("de", BattlefieldDescriptions.Load(new[] { _directory }, "de").Language);
        }

        [TestMethod]
        public void AFileThatIsNotATableAnswersNothing()
        {
            File.WriteAllText(Path.Combine(_directory, "en.json"), "{ \"layouts\": [ this is not json");

            Assert.IsNull(BattlefieldDescriptions.Load(new[] { _directory }, "en"));
            Assert.IsNull(BattlefieldDescriptions.Load(new[] { _directory }, "fr"));
            Assert.IsNull(BattlefieldDescriptions.Load(new[] { Path.Combine(_directory, "gone") }, "en"));
            Assert.IsNull(BattlefieldDescriptions.Parse("[]"));
        }
    }
}
