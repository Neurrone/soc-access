using System.IO;
using BepInEx.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>The settings that govern what the readout says on its own initiative.</summary>
    [TestClass]
    public sealed class ModSettingsReadingTests
    {
        private string _configPath;

        [TestInitialize]
        public void BindTemporaryConfig()
        {
            _configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cfg");
            ModSettings.Bind(new ConfigFile(_configPath, saveOnInit: false));
        }

        [TestCleanup]
        public void ResetSettings()
        {
            ModSettings.Reset();
            if (File.Exists(_configPath))
            {
                File.Delete(_configPath);
            }
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
    }
}
