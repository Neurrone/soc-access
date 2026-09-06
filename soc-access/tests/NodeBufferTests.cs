using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The BUFFER half of the same declaration <see cref="TooltipPartTests"/> covers the spoken half
    /// of. One list of sections, two surfaces, derived - the pairing these tests exist to hold, since
    /// wiring them separately is what let a row announce a tooltip it could not review three times
    /// over.
    /// </summary>
    [TestClass]
    public class NodeBufferTests
    {
        [TestInitialize]
        public void Setup()
        {
            GraphAnnouncer.Reset();
        }

        [TestCleanup]
        public void Cleanup()
        {
            GraphAnnouncer.Reset();
        }

        private static NodeVtable Control(params NodeSection[] sections)
        {
            return new NodeVtable
            {
                ControlType = Type("button", "button"),
                Announcements = new List<NodeAnnouncement>
                {
                    Part("Difficulty", AnnouncementKinds.Label),
                    Part("Normal", AnnouncementKinds.Value),
                    Part("unavailable", AnnouncementKinds.Enabled),
                },
                Sections = sections,
            };
        }

        /// <summary>The head is automatic: the control's name and the state words its readout appends,
        /// never its role word and never the auto-stamped position, which describe the control rather
        /// than being anything it has to say.</summary>
        [TestMethod]
        public void TheHeadIsTheControlsOwnNameAndStateWithoutItsRoleOrPosition()
        {
            CollectionAssert.AreEqual(new[] { "Difficulty", "Normal", "unavailable" }, BufferAmongNeighbours(Control()));
        }

        /// <summary>A control that declares NO sections still reviews correctly - which is what lets a
        /// paragraph of lore be declared as nothing but a label and still be walkable.</summary>
        [TestMethod]
        public void AControlWithNoSectionsBuffersItsOwnReadout()
        {
            CollectionAssert.AreEqual(
                new[] { "The Empire is under a central monarchy." },
                BufferAmongNeighbours(
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            Part("The Empire is under a central monarchy.", AnnouncementKinds.Label),
                        },
                    }
                )
            );
        }

        /// <summary>A part given the label's kind so it speaks beside the name - a cost, a card's
        /// markings - is a buffer line too, one per part: only the head itself is left out for being
        /// the name (owner ruling 2026-09-03).</summary>
        [TestMethod]
        public void APartSpokenBesideTheNameIsABufferLine()
        {
            CollectionAssert.AreEqual(
                new[] { "Team Spirit", "Aggressive", "Flotilla 1: Short Range", "unavailable" },
                BufferAmongNeighbours(
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            Part("Team Spirit", AnnouncementKinds.Label),
                            Part("Aggressive", AnnouncementKinds.Label),
                            Part("Flotilla 1: Short Range", AnnouncementKinds.Label),
                            Part("unavailable", AnnouncementKinds.Enabled),
                        },
                    }
                )
            );
        }

        /// <summary>A control whose first part is not a label - a table cell, which leads with its
        /// value because the column's caption is the edge the player crossed to reach it - opens its
        /// buffer with that value ONCE.</summary>
        [TestMethod]
        public void AControlLeadingWithItsValueDoesNotBufferItTwice()
        {
            CollectionAssert.AreEqual(
                new[] { "37", "selected" },
                BufferAmongNeighbours(
                    new NodeVtable
                    {
                        ControlType = Type("text", null),
                        Announcements = new List<NodeAnnouncement>
                        {
                            Part("37", AnnouncementKinds.Value),
                            Part("selected", AnnouncementKinds.Selected),
                        },
                    }
                )
            );
        }

        /// <summary>A control whose readout leaves out a word the buffer needs declares its own head -
        /// a table cell, whose column caption is spoken as the crossed edge and so is not in the
        /// readout. The declared head replaces the readout's, is what the first content line is tested
        /// against, and does not make the readout's own first part read again.</summary>
        [TestMethod]
        public void ADeclaredHeadOpensTheBufferAndTheCellsOwnFirstLineIsThenTheSame()
        {
            CollectionAssert.AreEqual(
                new[] { "Mods, Valid", "selected", "Requires: Vanilla 1.5" },
                BufferAmongNeighbours(
                    new NodeVtable
                    {
                        ControlType = Type("text", null),
                        Announcements = new List<NodeAnnouncement>
                        {
                            Part("Valid", AnnouncementKinds.Value),
                            Part("selected", AnnouncementKinds.Selected),
                        },
                        BufferHead = () => "Mods, Valid",
                        Sections = new List<NodeSection>
                        {
                            Section(TooltipMode.None, "Mods, Valid"),
                            Section(TooltipMode.Indicate, "Requires: Vanilla 1.5"),
                        },
                    }
                )
            );
        }

        /// <summary>Every section is reviewable whatever its mode: that is what makes "announce and
        /// review" and "indicate and review" the same promise.</summary>
        [TestMethod]
        public void EverySectionReachesTheBufferInDeclaredOrderWhateverItsMode()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Difficulty",
                    "Normal",
                    "unavailable",
                    "What this measures",
                    "Food 12",
                    "A stat block",
                },
                BufferAmongNeighbours(
                    Control(
                        Section(TooltipMode.Announce, "What this measures"),
                        Section(TooltipMode.None, "Food 12"),
                        Section(TooltipMode.Indicate, "A stat block")
                    )
                )
            );
        }

        /// <summary>An indicated tooltip must be readable from the buffer - the indication is a promise
        /// that there is something there.</summary>
        [TestMethod]
        public void AnIndicatedTooltipIsAlwaysInTheBuffer()
        {
            NodeVtable vtable = Control(Section(TooltipMode.Indicate, "Range 4", "Damage 12"));
            Assert.IsTrue(BufferAmongNeighbours(vtable).Contains("Range 4"));
            Assert.IsTrue(BufferAmongNeighbours(vtable).Contains("Damage 12"));
        }

        /// <summary>Native tooltips routinely open by repeating the control's name; the buffer already
        /// opened with it.</summary>
        [TestMethod]
        public void AFirstLineThatOnlyRepeatsTheLabelIsDropped()
        {
            CollectionAssert.AreEqual(
                new[] { "Difficulty", "Normal", "unavailable", "How hard the game is" },
                BufferAmongNeighbours(
                    Control(Section(TooltipMode.Announce, " difficulty ", "How hard the game is"))
                )
            );
        }

        /// <summary>Only the FIRST line of the whole list, and only an exact repeat: a later line that
        /// happens to match, or a heading that adds anything, still reads.</summary>
        [TestMethod]
        public void OnlyTheVeryFirstLineIsTestedAgainstTheLabel()
        {
            CollectionAssert.AreEqual(
                new[] { "Difficulty", "Normal", "unavailable", "How hard", "Difficulty" },
                BufferAmongNeighbours(Control(Section(TooltipMode.Announce, "How hard", "Difficulty")))
            );
            CollectionAssert.AreEqual(
                new[] { "Difficulty", "Normal", "unavailable", "Difficulty settings" },
                BufferAmongNeighbours(Control(Section(TooltipMode.Announce, "Difficulty settings")))
            );
        }

        /// <summary>The dedupe applies across the section boundary too: the first line that exists is
        /// the one tested, whichever section it came out of.</summary>
        [TestMethod]
        public void TheDedupeLooksAtTheFirstLineOfTheFirstSectionThatHasOne()
        {
            CollectionAssert.AreEqual(
                new[] { "Difficulty", "Normal", "unavailable", "How hard the game is" },
                BufferAmongNeighbours(
                    Control(
                        Section(TooltipMode.None),
                        Section(TooltipMode.Announce, "Difficulty", "How hard the game is")
                    )
                )
            );
        }

        /// <summary>A group's expanded state is part of what the readout says, so it is part of the
        /// head - and it comes before the sections, being state rather than content.</summary>
        [TestMethod]
        public void AGroupsExpansionStateIsPartOfTheHead()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(
                Id("g"),
                new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("Load Game", AnnouncementKinds.Label),
                    },
                    Sections = new[] { Section(TooltipMode.Announce, "Load a saved game") },
                }
            ));
            b.EndGroup();
            CollectionAssert.AreEqual(
                new[] { "Load Game", "collapsed", "Load a saved game" },
                NodeBuffer.Lines(Node(b.Build(), "g"))
            );
        }

        /// <summary>A section that throws while resolving is a section with nothing to say, not a
        /// screen with an empty buffer: the game's own readers throw on half-torn-down widgets.</summary>
        [TestMethod]
        public void ASectionThatThrowsIsSkippedAndTheRestStillRead()
        {
            CollectionAssert.AreEqual(
                new[] { "Difficulty", "Normal", "unavailable", "Still here" },
                BufferAmongNeighbours(
                    Control(
                        NodeSection.Composed(() => { throw new InvalidOperationException(); }),
                        Section(TooltipMode.None, "Still here")
                    )
                )
            );
        }
        /// <summary>The readout's own dedupe (<see cref="TooltipPartTests"/>) takes nothing out of the
        /// DECLARATION: a line the label already spoke is dropped from what the control says on arrival
        /// and is still here to walk. Which is the whole reason a call site no longer has to choose
        /// between saying a line twice and throwing the tooltip away.</summary>
        [TestMethod]
        public void ALineTheReadoutDedupedAwayIsStillReviewable()
        {
            CollectionAssert.AreEqual(
                new[] { "Difficulty", "Normal", "unavailable", "Normal", "How hard the AI plays" },
                BufferAmongNeighbours(Control(Section(TooltipMode.Announce, "Normal", "How hard the AI plays")))
            );
        }
    }
}
