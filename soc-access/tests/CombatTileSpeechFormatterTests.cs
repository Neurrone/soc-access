using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatTileSpeechFormatterTests
    {
        [TestMethod]
        public void DescribeTileContextPlacesMapEffectsBeforeElevation()
        {
            CombatTile tile = new CombatTile(new Vector2Int(2, 3))
            {
                Elevation = 1
            };
            tile.MapEffects.Add("Acid Cloud");

            string text = new CombatTileSpeechFormatter(null, null, includeEnemyInfluence: false).DescribeTileContext(tile);

            Assert.AreEqual("Acid Cloud, elevated ground, height 1", text);
        }
    }
}
