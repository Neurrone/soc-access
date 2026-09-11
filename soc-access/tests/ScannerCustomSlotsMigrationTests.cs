using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// Moving what an older build saved as a list onto the three numbered slots.
    /// The key a category answered to is what it was reached by, so it is what
    /// decides the number it keeps.
    /// </summary>
    [TestClass]
    public sealed class ScannerCustomSlotsMigrationTests
    {
        [TestMethod]
        public void AKeyedCategoryKeepsTheSlotItsKeyNowNames()
        {
            IReadOnlyList<string> dropped;
            ScannerCustomSlots slots = ScannerCustomSlotsMigration.Migrate(
                new[] { Saved("Threats", "slash"), Saved("Pickups", "comma") },
                out dropped);

            Assert.AreEqual("Pickups", slots.Slot(0).Name);
            Assert.IsNull(slots.Slot(1));
            Assert.AreEqual("Threats", slots.Slot(2).Name);
            Assert.AreEqual(0, dropped.Count);
        }

        [TestMethod]
        public void CategoriesWithNoKeyFillWhatIsLeftInTheOrderTheyWereSavedIn()
        {
            IReadOnlyList<string> dropped;
            ScannerCustomSlots slots = ScannerCustomSlotsMigration.Migrate(
                new[] { Saved("First", string.Empty), Saved("Keyed", "period"), Saved("Second", string.Empty) },
                out dropped);

            Assert.AreEqual("First", slots.Slot(0).Name);
            Assert.AreEqual("Keyed", slots.Slot(1).Name);
            Assert.AreEqual("Second", slots.Slot(2).Name);
            Assert.AreEqual(0, dropped.Count);
        }

        /// <summary>
        /// A player who had more than three loses the ones that do not fit, and
        /// they are named on the way out because nothing else will ever mention
        /// them again.
        /// </summary>
        [TestMethod]
        public void WhatDoesNotFitIsDroppedByName()
        {
            IReadOnlyList<string> dropped;
            ScannerCustomSlots slots = ScannerCustomSlotsMigration.Migrate(
                new[]
                {
                    Saved("First", string.Empty),
                    Saved("Second", string.Empty),
                    Saved("Third", string.Empty),
                    Saved("Fourth", string.Empty)
                },
                out dropped);

            Assert.AreEqual("Third", slots.Slot(2).Name);
            CollectionAssert.AreEqual(new[] { "Fourth" }, new List<string>(dropped));
        }

        private static ScannerSavedCategory Saved(string name, string quickKeyToken)
        {
            return new ScannerSavedCategory(new ScannerCustomCategory(name), quickKeyToken);
        }
    }
}
