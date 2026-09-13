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
                        region.Shape == BattlefieldRegionShape.Ridge
                            ? ModStrings.Scanner.TerrainRidge
                            : ModStrings.Scanner.TerrainPatch,
                        region.Count,
                        region.Count,
                        region.Height);
                case BattlefieldRegionKind.Cliff:
                    return region.Count == 1
                        ? ModText.Get(ModStrings.Scanner.TerrainCliff)
                        : ModText.Plural(ModStrings.Scanner.TerrainCliffCells, region.Count, region.Count);
                case BattlefieldRegionKind.Impassable:
                    if (region.Count == 1)
                    {
                        return ModText.Get(ModStrings.Spatial.Impassable);
                    }

                    return ModText.Plural(
                        region.Shape == BattlefieldRegionShape.Ridge
                            ? ModStrings.Scanner.TerrainImpassableWall
                            : ModStrings.Scanner.TerrainImpassableCells,
                        region.Count,
                        region.Count);
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
