using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The re-announce: what a control whose CONTENT the player moved within asks the navigator for
    /// (a tile grid's cursor walks a whole board inside one node), and what the navigator does with
    /// the memory it keeps so the answer reads as a landing does.
    ///
    /// Read through the differ's own fields rather than by running a frame: <c>EnsureFocus</c> draws
    /// the game's tooltip window and asks Unity whether a node is still drawn, neither of which can
    /// be compiled outside the engine. What the fields are FOR is asserted against the real
    /// announcer below.
    /// </summary>
    [TestClass]
    public class GraphNavigatorTests
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

        private static object Field(GraphNavigator navigator, string name)
        {
            FieldInfo field = typeof(GraphNavigator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name + " is gone from the navigator; this test is about it");
            return field.GetValue(navigator);
        }

        private static void SetField(GraphNavigator navigator, string name, object value)
        {
            typeof(GraphNavigator)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(navigator, value);
        }

        [TestMethod]
        public void ReannounceFocusedForgetsTheReadoutAndKeepsTheNodeItWasComposedAgainst()
        {
            GraphRender render = Renderer(b =>
            {
                b.BeginStop("board");
                b.PushContext("Battlefield");
                b.AddItem(new SyntheticNode(Id("tile"), Vt("Grass")));
                b.PopContext();
            })();
            GraphNode tile = Node(render, "tile");

            // What a frame leaves behind once the landing on the board has been read out: the
            // readout, the node it was composed against, the buffer it filled, the live baseline.
            GraphNavigator navigator = new GraphNavigator();
            SetField(navigator, "_lastSpokenKey", tile.Id);
            SetField(navigator, "_lastSpokenNode", tile);
            SetField(navigator, "_bufferKey", tile.Id);
            SetField(navigator, "_bufferReadout", "Grass");
            SetField(navigator, "_bufferLines", new List<string> { "Grass" });
            SetField(navigator, "_liveKey", tile.Id);
            ((List<string>)Field(navigator, "_liveValues")).Add("Grass");

            navigator.ReannounceFocused();

            Assert.IsNull(Field(navigator, "_lastSpokenKey"), "the next frame sees the node as freshly landed on");
            Assert.AreSame(tile, Field(navigator, "_lastSpokenNode"), "and composes it against the same chain");
            Assert.IsNull(Field(navigator, "_bufferKey"), "the review buffer is filled again");
            Assert.IsNull(Field(navigator, "_bufferReadout"));
            Assert.IsNull(Field(navigator, "_bufferLines"));
            Assert.IsNull(Field(navigator, "_liveKey"), "and the live watch baselines itself again");
            Assert.AreEqual(0, ((List<string>)Field(navigator, "_liveValues")).Count);

            // What keeping the node buys, said by the composer the navigator hands the pair to: the
            // leaf alone, as a landing within the same stop reads it, and not the whole arrival.
            Assert.AreEqual("Grass", GraphAnnouncer.Compose(tile, tile));
            Assert.AreEqual("Battlefield, Grass", GraphAnnouncer.ComposeFull(tile));
        }
    }
}
