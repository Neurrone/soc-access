using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// How the mod runs a handful of things together into one spoken list. The joining words are
    /// ModStrings so a language can put them where it needs them, which is why nothing here
    /// concatenates a separator itself.
    /// </summary>
    [TestClass]
    public sealed class ModTextJoinListTests
    {
        [TestMethod]
        public void NothingToJoinIsTheEmptyString()
        {
            Assert.AreEqual(string.Empty, ModText.JoinList(null));
            Assert.AreEqual(string.Empty, ModText.JoinList(new string[0]));
        }

        [TestMethod]
        public void OneThingIsSaidOnItsOwn()
        {
            Assert.AreEqual("Gold", ModText.JoinList(new[] { "Gold" }));
        }

        /// <summary>Two take the pair string, which is not the same wording as the end of a longer
        /// list: English says "Gold and Wood" but "Gold, Stone, and Wood".</summary>
        [TestMethod]
        public void TwoThingsTakeThePairWording()
        {
            Assert.AreEqual("Gold and Wood", ModText.JoinList(new[] { "Gold", "Wood" }));
        }

        [TestMethod]
        public void ThreeOrMoreRunTogetherAndEndWithTheFinalWording()
        {
            Assert.AreEqual("Gold, Stone, and Wood", ModText.JoinList(new[] { "Gold", "Stone", "Wood" }));
            Assert.AreEqual("Gold, Stone, Ore, and Wood", ModText.JoinList(new[] { "Gold", "Stone", "Ore", "Wood" }));
        }

        /// <summary>A part the caller had nothing to say for is dropped before the count is taken,
        /// so it cannot turn a pair into a list or leave a gap in one.</summary>
        [TestMethod]
        public void BlankPartsAreDroppedBeforeTheWordingIsChosen()
        {
            Assert.AreEqual("Gold and Wood", ModText.JoinList(new[] { "Gold", null, "   ", "Wood" }));
            Assert.AreEqual("Gold", ModText.JoinList(new[] { string.Empty, "Gold" }));
            Assert.AreEqual(string.Empty, ModText.JoinList(new[] { string.Empty, "  " }));
        }

        /// <summary>The separator overload folds the parts with one two-placeholder string all the
        /// way through, with no special wording for the last pair.</summary>
        [TestMethod]
        public void TheSeparatorOverloadUsesTheGivenWordingBetweenEveryPair()
        {
            IReadOnlyList<string> parts = new[] { "Gold", "Stone", "Wood" };

            Assert.AreEqual("Gold, Stone, Wood", ModText.JoinList(ModStrings.Common.ListSeparator, parts));
            Assert.AreEqual("Gold and Stone and Wood", ModText.JoinList(ModStrings.Common.ListPair, parts));
            Assert.AreEqual(string.Empty, ModText.JoinList(ModStrings.Common.ListSeparator, new string[0]));
        }
    }
}
