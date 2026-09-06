using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// Landing on a control that is not declared yet: finding what it hangs under from its id alone,
    /// opening one level per build until it appears, and giving up on one that never will.
    /// </summary>
    [TestClass]
    public class FocusReachTests
    {
        // ---- reading the ancestry out of a key ----

        [TestMethod]
        public void AncestorKeysAreThePathHeadsDeepestFirst()
        {
            IList<object> keys = KeyGraph.AncestorKeys("galaxy:system/548/planet/0/action/0");
            CollectionAssert.AreEqual(
                new object[]
                {
                    "galaxy:system/548/planet/0/action",
                    "galaxy:system/548/planet/0",
                    "galaxy:system/548/planet",
                    "galaxy:system/548",
                    "galaxy:system",
                },
                new List<object>(keys)
            );
        }

        [TestMethod]
        public void AKeyWithNoPathHasNoAncestors()
        {
            Assert.AreEqual(0, KeyGraph.AncestorKeys("hud:view-title").Count);
            Assert.AreEqual(0, KeyGraph.AncestorKeys(new object()).Count);
            Assert.AreEqual(0, KeyGraph.AncestorKeys(null).Count);
        }

        /// <summary>A level of the tree the keys do not mention - the scan lens's owner headings, whose
        /// stars keep the keys the ordinary map gives them - is named by the page and lands OUTERMOST,
        /// so a landing opens it after everything the key does say.</summary>
        [TestMethod]
        public void APageMayNameAGroupingTheKeysDoNotMention()
        {
            try
            {
                KeyGraph.GroupingAncestor = key =>
                    (string)key == "galaxy:constellation/1/system/162"
                        ? "galaxy:owner/none"
                        : null;

                CollectionAssert.AreEqual(
                    new object[]
                    {
                        "galaxy:constellation/1/system",
                        "galaxy:constellation/1",
                        "galaxy:constellation",
                        "galaxy:owner/none",
                    },
                    new List<object>(KeyGraph.AncestorKeys("galaxy:constellation/1/system/162"))
                );

                // Asked about the path ancestors too, so a row deep inside such a member gets the same
                // heading as its star.
                Assert.IsTrue(
                    KeyGraph.AncestorKeys("galaxy:constellation/1/system/162/planet/0").Contains("galaxy:owner/none")
                );

                // And nothing at all for a key it does not name.
                Assert.IsFalse(
                    KeyGraph.AncestorKeys("galaxy:constellation/2/system/9").Contains("galaxy:owner/none")
                );
            }
            finally
            {
                KeyGraph.Reset();
            }
        }

        [TestMethod]
        public void ASiblingWhoseKeyStartsTheSameIsNotAnAncestor()
        {
            // "galaxy:system/5" is a raw string prefix of "galaxy:system/548/..." and must not claim it.
            GraphRender render = Renderer(b =>
            {
                b.AddItem(new SyntheticNode(Id("galaxy:system/5"), Vt("Xiu")));
                b.AddItem(new SyntheticNode(Id("galaxy:system/548"), Vt("Bao")));
            })();

            GraphNode found = KeyGraph.DeepestDeclaredAncestor(
                render,
                Id("galaxy:system/548/planet/0")
            );
            Assert.AreEqual("galaxy:system/548", Key(found));
        }

        [TestMethod]
        public void TheDEEPESTDeclaredAncestorIsTheOneFound()
        {
            GraphState state = new GraphState();
            state.Expanded.Add(Id("s"));
            GraphRender render = Renderer(
                b =>
                {
                    b.BeginGroup(new SyntheticNode(Id("s"), Vt("System")));
                    b.AddItem(new SyntheticNode(Id("s/planet/0"), Vt("Planet")));
                    b.EndGroup();
                },
                state
            )();

            Assert.AreEqual(
                "s/planet/0",
                Key(KeyGraph.DeepestDeclaredAncestor(render, Id("s/planet/0/action/0")))
            );
        }

        [TestMethod]
        public void NothingLeadingToItAnswersNull()
        {
            GraphRender render = Renderer(b => b.AddItem(new SyntheticNode(Id("a"), Vt("A"))))();
            Assert.IsNull(KeyGraph.DeepestDeclaredAncestor(render, Id("b/c/d")));
        }

        // ---- opening the way, one level per build ----

        /// <summary>A two-level tree: a system holding planets, each planet holding actions - the shape
        /// the galaxy declares, and the shape a constellation layer adds a level above.</summary>
        private static KeyGraph Tree(GraphState state)
        {
            return new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.AddItem(new SyntheticNode(Id("top"), Vt("Top")));
                        b.BeginGroup(new SyntheticNode(Id("c"), Vt("Constellation")));
                        b.BeginGroup(new SyntheticNode(Id("c/system/1"), Vt("System")));
                        b.BeginGroup(new SyntheticNode(Id("c/system/1/planet/0"), Vt("Planet")));
                        b.AddItem(new SyntheticNode(Id("c/system/1/planet/0/action/0"), Vt("Colonize")));
                        b.EndGroup();
                        b.EndGroup();
                        b.EndGroup();
                    },
                    state
                ),
                state
            );
        }

        [TestMethod]
        public void ReachOpensOneLevelPerBuildUntilTheControlIsThere()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            ControlId target = Id("c/system/1/planet/0/action/0");

            g.Rerender();
            Assert.AreEqual(ReachStep.Opened, g.Reach(target));
            Assert.IsTrue(state.Expanded.Contains(Id("c")));

            g.Rerender();
            Assert.AreEqual(ReachStep.Opened, g.Reach(target));
            Assert.IsTrue(state.Expanded.Contains(Id("c/system/1")));

            g.Rerender();
            Assert.AreEqual(ReachStep.Opened, g.Reach(target));
            Assert.IsTrue(state.Expanded.Contains(Id("c/system/1/planet/0")));

            g.Rerender();
            Assert.AreEqual(ReachStep.Present, g.Reach(target));
            Assert.IsTrue(g.Focus(target));
        }

        [TestMethod]
        public void ReachOpensThroughTheGroupsOwnExpandHook()
        {
            // The galaxy's system node overrides OnExpand (it flies the camera in as well as flipping
            // the state), and an auto-expansion must run that rather than the engine's own bookkeeping.
            GraphState state = new GraphState();
            int opened = 0;
            KeyGraph g = new KeyGraph(
                () =>
                {
                    GraphBuilder b = new GraphBuilder(state.Expanded);
                    NodeVtable header = Vt("System");
                    header.OnExpand = () =>
                    {
                        opened++;
                        state.Expanded.Add(Id("s"));
                    };
                    b.BeginGroup(new SyntheticNode(Id("s"), header));
                    b.AddItem(new SyntheticNode(Id("s/planet/0"), Vt("Planet")));
                    b.EndGroup();
                    return b.Build();
                },
                state
            );

            g.Rerender();
            Assert.AreEqual(ReachStep.Opened, g.Reach(Id("s/planet/0")));
            Assert.AreEqual(1, opened);
            g.Rerender();
            Assert.AreEqual(ReachStep.Present, g.Reach(Id("s/planet/0")));
        }

        [TestMethod]
        public void AnAncestorThatIsAlreadyOpenIsWaitedOnRatherThanReopened()
        {
            // A planet with no card is a plain row, not a group: its actions do not exist yet and there
            // is nothing to open. The answer is "wait", not "unreachable".
            GraphState state = new GraphState();
            state.Expanded.Add(Id("s"));
            KeyGraph g = new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.BeginGroup(new SyntheticNode(Id("s"), Vt("System")));
                        b.AddItem(new SyntheticNode(Id("s/planet/0"), Vt("Planet")));
                        b.EndGroup();
                    },
                    state
                ),
                state
            );

            g.Rerender();
            Assert.AreEqual(ReachStep.Waiting, g.Reach(Id("s/planet/0/action/0")));
        }

        [TestMethod]
        public void AnIdNothingLeadsToIsUnreachable()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            Assert.AreEqual(ReachStep.Unreachable, g.Reach(Id("elsewhere/thing/0")));
            Assert.AreEqual(0, state.Expanded.Count);
        }

        // ---- the budget ----

        [TestMethod]
        public void APresentControlIsLandedOnAtOnce()
        {
            FocusRequest request = new FocusRequest(Id("a"), true);
            Assert.AreEqual(FocusOutcome.Land, request.Step(ReachStep.Present));
            Assert.AreEqual(FocusRequest.DefaultFrames, request.FramesLeft);
        }

        [TestMethod]
        public void AnUnreachableControlIsDroppedOnTheFirstFrame()
        {
            FocusRequest request = new FocusRequest(Id("a"), true);
            Assert.AreEqual(FocusOutcome.Drop, request.Step(ReachStep.Unreachable));
        }

        [TestMethod]
        public void OpeningAndWaitingBothSpendTheBudgetAndItRunsOut()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Opened));
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Waiting));
            Assert.AreEqual(FocusOutcome.Drop, request.Step(ReachStep.Waiting));
        }

        [TestMethod]
        public void ALandingStillArrivesOnTheLastFrameOfTheBudget()
        {
            FocusRequest request = new FocusRequest(Id("a"), false, 2);
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Opened));
            Assert.AreEqual(FocusOutcome.Land, request.Step(ReachStep.Present));
            Assert.IsFalse(request.Announce);
        }

        // ---- suspension: the frames the request is not being worked on ----

        [TestMethod]
        public void ASuspendedFrameSpendsNothingOfTheBudget()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.AreEqual(3, request.FramesLeft);
        }

        [TestMethod]
        public void ASuspendedRequestResumesWithTheBudgetItHad()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Waiting));
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.AreEqual(2, request.FramesLeft);
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Waiting));
            Assert.AreEqual(FocusOutcome.Drop, request.Step(ReachStep.Waiting));
        }

        [TestMethod]
        public void ASuspendedFrameDoesNotBelieveNothingLeadsThere()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Unreachable, true));
            Assert.AreEqual(3, request.FramesLeft);
            Assert.AreEqual(FocusOutcome.Drop, request.Step(ReachStep.Unreachable));
        }

        /// A landing announces itself once, and what a control SAYS depends on the view it is read
        /// in - a galaxy row reads the far view's version of itself while the camera is still flying.
        /// So a suspended frame holds even a control that is already there, and spends no budget doing
        /// it (owner ruling, batch 7).
        [TestMethod]
        public void ASuspendedFrameHoldsEvenAControlThatIsThere()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.AreEqual(FocusOutcome.Wait, request.Step(ReachStep.Present, true));
            Assert.AreEqual(3, request.FramesLeft);
            Assert.AreEqual(FocusOutcome.Land, request.Step(ReachStep.Present, false));
        }

        // ---- ownership: whose landing is it ----

        [TestMethod]
        public void ARequestRemembersTheScreenThatAskedForIt()
        {
            object screen = new object();
            FocusRequest request = new FocusRequest(Id("a"), true, screen);
            Assert.AreSame(screen, request.Owner);
            Assert.AreEqual(FocusRequest.DefaultFrames, request.FramesLeft);
        }

        [TestMethod]
        public void ARequestWithNoOwnerNamesNobody()
        {
            Assert.IsNull(new FocusRequest(Id("a"), true).Owner);
            Assert.IsNull(new FocusRequest(Id("a"), true, 5).Owner);
        }
    }
}
