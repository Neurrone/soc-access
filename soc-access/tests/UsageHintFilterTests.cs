using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The player's say over whether a control's USAGE HINTS are spoken. The hints stay in the review
    /// buffer either way - that is what makes the setting safe to turn off.
    /// </summary>
    [TestClass]
    public sealed class UsageHintFilterTests
    {
        private static readonly ModString HintDisband = new ModString("hint.disband", "{0} disbands");

        private string _configPath;

        [TestInitialize]
        public void Setup()
        {
            GraphAnnouncer.Reset();
            NodeHints.Reset();
            NodeHints.Chord = (action, index) => action == "ui.contextual" && index == 0 ? "Backslash" : null;
            GraphAnnouncer.PartFilter = UsageHints.Speaks;
            _configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cfg");
            ModSettings.Bind(new ConfigFile(_configPath, saveOnInit: false));
        }

        [TestCleanup]
        public void Cleanup()
        {
            GraphAnnouncer.Reset();
            NodeHints.Reset();
            ModSettings.Reset();
            if (File.Exists(_configPath))
            {
                File.Delete(_configPath);
            }
        }

        private static NodeVtable Hinted()
        {
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    Part("Sheng Yi", AnnouncementKinds.Label),
                },
            };
            NodeHints.Add(vtable, HintDisband, "ui.contextual");
            return vtable;
        }

        private static string Readout(NodeVtable vtable)
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("t"), vtable));
            return GraphAnnouncer.LeafText(Node(b.Build(), "t"));
        }

        [TestMethod]
        public void AlwaysSpeaksTheHintAndKeepsItInTheBuffer()
        {
            ModSettings.SetReadUsageHints(UsageHintReading.Always);
            NodeVtable vtable = Hinted();

            Assert.AreEqual("Sheng Yi, Backslash disbands", Readout(vtable));
            List<string> lines = Buffer(Hinted());
            Assert.AreEqual("Backslash disbands", lines[lines.Count - 1]);
        }

        [TestMethod]
        public void NeverDropsTheHintFromTheReadoutOnly()
        {
            ModSettings.SetReadUsageHints(UsageHintReading.Never);
            NodeVtable vtable = Hinted();

            Assert.AreEqual("Sheng Yi", Readout(vtable));
            List<string> lines = Buffer(Hinted());
            Assert.AreEqual("Backslash disbands", lines[lines.Count - 1]);
        }

        [TestMethod]
        public void ANonHintPartIsUntouched()
        {
            ModSettings.SetReadUsageHints(UsageHintReading.Never);
            Assert.IsTrue(UsageHints.Speaks(null, Part("Sheng Yi", AnnouncementKinds.Label)));
            Assert.IsTrue(UsageHints.Speaks(null, Part("1 of 2", AnnouncementKinds.Position)));
            Assert.IsTrue(UsageHints.Speaks(null, null));
        }
    }
}
