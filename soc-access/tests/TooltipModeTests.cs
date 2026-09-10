using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using BepInEx.Configuration;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// Which tooltips reach the focus readout. The tooltip's own length decides it - a long one is
    /// the game's troop or wielder dossier - and only the long ones answer to the player's setting.
    /// </summary>
    [TestClass]
    public sealed class TooltipModeTests
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

        private static Tooltip Of(bool isLong)
        {
            return new Tooltip(() => new List<string> { "Line" }, null, isLong: () => isLong);
        }

        [TestMethod]
        public void NoTooltipIsNoMode()
        {
            Assert.AreEqual(TooltipMode.None, GraphNodes.ModeFor(null));
        }

        [TestMethod]
        public void ALongTooltipFollowsTheSetting()
        {
            ModSettings.SetReadLongTooltips(false);
            Assert.AreEqual(TooltipMode.Indicate, GraphNodes.ModeFor(Of(true)));

            ModSettings.SetReadLongTooltips(true);
            Assert.AreEqual(TooltipMode.Announce, GraphNodes.ModeFor(Of(true)));
        }

        [TestMethod]
        public void AShortTooltipIsAnnouncedEitherWay()
        {
            ModSettings.SetReadLongTooltips(false);
            Assert.AreEqual(TooltipMode.Announce, GraphNodes.ModeFor(Of(false)));

            ModSettings.SetReadLongTooltips(true);
            Assert.AreEqual(TooltipMode.Announce, GraphNodes.ModeFor(Of(false)));
        }
    }
}
