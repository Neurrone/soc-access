using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Battlefields;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>What the shared reading of a battlefield's ground makes of a hand-drawn board.
    /// Rows are written top first, as the layout dumps draw them.</summary>
    [TestClass]
    public sealed class BattlefieldTerrainTests
    {
        /// <summary>A long thin region is a ridge, a compact one a patch, and one cell is one cell.
        /// </summary>
        [TestMethod]
        public void ElevatedRegionsAreNamedRidgePatchAndSingleCellByTheirShape()
        {
            BattlefieldTerrain terrain = Analyse(
                "1111111..",
                ".........",
                "..11.....",
                "..11.....",
                ".........",
                ".........",
                "1........");

            List<BattlefieldRegion> elevated = RegionsOf(terrain, BattlefieldRegionKind.Elevated);
            Assert.AreEqual(3, elevated.Count);
            Assert.AreEqual(BattlefieldRegionShape.SingleCell, ShapeOfSize(elevated, 1));
            Assert.AreEqual(BattlefieldRegionShape.Patch, ShapeOfSize(elevated, 4));
            Assert.AreEqual(BattlefieldRegionShape.Ridge, ShapeOfSize(elevated, 7));
        }

        /// <summary>Raised ground every step onto which is two heights or more is a cliff: nothing
        /// can enter it, so it is not a platform and not an elevated region.</summary>
        [TestMethod]
        public void RaisedGroundNothingCanStepOntoIsACliff()
        {
            BattlefieldTerrain terrain = Analyse(
                ".....",
                ".....",
                "..2..");

            Assert.AreEqual(BattlefieldCellKind.Cliff, terrain.GetKind(new Vector2Int(2, 0)));
            Assert.AreEqual(0, RegionsOf(terrain, BattlefieldRegionKind.Elevated).Count);
            List<BattlefieldRegion> cliffs = RegionsOf(terrain, BattlefieldRegionKind.Cliff);
            Assert.AreEqual(1, cliffs.Count);
            Assert.AreEqual(1, cliffs[0].Count);
            Assert.AreEqual(2, cliffs[0].Height);
        }

        /// <summary>Impassable cells in a line are a wall; the same cells scattered would not be.
        /// </summary>
        [TestMethod]
        public void ImpassableCellsInALineAreAWall()
        {
            BattlefieldTerrain terrain = Analyse(
                ".....",
                ".....",
                "####.");

            List<BattlefieldRegion> impassable = RegionsOf(terrain, BattlefieldRegionKind.Impassable);
            Assert.AreEqual(1, impassable.Count);
            Assert.AreEqual(4, impassable[0].Count);
            Assert.AreEqual(BattlefieldRegionShape.Ridge, impassable[0].Shape);
        }

        /// <summary>The one cell a barrier leaves open is a choke point of its own.</summary>
        [TestMethod]
        public void TheOnlyGapInABarrierIsAChokePointOfOneCell()
        {
            BattlefieldTerrain terrain = Analyse(
                ".......",
                ".......",
                ".......",
                "###.###",
                ".......",
                ".......",
                ".......");

            List<BattlefieldRegion> single = ChokePointsOfSize(terrain, 1);
            Assert.AreEqual(1, single.Count);
            Assert.AreEqual(new Vector2Int(3, 3), single[0].Cells[0]);
        }

        /// <summary>A gap of two is one choke point of two cells, and neither of them is one on its
        /// own: only the smallest set that splits the board is reported.</summary>
        [TestMethod]
        public void AGapOfTwoIsOneChokePointOfTwoCells()
        {
            BattlefieldTerrain terrain = Analyse(
                ".......",
                ".......",
                ".......",
                "##..###",
                ".......",
                ".......",
                ".......");

            List<BattlefieldRegion> chokePoints = RegionsOf(terrain, BattlefieldRegionKind.ChokePoint);
            Assert.AreEqual(1, chokePoints.Count);
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(2, 3), new Vector2Int(3, 3) },
                chokePoints[0].Cells);
        }

        /// <summary>A siege layout's decoration byte says what is built on a cell. Those cells are
        /// walls, towers and stairs, never elevated ground.</summary>
        [TestMethod]
        public void SiegeDecorationsAreWallsTowersAndStairsRatherThanElevatedGround()
        {
            BattlefieldTerrain terrain = Analyse(
                new[] { "22...", "3....", "1...." },
                new[] { "WW...", "T....", "S...." });

            Assert.AreEqual(BattlefieldCellKind.Wall, terrain.GetKind(new Vector2Int(1, 2)));
            Assert.AreEqual(BattlefieldCellKind.Tower, terrain.GetKind(new Vector2Int(0, 1)));
            Assert.AreEqual(BattlefieldCellKind.Stairs, terrain.GetKind(new Vector2Int(0, 0)));
            Assert.AreEqual(0, RegionsOf(terrain, BattlefieldRegionKind.Elevated).Count);
            Assert.AreEqual(2, RegionsOf(terrain, BattlefieldRegionKind.Wall)[0].Count);
            Assert.AreEqual(2, RegionsOf(terrain, BattlefieldRegionKind.Wall)[0].Height);
            Assert.AreEqual(3, RegionsOf(terrain, BattlefieldRegionKind.Tower)[0].Height);
            Assert.AreEqual(1, RegionsOf(terrain, BattlefieldRegionKind.Stairs)[0].Height);
        }

        /// <summary>An odd row is half a cell to the right and its last column falls off the board.
        /// A cell off the grid is no kind at all and joins no region.</summary>
        [TestMethod]
        public void ACellOffTheGridJoinsNoRegion()
        {
            BattlefieldTerrain terrain = Analyse(
                ".....",
                "1... ",
                ".....");

            Assert.AreEqual(BattlefieldCellKind.OffGrid, terrain.GetKind(new Vector2Int(4, 1)));
            List<BattlefieldRegion> elevated = RegionsOf(terrain, BattlefieldRegionKind.Elevated);
            Assert.AreEqual(1, elevated.Count);
            Assert.AreEqual(1, elevated[0].Count);
            Assert.AreEqual(new Vector2Int(0, 1), elevated[0].Cells[0]);
        }

        private static BattlefieldTerrain Analyse(params string[] rows)
        {
            return Analyse(rows, null);
        }

        /// <summary>Terrain rows top first: <c>' '</c> off the grid, <c>'#'</c> impassable,
        /// <c>'.'</c> flat, a digit raised ground of that height. The decoration rows, where a
        /// layout has any, mark <c>'W'</c> a wall, <c>'T'</c> a tower and <c>'S'</c> stairs, which
        /// only a siege layout has.</summary>
        private static BattlefieldTerrain Analyse(string[] rows, string[] decorationRows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            List<BattlefieldCell> cells = new List<BattlefieldCell>(width * height);
            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];
                string decorations = decorationRows != null ? decorationRows[height - 1 - y] : null;
                for (int x = 0; x < width; x++)
                {
                    char glyph = row[x];
                    int decoration = Decoration(decorations != null ? decorations[x] : '.');
                    cells.Add(new BattlefieldCell(
                        new Vector2Int(x, y),
                        glyph != ' ',
                        glyph >= '1' && glyph <= '9' ? glyph - '0' : 0,
                        glyph == '#',
                        decoration));
                }
            }

            return BattlefieldTerrain.Analyse(new Vector2Int(width, height), cells, decorationRows != null);
        }

        private static int Decoration(char glyph)
        {
            switch (glyph)
            {
                case 'T': return 6;
                case 'W': return 7;
                case 'S': return 8;
                default: return 0;
            }
        }

        private static List<BattlefieldRegion> RegionsOf(BattlefieldTerrain terrain, BattlefieldRegionKind kind)
        {
            List<BattlefieldRegion> regions = new List<BattlefieldRegion>();
            foreach (BattlefieldRegion region in terrain.Regions)
            {
                if (region.Kind == kind)
                {
                    regions.Add(region);
                }
            }

            return regions;
        }

        private static List<BattlefieldRegion> ChokePointsOfSize(BattlefieldTerrain terrain, int count)
        {
            List<BattlefieldRegion> matches = new List<BattlefieldRegion>();
            foreach (BattlefieldRegion region in RegionsOf(terrain, BattlefieldRegionKind.ChokePoint))
            {
                if (region.Count == count)
                {
                    matches.Add(region);
                }
            }

            return matches;
        }

        private static BattlefieldRegionShape ShapeOfSize(List<BattlefieldRegion> regions, int count)
        {
            foreach (BattlefieldRegion region in regions)
            {
                if (region.Count == count)
                {
                    return region.Shape;
                }
            }

            Assert.Fail("no region of " + count + " cells");
            return BattlefieldRegionShape.None;
        }
    }
}
