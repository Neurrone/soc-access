using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// What a screen keeps between two builds of the same table, and the question it asks before
    /// handing it back.
    /// </summary>
    [TestClass]
    public class SheetSnapshotTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            GraphSheet.Reset();
        }

        /// <summary>The keys decide: the same list, the same words and the same figure hand the table
        /// back; a new list, a retyped caption or a moved figure read the game again. A build that
        /// threw part way is not kept at all.</summary>
        [TestMethod]
        public void OnlyTheSameKeysHandTheTableBack()
        {
            object rows = new object();
            SheetSnapshot snapshot = new SheetSnapshot();

            Assert.IsFalse(snapshot.TryReplay(Sheet(), rows, "Fleets", 3));

            snapshot.Record(Sheet(), rows, "Fleets", 3);
            Assert.IsFalse(snapshot.TryReplay(Sheet(), rows, "Fleets", 3)); // nothing kept until Keep
            snapshot.Keep();

            Assert.IsTrue(snapshot.TryReplay(Sheet(), rows, "Fleets", 3));
            // A string is compared by its letters and a figure by its value, not by which box it is in.
            Assert.IsTrue(snapshot.TryReplay(Sheet(), rows, "Fle" + "ets", 1 + 2));

            Assert.IsFalse(snapshot.TryReplay(Sheet(), new object(), "Fleets", 3));
            Assert.IsFalse(snapshot.TryReplay(Sheet(), rows, "Systems", 3));
            Assert.IsFalse(snapshot.TryReplay(Sheet(), rows, "Fleets", 4));
            Assert.IsFalse(snapshot.TryReplay(Sheet(), rows, "Fleets"));
        }

        /// <summary>A replay declares what the recording declared, and the screen that asked for it
        /// finishes and lands as it would after a build of its own.</summary>
        [TestMethod]
        public void AKeptTableIsHandedBackWhole()
        {
            object rows = new object();
            SheetSnapshot snapshot = new SheetSnapshot();

            GraphBuilder recorded = new GraphBuilder();
            GraphSheet writing = new GraphSheet(recorded, "t:");
            snapshot.Record(writing, rows);
            writing.Region("Fleets", new[] { "Name", "Ships" });
            writing.Row(Vt("Alpha"), "a", null, () => "3");
            writing.Finish();
            snapshot.Keep();
            GraphRender first = recorded.Build();

            GraphBuilder again = new GraphBuilder();
            GraphSheet reading = new GraphSheet(again, "t:");
            Assert.IsTrue(snapshot.TryReplay(reading, rows));
            reading.Finish();
            GraphRender second = again.Build();

            CollectionAssert.AreEquivalent(
                new System.Collections.Generic.List<string>(Keys(first)),
                new System.Collections.Generic.List<string>(Keys(second)));
            Assert.AreEqual(Key(first.NodeAt(first.StartKey)), Key(second.NodeAt(reading.FirstRow)));
        }

        private static GraphSheet Sheet()
        {
            return new GraphSheet(new GraphBuilder(), "t:");
        }
    }
}
