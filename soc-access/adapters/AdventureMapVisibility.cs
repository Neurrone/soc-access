using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal static class AdventureMapVisibility
    {
        /// <summary>
        /// Finds a representative tile where this map entity can be identified from remembered map knowledge.
        /// Use this for focused-tile identity, scanner entries, and revealed tracking; fog 128 and fog 255
        /// both count as known, while fog 0 does not.
        /// </summary>
        public static bool TryGetKnownMapEntityIdentityTile(
            IClientAdventureFacade facade,
            IFogManager fogManager,
            IMapEntity entity,
            out Vector2Int tile)
        {
            tile = default(Vector2Int);
            if (!CanExposeMapEntityIdentity(entity))
            {
                return false;
            }

            List<Vector2Int> footprint = GetMapEntityFootprint(entity);
            if (Contains(footprint, entity.Position)
                && IsKnownMapEntityIdentityTile(facade, fogManager, entity, entity.Position))
            {
                tile = entity.Position;
                return true;
            }

            for (int i = 0; i < footprint.Count; i++)
            {
                if (IsKnownMapEntityIdentityTile(facade, fogManager, entity, footprint[i]))
                {
                    tile = footprint[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the specific tile identifies this map entity and is known to the player.
        /// Use this when reading the currently focused tile; it deliberately treats explored fog 128
        /// as known because the game can still render remembered entities there.
        /// </summary>
        public static bool IsKnownMapEntityIdentityTile(
            IClientAdventureFacade facade,
            IFogManager fogManager,
            IMapEntity entity,
            Vector2Int tile)
        {
            if (!CanExposeMapEntityIdentity(entity))
            {
                return false;
            }

            return IsTileKnown(fogManager, tile)
                && IsMapEntityIdentityTile(facade, entity, tile);
        }

        /// <summary>
        /// Returns whether any tile occupied by this entity is actively visible in current team vision.
        /// Use this to decide whether to expose tooltip-derived details. This is intentionally broader
        /// than the game's mouse tooltip rule, which requires the hovered tile itself to be fog 255.
        /// </summary>
        public static bool HasAnyActivelyVisibleMapEntityTile(IFogManager fogManager, IMapEntity entity)
        {
            if (!CanExposeMapEntityIdentity(entity))
            {
                return false;
            }

            List<Vector2Int> footprint = GetMapEntityFootprint(entity);
            for (int i = 0; i < footprint.Count; i++)
            {
                if (IsTileActivelyVisible(fogManager, footprint[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a representative actively visible identity tile for native tooltip lookup.
        /// Use this after HasAnyActivelyVisibleMapEntityTile when the focused tile may be remembered
        /// fog 128, because the game's private tooltip method still requires the tile it receives to be 255.
        /// </summary>
        public static bool TryGetActivelyVisibleMapEntityIdentityTile(
            IClientAdventureFacade facade,
            IFogManager fogManager,
            IMapEntity entity,
            out Vector2Int tile)
        {
            tile = default(Vector2Int);
            if (!CanExposeMapEntityIdentity(entity))
            {
                return false;
            }

            List<Vector2Int> footprint = GetMapEntityFootprint(entity);
            if (Contains(footprint, entity.Position)
                && IsTileActivelyVisible(fogManager, entity.Position)
                && IsMapEntityIdentityTile(facade, entity, entity.Position))
            {
                tile = entity.Position;
                return true;
            }

            for (int i = 0; i < footprint.Count; i++)
            {
                if (IsTileActivelyVisible(fogManager, footprint[i])
                    && IsMapEntityIdentityTile(facade, entity, footprint[i]))
                {
                    tile = footprint[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the entity itself is eligible to be named by accessibility.
        /// Use this as the base eligibility check before applying fog-specific rules; it does not
        /// answer whether the entity is known or actively visible.
        /// </summary>
        public static bool CanExposeMapEntityIdentity(IMapEntity entity)
        {
            return entity != null
                && entity.IsEnabled
                && entity.IsVisibleInGame
                && entity.Category != MapEntityCategory.Artistic
                && entity.CanHover();
        }

        private static List<Vector2Int> GetMapEntityFootprint(IMapEntity entity)
        {
            List<Vector2Int> footprint = new List<Vector2Int>();
            if (entity == null)
            {
                return footprint;
            }

            ILocationComponent location;
            if (entity.TryGetComponent<ILocationComponent>(out location)
                && location.CalculatedBlockingPoints != null
                && location.CalculatedBlockingPoints.Length != 0)
            {
                AddUnique(footprint, location.CalculatedBlockingPoints);
            }
            else
            {
                AddUnique(footprint, entity.Position);
            }

            return footprint;
        }

        private static bool IsTileKnown(IFogManager fogManager, Vector2Int tile)
        {
            try
            {
                return fogManager != null && fogManager.GetFog(tile.x, tile.y) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTileActivelyVisible(IFogManager fogManager, Vector2Int tile)
        {
            try
            {
                return fogManager != null && fogManager.GetFog(tile.x, tile.y) == byte.MaxValue;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMapEntityIdentityTile(
            IClientAdventureFacade facade,
            IMapEntity entity,
            Vector2Int tile)
        {
            if (facade == null || facade.MapEntities == null || entity == null)
            {
                return false;
            }

            try
            {
                IMapEntity tileEntity = facade.MapEntities.GetAt(tile);
                return tileEntity != null
                    && tileEntity.Id == entity.Id
                    && tileEntity.CanHover();
            }
            catch
            {
                return false;
            }
        }

        private static void AddUnique(List<Vector2Int> points, Vector2Int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                AddUnique(points, values[i]);
            }
        }

        private static void AddUnique(List<Vector2Int> points, Vector2Int value)
        {
            if (!Contains(points, value))
            {
                points.Add(value);
            }
        }

        private static bool Contains(List<Vector2Int> points, Vector2Int value)
        {
            if (points == null)
            {
                return false;
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
