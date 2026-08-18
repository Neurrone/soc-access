using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerCustomCategoryCodecTests
    {
        [TestMethod]
        public void ARoundTripKeepsEveryCategoryIntact()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            ScannerCustomCategory first = list.Add(position => "Custom " + position);
            first.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, selected: true);
            first.SetSelector(ScannerCategoryKeys.Buildings, ScannerSubcategoryKeys.Enemy, selected: true);
            first.AddKeyword("mine");
            ScannerCustomCategory second = list.Add(position => "Custom " + position);
            second.AddKeyword("gold");

            ScannerCustomCategoryList decoded = ScannerCustomCategoryCodec.Decode(ScannerCustomCategoryCodec.Encode(list));

            Assert.AreEqual(2, decoded.Categories.Count);
            Assert.AreEqual(list.NextId, decoded.NextId);
            ScannerCustomCategory decodedFirst = decoded.Get(first.Id);
            Assert.AreEqual("Custom 1", decodedFirst.Name);
            Assert.AreEqual(2, decodedFirst.Selectors.Count);
            Assert.AreEqual(ScannerCategoryKeys.Pickups, decodedFirst.Selectors[0].CategoryKey);
            Assert.AreEqual(ScannerSubcategoryKeys.Unvisited, decodedFirst.Selectors[0].SubcategoryKey);
            Assert.AreEqual(ScannerCategoryKeys.Buildings, decodedFirst.Selectors[1].CategoryKey);
            Assert.AreEqual(ScannerSubcategoryKeys.Enemy, decodedFirst.Selectors[1].SubcategoryKey);
            CollectionAssert.AreEqual(new[] { "mine" }, (System.Collections.ICollection)decodedFirst.Keywords);
            CollectionAssert.AreEqual(new[] { "gold" }, (System.Collections.ICollection)decoded.Get(second.Id).Keywords);
        }

        [TestMethod]
        public void SeparatorsInPlayerTextSurviveTheRoundTrip()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            ScannerCustomCategory category = list.Add(position => "a;b|c,d:e\\f");
            category.AddKeyword("one, two");
            category.AddKeyword("three;four|five");

            ScannerCustomCategoryList decoded = ScannerCustomCategoryCodec.Decode(ScannerCustomCategoryCodec.Encode(list));
            ScannerCustomCategory decodedCategory = decoded.Get(category.Id);

            Assert.AreEqual("a;b|c,d:e\\f", decodedCategory.Name);
            CollectionAssert.AreEqual(
                new[] { "one, two", "three;four|five" },
                (System.Collections.ICollection)decodedCategory.Keywords);
        }

        [TestMethod]
        public void AnEmptyCategorySurvivesSoTheSettingsListStillShowsIt()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            ScannerCustomCategory category = list.Add(position => "Custom " + position);

            ScannerCustomCategoryList decoded = ScannerCustomCategoryCodec.Decode(ScannerCustomCategoryCodec.Encode(list));

            Assert.AreEqual(1, decoded.Categories.Count);
            Assert.AreEqual(0, decoded.Get(category.Id).Selectors.Count);
            Assert.AreEqual(0, decoded.Get(category.Id).Keywords.Count);
        }

        [TestMethod]
        public void MissingOrDamagedTextDecodesToAnEmptyList()
        {
            Assert.AreEqual(0, ScannerCustomCategoryCodec.Decode(null).Categories.Count);
            Assert.AreEqual(0, ScannerCustomCategoryCodec.Decode(string.Empty).Categories.Count);
            Assert.AreEqual(0, ScannerCustomCategoryCodec.Decode("3;not-a-number|Custom 1||").Categories.Count);
        }
    }
}
