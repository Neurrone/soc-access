using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// What a control's declared SECTIONS do to its focus readout. A section says what its lines are
    /// and how loud they should be; the engine derives the spoken tooltip part from the modes alone,
    /// and the review buffer from every section regardless of mode
    /// (<see cref="NodeBufferTests"/>). Nothing here is a screen's decision any more, which is the
    /// point: the two surfaces cannot drift apart if they come from one declaration.
    /// </summary>
    [TestClass]
    public class TooltipPartTests
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

        private static Func<IList<string>> Tooltip(params string[] lines)
        {
            return () => new List<string>(lines);
        }

        // A button carrying the given sections, in a list of three so it also reads a position - the
        // shape a menu entry has.
        private static string Readout(params NodeSection[] sections)
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("Before")));
            b.AddItem(new SyntheticNode(
                Id("t"),
                new NodeVtable
                {
                    ControlType = Type("button", "button"),
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("New Game", AnnouncementKinds.Label),
                    },
                    Sections = sections,
                }
            ));
            b.AddItem(new SyntheticNode(Id("c"), Vt("After")));
            return GraphAnnouncer.LeafText(Node(b.Build(), "t"));
        }

        [TestMethod]
        public void AnnounceModeSpeaksTheTooltipAfterTheControlAndBeforeThePosition()
        {
            Assert.AreEqual(
                "New Game, button, Start a new game, 2 of 3",
                Readout(Section(TooltipMode.Announce, "Start a new game"))
            );
        }

        [TestMethod]
        public void AnnounceModeJoinsTheTooltipsLinesIntoOneReadout()
        {
            Assert.AreEqual(
                "New Game, button, Quick start Skips setup Uses the last settings, 2 of 3",
                Readout(
                    Section(
                        TooltipMode.Announce,
                        "Quick start",
                        "",
                        "Skips setup",
                        "Uses the last settings"
                    )
                )
            );
        }

        /// <summary>
        /// A long tooltip is READ, never announced - not its words, and not the fact that it is there.
        ///
        /// The mod used to say "has tooltip" here. On a live screen almost every control carries one,
        /// so the phrase arrived on nearly every readout and distinguished nothing; the convention that
        /// replaced it is simply that the player checks the review buffer, on any control, whenever
        /// they want more. Nothing was lost from the buffer - only from the announcement.
        /// </summary>
        [TestMethod]
        public void IndicateModeSaysNothingInTheReadout()
        {
            Assert.AreEqual(
                "New Game, button, 2 of 3",
                Readout(Section(TooltipMode.Indicate, "A long stat block", "line two"))
            );
            Assert.IsNull(TooltipParts.Part(new[] { Section(TooltipMode.Indicate, "A long stat block") }));
        }

        /// <summary>Whatever its state: with words, without them, and with the engine's own
        /// would-it-draw test answering either way. The test is still declared - the tooltip-parity
        /// audit and the focus pointer both ask it - it just no longer decides a spoken word.</summary>
        [TestMethod]
        public void AnIndicateSectionIsSilentWhateverItsStateIs()
        {
            Assert.AreEqual("New Game, button, 2 of 3", Readout(Section(TooltipMode.Indicate)));
            Assert.AreEqual("New Game, button, 2 of 3", Readout(Section(TooltipMode.Indicate, "", "   ")));
            Assert.AreEqual(
                "New Game, button, 2 of 3",
                Readout(NodeSection.Derived(Tooltip(), TooltipMode.Indicate, () => false))
            );
            Assert.AreEqual(
                "New Game, button, 2 of 3",
                Readout(NodeSection.Derived(Tooltip("A stat block"), TooltipMode.Indicate, () => true))
            );
        }

        [TestMethod]
        public void NoneModeContributesNoPart()
        {
            Assert.IsNull(TooltipParts.Part(new[] { Section(TooltipMode.None, "Start a new game") }));
            Assert.AreEqual(
                "New Game, button, 2 of 3",
                Readout(Section(TooltipMode.None, "Start a new game"))
            );
        }

        [TestMethod]
        public void AControlWithNoSectionsContributesNoPart()
        {
            Assert.IsNull(TooltipParts.Part(null));
            Assert.IsNull(TooltipParts.Part(new NodeSection[0]));
            Assert.IsNull(TooltipParts.Part(new NodeSection[] { null }));
            Assert.IsNull(TooltipParts.Part(new[] { NodeSection.Derived(null, TooltipMode.Announce, null) }));
        }

        [TestMethod]
        public void AnEmptyTooltipIsSilentWhenItsTextIsWhatWouldBeSpoken()
        {
            Assert.AreEqual("New Game, button, 2 of 3", Readout(Section(TooltipMode.Announce)));
        }

        /// <summary>
        /// A row carries the heading's explanation of what the measure IS and the value's description
        /// of what it SAYS, in drawn order. The one the player asked for by landing there is the
        /// value's - the last one drawn - so that is the one spoken.
        /// </summary>
        [TestMethod]
        public void TheLastShortTooltipIsTheOneSpoken()
        {
            Assert.AreEqual(
                "New Game, button, Currently 8 empires, 2 of 3",
                Readout(
                    Section(TooltipMode.Announce, "How many empires play"),
                    Section(TooltipMode.Announce, "Currently 8 empires")
                )
            );
        }

        /// <summary>
        /// A long section beside a short one takes nothing away from it and adds nothing of its own.
        /// The short one's words are the sentence the game's author wrote for exactly this moment, and
        /// they are said wherever the long one sits in the row; the long one is in the buffer, which is
        /// where the player looks for it whether or not anything said so.
        /// </summary>
        [TestMethod]
        public void ALongTooltipLeavesTheShortOnesWordsAloneAndAddsNothing()
        {
            Assert.AreEqual(
                "New Game, button, What this measures, 2 of 3",
                Readout(
                    Section(TooltipMode.Announce, "What this measures"),
                    Section(TooltipMode.Indicate, "a stat block")
                )
            );
            Assert.AreEqual(
                "New Game, button, What it is set to, 2 of 3",
                Readout(
                    Section(TooltipMode.Indicate, "a stat block"),
                    Section(TooltipMode.Announce, "What it is set to")
                )
            );
        }

        /// <summary>A buffer-only section is the control's drawn face: reviewable, and never a word in
        /// the readout - not its text, and not an indication that it exists.</summary>
        [TestMethod]
        public void ABufferOnlySectionIsNeitherSpokenNorIndicated()
        {
            Assert.AreEqual(
                "New Game, button, The description, 2 of 3",
                Readout(
                    Section(TooltipMode.None, "Food 12", "Industry 8"),
                    Section(TooltipMode.Announce, "The description")
                )
            );
            Assert.AreEqual(
                "New Game, button, 2 of 3",
                Readout(Section(TooltipMode.None, "Food 12", "Industry 8"))
            );
        }

        [TestMethod]
        public void TheTooltipIsReadAtSpeakTimeSoAnAppendedReasonStaysCurrent()
        {
            List<string> lines = new List<string> { "Join a multiplayer game" };
            NodeAnnouncement part = TooltipParts.Part(
                new[] { NodeSection.Composed(() => lines) }
            );
            Assert.AreEqual("Join a multiplayer game", part.Text());

            lines.Add("Steam is not running");
            Assert.AreEqual("Join a multiplayer game Steam is not running", part.Text());
        }

        [TestMethod]
        public void TheTooltipPartCarriesTheTooltipKind()
        {
            Assert.AreEqual(
                AnnouncementKinds.Tooltip,
                TooltipParts.Part(new[] { Section(TooltipMode.Announce, "Text") }).Kind
            );
        }

        /// <summary>A screen may still declare a tooltip-kind part of its own for something no section
        /// can express - a drop-list entry's live refusal - and the derived part must survive beside
        /// it, or a row the screen has one extra word about loses the tooltip it was reading.</summary>
        [TestMethod]
        public void ASectionStillSpeaksBesideAPartTheScreenDeclaredItself()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(
                Id("t"),
                new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("Vaulters", AnnouncementKinds.Label),
                        Part("The content pack is not activated", AnnouncementKinds.Tooltip),
                    },
                    Sections = new[] { Section(TooltipMode.Announce, "A faction of exiles") },
                }
            ));
            Assert.AreEqual(
                "Vaulters, The content pack is not activated, A faction of exiles",
                GraphAnnouncer.LeafText(Node(b.Build(), "t"))
            );
        }

        [TestMethod]
        public void AGroupReadsItsExpansionStateBeforeItsTooltip()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), new NodeVtable
            {
                ControlType = Type("button", "button"),
                Announcements = new List<NodeAnnouncement>
                {
                    Part("New Game", AnnouncementKinds.Label),
                },
                Sections = new[] { Section(TooltipMode.Announce, "Start a new game") },
            }));
            b.EndGroup();
            Assert.AreEqual(
                "New Game, button, collapsed, Start a new game",
                GraphAnnouncer.LeafText(Node(b.Build(), "g"))
            );
        }

        [TestMethod]
        public void AGroupWithNoTooltipStillReadsItsExpansionStateLast()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Options")));
            b.EndGroup();
            Assert.AreEqual("Options, collapsed", GraphAnnouncer.LeafText(Node(b.Build(), "g")));
        }

        // ---- the readout's own dedupe ----
        //
        // Half this game's icons are named by the first line of their own tooltip, and the readout is
        // where the label and the tooltip are both in hand. A line the readout is going to say anyway
        // comes out of the tooltip part; everything else is still handed over, and the review buffer
        // (NodeBufferTests) keeps all of it either way.

        [TestMethod]
        public void ALineTheLabelAlreadySaysIsDroppedFromTheTooltip()
        {
            Assert.AreEqual(
                "New Game, button, Skips setup, 2 of 3",
                Readout(Section(TooltipMode.Announce, "New Game", "Skips setup"))
            );
        }

        [TestMethod]
        public void TheDedupeIgnoresCaseAndSurroundingSpace()
        {
            Assert.AreEqual(
                "New Game, button, Skips setup, 2 of 3",
                Readout(Section(TooltipMode.Announce, "  new game  ", "Skips setup"))
            );
        }

        /// <summary>A one-line tooltip that IS the name says nothing: the label already said it, and
        /// there is nothing left. This is the case ~15 screens used to write out by hand.</summary>
        [TestMethod]
        public void ATooltipThatOnlyRepeatsTheNameSaysNothing()
        {
            Assert.AreEqual("New Game, button, 2 of 3", Readout(Section(TooltipMode.Announce, "New Game")));
        }

        /// <summary>Every other part of the readout counts, not just the label: a value read off the
        /// same tooltip is the other way a control comes to say one line twice.</summary>
        [TestMethod]
        public void ALineAnotherPartAlreadySaysIsDroppedToo()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(
                Id("t"),
                new NodeVtable
                {
                    ControlType = Type("button", "button"),
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("Steam Cloud", AnnouncementKinds.Label),
                        Part("Not running", AnnouncementKinds.Value),
                    },
                    Sections = new[]
                    {
                        Section(TooltipMode.Announce, "Steam Cloud", "Not running", "Saves stay local"),
                    },
                }
            ));
            Assert.AreEqual(
                "Steam Cloud, button, Not running, Saves stay local",
                GraphAnnouncer.LeafText(Node(b.Build(), "t"))
            );
        }

        /// <summary>Both sides are resolved at SPEAK time: a label read off the tooltip's first line
        /// changes when the tooltip does, and a dedupe settled at declare time would go on dropping
        /// last turn's sentence.</summary>
        [TestMethod]
        public void TheDedupeIsResolvedAtSpeakTimeOnBothSides()
        {
            string name = "Colonize";
            List<string> lines = new List<string> { "Colonize", "This world is barren" };
            NodeAnnouncement label = new NodeAnnouncement(() => name, kind: AnnouncementKinds.Label);
            NodeVtable vt = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { label },
                Sections = new[] { NodeSection.Derived(() => lines, TooltipMode.Announce, null) },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("t"), vt));
            GraphNode node = Node(b.Build(), "t");
            Assert.AreEqual("Colonize, This world is barren", GraphAnnouncer.LeafText(node));

            name = "This world is barren";
            lines[1] = "Outpost first";
            Assert.AreEqual(
                "This world is barren, Colonize Outpost first",
                GraphAnnouncer.LeafText(node)
            );
        }

        // ---- composed words and a tooltip are not in competition ----

        /// <summary>A control can have BOTH: words the mod went and got out of the model (a report
        /// row's outcome sentence) and a tooltip of its own whose kind says it speaks. Neither may
        /// swallow the other - "the last one wins" is about which TOOLTIP a control points at, and
        /// composed words are not a tooltip.</summary>
        [TestMethod]
        public void ComposedWordsAndAnAnnouncingTooltipBothSpeakInDeclaredOrder()
        {
            Assert.AreEqual(
                "New Game, button, A crushing defeat, What the fleet lost, 2 of 3",
                Readout(
                    NodeSection.Composed(Tooltip("A crushing defeat")),
                    Section(TooltipMode.Announce, "What the fleet lost")
                )
            );
        }

        /// <summary>And the competition still holds among the TOOLTIPS: a control points at one, so
        /// the last of them is the only one heard, whatever else is declared beside them.</summary>
        [TestMethod]
        public void OnlyTheLastTooltipSpeaksEvenBesideComposedWords()
        {
            Assert.AreEqual(
                "New Game, button, A crushing defeat, The value's own, 2 of 3",
                Readout(
                    NodeSection.Composed(Tooltip("A crushing defeat")),
                    Section(TooltipMode.Announce, "The caption's"),
                    Section(TooltipMode.Announce, "The value's own")
                )
            );
        }

        /// <summary>Where the two overlap - a sentence the row composed out of the same words its
        /// tooltip carries - the later section's copy comes out, so the readout says it once.</summary>
        [TestMethod]
        public void ALineAnEarlierSectionAlreadySpokeIsNotSaidAgainByALaterOne()
        {
            Assert.AreEqual(
                "New Game, button, A crushing defeat, What the fleet lost, 2 of 3",
                Readout(
                    NodeSection.Composed(Tooltip("A crushing defeat")),
                    Section(TooltipMode.Announce, "a crushing defeat", "What the fleet lost")
                )
            );
        }

        /// <summary>The dedupe is the ANNOUNCEMENT's alone. Nothing is taken out of the declaration,
        /// so the tooltip part built without the rest of the readout still says every line - which is
        /// what the parity audit and the buffer read.</summary>
        [TestMethod]
        public void ThePartBuiltWithNothingElseInHandStillSaysEveryLine()
        {
            NodeAnnouncement part = TooltipParts.Part(
                new[] { Section(TooltipMode.Announce, "New Game", "Skips setup") }
            );
            Assert.AreEqual("New Game Skips setup", part.Text());
        }
    }
}
