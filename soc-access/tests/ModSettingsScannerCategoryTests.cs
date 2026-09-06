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

        /// <summary>
        /// The category dialogs edit a category in place, so Cancel has to undo a name, a key, a set
        /// of subcategories and a list of keywords at once. The snapshot is the stored form, which
        /// already says all four.
        /// </summary>
        [TestMethod]
        public void SnapshotAndRestorePutTheWholeCategoryBack()
        {
            ScannerCustomCategory category = ModSettings.AddScannerCustomCategory(
                Taxonomy,
                position => "Custom " + position);
            Assert.IsNotNull(category);
            int id = category.Id;
            ModSettings.RenameScannerCustomCategory(Taxonomy, id, "Explorer");
            ModSettings.AddScannerCustomCategoryKeyword(Taxonomy, id, "mine");
            ModSettings.SetScannerCustomCategorySelector(Taxonomy, id, "pickups", "unvisited", true);
            string snapshot = ModSettings.SnapshotScannerCustomCategories(Taxonomy);

            ModSettings.RenameScannerCustomCategory(Taxonomy, id, "Something else");
            ModSettings.RemoveScannerCustomCategoryKeyword(Taxonomy, id, "mine");
            ModSettings.SetScannerCustomCategorySelector(Taxonomy, id, "pickups", "unvisited", false);
            ModSettings.SetScannerCustomCategoryQuickKey(Taxonomy, id, ScannerQuickKey.Slash);

            Assert.IsTrue(ModSettings.RestoreScannerCustomCategories(Taxonomy, snapshot));

            ScannerCustomCategory restored = ModSettings.GetScannerCustomCategory(Taxonomy, id);
            Assert.IsNotNull(restored);
            Assert.AreEqual("Explorer", restored.Name);
            Assert.IsTrue(restored.HasSelector("pickups", "unvisited"));
            CollectionAssert.AreEqual(new List<string> { "mine" }, new List<string>(restored.Keywords));
        }
    }
}
