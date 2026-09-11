using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// A custom category is picked out of the cycle by the name it is spoken
    /// under, so these tests pin which proposed names the rename prompt refuses.
    /// Slot 1 is "Trade run" and slot 2 "My scouting"; the name being judged is
    /// slot 1's own.
    /// </summary>
    [TestClass]
    public sealed class ScannerCustomCategoryNameConflictTests
    {
        [TestMethod]
        public void ABuiltInCategoryNameIsTakenWhateverTheCasing()
        {
            Assert.IsTrue(Exists("pickups", 1));
            Assert.IsTrue(Exists("PICKUPS", 1));
            Assert.IsTrue(Exists("  Wielders  ", 1));
        }

        [TestMethod]
        public void AnotherSlotsNameIsTakenWhateverTheCasing()
        {
            Assert.IsTrue(Exists("my scouting", 1));
            Assert.IsTrue(Exists("MY SCOUTING", 1));
        }

        [TestMethod]
        public void ASlotKeepingItsOwnNameIsNotAConflict()
        {
            Assert.IsFalse(Exists("Trade run", 1));
            Assert.IsFalse(Exists("TRADE RUN", 1));
        }

        [TestMethod]
        public void AFreshNameIsFree()
        {
            Assert.IsFalse(Exists("Harbours", 1));
        }

        [TestMethod]
        public void ABlankNameIsLeftToTheRenameItselfToRefuse()
        {
            Assert.IsFalse(Exists("   ", 1));
        }

        [TestMethod]
        public void BuiltInNamesComeFromTheTaxonomyLabels()
        {
            IReadOnlyList<string> names = ScannerCustomCategoryNameConflict.BuiltInNames(Taxonomy());

            CollectionAssert.AreEqual(new[] { "Pickups", "Wielders" }, new List<string>(names));
        }

        private static bool Exists(string name, int slot)
        {
            return ScannerCustomCategoryNameConflict.Exists(name, Taxonomy(), Slots(), slot);
        }

        private static ScannerTaxonomy Taxonomy()
        {
            return new ScannerTaxonomy(
                "adventure",
                new ScannerCategoryDefinition("pickups", () => "Pickups"),
                new ScannerCategoryDefinition("wielders", () => "Wielders"));
        }

        private static ScannerCustomSlots Slots()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            slots.Set(1, new ScannerCustomCategory("Trade run"));
            slots.Set(2, new ScannerCustomCategory("My scouting"));
            return slots;
        }
    }
}
