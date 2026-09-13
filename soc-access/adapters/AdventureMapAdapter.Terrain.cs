using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// TERRAIN, BYTE TO KIND: the map's road, bridge, water, decoration and ground layers are bytes,
    /// and this is the one place that turns them into the <see cref="AdventureTerrainKind"/> a tile
    /// is spoken and sounded as.
    ///
    /// Split out of AdventureMapAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureMapAdapter
    {
        private byte GetLayerValue(Vector2Int position, LayerKind kind)
        {
            try
            {
                switch (kind)
                {
                    case LayerKind.Road:
                        return _facade.Level.GetRoad(position);
                    case LayerKind.Bridge:
                        return _facade.Level.GetBridge(position);
                    case LayerKind.Water:
                        return _facade.Level.GetWater(position);
                    default:
                        return 0;
                }
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a tile's map layer", exception);
                return 0;
            }
        }

        private AdventureTerrainKind GetTerrain(Vector2Int position)
        {
            AdventureTerrainKind surface = GetSurfaceTerrain(position);
            if (surface != AdventureTerrainKind.Unknown)
            {
                return surface;
            }

            byte water = GetLayerValue(position, LayerKind.Water);
            switch (water)
            {
                case 1:
                    return AdventureTerrainKind.ShallowWater;
                case 2:
                    return AdventureTerrainKind.DeepWater;
                case 3:
                    return AdventureTerrainKind.WaterEdge;
                default:
                    if (water > 0)
                    {
                        return AdventureTerrainKind.Water;
                    }

                    break;
            }

            try
            {
                return GetGroundTerrain(_facade.Level.GetGroundType(position));
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a tile's ground type", exception);
                return AdventureTerrainKind.Unknown;
            }
        }

        private byte GetDecorationValue(Vector2Int position)
        {
            try
            {
                return _facade.Level.GetDecoration(position);
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a tile's decoration", exception);
                return 0;
            }
        }

        /// <summary>
        /// Whatever sits on top of the ground: a decoration hides the bridge or road beneath it,
        /// and a bridge hides the road. Returns Unknown when only ground or water is left.
        /// Kept separate from GetTerrain so that asking "is this a road" answers with the same
        /// rule that decides what the tile is called.
        /// </summary>
        private AdventureTerrainKind GetSurfaceTerrain(Vector2Int position)
        {
            AdventureTerrainKind decorationTerrain = GetDecorationTerrain(GetDecorationValue(position));
            if (decorationTerrain != AdventureTerrainKind.Unknown)
            {
                return decorationTerrain;
            }

            if (GetLayerValue(position, LayerKind.Bridge) > 0)
            {
                return AdventureTerrainKind.Bridge;
            }

            byte road = GetLayerValue(position, LayerKind.Road);
            switch (road)
            {
                case 1:
                    return AdventureTerrainKind.DirtRoad;
                case 2:
                    return AdventureTerrainKind.CobblestoneRoad;
                default:
                    return road > 0 ? AdventureTerrainKind.Road : AdventureTerrainKind.Unknown;
            }
        }

        /// <summary>
        /// The eight decoration brushes the level editor paints with. Value 5 is
        /// the lights brush, which is scenery standing on the ground rather than
        /// a kind of ground, so it deliberately falls through to whatever is
        /// underneath it.
        /// </summary>
        public static AdventureTerrainKind GetDecorationTerrain(byte decoration)
        {
            switch (decoration)
            {
                case 1:
                    return AdventureTerrainKind.AridTrees;
                case 2:
                    return AdventureTerrainKind.TemperateTrees;
                case 3:
                    return AdventureTerrainKind.Mountain;
                case 4:
                    return AdventureTerrainKind.Obstruction;
                case 6:
                    return AdventureTerrainKind.Wall;
                case 7:
                    return AdventureTerrainKind.Deforestation;
                case 8:
                    return AdventureTerrainKind.Farmland;
                default:
                    return AdventureTerrainKind.Unknown;
            }
        }

        private static AdventureTerrainKind GetGroundTerrain(MapGroundType groundType)
        {
            switch (groundType)
            {
                case MapGroundType.Grass:
                    return AdventureTerrainKind.Grass;
                case MapGroundType.Sand:
                    return AdventureTerrainKind.Sand;
                case MapGroundType.Dirt:
                    return AdventureTerrainKind.Dirt;
                case MapGroundType.Water:
                    return AdventureTerrainKind.Water;
                default:
                    return AdventureTerrainKind.Unknown;
            }
        }

        private enum LayerKind
        {
            Road,
            Bridge,
            Water
        }
    }
}
