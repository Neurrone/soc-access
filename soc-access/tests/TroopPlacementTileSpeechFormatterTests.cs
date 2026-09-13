using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>What a tile of the pre-battle deployment board reads as.</summary>
    [TestClass]
    public sealed class TroopPlacementTileSpeechFormatterTests : ModSettingsFixture
    {
        [TestMethod]
        public void DescribeTileReadsAnEmptyTileAsItsCoordinatesAlone()
        {
            Assert.AreEqual("1, 0", Describe(null, Tile(1, 0)));
        }

        [TestMethod]
        public void DescribeTileLeadsWithTheTroopStandingThere()
        {
            TroopPlacementTile tile = Tile(1, 0);
            tile.TroopLabel = "12 Bowmen";

            Assert.AreEqual("12 Bowmen, 1, 0", Describe(null, tile));
        }

        /// <summary>A spawn point on the player's own side is just a spawn point; the far side's is
        /// named as the enemy's, which is what tells a player where the battle starts from.</summary>
        [TestMethod]
        public void DescribeTileNamesASpawnPointBySideRelativeToThePlayer()
        {
            TroopPlacementTile own = Tile(1, 0);
            own.SpawnSide = BattleSide.Left_Attacker;
            TroopPlacementTile enemy = Tile(1, 0);
            enemy.SpawnSide = BattleSide.Right_Defender;

            Assert.AreEqual("spawn point, 1, 0", Describe(BattleSide.Left_Attacker, own));
            Assert.AreEqual("enemy spawn point, 1, 0", Describe(BattleSide.Left_Attacker, enemy));
        }

        /// <summary>Without a side of its own the board cannot tell whose spawn point this is, so it
        /// says the plain one rather than guessing.</summary>
        [TestMethod]
        public void DescribeTileSaysThePlainSpawnPointWhenThePlayerHasNoSide()
        {
            TroopPlacementTile tile = Tile(1, 0);
            tile.SpawnSide = BattleSide.Right_Defender;

            Assert.AreEqual("spawn point, 1, 0", Describe(null, tile));
        }

        /// <summary>A cell nothing can enter says so before the coordinates and says no height at
        /// all, however high it stands: no troop will ever be placed on it.</summary>
        [TestMethod]
        public void DescribeTileReadsImpassableWithoutItsHeightBeforeTheCoordinates()
        {
            TroopPlacementTile tile = Tile(1, 0);
            tile.IsImpassable = true;
            tile.Kind = BattlefieldCellKind.Impassable;
            tile.Elevation = 3;

            Assert.AreEqual("impassable, 1, 0", Describe(null, tile));
        }

        /// <summary>Raised ground is named by what it is where the ground itself has a name: a
        /// cliff nothing can climb, and a siege layout's wall, tower and stairs.</summary>
        [TestMethod]
        public void DescribeTileNamesACliffAndTheSiegeStructuresInsteadOfElevatedGround()
        {
            Assert.AreEqual("cliff, height 2, 1, 0", Describe(null, Ground(BattlefieldCellKind.Cliff, 2)));
            Assert.AreEqual("wall, height 2, 1, 0", Describe(null, Ground(BattlefieldCellKind.Wall, 2)));
            Assert.AreEqual("tower, height 3, 1, 0", Describe(null, Ground(BattlefieldCellKind.Tower, 3)));
            Assert.AreEqual("stairs, height 1, 1, 0", Describe(null, Ground(BattlefieldCellKind.Stairs, 1)));
        }

        /// <summary>Ground at the bottom of the board is the normal case and is not worth a word.
        /// </summary>
        [TestMethod]
        public void DescribeTileSaysNothingAboutTheGroundLevel()
        {
            Assert.AreEqual("1, 0", Describe(null, Tile(1, 0)));
        }

        /// <summary>A cell everything has to pass through says so, and it says it with the ground
        /// it stands on rather than instead of it: a choke cell is still flat or raised ground.
        /// </summary>
        [TestMethod]
        public void DescribeTileReadsAChokePointAfterTheGroundAndOnlyOnAChokeCell()
        {
            Assert.AreEqual("choke point, 1, 0", Describe(null, Choke(BattlefieldCellKind.Flat, 0)));
            Assert.AreEqual(
                "elevated ground, height 1, choke point, 1, 0",
                Describe(null, Choke(BattlefieldCellKind.Elevated, 1)));
            Assert.AreEqual("1, 0", Describe(null, Ground(BattlefieldCellKind.Flat, 0)));
        }

        /// <summary>Switched off in the settings, the part is silent and the rest of the tile reads
        /// as it did.</summary>
        [TestMethod]
        public void DescribeTileSaysNothingOfAChokePointWhenThePartIsSwitchedOff()
        {
            BindTemporaryConfig();
            ModSettings.SetAnnouncementElementEnabled(
                TroopDeploymentAnnouncementDefinitions.Tile,
                TroopDeploymentAnnouncementDefinitions.Tile.GetElement(
                    TroopDeploymentAnnouncementDefinitions.TileKeys.ChokePoint),
                false);

            Assert.AreEqual("1, 0", Describe(null, Choke(BattlefieldCellKind.Flat, 0)));
        }

        [TestMethod]
        public void DescribeTileFallsBackToTheScreenNameWithNoTile()
        {
            Assert.AreEqual("Troop placement", Describe(null, null));
        }

        [TestMethod]
        public void DescribeCoordinatesIsTheHexCoordinateAndNothingForNoTile()
        {
            Assert.AreEqual("0.5, 1", Formatter(null).DescribeCoordinates(Tile(0, 1)));
            Assert.AreEqual(string.Empty, Formatter(null).DescribeCoordinates(null));
        }

        private static string Describe(BattleSide? ownSide, TroopPlacementTile tile)
        {
            return Formatter(ownSide).DescribeTile(tile);
        }

        private static TroopPlacementTileSpeechFormatter Formatter(BattleSide? ownSide)
        {
            return new TroopPlacementTileSpeechFormatter(
                new TroopPlacementSnapshot(new Vector2Int(8, 6), ownSide, null));
        }

        private static TroopPlacementTile Choke(BattlefieldCellKind kind, byte elevation)
        {
            TroopPlacementTile tile = Ground(kind, elevation);
            tile.IsChokePoint = true;
            return tile;
        }

        private static TroopPlacementTile Ground(BattlefieldCellKind kind, byte elevation)
        {
            TroopPlacementTile tile = Tile(1, 0);
            tile.Kind = kind;
            tile.Elevation = elevation;
            return tile;
        }

        private static TroopPlacementTile Tile(int x, int y)
        {
            return new TroopPlacementTile(new Vector2Int(x, y));
        }
    }
}
