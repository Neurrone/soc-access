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
    /// THE SCANNER: the snapshot of everything on the map worth finding, the revalidation that
    /// keeps a held result pointing at something that still exists, and the zone-of-control reads
    /// both the snapshot and a tile depend on.
    ///
    /// Split out of AdventureMapAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureMapAdapter
    {
        public ScannerSnapshot BuildScannerSnapshot(Vector2Int origin)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot(AdventureScannerTaxonomy.Instance);
            Dictionary<Vector2Int, AdventureMapTile> tileCache = new Dictionary<Vector2Int, AdventureMapTile>();
            int localTeamId = GetLocalTeamId();
            ScannerContribution.Run(ScannerCategoryKeys.Pickups, () => AddPickupScannerResults(snapshot, tileCache));
            ScannerContribution.Run(ScannerCategoryKeys.ResourceGenerators, () => AddResourceGeneratorScannerResults(snapshot, localTeamId, tileCache));
            ScannerContribution.Run(ScannerSubcategoryKeys.Beacons, () => AddBeaconScannerResults(snapshot, tileCache));
            ScannerContribution.Run(ScannerCategoryKeys.Wielders, () => AddWielderScannerResults(snapshot, tileCache));
            ScannerContribution.Run(ScannerCategoryKeys.SettlementsAndBuildSites, () => AddStructuralScannerResults(snapshot, localTeamId, tileCache, MapEntityCategory.Town, MapEntityCategory.Settlement, MapEntityCategory.BuildSite));
            ScannerContribution.Run(ScannerCategoryKeys.TroopSources, () => AddTroopSourceScannerResults(snapshot, localTeamId, tileCache));
            ScannerContribution.Run(ScannerCategoryKeys.Buildings, () => AddStructuralScannerResults(snapshot, localTeamId, tileCache, MapEntityCategory.Building));
            ScannerContribution.Run(ScannerSubcategoryKeys.Objectives, () => AddObjectiveScannerResults(snapshot, tileCache));
            ScannerContribution.Run(ScannerCategoryKeys.Obstacles, () => AddObstacleScannerResults(snapshot, localTeamId, origin, tileCache));
            ScannerContribution.Run(ScannerSubcategoryKeys.ArtifactMarkets, () => AddArtifactMarketScannerResults(snapshot, tileCache));
            ScannerContribution.Run(ScannerSubcategoryKeys.Merchants, () => AddMerchantScannerResults(snapshot, tileCache));
            ScannerContribution.Run(ScannerSubcategoryKeys.Teleport, () => AddTeleportScannerResults(snapshot, tileCache));
            ScannerContribution.Run(ScannerCategoryKeys.Terrain, () => AddAdventureTerrainScannerResults(snapshot, origin, tileCache));
            ScannerContribution.Run(ScannerSubcategoryKeys.Unexplored, () => AddUnexploredScannerResults(snapshot, origin));
            ScannerContribution.Run(ScannerSubcategoryKeys.Revealed, () => AddRevealedScannerResults(snapshot));
            return snapshot;
        }

        public IReadOnlyList<ReachableAdventureEntity> GetReachableAdventureEntities()
        {
            List<ReachableAdventureEntity> results = new List<ReachableAdventureEntity>();
            if (_facade == null || _facade.MapEntities == null || _facade.Level == null || _selectionHandler == null)
            {
                return results;
            }

            ICommanderState selectedCommander = _selectionHandler.SelectedCommander;
            if (selectedCommander == null || !selectedCommander.IsAlive)
            {
                return results;
            }

            IEnumerable<IMapEntity> entities = _facade.MapEntities.All;
            if (entities == null)
            {
                return results;
            }

            int teamId = selectedCommander.TeamId;
            if (teamId < 0)
            {
                return results;
            }

            Dictionary<Vector2Int, AdventureMapTile> tileCache = new Dictionary<Vector2Int, AdventureMapTile>();
            AddReachableCommanderResults(results, selectedCommander, teamId, tileCache);
            AddReachableMapEntityResults(results, selectedCommander, teamId, tileCache);
            return results;
        }

        private void AddReachableMapEntityResults(
            List<ReachableAdventureEntity> results,
            ICommanderState selectedCommander,
            int teamId,
            Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            if (results == null || selectedCommander == null || _facade == null || _facade.MapEntities == null || _facade.Level == null)
            {
                return;
            }

            IEnumerable<IMapEntity> entities = _facade.MapEntities.All;
            if (entities == null)
            {
                return;
            }

            foreach (IMapEntity entity in entities)
            {
                if (entity == null || !entity.IsEnabled || !IsWithinMap(entity.Position))
                {
                    continue;
                }

                if (ShouldExcludeReachableMapEntity(entity, selectedCommander, teamId))
                {
                    continue;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    continue;
                }

                if (!_facade.Level.CanMoveToAndInteract(entity.Id, selectedCommander.Id))
                {
                    continue;
                }

                float distance;
                if (!TryGetReachableMapEntityDistance(entity, selectedCommander, teamId, out distance))
                {
                    distance = _facade.Level.Distance(selectedCommander.Position, tile.Position);
                }

                string name = FirstNonEmpty(tile.MapEntityName, GetMapEntityName(entity));
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                results.Add(new ReachableAdventureEntity(entity.Id, name, tile.Position, distance));
            }
        }

        private void AddReachableCommanderResults(
            List<ReachableAdventureEntity> results,
            ICommanderState selectedCommander,
            int teamId,
            Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            if (results == null || selectedCommander == null || _facade == null || _facade.Commanders == null || _facade.Level == null)
            {
                return;
            }

            IEnumerable<ICommanderState> commanders = _facade.Commanders.All;
            if (commanders == null)
            {
                return;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (commander == null
                    || !commander.IsAlive
                    || commander.Id == selectedCommander.Id
                    || !IsWithinMap(commander.Position))
                {
                    continue;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, commander.Position);
                if (tile == null || tile.Commander == null || tile.Commander.Raw == null || tile.Commander.Raw.Id != commander.Id)
                {
                    continue;
                }

                float distance;
                if (!TryGetReachableCommanderDistance(commander, selectedCommander, teamId, out distance))
                {
                    continue;
                }

                string name = FirstNonEmpty(tile.Commander.Name, AdventureMapEntityLabel.GetCommanderName(_facade, commander));
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                results.Add(new ReachableAdventureEntity(commander.Id, name, commander.Position, distance));
            }
        }

        public ScannerResultRefresh TryRefreshScannerResult(ScannerResult result, Vector2Int cursorHint)
        {
            if (result == null)
            {
                return ScannerResultRefresh.Invalid;
            }

            Vector2Int position;
            if (!TryRevalidateScannerResult(result, cursorHint, out position))
            {
                RemoveRevealedScannerResult(result);
                return ScannerResultRefresh.Invalid;
            }

            return ScannerResultRefresh.Valid(position);
        }

        /// <summary>
        /// Judges a result against the live map and picks the tile it speaks
        /// through. A result covering many tiles is announced through one
        /// representative, picked when the snapshot was built and measured from
        /// wherever the scan started; that goes stale the moment the cursor
        /// moves, so the representative is re-picked against the live cursor.
        /// A group is only gone once none of its tiles qualifies any more:
        /// exploring one fringe tile of a large unexplored region must not take
        /// the whole region away.
        /// </summary>
        private bool TryRevalidateScannerResult(ScannerResult result, Vector2Int cursorHint, out Vector2Int position)
        {
            position = result.Position;
            Func<Vector2Int, bool> isValidPoint = CreateScannerPointValidator(result);
            if (isValidPoint == null)
            {
                return false;
            }

            if (result.Points.Count == 0)
            {
                return isValidPoint(position);
            }

            Vector2Int nearest = ClosestPoint(result.Points, cursorHint);
            if (isValidPoint(nearest))
            {
                position = nearest;
                return true;
            }

            return TryPickSurvivingScannerGroupPoint(result, cursorHint, isValidPoint, out position);
        }

        /// <summary>
        /// Walks a group's tiles outwards from the cursor and answers through
        /// the first one that still qualifies, dropping the closer ones it
        /// proved gone so the group stops speaking through tiles it no longer
        /// covers. Judging a tile costs a pathfind or a map query, so the walk
        /// stops at the survivor rather than sweeping the whole group.
        /// </summary>
        public static bool TryPickSurvivingScannerGroupPoint(
            ScannerResult result,
            Vector2Int cursorHint,
            Func<Vector2Int, bool> isValidPoint,
            out Vector2Int position)
        {
            position = result.Position;
            List<Vector2Int> ordered = new List<Vector2Int>(result.Points);
            ordered.Sort((left, right) =>
            {
                int compared = DistanceSquared(cursorHint, left).CompareTo(DistanceSquared(cursorHint, right));
                return compared != 0 ? compared : ComparePointOrder(left, right);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                if (!isValidPoint(ordered[i]))
                {
                    continue;
                }

                if (i > 0)
                {
                    HashSet<Vector2Int> gone = new HashSet<Vector2Int>();
                    for (int rejected = 0; rejected < i; rejected++)
                    {
                        gone.Add(ordered[rejected]);
                    }

                    result.Points.RemoveAll(point => gone.Contains(point));
                }

                position = ordered[i];
                return true;
            }

            return false;
        }

        private static int ComparePointOrder(Vector2Int left, Vector2Int right)
        {
            int compared = left.x.CompareTo(right.x);
            return compared != 0 ? compared : left.y.CompareTo(right.y);
        }

        /// <summary>
        /// The live test for whether one tile still carries what the result
        /// stands for, or null where the result cannot be valid anywhere any
        /// more. Built once per refresh so the per-kind setup, the exploration
        /// array and the reachability flood, is paid once however many of the
        /// group's tiles have to be judged.
        /// </summary>
        private Func<Vector2Int, bool> CreateScannerPointValidator(ScannerResult result)
        {
            if (result.Kind == ScannerResultKind.CommanderZoneOfControl)
            {
                return CreateCommanderZoneOfControlPointValidator(result);
            }

            if (result.Kind == ScannerResultKind.UnexploredGroup)
            {
                return CreateUnexploredPointValidator();
            }

            return position => IsWithinMap(position) && IsScannerEntityPointValid(result, position);
        }

        private bool IsScannerEntityPointValid(ScannerResult result, Vector2Int position)
        {
            AdventureMapTile tile = GetTile(position);
            if (tile == null || !tile.IsExplored)
            {
                return false;
            }

            if (result.StableReference is int stableId)
            {
                if (tile.Commander != null && tile.Commander.Raw != null && tile.Commander.Raw.Id == stableId)
                {
                    return true;
                }

                IMapEntity entity = TryGetMapEntity(stableId);
                AdventureMapTile identityTile;
                return entity != null && TryGetMapEntityIdentityTile(entity, null, out identityTile);
            }

            return true;
        }

        private bool TryGetReachableMapEntityDistance(
            IMapEntity entity,
            ICommanderState selectedCommander,
            int teamId,
            out float distance)
        {
            distance = 0f;
            if (entity == null || selectedCommander == null || _facade == null || _facade.Level == null)
            {
                return false;
            }

            IInteractableComponent component;
            if (!entity.TryGetComponent<IInteractableComponent>(out component)
                || component.LocalInteractionPoints == null
                || component.LocalInteractionPoints.Length == 0
                || component.CalculatedInteractionPoints == null
                || component.CalculatedInteractionPoints.Length == 0)
            {
                return false;
            }

            Vector2Int destination;
            PathNode[] path;
            if (!_facade.Level.TryGetShortestPathToPoints(
                teamId,
                selectedCommander.Position,
                component.CalculatedInteractionPoints,
                out destination,
                out path,
                (PathfinderCacheType)0))
            {
                return false;
            }

            float movementCost = path != null && path.Length > 1
                ? path[path.Length - 2].travelCost
                : 0f;
            distance = movementCost + entity.GetInteractionCost(selectedCommander.Id);
            return true;
        }

        private bool TryGetReachableCommanderDistance(
            ICommanderState commander,
            ICommanderState selectedCommander,
            int teamId,
            out float distance)
        {
            distance = 0f;
            if (commander == null || selectedCommander == null || _facade == null || _facade.Level == null || _facade.Teams == null)
            {
                return false;
            }

            List<Vector2Int> destinations = new List<Vector2Int>();
            if (_facade.Teams.IsInPartnership(commander.TeamId, teamId))
            {
                destinations.Add(commander.Position);
            }
            else
            {
                IEnumerable<int2> zoneOfControl = _facade.Commanders.GetZoneOfControlPoints(teamId, commander.Id);
                if (zoneOfControl != null)
                {
                    foreach (int2 point in zoneOfControl)
                    {
                        destinations.Add(new Vector2Int(point.x, point.y));
                    }
                }
            }

            if (destinations.Count == 0)
            {
                return false;
            }

            Vector2Int destination;
            PathNode[] path;
            if (!_facade.Level.TryGetShortestPathToPoints(
                teamId,
                selectedCommander.Position,
                destinations,
                out destination,
                out path,
                (PathfinderCacheType)0))
            {
                return false;
            }

            return TryGetLastFinitePathCost(path, out distance)
                && distance <= selectedCommander.MovesLeft;
        }

        private static bool TryGetLastFinitePathCost(PathNode[] path, out float distance)
        {
            distance = 0f;
            if (path == null || path.Length == 0)
            {
                return false;
            }

            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (!float.IsInfinity(path[i].travelCost))
                {
                    distance = path[i].travelCost;
                    return true;
                }
            }

            return false;
        }

        private bool ShouldExcludeReachableMapEntity(IMapEntity entity, ICommanderState selectedCommander, int teamId)
        {
            if (entity == null)
            {
                return true;
            }

            if (IsScannerPickupEntity(entity)
                && selectedCommander != null
                && entity.DidVisit(selectedCommander.Id))
            {
                return true;
            }

            return entity.Category == MapEntityCategory.ResourceGenerator
                && GetMapEntityRelationship(entity, teamId) == "friendly";
        }

        private void RemoveRevealedScannerResult(ScannerResult result)
        {
            if (result == null || _revealedRegistry == null)
            {
                return;
            }

            _revealedRegistry.Remove(result.Key);
        }

        private void AddWielderScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders == null)
            {
                return;
            }

            int localTeamId = GetLocalTeamId();
            foreach (ICommanderState commander in commanders)
            {
                if (commander == null || !commander.IsAlive || !IsWithinMap(commander.Position))
                {
                    continue;
                }

                AdventureMapTile tile = GetScannerTile(tileCache, commander.Position);
                if (tile == null || tile.Commander == null)
                {
                    continue;
                }

                string name = FirstNonEmpty(tile.Commander.Name, ModText.Get(ModStrings.Events.Wielder));
                string relationship = GetCommanderRelationship(commander, localTeamId);
                ScannerResult result = new ScannerResult(ScannerKey("commander", commander.Id), name, commander.Position)
                {
                    NotVisible = !tile.IsVisible,
                    Relationship = ScannerRelationship(relationship),
                    StableReference = commander.Id,
                    EntityCategory = AdventureEntityCategory.Wielder
                };

                snapshot.Add(ScannerCategoryKeys.Wielders, ScannerSubcategoryKeys.All, result.Clone());
                snapshot.Add(ScannerCategoryKeys.Wielders, ScannerRelationshipKey(relationship), result.Clone());
            }
        }

        private void AddStructuralScannerResults(ScannerSnapshot snapshot, int localTeamId, Dictionary<Vector2Int, AdventureMapTile> tileCache, params MapEntityCategory[] categories)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!IsCategory(entity, categories))
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result == null)
                {
                    return;
                }

                string relationship = ScannerRelationshipKey(GetMapEntityRelationship(entity, localTeamId));
                AddStructuralMapEntityResult(snapshot, entity, relationship, result);
            });
        }

        private void AddTroopSourceScannerResults(ScannerSnapshot snapshot, int localTeamId, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!entity.HasComponent<IRecruitmentPoolComponent>() && !entity.HasComponent<ITroopDwellingComponent>())
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddTroopSourceResult(snapshot, entity, ScannerRelationshipKey(GetMapEntityRelationship(entity, localTeamId)), result);
                }
            });
        }

        private void AddPickupScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!IsScannerPickupEntity(entity))
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddPickupResult(snapshot, entity, result);
                }
            });
        }

        private void AddResourceGeneratorScannerResults(ScannerSnapshot snapshot, int localTeamId, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (entity.Category != MapEntityCategory.ResourceGenerator)
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddResourceGeneratorResult(snapshot, ScannerRelationshipKey(GetMapEntityRelationship(entity, localTeamId)), result);
                }
            });
        }

        private void AddBeaconScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!IsBeaconOfPowerEntity(entity))
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddSpecialSiteResult(snapshot, ScannerSubcategoryKeys.Beacons, result);
                }
            });
        }

        /// <summary>
        /// Merchants are picked by category rather than by a component, because
        /// what makes one worth visiting is that it trades, and the trading
        /// component is not one the scanner otherwise knows about. Without this
        /// the scanner cannot reach them at all: Merchant is in none of the
        /// other contributions' categories and carries none of their
        /// components.
        /// </summary>
        private void AddMerchantScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (entity.Category != MapEntityCategory.Merchant)
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddSpecialSiteResult(snapshot, ScannerSubcategoryKeys.Merchants, result);
                }
            });
        }

        private void AddArtifactMarketScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!entity.HasComponent<IArtifactMarketComponent>())
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddSpecialSiteResult(snapshot, ScannerSubcategoryKeys.ArtifactMarkets, result);
                }
            });
        }

        private void AddObjectiveScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (entity.Category != MapEntityCategory.Objective && entity.Category != MapEntityCategory.Story)
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddSpecialSiteResult(snapshot, ScannerSubcategoryKeys.Objectives, result);
                }
            });
        }

        private void AddTeleportScannerResults(ScannerSnapshot snapshot, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!entity.HasComponent<ITeleportComponent>() && !entity.HasComponent<ITownPortalComponent>() && !entity.HasComponent<ITownPortalBuildingComponent>())
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result != null)
                {
                    AddSpecialSiteResult(snapshot, ScannerSubcategoryKeys.Teleport, result);
                }
            });
        }

        private void AddRevealedScannerResults(ScannerSnapshot snapshot)
        {
            if (_revealedRegistry == null || snapshot == null)
            {
                return;
            }

            IReadOnlyList<AdventureMapRevealedEntry> entries = _revealedRegistry.Entries;
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            ScannerCategory category = snapshot.GetOrAddCategory(ScannerCategoryKeys.Exploration);
            ScannerSubcategory all = category.GetOrAddSubcategory(ScannerSubcategoryKeys.Revealed);
            for (int i = 0; i < entries.Count; i++)
            {
                AdventureMapRevealedEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Label))
                {
                    continue;
                }

                AdventureMapTile tile = IsWithinMap(entry.Position) ? GetTile(entry.Position) : null;
                Vector2Int position = entry.Position;
                if (entry.Kind == AdventureMapRevealedKind.MapEntity)
                {
                    IMapEntity entity = TryGetMapEntity(entry.StableReference);
                    if (entity == null)
                    {
                        RemoveStaleRevealedMapEntityEntry(entry);
                        continue;
                    }

                    if (!TryGetMapEntityIdentityTile(entity, null, out tile))
                    {
                        RemoveStaleRevealedMapEntityEntry(entry);
                        continue;
                    }

                    position = tile.Position;
                }

                all.Add(new ScannerResult(entry.Key, entry.Label, position)
                {
                    NotVisible = tile != null && !tile.IsVisible,
                    StableReference = entry.StableReference,
                    EntityCategory = tile != null ? tile.EntityCategory : AdventureEntityCategory.None
                });
            }
        }

        private void AddObstacleScannerResults(ScannerSnapshot snapshot, int localTeamId, Vector2Int origin, Dictionary<Vector2Int, AdventureMapTile> tileCache)
        {
            ForEachScannerEntity(tileCache, entity =>
            {
                if (!IsScannerObstacleEntity(entity))
                {
                    return;
                }

                AdventureMapTile tile;
                if (!TryGetMapEntityIdentityTile(entity, tileCache, out tile))
                {
                    return;
                }

                ScannerResult result = CreateMapEntityScannerResult(entity, tile);
                if (result == null)
                {
                    return;
                }

                snapshot.Add(ScannerCategoryKeys.Obstacles, ScannerSubcategoryKeys.All, result);
            });

            AddHostileZoneOfControlScannerResults(snapshot, localTeamId, origin);
        }

        private void AddHostileZoneOfControlScannerResults(ScannerSnapshot snapshot, int localTeamId, Vector2Int origin)
        {
            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders == null || localTeamId < 0)
            {
                return;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (!IsOverlayVisibleZoneOfControlSource(commander) || !IsHostileZoneOfControlSource(commander, localTeamId))
                {
                    continue;
                }

                List<Vector2Int> points = GetZoneOfControlPoints(localTeamId, commander);
                if (points.Count == 0)
                {
                    continue;
                }

                Vector2Int representative = ClosestPoint(points, origin);
                string name = FirstNonEmpty(AdventureMapEntityLabel.GetCommanderName(_facade, commander), ModText.Get(ModStrings.Spatial.Commander));
                // One item holding every hostile commander's reach, so the
                // item cycle spends one stop on zones of control however many
                // enemies are on the map. The owner and the size tell the
                // instances apart. No relationship: these are only ever built
                // for hostile commanders, so saying "enemy" adds nothing.
                ScannerResult result = new ScannerResult(
                    ScannerKey("zoc", commander.Id),
                    ScannerResultLabels.ZoneOfControl(points.Count, name),
                    representative)
                {
                    Kind = ScannerResultKind.CommanderZoneOfControl,
                    ItemKey = ScannerItemKeys.ZoneOfControl,
                    ItemLabel = ModText.Get(ModStrings.Scanner.ZoneOfControl),
                    InstanceLabel = ScannerResultLabels.ZoneOfControlInstance(points.Count, name),
                    StableReference = commander.Id
                };
                result.Points.AddRange(points);
                snapshot.Add(ScannerCategoryKeys.Obstacles, ScannerSubcategoryKeys.All, result);
            }
        }

        private void PopulateZoneOfControl(AdventureMapTile tile, int localTeamId)
        {
            if (tile == null || localTeamId < 0)
            {
                return;
            }

            Dictionary<Vector2Int, List<string>> zones = GetZoneOfControlNames(localTeamId);
            List<string> names;
            if (zones == null || !zones.TryGetValue(tile.Position, out names))
            {
                return;
            }

            for (int i = 0; i < names.Count; i++)
            {
                tile.ZoneOfControlNames.Add(names[i]);
            }
        }

        /// <summary>
        /// Which commanders' zones of control cover each tile, by the name the map speaks them
        /// under, built once per (local team, frame).
        ///
        /// This used to be asked per tile: every tile read walked every commander, and for each one
        /// enumerated <c>GetZoneOfControlPoints</c>, a lazy LINQ over the commander's cached points
        /// that calls <c>GetAtPoint</c> for each. A tile read is a lookup now. Every commander's
        /// zone, its name and the fog over it are read from the game on the miss, so a move, a
        /// death, a fog change and a hot reload all miss on their own; <c>Time.frameCount</c> closes
        /// the key because within one frame none of them can have happened.
        ///
        /// The commanders are walked in the facade's own order and a tile's names are added in that
        /// order and de-duplicated, which is what the per-tile walk produced.
        /// </summary>
        private Dictionary<Vector2Int, List<string>> GetZoneOfControlNames(int localTeamId)
        {
            int frame = Time.frameCount;
            if (_zoneOfControlNames != null && _zoneOfControlFrame == frame && _zoneOfControlTeamId == localTeamId)
            {
                return _zoneOfControlNames;
            }

            Dictionary<Vector2Int, List<string>> zones = new Dictionary<Vector2Int, List<string>>();
            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders != null)
            {
                foreach (ICommanderState commander in commanders)
                {
                    if (!IsOverlayVisibleZoneOfControlSource(commander))
                    {
                        continue;
                    }

                    List<Vector2Int> points = GetZoneOfControlPoints(localTeamId, commander);
                    if (points.Count == 0)
                    {
                        continue;
                    }

                    string name = FirstNonEmpty(AdventureMapEntityLabel.GetCommanderName(_facade, commander), ModText.Get(ModStrings.Spatial.Commander));
                    for (int i = 0; i < points.Count; i++)
                    {
                        List<string> names;
                        if (!zones.TryGetValue(points[i], out names))
                        {
                            names = new List<string>();
                            zones.Add(points[i], names);
                        }

                        if (!ContainsString(names, name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }

            _zoneOfControlNames = zones;
            _zoneOfControlFrame = frame;
            _zoneOfControlTeamId = localTeamId;
            return zones;
        }

        private Func<Vector2Int, bool> CreateCommanderZoneOfControlPointValidator(ScannerResult result)
        {
            if (!(result.StableReference is int commanderId))
            {
                return null;
            }

            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0)
            {
                return null;
            }

            ICommanderState commander = FindCommanderById(commanderId);
            if (!IsOverlayVisibleZoneOfControlSource(commander) || !IsHostileZoneOfControlSource(commander, localTeamId))
            {
                return null;
            }

            return position => IsWithinMap(position) && ZoneOfControlContains(localTeamId, commanderId, position);
        }

        private bool IsOverlayVisibleZoneOfControlSource(ICommanderState commander)
        {
            if (commander == null || commander.InternalState != CommanderInternalState.Default || !IsWithinMap(commander.Position))
            {
                return false;
            }

            return _fogManager.GetFog(commander.Position.x, commander.Position.y) == byte.MaxValue;
        }

        private bool IsHostileZoneOfControlSource(ICommanderState commander, int localTeamId)
        {
            return commander != null
                && _facade.Teams != null
                && !_facade.Teams.IsInPartnership(commander.TeamId, localTeamId);
        }

        private List<Vector2Int> GetZoneOfControlPoints(int localTeamId, ICommanderState commander)
        {
            List<Vector2Int> points = new List<Vector2Int>();
            IEnumerable<int2> nativePoints = _facade != null && _facade.Commanders != null && commander != null
                ? _facade.Commanders.GetZoneOfControlPoints(localTeamId, commander.Id)
                : null;
            if (nativePoints == null)
            {
                return points;
            }

            foreach (int2 point in nativePoints)
            {
                Vector2Int vector = new Vector2Int(point.x, point.y);
                if (IsWithinMap(vector) && (commander == null || vector != commander.Position))
                {
                    points.Add(vector);
                }
            }

            return points;
        }

        private bool ZoneOfControlContains(int localTeamId, int commanderId, Vector2Int position)
        {
            IEnumerable<int2> nativePoints = _facade != null && _facade.Commanders != null
                ? _facade.Commanders.GetZoneOfControlPoints(localTeamId, commanderId)
                : null;
            if (nativePoints == null)
            {
                return false;
            }

            foreach (int2 point in nativePoints)
            {
                if (point.x == position.x && point.y == position.y)
                {
                    ICommanderState commander = FindCommanderById(commanderId);
                    if (commander != null && position == commander.Position)
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        private ICommanderState FindCommanderById(int commanderId)
        {
            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null ? _facade.Commanders.All : null;
            if (commanders == null)
            {
                return null;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (commander != null && commander.Id == commanderId)
                {
                    return commander;
                }
            }

            return null;
        }

        private void ForEachScannerEntity(Dictionary<Vector2Int, AdventureMapTile> tileCache, Action<IMapEntity> action)
        {
            IEnumerable<IMapEntity> entities = _facade != null && _facade.MapEntities != null ? _facade.MapEntities.All : null;
            if (entities == null || action == null)
            {
                return;
            }

            foreach (IMapEntity entity in entities)
            {
                if (entity == null || !entity.IsEnabled || !IsWithinMap(entity.Position))
                {
                    continue;
                }

                action(entity);
            }
        }

        // No guard: MapEntities.Get is a dictionary TryGetValue (AbstractMapEntityManager.Get:36)
        // behind the two null checks this already makes, so there is nothing here that can throw.
        private IMapEntity TryGetMapEntity(int id)
        {
            return _facade != null && _facade.MapEntities != null ? _facade.MapEntities.Get(id) : null;
        }

        private bool TryGetMapEntityIdentityTile(
            IMapEntity entity,
            Dictionary<Vector2Int, AdventureMapTile> tileCache,
            out AdventureMapTile tile)
        {
            tile = null;
            Vector2Int identityTile;
            if (!AdventureMapVisibility.TryGetKnownMapEntityIdentityTile(
                _facade,
                _fogManager,
                entity,
                out identityTile))
            {
                return false;
            }

            AdventureMapTile candidate = GetScannerTile(tileCache, identityTile);
            if (candidate == null || candidate.MapEntity == null || candidate.MapEntity.Id != entity.Id)
            {
                return false;
            }

            tile = candidate;
            return true;
        }

        private ScannerResult CreateMapEntityScannerResult(IMapEntity entity, AdventureMapTile tile)
        {
            if (entity == null || tile == null || tile.MapEntity == null || tile.MapEntity.Id != entity.Id)
            {
                return null;
            }

            string name = FirstNonEmpty(tile.MapEntityName, GetMapEntityName(entity));
            return new ScannerResult(ScannerKey("entity", entity.Id), name, tile.Position)
            {
                NotVisible = !tile.IsVisible,
                Unvisited = IsScannerPickupEntity(entity) && IsUnvisited(entity),
                Relationship = ScannerRelationship(GetMapEntityRelationship(entity, GetLocalTeamId())),
                StableReference = entity.Id,
                EntityCategory = ClassifyMapEntity(entity)
            };
        }

        private static bool IsCategory(IMapEntity entity, MapEntityCategory[] categories)
        {
            if (entity == null || categories == null)
            {
                return false;
            }

            for (int i = 0; i < categories.Length; i++)
            {
                if (entity.Category == categories[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void AddStructuralMapEntityResult(ScannerSnapshot snapshot, IMapEntity entity, string relationship, ScannerResult result)
        {
            switch (entity.Category)
            {
                case MapEntityCategory.Town:
                case MapEntityCategory.Settlement:
                case MapEntityCategory.BuildSite:
                    snapshot.Add(ScannerCategoryKeys.SettlementsAndBuildSites, ScannerSubcategoryKeys.All, result.Clone());
                    snapshot.Add(ScannerCategoryKeys.SettlementsAndBuildSites, relationship, result.Clone());
                    break;
                case MapEntityCategory.Building:
                    snapshot.Add(ScannerCategoryKeys.Buildings, ScannerSubcategoryKeys.All, result.Clone());
                    snapshot.Add(ScannerCategoryKeys.Buildings, relationship, result.Clone());
                    break;
            }
        }

        /// <summary>
        /// Files a landmark under both the whole-category sweep and the one
        /// scope that says what kind of landmark it is.
        /// </summary>
        private void AddSpecialSiteResult(ScannerSnapshot snapshot, string subcategory, ScannerResult result)
        {
            snapshot.Add(ScannerCategoryKeys.SpecialSites, ScannerSubcategoryKeys.All, result.Clone());
            snapshot.Add(ScannerCategoryKeys.SpecialSites, subcategory, result.Clone());
        }

        private void AddResourceGeneratorResult(ScannerSnapshot snapshot, string relationship, ScannerResult result)
        {
            snapshot.Add(ScannerCategoryKeys.ResourceGenerators, ScannerSubcategoryKeys.All, result.Clone());
            snapshot.Add(ScannerCategoryKeys.ResourceGenerators, relationship, result.Clone());
        }

        private void AddTroopSourceResult(ScannerSnapshot snapshot, IMapEntity entity, string relationship, ScannerResult result)
        {
            if (entity.HasComponent<IRecruitmentPoolComponent>() || entity.HasComponent<ITroopDwellingComponent>())
            {
                snapshot.Add(ScannerCategoryKeys.TroopSources, ScannerSubcategoryKeys.All, result.Clone());
                snapshot.Add(ScannerCategoryKeys.TroopSources, relationship, result.Clone());
            }
        }

        private void AddPickupResult(ScannerSnapshot snapshot, IMapEntity entity, ScannerResult result)
        {
            MapEntityPreVisitDetails.PreVisitHint hint = GetPreVisitHint(entity);
            string subcategory = null;
            switch (hint)
            {
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfKnowledge:
                    subcategory = ScannerSubcategoryKeys.Knowledge;
                    break;
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfPower:
                    subcategory = ScannerSubcategoryKeys.Power;
                    break;
                case MapEntityPreVisitDetails.PreVisitHint.SourceOfRiches:
                    subcategory = ScannerSubcategoryKeys.Riches;
                    break;
            }

            if (subcategory == null)
            {
                return;
            }

            snapshot.Add(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.All, result.Clone());
            if (IsUnvisited(entity))
            {
                snapshot.Add(ScannerCategoryKeys.Pickups, ScannerSubcategoryKeys.Unvisited, result.Clone());
            }

            snapshot.Add(ScannerCategoryKeys.Pickups, subcategory, result.Clone());
        }

        private static bool IsScannerPickupEntity(IMapEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return entity.Category == MapEntityCategory.Pickup
                || entity.Category == MapEntityCategory.Artifact
                || entity.Category == MapEntityCategory.Experience
                || entity.Category == MapEntityCategory.Spell
                || entity.Category == MapEntityCategory.Effect;
        }

        private static bool IsBeaconOfPowerEntity(IMapEntity entity)
        {
            return entity != null
                && (entity.BlueprintId == ObjectiveBeaconBlueprintId
                    || entity.BlueprintId == FallenBeaconBlueprintId);
        }

        private static bool IsScannerObstacleEntity(IMapEntity entity)
        {
            return entity != null
                && (entity.Category == MapEntityCategory.Hostile
                    || entity.Category == MapEntityCategory.Obstacle
                    || entity.HasComponent<IMagicGateCommonComponent>()
                    || entity.HasComponent<IUnlockWithArtifactComponent>());
        }

        private void RemoveStaleRevealedMapEntityEntry(AdventureMapRevealedEntry entry)
        {
            if (entry != null && _revealedRegistry != null)
            {
                _revealedRegistry.Remove(entry.Key);
            }
        }

        private bool IsUnvisited(IMapEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            ICommanderState selectedCommander = _selectionHandler != null ? _selectionHandler.SelectedCommander : null;
            return selectedCommander == null || !entity.DidVisit(selectedCommander.Id);
        }

        private MapEntityPreVisitDetails.PreVisitHint GetPreVisitHint(IMapEntity entity)
        {
            try
            {
                ICommanderState selectedCommander = _selectionHandler != null ? _selectionHandler.SelectedCommander : null;
                IDetails details = entity.GetPreVisitDetails(
                    selectedCommander != null ? selectedCommander.Id : -1,
                    false,
                    ScoutingDetailLevel.VeryFar,
                    null,
                    selectedCommander != null && selectedCommander.IsAlive);
                MapEntityPreVisitDetails preVisit = details as MapEntityPreVisitDetails;
                return preVisit != null ? preVisit.Hint : MapEntityPreVisitDetails.PreVisitHint.None;
            }
            catch (Exception exception)
            {
                LogFailureOnce("reading a map entity's pre-visit hint", exception);
                return MapEntityPreVisitDetails.PreVisitHint.None;
            }
        }

        private AdventureMapTile GetScannerTile(Dictionary<Vector2Int, AdventureMapTile> tileCache, Vector2Int position)
        {
            if (tileCache == null)
            {
                return GetTile(position);
            }

            Vector2Int clamped = ClampToMap(position);
            AdventureMapTile tile;
            if (!tileCache.TryGetValue(clamped, out tile))
            {
                tile = GetTile(clamped);
                tileCache.Add(clamped, tile);
            }

            return tile;
        }

        private static Vector2Int ClosestPoint(List<Vector2Int> points, Vector2Int origin)
        {
            if (points == null || points.Count == 0)
            {
                return origin;
            }

            Vector2Int best = points[0];
            int bestDistance = DistanceSquared(origin, best);
            for (int i = 1; i < points.Count; i++)
            {
                int distance = DistanceSquared(origin, points[i]);
                if (distance < bestDistance)
                {
                    best = points[i];
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static int DistanceSquared(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            return x * x + y * y;
        }

        private static string ScannerKey(string prefix, int id)
        {
            return prefix + ":" + id;
        }

        private static string ScannerGroupKey(string category, string subcategory, string label, List<Vector2Int> points)
        {
            List<Vector2Int> sorted = points != null ? new List<Vector2Int>(points) : new List<Vector2Int>();
            sorted.Sort((left, right) =>
            {
                int xCompare = left.x.CompareTo(right.x);
                return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
            });

            List<string> parts = new List<string>
            {
                "group",
                category ?? string.Empty,
                subcategory ?? string.Empty,
                label ?? string.Empty,
                sorted.Count.ToString()
            };
            for (int i = 0; i < sorted.Count; i++)
            {
                parts.Add(sorted[i].x + "," + sorted[i].y);
            }

            return string.Join(":", parts.ToArray());
        }

        private static ScannerResultRelationship ScannerRelationship(string value)
        {
            switch (value)
            {
                case "friendly":
                    return ScannerResultRelationship.Friendly;
                case "enemy":
                    return ScannerResultRelationship.Enemy;
                case "neutral":
                    return ScannerResultRelationship.Neutral;
                default:
                    return ScannerResultRelationship.None;
            }
        }

        private static string ScannerRelationshipKey(string value)
        {
            switch (value)
            {
                case "friendly":
                    return ScannerSubcategoryKeys.Friendly;
                case "enemy":
                    return ScannerSubcategoryKeys.Enemy;
                default:
                    return ScannerSubcategoryKeys.Neutral;
            }
        }

        private static string FormatSpatialRelationship(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ModText.Get(ModStrings.Spatial.Neutral);
            }

            switch (value)
            {
                case "friendly":
                    return ModText.Get(ModStrings.Spatial.Friendly);
                case "enemy":
                    return ModText.Get(ModStrings.Spatial.Enemy);
                default:
                    return ModText.Get(ModStrings.Spatial.Neutral);
            }
        }

        /// <summary>
        /// Whether a result the normal pathfinder could not reach is blocked by something other
        /// than the ground, and the army to name for it. The terrain-only pathfinder is asked for
        /// the same journey: where it finds no route either, the ground itself is the answer, there
        /// is nothing to report and this answers false. Where it does find one, the result is
        /// blocked, and the route is walked for a tile a visible enemy or neutral army's zone of
        /// control covers - the army's own tile is one of its zone's points - which is the only
        /// thing named. Nothing built on the map is named: the terrain-only route runs through
        /// pickups and buildings a real route would walk around, so naming one is a guess.
        ///
        /// One extra path query, only for a result no route reaches, only when it is read.
        /// </summary>
        public bool TryGetPathBlocker(Vector2Int origin, ScannerResult result, out string armyName)
        {
            armyName = null;
            if (_facade == null || _facade.Level == null || !IsWithinMap(origin))
            {
                return false;
            }

            int teamId = GetLocalTeamId();
            if (teamId < 0)
            {
                return false;
            }

            List<Vector2Int> targets = GetScannerPathTargets(result, teamId);
            if (targets.Count == 0)
            {
                return false;
            }

            Vector2Int destination;
            PathNode[] route;
            if (!_facade.Level.TryGetShortestPathToPoints(
                    teamId,
                    origin,
                    targets,
                    out destination,
                    out route,
                    PathfinderCacheType.Static)
                || !route.GetIsValid())
            {
                return false;
            }

            armyName = FindRouteArmyName(route, teamId);
            return true;
        }

        /// <summary>
        /// The first army on a route that stands in the player's way: an enemy or neutral army
        /// whose zone of control covers one of the route's tiles. Neither end of the route is a
        /// candidate - the cursor is where the player stands and the last tile is the thing they
        /// are asking about, which would otherwise name itself. Null where no visible army lies
        /// anywhere on the route.
        /// </summary>
        private string FindRouteArmyName(PathNode[] route, int teamId)
        {
            Dictionary<Vector2Int, List<string>> zones = GetZoneOfControlNames(teamId);
            for (int i = 1; i < route.Length - 1; i++)
            {
                Vector2Int point = new Vector2Int(route[i].point.x, route[i].point.y);
                ICommanderState standing = GetCommanderAtVisiblePoint(point);
                if (standing != null && !IsHostileZoneOfControlSource(standing, teamId))
                {
                    // A partner's army is not in anyone's way; its own tile is the only point the
                    // team's own dynamic cache files under it.
                    continue;
                }

                List<string> names;
                if (zones.TryGetValue(point, out names) && names.Count > 0)
                {
                    return names[0];
                }

                AdventureMapTile tile = GetTile(point);
                if (tile != null && tile.Commander != null)
                {
                    // An army the zone map does not cover, which is how a hostile army under
                    // partial fog reads.
                    return tile.Commander.Name;
                }
            }

            return null;
        }

        /// <summary>
        /// What walking from the cursor to a scanner result costs, out of the one whole-map sweep
        /// the order asks for every result, or infinity where nothing reaches it.
        /// </summary>
        public float GetPathCost(Vector2Int origin, ScannerResult result)
        {
            return GetSweptCost(origin, result, terrainOnly: false);
        }

        /// <summary>
        /// What the same walk would cost over the terrain alone, out of a second whole-map sweep,
        /// or infinity where the ground itself refuses it. This is what a result an army or a
        /// building blocks is ordered by, so it sits where the length of the walk puts it.
        /// </summary>
        public float GetTerrainPathCost(Vector2Int origin, ScannerResult result)
        {
            return GetSweptCost(origin, result, terrainOnly: true);
        }

        private float GetSweptCost(Vector2Int origin, ScannerResult result, bool terrainOnly)
        {
            if (_facade == null || _facade.Level == null || !IsWithinMap(origin))
            {
                return float.PositiveInfinity;
            }

            int teamId = GetLocalTeamId();
            if (teamId < 0)
            {
                return float.PositiveInfinity;
            }

            Dictionary<Vector2Int, float> costs = terrainOnly
                ? GetTerrainCostsFrom(origin, teamId)
                : GetWalkableCostsFrom(origin, teamId);
            List<Vector2Int> targets = GetScannerPathTargets(result, teamId);
            float best = float.PositiveInfinity;
            for (int i = 0; i < targets.Count; i++)
            {
                float cost;
                if (TryGetStandingCost(costs, targets[i], out cost) && cost < best)
                {
                    best = cost;
                }
            }

            return best;
        }

        /// <summary>
        /// What it costs to get to a tile: the tile's own cost where a wielder can stand on it, and
        /// otherwise the cheapest neighbour, because that is where the wielder stops. Almost every
        /// entity's interaction point is the entity's own tile and a wielder can never stand there,
        /// so without this every entity would read as out of reach; the game reports the same
        /// number, the cost of the node before the destination
        /// (see <see cref="TryGetReachableMapEntityDistance"/>).
        /// </summary>
        private static bool TryGetStandingCost(Dictionary<Vector2Int, float> costs, Vector2Int target, out float cost)
        {
            if (costs.TryGetValue(target, out cost))
            {
                return true;
            }

            bool found = false;
            cost = float.PositiveInfinity;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    float neighbour;
                    if (costs.TryGetValue(new Vector2Int(target.x + x, target.y + y), out neighbour)
                        && neighbour < cost)
                    {
                        cost = neighbour;
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// The tiles a wielder would stop on to reach a result: an entity's interaction points, a
        /// partnered wielder's own tile and any other wielder's zone of control, and the result's
        /// own tile for everything else. The spoken route and the order both walk to these, so a
        /// result is ranked by the same journey the readout describes.
        /// </summary>
        private List<Vector2Int> GetScannerPathTargets(ScannerResult result, int teamId)
        {
            List<Vector2Int> targets = new List<Vector2Int>();
            if (result == null)
            {
                return targets;
            }

            if (result.EntityCategory == AdventureEntityCategory.Wielder && result.StableReference is int commanderId)
            {
                ICommanderState commander = FindCommanderById(commanderId);
                if (commander != null)
                {
                    if (_facade.Teams != null && _facade.Teams.IsInPartnership(commander.TeamId, teamId))
                    {
                        targets.Add(commander.Position);
                    }
                    else
                    {
                        targets.AddRange(GetZoneOfControlPoints(teamId, commander));
                    }

                    return targets;
                }
            }
            else if (result.Kind == ScannerResultKind.Point && result.StableReference is int entityId)
            {
                IMapEntity entity = TryGetMapEntity(entityId);
                IInteractableComponent component;
                if (entity != null
                    && entity.TryGetComponent<IInteractableComponent>(out component)
                    && component.CalculatedInteractionPoints != null
                    && component.CalculatedInteractionPoints.Length > 0)
                {
                    targets.AddRange(component.CalculatedInteractionPoints);
                    return targets;
                }
            }

            targets.Add(result.Position);
            return targets;
        }
    }
}
