using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>What a tile of the pre-battle deployment board reads as.</summary>
    [TestClass]
    public sealed class TroopPlacementTileSpeechFormatterTests
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

        [TestMethod]
        public void DescribeTileReadsImpassableAndElevationBeforeTheCoordinates()
        {
            TroopPlacementTile tile = Tile(1, 0);
            tile.IsImpassable = true;
            tile.Elevation = 2;

            Assert.AreEqual("impassable, elevated ground, height 2, 1, 0", Describe(null, tile));
        }

        /// <summary>Ground at the bottom of the board is the normal case and is not worth a word.
        /// </summary>
        [TestMethod]
        public void DescribeTileSaysNothingAboutTheGroundLevel()
        {
            Assert.AreEqual("1, 0", Describe(null, Tile(1, 0)));
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

        private static TroopPlacementTile Tile(int x, int y)
        {
            return new TroopPlacementTile(new Vector2Int(x, y));
        }
    }
}
