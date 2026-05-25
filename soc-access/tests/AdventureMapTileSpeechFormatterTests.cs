using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class AdventureMapTileSpeechFormatterTests
    {
        [TestMethod]
        public void DescribeTileReadsWallAsTerrainWithImpassableStatus()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(12, 9))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.Wall,
                IsImpassable = true
            };

            string text = new AdventureMapTileSpeechFormatter().DescribeTile(tile);

            Assert.AreEqual("Wall, impassable. 12, 9.", text);
        }

        [TestMethod]
        public void DescribeTileReadsDirtRoadAsTheOnlyTerrain()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.DirtRoad,
                IsReachable = true
            };

            string text = new AdventureMapTileSpeechFormatter().DescribeTile(tile);

            Assert.AreEqual("Dirt road, reachable. 4, 2.", text);
        }

        [TestMethod]
        public void DescribeTileReadsGroundTerrainWithoutEnvironmentLayer()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(7, 4))
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = AdventureTerrainKind.Grass
            };

            string text = new AdventureMapTileSpeechFormatter().DescribeTile(tile);

            Assert.AreEqual("Grass. 7, 4.", text);
        }
    }
}
