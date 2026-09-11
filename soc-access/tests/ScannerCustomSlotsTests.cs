using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The three fixed slots: a category is whichever number holds it, and
    /// emptying one leaves the slot where it was.
    /// </summary>
    [TestClass]
    public sealed class ScannerCustomSlotsTests
    {
        [TestMethod]
        public void ACategoryStaysInTheSlotItWasPutIn()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            ScannerCustomCategory category = new ScannerCustomCategory("Trade run");

            Assert.IsTrue(slots.Set(2, category));

            Assert.IsNull(slots.Slot(0));
            Assert.IsNull(slots.Slot(1));
            Assert.AreSame(category, slots.Slot(2));
            Assert.IsNull(slots.Slot(ScannerCustomSlots.Count));
        }

        /// <summary>
        /// Clearing is the delete, and the slot is still there afterwards, so
        /// the key that walks it keeps answering and nothing is renumbered.
        /// </summary>
        [TestMethod]
        public void ClearingASlotEmptiesItWithoutMovingTheOthers()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            slots.Set(0, new ScannerCustomCategory("First"));
            slots.Set(1, new ScannerCustomCategory("Second"));

            Assert.IsTrue(slots.Clear(0));

            Assert.IsNull(slots.Slot(0));
            Assert.AreEqual("Second", slots.Slot(1).Name);
            Assert.AreEqual(0, slots.FirstEmpty());
        }

        /// <summary>
        /// The name is what the category cycle says, so a slot holding a
        /// nameless category would be one the player cannot hear.
        /// </summary>
        [TestMethod]
        public void ANamelessCategoryIsRefusedASlot()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();

            Assert.IsFalse(slots.Set(0, new ScannerCustomCategory("   ")));
            Assert.IsNull(slots.Slot(0));
        }

        [TestMethod]
        public void AnotherSlotsNameIsTakenWhateverTheCasing()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            slots.Set(1, new ScannerCustomCategory("Trade run"));

            Assert.IsTrue(slots.NameTaken("  TRADE RUN  ", 0));
            Assert.IsFalse(slots.NameTaken("Trade run", 1));
            Assert.IsFalse(slots.NameTaken("Scouting", 0));
        }
    }
}
