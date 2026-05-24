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
        public static bool TryGetFullyVisibleMapEntityIdentityTile(
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
            if (!IsFootprintFullyVisible(fogManager, footprint))
            {
                return false;
            }

            if (Contains(footprint, entity.Position)
                && IsMapEntityIdentityTile(facade, entity, entity.Position))
            {
                tile = entity.Position;
                return true;
            }

            for (int i = 0; i < footprint.Count; i++)
            {
                if (IsMapEntityIdentityTile(facade, entity, footprint[i]))
                {
                    tile = footprint[i];
                    return true;
                }
            }

            return false;
        }

        public static bool IsFullyVisibleMapEntityIdentityTile(
            IClientAdventureFacade facade,
            IFogManager fogManager,
            IMapEntity entity,
            Vector2Int tile)
        {
            if (!CanExposeMapEntityIdentity(entity))
            {
                return false;
            }

            List<Vector2Int> footprint = GetMapEntityFootprint(entity);
            return IsFootprintFullyVisible(fogManager, footprint)
                && IsMapEntityIdentityTile(facade, entity, tile);
        }

        private static bool CanExposeMapEntityIdentity(IMapEntity entity)
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

        private static bool IsFootprintFullyVisible(IFogManager fogManager, List<Vector2Int> footprint)
        {
            if (fogManager == null || footprint == null || footprint.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < footprint.Count; i++)
            {
                if (!IsTileFullyVisible(fogManager, footprint[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTileFullyVisible(IFogManager fogManager, Vector2Int tile)
        {
            try
            {
                return fogManager.GetFog(tile.x, tile.y) == byte.MaxValue;
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
