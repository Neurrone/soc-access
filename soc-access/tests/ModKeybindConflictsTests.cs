using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The half of the conflict layer with no game in it (<see cref="ModKeybindConflicts"/>): a mod
    /// binding and the game's own <c>ToDisplayString</c> text reduced to one canonical chord so they
    /// can be compared. The display strings here are verbatim from the game's
    /// GetAllOverrideableActions output (single keys, " + " composites with full-word modifiers,
    /// " | " between separate bindings).
    /// </summary>
    [TestClass]
    public class ModKeybindConflictsTests
    {
        [TestMethod]
        public void MatchesASingleKey()
        {
            Assert.IsTrue(ModKeybindConflicts.Matches(
                ModKeybindConflicts.ChordOf(new KeyboardBinding(Key.Q)), "Q"));
            Assert.IsFalse(ModKeybindConflicts.Matches(
                ModKeybindConflicts.ChordOf(new KeyboardBinding(Key.Q)), "E"));
        }

        [TestMethod]
        public void MatchesACtrlComposite()
        {
            Assert.IsTrue(ModKeybindConflicts.Matches(
                ModKeybindConflicts.ChordOf(new KeyboardBinding(Key.Tab, ctrl: true)), "Control + Tab"));
            // The same key without the modifier is a different chord.
            Assert.IsFalse(ModKeybindConflicts.Matches(
                ModKeybindConflicts.ChordOf(new KeyboardBinding(Key.Tab)), "Control + Tab"));
        }

        [TestMethod]
        public void MatchesATwoModifierComposite()
        {
            Assert.IsTrue(ModKeybindConflicts.Matches(
                ModKeybindConflicts.ChordOf(new KeyboardBinding(Key.Z, ctrl: true, shift: true)),
                "Shift + Control + Z"));
        }

        [TestMethod]
        public void MatchesThePlainAlternativeOfAMultiBinding()
        {
            // The game draws SplitTroopSize1 as "1 | Numpad 1"; the mod's Ctrl-free digit meets the
            // plain alternative.
            Assert.IsTrue(ModKeybindConflicts.Matches(
                ModKeybindConflicts.ChordOf(new KeyboardBinding(Key.Digit1)), "1 | Numpad 1"));
        }

        [TestMethod]
        public void ALoneModifierKeyIsTheKeyNotAModifier()
        {
            // ToggleInfoMode is drawn "Left Alt": the alt key pressed alone, which is not the same as
            // Alt+something.
            Assert.IsFalse(ModKeybindConflicts.Matches(
                ModKeybindConflicts.ChordOf(new KeyboardBinding(Key.A, alt: true)), "Left Alt"));
        }

        [TestMethod]
        public void ADisplayNameBindingChordsToItsCharacter()
        {
            ModKeybindConflicts.Chord chord = ModKeybindConflicts.ChordOf(new KeyboardDisplayNameBinding("\\"));
            Assert.IsNotNull(chord);
            Assert.AreEqual("\\", chord.KeyToken);
        }
    }
}
