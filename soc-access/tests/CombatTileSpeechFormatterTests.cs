using System.Collections.Generic;
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

        [TestMethod]
        public void InspectRangeIndicatorsFormatsAttackRangeWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Attack
            });

            Assert.AreEqual("Attack range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsDeadlyRangeWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Attack,
                CombatRangeIndicator.Deadly
            });

            Assert.AreEqual("Deadly range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsMovementRangeWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Movement
            });

            Assert.AreEqual("Movement range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsAttackAndMovementWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Attack,
                CombatRangeIndicator.Movement
            });

            Assert.AreEqual("attack and movement range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsZoneOfControlWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.ZoneOfControl,
                CombatRangeIndicator.Movement
            });

            Assert.AreEqual("Zone of control and movement range", text);
        }

        [TestMethod]
        public void InspectTileSpeechUsesContextIndicators()
        {
            Vector2Int point = new Vector2Int(1, 0);
            CombatInspectContext context = CombatInspectContext.ForStack(new Vector2Int(0, 0));
            context.Add(point, CombatRangeIndicator.Attack);
            CombatTile tile = new CombatTile(point);

            string text = new CombatTileSpeechFormatter(null, context).DescribeInfluence(tile);

            Assert.AreEqual("Attack range", text);
        }
    }
}
