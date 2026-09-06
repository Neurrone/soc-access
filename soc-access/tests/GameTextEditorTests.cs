using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// How an edit that has ended is told from the outside: the text the player leaves behind.
    ///
    /// The game announces nothing about the way out of one of its text fields, and Escape and Enter
    /// both simply drop the keyboard, so the only evidence is the text. TMP puts the pre-edit text
    /// back when its own Escape ends the edit, which is what makes an unchanged text a cancel rather
    /// than a commit of the same words.
    /// </summary>
    [TestClass]
    public class GameTextEditorTests
    {
        [TestMethod]
        public void ChangedTextIsACommit()
        {
            Assert.IsTrue(GameTextEditor.Committed("Neurrone", "Neurrone2"));
        }

        [TestMethod]
        public void UnchangedTextIsACancel()
        {
            Assert.IsFalse(GameTextEditor.Committed("Neurrone", "Neurrone"));
        }

        [TestMethod]
        public void AnEmptyBoxAndAMissingOneAreTheSameText()
        {
            Assert.IsFalse(GameTextEditor.Committed(null, string.Empty));
            Assert.IsFalse(GameTextEditor.Committed(string.Empty, null));
        }

        [TestMethod]
        public void TypingIntoAnEmptyBoxIsACommit()
        {
            Assert.IsTrue(GameTextEditor.Committed(string.Empty, "ABCDE"));
        }

        [TestMethod]
        public void ClearingABoxIsACommit()
        {
            Assert.IsTrue(GameTextEditor.Committed("ABCDE", string.Empty));
        }

        [TestMethod]
        public void CaseIsAChange()
        {
            Assert.IsTrue(GameTextEditor.Committed("test", "Test"));
        }
    }
}
