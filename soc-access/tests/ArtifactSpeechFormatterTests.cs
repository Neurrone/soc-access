using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// An artifact's rarity is drawn as the colour of its name, which a screen reader cannot see.
    /// These pin the colours the mod turns back into words and the name it builds from them.
    ///
    /// The <c>TryFormatName</c> overloads are not here: they take the game's <c>ArtifactDetails</c>,
    /// whose <c>IDetails</c> interface carries default implementations that the .NET Framework
    /// 4.7.2 test host cannot load.
    /// </summary>
    [TestClass]
    public sealed class ArtifactSpeechFormatterTests
    {
        [TestMethod]
        public void EveryPowerLevelColourHasItsWord()
        {
            Assert.AreEqual("grey", ArtifactSpeechFormatter.GetRarityLabel(Rgba(0xD4, 0xD4, 0xD4, 0xC2)));
            Assert.AreEqual("green", ArtifactSpeechFormatter.GetRarityLabel(Rgba(0x4C, 0xA4, 0x1D, 0xFF)));
            Assert.AreEqual("blue", ArtifactSpeechFormatter.GetRarityLabel(Rgba(0x32, 0x7F, 0xF8, 0xFF)));
            Assert.AreEqual("violet", ArtifactSpeechFormatter.GetRarityLabel(Rgba(0xD2, 0x45, 0xE9, 0xFF)));
            Assert.AreEqual("orange", ArtifactSpeechFormatter.GetRarityLabel(Rgba(0xEF, 0x7D, 0x21, 0xFF)));
        }

        /// <summary>A colour the game draws for something other than rarity says nothing, so the
        /// name is spoken on its own rather than with a made-up rank.</summary>
        [TestMethod]
        public void AColourThatIsNotARarityHasNoWord()
        {
            Assert.AreEqual(string.Empty, ArtifactSpeechFormatter.GetRarityLabel(Rgba(0x00, 0x00, 0x00, 0xFF)));
            Assert.AreEqual(
                string.Empty,
                ArtifactSpeechFormatter.GetRarityLabel((ILocalizationHandler)null, Rgba(0x00, 0x00, 0x00, 0xFF)));
        }

        [TestMethod]
        public void FormatNameReadsTheNameWithItsRarity()
        {
            Assert.AreEqual(
                "Ring of Haste (blue)",
                ArtifactSpeechFormatter.FormatName("Ring of Haste", Rgba(0x32, 0x7F, 0xF8, 0xFF)));
        }

        /// <summary>Where the colour names no rarity there is nothing to add, and a nameless artifact
        /// is left as it came rather than spoken as a lone colour.</summary>
        [TestMethod]
        public void FormatNameLeavesTheNameAloneWithNothingToAdd()
        {
            Assert.AreEqual(
                "Ring of Haste",
                ArtifactSpeechFormatter.FormatName("Ring of Haste", Rgba(0x00, 0x00, 0x00, 0xFF)));
            Assert.AreEqual(string.Empty, ArtifactSpeechFormatter.FormatName(string.Empty, Rgba(0x32, 0x7F, 0xF8, 0xFF)));
            Assert.IsNull(ArtifactSpeechFormatter.FormatName(null, Rgba(0x32, 0x7F, 0xF8, 0xFF)));
        }

        private static Color Rgba(byte r, byte g, byte b, byte a)
        {
            return new Color32(r, g, b, a);
        }
    }
}
