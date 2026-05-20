using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Screens;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatTroopCycleTests
    {
        [TestMethod]
        public void MoveFirstPressFocusesFirstTroopWithoutWrap()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            CombatTroopCycleResult result = cycle.Move(new[] { 10, 20, 30 }, 1);

            Assert.IsTrue(result.Moved);
            Assert.AreEqual(10, result.TroopId);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void MoveSecondPressAdvancesWithoutWrap()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            cycle.Move(new[] { 10, 20, 30 }, 1);
            CombatTroopCycleResult result = cycle.Move(new[] { 10, 20, 30 }, 1);

            Assert.AreEqual(20, result.TroopId);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void MoveNextFromLastWrapsToFirst()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            cycle.Move(new[] { 10, 20 }, 1);
            cycle.Move(new[] { 10, 20 }, 1);
            CombatTroopCycleResult result = cycle.Move(new[] { 10, 20 }, 1);

            Assert.AreEqual(10, result.TroopId);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void MovePreviousFromFirstWrapsToLast()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            cycle.Move(new[] { 10, 20, 30 }, 1);
            CombatTroopCycleResult result = cycle.Move(new[] { 10, 20, 30 }, -1);

            Assert.AreEqual(30, result.TroopId);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void SingleTroopFirstPressDoesNotWrap()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            CombatTroopCycleResult result = cycle.Move(new[] { 10 }, 1);

            Assert.AreEqual(10, result.TroopId);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void SingleTroopSecondPressWraps()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            cycle.Move(new[] { 10 }, 1);
            CombatTroopCycleResult result = cycle.Move(new[] { 10 }, 1);

            Assert.AreEqual(10, result.TroopId);
            Assert.IsTrue(result.Wrapped);
        }

        [TestMethod]
        public void AnchorFirstMakesNextMoveToSecondTroop()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            cycle.AnchorFirst(new[] { 10, 20, 30 });
            CombatTroopCycleResult result = cycle.Move(new[] { 10, 20, 30 }, 1);

            Assert.AreEqual(20, result.TroopId);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void MissingAnchorRecoversToFirstTroopWithoutWrap()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            cycle.Move(new[] { 10, 20, 30 }, 1);
            cycle.Move(new[] { 10, 20, 30 }, 1);
            CombatTroopCycleResult result = cycle.Move(new[] { 10, 30 }, 1);

            Assert.AreEqual(10, result.TroopId);
            Assert.IsFalse(result.Wrapped);
        }

        [TestMethod]
        public void ResetMakesNextMoveActLikeFirstPress()
        {
            CombatTroopCycle cycle = new CombatTroopCycle();

            cycle.Move(new[] { 10, 20 }, 1);
            cycle.Reset();
            CombatTroopCycleResult result = cycle.Move(new[] { 10, 20 }, 1);

            Assert.AreEqual(10, result.TroopId);
            Assert.IsFalse(result.Wrapped);
        }
    }
}
