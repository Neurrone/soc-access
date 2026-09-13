using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Localization;
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
            Assert.AreEqual("torch, impassable", CellAt(ArleonTheme, "..L", 2, 0));
            Assert.AreEqual("knight statue, impassable", CellAt(ArleonTheme, "..M", 2, 0));
            Assert.AreEqual("fire, impassable", CellAt(ArleonTheme, "..F", 2, 0));
            Assert.AreEqual("water, impassable", CellAt(ArleonTheme, "..W", 2, 0));
        }

        /// <summary>Every theme draws its own props, so the word follows the ground the battle was
        /// joined on.</summary>
        [TestMethod]
        public void TheWordForAnObstacleFollowsTheThemeTheBoardIsPaintedWith()
        {
            Assert.AreEqual("pink mushrooms, impassable", CellAt(3, "..G", 2, 0));
            Assert.AreEqual("standing stone, impassable", CellAt(4, "..M", 2, 0));
            Assert.AreEqual("glowing blue mushrooms, impassable", CellAt(5, "..L", 2, 0));
        }

        /// <summary>THE ARTICLE IS THE DESCRIPTION'S, not the cursor's. A cursor line is a label, so
        /// the noun it names an obstacle with is bare.</summary>
        [TestMethod]
        public void TheCursorSpeaksAnObstacleAsABareNoun()
        {
            Assert.AreEqual("torch, impassable", CellAt(ArleonTheme, "..L", 2, 0));
        }

        /// <summary>A scanner item is a label too, whether the group under it is one cell or many.
        /// </summary>
        [TestMethod]
        public void TheScannerSpeaksAnObstacleAsABareNoun()
        {
            Assert.AreEqual("torch, impassable", ImpassableGroup(ArleonTheme, "L..", "...", "..."));
        }

        /// <summary>A description is prose, and the same torch is a noun phrase inside it.</summary>
        [TestMethod]
        public void ADescriptionNamesTheSameObstacleWithItsArticle()
        {
            Assert.AreEqual("a torch", Expand("{0,1}", Analyse(ArleonTheme, true, "...", "L..", "...")));
        }

        /// <summary>More than one of a thing is the same word on both surfaces - an English plural
        /// carries no article - and only the wording around it differs.</summary>
        [TestMethod]
        public void AGroupOfObstaclesIsThePluralOnBothSurfaces()
        {
            Assert.AreEqual(
                "wall of 5 torches, impassable",
                ImpassableGroup(ArleonTheme, ".....", "LLLLL", "....."));
            Assert.AreEqual(
                "a wall of torches",
                Expand("{0,1}", Analyse(ArleonTheme, true, ".....", "LLLLL", ".....")));
        }

        /// <summary>THE WALL FRAME CARRIES THE ARTICLE, so the words inside it are the bare ones -
        /// "a wall of bushes" is built from the same plural the scanner speaks, never from the
        /// description form a group standing on its own reads as. English spells the two alike in
        /// the plural and only keeps them apart in the singular, so it is French that hears the
        /// difference: a group on its own is "des buissons" and the same group in a wall is "un mur
        /// de buissons", which only the bare form gives.</summary>
        [TestMethod]
        public void AWallOfObstaclesIsBuiltFromTheBarePlural()
        {
            BattlefieldRegion wall = First(
                Analyse(ArleonTheme, true, ".....", "GGGGG", "....."), BattlefieldRegionKind.Impassable);

            Assert.AreEqual(
                ModText.Get(
                    ModStrings.Battlefield.DescriptionObstacleWall,
                    BattlefieldText.ObstacleWord(wall.Obstacle, wall.Count, true)),
                BattlefieldText.RegionDescription(wall));

            Assert.AreEqual("a bush", BattlefieldText.ObstacleWord(wall.Obstacle, 1, false));
            Assert.AreEqual("bush", BattlefieldText.ObstacleWord(wall.Obstacle, 1, true));
        }

        /// <summary>A group of blocked cells is named by what is on it and how much of it there is,
        /// and a long thin one is a wall of it.</summary>
        [TestMethod]
        public void TheScannerNamesAGroupOfBlockedCellsByWhatIsOnThem()
        {
            Assert.AreEqual("bushes, impassable", ImpassableGroup(ArleonTheme, "G..", "...", "..."));
            Assert.AreEqual("bushes, 3 cells, impassable", ImpassableGroup(ArleonTheme, "GG.", "G..", "..."));
            Assert.AreEqual("wall of 5 bushes, impassable", ImpassableGroup(ArleonTheme, ".....", "GGGGG", "....."));
        }

        /// <summary>Water and fire are not counted and not built with: a line of either is what it
        /// is and not a wall of it, in a description and in the scanner alike.</summary>
        [TestMethod]
        public void WaterAndFireAreNeverAWallOfThemselves()
        {
            Assert.AreEqual("water", Expand("{0,1}", Analyse(ArleonTheme, true, ".....", "WWWWW", ".....")));
            Assert.AreEqual("fire", Expand("{0,1}", Analyse(ArleonTheme, true, ".....", "FFFFF", ".....")));
            Assert.AreEqual("water, 5 cells, impassable", ImpassableGroup(ArleonTheme, ".....", "WWWWW", "....."));
            Assert.AreEqual("fire, 5 cells, impassable", ImpassableGroup(ArleonTheme, ".....", "FFFFF", "....."));
        }

        /// <summary>TOUCHING IS NOT BEING THE SAME THING. A pond that runs up against a boulder
        /// field is a pond and a boulder field, each named and counted on its own, rather than one
        /// group a sentence would have to list two words for.</summary>
        [TestMethod]
        public void APondTouchingBouldersIsTwoRegions()
        {
            BattlefieldTerrain terrain = Analyse(ArleonTheme, true, "......", "WWWWBB", "......");
            List<BattlefieldRegion> blocked = All(terrain, BattlefieldRegionKind.Impassable);

            Assert.AreEqual(2, blocked.Count);
            Assert.AreEqual("water, 4 cells, impassable", BattlefieldText.Region(blocked[0]));
            Assert.AreEqual("boulders, 2 cells, impassable", BattlefieldText.Region(blocked[1]));
            Assert.AreEqual("water", Expand("{0,1}", terrain));
            Assert.AreEqual("boulders", Expand("{4,1}", terrain));
        }

        /// <summary>A group is one family of prop, so nothing it is ever called lists two of them:
        /// the word inside "a wall of ..." is one word, and every cell of the group answers to it.
        /// </summary>
        [TestMethod]
        public void NoGroupOfBlockedGroundCarriesTwoWords()
        {
            BattlefieldTerrain terrain = Analyse(ArleonTheme, true, "GGBBWW", "GBMLFW", "..PP..");

            foreach (BattlefieldRegion region in All(terrain, BattlefieldRegionKind.Impassable))
            {
                Assert.AreEqual(region.ObstacleKind, region.Obstacle.Kind, region.Key);
                for (int i = 0; i < region.Cells.Count; i++)
                {
                    Assert.AreEqual(
                        region.ObstacleKind,
                        terrain.GetObstacle(region.Cells[i]).Kind,
                        "cell " + region.Cells[i] + " of " + region.Key);
                }
            }
        }

        /// <summary>The placement page reads the same bytes and splits blocked ground the same way;
        /// only the words are withheld there, because nothing has painted the board yet.</summary>
        [TestMethod]
        public void ThePlacementPageSplitsBlockedGroundWhereTheFightDoes()
        {
            string[] rows = { "GGBBWW", "GBMLFW", "..PP.." };
            CollectionAssert.AreEqual(
                Groups(Analyse(ArleonTheme, true, rows)), Groups(Analyse(ArleonTheme, false, rows)));
        }

        /// <summary>The cells of every group of blocked ground, one string per group, so two
        /// readings of the same board can be compared as a whole.</summary>
        private static List<string> Groups(BattlefieldTerrain terrain)
        {
            List<string> groups = new List<string>();
            foreach (BattlefieldRegion region in All(terrain, BattlefieldRegionKind.Impassable))
            {
                List<string> points = new List<string>(region.Cells.Count);
                for (int i = 0; i < region.Cells.Count; i++)
                {
                    points.Add(region.Cells[i].x + "," + region.Cells[i].y);
                }

                groups.Add(string.Join(" ", points.ToArray()));
            }

            return groups;
        }

        private static List<BattlefieldRegion> All(BattlefieldTerrain terrain, BattlefieldRegionKind kind)
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

        /// <summary>The blocked pieces framing a walled town siege's gate are stonework, the same
        /// stonework in every theme, and they are the frame of a gate rather than a wall of their
        /// own: a run of them is named by the cells it covers and never as a wall.</summary>
        [TestMethod]
        public void GatepostsAreOneWordInEveryThemeAndAreNeverAWall()
        {
            Assert.AreEqual("gatepost, impassable", CellAt(ArleonTheme, "..P", 2, 0));
            Assert.AreEqual("gatepost, impassable", CellAt(4, "..P", 2, 0));
            Assert.AreEqual("gatepost, impassable", ImpassableGroup(ArleonTheme, "P..", "...", "..."));
            Assert.AreEqual("gateposts, 2 cells, impassable", ImpassableGroup(ArleonTheme, "PP.", "...", "..."));
            Assert.AreEqual(
                "gateposts, 5 cells, impassable",
                ImpassableGroup(ArleonTheme, ".....", "PPPPP", "....."));

            Assert.AreEqual("a gatepost", Expand("{0,1}", Analyse(ArleonTheme, true, "...", "P..", "...")));
            Assert.AreEqual("gateposts", Expand("{0,1}", Analyse(ArleonTheme, true, ".....", "PPPPP", ".....")));
        }

        /// <summary>An obstacle nobody has a word for - an unnamed decoration byte, a prop standing
        /// on its own - is impassable ground and nothing more, even in a fight.</summary>
        [TestMethod]
        public void AnObstacleWithNoNameStaysPlainImpassable()
        {
            BattlefieldTerrain terrain = Analyse(ArleonTheme, true, ".....", "#####", ".....");
            Assert.AreEqual(
                "wall of 5 impassable cells",
                BattlefieldText.Region(First(terrain, BattlefieldRegionKind.Impassable)));
            Assert.AreEqual(string.Empty, BattlefieldText.CellImpassable(terrain.GetObstacle(new Vector2Int(0, 1))));
        }

        /// <summary>The placement page has nothing to name: its preview draws every blocked cell as
        /// the same styleless puck, so the words there stay the ones it always said.</summary>
        [TestMethod]
        public void ThePlacementPageStillSaysImpassableAndNothingElse()
        {
            BattlefieldTerrain terrain = Analyse(ArleonTheme, false, "......", "GGGGGG", "......");
            Assert.AreEqual(
                "wall of 6 impassable cells",
                BattlefieldText.Region(First(terrain, BattlefieldRegionKind.Impassable)));
            Assert.IsNull(terrain.GetObstacle(new Vector2Int(0, 1)));
        }

        /// <summary>A description points at a feature with the coordinates of one of its cells, and
        /// the words come from the ground as it is now: the shape, and the height in parentheses.
        /// </summary>
        [TestMethod]
        public void APlaceholderBecomesTheFeatureTheGroundHas()
        {
            BattlefieldTerrain terrain = Elevated(
                ".............",
                ".............",
                "1............",
                "......11.....",
                "..1..111.....",
                "..1.11.......",
                "..1..........",
                "..1..........",
                "..1..........");

            Assert.AreEqual(
                "a diagonal ridge (height 1) and a vertical ridge (height 1)",
                Expand("{4,3} and {2,0}", terrain));
            Assert.AreEqual("a single cell (height 1)", Expand("{0,6}", terrain));
        }

        /// <summary>The same placeholder is impassable ground on the placement page and what is
        /// standing on it in the fight, because the fight is what painted it there.</summary>
        [TestMethod]
        public void APlaceholderNamesBlockedGroundByWhatTheFightPaintedOnIt()
        {
            Assert.AreEqual("an impassable cell", Expand("{0,0}", Analyse(ArleonTheme, false, "...", "...", "B..")));
            Assert.AreEqual("a wall of impassable cells", Expand("{0,1}", Analyse(ArleonTheme, false, ".....", "GGGGG", ".....")));
            Assert.AreEqual("a boulder", Expand("{0,0}", Analyse(ArleonTheme, true, "...", "...", "B..")));
            Assert.AreEqual("a wall of bushes", Expand("{0,1}", Analyse(ArleonTheme, true, ".....", "GGGGG", ".....")));
            Assert.AreEqual("a boulder and a bush", Expand("{0,1} and {1,1}", Analyse(ArleonTheme, true, "...", "BG.", "...")));
        }

        /// <summary>A placeholder pointing at no group at all is an authoring mistake: it says
        /// nothing and is reported once.</summary>
        [TestMethod]
        public void APlaceholderThatNamesNoGroupSaysNothingAndIsReported()
        {
            BattlefieldTerrain terrain = Elevated("...", "...", "1..");
            List<string> reported = new List<string>();
            string text = BattlefieldText.Expand("Open ground with {2,2}.", terrain, reported.Add);

            Assert.AreEqual("Open ground with .", text);
            CollectionAssert.AreEqual(new[] { "{2,2}" }, reported);
        }

        /// <summary>What lies around the board is one sentence: one clause per kind of ground, the
        /// directions it lies in gathered into it, and no directions at all when it is all around.
        /// </summary>
        [TestMethod]
        public void TheSurroundingsAreOneSentenceGroupedByWhatLiesThere()
        {
            Assert.AreEqual(
                "The battlefield is surrounded by forest to the west and north, mountains to the east.",
                BattlefieldText.Surroundings(new BattlefieldSurroundings(
                    AdventureTerrainKind.TemperateTrees,
                    AdventureTerrainKind.AridTrees,
                    AdventureTerrainKind.Mountain)));
            Assert.AreEqual(
                "The battlefield is surrounded by open land.",
                BattlefieldText.Surroundings(new BattlefieldSurroundings(
                    AdventureTerrainKind.Unknown,
                    AdventureTerrainKind.Deforestation,
                    AdventureTerrainKind.Obstruction)));
            Assert.AreEqual(
                "The battlefield is surrounded by water to the west, open land to the north and east.",
                BattlefieldText.Surroundings(new BattlefieldSurroundings(
                    AdventureTerrainKind.DeepWater,
                    AdventureTerrainKind.Unknown,
                    AdventureTerrainKind.Unknown)));
        }

        private const int ArleonTheme = 0;

        private static string Expand(string text, BattlefieldTerrain terrain)
        {
            return BattlefieldText.Expand(text, terrain, null);
        }

        /// <summary>Rows top first, a digit raised ground of that height and <c>'.'</c> flat.
        /// </summary>
        private static BattlefieldTerrain Elevated(params string[] rows)
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
                        new Vector2Int(x, y), true, glyph >= '1' && glyph <= '9' ? glyph - '0' : 0, false, 0));
                }
            }

            return BattlefieldTerrain.Analyse(new Vector2Int(width, height), cells, false);
        }

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
        /// boulders, growth, a monument, a light, a gatepost, fire or water.</summary>
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
                case 'P': return 11;
                default: return 0;
            }
        }
    }
}
