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

            // A categorised entity speaks for the whole tile: the gesture is the acknowledgment,
            // so the terrain underneath stays silent rather than doubling every step.
            IReadOnlyList<TileCue> entity = ForAdventureEntity(tile);
            if (entity.Count > 0)
            {
                return entity;
            }

            // Occupied tiles are flagged impassable by the game; the thud is reserved for
            // impassable terrain itself, so occupants fall through to their terrain family.
            // Occupancy drives that, not the overlay, because neutral occupants play no cue.
            cues.Add(new TileCue(
                !HasOccupant(tile) && IsAdventureTileImpassable(tile)
                    ? CueLibrary.TerrainImpassable
                    : ForTerrain(tile.Terrain),
                0f));

            string affiliation = AffiliationCueKey(tile);
            if (affiliation != null)
            {
                cues.Add(new TileCue(affiliation, 0f));
            }

            return cues;
        }

        /// <summary>
        /// The gesture naming what stands on a tile: the category voice, then the affiliation
        /// marker serialized behind it. Empty when the tile holds nothing the sweep names.
        /// The sonar sweep plays exactly this, so a remote ping and a cursor step sound alike.
        /// </summary>
        public static IReadOnlyList<TileCue> ForAdventureEntity(AdventureMapTile tile)
        {
            return ForEntityCategory(tile != null ? tile.EntityCategory : AdventureEntityCategory.None, tile);
        }

        /// <summary>
        /// The same gesture for a category the caller already knows, such as the one the adapter
        /// stamped on a scanner result; the tile only supplies the affiliation marker.
        /// </summary>
        public static IReadOnlyList<TileCue> ForEntityCategory(AdventureEntityCategory category, AdventureMapTile tile)
        {
            string categoryCue = CategoryCueKey(category);
            if (categoryCue == null)
            {
                return NoCues;
            }

            List<TileCue> cues = new List<TileCue>(2);
            cues.Add(new TileCue(categoryCue, 0f));

            string affiliation = AffiliationCueKey(tile);
            if (affiliation != null)
            {
                cues.Add(new TileCue(affiliation, 0f, followsPrevious: true));
            }

            return cues;
        }

        /// <summary>The one mapping from the adapter's entity fact to a category voice.</summary>
        public static string CategoryCueKey(AdventureEntityCategory category)
        {
            switch (category)
            {
                case AdventureEntityCategory.Wielder:
                    return CueLibrary.SweepWielder;
                case AdventureEntityCategory.Settlement:
                    return CueLibrary.SweepSettlement;
                case AdventureEntityCategory.ResourceDeposit:
                    return CueLibrary.SweepResource;
                case AdventureEntityCategory.Pickup:
                    return CueLibrary.SweepPickup;
                default:
                    return null;
            }
        }

        public static IReadOnlyList<TileCue> ForCombatTile(CombatTile tile, bool isEnemyTroop, bool isActingTroop, bool isThreatened)
        {
            if (tile == null)
            {
                return NoCues;
            }

            List<TileCue> cues = new List<TileCue>(4);
            string elevation = ElevationCueKey(tile.Elevation);
            if (tile.Troop != null || tile.TroopId >= 0)
            {
                // Occupied tiles never warn, matching the speech formatter.
                AddElevatedGround(cues, elevation);
                cues.Add(new TileCue(isEnemyTroop ? CueLibrary.EntityEnemy : CueLibrary.EntityFriendly, 0f, followsPrevious: elevation != null));
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
                cues.Add(new TileCue(CueLibrary.TerrainImpassable, 0f, followsPrevious: elevation != null));
            }
            else
            {
                cues.Add(new TileCue(elevation ?? CueLibrary.HexEmpty, 0f));
            }

            return isThreatened ? WithDangerWarning(cues) : cues;
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
                cues.Add(new TileCue(isOwnTroop ? CueLibrary.EntityFriendly : CueLibrary.EntityEnemy, 0f, followsPrevious: elevation != null));
                return cues;
            }

            if (tile.IsImpassable || tile.EntityId >= 0)
            {
                AddElevatedGround(cues, elevation);
                cues.Add(new TileCue(CueLibrary.TerrainImpassable, 0f, followsPrevious: elevation != null));
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

        /// <summary>The warning leads and the rest of the tile serializes behind it, so the danger
        /// is heard first without losing what is on the hex.</summary>
        private static List<TileCue> WithDangerWarning(List<TileCue> cues)
        {
            cues.Insert(0, new TileCue(CueLibrary.HexDanger, 0f));
            if (cues.Count > 1)
            {
                cues[1] = new TileCue(cues[1].Key, cues[1].Semitones, followsPrevious: true);
            }

            return cues;
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

        private static bool HasOccupant(AdventureMapTile tile)
        {
            return tile.Commander != null || tile.MapEntity != null || tile.MapEntityId.HasValue;
        }

        /// <summary>The entity_* cue for whatever occupies the tile; null when nothing does or
        /// when the occupant is neutral.</summary>
        public static string AffiliationCueKey(AdventureMapTile tile)
        {
            if (tile == null)
            {
                return null;
            }

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

            // Most map objects are neutral, so only ally and enemy are marked; silence keeps
            // affiliation audible where it matters and speech still names the entity.
            return Matches(relationship, ModStrings.Spatial.Enemy) ? CueLibrary.EntityEnemy : null;
        }

        private static bool Matches(string relationship, ModString expected)
        {
            return !string.IsNullOrEmpty(relationship)
                && string.Equals(relationship, ModText.Get(expected), StringComparison.OrdinalIgnoreCase);
        }
    }
}
