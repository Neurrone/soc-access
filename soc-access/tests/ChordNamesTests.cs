using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// How a chord is written out for a player to hear (<see cref="ChordNames"/>).
    ///
    /// What is held here is the COMPOSITION, which is the half that has no game in it: that every
    /// piece of a chord is a translated word joined by a template rather than a "+" glued on in
    /// code, that the keys the mod binds gestures to are named by the mod and everything else by the
    /// keyboard's own display name, and that a hint's (action, binding index) really does address the
    /// binding it means - Ctrl+left click being the third binding of the same action as the plain
    /// left click is the whole reason the index exists.
    /// </summary>
    [TestClass]
    public class ChordNamesTests
    {
        private Func<Key, string> _display;

        [TestInitialize]
        public void Setup()
        {
            _display = ChordNames.KeyDisplayName;
            // The game-dependent half, stubbed: no keyboard device exists in a test run.
            ChordNames.KeyDisplayName = key => key == Key.Q ? "q" : null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            ChordNames.KeyDisplayName = _display;
        }

        [TestMethod]
        public void NamesAKeyTheModBoundAGestureTo()
        {
            Assert.AreEqual("Enter", ChordNames.Of(new KeyboardBinding(Key.Enter)));
        }

        [TestMethod]
        public void JoinsEveryModifierBeforeTheKey()
        {
            Assert.AreEqual("Ctrl+Enter", ChordNames.Of(new KeyboardBinding(Key.Enter, ctrl: true)));
            Assert.AreEqual(
                "Ctrl+Shift+Alt+Space",
                ChordNames.Of(new KeyboardBinding(Key.Space, ctrl: true, shift: true, alt: true)));
        }

        [TestMethod]
        public void NamesAnUnnamedKeyByTheKeyboardsOwnDisplayName()
        {
            Assert.AreEqual("Ctrl+q", ChordNames.Of(new KeyboardBinding(Key.Q, ctrl: true)));
        }

        [TestMethod]
        public void FallsBackToTheKeyItselfWhenNothingNamesIt()
        {
            Assert.AreEqual("F5", ChordNames.Of(new KeyboardBinding(Key.F5)));
        }

        [TestMethod]
        public void RendersADisplayNameBindingAsItsCharacter()
        {
            Assert.AreEqual("\\", ChordNames.Of(new KeyboardDisplayNameBinding("\\")));
            Assert.AreEqual("Ctrl+\\", ChordNames.Of(new KeyboardDisplayNameBinding("\\", ctrl: true)));
        }

        [TestMethod]
        public void AddressesTheBindingTheIndexNames()
        {
            string action = AccessibilityActions.UiLeftClick.Key;
            Assert.AreEqual("Enter", ChordNames.Of(action, 0));
            Assert.AreEqual(
                "Ctrl+Enter",
                ChordNames.Of(action, AccessibilityActions.UiLeftClickCtrlBindingIndex));
            Assert.AreEqual(
                "Ctrl+Backslash",
                ChordNames.Of(
                    AccessibilityActions.UiRightClick.Key,
                    AccessibilityActions.UiRightClickCtrlBindingIndex));
        }

        [TestMethod]
        public void NamesNothingForAnActionOrBindingThatIsNotThere()
        {
            Assert.IsNull(ChordNames.Of("no_such_action", 0));
            Assert.IsNull(ChordNames.Of(AccessibilityActions.UiCarry.Key, 7));
            Assert.IsNull(ChordNames.Of(AccessibilityActions.UiCarry.Key, -1));
        }
    }
}
