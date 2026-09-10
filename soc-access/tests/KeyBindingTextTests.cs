using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The one pure piece of the Controls key-binding table: the chip reads the hotkey the game
    /// draws, or the mod's "not bound" where the game draws an empty chip.
    /// </summary>
    [TestClass]
    public sealed class KeyBindingTextTests
    {
        [TestMethod]
        public void ABoundKeyReadsAsItsHotkey()
        {
            Assert.AreEqual("W", KeyBindingText.Display("W", "not bound"));
        }

        [TestMethod]
        public void AnEmptyChipReadsAsNotBound()
        {
            Assert.AreEqual("not bound", KeyBindingText.Display(null, "not bound"));
            Assert.AreEqual("not bound", KeyBindingText.Display(string.Empty, "not bound"));
            Assert.AreEqual("not bound", KeyBindingText.Display("   ", "not bound"));
        }
    }
}
