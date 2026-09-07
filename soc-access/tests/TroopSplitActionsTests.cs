using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The game's quick-split chords as the mod knows them
    /// (<see cref="AccessibilityActions.TroopSplits"/>): Ctrl+1 to Ctrl+0 splitting one to ten troops
    /// off, in the game's own order, where Ctrl+0 is the TENTH rather than a zeroth.
    ///
    /// What is held here is the half with no game in it - which chord means how many, and that a key
    /// nobody bound to a split answers zero, which is the gate that keeps the mod from claiming
    /// anything else.
    /// </summary>
    [TestClass]
    public class TroopSplitActionsTests
    {
        [TestMethod]
        public void EachChordSplitsOffAsManyTroopsAsItsDigit()
        {
            Assert.AreEqual(10, AccessibilityActions.TroopSplits.Length);
            for (int i = 0; i < AccessibilityActions.TroopSplits.Length; i++)
            {
                Assert.AreEqual(i + 1, AccessibilityActions.TroopSplitSize(AccessibilityActions.TroopSplits[i].Key));
            }
        }

        [TestMethod]
        public void CtrlZeroIsTheTenth()
        {
            SongsOfConquestAccess.Input.InputAction tenth = AccessibilityActions.TroopSplits[9];
            Assert.AreEqual(10, AccessibilityActions.TroopSplitSize(tenth.Key));
            KeyboardBinding binding = tenth.Bindings[0] as KeyboardBinding;
            Assert.IsNotNull(binding);
            Assert.AreEqual(Key.Digit0, binding.Key);
            Assert.IsTrue(binding.Ctrl);
        }

        [TestMethod]
        public void AnythingElseIsNotASplit()
        {
            Assert.AreEqual(0, AccessibilityActions.TroopSplitSize(AccessibilityActions.UiCarry.Key));
            Assert.AreEqual(0, AccessibilityActions.TroopSplitSize("troop_split_11"));
            Assert.AreEqual(0, AccessibilityActions.TroopSplitSize(null));
        }
    }
}
