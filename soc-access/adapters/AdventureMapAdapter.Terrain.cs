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
            return surface != AdventureTerrainKind.Unknown ? surface : GetWaterOrGroundTerrain(position);
        }

        /// <summary>
        /// What a decoration is painted over: the bridge or road under it, else the water or the
        /// ground. A walkable decoration (dead bodies, a campfire, farmland) names the tile, and
        /// this is what the tile is still walked on as, so it is what the tile sounds as.
        /// </summary>
        private AdventureTerrainKind GetTerrainBeneathDecorations(Vector2Int position)
        {
            AdventureTerrainKind paved = GetPavedTerrain(position);
            return paved != AdventureTerrainKind.Unknown ? paved : GetWaterOrGroundTerrain(position);
        }

        private AdventureTerrainKind GetWaterOrGroundTerrain(Vector2Int position)
        {
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

        private byte GetStandaloneDecorationValue(Vector2Int position)
        {
            try
            {
                return _facade.Level.GetStandaloneDecoration(position);
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a tile's standalone decoration", exception);
                return 0;
            }
        }

        private byte GetEffectValue(Vector2Int position)
        {
            try
            {
                return _facade.Level.GetEffect(position);
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a tile's effect", exception);
                return 0;
            }
        }

        public AdventureEffectKind GetEffect(Vector2Int position)
        {
            return GetEffectKind(GetEffectValue(position));
        }

        /// <summary>
        /// Whatever sits on top of the ground: a standalone decoration hides the decoration beneath
        /// it, a decoration hides the bridge or road beneath it, and a bridge hides the road.
        /// Returns Unknown when only ground or water is left.
        /// Kept separate from GetTerrain so that asking "is this a road" answers with the same
        /// rule that decides what the tile is called.
        /// </summary>
        private AdventureTerrainKind GetSurfaceTerrain(Vector2Int position)
        {
            AdventureTerrainKind standaloneTerrain = GetStandaloneDecorationTerrain(GetStandaloneDecorationValue(position));
            if (standaloneTerrain != AdventureTerrainKind.Unknown)
            {
                return standaloneTerrain;
            }

            AdventureTerrainKind decorationTerrain = GetDecorationTerrain(GetDecorationValue(position));
            if (decorationTerrain != AdventureTerrainKind.Unknown)
            {
                return decorationTerrain;
            }

            return GetPavedTerrain(position);
        }

        /// <summary>The bridge or the road on a tile, a bridge hiding the road; Unknown for neither.</summary>
        private AdventureTerrainKind GetPavedTerrain(Vector2Int position)
        {
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
        /// The eight decoration brushes the level editor paints with.
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
                case 5:
                    return AdventureTerrainKind.Torch;
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

        /// <summary>
        /// The standalone decoration brushes, by the brush numbers the cartography manifest gives
        /// them. Brushes that mean the same thing share a kind: the Yulan tombstones are tombstones.
        /// A switch rather than a manifest lookup, because this is read for every tile of the
        /// scanner's whole-map sweep.
        /// </summary>
        public static AdventureTerrainKind GetStandaloneDecorationTerrain(byte standaloneDecoration)
        {
            switch (standaloneDecoration)
            {
                case 1:
                    return AdventureTerrainKind.FaeyForest;
                case 2:
                    return AdventureTerrainKind.DeadBodies;
                case 3:
                    return AdventureTerrainKind.Bones;
                case 4:
                    return AdventureTerrainKind.Palisade;
                case 5:
                    return AdventureTerrainKind.Tombstones;
                case 6:
                    return AdventureTerrainKind.Farmland;
                case 7:
                    return AdventureTerrainKind.Structures;
                case 8:
                    return AdventureTerrainKind.Campfire;
                case 9:
                    return AdventureTerrainKind.WinterDecorations;
                case 10:
                    return AdventureTerrainKind.DragonBones;
                case 11:
                    return AdventureTerrainKind.Excavation;
                case 12:
                    return AdventureTerrainKind.HuntingCamp;
                case 13:
                    return AdventureTerrainKind.FishingSpot;
                case 14:
                    return AdventureTerrainKind.MidsummerDecorations;
                case 15:
                    return AdventureTerrainKind.DeadSoldiers;
                case 16:
                    return AdventureTerrainKind.Ruins;
                case 17:
                    return AdventureTerrainKind.FortifiedGate;
                case 18:
                    return AdventureTerrainKind.Barricade;
                case 19:
                    return AdventureTerrainKind.BirchForest;
                case 20:
                    return AdventureTerrainKind.Magnolia;
                case 21:
                    return AdventureTerrainKind.Bamboo;
                case 22:
                    return AdventureTerrainKind.Tombstones;
                default:
                    return AdventureTerrainKind.Unknown;
            }
        }

        /// <summary>
        /// The effect brushes, by the brush numbers the cartography manifest gives them. The three
        /// fog brushes are one fog.
        /// </summary>
        public static AdventureEffectKind GetEffectKind(byte effect)
        {
            switch (effect)
            {
                case 1:
                    return AdventureEffectKind.Fireflies;
                case 2:
                    return AdventureEffectKind.BurnMarks;
                case 3:
                case 8:
                case 9:
                    return AdventureEffectKind.Fog;
                case 4:
                    return AdventureEffectKind.Smoke;
                case 5:
                    return AdventureEffectKind.Wildfire;
                case 6:
                    return AdventureEffectKind.RaysOfLight;
                case 7:
                    return AdventureEffectKind.Snow;
                default:
                    return AdventureEffectKind.Unknown;
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
