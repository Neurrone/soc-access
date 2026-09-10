using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using TMPro;
using UnityEngine.InputSystem;

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

        /// <summary>A letter typed while an editor holds or awaits the keyboard is never a search,
        /// whatever the screen says about itself: the one queued as Enter came up would otherwise be
        /// searched with after the field had the keyboard, and the landing would take its selection.</summary>
        [TestMethod]
        public void TypingWhileAnEditorIsOwnedNeverStartsASearch()
        {
            GraphNavigator navigator = new GraphNavigator();
            navigator.Attach(new Searchable());
            Assert.IsTrue(navigator.TakesTypedKey(Key.A), "the screen searches when nothing holds the keyboard");

            GameTextEditor editor = new GameTextEditor();
            try
            {
                editor.Request(DetachedField());
                Assert.IsTrue(GameTextEditor.Owned, "asking for the editor is owning the keyboard");
                Assert.IsFalse(navigator.TakesTypedKey(Key.A), "the letter is the field's, not the search's");
                navigator.TypeText("a");
                Assert.IsFalse(navigator.TypeAheadTick(), "a letter already queued is dropped rather than searched with");
                Assert.IsFalse(navigator.SearchIsActive);
            }
            finally
            {
                editor.Abandon();
            }

            Assert.IsFalse(GameTextEditor.Owned, "letting go is letting go of the ownership too");
            Assert.IsTrue(navigator.TakesTypedKey(Key.A), "with no editor owned the same letter is a search again");
        }

        /// <summary>A TMP text box with no Unity behind it. The editor asks one thing of a field
        /// before it remembers it - that the field is there - and Unity answers that from the native
        /// pointer, so a stand-in needs a pointer and nothing else; the handover, which is where a
        /// real box would be needed, is never reached here.</summary>
        private static TMP_InputField DetachedField()
        {
            TMP_InputField field = (TMP_InputField)FormatterServices.GetUninitializedObject(typeof(TMP_InputField));
            typeof(UnityEngine.Object)
                .GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(field, new IntPtr(1));
            return field;
        }

        private sealed class Searchable : GraphScreen
        {
            private readonly object _subject = new object();

            public override string Key
            {
                get { return "searchable"; }
            }

            public override int Layer
            {
                get { return 1; }
            }

            public override bool IsActive()
            {
                return true;
            }

            public override void Build(GraphBuilder builder)
            {
                builder.BeginStop("stop");
                builder.AddItem(new SyntheticNode(ControlId.For(_subject, "alpha"), Graphs.Vt("Alpha")));
            }
        }
    }
}
