using System;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The builder's wiring contract: what arrows do in menu mode, where Tab-stops cut the graph, and
    /// which nodes get an auto "n of m".
    /// </summary>
    [TestClass]
    public class GraphBuilderTests
    {
        private static GraphRender TwoByTwo(object rowKey)
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow(rowKey).AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).EndRow();
            b.StartRow(rowKey).AddItem(new SyntheticNode(Id("c"), Vt("C"))).AddItem(new SyntheticNode(Id("d"), Vt("D"))).EndRow();
            return b.Build();
        }

        [TestMethod]
        public void RowMembersAreWiredLeftAndRight()
        {
            GraphRender r = TwoByTwo(null);
            Assert.AreEqual("b", DestKey(Node(r, "a"), GraphDir.Right));
            Assert.AreEqual("a", DestKey(Node(r, "b"), GraphDir.Left));
            Assert.IsNull(DestKey(Node(r, "a"), GraphDir.Left));
            Assert.IsNull(DestKey(Node(r, "b"), GraphDir.Right));
        }

        [TestMethod]
        public void ConsecutiveRowsAreWiredUpAndDown()
        {
            GraphRender r = TwoByTwo(null);
            Assert.AreEqual("c", DestKey(Node(r, "a"), GraphDir.Down));
            Assert.AreEqual("a", DestKey(Node(r, "c"), GraphDir.Up));
        }

        [TestMethod]
        public void RowsWithoutAKeyLandVerticalNavigationOnTheFirstItem()
        {
            GraphRender r = TwoByTwo(null);
            Assert.AreEqual("c", DestKey(Node(r, "b"), GraphDir.Down));
            Assert.AreEqual("a", DestKey(Node(r, "d"), GraphDir.Up));
        }

        [TestMethod]
        public void RowsSharingARowKeyPreserveTheColumn()
        {
            GraphRender r = TwoByTwo("grid");
            Assert.AreEqual("d", DestKey(Node(r, "b"), GraphDir.Down));
            Assert.AreEqual("b", DestKey(Node(r, "d"), GraphDir.Up));
        }

        [TestMethod]
        public void ColumnIsPreservedOnlyBetweenRowsSharingTheSameKey()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow("one").AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).EndRow();
            b.StartRow("two").AddItem(new SyntheticNode(Id("c"), Vt("C"))).AddItem(new SyntheticNode(Id("d"), Vt("D"))).EndRow();
            GraphRender r = b.Build();
            Assert.AreEqual("c", DestKey(Node(r, "b"), GraphDir.Down));
        }

        [TestMethod]
        public void ColumnNavigationFallsBackWhenTheAdjacentRowIsShorter()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow("grid").AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).EndRow();
            b.StartRow("grid").AddItem(new SyntheticNode(Id("c"), Vt("C"))).EndRow();
            GraphRender r = b.Build();
            Assert.AreEqual("c", DestKey(Node(r, "b"), GraphDir.Down));
        }

        [TestMethod]
        public void ItemsOutsideARowFormAPlainVerticalMenu()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).AddItem(new SyntheticNode(Id("c"), Vt("C")));
            GraphRender r = b.Build();
            Assert.AreEqual("b", DestKey(Node(r, "a"), GraphDir.Down));
            Assert.AreEqual("b", DestKey(Node(r, "c"), GraphDir.Up));
            Assert.IsNull(DestKey(Node(r, "a"), GraphDir.Right));
        }

        [TestMethod]
        public void ArrowsNeverCrossATabStop()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("s1").AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
            b.BeginStop("s2").AddItem(new SyntheticNode(Id("c"), Vt("C"))).AddItem(new SyntheticNode(Id("d"), Vt("D")));
            GraphRender r = b.Build();
            Assert.IsNull(DestKey(Node(r, "b"), GraphDir.Down));
            Assert.IsNull(DestKey(Node(r, "c"), GraphDir.Up));
            Assert.AreEqual("s1", Node(r, "b").StopKey);
            Assert.AreEqual("s2", Node(r, "c").StopKey);
        }

        [TestMethod]
        public void RegionsAreTaggedPerStopAndClearedByBeginStop()
        {
            GraphBuilder b = new GraphBuilder();
            b.SetRegion("r1").AddItem(new SyntheticNode(Id("a"), Vt("A")));
            b.SetRegion("r2").AddItem(new SyntheticNode(Id("b"), Vt("B")));
            b.BeginStop("s2").AddItem(new SyntheticNode(Id("c"), Vt("C")));
            GraphRender r = b.Build();
            Assert.AreEqual("r1", Node(r, "a").RegionKey);
            Assert.AreEqual("r2", Node(r, "b").RegionKey);
            Assert.IsNull(Node(r, "c").RegionKey);
        }

        [TestMethod]
        public void SinglesInTheSameStopArePositionedAmongTheirSiblings()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).AddItem(new SyntheticNode(Id("c"), Vt("C")));
            GraphRender r = b.Build();
            Assert.AreEqual(1, Node(r, "a").PositionIndex);
            Assert.AreEqual(3, Node(r, "a").PositionCount);
            Assert.AreEqual(2, Node(r, "b").PositionIndex);
            Assert.AreEqual(3, Node(r, "c").PositionCount);
        }

        [TestMethod]
        public void MultiItemRowMembersArePositionedWithinTheirRowNotTheStop()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).EndRow();
            b.AddItem(new SyntheticNode(Id("c"), Vt("C")));
            b.AddItem(new SyntheticNode(Id("d"), Vt("D")));
            GraphRender r = b.Build();
            Assert.AreEqual(2, Node(r, "a").PositionCount); // among its row
            Assert.AreEqual(2, Node(r, "b").PositionIndex);
            Assert.AreEqual(2, Node(r, "c").PositionCount); // among the single-item rows
            Assert.AreEqual(2, Node(r, "d").PositionIndex);
        }

        [TestMethod]
        public void ALoneSiblingGetsNoPosition()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A")));
            GraphRender r = b.Build();
            Assert.AreEqual(0, Node(r, "a").PositionCount);
        }

        [TestMethod]
        public void PositionsAreCountedPerParentContext()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Outer");
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
            b.PopContext();
            b.AddItem(new SyntheticNode(Id("c"), Vt("C")));
            GraphRender r = b.Build();
            Assert.AreEqual(2, Node(r, "a").PositionCount);
            Assert.AreEqual(0, Node(r, "c").PositionCount); // its own (parent, stop) group has one member
        }

        [TestMethod]
        public void AnOnlyChildOfAnExpandableGroupStillGetsItsPosition()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("before"), Vt("Before")));
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("G")), true);
            b.AddItem(new SyntheticNode(Id("only"), Vt("Only")));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.AreEqual(1, Node(r, "only").PositionIndex);
            Assert.AreEqual(1, Node(r, "only").PositionCount);
        }

        [TestMethod]
        public void AnOnlyChildOfAPlainContextStillGetsNoPosition()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Outer");
            b.AddItem(new SyntheticNode(Id("only"), Vt("Only")));
            b.PopContext();
            Assert.AreEqual(0, Node(b.Build(), "only").PositionCount);
        }

        [TestMethod]
        public void SuppressChildPositionsSilencesAContextsChildren()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Log", null, false);
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).AddItem(new SyntheticNode(Id("c"), Vt("C")));
            b.PopContext();
            GraphRender r = b.Build();
            Assert.AreEqual(0, Node(r, "a").PositionCount);
            Assert.AreEqual(0, Node(r, "c").PositionCount);
        }

        /// <summary>A row of COLUMNS - a table's heading band, a grid line - is not a bar of choices, and
        /// "1 of 8" there would count the table's columns.</summary>
        [TestMethod]
        public void ARowDeclaredWithoutPositionsStampsNone()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow(positions: false)
                .AddItem(new SyntheticNode(Id("a"), Vt("A")))
                .AddItem(new SyntheticNode(Id("b"), Vt("B")))
                .EndRow();
            b.StartRow().AddItem(new SyntheticNode(Id("c"), Vt("C"))).AddItem(new SyntheticNode(Id("d"), Vt("D"))).EndRow();
            GraphRender r = b.Build();
            Assert.AreEqual(0, Node(r, "a").PositionCount);
            Assert.AreEqual(0, Node(r, "b").PositionCount);
            Assert.AreEqual(2, Node(r, "c").PositionCount);
        }

        [TestMethod]
        public void RawNodesGetNoAutomaticWiringAndNoPositions()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(new SyntheticNode(Id("a"), Vt("A"))).AddNode(new SyntheticNode(Id("b"), Vt("B")));
            b.Connect(Id("a"), GraphDir.Right, Id("b"), "over there");
            GraphRender r = b.Build();
            Assert.AreEqual("b", DestKey(Node(r, "a"), GraphDir.Right));
            Assert.IsNull(DestKey(Node(r, "b"), GraphDir.Left));
            Assert.AreEqual(0, Node(r, "a").PositionCount);
        }

        [TestMethod]
        public void EdgesToUndeclaredNodesAreDropped()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(new SyntheticNode(Id("a"), Vt("A")));
            b.Connect(Id("a"), GraphDir.Right, Id("ghost"));
            GraphRender r = b.Build();
            Assert.IsNull(DestKey(Node(r, "a"), GraphDir.Right));
        }

        [TestMethod]
        public void MenuRowsAboveRawContentAreStitchedTogether()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(new SyntheticNode(Id("f1"), Vt("Filter"))).AddItem(new SyntheticNode(Id("f2"), Vt("Sort"))).EndRow();
            b.AddNode(new SyntheticNode(Id("cell"), Vt("Cell")));
            GraphRender r = b.Build();
            Assert.AreEqual("cell", DestKey(Node(r, "f1"), GraphDir.Down));
            Assert.AreEqual("cell", DestKey(Node(r, "f2"), GraphDir.Down));
            Assert.AreEqual("f1", DestKey(Node(r, "cell"), GraphDir.Up));
        }

        /// <summary>A menu row, then a raw ROW of three cells wired only to each other — the shape a
        /// sheet's top row has under a strip of buttons.</summary>
        private static GraphRender StripOverRawRow()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(new SyntheticNode(Id("f1"), Vt("Filter"))).AddItem(new SyntheticNode(Id("f2"), Vt("Sort"))).EndRow();
            b.AddNode(new SyntheticNode(Id("c0"), Vt("Alpha"))).AddNode(new SyntheticNode(Id("c1"), Vt("3"))).AddNode(new SyntheticNode(Id("c2"), Vt("5")));
            b.Connect(Id("c0"), GraphDir.Right, Id("c1"));
            b.Connect(Id("c1"), GraphDir.Left, Id("c0"));
            b.Connect(Id("c1"), GraphDir.Right, Id("c2"));
            b.Connect(Id("c2"), GraphDir.Left, Id("c1"));
            return b.Build();
        }

        [TestMethod]
        public void EveryCellOfARawTopRowReachesTheMenuRowAbove()
        {
            GraphRender r = StripOverRawRow();
            Assert.AreEqual("f1", DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.AreEqual("f1", DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.AreEqual("f1", DestKey(Node(r, "c2"), GraphDir.Up));
            Assert.AreEqual("c0", DestKey(Node(r, "f1"), GraphDir.Down));
            Assert.AreEqual("c0", DestKey(Node(r, "f2"), GraphDir.Down));
        }

        [TestMethod]
        public void TheRawTopRunEndsAtTheFirstNodeThatAlreadyHasAnUp()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(new SyntheticNode(Id("f1"), Vt("Filter"))).EndRow();
            b.AddNode(new SyntheticNode(Id("c0"), Vt("Alpha"))).AddNode(new SyntheticNode(Id("c1"), Vt("3")));
            b.AddNode(new SyntheticNode(Id("d0"), Vt("Beta"))).AddNode(new SyntheticNode(Id("d1"), Vt("2")));
            b.Connect(Id("d0"), GraphDir.Up, Id("c0")); // a second table row wires itself
            GraphRender r = b.Build();
            Assert.AreEqual("f1", DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.AreEqual("f1", DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.AreEqual("c0", DestKey(Node(r, "d0"), GraphDir.Up));
            Assert.IsNull(DestKey(Node(r, "d1"), GraphDir.Up));
        }

        [TestMethod]
        public void EveryCellOfARawBottomRowReachesTheMenuRowBelow()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(new SyntheticNode(Id("c0"), Vt("Alpha"))).AddNode(new SyntheticNode(Id("c1"), Vt("3"))).AddNode(new SyntheticNode(Id("c2"), Vt("5")));
            b.StartRow().AddItem(new SyntheticNode(Id("ok"), Vt("OK"))).AddItem(new SyntheticNode(Id("no"), Vt("Cancel"))).EndRow();
            GraphRender r = b.Build();
            Assert.AreEqual("ok", DestKey(Node(r, "c0"), GraphDir.Down));
            Assert.AreEqual("ok", DestKey(Node(r, "c1"), GraphDir.Down));
            Assert.AreEqual("ok", DestKey(Node(r, "c2"), GraphDir.Down));

            // Back into the block lands on the run's FIRST node - a table row's primary cell, which
            // reads the row's name - not on whichever column happened to be declared last.
            Assert.AreEqual("c0", DestKey(Node(r, "ok"), GraphDir.Up));
            Assert.AreEqual("c0", DestKey(Node(r, "no"), GraphDir.Up));
        }

        [TestMethod]
        public void TheRawBottomRunEndsAtTheFirstNodeThatAlreadyHasADown()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(new SyntheticNode(Id("c0"), Vt("Alpha"))).AddNode(new SyntheticNode(Id("c1"), Vt("3")));
            b.AddNode(new SyntheticNode(Id("d0"), Vt("Beta"))).AddNode(new SyntheticNode(Id("d1"), Vt("2")));
            b.Connect(Id("c0"), GraphDir.Down, Id("d0")); // the row above wires itself
            b.StartRow().AddItem(new SyntheticNode(Id("ok"), Vt("OK"))).EndRow();
            GraphRender r = b.Build();
            Assert.AreEqual("d0", DestKey(Node(r, "c0"), GraphDir.Down));
            Assert.AreEqual("ok", DestKey(Node(r, "c1"), GraphDir.Down));
            Assert.AreEqual("ok", DestKey(Node(r, "d0"), GraphDir.Down));
            Assert.AreEqual("ok", DestKey(Node(r, "d1"), GraphDir.Down));
            Assert.AreEqual("c1", DestKey(Node(r, "ok"), GraphDir.Up));
        }

        [TestMethod]
        public void ASingleRawNodeBetweenTwoMenuRowsIsStitchedBothWays()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(new SyntheticNode(Id("f1"), Vt("Filter"))).EndRow();
            b.AddNode(new SyntheticNode(Id("prose"), Vt("What happened")));
            b.StartRow().AddItem(new SyntheticNode(Id("ok"), Vt("OK"))).AddItem(new SyntheticNode(Id("no"), Vt("Cancel"))).EndRow();
            GraphRender r = b.Build();
            Assert.AreEqual("prose", DestKey(Node(r, "f1"), GraphDir.Down));
            Assert.AreEqual("f1", DestKey(Node(r, "prose"), GraphDir.Up));
            Assert.AreEqual("ok", DestKey(Node(r, "prose"), GraphDir.Down));
            Assert.AreEqual("prose", DestKey(Node(r, "ok"), GraphDir.Up));
            Assert.AreEqual("prose", DestKey(Node(r, "no"), GraphDir.Up));
        }

        private static NodeVtable Col(string label, int column)
        {
            NodeVtable vtable = Vt(label);
            vtable.Column = column;
            return vtable;
        }

        /// <summary>A table's heading band (a menu row of columns) over a raw row of the same columns —
        /// the shape every <c>TableSheet</c> declares.</summary>
        private static GraphRender BandOverRow()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow(positions: false)
                .AddItem(new SyntheticNode(Id("h0"), Col("Name", 0)))
                .AddItem(new SyntheticNode(Id("h1"), Col("Status", 1)))
                .AddItem(new SyntheticNode(Id("h2"), Col("Population", 2)))
                .EndRow();
            b.AddNode(new SyntheticNode(Id("c0"), Col("Xiu", 0)))
                .AddNode(new SyntheticNode(Id("c1"), Col("Colony", 1)))
                .AddNode(new SyntheticNode(Id("c2"), Col("3", 2)));
            return b.Build();
        }

        [TestMethod]
        public void UpFromARowCellReachesItsOwnColumnsHeading()
        {
            GraphRender r = BandOverRow();
            Assert.AreEqual("h0", DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.AreEqual("h1", DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.AreEqual("h2", DestKey(Node(r, "c2"), GraphDir.Up));
        }

        [TestMethod]
        public void DownFromAHeadingReachesItsOwnColumnsCell()
        {
            GraphRender r = BandOverRow();
            Assert.AreEqual("c0", DestKey(Node(r, "h0"), GraphDir.Down));
            Assert.AreEqual("c1", DestKey(Node(r, "h1"), GraphDir.Down));
            Assert.AreEqual("c2", DestKey(Node(r, "h2"), GraphDir.Down));
        }

        /// <summary>Sparse rows exist: a column the first row does not draw has no cell to land on, and
        /// that heading falls back to the row's primary rather than dead-ending.</summary>
        [TestMethod]
        public void AColumnTheOtherSideLacksFallsBackToTheSingleTarget()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow(positions: false)
                .AddItem(new SyntheticNode(Id("h0"), Col("Name", 0)))
                .AddItem(new SyntheticNode(Id("h1"), Col("Status", 1)))
                .AddItem(new SyntheticNode(Id("h2"), Col("Hero", 2)))
                .EndRow();
            b.AddNode(new SyntheticNode(Id("c0"), Col("Xiu", 0))).AddNode(new SyntheticNode(Id("c2"), Col("Dmitri", 2)));
            GraphRender r = b.Build();
            Assert.AreEqual("c0", DestKey(Node(r, "h1"), GraphDir.Down));
            Assert.AreEqual("c2", DestKey(Node(r, "h2"), GraphDir.Down));
            Assert.AreEqual("h0", DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.AreEqual("h2", DestKey(Node(r, "c2"), GraphDir.Up));
        }

        /// <summary>A bar of ordinary controls is not a set of columns — every one of them is column 0 —
        /// so the seam keeps its single target in both directions.</summary>
        [TestMethod]
        public void ABarOfControlsIsNotPairedByColumn()
        {
            GraphRender r = StripOverRawRow();
            Assert.AreEqual("f1", DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.AreEqual("f1", DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.AreEqual("c0", DestKey(Node(r, "f2"), GraphDir.Down));
        }

        [TestMethod]
        public void ARawBottomRowMeetsAMenuRowBelowColumnByColumn()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(new SyntheticNode(Id("c0"), Col("Xiu", 0)))
                .AddNode(new SyntheticNode(Id("c1"), Col("Colony", 1)))
                .AddNode(new SyntheticNode(Id("c2"), Col("3", 2)));
            b.StartRow(positions: false)
                .AddItem(new SyntheticNode(Id("t0"), Col("Total", 0)))
                .AddItem(new SyntheticNode(Id("t1"), Col("-", 1)))
                .AddItem(new SyntheticNode(Id("t2"), Col("9", 2)))
                .EndRow();
            GraphRender r = b.Build();
            Assert.AreEqual("t1", DestKey(Node(r, "c1"), GraphDir.Down));
            Assert.AreEqual("t2", DestKey(Node(r, "c2"), GraphDir.Down));
            Assert.AreEqual("c1", DestKey(Node(r, "t1"), GraphDir.Up));
            Assert.AreEqual("c2", DestKey(Node(r, "t2"), GraphDir.Up));
        }

        [TestMethod]
        public void AStopOfMenuRowsOnlyIsLeftToItsOwnWiring()
        {
            GraphRender r = TwoByTwo(null);
            Assert.AreEqual("c", DestKey(Node(r, "a"), GraphDir.Down));
            Assert.AreEqual("a", DestKey(Node(r, "c"), GraphDir.Up));
            Assert.IsNull(DestKey(Node(r, "a"), GraphDir.Up));
            Assert.IsNull(DestKey(Node(r, "d"), GraphDir.Down));
        }

        [TestMethod]
        public void AStopOfRawNodesOnlyIsNeverStitched()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(new SyntheticNode(Id("c0"), Vt("Alpha"))).AddNode(new SyntheticNode(Id("c1"), Vt("Beta")));
            GraphRender r = b.Build();
            Assert.IsNull(DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.IsNull(DestKey(Node(r, "c0"), GraphDir.Down));
            Assert.IsNull(DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.IsNull(DestKey(Node(r, "c1"), GraphDir.Down));
        }

        [TestMethod]
        public void BuildReturnsNullWhenNothingWasDeclared()
        {
            Assert.IsNull(new GraphBuilder().Build());
        }

        [TestMethod]
        public void DuplicateControlIdsAreRejected()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A")));
            Assert.ThrowsException<InvalidOperationException>(() => b.AddItem(new SyntheticNode(Id("a"), Vt("Again"))));
        }

        [TestMethod]
        public void AControlWithoutAnnouncementsIsRejected()
        {
            GraphBuilder b = new GraphBuilder();
            Assert.ThrowsException<ArgumentException>(() => b.AddItem(new SyntheticNode(Id("a"), new NodeVtable())));
        }

        [TestMethod]
        public void AnUnclosedRowIsRejectedAtBuild()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(new SyntheticNode(Id("a"), Vt("A")));
            Assert.ThrowsException<InvalidOperationException>(() => b.Build());
        }

        [TestMethod]
        public void SetStartOverridesTheDefaultStartNode()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
            b.SetStart(Id("b"));
            Assert.AreEqual("b", b.Build().StartKey.StructuralKey);
        }

        [TestMethod]
        public void CollapsedGroupChildrenAreNotDeclared()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
            b.AddItem(new SyntheticNode(Id("child"), Vt("Child")));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.IsNotNull(Node(r, "g"));
            Assert.IsNull(Node(r, "child"));
            Assert.IsTrue(Node(r, "g").Expandable);
            Assert.IsFalse(Node(r, "g").Expanded);
        }

        [TestMethod]
        public void ACollapsedAncestorSuppressesTheWholeSubtree()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("outer"), Vt("Outer")));
            b.BeginGroup(new SyntheticNode(Id("inner"), Vt("Inner")), true);
            b.AddItem(new SyntheticNode(Id("leaf"), Vt("Leaf")));
            b.EndGroup();
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.IsNull(Node(r, "inner"));
            Assert.IsNull(Node(r, "leaf"));
        }

        [TestMethod]
        public void ExpandedGroupChildrenHangOffTheHeader()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")), true);
            b.AddItem(new SyntheticNode(Id("child"), Vt("Child")));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.AreSame(Node(r, "g"), Node(r, "child").Parent);
            Assert.AreEqual("child", DestKey(Node(r, "g"), GraphDir.Down));
        }

        // A contribution that opens regions of its own inside a stop somebody else regioned has to
        // hand the stop back as it found it, and it cannot know what that was without asking: the two
        // regions a dossier-bearing node declares sit inside a list whose every OTHER row belongs to
        // the list's own region, and a stop left tagged with the inner one swallows them all.
        [TestMethod]
        public void TheBuilderAnswersWhichRegionItIsTagging()
        {
            GraphBuilder b = new GraphBuilder();
            b.SetRegion("outer");
            b.AddItem(new SyntheticNode(Id("before"), Vt("Before")));
            object restore = b.Region;
            b.SetRegion("inner");
            b.AddItem(new SyntheticNode(Id("inside"), Vt("Inside")));
            b.SetRegion(restore);
            b.AddItem(new SyntheticNode(Id("after"), Vt("After")));

            GraphRender r = b.Build();
            Assert.AreEqual("outer", Node(r, "before").RegionKey);
            Assert.AreEqual("inner", Node(r, "inside").RegionKey);
            Assert.AreEqual("outer", Node(r, "after").RegionKey);
        }

        // A factory's vtable is a starting point, not a finished thing: a row that wants one more part
        // than the factory knew about adds it. Backed by an array the list advertised Add and threw
        // NotSupportedException from it, at run time and nowhere else - which is how a whole screen's
        // tree read silently died mid-build with only a log line to show for it.
        [TestMethod]
        public void ALabelsPartsCanBeExtended()
        {
            NodeVtable vtable = GraphBuilder.Label(() => "Leaper");
            vtable.Announcements.Add(NodeAnnouncement.Static("-83, 24"));
            Assert.AreEqual(2, vtable.Announcements.Count);
            Assert.IsFalse(vtable.Announcements.IsReadOnly);
        }
    }
}
