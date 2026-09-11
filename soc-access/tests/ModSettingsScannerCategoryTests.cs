using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ModSettingsScannerCategoryTests
    {
        private const string Taxonomy = "adventure";

        private string _configPath;

        [TestInitialize]
        public void MakeATemporaryConfigPath()
        {
            _configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cfg");
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

        /// <summary>
        /// The slot editor edits a category in place, so Cancel has to undo a name, a set of
        /// subcategories and a list of keywords at once. The snapshot is the stored form, which
        /// already says all three.
        /// </summary>
        [TestMethod]
        public void SnapshotAndRestorePutTheWholeSlotBack()
        {
            ModSettings.Bind(new ConfigFile(_configPath, saveOnInit: false));
            Assert.IsNotNull(ModSettings.AddScannerCustomCategory(Taxonomy, 1, "Custom 2"));
            ModSettings.RenameScannerCustomCategory(Taxonomy, 1, "Explorer");
            ModSettings.AddScannerCustomCategoryKeyword(Taxonomy, 1, "mine");
            ModSettings.SetScannerCustomCategorySelector(Taxonomy, 1, "pickups", "unvisited", true);
            string snapshot = ModSettings.SnapshotScannerCustomCategories(Taxonomy);

            ModSettings.RenameScannerCustomCategory(Taxonomy, 1, "Something else");
            ModSettings.RemoveScannerCustomCategoryKeyword(Taxonomy, 1, "mine");
            ModSettings.SetScannerCustomCategorySelector(Taxonomy, 1, "pickups", "unvisited", false);

            Assert.IsTrue(ModSettings.RestoreScannerCustomCategories(Taxonomy, snapshot));

            ScannerCustomCategory restored = ModSettings.GetScannerCustomCategory(Taxonomy, 1);
            Assert.IsNotNull(restored);
            Assert.AreEqual("Explorer", restored.Name);
            Assert.IsTrue(restored.HasSelector("pickups", "unvisited"));
            CollectionAssert.AreEqual(new List<string> { "mine" }, new List<string>(restored.Keywords));
        }

        /// <summary>
        /// Clearing a slot empties it and leaves it there, so the key that walks it keeps answering
        /// and the slot can be filled again.
        /// </summary>
        [TestMethod]
        public void ClearingASlotEmptiesItAndLeavesItFillable()
        {
            ModSettings.Bind(new ConfigFile(_configPath, saveOnInit: false));
            ModSettings.AddScannerCustomCategory(Taxonomy, 0, "Custom 1");

            Assert.IsTrue(ModSettings.ClearScannerCustomCategory(Taxonomy, 0));

            Assert.IsNull(ModSettings.GetScannerCustomCategory(Taxonomy, 0));
            Assert.IsFalse(ModSettings.ClearScannerCustomCategory(Taxonomy, 0));
            Assert.IsNotNull(ModSettings.AddScannerCustomCategory(Taxonomy, 0, "Custom 1"));
        }
    }
}
