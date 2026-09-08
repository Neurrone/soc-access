using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The map and battle tile's click hints (<see cref="TileInstructionHints"/>): the tile says what
    /// the game's own instruction rows said a click would do, and says NOTHING when the game named no
    /// action - a hint per kind is declared on the node, so the gate is the whole mechanism.
    /// </summary>
    [TestClass]
    public class TileInstructionHintsTests
    {
        [TestInitialize]
        public void Setup()
        {
            GraphAnnouncer.Reset();
            NodeHints.Reset();
            NodeHints.Chord = (action, index) =>
                action == AccessibilityActions.UiLeftClick.Key && index == 0
                    ? "Enter"
                    : action == AccessibilityActions.UiRightClick.Key && index == 0
                        ? "Backslash"
                        : null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            GraphAnnouncer.Reset();
            NodeHints.Reset();
        }

        private static NodeVtable Tile()
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { Part("Grass", AnnouncementKinds.Label) },
            };
        }

        private static Tooltip Tip(TileInstruction primary, TileInstruction secondary)
        {
            return new Tooltip(() => new string[0], null, primary, secondary);
        }

        [TestMethod]
        public void ATileSaysOneHintPerInstructionTheGameNamed()
        {
            Tooltip tooltip = Tip(TileInstruction.Select, TileInstruction.Visit);
            NodeVtable vtable = Tile();
            TileInstructionHints.Add(vtable, () => tooltip);

            CollectionAssert.AreEqual(
                new[] { "Grass", "Enter selects", "Backslash visits" },
                Buffer(vtable)
            );
        }

        [TestMethod]
        public void ATileTheGameNamedNoActionOnSaysNothingAboutClicking()
        {
            Tooltip tooltip = Tip(TileInstruction.None, TileInstruction.None);
            NodeVtable vtable = Tile();
            TileInstructionHints.Add(vtable, () => tooltip);

            CollectionAssert.AreEqual(new[] { "Grass" }, Buffer(vtable));
        }
    }
}
