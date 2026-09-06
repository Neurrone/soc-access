using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// Type-ahead: which tier a candidate falls into, and how the tiers merge into the results the user
    /// steps through.
    /// </summary>
    [TestClass]
    public class TypeAheadSearchTests
    {
        private static int Tier(string name, string typed)
        {
            int pos;
            return TypeAheadSearch.MatchTier(name.ToLowerInvariant(), typed.ToLowerInvariant(), out pos);
        }

        private static int Position(string name, string typed)
        {
            int pos;
            TypeAheadSearch.MatchTier(name.ToLowerInvariant(), typed.ToLowerInvariant(), out pos);
            return pos;
        }

        [DataTestMethod]
        [DataRow("Load Game", "load", 0)]   // start of string, whole word
        [DataRow("Load Game", "l", 1)]      // start of string, prefix
        [DataRow("Quick Load", "load", 2)]  // mid string, whole word
        [DataRow("Quick Loader", "load", 3)]// mid string, word prefix
        [DataRow("Reload", "load", 4)]      // substring anywhere
        [DataRow("Gas Pipeline", "ga pi", 5)] // space-delimited word-prefix abbreviation
        [DataRow("Load Game", "zzz", -1)]   // no match
        public void TiersRankFromWholeWordDownToAbbreviation(string name, string typed, int expected)
        {
            Assert.AreEqual(expected, Tier(name, typed));
        }

        [TestMethod]
        public void AWholeWordEndingAtACommaCountsAsAWholeWord()
        {
            Assert.AreEqual(0, Tier("Load, ready", "load"));
            Assert.AreEqual(2, Tier("Fleet, load, ready", "load"));
        }

        [TestMethod]
        public void APrefixLongerThanTheNameNeverMatches()
        {
            Assert.AreEqual(-1, Tier("Sol", "solar system"));
        }

        [TestMethod]
        public void DiacriticsAreIgnored()
        {
            Assert.AreEqual(0, Tier("Séance", "seance"));
            Assert.AreEqual(0, Tier("Œuvre", "oeuvre"));
        }

        [TestMethod]
        public void TheMatchPositionIsReported()
        {
            Assert.AreEqual(0, Position("Load Game", "load"));
            Assert.AreEqual(6, Position("Quick Load", "load"));
        }

        [TestMethod]
        public void AnAbbreviationMustStayWithinOneCommaSegment()
        {
            Assert.AreEqual(5, Tier("Gas Pipe", "ga pi"));
            Assert.AreEqual(-1, Tier("Gas, Pipe", "ga pi"));
        }

        // ---- result list ----

        // A search whose "nothing matched" feedback records -1, so the announce log reads as a sequence.
        private static TypeAheadSearch Over(List<int> announced)
        {
            TypeAheadSearch s = new TypeAheadSearch();
            s.OnNoMatch = text => announced.Add(-1);
            return s;
        }

        private static void Type(TypeAheadSearch s, string text, List<string> items, List<int> announced)
        {
            foreach (char c in text) s.AddChar(c);
            s.Search(items.Count, i => items[i], i => announced.Add(i));
        }

        [TestMethod]
        public void StrongerTiersComeFirstAndItemOrderBreaksTheTie()
        {
            List<string> items = new List<string> { "License", "Load Game", "DLC" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "l", items, announced);
            Assert.AreEqual(3, s.ResultCount);
            Assert.AreEqual(0, announced[0]); // License and Load Game are both tier 1; list order wins
            Assert.AreEqual(0, s.CurrentResultIndex);
        }

        [TestMethod]
        public void RepeatingALetterCyclesThroughAllOfItsMatches()
        {
            List<string> items = new List<string> { "License", "Load Game", "DLC" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "l", items, announced);
            Type(s, "l", items, announced); // "ll" collapses back to "l" and steps
            Type(s, "l", items, announced);
            Type(s, "l", items, announced); // wraps

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 0 }, announced);
            Assert.AreEqual("l", s.Buffer);
        }

        [TestMethod]
        public void MatchesInTheNameOutrankMatchesInTheAppendedMetadata()
        {
            List<string> items = new List<string> { "Alpha, warp drive", "Warp Beacon" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "warp", items, announced);
            Assert.AreEqual(1, announced[0]);
            Assert.AreEqual(2, s.ResultCount);
        }

        [TestMethod]
        public void NoMatchReportsTheBufferAndLeavesNoResults()
        {
            List<string> items = new List<string> { "Alpha", "Beta" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "zz", items, announced);
            Assert.AreEqual(0, s.ResultCount);
            Assert.AreEqual(-1, s.CurrentResultIndex);
            CollectionAssert.AreEqual(new[] { -1 }, announced);
            Assert.IsTrue(s.IsSearchActive);
        }

        [TestMethod]
        public void NavigateResultsWrapsInBothDirections()
        {
            List<string> items = new List<string> { "Alpha", "Alpha two", "Beta" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "alpha", items, announced);
            s.NavigateResults(-1);
            Assert.AreEqual(1, s.CurrentResultIndex);
            s.NavigateResults(1);
            Assert.AreEqual(0, s.CurrentResultIndex);
            s.JumpToLastResult();
            Assert.AreEqual(1, s.CurrentResultIndex);
            s.JumpToFirstResult();
            Assert.AreEqual(0, s.CurrentResultIndex);
        }

        [TestMethod]
        public void BackspaceAndClearResetTheBuffer()
        {
            List<string> items = new List<string> { "Alpha" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "al", items, announced);
            Assert.IsTrue(s.RemoveChar());
            Assert.AreEqual("a", s.Buffer);
            s.Clear();
            Assert.IsFalse(s.HasBuffer);
            Assert.IsFalse(s.IsSearchActive);
            Assert.IsFalse(s.RemoveChar());
        }
    }
}
