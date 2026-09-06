using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Localization;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// What focus actually says. The announcer joins parts with the ModStrings list separator and reads
    /// injected delegates, so it shares the ModStrings collection: nothing here may run beside a test
    /// that installs a translation.
    /// </summary>
    [TestClass]
    public class GraphAnnouncerTests
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

        // A context "Settings, list" wrapping two controls.
        private static GraphRender Contextual()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Settings", "list");
            b.AddItem(new SyntheticNode(Id("a"), Vt("Item A")));
            b.AddItem(new SyntheticNode(Id("b"), Vt("Item B")));
            b.PopContext();
            return b.Build();
        }

        [TestMethod]
        public void EnteringAContextReadsItsLevelsThenTheControl()
        {
            GraphRender r = Contextual();
            Assert.AreEqual("Settings, list, Item A", GraphAnnouncer.ComposeFull(Node(r, "a")));
        }

        [TestMethod]
        public void MovingWithinAContextReadsOnlyTheControl()
        {
            GraphRender r = Contextual();
            Assert.AreEqual("Item B", GraphAnnouncer.Compose(Node(r, "a"), Node(r, "b")));
        }

        [TestMethod]
        public void ATransitionLabelLeadsTheLine()
        {
            GraphRender r = Contextual();
            Assert.AreEqual("Ships, Item B", GraphAnnouncer.Compose(Node(r, "a"), Node(r, "b"), "Ships"));
        }

        [TestMethod]
        public void DescendingOntoAGroupsOwnChildReadsOnlyTheChild()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")), true);
            b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.AreEqual("Child 1", GraphAnnouncer.Compose(Node(r, "g"), Node(r, "c1")));
        }

        [TestMethod]
        public void AscendingReadsJustTheLandingNode()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")), true);
            b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.AreEqual("Group", GraphAnnouncer.Compose(Node(r, "c1"), Node(r, "g")));
        }

        [TestMethod]
        public void ALevelWhoseLabelDuplicatesTheNextIsDropped()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Game difficulty");
            b.AddItem(new SyntheticNode(Id("a"), Vt("Game difficulty", Part("combo box", AnnouncementKinds.Role))));
            b.PopContext();
            GraphRender r = b.Build();
            Assert.AreEqual("Game difficulty, combo box", GraphAnnouncer.ComposeFull(Node(r, "a")));
        }

        [TestMethod]
        public void ADistinctLevelLabelIsKept()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Game difficulty");
            b.AddItem(new SyntheticNode(Id("a"), Vt("Normal")));
            b.PopContext();
            GraphRender r = b.Build();
            Assert.AreEqual("Game difficulty, Normal", GraphAnnouncer.ComposeFull(Node(r, "a")));
        }

        [TestMethod]
        public void ComposeSaysNothingWhenThereIsNoNode()
        {
            Assert.IsNull(GraphAnnouncer.Compose(null, null));
        }

        // ---- control types ----

        [TestMethod]
        public void PartsSpeakInTheControlTypesKindOrder()
        {
            ControlType toggle = Type("toggle", "toggle");
            NodeVtable vt = new NodeVtable
            {
                ControlType = toggle,
                Announcements = new[]
                {
                    Part("on", AnnouncementKinds.Value),
                    Part("Auto explore", AnnouncementKinds.Label),
                },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.AreEqual("Auto explore, toggle, on", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [TestMethod]
        public void ANodePartOverridesTheTypesCommonPartOfTheSameKind()
        {
            ControlType button = Type("button", "button");
            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[]
                {
                    Part("Colonize", AnnouncementKinds.Label),
                    Part("menu button", AnnouncementKinds.Role),
                },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.AreEqual("Colonize, menu button", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [TestMethod]
        public void KindlessPartsKeepTheirDeclarationOrderAfterTheOrderedOnes()
        {
            ControlType button = Type("button", "button");
            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => "first extra"),
                    Part("Colonize", AnnouncementKinds.Label),
                    new NodeAnnouncement(() => "second extra"),
                },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.AreEqual("Colonize, button, first extra, second extra",
                GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        // ---- what it costs ----
        //
        // No screen composes these words either: the game draws every price from the tooltip a control
        // points at, so the price rides on that section and the announcer says it wherever there is
        // one.

        private static string Priced(params NodeSection[] sections)
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(
                Id("a"),
                new NodeVtable
                {
                    ControlType = Type("button", "button"),
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("The Analytical Engine", AnnouncementKinds.Label),
                    },
                    Sections = sections,
                }
            ));
            return GraphAnnouncer.LeafText(Node(b.Build(), "a"));
        }

        /// <summary>Right after the name and before the role word - which is where the player decides
        /// whether to go on listening, and is a matter of the part's KIND rather than of where it was
        /// added.</summary>
        [TestMethod]
        public void APricedControlSaysItsPriceStraightAfterItsName()
        {
            Assert.AreEqual(
                "The Analytical Engine, 780 Industry (8 turns), button",
                Priced(Graphs.Priced(TooltipMode.Indicate, "780 Industry (8 turns)", "A stat block"))
            );
        }

        /// <summary>The price is the POINTED-AT tooltip's, the same rule the spoken tooltip follows:
        /// a row carrying a heading's explanation as well as its value's says the value's price.
        /// </summary>
        [TestMethod]
        public void ThePriceComesOffTheTooltipTheControlPointsAt()
        {
            Assert.AreEqual(
                "The Analytical Engine, 218 Science, button",
                Priced(
                    Graphs.Priced(TooltipMode.Indicate, "780 Industry", "What this measures"),
                    Graphs.Priced(TooltipMode.Indicate, "218 Science", "A stat block")
                )
            );
        }

        /// <summary>A tooltip the readout says nothing about still yields its price: the classes that
        /// draw a cost panel are exactly the renderer-assembled kind, so a rule that only looked among
        /// the announcing sections would find a price on none of them.</summary>
        [TestMethod]
        public void AnIndicatedTooltipsPriceSpeaksEvenThoughItsWordsDoNot()
        {
            Assert.IsFalse(
                Priced(Graphs.Priced(TooltipMode.Indicate, "780 Industry", "A stat block")).Contains("A stat block")
            );
        }

        /// <summary>Nothing extra where there is no price - a tooltip whose class draws no cost panel,
        /// and a row that draws its own turn count and had the price taken off it at the door.
        /// </summary>
        [TestMethod]
        public void AControlWithNoPriceSaysNothingAboutOne()
        {
            Assert.AreEqual(
                "The Analytical Engine, button",
                Priced(Section(TooltipMode.Indicate, "A stat block"))
            );

            NodeSection priced = Graphs.Priced(TooltipMode.Indicate, "780 Industry", "A stat block");
            priced.Cost = null;
            Assert.AreEqual("The Analytical Engine, button", Priced(priced));
        }

        // ---- the drag ----
        //
        // No screen composes these words: the announcer derives them from the control's own pick-up and
        // drop declarations, so a screen written tomorrow says them too.

        [TestMethod]
        public void AControlThatCanBeDraggedSaysSoAtTheTailOfItsReadout()
        {
            CarryState carry = new CarryState();
            GraphAnnouncer.Carry = carry;
            ControlType button = Type("button", "button");
            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Explorer", AnnouncementKinds.Label) },
                Sections = new[] { NodeSection.Composed(() => new[] { "A ship" }) },
                OnPickUp = () => new CarryItem(new object(), "Explorer", "ship"),
                DropKind = "ship",
                OnDrop = held => DropResult.Done(),
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            GraphNode node = Node(b.Build(), "a");

            Assert.AreEqual("Explorer, button, A ship, draggable", GraphAnnouncer.LeafText(node));

            // Mid-drag the useful fact is where the thing can GO, so the same control swaps one word for
            // the other rather than saying both.
            carry.PickUp(new CarryItem(new object(), "Hunter", "ship"), "fleets");
            Assert.AreEqual("Explorer, button, A ship, drop target", GraphAnnouncer.LeafText(node));
        }

        [TestMethod]
        public void AControlWithNoDragDeclarationsSaysNothingAboutDragging()
        {
            GraphAnnouncer.Carry = new CarryState();
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("Colonize")));
            Assert.AreEqual("Colonize", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [TestMethod]
        public void EmptyPartsAreSilent()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), new NodeVtable
            {
                Announcements = new[]
                {
                    NodeAnnouncement.Static("Label"),
                    new NodeAnnouncement(() => null),
                    NodeAnnouncement.Static(""),
                    NodeAnnouncement.Static("Tail"),
                },
            }));
            Assert.AreEqual("Label, Tail", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [TestMethod]
        public void ThePartFilterDropsPartsFromTheReadout()
        {
            ControlType button = Type("button", "button");
            GraphAnnouncer.PartFilter = (type, part) => part.Kind != AnnouncementKinds.Role;

            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Colonize", AnnouncementKinds.Label) },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.AreEqual("Colonize", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [TestMethod]
        public void ThePartFilterSeesTheControlType()
        {
            List<string> seen = new List<string>();
            ControlType button = Type("button", "button");
            GraphAnnouncer.PartFilter = (type, part) =>
            {
                seen.Add(type == null ? "<untyped>" : type.Key);
                return true;
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Colonize", AnnouncementKinds.Label) },
            }));
            GraphAnnouncer.LeafText(Node(b.Build(), "a"));
            CollectionAssert.AreEqual(new[] { "button", "button" }, seen); // the common role part and the label
        }

        // ---- expansion + position ----

        [TestMethod]
        public void AGroupReadsItsExpandedState()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
            b.EndGroup();
            Assert.AreEqual("Group, collapsed", GraphAnnouncer.LeafText(Node(b.Build(), "g")));
        }

        [TestMethod]
        public void AGroupThatSpeaksItsOwnExpansionIsNotAnnotated()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            NodeVtable vt = Vt("Group, open");
            vt.SpeaksOwnExpansion = true;
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), vt));
            b.EndGroup();
            Assert.AreEqual("Group, open", GraphAnnouncer.LeafText(Node(b.Build(), "g")));
        }

        [TestMethod]
        public void PositionIsStampedAmongRealSiblingsOnly()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).AddItem(new SyntheticNode(Id("c"), Vt("C")));
            GraphRender r = b.Build();
            Assert.AreEqual("B, 2 of 3", GraphAnnouncer.LeafText(Node(r, "b")));
        }

        [TestMethod]
        public void ALoneControlReadsNoPosition()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A")));
            Assert.AreEqual("A", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [TestMethod]
        public void AGroupsOnlyChildStillReadsOneOfOne()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("G")), true);
            b.AddItem(new SyntheticNode(Id("only"), Vt("Only")));
            b.EndGroup();
            Assert.AreEqual("Only, 1 of 1", GraphAnnouncer.LeafText(Node(b.Build(), "only")));
        }

        [TestMethod]
        public void SuppressChildPositionsSilencesThePositionReadout()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Log", null, false);
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
            b.PopContext();
            Assert.AreEqual("B", GraphAnnouncer.LeafText(Node(b.Build(), "b")));
        }

        [TestMethod]
        public void ANodeThatSpeaksItsOwnPositionIsNotStamped()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            NodeVtable vt = Vt("B, second of two");
            vt.SpeaksOwnPosition = true;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), vt));
            Assert.AreEqual("B, second of two", GraphAnnouncer.LeafText(Node(b.Build(), "b")));
        }

        [TestMethod]
        public void ThePositionKindFilterGovernsTheAutoStampedPosition()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphAnnouncer.PartFilter = (type, part) => part.Kind != AnnouncementKinds.Position;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
            Assert.AreEqual("B", GraphAnnouncer.LeafText(Node(b.Build(), "b")));
        }

        // ---- the effective-parts memo ----

        [TestMethod]
        public void TheEffectivePartsAreComputedOncePerNodePerRender()
        {
            GraphRender r = Contextual();
            GraphNode node = Node(r, "a");
            Assert.AreSame(
                GraphAnnouncer.EffectiveAnnouncements(node),
                GraphAnnouncer.EffectiveAnnouncements(node));
        }

        [TestMethod]
        public void ANewRenderRecomputesTheEffectiveParts()
        {
            List<NodeAnnouncement> first = GraphAnnouncer.EffectiveAnnouncements(Node(Contextual(), "a"));
            List<NodeAnnouncement> second = GraphAnnouncer.EffectiveAnnouncements(Node(Contextual(), "a"));
            Assert.AreNotSame(first, second);
        }

        [TestMethod]
        public void InstallingAPartFilterReFiltersTheSameNode()
        {
            ControlType button = Type("button", "button");
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Colonize", AnnouncementKinds.Label) },
            }));
            GraphNode node = Node(b.Build(), "a");

            Assert.AreEqual("Colonize, button", GraphAnnouncer.LeafText(node));
            GraphAnnouncer.PartFilter = (type, part) => part.Kind != AnnouncementKinds.Role;
            Assert.AreEqual("Colonize", GraphAnnouncer.LeafText(node));
        }

        // ---- the usage hints, spoken (owner ruling 2026-09-03) ----

        private static void Chords()
        {
            NodeHints.Chord = (action, index) =>
                action == "ui.alternate" && index == 0
                    ? "Ctrl+Shift+Enter"
                    : action == "ui.contextual" && index == 0
                        ? "Backslash"
                        : null;
        }

        // A hinted button carrying the given sections.
        private static NodeVtable Hinted(params NodeSection[] sections)
        {
            NodeVtable vt = new NodeVtable
            {
                ControlType = Type("button", "button"),
                Announcements = new List<NodeAnnouncement>
                {
                    Part("The Analytical Engine", AnnouncementKinds.Label),
                },
                Sections = sections,
            };
            NodeHints.Add(vt, HintQueueFirst, "ui.alternate");
            return vt;
        }

        // The same control second of three, so its readout also carries a position.
        private static string HintedReadout(NodeVtable vt)
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("Before")));
            b.AddItem(new SyntheticNode(Id("t"), vt));
            b.AddItem(new SyntheticNode(Id("c"), Vt("After")));
            return GraphAnnouncer.LeafText(Node(b.Build(), "t"));
        }

        [TestMethod]
        public void AHintIsSaidAfterEverythingIncludingThePosition()
        {
            Chords();
            Assert.AreEqual(
                "The Analytical Engine, button, 2 of 3, Ctrl+Shift+Enter to queue it first",
                HintedReadout(Hinted())
            );
        }

        [TestMethod]
        public void AHintFollowsTheTooltipTheControlAnnounces()
        {
            Chords();
            Assert.AreEqual(
                "The Analytical Engine, button, Queues the improvement, 2 of 3, "
                    + "Ctrl+Shift+Enter to queue it first",
                HintedReadout(Hinted(Section(TooltipMode.Announce, "Queues the improvement")))
            );
        }

        [TestMethod]
        public void SeveralHintsAreOnePartInDeclaredOrder()
        {
            Chords();
            NodeVtable vt = Hinted();
            NodeHints.Add(vt, HintMoveFleetHere, "ui.contextual");
            Assert.AreEqual(
                "The Analytical Engine, button, 2 of 3, Ctrl+Shift+Enter to queue it first, "
                    + "Backslash to move the fleet here",
                HintedReadout(vt)
            );
        }

        [TestMethod]
        public void AHintWhoseGateSaysNoIsNotInTheReadout()
        {
            Chords();
            NodeVtable vt = new NodeVtable
            {
                ControlType = Type("button", "button"),
                Announcements = new List<NodeAnnouncement>
                {
                    Part("The Analytical Engine", AnnouncementKinds.Label),
                },
            };
            NodeHints.Add(vt, HintQueueFirst, "ui.alternate", 0, () => false);
            Assert.AreEqual("The Analytical Engine, button, 2 of 3", HintedReadout(vt));
        }

        /// <summary>The expanded/collapsed word is about the control, so it stays ahead of the
        /// keyboard sentence even where there is no tooltip to anchor it.</summary>
        [TestMethod]
        public void TheExpandedWordStaysAheadOfTheHint()
        {
            Chords();
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Hinted()));
            b.EndGroup();
            Assert.AreEqual(
                "The Analytical Engine, button, collapsed, Ctrl+Shift+Enter to queue it first",
                GraphAnnouncer.LeafText(Node(b.Build(), "g"))
            );
        }

        /// <summary>A table row's position is composed by the announcer rather than by the node, and
        /// it too comes before the hint.</summary>
        [TestMethod]
        public void ATableRowsPositionStaysAheadOfTheHint()
        {
            Chords();
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            NodeVtable vt = Hinted();
            vt.Row = new TableRow { Key = "r2", Index = 2, Count = 5 };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("t"), vt));
            Assert.AreEqual(
                "The Analytical Engine, button, 2 of 5, Ctrl+Shift+Enter to queue it first",
                GraphAnnouncer.ComposeFull(Node(b.Build(), "t"))
            );
        }

        /// <summary>On a tooltip the game only assembles on hover, and that this player asked to hear,
        /// the hint waits for the tooltip's own words: it is the last thing said about the control, and
        /// on such a control the tooltip is heard frames after the readout.</summary>
        [TestMethod]
        public void AHintOnALateTooltipWaitsForTheWordsAndIsNotSaidTwice()
        {
            Chords();
            List<string> drawn = new List<string>();
            NodeVtable vt = Hinted(
                LateSection(() => drawn, "Emperor's Will +78 Influence")
            );
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("Before")));
            b.AddItem(new SyntheticNode(Id("t"), vt));
            b.AddItem(new SyntheticNode(Id("c"), Vt("After")));
            GraphNode node = Node(b.Build(), "t");

            Assert.AreEqual("The Analytical Engine, button, 2 of 3", GraphAnnouncer.LeafText(node));

            drawn.Add("Emperor's Will +78 Influence");
            Assert.AreEqual(
                "The Analytical Engine, button, Emperor's Will +78 Influence, 2 of 3, "
                    + "Ctrl+Shift+Enter to queue it first",
                GraphAnnouncer.LeafText(node)
            );
        }

        /// <summary>Which is why that hint is WATCHED and an ordinary one is not: the watch is what
        /// speaks it when the words arrive.</summary>
        [TestMethod]
        public void OnlyAWaitingHintIsLive()
        {
            Chords();
            Assert.IsFalse(HintPartOf(Hinted()).Live);
            Assert.IsTrue(HintPartOf(Hinted(LateSection(() => new List<string>(), "Late"))).Live);
        }

        private static NodeAnnouncement HintPartOf(NodeVtable vt)
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("t"), vt));
            foreach (NodeAnnouncement part in GraphAnnouncer.EffectiveAnnouncements(Node(b.Build(), "t")))
            {
                if (part.Kind == AnnouncementKinds.Hint) return part;
            }

            return null;
        }
    }
}
