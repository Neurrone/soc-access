using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>The words the battlefield's ground is spoken in: what the cursor says about one
    /// blocked cell and what the scanner calls a group of them. Rows are written top first, as the
    /// layout dumps draw them.</summary>
    [TestClass]
    public sealed class BattlefieldTextTests
    {
        /// <summary>In a fight a blocked cell says what is standing on it. A cell full of small
        /// things takes the plural, a cell holding one thing the singular.</summary>
        [TestMethod]
        public void TheCursorNamesWhatBlocksACellInCombat()
        {
            Assert.AreEqual("boulders, impassable", CellAt(ArleonTheme, "..B", 2, 0));
            Assert.AreEqual("bushes, impassable", CellAt(ArleonTheme, "..G", 2, 0));
            Assert.AreEqual("a torch, impassable", CellAt(ArleonTheme, "..L", 2, 0));
            Assert.AreEqual("a knight statue, impassable", CellAt(ArleonTheme, "..M", 2, 0));
            Assert.AreEqual("fire, impassable", CellAt(ArleonTheme, "..F", 2, 0));
            Assert.AreEqual("water, impassable", CellAt(ArleonTheme, "..W", 2, 0));
        }

        /// <summary>Every theme draws its own props, so the word follows the ground the battle was
        /// joined on.</summary>
        [TestMethod]
        public void TheWordForAnObstacleFollowsTheThemeTheBoardIsPaintedWith()
        {
            Assert.AreEqual("pink mushrooms, impassable", CellAt(3, "..G", 2, 0));
            Assert.AreEqual("a standing stone, impassable", CellAt(4, "..M", 2, 0));
            Assert.AreEqual("glowing blue mushrooms, impassable", CellAt(5, "..L", 2, 0));
        }

        /// <summary>A group of blocked cells is named by what is on it and how much of it there is,
        /// and a long thin one is a wall of it. Where the group holds more than one thing, the
        /// commonest comes first.</summary>
        [TestMethod]
        public void TheScannerNamesAGroupOfBlockedCellsByWhatIsOnThem()
        {
            Assert.AreEqual("bushes, impassable", ImpassableGroup(ArleonTheme, "G..", "...", "..."));
            Assert.AreEqual("bushes, 3 cells, impassable", ImpassableGroup(ArleonTheme, "GG.", "G..", "..."));
            Assert.AreEqual("wall of 5 bushes, impassable", ImpassableGroup(ArleonTheme, ".....", "GGGGG", "....."));
            Assert.AreEqual(
                "wall of 6 bushes and boulders, impassable",
                ImpassableGroup(ArleonTheme, "......", "GGGGBB", "......"));
        }

        /// <summary>The placement page has nothing to name: its preview draws every blocked cell as
        /// the same styleless puck, so the words there stay the ones it always said.</summary>
        [TestMethod]
        public void ThePlacementPageStillSaysImpassableAndNothingElse()
        {
            BattlefieldTerrain terrain = Analyse(ArleonTheme, false, "......", "GGGGBB", "......");
            Assert.AreEqual(
                "wall of 6 impassable cells",
                BattlefieldText.Region(First(terrain, BattlefieldRegionKind.Impassable)));
            Assert.IsNull(terrain.GetObstacle(new Vector2Int(0, 1)));
        }

        private const int ArleonTheme = 0;

        private static string CellAt(int theme, string row, int x, int y)
        {
            BattlefieldTerrain terrain = Analyse(theme, true, row);
            return BattlefieldText.CellImpassable(terrain.GetObstacle(new Vector2Int(x, y)));
        }

        private static string ImpassableGroup(int theme, params string[] rows)
        {
            return BattlefieldText.Region(First(Analyse(theme, true, rows), BattlefieldRegionKind.Impassable));
        }

        private static BattlefieldRegion First(BattlefieldTerrain terrain, BattlefieldRegionKind kind)
        {
            foreach (BattlefieldRegion region in terrain.Regions)
            {
                if (region.Kind == kind)
                {
                    return region;
                }
            }

            Assert.Fail("no " + kind + " region");
            return null;
        }

        /// <summary>Rows top first: <c>'.'</c> flat ground, and a letter a blocked cell holding
        /// boulders, growth, a monument, a light, fire or water.</summary>
        private static BattlefieldTerrain Analyse(int theme, bool namesObstacles, params string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            List<BattlefieldCell> cells = new List<BattlefieldCell>(width * height);
            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];
                for (int x = 0; x < width; x++)
                {
                    char glyph = row[x];
                    cells.Add(new BattlefieldCell(
                        new Vector2Int(x, y),
                        true,
                        0,
                        glyph != '.',
                        Decoration(glyph),
                        glyph == 'F' ? 5 : 0,
                        glyph == 'W',
                        theme));
                }
            }

            return BattlefieldTerrain.Analyse(new Vector2Int(width, height), cells, false, namesObstacles);
        }

        private static int Decoration(char glyph)
        {
            switch (glyph)
            {
                case 'B': return 4;
                case 'L': return 5;
                case 'G': return 9;
                case 'M': return 10;
                default: return 0;
            }
        }
    }
}
