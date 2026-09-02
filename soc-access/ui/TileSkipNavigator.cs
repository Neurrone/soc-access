using System;
using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal static class TileSkipNavigator
    {
        public static TileSkipResult FindTarget<TSignature>(
            Vector2Int origin,
            Func<Vector2Int, Vector2Int> step,
            Func<Vector2Int, bool> isValid,
            Func<Vector2Int, TSignature> getSignature)
        {
            if (step == null || isValid == null || getSignature == null)
            {
                return new TileSkipResult(origin, 0);
            }

            TSignature source = getSignature(origin);
            Vector2Int candidate = step(origin);
            Vector2Int lastValid = origin;
            int matchingTiles = 0;

            while (isValid(candidate))
            {
                TSignature candidateSignature = getSignature(candidate);
                if (!EqualityComparer<TSignature>.Default.Equals(source, candidateSignature))
                {
                    return new TileSkipResult(candidate, matchingTiles);
                }

                matchingTiles++;
                lastValid = candidate;
                candidate = step(candidate);
            }

            if (matchingTiles == 0)
            {
                return new TileSkipResult(origin, 0);
            }

            return new TileSkipResult(lastValid, Math.Max(0, matchingTiles - 1));
        }
    }

    internal sealed class TileSkipResult
    {
        public TileSkipResult(Vector2Int target, int skippedCount)
        {
            Target = target;
            SkippedCount = Math.Max(0, skippedCount);
        }

        public Vector2Int Target { get; private set; }

        public int SkippedCount { get; private set; }
    }

    internal sealed class AdventureTileSkipSignature : IEquatable<AdventureTileSkipSignature>
    {
        private AdventureTileSkipSignature(
            AdventureTileVisibility visibility,
            AdventureTerrainKind terrain,
            bool isReachable,
            bool hasBookmark,
            string occupantKind,
            int occupantId,
            bool isRoadFork)
        {
            Visibility = visibility;
            Terrain = terrain;
            IsReachable = isReachable;
            HasBookmark = hasBookmark;
            OccupantKind = occupantKind ?? string.Empty;
            OccupantId = occupantId;
            IsRoadFork = isRoadFork;
        }

        public AdventureTileVisibility Visibility { get; private set; }

        public AdventureTerrainKind Terrain { get; private set; }

        public bool IsReachable { get; private set; }

        public bool HasBookmark { get; private set; }

        public string OccupantKind { get; private set; }

        public int OccupantId { get; private set; }

        /// <summary>
        /// Set only when road directions are being announced, so a skip that would otherwise
        /// run past a junction stops on it. A road painted two tiles wide makes a run of tiles
        /// that are all forks; they share this signature and are still skipped over together,
        /// which is right: the run is one junction, not several.
        /// </summary>
        public bool IsRoadFork { get; private set; }

        public static AdventureTileSkipSignature FromTile(AdventureMapTile tile, bool hasBookmark, bool stopsAtRoadForks)
        {
            if (tile == null)
            {
                return new AdventureTileSkipSignature(
                    AdventureTileVisibility.Unexplored,
                    AdventureTerrainKind.Unknown,
                    false,
                    hasBookmark,
                    string.Empty,
                    -1,
                    false);
            }

            string occupantKind = string.Empty;
            int occupantId = -1;
            if (tile.Commander != null)
            {
                occupantKind = "commander";
                occupantId = tile.Commander.Id;
            }
            else if (tile.MapEntityId.HasValue)
            {
                occupantKind = "map-entity";
                occupantId = tile.MapEntityId.Value;
            }
            else if (tile.MapEntity != null)
            {
                occupantKind = "map-entity";
                occupantId = tile.MapEntity.Id;
            }

            return new AdventureTileSkipSignature(
                GetVisibility(tile),
                tile.Terrain,
                tile.IsReachable,
                hasBookmark,
                occupantKind,
                occupantId,
                stopsAtRoadForks && tile.IsRoadFork);
        }

        public bool Equals(AdventureTileSkipSignature other)
        {
            return other != null
                && Visibility == other.Visibility
                && (Terrain == other.Terrain || HasSameOccupyingEntity(other))
                && IsReachable == other.IsReachable
                && HasBookmark == other.HasBookmark
                && OccupantKind == other.OccupantKind
                && OccupantId == other.OccupantId
                && IsRoadFork == other.IsRoadFork;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AdventureTileSkipSignature);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)Visibility;
                hash = hash * 31 + IsReachable.GetHashCode();
                hash = hash * 31 + HasBookmark.GetHashCode();
                hash = hash * 31 + OccupantKind.GetHashCode();
                hash = hash * 31 + OccupantId;
                hash = hash * 31 + IsRoadFork.GetHashCode();
                return hash;
            }
        }

        private static AdventureTileVisibility GetVisibility(AdventureMapTile tile)
        {
            if (tile == null || !tile.IsExplored)
            {
                return AdventureTileVisibility.Unexplored;
            }

            return tile.IsVisible ? AdventureTileVisibility.Visible : AdventureTileVisibility.ExploredFog;
        }

        private bool HasSameOccupyingEntity(AdventureTileSkipSignature other)
        {
            return other != null
                && OccupantKind == "map-entity"
                && other.OccupantKind == "map-entity"
                && OccupantId >= 0
                && OccupantId == other.OccupantId;
        }
    }

    internal enum AdventureTileVisibility
    {
        Unexplored,
        ExploredFog,
        Visible
    }

    internal sealed class CombatTileSkipSignature : IEquatable<CombatTileSkipSignature>
    {
        public CombatTileSkipSignature(
            byte elevation,
            bool isReachable,
            bool isImpassable,
            bool isBlocked,
            int troopId,
            int entityId,
            IReadOnlyList<int> dangerousEffectEntityIds,
            string decorativeFeature)
        {
            Elevation = elevation;
            IsReachable = isReachable;
            IsImpassable = isImpassable;
            IsBlocked = isBlocked;
            TroopId = troopId;
            EntityId = entityId;
            DangerousEffectEntityIds = CopyIds(dangerousEffectEntityIds);
            DecorativeFeature = decorativeFeature ?? string.Empty;
        }

        public byte Elevation { get; private set; }

        public bool IsReachable { get; private set; }

        public bool IsImpassable { get; private set; }

        public bool IsBlocked { get; private set; }

        public int TroopId { get; private set; }

        public int EntityId { get; private set; }

        public IReadOnlyList<int> DangerousEffectEntityIds { get; private set; }

        public string DecorativeFeature { get; private set; }

        public static CombatTileSkipSignature FromTile(CombatTile tile)
        {
            if (tile == null)
            {
                return new CombatTileSkipSignature(0, false, false, false, -1, -1, null, string.Empty);
            }

            int troopId = tile.TroopId;
            if (troopId < 0 && tile.Troop != null)
            {
                troopId = tile.Troop.Id;
            }

            int entityId = tile.EntityId;
            if (entityId < 0 && tile.Entity != null)
            {
                entityId = tile.Entity.Id;
            }

            return new CombatTileSkipSignature(
                tile.Elevation,
                tile.IsReachable,
                tile.IsImpassable,
                tile.IsBlocked,
                troopId,
                entityId,
                tile.DangerousMapEffectEntityIds,
                tile.DecorativeFeature);
        }

        public bool Equals(CombatTileSkipSignature other)
        {
            return other != null
                && Elevation == other.Elevation
                && IsReachable == other.IsReachable
                && IsImpassable == other.IsImpassable
                && IsBlocked == other.IsBlocked
                && TroopId == other.TroopId
                && EntityId == other.EntityId
                && DecorativeFeature == other.DecorativeFeature
                && IdsEqual(DangerousEffectEntityIds, other.DangerousEffectEntityIds);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CombatTileSkipSignature);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Elevation.GetHashCode();
                hash = hash * 31 + IsReachable.GetHashCode();
                hash = hash * 31 + IsImpassable.GetHashCode();
                hash = hash * 31 + IsBlocked.GetHashCode();
                hash = hash * 31 + TroopId;
                hash = hash * 31 + EntityId;
                hash = hash * 31 + DecorativeFeature.GetHashCode();
                for (int i = 0; i < DangerousEffectEntityIds.Count; i++)
                {
                    hash = hash * 31 + DangerousEffectEntityIds[i];
                }

                return hash;
            }
        }

        private static IReadOnlyList<int> CopyIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return new int[0];
            }

            List<int> copy = new List<int>(ids);
            copy.Sort();
            return copy.ToArray();
        }

        private static bool IdsEqual(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class TroopPlacementTileSkipSignature : IEquatable<TroopPlacementTileSkipSignature>
    {
        private TroopPlacementTileSkipSignature(
            byte elevation,
            bool isImpassable,
            BattleSide? spawnSide,
            BattleSide? troopSide,
            int troopId,
            int entityId,
            string entityLabel)
        {
            Elevation = elevation;
            IsImpassable = isImpassable;
            SpawnSide = spawnSide;
            TroopSide = troopSide;
            TroopId = troopId;
            EntityId = entityId;
            EntityLabel = entityLabel ?? string.Empty;
        }

        public byte Elevation { get; private set; }

        public bool IsImpassable { get; private set; }

        public BattleSide? SpawnSide { get; private set; }

        public BattleSide? TroopSide { get; private set; }

        public int TroopId { get; private set; }

        public int EntityId { get; private set; }

        public string EntityLabel { get; private set; }

        public static TroopPlacementTileSkipSignature FromTile(TroopPlacementTile tile)
        {
            if (tile == null)
            {
                return new TroopPlacementTileSkipSignature(0, false, null, null, -1, -1, string.Empty);
            }

            return new TroopPlacementTileSkipSignature(
                tile.Elevation,
                tile.IsImpassable,
                tile.SpawnSide,
                tile.TroopSide,
                tile.TroopId,
                tile.EntityId,
                tile.EntityLabel);
        }

        public bool Equals(TroopPlacementTileSkipSignature other)
        {
            return other != null
                && Elevation == other.Elevation
                && IsImpassable == other.IsImpassable
                && SpawnSide == other.SpawnSide
                && TroopSide == other.TroopSide
                && TroopId == other.TroopId
                && EntityId == other.EntityId
                && EntityLabel == other.EntityLabel;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TroopPlacementTileSkipSignature);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Elevation.GetHashCode();
                hash = hash * 31 + IsImpassable.GetHashCode();
                hash = hash * 31 + (SpawnSide.HasValue ? (int)SpawnSide.Value + 1 : 0);
                hash = hash * 31 + (TroopSide.HasValue ? (int)TroopSide.Value + 1 : 0);
                hash = hash * 31 + TroopId;
                hash = hash * 31 + EntityId;
                hash = hash * 31 + EntityLabel.GetHashCode();
                return hash;
            }
        }
    }
}
