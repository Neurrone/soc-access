using System.IO;
using BepInEx.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// A settings test's temporary config file: a fresh path per test, deleted afterwards, with
    /// <see cref="ModSettings"/> reset so nothing a test set leaks into the next one.
    /// </summary>
    public abstract class ModSettingsFixture
    {
        protected string ConfigPath;

        [TestInitialize]
        public void MakeATemporaryConfigPath()
        {
            ConfigPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cfg");
        }

        [TestCleanup]
        public void ResetSettings()
        {
            ModSettings.Reset();
            if (File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
            }
        }

        /// <summary>Bind the settings to that file, which is what a test does before reading one.</summary>
        protected void BindTemporaryConfig()
        {
            ModSettings.Bind(new ConfigFile(ConfigPath, saveOnInit: false));
        }
    }
}
