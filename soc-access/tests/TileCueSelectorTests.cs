using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class TileCueSelectorTests
    {
        private static readonly HashSet<string> TerrainCues = new HashSet<string>
        {
            CueLibrary.TerrainRoad,
            CueLibrary.TerrainGround,
            CueLibrary.TerrainSand,
            CueLibrary.TerrainWater,
            CueLibrary.TerrainTrees,
            CueLibrary.TerrainImpassable
        };

        [TestMethod]
        public void NullTilesProduceNoCues()
        {
            Assert.AreEqual(0, TileCueSelector.ForAdventureTile(null).Count);
            Assert.AreEqual(0, TileCueSelector.ForCombatTile(null, isEnemyTroop: false, isActingTroop: false, isThreatened: false).Count);
            Assert.AreEqual(0, TileCueSelector.ForTroopPlacementTile(null, isOwnTroop: false).Count);
        }

        [TestMethod]
        public void EveryTerrainKindMapsToExactlyOneTerrainCue()
        {
            foreach (AdventureTerrainKind terrain in Enum.GetValues(typeof(AdventureTerrainKind)))
            {
                string cue = TileCueSelector.ForTerrain(terrain);
                Assert.IsTrue(TerrainCues.Contains(cue), terrain + " mapped to unexpected cue " + cue);

                IReadOnlyList<TileCue> cues = TileCueSelector.ForAdventureTile(ExploredTile(terrain));
                Assert.AreEqual(1, cues.Count, terrain + " produced more than one cue");
                Assert.AreEqual(cue, cues[0].Key);
            }
        }

        [TestMethod]
        public void TerrainFamiliesFollowThePlannedGrouping()
        {
            AssertTerrainCue(CueLibrary.TerrainRoad, AdventureTerrainKind.Road, AdventureTerrainKind.DirtRoad, AdventureTerrainKind.CobblestoneRoad, AdventureTerrainKind.Bridge);
            AssertTerrainCue(CueLibrary.TerrainGround, AdventureTerrainKind.Grass, AdventureTerrainKind.Dirt, AdventureTerrainKind.Deforestation, AdventureTerrainKind.Farmland, AdventureTerrainKind.Unknown);
            AssertTerrainCue(CueLibrary.TerrainSand, AdventureTerrainKind.Sand);
            AssertTerrainCue(CueLibrary.TerrainWater, AdventureTerrainKind.Water, AdventureTerrainKind.ShallowWater, AdventureTerrainKind.DeepWater, AdventureTerrainKind.WaterEdge);
            AssertTerrainCue(CueLibrary.TerrainTrees, AdventureTerrainKind.AridTrees, AdventureTerrainKind.TemperateTrees);
            AssertTerrainCue(CueLibrary.TerrainImpassable, AdventureTerrainKind.Mountain, AdventureTerrainKind.Wall);
        }

        [TestMethod]
        public void UnexploredTileSuppressesTerrainAndEntityCues()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.IsExplored = false;
            tile.Commander = Commander(ModStrings.Spatial.Enemy, isOwnedByLocalTeam: false);

            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainUnexplored }, ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void ImpassableFlagReplacesTerrainCue()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.IsImpassable = true;

            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainImpassable }, ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void BlockedFlagReplacesTerrainCue()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Sand);
            tile.IsBlocked = true;

            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainImpassable }, ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void ImpassableTileStillPlaysTheEntityOverlay()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Mountain);
            tile.MapEntityId = 42;
            tile.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Enemy);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainImpassable, CueLibrary.EntityEnemy },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void OccupiedImpassableTilePlaysItsTerrainFamilyInsteadOfTheThud()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.IsImpassable = true;
            tile.MapEntityId = 7;
            tile.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Neutral);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainGround },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void CommanderOnImpassableTilePlaysItsTerrainFamilyInsteadOfTheThud()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Road);
            tile.IsImpassable = true;
            tile.Commander = Commander(ModStrings.Spatial.Friendly, isOwnedByLocalTeam: true);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainRoad, CueLibrary.EntityFriendly },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void OwnedCommanderPlaysTheFriendlyOverlay()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.Commander = Commander(ModStrings.Spatial.Friendly, isOwnedByLocalTeam: true);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainGround, CueLibrary.EntityFriendly },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void AlliedCommanderPlaysTheFriendlyOverlay()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Road);
            tile.Commander = Commander(ModStrings.Spatial.Friendly, isOwnedByLocalTeam: false);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainRoad, CueLibrary.EntityFriendly },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void EnemyCommanderPlaysTheEnemyOverlay()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Road);
            tile.Commander = Commander(ModStrings.Spatial.Enemy, isOwnedByLocalTeam: false);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainRoad, CueLibrary.EntityEnemy },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void NeutralCommanderPlaysNoOverlay()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Road);
            tile.Commander = Commander(ModStrings.Spatial.Neutral, isOwnedByLocalTeam: false);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainRoad },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void ACategorisedEntityReplacesTheTerrainCue()
        {
            // A treasure trove on grass is a pickup, and the pickup voice is the whole
            // acknowledgment: the ground under it would only add noise.
            AdventureMapTile trove = ExploredTile(AdventureTerrainKind.Grass);
            trove.MapEntityId = 1;
            trove.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Neutral);
            trove.EntityCategory = AdventureEntityCategory.Pickup;

            CollectionAssert.AreEqual(new[] { CueLibrary.SweepPickup }, ToArray(TileCueSelector.ForAdventureTile(trove)));
        }

        [TestMethod]
        public void ACategorisedEntityKeepsItsAffiliationMarkerBehindTheCategoryVoice()
        {
            AdventureMapTile generator = ExploredTile(AdventureTerrainKind.Sand);
            generator.MapEntityId = 2;
            generator.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Enemy);
            generator.EntityCategory = AdventureEntityCategory.ResourceDeposit;

            IReadOnlyList<TileCue> cues = TileCueSelector.ForAdventureTile(generator);

            CollectionAssert.AreEqual(new[] { CueLibrary.SweepResource, CueLibrary.EntityEnemy }, ToArray(cues));
            Assert.IsFalse(cues[0].FollowsPrevious);
            Assert.IsTrue(cues[1].FollowsPrevious);
        }

        [TestMethod]
        public void AnOwnCommanderSoundsAsAFriendlyWielder()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Road);
            tile.Commander = Commander(ModStrings.Spatial.Friendly, isOwnedByLocalTeam: true);
            tile.EntityCategory = AdventureEntityCategory.Wielder;

            CollectionAssert.AreEqual(
                new[] { CueLibrary.SweepWielder, CueLibrary.EntityFriendly },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void ACategorisedEntityOnImpassableGroundStillPlaysOnlyItsGesture()
        {
            AdventureMapTile settlement = ExploredTile(AdventureTerrainKind.Mountain);
            settlement.IsImpassable = true;
            settlement.MapEntityId = 3;
            settlement.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Neutral);
            settlement.EntityCategory = AdventureEntityCategory.Settlement;

            CollectionAssert.AreEqual(
                new[] { CueLibrary.SweepSettlement },
                ToArray(TileCueSelector.ForAdventureTile(settlement)));
        }

        [TestMethod]
        public void AnUncategorisedOccupantStillPlaysTerrainAndItsMarker()
        {
            AdventureMapTile obstacle = ExploredTile(AdventureTerrainKind.Grass);
            obstacle.MapEntityId = 4;
            obstacle.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Enemy);

            IReadOnlyList<TileCue> cues = TileCueSelector.ForAdventureTile(obstacle);

            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainGround, CueLibrary.EntityEnemy }, ToArray(cues));
            Assert.IsFalse(cues[1].FollowsPrevious);
        }

        [TestMethod]
        public void UnexploredStillSuppressesTheCategoryVoice()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.IsExplored = false;
            tile.EntityCategory = AdventureEntityCategory.Pickup;

            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainUnexplored }, ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void NeutralAffiliationHasNoCue()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.MapEntityId = 1;
            tile.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Neutral);

            Assert.IsNull(TileCueSelector.AffiliationCueKey(tile));
        }

        [TestMethod]
        public void MapEntityRelationshipSelectsTheOverlayCue()
        {
            AssertMapEntityOverlay(ModStrings.Spatial.Friendly, CueLibrary.EntityFriendly);
            AssertMapEntityOverlay(ModStrings.Spatial.Enemy, CueLibrary.EntityEnemy);
            AssertMapEntityOverlay(ModStrings.Spatial.Neutral, null);
        }

        [TestMethod]
        public void CommanderTakesPrecedenceOverTheMapEntity()
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.Commander = Commander(ModStrings.Spatial.Enemy, isOwnedByLocalTeam: false);
            tile.MapEntityId = 7;
            tile.MapEntityRelationship = ModText.Get(ModStrings.Spatial.Friendly);

            CollectionAssert.AreEqual(
                new[] { CueLibrary.TerrainGround, CueLibrary.EntityEnemy },
                ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        [TestMethod]
        public void CombatTileWithAlliedTroopPlaysTheAllyCue()
        {
            CombatTile tile = new CombatTile(new Vector2Int(3, 4)) { TroopId = 11 };

            CollectionAssert.AreEqual(
                new[] { CueLibrary.EntityFriendly },
                ToArray(TileCueSelector.ForCombatTile(tile, isEnemyTroop: false, isActingTroop: false, isThreatened: false)));
        }

        [TestMethod]
        public void CombatTileWithEnemyTroopPlaysTheEnemyCue()
        {
            CombatTile tile = new CombatTile(new Vector2Int(3, 4)) { TroopId = 11 };

            CollectionAssert.AreEqual(
                new[] { CueLibrary.EntityEnemy },
                ToArray(TileCueSelector.ForCombatTile(tile, isEnemyTroop: true, isActingTroop: false, isThreatened: false)));
        }

        [TestMethod]
        public void CombatTileWithActingTroopAppendsTheActiveCue()
        {
            CombatTile tile = new CombatTile(new Vector2Int(3, 4)) { TroopId = 11 };

            CollectionAssert.AreEqual(
                new[] { CueLibrary.EntityEnemy, CueLibrary.HexActive },
                ToArray(TileCueSelector.ForCombatTile(tile, isEnemyTroop: true, isActingTroop: true, isThreatened: false)));
        }

        [TestMethod]
        public void CombatObstacleTilesPlayTheObstacleCue()
        {
            CombatTile impassable = new CombatTile(new Vector2Int(1, 1)) { IsImpassable = true };
            CombatTile blocked = new CombatTile(new Vector2Int(1, 2)) { IsBlocked = true };
            CombatTile withEntity = new CombatTile(new Vector2Int(1, 3)) { EntityId = 5 };

            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainImpassable }, ToArray(TileCueSelector.ForCombatTile(impassable, false, false, false)));
            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainImpassable }, ToArray(TileCueSelector.ForCombatTile(blocked, false, false, false)));
            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainImpassable }, ToArray(TileCueSelector.ForCombatTile(withEntity, false, false, false)));
        }

        [TestMethod]
        public void EmptyCombatTilePlaysTheEmptyCue()
        {
            CombatTile tile = new CombatTile(new Vector2Int(2, 2));

            CollectionAssert.AreEqual(
                new[] { CueLibrary.HexEmpty },
                ToArray(TileCueSelector.ForCombatTile(tile, isEnemyTroop: false, isActingTroop: false, isThreatened: false)));
        }

        [TestMethod]
        public void EmptyElevatedCombatTilePlaysTheElevationCueAlone()
        {
            CombatTile tile = new CombatTile(new Vector2Int(2, 2)) { Elevation = 2 };

            IReadOnlyList<TileCue> cues = TileCueSelector.ForCombatTile(tile, isEnemyTroop: false, isActingTroop: false, isThreatened: false);

            CollectionAssert.AreEqual(new[] { CueLibrary.HexElevation2 }, ToArray(cues));
            CollectionAssert.AreEqual(new[] { 0f }, ToSemitones(cues));
        }

        [TestMethod]
        public void OccupiedElevatedCombatTileStacksTheElevationCueUnderTheTroop()
        {
            CombatTile tile = new CombatTile(new Vector2Int(3, 4)) { TroopId = 11, Elevation = 1 };

            IReadOnlyList<TileCue> cues = TileCueSelector.ForCombatTile(tile, isEnemyTroop: true, isActingTroop: false, isThreatened: false);

            CollectionAssert.AreEqual(new[] { CueLibrary.HexElevation1, CueLibrary.EntityEnemy }, ToArray(cues));
            CollectionAssert.AreEqual(new[] { 0f, 0f }, ToSemitones(cues));
        }

        [TestMethod]
        public void ObstacleOnElevatedCombatTileStacksTheElevationCue()
        {
            CombatTile tile = new CombatTile(new Vector2Int(1, 1)) { IsImpassable = true, Elevation = 3 };

            IReadOnlyList<TileCue> cues = TileCueSelector.ForCombatTile(tile, isEnemyTroop: false, isActingTroop: false, isThreatened: false);

            CollectionAssert.AreEqual(new[] { CueLibrary.HexElevation3, CueLibrary.TerrainImpassable }, ToArray(cues));
            CollectionAssert.AreEqual(new[] { 0f, 0f }, ToSemitones(cues));
        }

        [TestMethod]
        public void OccupantCueFollowsTheElevationCueOnlyWhenElevated()
        {
            CombatTile elevated = new CombatTile(new Vector2Int(3, 4)) { TroopId = 11, Elevation = 2 };
            CombatTile flat = new CombatTile(new Vector2Int(3, 5)) { TroopId = 12 };

            IReadOnlyList<TileCue> elevatedCues = TileCueSelector.ForCombatTile(elevated, isEnemyTroop: false, isActingTroop: true, isThreatened: false);
            Assert.IsFalse(elevatedCues[0].FollowsPrevious);
            Assert.IsTrue(elevatedCues[1].FollowsPrevious, "occupant cue must wait for the elevation tick");
            Assert.IsFalse(elevatedCues[2].FollowsPrevious, "active cue stays aligned with the occupant cue");

            IReadOnlyList<TileCue> flatCues = TileCueSelector.ForCombatTile(flat, isEnemyTroop: false, isActingTroop: false, isThreatened: false);
            Assert.IsFalse(flatCues[0].FollowsPrevious);
        }

        [TestMethod]
        public void ThreatenedEmptyHexWarnsBeforeTheTileCue()
        {
            CombatTile tile = new CombatTile(new Vector2Int(4, 4));

            IReadOnlyList<TileCue> cues = TileCueSelector.ForCombatTile(
                tile,
                isEnemyTroop: false,
                isActingTroop: false,
                isThreatened: true);

            CollectionAssert.AreEqual(new[] { CueLibrary.HexDanger, CueLibrary.HexEmpty }, ToArray(cues));
            Assert.IsFalse(cues[0].FollowsPrevious);
            Assert.IsTrue(cues[1].FollowsPrevious, "the tile cue must wait for the warning");
        }

        [TestMethod]
        public void ThreatenedElevatedAndObstacleHexesKeepTheirOwnSerialization()
        {
            CombatTile elevated = new CombatTile(new Vector2Int(4, 5)) { Elevation = 2 };
            CombatTile obstacle = new CombatTile(new Vector2Int(4, 6)) { IsImpassable = true, Elevation = 1 };

            IReadOnlyList<TileCue> elevatedCues = TileCueSelector.ForCombatTile(
                elevated,
                isEnemyTroop: false,
                isActingTroop: false,
                isThreatened: true);
            CollectionAssert.AreEqual(new[] { CueLibrary.HexDanger, CueLibrary.HexElevation2 }, ToArray(elevatedCues));
            Assert.IsTrue(elevatedCues[1].FollowsPrevious);

            IReadOnlyList<TileCue> obstacleCues = TileCueSelector.ForCombatTile(
                obstacle,
                isEnemyTroop: false,
                isActingTroop: false,
                isThreatened: true);
            CollectionAssert.AreEqual(
                new[] { CueLibrary.HexDanger, CueLibrary.HexElevation1, CueLibrary.TerrainImpassable },
                ToArray(obstacleCues));
            Assert.IsTrue(obstacleCues[1].FollowsPrevious, "elevation waits for the warning");
            Assert.IsTrue(obstacleCues[2].FollowsPrevious, "the obstacle still waits for the elevation tick");
        }

        [TestMethod]
        public void OccupiedHexesNeverWarnEvenWhenTheCallerSaysThreatened()
        {
            CombatTile tile = new CombatTile(new Vector2Int(5, 5)) { TroopId = 9, Elevation = 1 };

            IReadOnlyList<TileCue> threatened = TileCueSelector.ForCombatTile(
                tile,
                isEnemyTroop: true,
                isActingTroop: false,
                isThreatened: true);

            CollectionAssert.AreEqual(
                ToArray(TileCueSelector.ForCombatTile(tile, isEnemyTroop: true, isActingTroop: false, isThreatened: false)),
                ToArray(threatened));
        }

        [TestMethod]
        public void ComputeDelaySerializesFollowersAndCarriesTheDelayForward()
        {
            TileCue[] cues =
            {
                new TileCue(CueLibrary.HexElevation2, 0f),
                new TileCue(CueLibrary.EntityFriendly, 0f, followsPrevious: true),
                new TileCue(CueLibrary.HexActive, 0f)
            };

            float[] delays = CueLibrary.ComputeDelaySeconds(cues, key => key == CueLibrary.HexElevation2 ? 0.05f : 0.1f);

            Assert.AreEqual(0f, delays[0]);
            Assert.AreEqual(0.05f + CueLibrary.StackGapSeconds, delays[1], 0.0001f);
            Assert.AreEqual(delays[1], delays[2], "unmarked cue keeps the accumulated delay");
        }

        [TestMethod]
        public void ComputeDelayIgnoresSilentPredecessors()
        {
            TileCue[] cues =
            {
                new TileCue(CueLibrary.HexElevation2, 0f),
                new TileCue(CueLibrary.EntityFriendly, 0f, followsPrevious: true)
            };

            float[] delays = CueLibrary.ComputeDelaySeconds(cues, key => 0f);

            Assert.AreEqual(0f, delays[0]);
            Assert.AreEqual(0f, delays[1], "a disabled elevation cue must not delay the occupant cue");
        }

        [TestMethod]
        public void ElevationClampsAtLevelThree()
        {
            Assert.IsNull(TileCueSelector.ElevationCueKey(0));
            Assert.AreEqual(CueLibrary.HexElevation1, TileCueSelector.ElevationCueKey(1));
            Assert.AreEqual(CueLibrary.HexElevation3, TileCueSelector.ElevationCueKey(3));
            Assert.AreEqual(CueLibrary.HexElevation3, TileCueSelector.ElevationCueKey(7));
        }

        [TestMethod]
        public void ElevatedTroopPlacementTilesCarryTheElevationCue()
        {
            TroopPlacementTile occupied = new TroopPlacementTile(new Vector2Int(2, 4)) { TroopId = 3, Elevation = 2 };
            TroopPlacementTile empty = new TroopPlacementTile(new Vector2Int(2, 5)) { Elevation = 1 };

            IReadOnlyList<TileCue> occupiedCues = TileCueSelector.ForTroopPlacementTile(occupied, isOwnTroop: true);
            CollectionAssert.AreEqual(new[] { CueLibrary.HexElevation2, CueLibrary.EntityFriendly }, ToArray(occupiedCues));
            CollectionAssert.AreEqual(new[] { 0f, 0f }, ToSemitones(occupiedCues));

            IReadOnlyList<TileCue> emptyCues = TileCueSelector.ForTroopPlacementTile(empty, isOwnTroop: false);
            CollectionAssert.AreEqual(new[] { CueLibrary.HexElevation1 }, ToArray(emptyCues));
            CollectionAssert.AreEqual(new[] { 0f }, ToSemitones(emptyCues));
        }

        [TestMethod]
        public void TroopPlacementTroopTilesPlayTheOccupantCue()
        {
            TroopPlacementTile own = new TroopPlacementTile(new Vector2Int(1, 1)) { TroopId = 3 };
            TroopPlacementTile enemy = new TroopPlacementTile(new Vector2Int(1, 2)) { TroopId = 4 };

            CollectionAssert.AreEqual(new[] { CueLibrary.EntityFriendly }, ToArray(TileCueSelector.ForTroopPlacementTile(own, isOwnTroop: true)));
            CollectionAssert.AreEqual(new[] { CueLibrary.EntityEnemy }, ToArray(TileCueSelector.ForTroopPlacementTile(enemy, isOwnTroop: false)));
        }

        [TestMethod]
        public void TroopPlacementObstacleAndEmptyTiles()
        {
            TroopPlacementTile impassable = new TroopPlacementTile(new Vector2Int(2, 1)) { IsImpassable = true };
            TroopPlacementTile withEntity = new TroopPlacementTile(new Vector2Int(2, 2)) { EntityId = 9 };
            TroopPlacementTile empty = new TroopPlacementTile(new Vector2Int(2, 3));

            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainImpassable }, ToArray(TileCueSelector.ForTroopPlacementTile(impassable, false)));
            CollectionAssert.AreEqual(new[] { CueLibrary.TerrainImpassable }, ToArray(TileCueSelector.ForTroopPlacementTile(withEntity, false)));
            CollectionAssert.AreEqual(new[] { CueLibrary.HexEmpty }, ToArray(TileCueSelector.ForTroopPlacementTile(empty, false)));
        }

        /// <summary>A null expected cue means the affiliation is not marked at all.</summary>
        private static void AssertMapEntityOverlay(ModString relationship, string expectedCue)
        {
            AdventureMapTile tile = ExploredTile(AdventureTerrainKind.Grass);
            tile.MapEntityId = 1;
            tile.MapEntityRelationship = ModText.Get(relationship);

            string[] expected = expectedCue == null
                ? new[] { CueLibrary.TerrainGround }
                : new[] { CueLibrary.TerrainGround, expectedCue };
            CollectionAssert.AreEqual(expected, ToArray(TileCueSelector.ForAdventureTile(tile)));
        }

        private static void AssertTerrainCue(string expectedCue, params AdventureTerrainKind[] terrains)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                Assert.AreEqual(expectedCue, TileCueSelector.ForTerrain(terrains[i]), terrains[i].ToString());
            }
        }

        private static AdventureMapTile ExploredTile(AdventureTerrainKind terrain)
        {
            return new AdventureMapTile(new Vector2Int(5, 6))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = terrain
            };
        }

        private static AdventureMapTile.CommanderInfo Commander(ModString relationship, bool isOwnedByLocalTeam)
        {
            return new AdventureMapTile.CommanderInfo
            {
                Relationship = ModText.Get(relationship),
                IsOwnedByLocalTeam = isOwnedByLocalTeam
            };
        }

        private static string[] ToArray(IReadOnlyList<TileCue> cues)
        {
            string[] result = new string[cues.Count];
            for (int i = 0; i < cues.Count; i++)
            {
                result[i] = cues[i].Key;
            }

            return result;
        }

        private static float[] ToSemitones(IReadOnlyList<TileCue> cues)
        {
            float[] result = new float[cues.Count];
            for (int i = 0; i < cues.Count; i++)
            {
                result[i] = cues[i].Semitones;
            }

            return result;
        }
    }
}
