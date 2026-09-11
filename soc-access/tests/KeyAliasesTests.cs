using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The numpad twins (<see cref="KeyAliases"/>): Enter and the digits match either key of the
    /// pair, everything else only itself.
    /// </summary>
    [TestClass]
    public class KeyAliasesTests
    {
        [TestMethod]
        public void NumpadEnterAndDigitsFoldOntoTheMainBlock()
        {
            Assert.AreEqual(Key.Enter, KeyAliases.Canonical(Key.NumpadEnter));
            Assert.AreEqual(Key.Enter, KeyAliases.Canonical(Key.Enter));
            Assert.AreEqual(Key.Digit0, KeyAliases.Canonical(Key.Numpad0));
            Assert.AreEqual(Key.Digit9, KeyAliases.Canonical(Key.Numpad9));
            Assert.AreEqual(Key.Digit5, KeyAliases.Canonical(Key.Digit5));
        }

        [TestMethod]
        public void OtherKeysStayThemselves()
        {
            Assert.AreEqual(Key.Tab, KeyAliases.Canonical(Key.Tab));
            Assert.AreEqual(Key.NumpadPlus, KeyAliases.Canonical(Key.NumpadPlus));
            Assert.AreEqual(Key.Backslash, KeyAliases.Canonical(Key.Backslash));
        }
    }
}
