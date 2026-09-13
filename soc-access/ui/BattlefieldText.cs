using System.Collections.Generic;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Localization;

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
        /// <summary>The three lines of a description, one per field so the review buffer holds three
        /// and the node speaks them as one body. Empty where the layout has no description.</summary>
        public static List<string> Lines(BattlefieldDescription description)
        {
            List<string> lines = new List<string>(3);
            if (description == null)
            {
                return lines;
            }

            AddLine(lines, ModStrings.Screens.DescriptionTerrain, description.Terrain);
            AddLine(lines, ModStrings.Screens.DescriptionAttacker, description.Attacker);
            AddLine(lines, ModStrings.Screens.DescriptionDefender, description.Defender);
            return lines;
        }

        /// <summary>What the describe-battlefield gesture says on the placement board: the three
        /// labelled lines as one breath, or that nobody has described this layout.</summary>
        public static string Spoken(string layoutKey)
        {
            BattlefieldDescription description;
            if (!BattlefieldDescriptions.TryGet(layoutKey, out description))
            {
                return ModText.Get(ModStrings.Screens.NoBattlefieldDescription);
            }

            List<string> lines = Lines(description);
            return lines.Count == 0
                ? ModText.Get(ModStrings.Screens.NoBattlefieldDescription)
                : ModText.JoinList(ModStrings.Common.PhraseSeparator, lines);
        }

        /// <summary>What the same gesture says in combat: the terrain alone, unlabelled.</summary>
        public static string SpokenTerrain(string layoutKey)
        {
            BattlefieldDescription description;
            return BattlefieldDescriptions.TryGet(layoutKey, out description)
                && !string.IsNullOrWhiteSpace(description.Terrain)
                ? description.Terrain
                : ModText.Get(ModStrings.Screens.NoBattlefieldDescription);
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
                case BattlefieldRegionKind.Impassable:
                    return Impassable(region);
                case BattlefieldRegionKind.ChokePoint:
                    return region.Count == 1
                        ? ModText.Get(ModStrings.Scanner.TerrainChokePoint)
                        : ModText.Get(ModStrings.Scanner.TerrainChokePointPair);
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
            if (region.Obstacles.Count == 0)
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
                region.IsRidge
                    ? ModStrings.Scanner.TerrainObstacleWall
                    : ModStrings.Scanner.TerrainObstacleCells,
                region.Count,
                region.Count,
                ObstacleWords(region.Obstacles, region.Count));
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
        /// it: a cliff, a wall, a tower, stairs. Ordinary raised ground and flat ground are not
        /// here - the tile formatters say those from the elevation itself.</summary>
        public static string CellGround(BattlefieldCellKind kind, int elevation)
        {
            switch (kind)
            {
                case BattlefieldCellKind.Cliff:
                    return ModText.Get(ModStrings.Spatial.CliffHeight, elevation);
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

        private static void AddLine(List<string> lines, ModString label, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(ModText.Get(label, text));
            }
        }
    }
}
