using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Localization;
using Unity.Mathematics;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal static class StoryCameraFocusResolver
    {
        public static StoryCameraFocusTarget ResolvePointTarget(
            IClientAdventureFacade facade,
            object cartographyConverter,
            ILocalizationHandler localizationHandler,
            CameraFocusPointIdentifier camera)
        {
            if (facade == null || camera.TargetType != CameraFocusPointTargetType.Point)
            {
                return null;
            }

            IMapEntity focusPoint = FindCameraFocusPoint(facade, camera.reference);
            if (focusPoint == null)
            {
                SocAccessPlugin.Instance?.LogWarning("Story camera focus point not found: " + camera.reference);
                return null;
            }

            Vector2Int tile = focusPoint.Position;
            return new StoryCameraFocusTarget(DescribeTileTarget(facade, localizationHandler, tile, focusPoint), tile);
        }

        public static StoryCameraFocusTarget ResolveWielderTarget(
            IClientAdventureFacade facade,
            ICommanderState commander)
        {
            if (facade == null || commander == null)
            {
                return null;
            }

            string name = SafeCommanderName(facade, commander.Id);
            return new StoryCameraFocusTarget(name, commander.Position);
        }

        public static StoryCameraFocusTarget ResolveWorldPositionTarget(
            IClientAdventureFacade facade,
            object cartographyConverter,
            ILocalizationHandler localizationHandler,
            string label,
            Vector3 worldPosition)
        {
            if (!IsValidWorldPosition(worldPosition))
            {
                return null;
            }

            Vector2Int? tile = WorldToTile(cartographyConverter, worldPosition);
            if (!tile.HasValue)
            {
                return null;
            }

            string resolvedLabel = string.IsNullOrWhiteSpace(label)
                ? DescribeTileTarget(facade, localizationHandler, tile.Value, null)
                : label;
            return new StoryCameraFocusTarget(resolvedLabel, tile.Value);
        }

        public static Vector2Int? TryWorldToTile(object cartographyConverter, Vector3 worldPosition)
        {
            if (!IsValidWorldPosition(worldPosition))
            {
                return null;
            }

            return WorldToTile(cartographyConverter, worldPosition);
        }

        public static string LocalizeName(ILocalizationHandler localizationHandler, string nameKey, int pluralCount)
        {
            if (string.IsNullOrWhiteSpace(nameKey))
            {
                return string.Empty;
            }

            if (localizationHandler == null)
            {
                return nameKey;
            }

            try
            {
                return pluralCount >= 0
                    ? localizationHandler.TryGetPluralText(nameKey, pluralCount, nameKey)
                    : localizationHandler.TryGetText(nameKey, nameKey);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to localize story camera focus target " + nameKey + ": " + exception.Message);
                return nameKey;
            }
        }

        public static bool IsValidWorldPosition(Vector3 position)
        {
            return !float.IsInfinity(position.x)
                && !float.IsInfinity(position.y)
                && !float.IsInfinity(position.z)
                && !float.IsNaN(position.x)
                && !float.IsNaN(position.y)
                && !float.IsNaN(position.z);
        }

        private static string DescribeTileTarget(
            IClientAdventureFacade facade,
            ILocalizationHandler localizationHandler,
            Vector2Int tile,
            IMapEntity focusPoint)
        {
            ICommanderState commander = FindCommanderAt(facade, tile);
            if (commander != null)
            {
                return SafeCommanderName(facade, commander.Id);
            }

            IMapEntity entity = FindMeaningfulEntityAt(facade, tile, focusPoint);
            if (entity != null)
            {
                return GetMapEntityName(entity, localizationHandler);
            }

            return ModText.Get(ModStrings.Events.StoryCameraFocusTile);
        }

        private static ICommanderState FindCommanderAt(IClientAdventureFacade facade, Vector2Int tile)
        {
            if (facade == null || facade.Commanders == null || facade.Commanders.All == null)
            {
                return null;
            }

            return facade.Commanders.All.FirstOrDefault(commander =>
                commander != null
                && commander.IsAlive
                && commander.Position == tile);
        }

        private static IMapEntity FindMeaningfulEntityAt(IClientAdventureFacade facade, Vector2Int tile, IMapEntity focusPoint)
        {
            if (facade == null || facade.MapEntities == null)
            {
                return null;
            }

            try
            {
                IMapEntity entity = facade.MapEntities.GetAt(tile);
                if (IsMeaningfulEntity(entity, focusPoint))
                {
                    return entity;
                }
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to resolve story camera focus tile entity at " + FormatTile(tile) + ": " + exception.Message);
            }

            return facade.MapEntities.All != null
                ? facade.MapEntities.All.FirstOrDefault(entity => IsMeaningfulEntity(entity, focusPoint) && entity.Position == tile)
                : null;
        }

        private static bool IsMeaningfulEntity(IMapEntity entity, IMapEntity focusPoint)
        {
            return entity != null
                && !ReferenceEquals(entity, focusPoint)
                && entity.IsVisibleInGame
                && entity.Category != MapEntityCategory.Artistic
                && !entity.HasComponent<ICameraFocusPointComponent>();
        }

        private static IMapEntity FindCameraFocusPoint(IClientAdventureFacade facade, string reference)
        {
            if (facade == null || facade.MapEntities == null || facade.MapEntities.All == null || string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            return facade.MapEntities.All.FirstOrDefault(entity =>
                entity != null
                && entity.HasComponent<ICameraFocusPointComponent>()
                && entity.GetComponent<ICameraFocusPointComponent>().Name == reference);
        }

        private static Vector2Int? WorldToTile(object cartographyConverter, Vector3 worldPosition)
        {
            if (cartographyConverter == null)
            {
                return null;
            }

            try
            {
                MethodInfo method = AccessTools.Method(cartographyConverter.GetType(), "WorldToPoint", new[] { typeof(float3) });
                if (method == null)
                {
                    return null;
                }

                object point = method.Invoke(cartographyConverter, new object[] { new float3(worldPosition.x, worldPosition.y, worldPosition.z) });
                if (point is int2)
                {
                    int2 intPoint = (int2)point;
                    return new Vector2Int(intPoint.x, intPoint.y);
                }
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to convert story camera focus world position to tile: " + exception.Message);
            }

            return null;
        }

        private static string SafeCommanderName(IClientAdventureFacade facade, int commanderId)
        {
            try
            {
                string name = facade != null && facade.Commanders != null
                    ? facade.Commanders.GetName(commanderId)
                    : string.Empty;
                return string.IsNullOrWhiteSpace(name) ? ModText.Get(ModStrings.Events.Wielder) : name;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to resolve story camera focus wielder name: " + exception.Message);
                return ModText.Get(ModStrings.Events.Wielder);
            }
        }

        private static string GetMapEntityName(IMapEntity entity, ILocalizationHandler localizationHandler)
        {
            if (entity == null)
            {
                return ModText.Get(ModStrings.Events.StoryCameraFocusMapEntity);
            }

            string customNameKey;
            if (entity.TryGetCustomNameKey(out customNameKey))
            {
                string customName = Localize(localizationHandler, customNameKey);
                if (!string.IsNullOrWhiteSpace(customName))
                {
                    return customName;
                }
            }

            string localizedName = Localize(localizationHandler, entity.NameKey);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return localizedName;
            }

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                return entity.Name;
            }

            return string.IsNullOrWhiteSpace(entity.NameKey) ? ModText.Get(ModStrings.Events.StoryCameraFocusMapEntity) : entity.NameKey;
        }

        private static string Localize(ILocalizationHandler localizationHandler, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || localizationHandler == null)
            {
                return string.Empty;
            }

            try
            {
                string text = localizationHandler.GetText(key);
                return string.IsNullOrWhiteSpace(text) || text == key ? string.Empty : text;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + ", " + tile.y;
        }
    }
}
