using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerCustomCategoryTests
    {
        [TestMethod]
        public void ABlankRenameIsRefusedSoTheCategoryKeepsASpokenName()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Custom 1");

            Assert.IsFalse(category.Rename("   "));
            Assert.AreEqual("Custom 1", category.Name);
            Assert.IsTrue(category.Rename("  My scouting  "));
            Assert.AreEqual("My scouting", category.Name);
        }

        [TestMethod]
        public void SelectorsToggleOnAndOffWithoutDuplicating()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Custom 1");

            Assert.IsTrue(category.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, selected: true));
            Assert.IsFalse(category.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, selected: true));
            Assert.IsTrue(category.HasSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited));
            Assert.AreEqual(1, category.Selectors.Count);

            Assert.IsTrue(category.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, selected: false));
            Assert.IsFalse(category.HasSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited));
            Assert.AreEqual(0, category.Selectors.Count);
        }

        [TestMethod]
        public void TheSameSubcategoryKeyUnderTwoCategoriesStaysTwoSelectors()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Custom 1");

            category.SetSelector(ScannerCategoryKeys.Buildings, ScannerSubcategoryKeys.Enemy, selected: true);
            category.SetSelector(ScannerCategoryKeys.TroopSources, ScannerSubcategoryKeys.Enemy, selected: true);

            Assert.AreEqual(2, category.Selectors.Count);
        }

        [TestMethod]
        public void ABlankOrRepeatedKeywordIsRefused()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Custom 1");

            Assert.IsTrue(category.AddKeyword("  mine  "));
            Assert.IsFalse(category.AddKeyword("MINE"));
            Assert.IsFalse(category.AddKeyword(" "));
            Assert.AreEqual(1, category.Keywords.Count);
            Assert.AreEqual("mine", category.Keywords[0]);

            Assert.IsTrue(category.RemoveKeyword("Mine"));
            Assert.AreEqual(0, category.Keywords.Count);
        }

    }
}
