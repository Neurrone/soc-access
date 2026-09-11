using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>The settings that govern what the readout says on its own initiative.</summary>
    [TestClass]
    public sealed class ModSettingsReadingTests : ModSettingsFixture
    {
        [TestInitialize]
        public void BindTheConfig()
        {
            BindTemporaryConfig();
        }

        [TestMethod]
        public void LongTooltipsAreReadByDefault()
        {
            Assert.IsTrue(ModSettings.ReadLongTooltips);
        }

        [TestMethod]
        public void ReadLongTooltipsRoundTrips()
        {
            ModSettings.SetReadLongTooltips(false);
            Assert.IsFalse(ModSettings.ReadLongTooltips);

            ModSettings.SetReadLongTooltips(true);
            Assert.IsTrue(ModSettings.ReadLongTooltips);
        }

        [TestMethod]
        public void UsageHintsAreReadByDefault()
        {
            Assert.AreEqual(UsageHintReading.Always, ModSettings.ReadUsageHints);
        }

        [TestMethod]
        public void ReadUsageHintsRoundTrips()
        {
            ModSettings.SetReadUsageHints(UsageHintReading.Never);
            Assert.AreEqual(UsageHintReading.Never, ModSettings.ReadUsageHints);

            ModSettings.SetReadUsageHints(UsageHintReading.Always);
            Assert.AreEqual(UsageHintReading.Always, ModSettings.ReadUsageHints);
        }

        /// <summary>A value written by a later build reads as the default and is left where it is:
        /// the string setting exists so a third value can land without a migration.</summary>
        [TestMethod]
        public void AnUnknownUsageHintValueReadsAsAlwaysAndIsNotRewritten()
        {
            ModSettings.SetReadUsageHints("changed");
            Assert.AreEqual(UsageHintReading.Always, ModSettings.ReadUsageHints);
            Assert.IsTrue(File.ReadAllText(ConfigPath).Contains("ReadUsageHints = changed"));
        }
    }
}
