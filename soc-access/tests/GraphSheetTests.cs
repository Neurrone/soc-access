using System;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The sheet emitter: a table whose framing rules are ordinary graph edges. Shares the ModStrings
    /// collection because the readouts go through the announcer.
    /// </summary>
    [TestClass]
    public class GraphSheetTests
    {
        private readonly object _rowA = new object();
        private readonly object _rowB = new object();

        [TestInitialize]
        public void Setup()
        {
            GraphAnnouncer.Reset();
            GraphSheet.Reset();
            GraphSheet.TableRoleText = () => "table";
            GraphSheet.BlankText = () => "blank";
        }

        [TestCleanup]
        public void Cleanup()
        {
            GraphAnnouncer.Reset();
            GraphSheet.Reset();
        }

        private KeyGraph Table(GraphState state, bool raggedSecondRow = false)
        {
            return new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Name", "Ships", "Move" });
                s.Row(Vt("Alpha"), _rowA, null, () => "3", () => "5");
                if (raggedSecondRow) s.Row(Vt("Beta"), _rowB, null);
                else s.Row(Vt("Beta"), _rowB, null, () => "2", () => "4");
                s.Finish();
                return b.Build();
            }, state);
        }

        [TestMethod]
        public void FocusStartsOnTheFirstRowsPrimaryCellAndReadsTheRegionOnce()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            Assert.AreEqual("Fleets, table, Alpha", GraphAnnouncer.ComposeFull(g.CurrentNode));
        }

        [TestMethod]
        public void CrossingIntoAColumnSpeaksItsHeader()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();

            MoveResult right = g.Move(GraphDir.Right);
            Assert.IsTrue(right.Moved);
            Assert.AreEqual("Ships", right.TransitionLabel);
            Assert.AreEqual("3", GraphAnnouncer.LeafText(right.To));

            MoveResult right2 = g.Move(GraphDir.Right);
            Assert.AreEqual("Move", right2.TransitionLabel);
            Assert.AreEqual("5", GraphAnnouncer.LeafText(right2.To));

            MoveResult back = g.Move(GraphDir.Left);
            Assert.AreEqual("Ships", back.TransitionLabel);
        }

        /// <summary>The primary is a column like any other: crossing back into it says its caption, so a
        /// player walking a row hears the same shape of line in both directions.</summary>
        [TestMethod]
        public void ReturningToThePrimaryCellSpeaksItsColumnCaption()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            g.Move(GraphDir.Right);
            MoveResult back = g.Move(GraphDir.Left);
            Assert.IsTrue(back.Moved);
            Assert.AreEqual("Name", back.TransitionLabel);
            Assert.AreEqual("Alpha", GraphAnnouncer.LeafText(back.To));
        }

        /// <summary>A table whose primary column the game drew no caption over: the entry is null and
        /// the crossing stays label-free rather than inventing a word for it.</summary>
        [TestMethod]
        public void APrimaryWithNoCaptionCrossesUnlabeled()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { null, "Ships" });
                s.Row(Vt("Alpha"), _rowA, null, () => "3");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();

            Assert.AreEqual("Ships", g.Move(GraphDir.Right).TransitionLabel);
            MoveResult back = g.Move(GraphDir.Left);
            Assert.IsTrue(back.Moved);
            Assert.IsNull(back.TransitionLabel);
            Assert.AreEqual("Alpha", GraphAnnouncer.LeafText(back.To));
        }

        /// <summary>A plain list region has no captions at all, and gains none: neither direction is
        /// labeled and the region is not called a table.</summary>
        [TestMethod]
        public void APlainListRegionLabelsNothingAndIsNoTable()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets");
                s.Row(Vt("Alpha"), _rowA, null, () => "3");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.AreEqual("Fleets, Alpha", GraphAnnouncer.ComposeFull(g.CurrentNode));

            // The crossings have to HAPPEN for their silence to mean anything: an unlabeled edge and
            // an edge that is not there read the same off TransitionLabel alone.
            MoveResult right = g.Move(GraphDir.Right);
            Assert.IsTrue(right.Moved);
            Assert.IsNull(right.TransitionLabel);

            MoveResult left = g.Move(GraphDir.Left);
            Assert.IsTrue(left.Moved);
            Assert.IsNull(left.TransitionLabel);
        }

        /// <summary>One captioned column and nothing beside it is the list it looks like: the header
        /// array counts the primary, so a lone entry does not make a region a table.</summary>
        [TestMethod]
        public void APrimaryCaptionAloneDoesNotMakeARegionATable()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Name" });
                s.Row(Vt("Alpha"), _rowA, null);
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.AreEqual("Fleets, Alpha", GraphAnnouncer.ComposeFull(g.CurrentNode));
        }

        [TestMethod]
        public void VerticalNavigationPreservesTheColumnAndNamesTheRow()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            g.Move(GraphDir.Right); // Alpha / Ships

            MoveResult down = g.Move(GraphDir.Down);
            Assert.IsTrue(down.Moved);
            Assert.AreEqual("Beta", down.TransitionLabel);
            Assert.AreEqual("2", GraphAnnouncer.LeafText(down.To)); // still the Ships column

            MoveResult up = g.Move(GraphDir.Up);
            Assert.AreEqual("Alpha", up.TransitionLabel);
            Assert.AreEqual("3", GraphAnnouncer.LeafText(up.To));
        }

        /// <summary>A grid whose rows are only the lines the game wrapped one lattice onto: column 0 is
        /// a cell like any other, so a vertical crossing names no row - saying one would announce a
        /// NEIGHBOURING cell's words in front of the cell landed on.</summary>
        [TestMethod]
        public void UnnamedRowsLabelNoVerticalCrossing()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.NamedRows = false;
                s.Region("Luxuries", new[] { "Food", "Industry" });
                s.Row(Vt("Alpha"), null, null, () => "3");
                s.Row(Vt("Beta"), null, null, () => "2");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Right);

            MoveResult down = g.Move(GraphDir.Down);
            Assert.IsTrue(down.Moved);
            Assert.IsNull(down.TransitionLabel);
            Assert.AreEqual("2", GraphAnnouncer.LeafText(down.To));
            Assert.IsNull(g.Move(GraphDir.Up).TransitionLabel);
        }

        /// <summary>And every cell of such a grid is its own search result: the one-result-per-row filter
        /// exists because a named row's cells all search as that row, which here would make seven columns
        /// of eight unreachable by typing.</summary>
        [TestMethod]
        public void UnnamedRowsMakeEveryCellSearchable()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("lux");
            GraphSheet s = new GraphSheet(b, "t:");
            s.NamedRows = false;
            s.Region("Luxuries", new[] { "Food", "Industry" });
            s.Row(Vt("Alpha"), null, null, () => "Transvine");
            s.Finish();
            GraphRender render = b.Build();

            SearchScope scope = SearchScope.OverStop(render, "lux");
            Assert.AreEqual(2, scope.Count);
            Assert.AreEqual("Transvine", scope.TextOf(1));
        }

        /// <summary>The primary included. A search made from a metadata column steps back into that
        /// column after landing, because a named row's cells all matched by the row's NAME and the
        /// player was reading a column; a cell that matched by its own words is already the thing asked
        /// for, so the stamp has to be on column 0 too or the landing walks one cell past it.</summary>
        [TestMethod]
        public void UnnamedRowsMatchByTheirOwnWordsInEveryColumnIncludingTheFirst()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("lux");
            GraphSheet s = new GraphSheet(b, "t:");
            s.NamedRows = false;
            s.Region("Luxuries", new[] { "Food", "Industry" });
            s.Row(Vt("Transvine"), null, null, () => "3");
            s.Finish();
            foreach (GraphNode node in b.Build().Order)
                Assert.IsTrue(node.Vtable.SearchesAsItself, "column " + node.Vtable.Column);

            GraphBuilder named = new GraphBuilder();
            named.BeginStop("fleets");
            GraphSheet n = new GraphSheet(named, "t:");
            n.Region("Fleets", new[] { "Name", "Ships" });
            n.Row(Vt("Alpha"), _rowA, null, () => "3");
            n.Finish();
            foreach (GraphNode node in named.Build().Order)
                Assert.IsFalse(node.Vtable.SearchesAsItself);
        }

        /// <summary>The default is unchanged: a table whose rows are things offers one result per row.
        /// </summary>
        [TestMethod]
        public void NamedRowsStillOfferOneSearchResultPerRow()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("fleets");
            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Fleets", new[] { "Name", "Ships" });
            s.Row(Vt("Alpha"), _rowA, null, () => "3");
            s.Finish();

            Assert.AreEqual(1, SearchScope.OverStop(b.Build(), "fleets").Count);
        }

        [TestMethod]
        public void MovingDownThePrimaryColumnIsUnlabeled()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            MoveResult down = g.Move(GraphDir.Down);
            Assert.IsTrue(down.Moved);
            Assert.IsNull(down.TransitionLabel);
            Assert.AreEqual("Beta", GraphAnnouncer.LeafText(down.To));
        }

        [TestMethod]
        public void ARaggedRowFallsBackToItsPrimaryCell()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state, true);
            g.Rerender();
            g.Move(GraphDir.Right); // Alpha / Ships

            MoveResult down = g.Move(GraphDir.Down);
            Assert.IsTrue(down.Moved);
            Assert.IsNull(down.TransitionLabel); // landing on a primary is never row-labeled
            Assert.AreEqual("Beta", GraphAnnouncer.LeafText(down.To));
        }

        /// <summary>A table whose second column is read as PIECES on one row and as one cell on the
        /// next - a save with two expansion badges above a base-game save.</summary>
        private KeyGraph Pieced(GraphState state)
        {
            return new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Saves", new[] { "Name", "Content", "Status" });
                s.RowAt(Vt("Alpha"), _rowA, new[]
                {
                    new GraphSheet.SheetCell(1, 0, Vt("Vaulters")),
                    new GraphSheet.SheetCell(1, 1, Vt("Hissho")),
                    new GraphSheet.SheetCell(2, 0, Vt("Valid")),
                });
                s.RowAt(Vt("Beta"), _rowB, new[]
                {
                    new GraphSheet.SheetCell(1, 0, Vt("blank")),
                    new GraphSheet.SheetCell(2, 0, Vt("Corrupt")),
                });
                s.Finish();
                return b.Build();
            }, state);
        }

        /// <summary>The pieces of one cell are entered under that cell's caption, said once - a step from
        /// piece to piece crosses no column - and the next column's caption follows them, never one
        /// borrowed from a row with fewer pieces.</summary>
        [TestMethod]
        public void ThePiecesOfACellShareItsCaptionAndTheNextColumnFollowsThem()
        {
            GraphState state = new GraphState();
            KeyGraph g = Pieced(state);
            g.Rerender();
            MoveResult first = g.Move(GraphDir.Right);
            Assert.AreEqual("Content", first.TransitionLabel);
            Assert.AreEqual("Vaulters", GraphAnnouncer.LeafText(first.To));
            MoveResult second = g.Move(GraphDir.Right);
            Assert.IsNull(second.TransitionLabel); // still Content: no column was crossed
            Assert.AreEqual("Hissho", GraphAnnouncer.LeafText(second.To));
            MoveResult third = g.Move(GraphDir.Right);
            Assert.AreEqual("Status", third.TransitionLabel);
            Assert.AreEqual("Valid", GraphAnnouncer.LeafText(third.To));
        }

        /// <summary>Up and Down move by column IDENTITY: from a cell's second piece the row below,
        /// which has one, lands on its nearest piece of the same column, and the row's other columns
        /// stay themselves whatever position the pieces pushed them to.</summary>
        [TestMethod]
        public void VerticalMovesLandOnTheSameColumnWhateverThePieceCount()
        {
            GraphState state = new GraphState();
            KeyGraph g = Pieced(state);
            g.Rerender();
            g.Move(GraphDir.Right);
            g.Move(GraphDir.Right); // Alpha / Content, piece 2
            MoveResult down = g.Move(GraphDir.Down);
            Assert.IsTrue(down.Moved);
            Assert.AreEqual("Beta", down.TransitionLabel);
            Assert.AreEqual("blank", GraphAnnouncer.LeafText(down.To));
            MoveResult up = g.Move(GraphDir.Up);
            Assert.AreEqual("Alpha", up.TransitionLabel);
            Assert.AreEqual("Vaulters", GraphAnnouncer.LeafText(up.To));
            g.Move(GraphDir.Down);
            MoveResult status = g.Move(GraphDir.Right); // Beta / Status
            Assert.AreEqual("Status", status.TransitionLabel);
            Assert.AreEqual("Corrupt", GraphAnnouncer.LeafText(status.To));
            MoveResult back = g.Move(GraphDir.Up);
            Assert.AreEqual("Alpha", back.TransitionLabel);
            Assert.AreEqual("Valid", GraphAnnouncer.LeafText(back.To));
        }

        [TestMethod]
        public void AnEmptyCellReadsBlank()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Name", "Ships" });
                s.Row(Vt("Alpha"), _rowA, null, () => "   ");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.AreEqual("blank", GraphAnnouncer.LeafText(g.Move(GraphDir.Right).To));
        }

        [TestMethod]
        public void SheetCellsAreRawNodesSoTheyCarryNoAutoPosition()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            foreach (GraphNode n in g.Current.Order) Assert.AreEqual(0, n.PositionCount);
        }

        [TestMethod]
        public void RowsAreTaggedWithTheirRegionAndStayInOneStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets");
                s.Row(Vt("Alpha"), _rowA, null);
                s.Region("Systems");
                s.Row(Vt("Sol"), _rowB, null);
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.AreEqual("t:reg:0", g.CurrentNode.RegionKey);

            MoveResult jump = g.MoveRegion(1);
            Assert.IsTrue(jump.Moved);
            Assert.AreEqual("t:reg:1", jump.To.RegionKey);
            Assert.AreEqual("Systems, Sol", GraphAnnouncer.Compose(g.Current.Order[0], jump.To));
        }

        /// <summary>A popup's shape: a strip of buttons, the paragraph it opened with, its table, and
        /// the buttons along the bottom - all in one Tab stop, so every seam is the builder's.</summary>
        private GraphRender ProseOverTable(bool prose)
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(new SyntheticNode(Id("next"), Vt("Next"))).AddItem(new SyntheticNode(Id("prev"), Vt("Previous"))).EndRow();
            if (prose) b.AddNode(new SyntheticNode(Id("words"), Vt("Something happened.")));

            GraphSheet s = new GraphSheet(b, "t:");
            _sheet = s;
            s.Region("Report", new[] { "Name", "Ships", "Move" });
            if (prose) s.Follows(Id("words"));
            s.Row(Vt("Alpha"), _rowA, null, () => "3", () => "5");
            s.Row(Vt("Beta"), _rowB, null, () => "2", () => "4");
            s.Finish();

            b.StartRow().AddItem(new SyntheticNode(Id("done"), Vt("Done"))).EndRow();
            return b.Build();
        }

        // The sheet ProseOverTable built, so the wiring assertions can ask it which id it minted for
        // a cell rather than re-spelling its private key format.
        private GraphSheet _sheet;

        private string Cell(object rowRef, int col)
        {
            return _sheet.CellKey(rowRef, col);
        }

        [TestMethod]
        public void EveryCellOfTheTopRowReachesTheStripAboveIt()
        {
            GraphRender r = ProseOverTable(false);
            Assert.AreEqual("next", DestKey(Node(r, Cell(_rowA, 0)), GraphDir.Up));
            Assert.AreEqual("next", DestKey(Node(r, Cell(_rowA, 1)), GraphDir.Up));
            Assert.AreEqual("next", DestKey(Node(r, Cell(_rowA, 2)), GraphDir.Up));
            Assert.AreEqual(Cell(_rowA, 0), DestKey(Node(r, "next"), GraphDir.Down));
        }

        [TestMethod]
        public void EveryCellOfTheBottomRowReachesTheStripBelowIt()
        {
            GraphRender r = ProseOverTable(false);
            Assert.AreEqual("done", DestKey(Node(r, Cell(_rowB, 0)), GraphDir.Down));
            Assert.AreEqual("done", DestKey(Node(r, Cell(_rowB, 1)), GraphDir.Down));
            Assert.AreEqual("done", DestKey(Node(r, Cell(_rowB, 2)), GraphDir.Down));
            Assert.AreEqual(Cell(_rowB, 0), DestKey(Node(r, "done"), GraphDir.Up));
        }

        [TestMethod]
        public void ATableToldToFollowANodeMeetsItAsThoughItWereTheRowAbove()
        {
            GraphRender r = ProseOverTable(true);
            string a0 = Cell(_rowA, 0);
            Assert.AreEqual("words", DestKey(Node(r, a0), GraphDir.Up));
            Assert.AreEqual("words", DestKey(Node(r, Cell(_rowA, 1)), GraphDir.Up));
            Assert.AreEqual("words", DestKey(Node(r, Cell(_rowA, 2)), GraphDir.Up));
            Assert.AreEqual(a0, DestKey(Node(r, "words"), GraphDir.Down));

            // and the strip above stops at the words rather than reaching over them into the table
            Assert.AreEqual("words", DestKey(Node(r, "next"), GraphDir.Down));
            Assert.AreEqual("next", DestKey(Node(r, "words"), GraphDir.Up));
        }

        [TestMethod]
        public void CrossingUpOntoAFollowedNodeIsUnlabeled()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() => ProseOverTable(true), state);
            g.Rerender();
            g.Move(GraphDir.Down); // the strip -> the words
            g.Move(GraphDir.Down); // the words -> Alpha
            g.Move(GraphDir.Right); // Alpha / Ships
            MoveResult up = g.Move(GraphDir.Up);
            Assert.IsTrue(up.Moved);
            Assert.IsNull(up.TransitionLabel); // a followed node is nobody's row name
            Assert.AreEqual("Something happened.", GraphAnnouncer.LeafText(up.To));
        }

        [TestMethod]
        public void TheSheetNamesItsFirstRowSoAScreenNeverRebuildsAKey()
        {
            GraphBuilder b = new GraphBuilder();
            GraphSheet s = new GraphSheet(b, "t:");
            Assert.IsNull(s.FirstRow);
            s.Region("Report");
            s.Row(Vt("Alpha"), _rowA, null);
            s.Row(Vt("Beta"), _rowB, null);
            s.Finish();
            Assert.AreEqual(s.CellKey(_rowA, 0), s.FirstRow.StructuralKey);
            Assert.AreSame(_rowA, s.FirstRow.Subject);

            b.SetStart(s.FirstRow);
            Assert.AreEqual(s.FirstRow.StructuralKey, b.Build().StartKey.StructuralKey);
        }

        private static string Say(MoveResult move)
        {
            return GraphAnnouncer.Compose(move.From, move.To, move.TransitionLabel);
        }

        [TestMethod]
        public void TheRowPositionIsSaidOnArrivalAndOnRowChangesOnly()
        {
            GraphAnnouncer.PositionText = (index, count) => index + " of " + count;
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            Assert.AreEqual("Fleets, table, Alpha, 1 of 2", GraphAnnouncer.ComposeFull(g.CurrentNode));

            // Along the row: the row has not changed, so its position is not said again.
            Assert.AreEqual("Ships, 3", Say(g.Move(GraphDir.Right)));

            // A different row, reached off-primary: said.
            Assert.AreEqual("Beta, 2, 2 of 2", Say(g.Move(GraphDir.Down)));

            // Back onto column 0 of the row we are already in: the column is named, the position is
            // not - the row has not changed.
            Assert.AreEqual("Name, Beta", Say(g.Move(GraphDir.Left)));

            Assert.AreEqual("Alpha, 1 of 2", Say(g.Move(GraphDir.Up)));
        }

        [TestMethod]
        public void ACellSaysNoPositionOfItsOwn()
        {
            GraphBuilder b = new GraphBuilder();
            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Fleets", new[] { "Name", "Ships", "Move" });
            s.Row(Vt("Alpha"), _rowA, null, () => "3", () => "5");
            s.Row(Vt("Beta"), _rowB, null, () => "2", () => "4");
            s.Finish();
            GraphRender render = b.Build();
            foreach (GraphNode node in render.Order)
            {
                Assert.AreEqual(0, node.PositionCount);
            }
        }

        [TestMethod]
        public void EachRegionCountsItsOwnRows()
        {
            GraphAnnouncer.PositionText = (index, count) => index + " of " + count;
            object rowC = new object();
            GraphBuilder b = new GraphBuilder();
            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Fleets");
            s.Row(Vt("Alpha"), _rowA, null);
            s.Row(Vt("Beta"), _rowB, null);
            s.Region("Ships");
            s.Row(Vt("Gamma"), rowC, null);
            s.Finish();
            GraphRender render = b.Build();

            Assert.AreEqual(
                "Fleets, Alpha, 1 of 2",
                GraphAnnouncer.ComposeFull(Node(render, s.CellKey(_rowA, 0)))
            );
            Assert.AreEqual(
                "Ships, Gamma, 1 of 1",
                GraphAnnouncer.ComposeFull(Node(render, s.CellKey(rowC, 0)))
            );
        }

        [TestMethod]
        public void ARowThatHasMovedStillReadsAsTheSameRow()
        {
            GraphAnnouncer.PositionText = (index, count) => index + " of " + count;
            GraphState state = new GraphState();
            bool swapped = false;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Name", "Ships" });
                if (swapped)
                {
                    s.Row(Vt("Beta"), _rowB, null, () => "2");
                    s.Row(Vt("Alpha"), _rowA, null, () => "3");
                }
                else
                {
                    s.Row(Vt("Alpha"), _rowA, null, () => "3");
                    s.Row(Vt("Beta"), _rowB, null, () => "2");
                }

                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();

            // A re-sort while the cursor stands still: stepping across the row afterwards is still a
            // step within ONE row, because the row is identified by what it stands for.
            swapped = true;
            g.Rerender();
            Assert.AreEqual("Ships, 3", Say(g.Move(GraphDir.Right)));
        }

        [TestMethod]
        public void ThePrimaryCellCarriesTheRowObjectSoFocusFollowsAReorder()
        {
            GraphState state = new GraphState();
            bool swapped = false;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets");
                if (swapped) { s.Row(Vt("Beta"), _rowB, null); s.Row(Vt("Alpha"), _rowA, null); }
                else { s.Row(Vt("Alpha"), _rowA, null); s.Row(Vt("Beta"), _rowB, null); }
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.AreEqual("Beta", GraphAnnouncer.LeafText(g.CurrentNode));

            swapped = true;
            g.Rerender();
            Assert.AreEqual("Beta", GraphAnnouncer.LeafText(g.CurrentNode));
            Assert.AreSame(_rowB, g.CurrentNode.Id.Subject);
        }

        /// <summary>One row, emitted with or without the widget the game draws it as.</summary>
        private static GraphRender OneRow(object rowWidget)
        {
            GraphBuilder b = new GraphBuilder();
            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Fleets", new[] { "Name", "Ships" });
            s.Row(Vt("Alpha"), new object(), rowWidget, () => "3");
            s.Finish();
            return b.Build();
        }

        // A sheet handed the row's widget can vouch for every cell of that row, and says so in the
        // nature it declares - that is what puts the row under the host's existence gate. It is per
        // CELL, not per row: the metadata cells are the ones a pooled table leaves holding the
        // previous binding's words.
        [TestMethod]
        public void ARowGivenTheWidgetTheGameDrawsItAsDeclaresItsCellsDrawn()
        {
            object widget = new object();
            GraphRender r = OneRow(widget);
            Assert.AreEqual(2, r.Order.Count);
            foreach (GraphNode node in r.Order)
            {
                Assert.IsInstanceOfType(node.Declared, typeof(DrawnNode));
                DrawnNode drawn = (DrawnNode)node.Declared;
                Assert.AreSame(widget, drawn.DrawnBy);
            }
        }

        // And without one it claims nothing it cannot back: a grid the mod laid out has no widget any
        // cell of it stands on, and a node that pretended otherwise would be gated on somebody else's
        // rectangle.
        [TestMethod]
        public void ARowWithNoWidgetDeclaresItsCellsSynthetic()
        {
            GraphRender r = OneRow(null);
            Assert.AreEqual(2, r.Order.Count);
            foreach (GraphNode node in r.Order)
            {
                Assert.IsInstanceOfType(node.Declared, typeof(SyntheticNode));
            }
        }
    }
}
