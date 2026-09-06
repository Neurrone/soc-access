using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Localization;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The USAGE HINTS a control ends its review buffer with.
    ///
    /// Three things are held here and nowhere else: that a hint's chord comes from the INJECTED
    /// renderer rather than from anything written into the sentence (which is what makes a rebind
    /// re-word every hint), that the hint names a binding INDEX and not just an action (the map's
    /// off-lane move is the second chord of the same action as the ordinary move), and that the lines
    /// land at the very END of the buffer, after everything the control itself has to say.
    /// </summary>
    [TestClass]
    public class NodeHintTests
    {
        [TestInitialize]
        public void Setup()
        {
            GraphAnnouncer.Reset();
            NodeHints.Reset();
        }

        [TestCleanup]
        public void Cleanup()
        {
            GraphAnnouncer.Reset();
            NodeHints.Reset();
        }

        // The hint sentences these tests exercise, as the templates a screen would declare.
        private static readonly ModString HintQueueFirst =
            new ModString("hint.queue-first", "{0} to queue it first");

        private static readonly ModString HintMoveFleetHere =
            new ModString("hint.move-fleet-here", "{0} to move the fleet here");

        private static readonly ModString HintFreeMovement =
            new ModString("hint.free-movement", "{0} to force free movement");

        private static readonly ModString HintDismiss =
            new ModString("hint.dismiss", "{0} to dismiss");

        /// <summary>A stand-in for the real formatter: two chords on one action, so the binding index
        /// is visible in the output.</summary>
        private static void InstallFakeFormatter()
        {
            NodeHints.Chord = (action, index) =>
                action == "ui.contextual"
                    ? (index == 0 ? "Backslash" : index == 1 ? "Ctrl+Backslash" : null)
                    : action == "ui.alternate" && index == 0
                        ? "Ctrl+Shift+Enter"
                        : null;
        }

        private static NodeVtable Control()
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    Part("Dusay", AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection>
                {
                    NodeSection.Buffer(() => new List<string> { "Two planets" }),
                },
            };
        }

        [TestMethod]
        public void AHintReadsAsItsTemplateWithTheRenderedChordInIt()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintMoveFleetHere, "ui.contextual");

            CollectionAssert.AreEqual(
                new[] { "Dusay", "Two planets", "Backslash to move the fleet here" },
                Buffer(vtable)
            );
        }

        /// <summary>The hints are the LAST lines: content first, keyboard afterwards.</summary>
        [TestMethod]
        public void HintsComeAfterEverythingTheControlHasToSay()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintMoveFleetHere, "ui.contextual");
            NodeHints.Add(vtable, HintFreeMovement, "ui.contextual", 1);

            List<string> lines = Buffer(vtable);
            Assert.AreEqual("Two planets", lines[lines.Count - 3]);
            Assert.AreEqual("Backslash to move the fleet here", lines[lines.Count - 2]);
            Assert.AreEqual(
                "Ctrl+Backslash to force free movement",
                lines[lines.Count - 1]
            );
        }

        /// <summary>One hint per LINE, in declared order - never joined into one sentence.</summary>
        [TestMethod]
        public void EachHintIsItsOwnLine()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintQueueFirst, "ui.alternate");
            NodeHints.Add(vtable, HintMoveFleetHere, "ui.contextual");

            CollectionAssert.AreEqual(
                new[]
                {
                    "Dusay",
                    "Two planets",
                    "Ctrl+Shift+Enter to queue it first",
                    "Backslash to move the fleet here",
                },
                Buffer(vtable)
            );
        }

        /// <summary>The readout says the hints too now (owner ruling 2026-09-03), and the buffer still
        /// ends with them ONCE: the part the readout composes is not also written into the buffer as a
        /// part, or every hinted control would review its gestures twice.</summary>
        [TestMethod]
        public void TheBufferEndsWithEachHintExactlyOnce()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintQueueFirst, "ui.alternate");

            CollectionAssert.AreEqual(
                new[] { "Dusay", "Two planets", "Ctrl+Shift+Enter to queue it first" },
                Buffer(vtable)
            );
        }

        /// <summary>The chord is not in the sentence: re-rendering the SAME declaration through a
        /// different formatter re-words the hint. This is the whole point of naming an action.</summary>
        [TestMethod]
        public void RebindingTheActionRewordsTheHint()
        {
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintQueueFirst, "ui.alternate");

            InstallFakeFormatter();
            Assert.IsTrue(Buffer(vtable).Contains("Ctrl+Shift+Enter to queue it first"));

            NodeHints.Chord = (action, index) => "Alt+F3";
            Assert.IsTrue(Buffer(vtable).Contains("Alt+F3 to queue it first"));
            Assert.IsFalse(Buffer(vtable).Contains("Ctrl+Shift+Enter to queue it first"));
        }

        /// <summary>A gate that says no takes its own line away and leaves the rest alone.</summary>
        [TestMethod]
        public void AGatedHintIsAbsentWhileItsGateSaysNo()
        {
            InstallFakeFormatter();
            bool possible = false;
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintMoveFleetHere, "ui.contextual");
            NodeHints.Add(
                vtable,
                HintFreeMovement,
                "ui.contextual",
                1,
                () => possible
            );

            Assert.IsFalse(Buffer(vtable).Contains("Ctrl+Backslash to force free movement"));
            Assert.IsTrue(Buffer(vtable).Contains("Backslash to move the fleet here"));

            possible = true;
            Assert.IsTrue(Buffer(vtable).Contains("Ctrl+Backslash to force free movement"));
        }

        /// <summary>A chord the renderer cannot produce - an action with no such binding - says
        /// nothing at all, rather than a sentence with a hole in it.</summary>
        [TestMethod]
        public void AHintWhoseChordCannotBeRenderedIsSilent()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintDismiss, "ui.contextual", 7);

            CollectionAssert.AreEqual(new[] { "Dusay", "Two planets" }, Buffer(vtable));
        }

        /// <summary>With no renderer installed - boot, teardown, a test - nothing renders. Teardown
        /// safety: a stale delegate would keep calling into an assembly nobody can reach.</summary>
        [TestMethod]
        public void NoRendererMeansNoHints()
        {
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, HintQueueFirst, "ui.alternate");

            CollectionAssert.AreEqual(new[] { "Dusay", "Two planets" }, Buffer(vtable));
        }

        /// <summary>A gate that throws costs its own line and nothing else - a hint is the least
        /// important thing in a buffer.</summary>
        [TestMethod]
        public void AThrowingGateCostsOnlyItsOwnLine()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(
                vtable,
                HintFreeMovement,
                "ui.contextual",
                1,
                () => { throw new InvalidOperationException("no"); }
            );
            NodeHints.Add(vtable, HintMoveFleetHere, "ui.contextual");

            CollectionAssert.AreEqual(
                new[] { "Dusay", "Two planets", "Backslash to move the fleet here" },
                Buffer(vtable)
            );
        }
    }
}
