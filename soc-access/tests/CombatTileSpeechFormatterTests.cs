using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatTileSpeechFormatterTests
    {
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

        [TestMethod]
        public void ConfigurableAnnouncementComposerUsesSuffixBetweenRenderedPartsOnly()
        {
            AnnouncementGroupDefinition group = new AnnouncementGroupDefinition(
                "test",
                "Test",
                ModStrings.Screens.TileAnnouncements,
                new AnnouncementElementDefinition("first", ModStrings.Screens.AnnouncementReachable),
                new AnnouncementElementDefinition("second", ModStrings.Screens.AnnouncementCoordinates),
                new AnnouncementElementDefinition("third", ModStrings.Screens.AnnouncementInfluence));

            string text = ComposeWithDefaults(group, new[]
            {
                new AnnouncementPart("first", "one"),
                new AnnouncementPart("second", "two")
            });

            Assert.AreEqual("one, two", text);
        }

        [TestMethod]
        public void ConfigurableAnnouncementComposerHonorsDefaultSuffixOff()
        {
            AnnouncementGroupDefinition group = new AnnouncementGroupDefinition(
                "test_no_suffix",
                "Test",
                ModStrings.Screens.TileAnnouncements,
                new AnnouncementElementDefinition("first", ModStrings.Screens.AnnouncementReachable, defaultSuffix: false),
                new AnnouncementElementDefinition("second", ModStrings.Screens.AnnouncementCoordinates));

            string text = ComposeWithDefaults(group, new[]
            {
                new AnnouncementPart("first", "one"),
                new AnnouncementPart("second", "two")
            });

            Assert.AreEqual("one two", text);
        }

        private static string ComposeWithDefaults(AnnouncementGroupDefinition group, IEnumerable<AnnouncementPart> parts)
        {
            return ConfigurableAnnouncementComposer.Compose(
                group,
                parts,
                testGroup =>
                {
                    List<string> keys = new List<string>();
                    for (int i = 0; i < testGroup.Elements.Count; i++)
                    {
                        keys.Add(testGroup.Elements[i].Key);
                    }

                    return keys;
                },
                (testGroup, element) => element.DefaultEnabled,
                (testGroup, element) => element.DefaultSuffix);
        }
    }
}
