using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Audio
{
    /// <summary>
    /// Pure mapping from a tile snapshot to the cue keys describing it, in play order.
    /// Relationship and acting-troop facts come from the caller's adapter so this stays testable.
    /// </summary>
    internal static class TileCueSelector
    {
        private static readonly TileCue[] NoCues = new TileCue[0];

        public static IReadOnlyList<TileCue> ForAdventureTile(AdventureMapTile tile)
        {
            if (tile == null)
            {
                return NoCues;
            }

            List<TileCue> cues = new List<TileCue>(2);
            if (!tile.IsExplored)
            {
                cues.Add(new TileCue(CueLibrary.TerrainUnexplored, 0f));
                return cues;
            }

            // Occupied tiles are flagged impassable by the game; the thud is reserved for
            // impassable terrain itself, so occupants fall through to their terrain family.
            string entity = ForAdventureEntity(tile);
            cues.Add(new TileCue(
                entity == null && IsAdventureTileImpassable(tile)
                    ? CueLibrary.TerrainImpassable
                    : ForTerrain(tile.Terrain),
                0f));

            if (entity != null)
            {
                cues.Add(new TileCue(entity, 0f));
            }

            return cues;
        }

        public static IReadOnlyList<TileCue> ForCombatTile(CombatTile tile, bool isEnemyTroop, bool isActingTroop)
        {
            if (tile == null)
            {
                return NoCues;
            }

            List<TileCue> cues = new List<TileCue>(3);
            string elevation = ElevationCueKey(tile.Elevation);
            if (tile.Troop != null || tile.TroopId >= 0)
            {
                AddElevatedGround(cues, elevation);
                cues.Add(new TileCue(isEnemyTroop ? CueLibrary.HexEnemy : CueLibrary.HexAlly, 0f, followsPrevious: elevation != null));
                if (isActingTroop)
                {
                    cues.Add(new TileCue(CueLibrary.HexActive, 0f));
                }

                return cues;
            }

            bool obstacle = tile.IsImpassable || tile.IsBlocked || tile.Entity != null || tile.EntityId >= 0;
            if (obstacle)
            {
                AddElevatedGround(cues, elevation);
                cues.Add(new TileCue(CueLibrary.HexObstacle, 0f, followsPrevious: elevation != null));
                return cues;
            }

            cues.Add(new TileCue(elevation ?? CueLibrary.HexEmpty, 0f));
            return cues;
        }

        public static IReadOnlyList<TileCue> ForTroopPlacementTile(TroopPlacementTile tile, bool isOwnTroop)
        {
            if (tile == null)
            {
                return NoCues;
            }

            List<TileCue> cues = new List<TileCue>(2);
            string elevation = ElevationCueKey(tile.Elevation);
            if (tile.Troop != null || tile.TroopId >= 0)
            {
                AddElevatedGround(cues, elevation);
                cues.Add(new TileCue(isOwnTroop ? CueLibrary.HexAlly : CueLibrary.HexEnemy, 0f, followsPrevious: elevation != null));
                return cues;
            }

            if (tile.IsImpassable || tile.EntityId >= 0)
            {
                AddElevatedGround(cues, elevation);
                cues.Add(new TileCue(CueLibrary.HexObstacle, 0f, followsPrevious: elevation != null));
                return cues;
            }

            cues.Add(new TileCue(elevation ?? CueLibrary.HexEmpty, 0f));
            return cues;
        }

        /// <summary>Cue for raised ground, or null at ground level; levels above 3 sound as 3.</summary>
        public static string ElevationCueKey(byte elevation)
        {
            switch (elevation)
            {
                case 0:
                    return null;
                case 1:
                    return CueLibrary.HexElevation1;
                case 2:
                    return CueLibrary.HexElevation2;
                default:
                    return CueLibrary.HexElevation3;
            }
        }

        /// <summary>Elevation must stay audible under occupant and obstacle cues, so the raised
        /// ground speaks as its own cue stacked beneath them.</summary>
        private static void AddElevatedGround(List<TileCue> cues, string elevationCueKey)
        {
            if (elevationCueKey != null)
            {
                cues.Add(new TileCue(elevationCueKey, 0f));
            }
        }

        public static string ForTerrain(AdventureTerrainKind terrain)
        {
            switch (terrain)
            {
                case AdventureTerrainKind.Road:
                case AdventureTerrainKind.DirtRoad:
                case AdventureTerrainKind.CobblestoneRoad:
                case AdventureTerrainKind.Bridge:
                    return CueLibrary.TerrainRoad;
                case AdventureTerrainKind.Sand:
                    return CueLibrary.TerrainSand;
                case AdventureTerrainKind.Water:
                case AdventureTerrainKind.ShallowWater:
                case AdventureTerrainKind.DeepWater:
                case AdventureTerrainKind.WaterEdge:
                    return CueLibrary.TerrainWater;
                case AdventureTerrainKind.AridTrees:
                case AdventureTerrainKind.TemperateTrees:
                    return CueLibrary.TerrainTrees;
                case AdventureTerrainKind.Mountain:
                case AdventureTerrainKind.Wall:
                    return CueLibrary.TerrainImpassable;
                default:
                    return CueLibrary.TerrainGround;
            }
        }

        private static bool IsAdventureTileImpassable(AdventureMapTile tile)
        {
            return tile.IsImpassable
                || tile.IsBlocked
                || tile.Terrain == AdventureTerrainKind.Mountain
                || tile.Terrain == AdventureTerrainKind.Wall;
        }

        private static string ForAdventureEntity(AdventureMapTile tile)
        {
            if (tile.Commander != null)
            {
                return ForRelationship(tile.Commander.Relationship, tile.Commander.IsOwnedByLocalTeam);
            }

            bool hasMapEntity = tile.MapEntity != null || tile.MapEntityId.HasValue;
            return hasMapEntity ? ForRelationship(tile.MapEntityRelationship, false) : null;
        }

        /// <summary>
        /// The tile carries the relationship already localized, so it is compared against the same
        /// ModStrings the adapter formatted it from rather than against raw English.
        /// </summary>
        private static string ForRelationship(string relationship, bool isOwnedByLocalTeam)
        {
            if (isOwnedByLocalTeam || Matches(relationship, ModStrings.Spatial.Friendly))
            {
                return CueLibrary.EntityFriendly;
            }

            return Matches(relationship, ModStrings.Spatial.Enemy) ? CueLibrary.EntityEnemy : CueLibrary.EntityNeutral;
        }

        private static bool Matches(string relationship, ModString expected)
        {
            return !string.IsNullOrEmpty(relationship)
                && string.Equals(relationship, ModText.Get(expected), StringComparison.OrdinalIgnoreCase);
        }
    }
}
