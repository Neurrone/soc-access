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
        /// <summary>A long thin region is a ridge, named by the way it runs; a compact one is a
        /// patch, and one cell is one cell.</summary>
        [TestMethod]
        public void ElevatedRegionsAreNamedByTheirShapeAndTheirAxis()
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

        /// <summary>A column of cells runs up the board, however much the odd rows zigzag it.
        /// </summary>
        [TestMethod]
        public void AColumnOfRaisedCellsIsAVerticalRidge()
        {
            BattlefieldTerrain terrain = Analyse(
                ".........",
                "..1......",
                "..1......",
                "..1......",
                "..1......",
                "..1......",
                ".........");

            List<BattlefieldRegion> elevated = RegionsOf(terrain, BattlefieldRegionKind.Elevated);
            Assert.AreEqual(1, elevated.Count);
            Assert.AreEqual(BattlefieldRegionShape.VerticalRidge, elevated[0].Shape);
        }

        /// <summary>The reference case the shape rule was fixed against: the band across the middle
        /// of Hills3, which a bounding box calls square and its own axis calls a diagonal ridge.
        /// </summary>
        [TestMethod]
        public void Hills3sCentreBandIsADiagonalRidge()
        {
            BattlefieldTerrain terrain = Analyse(
                ".............",
                ".............",
                ".............",
                "......11.....",
                ".....111.....",
                "....11.......",
                ".............",
                ".............",
                ".............");

            List<BattlefieldRegion> elevated = RegionsOf(terrain, BattlefieldRegionKind.Elevated);
            Assert.AreEqual(1, elevated.Count);
            Assert.AreEqual(7, elevated[0].Count);
            Assert.AreEqual(BattlefieldRegionShape.DiagonalRidge, elevated[0].Shape);
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

        /// <summary>A structure byte on ground nothing can enter is not a structure: a tower nobody
        /// can walk onto is impassable, whatever the layout drew there.</summary>
        [TestMethod]
        public void AnImpassableCellIsImpassableWhateverIsBuiltOnIt()
        {
            BattlefieldTerrain terrain = Analyse(
                new[] { "2#...", "3....", "1...." },
                new[] { "WT...", "T....", "S...." });

            Assert.AreEqual(BattlefieldCellKind.Impassable, terrain.GetKind(new Vector2Int(1, 2)));
            Assert.AreEqual(BattlefieldCellKind.Wall, terrain.GetKind(new Vector2Int(0, 2)));
            Assert.AreEqual(1, RegionsOf(terrain, BattlefieldRegionKind.Impassable).Count);
            Assert.AreEqual(1, RegionsOf(terrain, BattlefieldRegionKind.Wall)[0].Count);
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

        /// <summary>The flood starts where the layout sets troops down, not on flat ground: the
        /// left column here is raised two above the rest, so nothing can climb it from the flat
        /// ground beside it, and a spawn point standing on it says a troop is there all the same.
        /// RootsHillSiege, raised from edge to edge, was one cliff of 83 cells before this.
        /// </summary>
        [TestMethod]
        public void TheFloodStartsFromTheSpawnPointsAndNotFromFlatGround()
        {
            string[] rows = { "22...", "22...", "22..." };

            BattlefieldTerrain unseeded = Analyse(rows);
            Assert.AreEqual(6, RegionsOf(unseeded, BattlefieldRegionKind.Cliff)[0].Count);

            BattlefieldTerrain terrain = Analyse(
                rows, null, new[] { new Vector2Int(0, 0), new Vector2Int(4, 0) });

            Assert.AreEqual(0, RegionsOf(terrain, BattlefieldRegionKind.Cliff).Count);
            Assert.AreEqual(0, RegionsOf(terrain, BattlefieldRegionKind.Unreachable).Count);
            List<BattlefieldRegion> elevated = RegionsOf(terrain, BattlefieldRegionKind.Elevated);
            Assert.AreEqual(1, elevated.Count);
            Assert.AreEqual(6, elevated[0].Count);
        }

        /// <summary>Walkable ground the flood never reaches is unreachable ground, not a cliff: a
        /// floor sealed behind blocked cells, and the raised cell inside it that a troop standing
        /// on that floor could step onto - "every step onto it is two heights or more" is untrue of
        /// it, so the cliff word is not its. A raised cell with nothing beside it to be stood on
        /// stays a cliff.</summary>
        [TestMethod]
        public void ASealedPocketIsUnreachableGroundAndOnlyGroundNothingCanBeStoodBesideIsACliff()
        {
            BattlefieldTerrain terrain = Analyse(
                new[]
                {
                    "..1####",
                    "###....",
                    ".......",
                    "...2...",
                    "......."
                },
                null,
                new[] { new Vector2Int(0, 0) });

            Assert.AreEqual(BattlefieldCellKind.Unreachable, terrain.GetKind(new Vector2Int(0, 4)));
            Assert.AreEqual(BattlefieldCellKind.Unreachable, terrain.GetKind(new Vector2Int(2, 4)));
            List<BattlefieldRegion> unreachable = RegionsOf(terrain, BattlefieldRegionKind.Unreachable);
            Assert.AreEqual(1, unreachable.Count);
            Assert.AreEqual(3, unreachable[0].Count);

            Assert.AreEqual(BattlefieldCellKind.Cliff, terrain.GetKind(new Vector2Int(3, 1)));
            List<BattlefieldRegion> cliffs = RegionsOf(terrain, BattlefieldRegionKind.Cliff);
            Assert.AreEqual(1, cliffs.Count);
            Assert.AreEqual(1, cliffs[0].Count);
        }

        /// <summary>Two minimal cut sets sharing a cell are one way through, not two: they become
        /// one group over the union of their cells, which is also what makes the group's first cell
        /// - the placeholder an authored description points at - name one group again.</summary>
        [TestMethod]
        public void ChokeSetsThatShareACellAreOneGroup()
        {
            BattlefieldTerrain terrain = Analyse(
                "....#....",
                ".#.......",
                ".#.......",
                "#........",
                ".........",
                ".........",
                ".........");

            List<BattlefieldRegion> chokePoints = RegionsOf(terrain, BattlefieldRegionKind.ChokePoint);
            Assert.AreEqual(1, chokePoints.Count);
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(2, 5), new Vector2Int(3, 5), new Vector2Int(3, 6) },
                chokePoints[0].Cells);
        }

        private static BattlefieldTerrain Analyse(params string[] rows)
        {
            return Analyse(rows, null, null);
        }

        private static BattlefieldTerrain Analyse(string[] rows, string[] decorationRows)
        {
            return Analyse(rows, decorationRows, null);
        }

        /// <summary>Terrain rows top first: <c>' '</c> off the grid, <c>'#'</c> impassable,
        /// <c>'.'</c> flat, a digit raised ground of that height. The decoration rows, where a
        /// layout has any, mark <c>'W'</c> a wall, <c>'T'</c> a tower and <c>'S'</c> stairs, which
        /// only a siege layout has. The spawn cells are where the layout sets troops down, which is
        /// where walking from starts; with none given the reading falls back to the board's lowest
        /// ground, which is what every board with flat ground on it starts from anyway.</summary>
        private static BattlefieldTerrain Analyse(
            string[] rows, string[] decorationRows, Vector2Int[] spawnPoints)
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

            return BattlefieldTerrain.Analyse(
                new Vector2Int(width, height), cells, decorationRows != null, false, spawnPoints);
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
