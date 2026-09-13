using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// THE WORDING AROUND AN AUTHORED BATTLEFIELD DESCRIPTION. The table holds three pieces of raw
    /// text and nothing else (<see cref="BattlefieldDescriptions"/>); the labels, the order and what
    /// is said about a layout nobody has described yet are the mod's, so they live here rather than
    /// beside the lookup.
    ///
    /// The placement page reads all three, labelled, because nothing has been placed yet and where
    /// each side starts is what the player is deciding about. In combat the spawns are already
    /// behind them, so the gesture there says the terrain alone and does not label it.
    ///
    /// The same vocabulary read off the ground itself is here too: <see cref="Region"/> names a
    /// group of cells the way the descriptions name it, and <see cref="CellGround"/> names one
    /// cell for the cursor.
    /// </summary>
    public static class BattlefieldText
    {
        /// <summary>A reference to a group of ground in an authored description: the raw
        /// coordinates of any one of its cells, as the dump's <c>regions</c> list them.</summary>
        private static readonly Regex RegionPlaceholder = new Regex(@"\{\s*(\d+)\s*,\s*(\d+)\s*\}");

        /// <summary>The three lines of a description, one per field so the review buffer holds three
        /// and the node speaks them as one body. Empty where the layout has no description.</summary>
        public static List<string> Lines(
            BattlefieldDescription description, BattlefieldTerrain terrain, Action<string> onUnknownRegion)
        {
            List<string> lines = new List<string>(3);
            if (description == null)
            {
                return lines;
            }

            AddLine(lines, ModStrings.Screens.DescriptionTerrain, description.Terrain, terrain, onUnknownRegion);
            AddLine(lines, ModStrings.Screens.DescriptionAttacker, description.Attacker, terrain, onUnknownRegion);
            AddLine(lines, ModStrings.Screens.DescriptionDefender, description.Defender, terrain, onUnknownRegion);
            return lines;
        }

        /// <summary>What the describe-battlefield gesture says on the placement board: the three
        /// labelled lines as one breath, or that nobody has described this layout.</summary>
        public static string Spoken(string layoutKey, BattlefieldTerrain terrain, Action<string> onUnknownRegion)
        {
            BattlefieldDescription description;
            if (!BattlefieldDescriptions.TryGet(layoutKey, out description))
            {
                return ModText.Get(ModStrings.Screens.NoBattlefieldDescription);
            }

            List<string> lines = Lines(description, terrain, onUnknownRegion);
            return lines.Count == 0
                ? ModText.Get(ModStrings.Screens.NoBattlefieldDescription)
                : ModText.JoinList(ModStrings.Common.PhraseSeparator, lines);
        }

        /// <summary>What the same gesture says in combat: the terrain alone, unlabelled.</summary>
        public static string SpokenTerrain(string layoutKey, BattlefieldTerrain terrain, Action<string> onUnknownRegion)
        {
            BattlefieldDescription description;
            return BattlefieldDescriptions.TryGet(layoutKey, out description)
                && !string.IsNullOrWhiteSpace(description.Terrain)
                ? Expand(description.Terrain, terrain, onUnknownRegion)
                : ModText.Get(ModStrings.Screens.NoBattlefieldDescription);
        }

        /// <summary>What the describe-battlefield gesture says in a fight: the terrain, and then
        /// what lies around the board, which is read from the battle itself rather than authored.
        /// </summary>
        public static string SpokenCombat(
            string layoutKey,
            BattlefieldTerrain terrain,
            BattlefieldSurroundings surroundings,
            Action<string> onUnknownRegion)
        {
            return ModText.JoinList(
                ModStrings.Common.PhraseSeparator,
                new List<string>
                {
                    SpokenTerrain(layoutKey, terrain, onUnknownRegion),
                    Surroundings(surroundings)
                });
        }

        /// <summary>
        /// WHAT LIES AROUND THE BOARD, in one sentence. The game sampled three adventure tiles when
        /// it started the battle and drew the scenery from them, so this says what a sighted player
        /// sees past the edges: one clause per kind of ground, naming the directions it lies in,
        /// and no directions at all where it lies in all three - "The battlefield is surrounded by
        /// open land." Empty where the battle carries no surroundings.
        /// </summary>
        public static string Surroundings(BattlefieldSurroundings surroundings)
        {
            if (surroundings == null)
            {
                return string.Empty;
            }

            List<ModString> words = new List<ModString>(3);
            List<List<ModString>> directions = new List<List<ModString>>(3);
            AddSurrounding(words, directions, surroundings.West, ModStrings.Scanner.West);
            AddSurrounding(words, directions, surroundings.North, ModStrings.Scanner.North);
            AddSurrounding(words, directions, surroundings.East, ModStrings.Scanner.East);

            List<string> clauses = new List<string>(words.Count);
            for (int i = 0; i < words.Count; i++)
            {
                string word = ModText.Get(words[i]);
                clauses.Add(directions[i].Count == 3
                    ? word
                    : ModText.Get(ModStrings.Battlefield.SurroundingDirection, word, Names(directions[i])));
            }

            return ModText.Get(ModStrings.Battlefield.Surroundings, ModText.JoinListWithCommas(clauses));
        }

        /// <summary>One sampled tile onto the clause it belongs to: the same ground in two
        /// directions is one clause naming both.</summary>
        private static void AddSurrounding(
            List<ModString> words, List<List<ModString>> directions, AdventureTerrainKind kind, ModString direction)
        {
            ModString word = SurroundingWord(kind);
            for (int i = 0; i < words.Count; i++)
            {
                if (string.Equals(words[i].Key, word.Key, StringComparison.Ordinal))
                {
                    directions[i].Add(direction);
                    return;
                }
            }

            words.Add(word);
            directions.Add(new List<ModString> { direction });
        }

        private static string Names(List<ModString> directions)
        {
            List<string> names = new List<string>(directions.Count);
            for (int i = 0; i < directions.Count; i++)
            {
                names.Add(ModText.Get(directions[i]));
            }

            return ModText.JoinList(names);
        }

        /// <summary>What one sampled tile is called from the board: the few kinds of ground worth
        /// hearing about, and open land for everything else - a player told "open land to the east"
        /// knows there is nothing there.</summary>
        private static ModString SurroundingWord(AdventureTerrainKind kind)
        {
            switch (kind)
            {
                case AdventureTerrainKind.AridTrees:
                case AdventureTerrainKind.TemperateTrees:
                    return ModStrings.Battlefield.SurroundingForest;
                case AdventureTerrainKind.Mountain:
                    return ModStrings.Battlefield.SurroundingMountains;
                case AdventureTerrainKind.Water:
                case AdventureTerrainKind.ShallowWater:
                case AdventureTerrainKind.DeepWater:
                case AdventureTerrainKind.WaterEdge:
                    return ModStrings.Battlefield.SurroundingWater;
                case AdventureTerrainKind.Wall:
                    return ModStrings.Battlefield.SurroundingWalls;
                case AdventureTerrainKind.Farmland:
                    return ModStrings.Battlefield.SurroundingFarmland;
                default:
                    return ModStrings.Battlefield.SurroundingOpenLand;
            }
        }

        /// <summary>
        /// AN AUTHORED DESCRIPTION AGAINST THE GROUND IT DESCRIBES. A description never names a
        /// feature in words: it points at one with the coordinates of any of its cells,
        /// <c>{4,3}</c>, and the words come from the live reading of the board - so the same
        /// sentence says "a diagonal ridge (height 1)" on the placement page and "a wall of bushes"
        /// in the fight that painted bushes onto it, and a shape rule that changes changes both.
        ///
        /// A placeholder pointing at no group at all is an authoring mistake: it expands to nothing
        /// and <paramref name="onUnknownRegion"/> is told, which is how it gets said once rather
        /// than once a frame.
        /// </summary>
        public static string Expand(string text, BattlefieldTerrain terrain, Action<string> onUnknownRegion)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
            {
                return text;
            }

            return RegionPlaceholder.Replace(text, match =>
            {
                int x;
                int y;
                if (terrain == null
                    || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    || !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                {
                    Unknown(onUnknownRegion, match.Value);
                    return string.Empty;
                }

                BattlefieldRegion region = terrain.RegionAt(new Vector2Int(x, y));
                if (region == null)
                {
                    Unknown(onUnknownRegion, match.Value);
                    return string.Empty;
                }

                return RegionDescription(region);
            });
        }

        /// <summary>
        /// WHAT ONE GROUP OF GROUND IS CALLED INSIDE A SENTENCE, which is not what the scanner calls
        /// it: a description is prose, so the height goes in parentheses, the count is left out, and
        /// impassability is never spoken - the player is being told the shape of the board, and "a
        /// wall of bushes" already says nothing walks through it.
        /// </summary>
        public static string RegionDescription(BattlefieldRegion region)
        {
            if (region == null)
            {
                return string.Empty;
            }

            switch (region.Kind)
            {
                case BattlefieldRegionKind.Elevated:
                    return ModText.Get(ElevatedDescription(region.Shape), region.Height);
                case BattlefieldRegionKind.Cliff:
                    return ModText.Get(ModStrings.Battlefield.DescriptionCliffs);
                case BattlefieldRegionKind.Unreachable:
                    return ModText.Get(ModStrings.Battlefield.DescriptionUnreachableGround);
                case BattlefieldRegionKind.ChokePoint:
                    return ModText.Get(ModStrings.Battlefield.DescriptionChokePoint);
                case BattlefieldRegionKind.Impassable:
                    return ImpassableDescription(region);
                case BattlefieldRegionKind.Wall:
                    return ModText.Get(ModStrings.Battlefield.DescriptionWall, region.Height);
                case BattlefieldRegionKind.Tower:
                    return ModText.Get(ModStrings.Battlefield.DescriptionTower, region.Height);
                case BattlefieldRegionKind.Stairs:
                    return ModText.Get(ModStrings.Battlefield.DescriptionStairs, region.Height);
                default:
                    return string.Empty;
            }
        }

        private static ModString ElevatedDescription(BattlefieldRegionShape shape)
        {
            switch (shape)
            {
                case BattlefieldRegionShape.SingleCell:
                    return ModStrings.Battlefield.DescriptionSingleCell;
                case BattlefieldRegionShape.Ridge:
                    return ModStrings.Battlefield.DescriptionRidge;
                case BattlefieldRegionShape.VerticalRidge:
                    return ModStrings.Battlefield.DescriptionRidgeVertical;
                case BattlefieldRegionShape.DiagonalRidge:
                    return ModStrings.Battlefield.DescriptionRidgeDiagonal;
                default:
                    return ModStrings.Battlefield.DescriptionPatch;
            }
        }

        /// <summary>Blocked ground in a sentence: what is standing on it where the fight named it,
        /// and the plain words where nothing did.</summary>
        private static string ImpassableDescription(BattlefieldRegion region)
        {
            string words = ObstacleWords(region.Obstacles, region.Count);
            if (string.IsNullOrEmpty(words))
            {
                if (region.IsRidge)
                {
                    return ModText.Get(ModStrings.Battlefield.DescriptionImpassableWall);
                }

                return ModText.Get(region.Count == 1
                    ? ModStrings.Battlefield.DescriptionImpassableCell
                    : ModStrings.Battlefield.DescriptionImpassableCells);
            }

            return region.IsRidge && !IsUncountable(region.Obstacles)
                ? ModText.Get(ModStrings.Battlefield.DescriptionObstacleWall, words)
                : words;
        }

        /// <summary>Whether a group is nothing but stuff there is no counting and no building
        /// with: water and fire. "A wall of water" and "wall of 5 water" are not English, so a
        /// group of them is named by what it is whatever shape it lies in - a moat is water, a
        /// burning line is fire. A group that mixes them with something countable is a list of
        /// what is on it, walls and all.</summary>
        private static bool IsUncountable(List<BattlefieldObstacle> obstacles)
        {
            if (obstacles == null || obstacles.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < obstacles.Count; i++)
            {
                if (obstacles[i].Kind != BattlefieldObstacleKind.Water
                    && obstacles[i].Kind != BattlefieldObstacleKind.Fire)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Unknown(Action<string> onUnknownRegion, string placeholder)
        {
            if (onUnknownRegion != null)
            {
                onUnknownRegion(placeholder);
            }
        }

        /// <summary>
        /// WHAT ONE GROUP OF GROUND IS CALLED, the words the scanner's Terrain category speaks and
        /// the words the authored descriptions are written in. The analysis
        /// (<see cref="BattlefieldTerrain"/>) knows kinds, counts and heights and no language at
        /// all; this is the one place they become a phrase, so the dump's JSON and the spoken line
        /// cannot drift apart.
        /// </summary>
        public static string Region(BattlefieldRegion region)
        {
            if (region == null)
            {
                return string.Empty;
            }

            switch (region.Kind)
            {
                case BattlefieldRegionKind.Elevated:
                    if (region.Shape == BattlefieldRegionShape.SingleCell)
                    {
                        return ModText.Get(ModStrings.Scanner.TerrainSingleCell, region.Height);
                    }

                    return ModText.Plural(
                        ElevatedShape(region.Shape), region.Count, region.Count, region.Height);
                case BattlefieldRegionKind.Cliff:
                    return region.Count == 1
                        ? ModText.Get(ModStrings.Scanner.TerrainCliff)
                        : ModText.Plural(ModStrings.Scanner.TerrainCliffCells, region.Count, region.Count);
                case BattlefieldRegionKind.Unreachable:
                    return region.Count == 1
                        ? ModText.Get(ModStrings.Scanner.TerrainUnreachable)
                        : ModText.Plural(ModStrings.Scanner.TerrainUnreachableCells, region.Count, region.Count);
                case BattlefieldRegionKind.Impassable:
                    return Impassable(region);
                case BattlefieldRegionKind.ChokePoint:
                    return region.Count == 1
                        ? ModText.Get(ModStrings.Scanner.TerrainChokePoint)
                        : ModText.Plural(ModStrings.Scanner.TerrainChokePointCells, region.Count, region.Count);
                case BattlefieldRegionKind.Wall:
                    return ModText.Plural(
                        ModStrings.Scanner.TerrainSiegeWall, region.Count, region.Count, region.Height);
                case BattlefieldRegionKind.Tower:
                    return ModText.Get(ModStrings.Spatial.TowerHeight, region.Height);
                case BattlefieldRegionKind.Stairs:
                    return ModText.Get(ModStrings.Spatial.StairsHeight, region.Height);
                default:
                    return string.Empty;
            }
        }

        /// <summary>A group of blocked cells, named by what is standing on them where the fight has
        /// given them a name and by their number alone where it has not (the placement page, whose
        /// preview draws every blocked cell the same way).</summary>
        private static string Impassable(BattlefieldRegion region)
        {
            // Empty where the fight named nothing, and where it named only obstacles nobody has a
            // word for: a lone standalone prop is impassable ground and nothing more.
            string obstacles = ObstacleWords(region.Obstacles, region.Count);
            if (string.IsNullOrEmpty(obstacles))
            {
                if (region.Count == 1)
                {
                    return ModText.Get(ModStrings.Spatial.Impassable);
                }

                return ModText.Plural(
                    region.IsRidge
                        ? ModStrings.Scanner.TerrainImpassableWall
                        : ModStrings.Scanner.TerrainImpassableCells,
                    region.Count,
                    region.Count);
            }

            if (region.Count == 1)
            {
                return ModText.Get(ModStrings.Spatial.ImpassableObstacle, CellObstacle(region.Obstacles[0]));
            }

            return ModText.Plural(
                region.IsRidge && !IsUncountable(region.Obstacles)
                    ? ModStrings.Scanner.TerrainObstacleWall
                    : ModStrings.Scanner.TerrainObstacleCells,
                region.Count,
                region.Count,
                obstacles);
        }

        /// <summary>What the cursor says about a blocked cell in a fight: what stands there and that
        /// nothing can enter it. Empty where the obstacle has no name, which leaves the tile
        /// formatter's plain "impassable" to say it.</summary>
        public static string CellImpassable(BattlefieldObstacle obstacle)
        {
            string word = CellObstacle(obstacle);
            return string.IsNullOrEmpty(word)
                ? string.Empty
                : ModText.Get(ModStrings.Spatial.ImpassableObstacle, word);
        }

        /// <summary>What a whole cell of one obstacle is called: the plural where a cell holds many
        /// of them - boulders, bushes - and the singular where it holds one, a statue or a torch.
        /// </summary>
        public static string CellObstacle(BattlefieldObstacle obstacle)
        {
            if (obstacle == null)
            {
                return string.Empty;
            }

            bool one = obstacle.Kind == BattlefieldObstacleKind.Manufactured
                || obstacle.Kind == BattlefieldObstacleKind.Light;
            return Word(obstacle, one ? 1 : 2);
        }

        /// <summary>What a group of blocked cells is standing under, most of it first and joined as
        /// a list. <paramref name="count"/> is the group's size, which is the number spoken beside
        /// the words and so the number they have to agree with.</summary>
        public static string ObstacleWords(List<BattlefieldObstacle> obstacles, int count)
        {
            if (obstacles == null || obstacles.Count == 0)
            {
                return string.Empty;
            }

            List<string> words = new List<string>(obstacles.Count);
            for (int i = 0; i < obstacles.Count; i++)
            {
                string word = Word(obstacles[i], count);
                if (!string.IsNullOrEmpty(word))
                {
                    words.Add(word);
                }
            }

            return ModText.JoinList(words);
        }

        /// <summary>One obstacle in the form <paramref name="count"/> asks for, in the words of the
        /// theme the board is painted with. Empty for an obstacle nobody named - an unnamed
        /// decoration byte, a standalone prop - which leaves the plain wording to say it.</summary>
        private static string Word(BattlefieldObstacle obstacle, int count)
        {
            ModPluralString words;
            return obstacle != null && TryWords(obstacle.Kind, obstacle.Theme, out words)
                ? ModText.Plural(words, count)
                : string.Empty;
        }

        /// <summary>The owner's word for one family of prop in one theme. Theme 7 is a second
        /// Arleon, and a theme byte nobody knows reads as Arleon too rather than falling silent.
        /// </summary>
        private static bool TryWords(BattlefieldObstacleKind kind, int theme, out ModPluralString words)
        {
            switch (kind)
            {
                case BattlefieldObstacleKind.Rock:
                    words = ModStrings.Battlefield.ObstacleRock;
                    return true;
                case BattlefieldObstacleKind.Fire:
                    words = ModStrings.Battlefield.ObstacleFire;
                    return true;
                case BattlefieldObstacleKind.Water:
                    words = ModStrings.Battlefield.ObstacleWater;
                    return true;
                case BattlefieldObstacleKind.Growth:
                    words = Growth(theme);
                    return true;
                case BattlefieldObstacleKind.Manufactured:
                    words = Manufactured(theme);
                    return true;
                case BattlefieldObstacleKind.Light:
                    words = Light(theme);
                    return true;
                default:
                    words = ModStrings.Battlefield.ObstacleRock;
                    return false;
            }
        }

        private static ModPluralString Growth(int theme)
        {
            switch (theme)
            {
                case 1:
                    return ModStrings.Battlefield.ObstacleGrowthLoth;
                case 2:
                    return ModStrings.Battlefield.ObstacleGrowthBarya;
                case 3:
                    return ModStrings.Battlefield.ObstacleGrowthRana;
                case 4:
                    return ModStrings.Battlefield.ObstacleGrowthVanir;
                case 5:
                    return ModStrings.Battlefield.ObstacleGrowthRoots;
                case 6:
                    return ModStrings.Battlefield.ObstacleGrowthYulan;
                default:
                    return ModStrings.Battlefield.ObstacleGrowthArleon;
            }
        }

        private static ModPluralString Manufactured(int theme)
        {
            switch (theme)
            {
                case 1:
                    return ModStrings.Battlefield.ObstacleManufacturedLoth;
                case 2:
                    return ModStrings.Battlefield.ObstacleManufacturedBarya;
                case 3:
                    return ModStrings.Battlefield.ObstacleManufacturedRana;
                case 4:
                    return ModStrings.Battlefield.ObstacleManufacturedVanir;
                case 5:
                    return ModStrings.Battlefield.ObstacleManufacturedRoots;
                case 6:
                    return ModStrings.Battlefield.ObstacleManufacturedYulan;
                default:
                    return ModStrings.Battlefield.ObstacleManufacturedArleon;
            }
        }

        private static ModPluralString Light(int theme)
        {
            switch (theme)
            {
                case 1:
                    return ModStrings.Battlefield.ObstacleLightLoth;
                case 2:
                    return ModStrings.Battlefield.ObstacleLightBarya;
                case 3:
                    return ModStrings.Battlefield.ObstacleLightRana;
                case 4:
                    return ModStrings.Battlefield.ObstacleLightVanir;
                case 5:
                    return ModStrings.Battlefield.ObstacleLightRoots;
                case 6:
                    return ModStrings.Battlefield.ObstacleLightYulan;
                default:
                    return ModStrings.Battlefield.ObstacleLightArleon;
            }
        }

        /// <summary>How a group of raised ground more than one cell across is named: a ridge by the
        /// way it runs, and a patch where it is not long enough to be one.</summary>
        private static ModPluralString ElevatedShape(BattlefieldRegionShape shape)
        {
            switch (shape)
            {
                case BattlefieldRegionShape.Ridge:
                    return ModStrings.Scanner.TerrainRidge;
                case BattlefieldRegionShape.VerticalRidge:
                    return ModStrings.Scanner.TerrainRidgeVertical;
                case BattlefieldRegionShape.DiagonalRidge:
                    return ModStrings.Scanner.TerrainRidgeDiagonal;
                default:
                    return ModStrings.Scanner.TerrainPatch;
            }
        }

        /// <summary>What the cursor says about the ground of one cell, where the kind alone answers
        /// it: a cliff, unreachable ground, a wall, a tower, stairs. Ordinary raised ground and flat
        /// ground are not here - the tile formatters say those from the elevation itself. The one
        /// answer with no height in it is unreachable ground, which no troop will ever be standing
        /// on, and which is the only one of these a cell at height 0 can be.</summary>
        public static string CellGround(BattlefieldCellKind kind, int elevation)
        {
            switch (kind)
            {
                case BattlefieldCellKind.Cliff:
                    return ModText.Get(ModStrings.Spatial.CliffHeight, elevation);
                case BattlefieldCellKind.Unreachable:
                    return ModText.Get(ModStrings.Spatial.Unreachable);
                case BattlefieldCellKind.Wall:
                    return ModText.Get(ModStrings.Spatial.WallHeight, elevation);
                case BattlefieldCellKind.Tower:
                    return ModText.Get(ModStrings.Spatial.TowerHeight, elevation);
                case BattlefieldCellKind.Stairs:
                    return ModText.Get(ModStrings.Spatial.StairsHeight, elevation);
                default:
                    return string.Empty;
            }
        }

        private static void AddLine(
            List<string> lines,
            ModString label,
            string text,
            BattlefieldTerrain terrain,
            Action<string> onUnknownRegion)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(ModText.Get(label, Expand(text, terrain, onUnknownRegion)));
            }
        }
    }
}
