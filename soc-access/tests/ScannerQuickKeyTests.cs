using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// Which custom category answers to which key, and how that survives being
    /// written out and read back.
    /// </summary>
    [TestClass]
    public sealed class ScannerQuickKeyTests
    {
        [TestMethod]
        public void KeysFillInOrderUntilThereAreNoneLeft()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();

            Assert.AreEqual(ScannerQuickKey.Comma, Fill(list));
            Assert.AreEqual(ScannerQuickKey.Period, Fill(list));
            Assert.AreEqual(ScannerQuickKey.Slash, Fill(list));
            Assert.AreEqual(ScannerQuickKey.None, Fill(list));
        }

        [TestMethod]
        public void TakingAHeldKeyClearsItFromWhoeverHadIt()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            ScannerCustomCategory first = Add(list);
            ScannerCustomCategory second = Add(list);

            Assert.IsTrue(list.SetQuickKey(second.Id, ScannerQuickKey.Comma));

            Assert.AreEqual(ScannerQuickKey.Comma, second.QuickKey);
            Assert.AreEqual(ScannerQuickKey.None, first.QuickKey);
            Assert.AreSame(second, list.GetByQuickKey(ScannerQuickKey.Comma));
        }

        /// <summary>
        /// The key a deletion frees is left alone rather than handed on, so a
        /// category the player never touched cannot change key underneath them.
        /// </summary>
        [TestMethod]
        public void AFreedKeyIsNotHandedToAnyoneUntilItIsAskedFor()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            ScannerCustomCategory first = Add(list);
            ScannerCustomCategory second = Add(list);
            list.Remove(first.Id);

            Assert.AreEqual(ScannerQuickKey.Period, second.QuickKey);
            Assert.IsNull(list.GetByQuickKey(ScannerQuickKey.Comma));
            Assert.AreEqual(ScannerQuickKey.Comma, list.FirstFreeQuickKey());
        }

        [TestMethod]
        public void ClearingAKeyLeavesItFree()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            ScannerCustomCategory category = Add(list);

            Assert.IsTrue(list.SetQuickKey(category.Id, ScannerQuickKey.None));

            Assert.AreEqual(ScannerQuickKey.None, category.QuickKey);
            Assert.AreEqual(ScannerQuickKey.Comma, list.FirstFreeQuickKey());
        }

        [TestMethod]
        public void AKeySurvivesTheRoundTrip()
        {
            ScannerCustomCategoryList list = new ScannerCustomCategoryList();
            ScannerCustomCategory withKey = Add(list);
            ScannerCustomCategory withoutKey = list.Add(position => "Custom " + position);

            ScannerCustomCategoryList decoded = ScannerCustomCategoryCodec.Decode(ScannerCustomCategoryCodec.Encode(list));

            Assert.AreEqual(ScannerQuickKey.Comma, decoded.Get(withKey.Id).QuickKey);
            Assert.AreEqual(ScannerQuickKey.None, decoded.Get(withoutKey.Id).QuickKey);
        }

        [TestMethod]
        public void TextWrittenBeforeKeysExistedStillLoadsWithNoKeySet()
        {
            ScannerCustomCategoryList decoded = ScannerCustomCategoryCodec.Decode("3;1|Threats||mine;2|Pickups||");

            Assert.AreEqual(3, decoded.NextId);
            Assert.AreEqual(2, decoded.Categories.Count);
            Assert.AreEqual("Threats", decoded.Categories[0].Name);
            Assert.AreEqual(ScannerQuickKey.None, decoded.Categories[0].QuickKey);
            CollectionAssert.AreEqual(new[] { "mine" }, (System.Collections.ICollection)decoded.Categories[0].Keywords);
        }

        private static ScannerCustomCategory Add(ScannerCustomCategoryList list)
        {
            ScannerCustomCategory category = list.Add(position => "Custom " + position);
            category.SetQuickKey(list.FirstFreeQuickKey());
            return category;
        }

        private static ScannerQuickKey Fill(ScannerCustomCategoryList list)
        {
            return Add(list).QuickKey;
        }
    }
}
