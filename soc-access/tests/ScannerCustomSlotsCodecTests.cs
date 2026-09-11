using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerCustomSlotsCodecTests
    {
        [TestMethod]
        public void ARoundTripKeepsEverySlotWhereItWas()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            ScannerCustomCategory first = new ScannerCustomCategory("Custom 1");
            first.SetSelector(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, selected: true);
            first.SetSelector(ScannerCategoryKeys.Buildings, ScannerSubcategoryKeys.Enemy, selected: true);
            first.AddKeyword("mine");
            slots.Set(0, first);
            ScannerCustomCategory third = new ScannerCustomCategory("Custom 3");
            third.AddKeyword("gold");
            slots.Set(2, third);

            ScannerCustomSlots decoded = ScannerCustomSlotsCodec.Decode(ScannerCustomSlotsCodec.Encode(slots));

            ScannerCustomCategory decodedFirst = decoded.Slot(0);
            Assert.AreEqual("Custom 1", decodedFirst.Name);
            Assert.AreEqual(2, decodedFirst.Selectors.Count);
            Assert.AreEqual(ScannerCategoryKeys.Pickups, decodedFirst.Selectors[0].CategoryKey);
            Assert.AreEqual(ScannerSubcategoryKeys.Unvisited, decodedFirst.Selectors[0].SubcategoryKey);
            Assert.AreEqual(ScannerCategoryKeys.Buildings, decodedFirst.Selectors[1].CategoryKey);
            Assert.AreEqual(ScannerSubcategoryKeys.Enemy, decodedFirst.Selectors[1].SubcategoryKey);
            CollectionAssert.AreEqual(new[] { "mine" }, (System.Collections.ICollection)decodedFirst.Keywords);
            // The empty slot in the middle stays empty rather than closing up.
            Assert.IsNull(decoded.Slot(1));
            CollectionAssert.AreEqual(new[] { "gold" }, (System.Collections.ICollection)decoded.Slot(2).Keywords);
        }

        [TestMethod]
        public void SeparatorsInPlayerTextSurviveTheRoundTrip()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            ScannerCustomCategory category = new ScannerCustomCategory("a;b|c,d:e\\f");
            category.AddKeyword("one, two");
            category.AddKeyword("three;four|five");
            slots.Set(0, category);

            ScannerCustomSlots decoded = ScannerCustomSlotsCodec.Decode(ScannerCustomSlotsCodec.Encode(slots));

            Assert.AreEqual("a;b|c,d:e\\f", decoded.Slot(0).Name);
            CollectionAssert.AreEqual(
                new[] { "one, two", "three;four|five" },
                (System.Collections.ICollection)decoded.Slot(0).Keywords);
        }

        [TestMethod]
        public void MissingOrDamagedTextDecodesToThreeEmptySlots()
        {
            Assert.IsNull(ScannerCustomSlotsCodec.Decode(null).Slot(0));
            Assert.IsNull(ScannerCustomSlotsCodec.Decode(string.Empty).Slot(0));
            Assert.IsNull(ScannerCustomSlotsCodec.Decode(";;").Slot(2));
        }
    }
}
