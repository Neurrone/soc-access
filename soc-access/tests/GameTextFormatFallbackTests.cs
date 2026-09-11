using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The last step of every localized string the mod speaks: the source text, translated or
    /// English, with the caller's values put into its placeholders.
    /// </summary>
    [TestClass]
    public sealed class GameTextFormatFallbackTests
    {
        [TestMethod]
        public void PlaceholdersAreFilledFromTheArguments()
        {
            Assert.AreEqual("3 Gold", GameText.FormatFallback("{0} {1}", 3, "Gold"));
        }

        [TestMethod]
        public void TextWithoutArgumentsIsLeftExactlyAsItIs()
        {
            Assert.AreEqual("{0} {1}", GameText.FormatFallback("{0} {1}"));
            Assert.AreEqual("Impassable", GameText.FormatFallback("Impassable", new object[0]));
        }

        /// <summary>A source string whose braces do not parse is spoken as written rather than
        /// throwing on the speech path.</summary>
        [TestMethod]
        public void AMalformedFormatIsSpokenAsWritten()
        {
            Assert.AreEqual("{0 Gold", GameText.FormatFallback("{0 Gold", 3));
            Assert.AreEqual("{2} of {0}", GameText.FormatFallback("{2} of {0}", 3));
        }

        [TestMethod]
        public void NothingToSayIsTheEmptyString()
        {
            Assert.AreEqual(string.Empty, GameText.FormatFallback(null, 3));
            Assert.AreEqual(string.Empty, GameText.FormatFallback(string.Empty));
        }
    }
}
