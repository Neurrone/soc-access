using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerTextMatchTests
    {
        [TestMethod]
        public void RanksWholeWordStartAheadOfPrefixStart()
        {
            Assert.AreEqual(0, ScannerTextMatch.TierForLabel("Gold mine", "gold"));
            Assert.AreEqual(1, ScannerTextMatch.TierForLabel("Goldmine", "gold"));
        }

        [TestMethod]
        public void RanksLaterWholeWordAheadOfLaterPrefix()
        {
            Assert.AreEqual(2, ScannerTextMatch.TierForLabel("Ancient mine", "mine"));
            Assert.AreEqual(3, ScannerTextMatch.TierForLabel("Ancient mineshaft", "mine"));
        }

        [TestMethod]
        public void RanksInteriorSubstringBelowWordMatches()
        {
            Assert.AreEqual(4, ScannerTextMatch.TierForLabel("Stronghold", "ngh"));
        }

        [TestMethod]
        public void RanksOrderedWordPrefixTokensLast()
        {
            Assert.AreEqual(5, ScannerTextMatch.TierForLabel("Ancient Stone Circle", "anc cir"));
        }

        [TestMethod]
        public void RejectsOutOfOrderTokens()
        {
            Assert.AreEqual(ScannerTextMatch.NoMatch, ScannerTextMatch.TierForLabel("Ancient Stone Circle", "cir anc"));
        }

        [TestMethod]
        public void TreatsCommaAsAWordBoundary()
        {
            Assert.AreEqual(2, ScannerTextMatch.TierForLabel("Militia, spawn point", "spawn"));
        }

        [TestMethod]
        public void MatchingIsCaseInsensitive()
        {
            Assert.IsTrue(ScannerTextMatch.Matches("Gold Mine", ScannerTextMatch.NormalizeQuery("  GOLD  ")));
        }

        [TestMethod]
        public void RejectsQueriesLongerThanTheLabel()
        {
            Assert.AreEqual(ScannerTextMatch.NoMatch, ScannerTextMatch.TierForLabel("Ore", "ore mine"));
        }

        /// <summary>
        /// The pre-normalized overload is there so a caller asking about one
        /// label many times over lowercases it once. It has to be the same
        /// question, blank labels and all.
        /// </summary>
        [TestMethod]
        public void PreNormalizedMatchingAsksTheSameQuestion()
        {
            string query = ScannerTextMatch.NormalizeQuery("GOLD");

            Assert.IsTrue(ScannerTextMatch.MatchesNormalized(ScannerTextMatch.NormalizeLabel("Gold Mine"), query));
            Assert.AreEqual(
                ScannerTextMatch.Matches("Ore Mine", query),
                ScannerTextMatch.MatchesNormalized(ScannerTextMatch.NormalizeLabel("Ore Mine"), query));
            Assert.AreEqual(
                ScannerTextMatch.Matches(null, query),
                ScannerTextMatch.MatchesNormalized(ScannerTextMatch.NormalizeLabel(null), query));
        }

        [TestMethod]
        public void NormalizeQueryRejectsBlankInput()
        {
            Assert.IsNull(ScannerTextMatch.NormalizeQuery(null));
            Assert.IsNull(ScannerTextMatch.NormalizeQuery("   "));
            Assert.AreEqual("gold", ScannerTextMatch.NormalizeQuery(" Gold "));
        }
    }
}
