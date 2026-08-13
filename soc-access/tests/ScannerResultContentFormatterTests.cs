using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerResultContentFormatterTests
    {
        [TestMethod]
        public void ReadsTheNameAloneWhenThereIsNoOwnerOrStatus()
        {
            ScannerResult result = new ScannerResult("terrain:road", "12 road tiles", new Vector2Int(3, 4));

            Assert.AreEqual("12 road tiles", Describe(result));
        }

        [TestMethod]
        public void ReadsTheOwnerAfterTheName()
        {
            ScannerResult result = new ScannerResult("entity:7", "Gold mine", new Vector2Int(3, 4))
            {
                Relationship = ScannerResultRelationship.Enemy
            };

            Assert.AreEqual("Gold mine, Enemy", Describe(result));
        }

        [TestMethod]
        public void ReadsBothStatusesAfterTheOwner()
        {
            ScannerResult result = new ScannerResult("entity:7", "Ancient amber", new Vector2Int(3, 4))
            {
                Relationship = ScannerResultRelationship.Neutral,
                Unvisited = true,
                NotVisible = true
            };

            Assert.AreEqual("Ancient amber, Neutral, Unvisited, Unseen", Describe(result));
        }

        [TestMethod]
        public void OmitsTheOwnerWhenTheThingHasNone()
        {
            ScannerResult result = new ScannerResult("terrain:impassable", "Impassable", new Vector2Int(3, 4))
            {
                NotVisible = true
            };

            Assert.AreEqual("Impassable, Unseen", Describe(result));
        }

        [TestMethod]
        public void DescribesNothingForAMissingResult()
        {
            Assert.AreEqual(string.Empty, Describe(null));
        }

        [TestMethod]
        public void HonoursADisabledElement()
        {
            ScannerResult result = new ScannerResult("entity:7", "Gold mine", new Vector2Int(3, 4))
            {
                Relationship = ScannerResultRelationship.Friendly
            };

            string text = ScannerResultContentFormatter.Describe(
                AdventureMapAnnouncementDefinitions.ScannerContent,
                result,
                null,
                DefaultOrder,
                (group, element) => element.Key != ScannerAnnouncementDefinitions.ContentKeys.Owner,
                (group, element) => element.DefaultSuffix);

            Assert.AreEqual("Gold mine", text);
        }

        [TestMethod]
        public void HonoursACustomOrder()
        {
            ScannerResult result = new ScannerResult("entity:7", "Gold mine", new Vector2Int(3, 4))
            {
                Relationship = ScannerResultRelationship.Friendly
            };

            string text = ScannerResultContentFormatter.Describe(
                AdventureMapAnnouncementDefinitions.ScannerContent,
                result,
                null,
                group => new[]
                {
                    ScannerAnnouncementDefinitions.ContentKeys.Owner,
                    ScannerAnnouncementDefinitions.ContentKeys.Name
                },
                (group, element) => element.DefaultEnabled,
                (group, element) => element.DefaultSuffix);

            Assert.AreEqual("Friendly, Gold mine", text);
        }

        [TestMethod]
        public void EveryScannerContentGroupLeadsWithTheThingItself()
        {
            Assert.AreEqual(
                "name,owner,status",
                AdventureMapAnnouncementDefinitions.ScannerContent.DefaultOrderCsv);
            Assert.AreEqual(
                "name,owner,status,reachable,elevation",
                CombatAnnouncementDefinitions.ScannerContent.DefaultOrderCsv);
            Assert.AreEqual(
                "name,owner,status,elevation",
                TroopDeploymentAnnouncementDefinitions.ScannerContent.DefaultOrderCsv);
        }

        [TestMethod]
        public void ReadsTileFactsAfterTheThing()
        {
            ScannerResult result = new ScannerResult("troop:friendly:2:3", "20 Militia", new Vector2Int(2, 3))
            {
                Relationship = ScannerResultRelationship.Friendly
            };

            string text = ScannerResultContentFormatter.Describe(
                CombatAnnouncementDefinitions.ScannerContent,
                result,
                new[]
                {
                    new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.Reachable, "reachable"),
                    new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.Elevation, "elevated ground, height 2")
                },
                DefaultOrder,
                (group, element) => element.DefaultEnabled,
                (group, element) => element.DefaultSuffix);

            Assert.AreEqual("20 Militia, Friendly, reachable, elevated ground, height 2", text);
        }

        [TestMethod]
        public void IgnoresTilePartsTheGroupDoesNotDeclare()
        {
            ScannerResult result = new ScannerResult("entity:7", "Gold mine", new Vector2Int(3, 4));

            string text = Describe(
                result,
                new[] { new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.Reachable, "reachable") });

            Assert.AreEqual("Gold mine", text);
        }

        private static string Describe(ScannerResult result)
        {
            return Describe(result, null);
        }

        private static string Describe(ScannerResult result, IEnumerable<AnnouncementPart> tileParts)
        {
            return ScannerResultContentFormatter.Describe(
                AdventureMapAnnouncementDefinitions.ScannerContent,
                result,
                tileParts,
                DefaultOrder,
                (group, element) => element.DefaultEnabled,
                (group, element) => element.DefaultSuffix);
        }

        private static IReadOnlyList<string> DefaultOrder(AnnouncementGroupDefinition group)
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < group.Elements.Count; i++)
            {
                keys.Add(group.Elements[i].Key);
            }

            return keys;
        }
    }
}
