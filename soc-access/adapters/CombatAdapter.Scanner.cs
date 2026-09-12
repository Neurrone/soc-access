using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // WHAT THE SCANNER FINDS ON THE BATTLEFIELD, moved out of CombatAdapter.cs unchanged: one
    // sweep of the board per category - the stacks on each side, the things that can be attacked and
    // the gates, the raised and impassable ground - and the check that a result the player is
    // walking is still a tile of this battle.

    public sealed partial class CombatAdapter
    {
        public ScannerSnapshot BuildScannerSnapshot(Vector2Int origin)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot(BattleScannerTaxonomy.Instance);
            if (_facade == null || _facade.Level == null)
            {
                return snapshot;
            }

            ScannerContribution.Run(ScannerCategoryKeys.Troops, () => AddCombatTroopScannerResults(snapshot));
            ScannerContribution.Run(ScannerCategoryKeys.Entities, () => AddCombatEntityScannerResults(snapshot));
            ScannerContribution.Run(ScannerCategoryKeys.Terrain, () => AddCombatTerrainScannerResults(snapshot));
            return snapshot;
        }

        private void AddCombatTroopScannerResults(ScannerSnapshot snapshot)
        {
            AddCombatTroopScannerResults(snapshot, friendly: true);
            AddCombatTroopScannerResults(snapshot, friendly: false);
        }

        private void AddCombatTroopScannerResults(ScannerSnapshot snapshot, bool friendly)
        {
            for (int y = 0; y < _facade.Level.Size.y; y++)
            {
                for (int x = 0; x < _facade.Level.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    CombatTile tile = GetTile(point);
                    if (tile == null)
                    {
                        continue;
                    }

                    if (tile.Troop != null && IsFriendlyTroop(tile.Troop) == friendly)
                    {
                        ScannerResult result = new ScannerResult(
                            ScannerTileKeys.For(friendly ? "troop:friendly" : "troop:enemy", point),
                            CombatTroopText.Stack(GetTroopFacts(tile.Troop)),
                            point)
                        {
                            // Keyed by troop type, not by the label: the label
                            // carries stack size and health, so grouping on it
                            // would give every stack an item of its own and
                            // split one apart the moment it took damage.
                            ItemKey = ScannerTroopItemKey(tile.Troop),
                            Relationship = friendly
                                ? ScannerResultRelationship.Friendly
                                : ScannerResultRelationship.Enemy,
                            Attackable = tile.IsTroopAttackable
                        };
                        snapshot.Add(ScannerCategoryKeys.Troops, ScannerSubcategoryKeys.All, result.Clone());
                        snapshot.Add(ScannerCategoryKeys.Troops, friendly ? ScannerSubcategoryKeys.Friendly : ScannerSubcategoryKeys.Enemy, result);
                    }
                }
            }
        }

        private void AddCombatEntityScannerResults(ScannerSnapshot snapshot)
        {
            for (int y = 0; y < _facade.Level.Size.y; y++)
            {
                for (int x = 0; x < _facade.Level.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    CombatTile tile = GetTile(point);
                    if (tile == null)
                    {
                        continue;
                    }

                    IMapEntity mapEntity = _facade.MapEntities != null ? _facade.MapEntities.GetAtIncludingNonBlockers(point) : null;
                    if (mapEntity != null && mapEntity.IsEnabled && mapEntity.IsVisibleInGame)
                    {
                        if (mapEntity.Category == MapEntityCategory.TownWallGate)
                        {
                            bool friendlyGate = IsFriendlyMapEntity(mapEntity);
                            ScannerResult result = new ScannerResult(
                                ScannerTileKeys.For(friendlyGate ? "gate:friendly" : "gate:enemy", point),
                                GetMapEntityName(mapEntity),
                                point)
                            {
                                Relationship = friendlyGate
                                    ? ScannerResultRelationship.Friendly
                                    : ScannerResultRelationship.Enemy,
                                Attackable = tile.IsEntityAttackable
                            };
                            snapshot.Add(ScannerCategoryKeys.Entities, ScannerSubcategoryKeys.All, result.Clone());
                            snapshot.Add(ScannerCategoryKeys.Entities, friendlyGate ? ScannerSubcategoryKeys.FriendlyGates : ScannerSubcategoryKeys.EnemyGates, result);
                        }
                        else if (tile.Entity != null)
                        {
                            ScannerResult result = new ScannerResult(
                                ScannerTileKeys.For("entity:attackable", point),
                                GetMapEntityName(tile.Entity),
                                point)
                            {
                                // The subcategory says these can be attacked in
                                // principle. This says the acting troop can
                                // reach one now, which is a different question
                                // and the one worth answering per result.
                                Attackable = tile.IsEntityAttackable
                            };
                            snapshot.Add(ScannerCategoryKeys.Entities, ScannerSubcategoryKeys.All, result.Clone());
                            snapshot.Add(ScannerCategoryKeys.Entities, ScannerSubcategoryKeys.Attackable, result);
                        }
                        else if (tile.MapEffects.Count > 0)
                        {
                            ScannerResult result = new ScannerResult(
                                ScannerTileKeys.For("entity:dangerous", point),
                                GetMapEntityName(mapEntity),
                                point);
                            snapshot.Add(ScannerCategoryKeys.Entities, ScannerSubcategoryKeys.All, result.Clone());
                            snapshot.Add(ScannerCategoryKeys.Entities, ScannerSubcategoryKeys.Dangerous, result);
                        }
                    }
                }
            }
        }

        private void AddCombatTerrainScannerResults(ScannerSnapshot snapshot)
        {
            for (int elevation = 1; elevation <= 3; elevation++)
            {
                for (int y = 0; y < _facade.Level.Size.y; y++)
                {
                    for (int x = 0; x < _facade.Level.Size.x; x++)
                    {
                        Vector2Int point = new Vector2Int(x, y);
                        CombatTile tile = GetTile(point);
                        if (tile == null)
                        {
                            continue;
                        }

                        if (tile.Elevation == elevation)
                        {
                            ScannerResult result = new ScannerResult(
                                ScannerTileKeys.For("terrain:elevated:" + elevation, point),
                                ModText.Get(ModStrings.Scanner.ElevatedGround, elevation),
                                point)
                            {
                                Kind = ScannerResultKind.TerrainPoint,
                                ItemKey = ScannerItemKeys.ElevatedGround + elevation
                            };
                            snapshot.Add(ScannerCategoryKeys.Terrain, ScannerSubcategoryKeys.All, result);
                        }
                    }
                }
            }

            for (int y = 0; y < _facade.Level.Size.y; y++)
            {
                for (int x = 0; x < _facade.Level.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    CombatTile tile = GetTile(point);
                    if (tile == null)
                    {
                        continue;
                    }

                    if (tile.IsImpassable)
                    {
                        ScannerResult result = new ScannerResult(
                            ScannerTileKeys.For("terrain:impassable", point),
                            ModText.Get(ModStrings.Scanner.ImpassableTerrain),
                            point)
                        {
                            Kind = ScannerResultKind.TerrainPoint,
                            ItemKey = ScannerItemKeys.ImpassableTerrain
                        };
                        snapshot.Add(ScannerCategoryKeys.Terrain, ScannerSubcategoryKeys.All, result);
                    }
                }
            }
        }

        /// <summary>
        /// Identifies the kind of troop rather than the stack, so every stack
        /// of the same unit collapses into one stop in the item cycle.
        /// </summary>
        private static string ScannerTroopItemKey(ICommonTroopState troop)
        {
            if (troop == null)
            {
                return null;
            }

            TroopReference reference = troop.Reference;
            return "troop:" + reference.FactionIndex + ":" + reference.UnitIndex + ":" + reference.UpgradeType;
        }

        public ScannerResultRefresh TryRefreshScannerResult(ScannerResult result, Vector2Int cursorHint)
        {
            return result != null && IsValidTile(result.Position)
                ? ScannerResultRefresh.Valid(result.Position)
                : ScannerResultRefresh.Invalid;
        }
    }
}
