using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public class SpokenLinesTests
    {
        [TestMethod]
        public void StripsRichTextTagsFromEachLine()
        {
            IList<string> lines = SpokenLines.Of(new[] { "<color=#decca8><b>+10%</b></color> Melee Resistance" });
            CollectionAssert.AreEqual(new[] { "+10% Melee Resistance" }, (List<string>)lines);
        }

        [TestMethod]
        public void SplitsOnNewlinesBeforeCollapsingWhitespace()
        {
            IList<string> lines = SpokenLines.Of(new[] { "Offence: 10\nDefence:   5\r\n\n<i>Movement</i>: 12" });
            CollectionAssert.AreEqual(new[] { "Offence: 10", "Defence: 5", "Movement: 12" }, (List<string>)lines);
        }

        [TestMethod]
        public void BreaksTheLineWhereTheGameDrewOne()
        {
            IList<string> lines = SpokenLines.Of(
                new[] { "<color=#C1AC82><hl>Essence</hl><br>Order controls essence. </color>" });
            CollectionAssert.AreEqual(new[] { "Essence", "Order controls essence." }, (List<string>)lines);
        }

        [TestMethod]
        public void DropsEmptyAndNullEntries()
        {
            IList<string> lines = SpokenLines.Of(new[] { null, "", "  ", "<hl></hl>", "Skills" });
            CollectionAssert.AreEqual(new[] { "Skills" }, (List<string>)lines);
            Assert.AreEqual(0, SpokenLines.Of(null).Count);
        }

        [TestMethod]
        public void CleanKeepsTheLinesOfOneRawString()
        {
            Assert.AreEqual(
                "Offence: 10\nDefence: 5\nMovement: 12",
                SpokenLines.Clean("<b>Offence</b>: 10\nDefence:   5\r\n\n<i>Movement</i>: 12"));
        }

        [TestMethod]
        public void CleanTrimsASingleLabel()
        {
            Assert.AreEqual("Start Campaign", SpokenLines.Clean("  <color=#decca8>Start  Campaign</color> "));
            Assert.AreEqual(string.Empty, SpokenLines.Clean(null));
            Assert.AreEqual(string.Empty, SpokenLines.Clean("   "));
        }
    }
}
