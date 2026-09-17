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
    /// THE TERRAIN SWEEP: the whole-map scans the scanner's terrain, unexplored and obstacle
    /// categories are built from, and the flood fill that gathers neighbouring tiles of a kind into
    /// one group so the player hears "forest" once rather than once per tile.
    ///
    /// Split out of AdventureMapAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureMapAdapter
    {
        private void AddAdventureTerrainScannerResults(ScannerSnapshot snapshot, Vector2Int origin, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            if (_facade == null || _facade.Level == null)
            {
                return;
            }

            TerrainScanCell[,] terrain = BuildTerrainScan(GetLocalTeamId());
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.RoadsAndCrossings, ScannerItemKeys.Road, ModStrings.Spatial.Road, origin, cell => cell.Terrain == AdventureTerrainKind.Road);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.RoadsAndCrossings, ScannerItemKeys.DirtRoad, ModStrings.Spatial.DirtRoad, origin, cell => cell.Terrain == AdventureTerrainKind.DirtRoad);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.RoadsAndCrossings, ScannerItemKeys.CobblestoneRoad, ModStrings.Spatial.CobblestoneRoad, origin, cell => cell.Terrain == AdventureTerrainKind.CobblestoneRoad);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.RoadsAndCrossings, ScannerItemKeys.Bridge, ModStrings.Spatial.Bridge, origin, cell => cell.Terrain == AdventureTerrainKind.Bridge);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Grass, ModStrings.Spatial.Grass, origin, cell => cell.Terrain == AdventureTerrainKind.Grass);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Sand, ModStrings.Spatial.Sand, origin, cell => cell.Terrain == AdventureTerrainKind.Sand);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Dirt, ModStrings.Spatial.Dirt, origin, cell => cell.Terrain == AdventureTerrainKind.Dirt);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Farmland, ModStrings.Spatial.Farmland, origin, cell => cell.Terrain == AdventureTerrainKind.Farmland);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.AridTrees, ModStrings.Spatial.AridTrees, origin, cell => cell.Terrain == AdventureTerrainKind.AridTrees);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.TemperateTrees, ModStrings.Spatial.TemperateTrees, origin, cell => cell.Terrain == AdventureTerrainKind.TemperateTrees);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Deforestation, ModStrings.Spatial.Deforestation, origin, cell => cell.Terrain == AdventureTerrainKind.Deforestation);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.DeadBodies, ModStrings.Spatial.DeadBodies, origin, cell => cell.Terrain == AdventureTerrainKind.DeadBodies);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.DeadSoldiers, ModStrings.Spatial.DeadSoldiers, origin, cell => cell.Terrain == AdventureTerrainKind.DeadSoldiers);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Bones, ModStrings.Spatial.Bones, origin, cell => cell.Terrain == AdventureTerrainKind.Bones);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.DragonBones, ModStrings.Spatial.DragonBones, origin, cell => cell.Terrain == AdventureTerrainKind.DragonBones);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Structures, ModStrings.Spatial.Structures, origin, cell => cell.Terrain == AdventureTerrainKind.Structures);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Campfire, ModStrings.Spatial.Campfire, origin, cell => cell.Terrain == AdventureTerrainKind.Campfire);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.Excavation, ModStrings.Spatial.Excavation, origin, cell => cell.Terrain == AdventureTerrainKind.Excavation);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.HuntingCamp, ModStrings.Spatial.HuntingCamp, origin, cell => cell.Terrain == AdventureTerrainKind.HuntingCamp);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.OpenGround, ScannerItemKeys.FishingSpot, ModStrings.Spatial.FishingSpot, origin, cell => cell.Terrain == AdventureTerrainKind.FishingSpot);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Mountain, ModStrings.Spatial.Mountain, origin, cell => cell.Terrain == AdventureTerrainKind.Mountain);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Wall, ModStrings.Spatial.Wall, origin, cell => cell.Terrain == AdventureTerrainKind.Wall);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Obstruction, ModStrings.Spatial.Obstruction, origin, cell => cell.Terrain == AdventureTerrainKind.Obstruction);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Water, ModStrings.Spatial.Water, origin, cell => cell.Terrain == AdventureTerrainKind.Water);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.ShallowWater, ModStrings.Spatial.ShallowWater, origin, cell => cell.Terrain == AdventureTerrainKind.ShallowWater);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.DeepWater, ModStrings.Spatial.DeepWater, origin, cell => cell.Terrain == AdventureTerrainKind.DeepWater);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.WaterEdge, ModStrings.Spatial.WaterEdge, origin, cell => cell.Terrain == AdventureTerrainKind.WaterEdge);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.FaeyForest, ModStrings.Spatial.FaeyForest, origin, cell => cell.Terrain == AdventureTerrainKind.FaeyForest);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.BirchForest, ModStrings.Spatial.BirchForest, origin, cell => cell.Terrain == AdventureTerrainKind.BirchForest);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Magnolia, ModStrings.Spatial.Magnolia, origin, cell => cell.Terrain == AdventureTerrainKind.Magnolia);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Bamboo, ModStrings.Spatial.Bamboo, origin, cell => cell.Terrain == AdventureTerrainKind.Bamboo);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Palisade, ModStrings.Spatial.Palisade, origin, cell => cell.Terrain == AdventureTerrainKind.Palisade);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.FortifiedGate, ModStrings.Spatial.FortifiedGate, origin, cell => cell.Terrain == AdventureTerrainKind.FortifiedGate);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Barricade, ModStrings.Spatial.Barricade, origin, cell => cell.Terrain == AdventureTerrainKind.Barricade);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Tombstones, ModStrings.Spatial.Tombstones, origin, cell => cell.Terrain == AdventureTerrainKind.Tombstones);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Ruins, ModStrings.Spatial.Ruins, origin, cell => cell.Terrain == AdventureTerrainKind.Ruins);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.WinterDecorations, ModStrings.Spatial.WinterDecorations, origin, cell => cell.Terrain == AdventureTerrainKind.WinterDecorations);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.MidsummerDecorations, ModStrings.Spatial.MidsummerDecorations, origin, cell => cell.Terrain == AdventureTerrainKind.MidsummerDecorations);
            AddTerrainGroups(snapshot, terrain, ScannerSubcategoryKeys.Barriers, ScannerItemKeys.Torch, ModStrings.Spatial.Torch, origin, cell => cell.Terrain == AdventureTerrainKind.Torch);
            // No Impassable item: it is a property of the ground rather than a
            // kind of it, so every tile it would gather is already under its
            // own name in this same subcategory.
            AddScannerGroups(
                snapshot,
                ScannerCategoryKeys.Obstacles,
                ScannerSubcategoryKeys.All,
                terrain,
                ScannerItemKeys.GuardedGround,
                ModStrings.Scanner.GuardedGround,
                origin,
                ScannerResultKind.AreaGroup,
                cell => cell.Guarded);
        }

        private void AddUnexploredScannerResults(ScannerSnapshot snapshot, Vector2Int origin)
        {
            if (_facade == null || _facade.Level == null || _selectionHandler == null)
            {
                return;
            }

            TerrainScanCell[,] unexplored = BuildUnexploredScan(GetLocalTeamId());
            AddScannerGroups(
                snapshot,
                ScannerCategoryKeys.Exploration,
                ScannerSubcategoryKeys.Unexplored,
                unexplored,
                ScannerItemKeys.Unexplored,
                ModStrings.Scanner.Unexplored,
                origin,
                ScannerResultKind.UnexploredGroup,
                cell => true);
        }

        private TerrainScanCell[,] BuildUnexploredScan(int localTeamId)
        {
            int width = _facade.Level.Width;
            int height = _facade.Level.Height;
            TerrainScanCell[,] unexplored = new TerrainScanCell[width, height];
            ICommanderState selectedCommander = _selectionHandler != null ? _selectionHandler.SelectedCommander : null;
            if (selectedCommander == null || !selectedCommander.IsAlive || localTeamId < 0)
            {
                return unexplored;
            }

            int pathingTeamId = selectedCommander.TeamId;
            byte[] exploration = _facade.Level.GetExplorationForTeam(localTeamId);
            bool[,] reachable = BuildReachableMoveDestinationScan(selectedCommander, pathingTeamId);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Vector2Int point = new Vector2Int(x, y);
                    bool eligible = IsUnexplored(exploration, index, point)
                        && reachable[x, y];
                    unexplored[x, y] = new TerrainScanCell
                    {
                        Explored = eligible
                    };
                }
            }

            return unexplored;
        }

        private TerrainScanCell[,] BuildTerrainScan(int localTeamId)
        {
            int width = _facade.Level.Width;
            int height = _facade.Level.Height;
            byte[] exploration = localTeamId >= 0 ? _facade.Level.GetExplorationForTeam(localTeamId) : null;
            // The guarded ground is the zone of control the tile readout speaks, read from the
            // same per-frame map, so a tile the scan calls guarded is a tile the cursor says is
            // within someone's zone of control.
            Dictionary<Vector2Int, List<string>> zonesOfControl = localTeamId >= 0 ? GetZoneOfControlNames(localTeamId) : null;
            TerrainScanCell[,] terrain = new TerrainScanCell[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Vector2Int point = new Vector2Int(x, y);
                    bool explored = IsExplored(exploration, index);
                    bool visible = false;
                    if (!explored && _fogManager != null)
                    {
                        visible = GetFog(point) == byte.MaxValue || _fogManager.IsVisible(point);
                    }

                    bool eligible = explored || visible;
                    bool impassable = eligible && IsImpassableTerrain(localTeamId, point);
                    terrain[x, y] = new TerrainScanCell
                    {
                        Explored = eligible,
                        Terrain = eligible ? GetTerrain(point) : AdventureTerrainKind.Unknown,
                        Impassable = impassable,
                        Guarded = eligible && !impassable && zonesOfControl != null && zonesOfControl.ContainsKey(point)
                    };
                }
            }

            return terrain;
        }

        private static bool IsExplored(byte[] exploration, int index)
        {
            return exploration != null
                && index >= 0
                && index < exploration.Length
                && exploration[index] == ExploredButNotVisibleFogValue;
        }

        private bool IsUnexplored(byte[] exploration, int index, Vector2Int point)
        {
            if (IsExplored(exploration, index))
            {
                return false;
            }

            try
            {
                return _fogManager == null || (GetFog(point) == 0 && !_fogManager.IsVisible(point));
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading whether a point is unexplored", exception);
                return false;
            }
        }

        private Func<Vector2Int, bool> CreateUnexploredPointValidator()
        {
            if (_selectionHandler == null || _facade == null || _facade.Level == null)
            {
                return null;
            }

            ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
            if (selectedCommander == null || !selectedCommander.IsAlive)
            {
                return null;
            }

            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0)
            {
                return null;
            }

            byte[] exploration = _facade.Level.GetExplorationForTeam(localTeamId);
            int width = _facade.Level.Width;
            // The reachability flood walks the whole map, so it is built once
            // for the refresh rather than once per tile judged.
            bool[,] reachable = BuildReachableMoveDestinationScan(selectedCommander, selectedCommander.TeamId);
            return position => IsWithinMap(position)
                && IsUnexplored(exploration, position.y * width + position.x, position)
                && reachable[position.x, position.y];
        }

        private bool[,] BuildReachableMoveDestinationScan(ICommanderState selectedCommander, int teamId)
        {
            int width = _facade.Level.Width;
            int height = _facade.Level.Height;
            bool[,] reachable = new bool[width, height];
            if (selectedCommander == null || _facade == null || _facade.Level == null || teamId < 0)
            {
                return reachable;
            }

            Vector2Int start = selectedCommander.Position;
            if (!IsWithinMap(start))
            {
                return reachable;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            reachable[start.x, start.y] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int point = queue.Dequeue();
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x + 1, point.y);
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x - 1, point.y);
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x, point.y + 1);
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x, point.y - 1);
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x + 1, point.y + 1);
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x - 1, point.y + 1);
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x + 1, point.y - 1);
                EnqueueReachableMoveNeighbor(queue, reachable, teamId, point.x - 1, point.y - 1);
            }

            return reachable;
        }

        private void EnqueueReachableMoveNeighbor(Queue<Vector2Int> queue, bool[,] reachable, int teamId, int x, int y)
        {
            if (x < 0 || y < 0 || x >= _facade.Level.Width || y >= _facade.Level.Height || reachable[x, y])
            {
                return;
            }

            Vector2Int point = new Vector2Int(x, y);
            if (!IsValidUnexploredMovementDestination(teamId, point))
            {
                return;
            }

            reachable[x, y] = true;
            queue.Enqueue(point);
        }

        private bool IsValidUnexploredMovementDestination(int teamId, Vector2Int point)
        {
            try
            {
                return _facade.Level.IsValidMoveDestination(teamId, point);
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading whether an unexplored point can be moved to", exception);
                return false;
            }
        }

        private bool IsImpassableTerrain(int localTeamId, Vector2Int point)
        {
            if (localTeamId < 0)
            {
                return false;
            }

            try
            {
                return float.IsPositiveInfinity(_facade.Level.GetStaticTravelCost(localTeamId, point));
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a point's static travel cost", exception);
                return false;
            }
        }

        private void AddTerrainGroups(
            ScannerSnapshot snapshot,
            TerrainScanCell[,] terrain,
            string subcategory,
            string itemKey,
            ModString itemLabel,
            Vector2Int origin,
            Func<TerrainScanCell, bool> predicate)
        {
            AddScannerGroups(snapshot, ScannerCategoryKeys.Terrain, subcategory, terrain, itemKey, itemLabel, origin, ScannerResultKind.TerrainGroup, predicate);
        }

        /// <summary>
        /// The kind of ground is the item and each contiguous cluster of it is
        /// an instance, so cycling terrain steps between kinds and the tile
        /// counts belong to the patches.
        /// </summary>
        private void AddScannerGroups(
            ScannerSnapshot snapshot,
            string category,
            string subcategory,
            TerrainScanCell[,] terrain,
            string itemKey,
            ModString itemLabel,
            Vector2Int origin,
            ScannerResultKind kind,
            Func<TerrainScanCell, bool> predicate)
        {
            int width = _facade.Level.Width;
            int height = _facade.Level.Height;
            bool[,] visited = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (visited[x, y])
                    {
                        continue;
                    }

                    Vector2Int start = new Vector2Int(x, y);
                    if (!terrain[x, y].Explored || !predicate(terrain[x, y]))
                    {
                        visited[x, y] = true;
                        continue;
                    }

                    List<Vector2Int> group = FloodTerrainGroup(start, terrain, visited, predicate);
                    Vector2Int representative = ClosestPoint(group, origin);
                    ScannerResult result = new ScannerResult(
                        ScannerGroupKey(category, subcategory, itemKey, group),
                        ModText.Get(itemLabel),
                        representative)
                    {
                        Kind = kind,
                        ItemKey = itemKey,
                        InstanceLabel = ModText.Plural(ModStrings.Common.TileCount, group.Count, group.Count)
                    };
                    result.Points.AddRange(group);
                    snapshot.Add(category, subcategory, result);
                }
            }
        }

        private List<Vector2Int> FloodTerrainGroup(Vector2Int start, TerrainScanCell[,] terrain, bool[,] visited, Func<TerrainScanCell, bool> predicate)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited[start.x, start.y] = true;
            while (queue.Count > 0)
            {
                Vector2Int point = queue.Dequeue();
                TerrainScanCell cell = terrain[point.x, point.y];
                if (!cell.Explored || !predicate(cell))
                {
                    continue;
                }

                result.Add(point);
                EnqueueTerrainNeighbors(queue, visited, point);
            }

            return result;
        }

        private struct TerrainScanCell
        {
            public bool Explored;

            public AdventureTerrainKind Terrain;

            public bool Impassable;

            public bool Guarded;
        }

        private void EnqueueTerrainNeighbors(Queue<Vector2Int> queue, bool[,] visited, Vector2Int point)
        {
            EnqueueTerrainNeighbor(queue, visited, point.x + 1, point.y);
            EnqueueTerrainNeighbor(queue, visited, point.x - 1, point.y);
            EnqueueTerrainNeighbor(queue, visited, point.x, point.y + 1);
            EnqueueTerrainNeighbor(queue, visited, point.x, point.y - 1);
            EnqueueTerrainNeighbor(queue, visited, point.x + 1, point.y + 1);
            EnqueueTerrainNeighbor(queue, visited, point.x - 1, point.y + 1);
            EnqueueTerrainNeighbor(queue, visited, point.x + 1, point.y - 1);
            EnqueueTerrainNeighbor(queue, visited, point.x - 1, point.y - 1);
        }

        private void EnqueueTerrainNeighbor(Queue<Vector2Int> queue, bool[,] visited, int x, int y)
        {
            if (x < 0 || y < 0 || x >= _facade.Level.Width || y >= _facade.Level.Height || visited[x, y])
            {
                return;
            }

            visited[x, y] = true;
            queue.Enqueue(new Vector2Int(x, y));
        }
    }
}
