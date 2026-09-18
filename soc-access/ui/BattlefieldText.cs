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
        /// it: a description is prose, so the height goes in parentheses, the count is left out,
        /// obstacles carry their article, and impassability is never spoken - the player is being
        /// told the shape of the board, and "a wall of bushes" already says nothing walks through
        /// it.
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
            // "a wall of {0}" carries the article itself, so what goes inside it is the bare noun -
            // "un mur de rochers", not "un mur de des rochers". Standing on its own the group is the
            // whole noun phrase and takes the description form, which carries the article a language
            // needs: "bloqué par des rochers". English spells the two alike and hears no difference.
            bool wall = region.IsRidge && !NeverAWall(region.Obstacle);
            string words = ObstacleWord(region.Obstacle, region.Count, wall);
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

            return wall
                ? ModText.Get(ModStrings.Battlefield.DescriptionObstacleWall, words)
                : words;
        }

        /// <summary>Whether a group is made of stuff no wall is built out of: water and fire, which
        /// there is no counting and no building with - "a wall of water" and "wall of 5 water" are
        /// not English, so a moat is water and a burning line is fire whatever shape it lies in -
        /// and gateposts, which are the frame of a gate and not a wall of their own.</summary>
        private static bool NeverAWall(BattlefieldObstacle obstacle)
        {
            return obstacle != null
                && (obstacle.Kind == BattlefieldObstacleKind.Water
                    || obstacle.Kind == BattlefieldObstacleKind.Fire
                    || obstacle.Kind == BattlefieldObstacleKind.Gatepost);
        }

        private static void Unknown(Action<string> onUnknownRegion, string placeholder)
        {
            if (onUnknownRegion != null)
            {
                onUnknownRegion(placeholder);
            }
        }

        /// <summary>
        /// WHAT ONE GROUP OF GROUND IS CALLED, the words the scanner's Terrain category speaks: a
        /// label, so an obstacle is named by a bare noun rather than the noun phrase
        /// <see cref="RegionDescription"/> writes into a sentence. The analysis
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
            string obstacles = ObstacleWord(region.Obstacle, region.Count, true);
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
                return ModText.Get(ModStrings.Spatial.ImpassableObstacle, CellObstacle(region.Obstacle));
            }

            return ModText.Plural(
                region.IsRidge && !NeverAWall(region.Obstacle)
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

        /// <summary>What the cursor says about a cliff cell, worded the way a named obstacle is:
        /// "cliff, impassable", with no height. The game calls the cell walkable, and no walk,
        /// teleport, push or summon can put a troop on it, so how high it stands is no use.</summary>
        public static string CellCliff()
        {
            return ModText.Get(
                ModStrings.Spatial.ImpassableObstacle,
                ModText.Get(ModStrings.Scanner.TerrainCliff));
        }

        /// <summary>What a whole cell of one obstacle is called: the plural where a cell holds many
        /// of them - boulders, bushes - and the singular where it holds one, a statue, a torch or a
        /// gatepost. The cursor is a label rather than a sentence, so the noun is bare: "gatepost,
        /// impassable", not "a gatepost, impassable".</summary>
        public static string CellObstacle(BattlefieldObstacle obstacle)
        {
            if (obstacle == null)
            {
                return string.Empty;
            }

            bool one = obstacle.Kind == BattlefieldObstacleKind.Manufactured
                || obstacle.Kind == BattlefieldObstacleKind.Light
                || obstacle.Kind == BattlefieldObstacleKind.Gatepost;
            return ObstacleWord(obstacle, one ? 1 : 2, true);
        }

        /// <summary>What a group of blocked cells is standing under, in the words of the theme the
        /// board is painted with: one word, because a group is one family of prop and never a
        /// mixture. <paramref name="count"/> is the group's size, which is the number spoken beside
        /// the word and so the number it has to agree with; <paramref name="bare"/> asks for the
        /// bare noun the scanner speaks rather than the noun phrase a description reads. Empty for
        /// an obstacle nobody named - an unnamed decoration byte, a standalone prop - which leaves
        /// the plain wording to say it.</summary>
        public static string ObstacleWord(BattlefieldObstacle obstacle, int count, bool bare)
        {
            ModPluralString words;
            return obstacle != null && TryWords(obstacle.Kind, obstacle.Theme, bare, out words)
                ? ModText.Plural(words, count)
                : string.Empty;
        }

        /// <summary>The owner's word for one family of prop in one theme. Theme 7 is a second
        /// Arleon, and a theme byte nobody knows reads as Arleon too rather than falling silent.
        /// <paramref name="bare"/> picks the bare noun the cursor, the scanner and the inside of a
        /// wall speak over the noun phrase a description reads on its own; the words English writes
        /// without an article still have both, because French does not write them without one.
        /// </summary>
        private static bool TryWords(
            BattlefieldObstacleKind kind, int theme, bool bare, out ModPluralString words)
        {
            switch (kind)
            {
                case BattlefieldObstacleKind.Rock:
                    words = bare
                        ? ModStrings.Battlefield.ObstacleRockBare
                        : ModStrings.Battlefield.ObstacleRock;
                    return true;
                case BattlefieldObstacleKind.Fire:
                    words = bare
                        ? ModStrings.Battlefield.ObstacleFireBare
                        : ModStrings.Battlefield.ObstacleFire;
                    return true;
                case BattlefieldObstacleKind.Water:
                    words = bare
                        ? ModStrings.Battlefield.ObstacleWaterBare
                        : ModStrings.Battlefield.ObstacleWater;
                    return true;
                case BattlefieldObstacleKind.Growth:
                    words = Growth(theme, bare);
                    return true;
                case BattlefieldObstacleKind.Manufactured:
                    words = Manufactured(theme, bare);
                    return true;
                case BattlefieldObstacleKind.Light:
                    words = Light(theme, bare);
                    return true;
                case BattlefieldObstacleKind.Gatepost:
                    // Stonework, and the same stonework whatever theme the battle is painted in.
                    words = bare
                        ? ModStrings.Battlefield.ObstacleGatepostBare
                        : ModStrings.Battlefield.ObstacleGatepost;
                    return true;
                default:
                    words = ModStrings.Battlefield.ObstacleRock;
                    return false;
            }
        }

        private static ModPluralString Growth(int theme, bool bare)
        {
            switch (theme)
            {
                case 1:
                    return bare
                        ? ModStrings.Battlefield.ObstacleGrowthLothBare
                        : ModStrings.Battlefield.ObstacleGrowthLoth;
                case 2:
                    return bare
                        ? ModStrings.Battlefield.ObstacleGrowthBaryaBare
                        : ModStrings.Battlefield.ObstacleGrowthBarya;
                case 3:
                    return bare
                        ? ModStrings.Battlefield.ObstacleGrowthRanaBare
                        : ModStrings.Battlefield.ObstacleGrowthRana;
                case 4:
                    return bare
                        ? ModStrings.Battlefield.ObstacleGrowthVanirBare
                        : ModStrings.Battlefield.ObstacleGrowthVanir;
                case 5:
                    return bare
                        ? ModStrings.Battlefield.ObstacleGrowthRootsBare
                        : ModStrings.Battlefield.ObstacleGrowthRoots;
                case 6:
                    return bare
                        ? ModStrings.Battlefield.ObstacleGrowthYulanBare
                        : ModStrings.Battlefield.ObstacleGrowthYulan;
                default:
                    return bare
                        ? ModStrings.Battlefield.ObstacleGrowthArleonBare
                        : ModStrings.Battlefield.ObstacleGrowthArleon;
            }
        }

        private static ModPluralString Manufactured(int theme, bool bare)
        {
            switch (theme)
            {
                case 1:
                    return bare
                        ? ModStrings.Battlefield.ObstacleManufacturedLothBare
                        : ModStrings.Battlefield.ObstacleManufacturedLoth;
                case 2:
                    return bare
                        ? ModStrings.Battlefield.ObstacleManufacturedBaryaBare
                        : ModStrings.Battlefield.ObstacleManufacturedBarya;
                case 3:
                    return bare
                        ? ModStrings.Battlefield.ObstacleManufacturedRanaBare
                        : ModStrings.Battlefield.ObstacleManufacturedRana;
                case 4:
                    return bare
                        ? ModStrings.Battlefield.ObstacleManufacturedVanirBare
                        : ModStrings.Battlefield.ObstacleManufacturedVanir;
                case 5:
                    return bare
                        ? ModStrings.Battlefield.ObstacleManufacturedRootsBare
                        : ModStrings.Battlefield.ObstacleManufacturedRoots;
                case 6:
                    return bare
                        ? ModStrings.Battlefield.ObstacleManufacturedYulanBare
                        : ModStrings.Battlefield.ObstacleManufacturedYulan;
                default:
                    return bare
                        ? ModStrings.Battlefield.ObstacleManufacturedArleonBare
                        : ModStrings.Battlefield.ObstacleManufacturedArleon;
            }
        }

        private static ModPluralString Light(int theme, bool bare)
        {
            switch (theme)
            {
                case 1:
                    return bare
                        ? ModStrings.Battlefield.ObstacleLightLothBare
                        : ModStrings.Battlefield.ObstacleLightLoth;
                case 2:
                    return bare
                        ? ModStrings.Battlefield.ObstacleLightBaryaBare
                        : ModStrings.Battlefield.ObstacleLightBarya;
                case 3:
                    return bare
                        ? ModStrings.Battlefield.ObstacleLightRanaBare
                        : ModStrings.Battlefield.ObstacleLightRana;
                case 4:
                    return bare
                        ? ModStrings.Battlefield.ObstacleLightVanirBare
                        : ModStrings.Battlefield.ObstacleLightVanir;
                case 5:
                    return bare
                        ? ModStrings.Battlefield.ObstacleLightRootsBare
                        : ModStrings.Battlefield.ObstacleLightRoots;
                case 6:
                    return bare
                        ? ModStrings.Battlefield.ObstacleLightYulanBare
                        : ModStrings.Battlefield.ObstacleLightYulan;
                default:
                    return bare
                        ? ModStrings.Battlefield.ObstacleLightArleonBare
                        : ModStrings.Battlefield.ObstacleLightArleon;
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
        /// it: unreachable ground, a wall, a tower, stairs. Ordinary raised ground and flat ground
        /// are not here - the tile formatters say those from the elevation itself - and neither is
        /// a cliff, which is said as blocked ground (<see cref="CellCliff"/>). The one answer with
        /// no height in it is unreachable ground, which no troop will ever be standing on, and
        /// which is the only one of these a cell at height 0 can be.</summary>
        public static string CellGround(BattlefieldCellKind kind, int elevation)
        {
            switch (kind)
            {
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
