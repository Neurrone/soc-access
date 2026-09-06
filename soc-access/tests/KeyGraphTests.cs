using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The engine: stepping, Tab-stop and region operations, tree semantics, and — the part that earns
    /// the two-tier identity — where focus lands after the world was rebuilt under it.
    /// </summary>
    [TestClass]
    public class KeyGraphTests
    {
        private static string Focused(KeyGraph g)
        {
            return Key(g.CurrentNode);
        }

        // ---- stepping ----

        [TestMethod]
        public void FocusStartsAtTheStartNode()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")))), state);
            Assert.IsTrue(g.Rerender());
            Assert.AreEqual("a", Focused(g));
        }

        [TestMethod]
        public void MoveReportsTheCrossedEdgeLabel()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.AddNode(new SyntheticNode(Id("a"), Vt("A"))).AddNode(new SyntheticNode(Id("b"), Vt("B")));
                b.Connect(Id("a"), GraphDir.Right, Id("b"), "Ships");
            }), state);
            g.Rerender();
            MoveResult r = g.Move(GraphDir.Right);
            Assert.IsTrue(r.Moved);
            Assert.AreEqual("Ships", r.TransitionLabel);
            Assert.AreEqual("b", Key(r.To));
            Assert.AreEqual("a", Key(r.From));
        }

        [TestMethod]
        public void MoveAtAnEdgeReportsNotMovedAndKeepsFocus()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")))), state);
            g.Rerender();
            MoveResult r = g.Move(GraphDir.Up);
            Assert.IsFalse(r.Moved);
            Assert.AreSame(r.From, r.To);
            Assert.AreEqual("a", Focused(g));
        }

        [TestMethod]
        public void MoveToEdgeRunsToTheEndOfTheLine()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).AddItem(new SyntheticNode(Id("c"), Vt("C")))), state);
            g.Rerender();
            Assert.IsTrue(g.MoveToEdge(GraphDir.Down).Moved);
            Assert.AreEqual("c", Focused(g));
        }

        // ---- tab stops ----

        private static KeyGraph TwoStops(GraphState state)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.BeginStop("s1").AddItem(new SyntheticNode(Id("a1"), Vt("A1"))).AddItem(new SyntheticNode(Id("a2"), Vt("A2")));
                b.BeginStop("s2").AddItem(new SyntheticNode(Id("b1"), Vt("B1"))).AddItem(new SyntheticNode(Id("b2"), Vt("B2")));
            }), state);
        }

        [TestMethod]
        public void MoveStopCyclesStopsInFirstAppearanceOrder()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            Assert.IsTrue(g.MoveStop(1, false).Moved);
            Assert.AreEqual("b1", Focused(g));
        }

        [TestMethod]
        public void MoveStopStopsAtTheLastStopWithoutWrap()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            g.MoveStop(1, false);
            Assert.IsFalse(g.MoveStop(1, false).Moved);
            Assert.AreEqual("b1", Focused(g));
        }

        [TestMethod]
        public void MoveStopWrapsWhenAsked()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            g.MoveStop(1, false);
            Assert.IsTrue(g.MoveStop(1, true).Moved);
            Assert.AreEqual("a1", Focused(g));
        }

        [TestMethod]
        public void MoveStopWrapsBackwardsFromTheFirstStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            Assert.IsTrue(g.MoveStop(-1, true).Moved);
            Assert.AreEqual("b1", Focused(g));
        }

        // What Tab does on a page with one panel: nothing. Wrapping round to the stop the player is
        // already on is not a move, so the key is consumed and says nothing rather than re-reading the
        // same control (GraphNavigator.Stop).
        [TestMethod]
        public void MoveStopWithOnlyOneStopNeverMoves()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.BeginStop("s1").AddItem(new SyntheticNode(Id("a1"), Vt("A1"))).AddItem(new SyntheticNode(Id("a2"), Vt("A2")))), state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("a2", Focused(g));
            Assert.IsFalse(g.MoveStop(1, true).Moved);
            Assert.IsFalse(g.MoveStop(-1, true).Moved);
            Assert.AreEqual("a2", Focused(g));
        }

        [TestMethod]
        public void ReturningToAStopLandsOnItsRememberedPosition()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            g.MoveStop(1, false);
            g.Move(GraphDir.Down);
            Assert.AreEqual("b2", Focused(g));
            g.MoveStop(-1, false);
            Assert.AreEqual("a1", Focused(g));
            g.MoveStop(1, false);
            Assert.AreEqual("b2", Focused(g));
        }

        [TestMethod]
        public void InitialFocusPrefersTheSelectedMemberOfTheStartStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.AddItem(new SyntheticNode(Id("a"), Vt("A", Part(null, AnnouncementKinds.Selected))));
                b.AddItem(new SyntheticNode(Id("b"), Vt("B", Part("selected", AnnouncementKinds.Selected))));
                b.AddItem(new SyntheticNode(Id("c"), Vt("C", Part(null, AnnouncementKinds.Selected))));
            }), state);
            g.Rerender();
            Assert.AreEqual("b", Focused(g));
        }

        /// <summary>A start node that is not one of the alternatives keeps focus: a popup's block of
        /// text is where the screen wants reading to begin, and the dots marking which page it is on
        /// merely share its stop.</summary>
        [TestMethod]
        public void InitialFocusStaysOnAStartThatIsNotOneOfTheAlternatives()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.AddNode(new SyntheticNode(Id("text"), Vt("Some words")));
                b.SetStart(Id("text"));
                b.AddItem(new SyntheticNode(Id("dot1"), Vt("Page 1", Part("selected", AnnouncementKinds.Selected))));
                b.AddItem(new SyntheticNode(Id("dot2"), Vt("Page 2", Part(null, AnnouncementKinds.Selected))));
            }), state);
            g.Rerender();
            Assert.AreEqual("text", Focused(g));
        }

        /// <summary>A stop whose first nodes are not what the player came for - a table's sort headings,
        /// where the SORTED column reads "selected" - says where Tab lands, and the
        /// land-on-the-selected-one rule runs from there rather than over the headings.</summary>
        private static KeyGraph TableUnderHeadings(GraphState state, bool rowSelected)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.BeginStop("elsewhere").AddItem(new SyntheticNode(Id("away"), Vt("Away")));
                b.BeginStop("table");
                b.AddItem(new SyntheticNode(Id("head1"), Vt("Name", Part("selected", AnnouncementKinds.Selected))));
                b.AddItem(new SyntheticNode(Id("head2"), Vt("Ships", Part(null, AnnouncementKinds.Selected))));
                b.AddItem(new SyntheticNode(Id("row1"), Vt("Alpha", Part(null, AnnouncementKinds.Selected))));
                b.AddItem(new SyntheticNode(
                    Id("row2"),
                    Vt("Beta", Part(rowSelected ? "selected" : null, AnnouncementKinds.Selected))
                ));
                b.LandStopOn(Id("row1"));
            }), state);
        }

        [TestMethod]
        public void ATabStopLandsWhereItSaidRatherThanOnItsFirstNode()
        {
            GraphState state = new GraphState();
            KeyGraph g = TableUnderHeadings(state, false);
            g.Rerender();
            Assert.IsTrue(g.MoveStop(1, true).Moved);
            Assert.AreEqual("row1", Focused(g));
        }

        [TestMethod]
        public void ATabStopWithADeclaredLandingStillPrefersASelectedNodeBelowIt()
        {
            GraphState state = new GraphState();
            KeyGraph g = TableUnderHeadings(state, true);
            g.Rerender();
            Assert.IsTrue(g.MoveStop(1, true).Moved);
            Assert.AreEqual("row2", Focused(g));
        }

        // ---- regions ----

        [TestMethod]
        public void MoveRegionJumpsToTheNextRegionOfTheSameStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.SetRegion("r1").AddItem(new SyntheticNode(Id("a1"), Vt("A1"))).AddItem(new SyntheticNode(Id("a2"), Vt("A2")));
                b.SetRegion("r2").AddItem(new SyntheticNode(Id("b1"), Vt("B1"))).AddItem(new SyntheticNode(Id("b2"), Vt("B2")));
            }), state);
            g.Rerender();
            Assert.IsTrue(g.MoveRegion(1).Moved);
            Assert.AreEqual("b1", Focused(g));
            Assert.IsFalse(g.MoveRegion(1).Moved);
            Assert.IsTrue(g.MoveRegion(-1).Moved);
            Assert.AreEqual("a1", Focused(g));
        }

        [TestMethod]
        public void MoveRegionNeverLeavesTheCurrentStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.SetRegion("r1").AddItem(new SyntheticNode(Id("a1"), Vt("A1")));
                b.BeginStop("s2").SetRegion("r2").AddItem(new SyntheticNode(Id("b1"), Vt("B1")));
            }), state);
            g.Rerender();
            Assert.IsFalse(g.MoveRegion(1).Moved);
            Assert.AreEqual("a1", Focused(g));
        }

        // ---- jumping to a named stop ----

        /// <summary>The availability question a global "go to that panel" key asks, and the claim it is
        /// taken from the game by. It has to agree with the landing walk: a stop that is there has a
        /// landing, and one that is not has neither.</summary>
        [TestMethod]
        public void DeclaresStopAgreesWithTheLanding()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.BeginStop("s1").AddItem(new SyntheticNode(Id("a1"), Vt("A1")));
                b.BeginStop("s2").AddItem(new SyntheticNode(Id("b1"), Vt("B1")));
            }), state);
            g.Rerender();

            Assert.IsTrue(KeyGraph.DeclaresStop(g.Current, "s2"));
            Assert.IsNotNull(KeyGraph.StopLanding(g.Current, state, "s2"));

            Assert.IsFalse(KeyGraph.DeclaresStop(g.Current, "nowhere"));
            Assert.IsNull(KeyGraph.StopLanding(g.Current, state, "nowhere"));
            Assert.IsFalse(KeyGraph.DeclaresStop(null, "s1"));
        }

        // ---- trees ----

        private static KeyGraph Tree(GraphState state, bool withChildren = true)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.AddItem(new SyntheticNode(Id("top"), Vt("Top")));
                b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
                if (withChildren)
                {
                    b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
                    b.AddItem(new SyntheticNode(Id("c2"), Vt("Child 2")));
                }
                b.EndGroup();
                b.AddItem(new SyntheticNode(Id("bottom"), Vt("Bottom")));
            }, state), state);
        }

        // One press opens the group AND lands on its first child: the player hears the child and its
        // position, never the header's "expanded".
        [TestMethod]
        public void TreeRightOpensAndDescendsInOnePress()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("g", Focused(g));

            KeyGraph.TreeResult open = g.TreeRight();
            Assert.AreEqual(KeyGraph.TreeMove.Descended, open.Kind);
            Assert.IsTrue(state.Expanded.Contains(Id("g")));
            Assert.AreEqual("g", Key(open.Move.From));
            Assert.AreEqual("c1", Key(open.Move.To));
            Assert.AreEqual("c1", Focused(g));
        }

        // A group whose children only appear once the GAME has caught up: the first render of the open
        // branch holds "c1, c2", and every render after the flip is flipped holds an "open" button in
        // FRONT of them - which is what a system's row of buttons does when the camera comes in.
        private static KeyGraph SettlingTree(GraphState state, bool[] settled, bool keepChildren = true)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.AddItem(new SyntheticNode(Id("top"), Vt("Top")));
                b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
                if (settled[0])
                {
                    if (keepChildren)
                    {
                        b.AddItem(new SyntheticNode(Id("open"), Vt("Open system")));
                    }
                }

                if (!settled[0] || keepChildren)
                {
                    b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
                    b.AddItem(new SyntheticNode(Id("c2"), Vt("Child 2")));
                }

                b.EndGroup();
            }, state), state);
        }

        // The press says which group it opened, so the caller can come back to it once the page it
        // acted on has settled. A descend into a branch that was ALREADY open opened nothing and says
        // so - there is nothing provisional about it.
        [TestMethod]
        public void TreeRightNamesTheGroupItOpened()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);

            KeyGraph.TreeResult open = g.TreeRight();
            Assert.AreEqual("g", Key(open.Opened));

            g.Move(GraphDir.Up);
            Assert.AreEqual("g", Focused(g));
            Assert.IsNull(g.TreeRight().Opened);
        }

        // The descend re-made against the settled build lands on the first child the settled build has,
        // not the one the half-built list started with.
        [TestMethod]
        public void TreeDescendRemakesTheDescendAgainstTheSettledBuild()
        {
            GraphState state = new GraphState();
            bool[] settled = { false };
            KeyGraph g = SettlingTree(state, settled);
            g.Rerender();
            g.Move(GraphDir.Down);

            KeyGraph.TreeResult open = g.TreeRight();
            Assert.AreEqual(KeyGraph.TreeMove.Descended, open.Kind);
            Assert.AreEqual("c1", Focused(g));

            settled[0] = true;
            KeyGraph.TreeResult again = g.TreeDescend(open.Opened.Id);
            Assert.AreEqual(KeyGraph.TreeMove.Descended, again.Kind);
            Assert.AreEqual("g", Key(again.Move.From));
            Assert.AreEqual("open", Key(again.Move.To));
            Assert.AreEqual("open", Focused(g));
        }

        // On a page that never changed, the re-made descend is the descend that was already made: the
        // same node, announced once.
        [TestMethod]
        public void TreeDescendOnASettledPageLandsWhereItAlreadyIs()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);

            KeyGraph.TreeResult open = g.TreeRight();
            KeyGraph.TreeResult again = g.TreeDescend(open.Opened.Id);
            Assert.AreEqual(KeyGraph.TreeMove.Descended, again.Kind);
            Assert.AreEqual("c1", Key(again.Move.To));
            Assert.AreEqual("c1", Focused(g));
        }

        // A branch that has lost every child by the time the page settles is the "no details" the
        // provisional descend was too early to judge.
        [TestMethod]
        public void TreeDescendReportsAnEmptyGroupWhenTheChildrenHaveGone()
        {
            GraphState state = new GraphState();
            bool[] settled = { false };
            KeyGraph g = SettlingTree(state, settled, false);
            g.Rerender();
            g.Move(GraphDir.Down);

            KeyGraph.TreeResult open = g.TreeRight();
            Assert.AreEqual(KeyGraph.TreeMove.Descended, open.Kind);

            settled[0] = true;
            Assert.AreEqual(KeyGraph.TreeMove.EmptyGroup, g.TreeDescend(open.Opened.Id).Kind);
            Assert.IsTrue(state.Expanded.Contains(Id("g")));
        }

        // And a group that is no longer declared at all answers nothing: something else has changed the
        // page, and wherever the cursor has been reconciled to is the answer.
        [TestMethod]
        public void TreeDescendOnAGroupThatHasGoneAnswersNone()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();

            Assert.AreEqual(KeyGraph.TreeMove.None, g.TreeDescend(Id("nowhere")).Kind);
            Assert.AreEqual(KeyGraph.TreeMove.None, g.TreeDescend(null).Kind);
            Assert.AreEqual("c1", Focused(g));
        }

        // And an already-open group answers Right exactly the same way, which is what makes the two
        // states of a group indistinguishable to the key.
        [TestMethod]
        public void TreeRightOnAnOpenGroupDescends()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeLeft(); // back up, shutting it
            Assert.AreEqual("g", Focused(g));

            state.Expanded.Add(Id("g"));
            g.Rerender();
            Assert.AreEqual(KeyGraph.TreeMove.Descended, g.TreeRight().Kind);
            Assert.AreEqual("c1", Focused(g));
        }

        // One press goes up AND shuts the branch behind it. The parent is announced, and it is a
        // COLLAPSED parent that is announced.
        [TestMethod]
        public void TreeLeftAscendsAndCollapsesInOnePress()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            Assert.AreEqual("c1", Focused(g));

            KeyGraph.TreeResult up = g.TreeLeft();
            Assert.AreEqual(KeyGraph.TreeMove.Ascended, up.Kind);
            Assert.AreEqual("g", Key(up.Move.To));
            Assert.IsFalse(up.Move.To.Expanded);
            Assert.IsFalse(state.Expanded.Contains(Id("g")));
            Assert.AreEqual("g", Focused(g));
        }

        // Left on the header itself is still the plain collapse, cursor unmoved - which is how a group
        // opened on an empty answer, or one walked back into with Up, is shut.
        [TestMethod]
        public void TreeLeftOnTheHeaderCollapsesWithoutMoving()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.Move(GraphDir.Up);
            Assert.AreEqual("g", Focused(g));

            Assert.AreEqual(KeyGraph.TreeMove.Collapsed, g.TreeLeft().Kind);
            Assert.IsFalse(state.Expanded.Contains(Id("g")));
            Assert.AreEqual("g", Focused(g));
        }

        [TestMethod]
        public void ExpansionSurvivesRebuilds()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            Assert.IsTrue(g.Rerender());
            Assert.IsNotNull(g.Current.NodeAt(Id("c2")));
            Assert.IsTrue(g.Current.NodeAt(Id("g")).Expanded);
        }

        // A group that turns out to be empty stays OPEN, with the cursor still on it: expanding is
        // allowed to act (a map node's expansion brings the camera in) and bouncing it shut would undo
        // that. Left is what shuts it.
        [TestMethod]
        public void AnEmptyGroupStaysOpen()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state, false);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual(KeyGraph.TreeMove.EmptyGroup, g.TreeRight().Kind);
            Assert.IsTrue(state.Expanded.Contains(Id("g")));
            Assert.IsTrue(g.Current.NodeAt(Id("g")).Expanded);
            Assert.AreEqual("g", Focused(g));

            Assert.AreEqual(KeyGraph.TreeMove.Collapsed, g.TreeLeft().Kind);
            Assert.IsFalse(state.Expanded.Contains(Id("g")));
        }

        // And Right again on the open-but-empty group is an ordinary consumed press, not a second
        // "Nothing in here".
        [TestMethod]
        public void RightAgainOnAnEmptyOpenGroupIsALeaf()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state, false);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            Assert.AreEqual(KeyGraph.TreeMove.Leaf, g.TreeRight().Kind);
        }

        // A group whose children all sit under a NAMED SECTION - the shape the "Tooltips" region of a
        // dossier-bearing node has, and the one a technology dot has when everything it holds is a
        // dossier. The section is a context: non-focusable, so it is the children's parent and the
        // group is their grandparent. Comparing parents alone called the group empty and
        // auto-recollapsed it, which is "Nothing in here" said over a group full of nodes.
        private static KeyGraph Sectioned(GraphState state)
        {
            return new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
                        b.PushContext("Tooltips");
                        b.AddItem(new SyntheticNode(Id("t1"), Vt("Dossier 1")));
                        b.AddItem(new SyntheticNode(Id("t2"), Vt("Dossier 2")));
                        b.PopContext();
                        b.EndGroup();
                    },
                    state
                ),
                state
            );
        }

        [TestMethod]
        public void AGroupWhoseChildrenAreAllInASectionIsNotEmpty()
        {
            GraphState state = new GraphState();
            KeyGraph g = Sectioned(state);
            g.Rerender();
            Assert.AreEqual("g", Focused(g));

            // One press opens the group AND lands on its first child (the single-press contract).
            KeyGraph.TreeResult descend = g.TreeRight();
            Assert.AreEqual(KeyGraph.TreeMove.Descended, descend.Kind);
            Assert.IsTrue(state.Expanded.Contains(Id("g")));
            Assert.AreEqual("t1", Focused(g));
        }

        [TestMethod]
        public void DescendingSkipsPastANestedGroupsOwnChildren()
        {
            GraphState state = new GraphState();
            state.Expanded.Add(Id("outer"));
            state.Expanded.Add(Id("inner"));
            KeyGraph g = new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.BeginGroup(new SyntheticNode(Id("outer"), Vt("Outer")));
                        b.BeginGroup(new SyntheticNode(Id("inner"), Vt("Inner")));
                        b.AddItem(new SyntheticNode(Id("deep"), Vt("Deep")));
                        b.EndGroup();
                        b.EndGroup();
                    },
                    state
                ),
                state
            );
            g.Rerender();
            Assert.AreEqual("outer", Focused(g));
            Assert.AreEqual(KeyGraph.TreeMove.Descended, g.TreeRight().Kind);
            Assert.AreEqual("inner", Focused(g));
        }

        // ---- following a reference (a leaf that names somewhere else) ----

        /// <summary>A tree whose second child NAMES the top-level node rather than holding anything of
        /// its own - the shape a starlane has, pointing at the system it runs to.</summary>
        private static KeyGraph FollowTree(GraphState state, List<string> followed, bool expandable)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.AddItem(new SyntheticNode(Id("top"), Vt("Top")));
                b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
                b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
                NodeVtable lane = Vt("Lane");
                lane.OnFollow = () => followed.Add("followed");
                if (expandable)
                {
                    b.BeginGroup(new SyntheticNode(Id("lane"), lane));
                    b.AddItem(new SyntheticNode(Id("far"), Vt("Far")));
                    b.EndGroup();
                }
                else
                {
                    b.AddItem(new SyntheticNode(Id("lane"), lane));
                }
                b.EndGroup();
            }, state), state);
        }

        // Right on the leaf runs the handler and reports Followed - the handler moves focus itself, so
        // the engine leaves the cursor exactly where it was and says nothing.
        [TestMethod]
        public void TreeRightFollowsALeafThatNamesSomewhereElse()
        {
            GraphState state = new GraphState();
            List<string> followed = new List<string>();
            KeyGraph g = FollowTree(state, followed, false);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            g.Move(GraphDir.Down);
            Assert.AreEqual("lane", Focused(g));

            Assert.AreEqual(KeyGraph.TreeMove.Followed, g.TreeRight().Kind);
            Assert.AreEqual(1, followed.Count);
            Assert.AreEqual("lane", Focused(g));
        }

        // The same leaf without a handler is an ordinary Leaf: consumed, nothing run.
        [TestMethod]
        public void TreeRightOnALeafWithoutOneStaysALeaf()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            Assert.AreEqual("c1", Focused(g));
            Assert.AreEqual(KeyGraph.TreeMove.Leaf, g.TreeRight().Kind);
        }

        // A node that has children of its own is not standing in for somewhere else: its own expansion
        // wins and the follow handler is never asked.
        [TestMethod]
        public void AnExpandableNodeIgnoresItsFollowHandler()
        {
            GraphState state = new GraphState();
            List<string> followed = new List<string>();
            KeyGraph g = FollowTree(state, followed, true);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.Move(GraphDir.Down);
            Assert.AreEqual("lane", Focused(g));

            Assert.AreEqual(KeyGraph.TreeMove.Descended, g.TreeRight().Kind);
            Assert.AreEqual("far", Focused(g));
            Assert.AreEqual(0, followed.Count);
        }

        /// <summary>Two panels, each with a group at the top level: Home and End inside one of them are
        /// about that panel and never reach into the other. The trap is that a top-level node has no
        /// parent, so "same parent" alone made every root-level node on the page a sibling.</summary>
        private static KeyGraph TwoStopTrees(GraphState state)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.BeginStop("s1");
                b.AddItem(new SyntheticNode(Id("a1"), Vt("A1")));
                b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
                b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
                b.AddItem(new SyntheticNode(Id("c2"), Vt("Child 2")));
                b.EndGroup();
                b.AddItem(new SyntheticNode(Id("a2"), Vt("A2")));
                b.BeginStop("s2");
                b.AddItem(new SyntheticNode(Id("b1"), Vt("B1")));
                b.AddItem(new SyntheticNode(Id("b2"), Vt("B2")));
            }, state), state);
        }

        [TestMethod]
        public void EndOnAnExpandedGroupStaysInItsOwnStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStopTrees(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("g", Focused(g));
            Assert.AreEqual(KeyGraph.TreeMove.Descended, g.TreeRight().Kind);
            g.Move(GraphDir.Up); // back onto the header, the branch left open
            Assert.AreEqual("g", Focused(g));

            MoveResult end = g.MoveToSiblingEdge(false);
            Assert.IsTrue(end.Moved);
            Assert.AreEqual("a2", Key(end.To));

            MoveResult home = g.MoveToSiblingEdge(true);
            Assert.IsTrue(home.Moved);
            Assert.AreEqual("a1", Key(home.To));
        }

        [TestMethod]
        public void EndOnAChildStaysAmongItsSiblings()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStopTrees(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            Assert.AreEqual("c1", Focused(g));

            Assert.AreEqual("c2", Key(g.MoveToSiblingEdge(false).To));
            Assert.AreEqual("c1", Key(g.MoveToSiblingEdge(true).To));
        }

        [TestMethod]
        public void SiblingEdgesInAOneNodeStopMoveNothing()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.BeginStop("s1");
                b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
                b.AddItem(new SyntheticNode(Id("c1"), Vt("Child")));
                b.EndGroup();
                b.BeginStop("s2").AddItem(new SyntheticNode(Id("z"), Vt("Z")));
            }, state), state);
            g.Rerender();
            Assert.AreEqual("g", Focused(g));
            Assert.IsFalse(g.MoveToSiblingEdge(false).Moved);
            Assert.IsFalse(g.MoveToSiblingEdge(true).Moved);
            Assert.AreEqual("g", Focused(g));
        }

        [TestMethod]
        public void TreeMovesOutsideATreeReportNone()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            Assert.AreEqual("top", Focused(g));
            Assert.AreEqual(KeyGraph.TreeMove.None, g.TreeRight().Kind);
            Assert.AreEqual(KeyGraph.TreeMove.None, g.TreeLeft().Kind);
        }

        [TestMethod]
        public void RightOnALeafInsideATreeIsConsumed()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            Assert.AreEqual(KeyGraph.TreeMove.Leaf, g.TreeRight().Kind);
        }

        [TestMethod]
        public void ExpansionGoesThroughTheVtableOverrideWhenDeclared()
        {
            GraphState state = new GraphState();
            bool expanded = false;
            NodeVtable header = Vt("Group");
            header.OnExpand = () => expanded = true;
            header.OnCollapse = () => expanded = false;

            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder(state.Expanded);
                b.BeginGroup(new SyntheticNode(Id("g"), header), expanded);
                b.AddItem(new SyntheticNode(Id("c1"), Vt("Child")));
                b.EndGroup();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.AreEqual(KeyGraph.TreeMove.Descended, g.TreeRight().Kind);
            Assert.IsTrue(expanded);
            Assert.AreEqual("c1", Focused(g));
            Assert.AreEqual(0, state.Expanded.Count); // the persistent set stays out of it
        }

        // ---- reconciliation ----

        [TestMethod]
        public void FocusFollowsTheBackingObjectWhenItsStructuralKeyChanges()
        {
            GraphState state = new GraphState();
            object thing = new object();
            string key = "slot1";
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(new SyntheticNode(Id("other"), Vt("Other")));
                b.AddItem(new SyntheticNode(ControlId.For(thing, key), Vt("Thing")));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("slot1", Focused(g));

            key = "slot9"; // the object moved
            g.Rerender();
            Assert.AreEqual("slot9", Focused(g));
        }

        [TestMethod]
        public void FocusFollowsTheStructuralKeyWhenTheBackingObjectIsRebuilt()
        {
            GraphState state = new GraphState();
            object thing = new object();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(new SyntheticNode(Id("other"), Vt("Other")));
                b.AddItem(new SyntheticNode(ControlId.For(thing, "slot1"), Vt("Thing")));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);

            thing = new object(); // same logical control, fresh instance
            g.Rerender();
            Assert.AreEqual("slot1", Focused(g));
            Assert.AreSame(thing, g.CurrentNode.Id.Subject);
        }

        [TestMethod]
        public void TwoNodesSharingABackingObjectAreOneControlToTheCursor()
        {
            // The consequence of following the reference BEFORE the structural key, pinned here
            // because it is the trap adapters keep walking into: two surfaces that show the same
            // entity and both carry it as a reference are indistinguishable to reconciliation, so the
            // cursor lands on whichever one comes first and the player is teleported off the surface
            // they were reading. ES2 Access hit it twice - a research-queue row against its wheel
            // node, and the two ends of one starlane, each declared under its own system. Where two
            // nodes show one entity, at most one of them may carry the reference.
            GraphState state = new GraphState();
            object thing = new object();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(new SyntheticNode(ControlId.For(thing, "here"), Vt("Here")));
                b.AddItem(new SyntheticNode(ControlId.For(thing, "there"), Vt("There")));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("there", Focused(g));

            g.Rerender();
            Assert.AreEqual("here", Focused(g));
        }

        [TestMethod]
        public void AVanishedControlFallsBackToTheNearestSurvivor()
        {
            GraphState state = new GraphState();
            bool withC = true;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(new SyntheticNode(Id("a"), Vt("A")));
                b.AddItem(new SyntheticNode(Id("b"), Vt("B")));
                if (withC) b.AddItem(new SyntheticNode(Id("c"), Vt("C")));
                b.AddItem(new SyntheticNode(Id("d"), Vt("D")));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.Move(GraphDir.Down);
            Assert.AreEqual("c", Focused(g));

            withC = false;
            g.Rerender();
            Assert.AreEqual("b", Focused(g)); // the survivor before it in the previous order
        }

        /// <summary>The Create-button shape: the control under the cursor is destroyed by pressing it.
        /// Recovery is the same backward walk it always was - pinned here because the stop memory is now
        /// rewritten on the same rebuild, and it must follow the cursor rather than fight it.</summary>
        [TestMethod]
        public void TheFocusedControlDyingTakesItsStopMemoryWithIt()
        {
            GraphState state = new GraphState();
            bool withButton = true;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.BeginStop("band");
                b.AddItem(new SyntheticNode(Id("band/split"), Vt("Split")));
                if (withButton) b.AddItem(new SyntheticNode(Id("mgmt/create"), Vt("Create")));
                b.AddItem(new SyntheticNode(Id("band/merge"), Vt("Merge")));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("mgmt/create", Focused(g));

            withButton = false; // pressing Create is what destroyed it
            g.Rerender();
            Assert.AreEqual("band/split", Focused(g));
            Assert.AreEqual(Id("band/split"), state.StopMemory["band"]);
        }

        /// <summary>The disband shape: the control dies while the player stands in ANOTHER stop, so no
        /// cursor reconciliation ever sees it - only the stop's memory does. Coming back must land beside
        /// where the player was, not at the top of the tree.</summary>
        [TestMethod]
        public void AStopRemembersTheNeighbourOfAControlThatDiedWhileAway()
        {
            GraphState state = new GraphState();
            bool withFleet = true;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.BeginStop("map");
                b.AddItem(new SyntheticNode(Id("system/585"), Vt("Ingris")));
                b.AddItem(new SyntheticNode(Id("system/585/fleet/1"), Vt("Scout")));
                if (withFleet) b.AddItem(new SyntheticNode(Id("system/585/fleet/2"), Vt("Doomed")));
                b.BeginStop("panel");
                b.AddItem(new SyntheticNode(Id("panel/hangar"), Vt("Hangar")));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.Move(GraphDir.Down);
            Assert.AreEqual("system/585/fleet/2", Focused(g));
            g.MoveStop(1, true);
            Assert.AreEqual("panel/hangar", Focused(g));

            withFleet = false; // disbanded from the panel, which stays open
            g.Rerender();
            g.MoveStop(-1, true);
            Assert.AreEqual("system/585/fleet/1", Focused(g)); // its sibling, not "system/585" the stop's first node
        }

        /// <summary>The map's own case: a zoom step stops the picture drawing fleets at all, so the
        /// fleet row the cursor was on dies together with every other fleet row. The survivor beside it
        /// is a lane - a different thing - while the system it was parked at is the same thing read
        /// from further off, which is where the cursor belongs.</summary>
        private static KeyGraph BandedMap(GraphState state, Func<bool> fleets)
        {
            return new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.SeatOnContainer = !fleets();
                b.BeginGroup(new SyntheticNode(Id("sky/1"), Vt("Serpens")), expanded: true);
                b.BeginGroup(new SyntheticNode(Id("sky/1/system/5"), Vt("Osulo")), expanded: true);
                b.AddItem(new SyntheticNode(Id("sky/1/system/5/lane/0"), Vt("Lane to Kais")));
                if (fleets())
                {
                    b.AddItem(new SyntheticNode(Id("sky/1/system/5/fleet/0"), Vt("Scout")));
                }

                b.EndGroup();
                b.EndGroup();
                return b.Build();
            }, state);
        }

        [TestMethod]
        public void ABuildShowingFewerKindsSeatsALostCursorOnWhatContainedIt()
        {
            GraphState state = new GraphState();
            bool fleets = true;
            KeyGraph g = BandedMap(state, () => fleets);
            g.Rerender();
            Assert.IsTrue(g.Focus(Id("sky/1/system/5/fleet/0")));

            fleets = false;
            g.Rerender();
            Assert.AreEqual("sky/1/system/5", Focused(g));
        }

        [TestMethod]
        public void OneRowGoingAwayStillLandsOnTheNeighbourBesideIt()
        {
            GraphState state = new GraphState();
            bool fleets = true;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.BeginGroup(new SyntheticNode(Id("sky/1"), Vt("Serpens")), expanded: true);
                b.BeginGroup(new SyntheticNode(Id("sky/1/system/5"), Vt("Osulo")), expanded: true);
                b.AddItem(new SyntheticNode(Id("sky/1/system/5/lane/0"), Vt("Lane to Kais")));
                if (fleets)
                {
                    b.AddItem(new SyntheticNode(Id("sky/1/system/5/fleet/0"), Vt("Scout")));
                }

                b.EndGroup();
                b.EndGroup();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.IsTrue(g.Focus(Id("sky/1/system/5/fleet/0")));

            // The same row dying with the shape UNCHANGED - a fleet disbanded - keeps the old answer.
            fleets = false;
            g.Rerender();
            Assert.AreEqual("sky/1/system/5/lane/0", Focused(g));
        }

        [TestMethod]
        public void AStopMemoryOfALostRowComesBackOnWhatContainedIt()
        {
            GraphState state = new GraphState();
            bool fleets = true;
            KeyGraph g = BandedMap(state, () => fleets);
            g.Rerender();
            Assert.IsTrue(g.Focus(Id("sky/1/system/5/fleet/0")));
            object stop = g.CurrentNode.StopKey;

            fleets = false;
            g.Rerender();
            Assert.AreEqual(Id("sky/1/system/5"), state.StopMemory[stop]);
        }

        [TestMethod]
        public void RepairingAStopMemorySkipsSurvivorsFromOtherStops()
        {
            GraphState state = new GraphState();
            GraphRender render = TwoStopRender();
            ControlId dead = Id("m3");
            state.CurKey = Id("o1"); // the player is standing in the other stop
            state.StopMemory["map"] = dead;
            state.KeyOrder = new List<ControlId> { Id("m1"), Id("m2"), Id("o1"), dead };

            KeyGraph.Reconcile(render, state);

            Assert.AreEqual(Id("m2"), state.StopMemory["map"]); // "o1" is nearer, and belongs elsewhere
        }

        /// <summary>A stop absent from this render - a hidden panel, a modal up - is not a stop whose
        /// control died: leave the memory alone, because the stop may return with the very keys it
        /// names, and it does.</summary>
        [TestMethod]
        public void AStopMissingFromTheRenderKeepsItsMemory()
        {
            GraphState state = new GraphState();
            GraphRender hidden = Renderer(b => b.BeginStop("other").AddItem(new SyntheticNode(Id("o1"), Vt("O1"))))();
            ControlId remembered = Id("m2");
            state.CurKey = Id("o1");
            state.StopMemory["map"] = remembered;
            state.KeyOrder = new List<ControlId> { Id("m1"), remembered, Id("o1") };

            KeyGraph.Reconcile(hidden, state);
            Assert.AreEqual(remembered, state.StopMemory["map"]);

            Assert.AreEqual("m2", Key(KeyGraph.StopLanding(TwoStopRender(), state, "map")));
        }

        /// <summary>A control the previous order never listed leaves no neighbourhood to fall back into,
        /// so the memory stands and the landing chain answers as it always did.</summary>
        [TestMethod]
        public void AMemoryTheOldOrderNeverKnewIsLeftAlone()
        {
            GraphState state = new GraphState();
            GraphRender render = TwoStopRender();
            ControlId dead = Id("came-and-went");
            state.CurKey = Id("o1");
            state.StopMemory["map"] = dead;
            state.KeyOrder = new List<ControlId> { Id("m1"), Id("m2"), Id("o1") };

            KeyGraph.Reconcile(render, state);

            Assert.AreEqual(dead, state.StopMemory["map"]);
            Assert.AreEqual("m1", Key(KeyGraph.StopLanding(render, state, "map"))); // the stop's first node
        }

        /// <summary>A structural key that is not a string is just a key to the walk - repair reads no
        /// structure out of it and must not trip over one.</summary>
        [TestMethod]
        public void ANonStringKeyFlowsThroughMemoryRepair()
        {
            GraphState state = new GraphState();
            ControlId cell = ControlId.Structural(new int[] { 3, 7 });
            GraphRender render = TwoStopRender();
            state.CurKey = Id("o1");
            state.StopMemory["map"] = cell;
            state.KeyOrder = new List<ControlId> { Id("m1"), Id("m2"), cell, Id("o1") };

            KeyGraph.Reconcile(render, state);

            Assert.AreEqual(Id("m2"), state.StopMemory["map"]);
        }

        /// <summary>Two stops of two and one - the fixture the memory-repair cases walk backwards
        /// through.</summary>
        private static GraphRender TwoStopRender()
        {
            return Renderer(b =>
            {
                b.BeginStop("map");
                b.AddItem(new SyntheticNode(Id("m1"), Vt("M1")));
                b.AddItem(new SyntheticNode(Id("m2"), Vt("M2")));
                b.BeginStop("other");
                b.AddItem(new SyntheticNode(Id("o1"), Vt("O1")));
            })();
        }

        [TestMethod]
        public void AnUnrecognizableRebuildFallsBackToTheStartNode()
        {
            GraphState state = new GraphState();
            bool second = false;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                if (second) b.AddItem(new SyntheticNode(Id("x"), Vt("X"))).AddItem(new SyntheticNode(Id("y"), Vt("Y")));
                else b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("b", Focused(g));

            second = true;
            g.Rerender();
            Assert.AreEqual("x", Focused(g));
        }

        [TestMethod]
        public void ASuggestedMoveIsHonoredOnceAndThenConsumed()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")))), state);
            g.Rerender();
            state.NextSuggestedMove = Id("b");
            g.Rerender();
            Assert.AreEqual("b", Focused(g));
            Assert.IsNull(state.NextSuggestedMove);
            g.Rerender();
            Assert.AreEqual("b", Focused(g));
        }

        [TestMethod]
        public void RerenderReportsFalseWhenTheScreenProducesNothing()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() => null, state);
            Assert.IsFalse(g.Rerender());
            Assert.IsNull(g.Current);
            Assert.IsNull(g.CurrentNode);
        }

        [TestMethod]
        public void ComputeOrderVisitsAGridInReadingOrder()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow("g").AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).EndRow();
            b.StartRow("g").AddItem(new SyntheticNode(Id("c"), Vt("C"))).AddItem(new SyntheticNode(Id("d"), Vt("D"))).EndRow();
            GraphRender r = b.Build();
            List<ControlId> order = KeyGraph.ComputeOrder(r);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, order.ConvertAll(id => (string)id.StructuralKey));
        }

        [TestMethod]
        public void ComputeOrderAppendsNodesTheWalkCannotReach()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("s1").AddItem(new SyntheticNode(Id("a"), Vt("A")));
            b.BeginStop("s2").AddItem(new SyntheticNode(Id("z"), Vt("Z")));
            List<ControlId> order = KeyGraph.ComputeOrder(b.Build());
            CollectionAssert.AreEqual(new[] { "a", "z" }, order.ConvertAll(id => (string)id.StructuralKey));
        }

        // ---- behaviors ----

        [TestMethod]
        public void ActivateAndSecondaryReportWhetherTheControlHasThem()
        {
            GraphState state = new GraphState();
            int activated = 0, secondary = 0;
            NodeVtable rich = Vt("Rich");
            rich.OnActivate = () => activated++;
            rich.OnSecondary = () => secondary++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), rich)).AddItem(new SyntheticNode(Id("b"), Vt("Plain")))), state);
            g.Rerender();
            Assert.IsTrue(g.Activate());
            Assert.IsTrue(g.Secondary());
            Assert.AreEqual(1, activated);
            Assert.AreEqual(1, secondary);

            g.Move(GraphDir.Down);
            Assert.IsFalse(g.Activate());
            Assert.IsFalse(g.Secondary());
        }

        /// <summary>
        /// The right click is a slot of its own and nothing else: a control without one answers false
        /// so the caller stays silent, and never borrows the plain click. A right click that does not
        /// exist has nothing to replay - unlike the modified LEFT clicks below.
        /// </summary>
        [TestMethod]
        public void TheContextualCommandRunsItsOwnSlotAndNothingWhereThereIsNone()
        {
            GraphState state = new GraphState();
            int activated = 0, contextual = 0;
            NodeVtable row = Vt("Ship");
            row.OnActivate = () => activated++;
            row.OnContextual = () => contextual++;
            NodeVtable plain = Vt("Plain");
            plain.OnActivate = () => activated++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), row)).AddItem(new SyntheticNode(Id("b"), plain))), state);
            g.Rerender();
            Assert.IsTrue(g.Contextual());
            Assert.AreEqual(0, activated);
            Assert.AreEqual(1, contextual);

            g.Move(GraphDir.Down);
            Assert.IsFalse(g.Contextual());
            Assert.AreEqual(0, activated);
        }

        /// <summary>
        /// The three modified LEFT clicks - Alt+click and the two selection chords. Where the control
        /// wires the slot, the slot runs and the plain click does not. Where it does not, the plain
        /// click is replayed instead: the player is physically holding the modifier, so the game's own
        /// handler is what branches on it (Ctrl+click to locate a technology), and that must work
        /// without every screen wiring a slot for behavior that is entirely the game's.
        /// </summary>
        [TestMethod]
        public void TheModifiedClicksRunTheirOwnSlotAndOtherwiseReplayThePlainClick()
        {
            GraphState state = new GraphState();
            int activated = 0, alternate = 0, toggled = 0, ranged = 0;
            NodeVtable row = Vt("Ship");
            row.OnActivate = () => activated++;
            row.OnAlternate = () => alternate++;
            row.OnSelectToggle = () => toggled++;
            row.OnSelectRange = () => ranged++;
            NodeVtable button = Vt("Behemoth");
            button.OnActivate = () => activated++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), row)).AddItem(new SyntheticNode(Id("b"), button)).AddItem(new SyntheticNode(Id("c"), Vt("Label")))), state);
            g.Rerender();
            Assert.IsTrue(g.Alternate());
            Assert.IsTrue(g.SelectToggle());
            Assert.IsTrue(g.SelectRange());
            Assert.AreEqual(0, activated);
            Assert.AreEqual(1, alternate);
            Assert.AreEqual(1, toggled);
            Assert.AreEqual(1, ranged);

            // No slot, but a click: each chord replays the click, once.
            g.Move(GraphDir.Down);
            Assert.IsTrue(g.Alternate());
            Assert.IsTrue(g.SelectToggle());
            Assert.IsTrue(g.SelectRange());
            Assert.AreEqual(3, activated);
            Assert.AreEqual(1, alternate);
            Assert.AreEqual(1, toggled);
            Assert.AreEqual(1, ranged);

            // Neither: nothing ran, and false is how the caller knows to stay silent.
            g.Move(GraphDir.Down);
            Assert.IsFalse(g.Alternate());
            Assert.IsFalse(g.SelectToggle());
            Assert.IsFalse(g.SelectRange());
            Assert.AreEqual(3, activated);
        }

        /// <summary>
        /// The double click is a slot of its own: a control that has one runs it and NOT its single
        /// click, and a control that has none answers false so the caller stays silent instead of
        /// clicking. The two are different commands wherever the game bothers to wire both - a module
        /// tile answers a single click with nothing at all and its double click fits the module - so a
        /// fall back to activation would do something the player never asked for.
        /// </summary>
        [TestMethod]
        public void TheDoubleClickRunsItsOwnSlotAndNothingWhereThereIsNone()
        {
            GraphState state = new GraphState();
            int activated = 0, doubled = 0, contextual = 0;
            NodeVtable tile = Vt("Kinetic module");
            tile.OnActivate = () => activated++;
            tile.OnDoubleClick = () => doubled++;
            NodeVtable other = Vt("Fleet");
            other.OnActivate = () => activated++;
            other.OnContextual = () => contextual++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(new SyntheticNode(Id("a"), tile)).AddItem(new SyntheticNode(Id("b"), other))), state);
            g.Rerender();
            Assert.IsTrue(g.DoubleClick());
            Assert.AreEqual(1, doubled);
            Assert.AreEqual(0, activated);

            // A control with every other behavior and no double click: false, and nothing ran.
            g.Move(GraphDir.Down);
            Assert.IsFalse(g.DoubleClick());
            Assert.AreEqual(1, doubled);
            Assert.AreEqual(0, activated);
            Assert.AreEqual(0, contextual);
        }

        [TestMethod]
        public void TryAdjustPreemptsHorizontalNavigation()
        {
            GraphState state = new GraphState();
            List<string> adjustments = new List<string>();
            NodeVtable slider = Vt("Volume");
            slider.OnAdjust = (sign, large) => adjustments.Add(sign + (large ? " large" : " small"));

            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.StartRow().AddItem(new SyntheticNode(Id("s"), slider)).AddItem(new SyntheticNode(Id("n"), Vt("Next"))).EndRow();
            }), state);
            g.Rerender();

            Assert.IsTrue(g.TryAdjust(1, false));
            Assert.IsTrue(g.TryAdjust(-1, true));
            CollectionAssert.AreEqual(new[] { "1 small", "-1 large" }, adjustments);
            Assert.AreEqual("s", Focused(g)); // adjusting never moves focus

            // The caller navigates only when the control declines to adjust.
            g.Move(GraphDir.Right);
            Assert.IsFalse(g.TryAdjust(1, false));
        }

        [TestMethod]
        public void FocusByReferenceSyncsFromTheGameSide()
        {
            GraphState state = new GraphState();
            object thing = new object();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(new SyntheticNode(Id("a"), Vt("A")));
                b.AddItem(new SyntheticNode(ControlId.For(thing, "b"), Vt("B")));
                return b.Build();
            }, state);
            g.Rerender();
            Assert.IsTrue(g.FocusByReference(thing));
            Assert.AreEqual("b", Focused(g));
            Assert.IsFalse(g.FocusByReference(thing)); // already there: no change
            Assert.IsFalse(g.FocusByReference(new object()));
        }
    }
}
