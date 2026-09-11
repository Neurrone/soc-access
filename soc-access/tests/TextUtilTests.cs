using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>The text folding the type-ahead search matches through.</summary>
    [TestClass]
    public sealed class TextUtilTests
    {
        [TestMethod]
        public void RemoveDiacriticsFoldsAccentsAwayAndLeavesTheLetters()
        {
            Assert.AreEqual("Seance", TextUtil.RemoveDiacritics("Séance"));
            Assert.AreEqual("Uber", TextUtil.RemoveDiacritics("Über"));
        }

        /// <summary>A ligature is two letters a searcher will type separately.</summary>
        [TestMethod]
        public void RemoveDiacriticsExpandsLigatures()
        {
            Assert.AreEqual("coeur", TextUtil.RemoveDiacritics("cœur"));
            Assert.AreEqual("aegis", TextUtil.RemoveDiacritics("ægis"));
        }

        [TestMethod]
        public void RemoveDiacriticsLeavesUnaccentedTextAndNothingAlone()
        {
            Assert.AreEqual("Gold Mine", TextUtil.RemoveDiacritics("Gold Mine"));
            Assert.AreEqual(string.Empty, TextUtil.RemoveDiacritics(string.Empty));
            Assert.IsNull(TextUtil.RemoveDiacritics(null));
        }

        [TestMethod]
        public void IsBlankAnswersForNothingEmptinessAndWhitespace()
        {
            Assert.IsTrue(TextUtil.IsBlank(null));
            Assert.IsTrue(TextUtil.IsBlank(string.Empty));
            Assert.IsTrue(TextUtil.IsBlank(" \t\n "));
            Assert.IsFalse(TextUtil.IsBlank(" a "));
        }
    }
}
