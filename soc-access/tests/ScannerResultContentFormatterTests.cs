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
        public void EveryScannerContentGroupSharesTheSameElementSet()
        {
            AnnouncementGroupDefinition[] groups =
            {
                AdventureMapAnnouncementDefinitions.ScannerContent,
                CombatAnnouncementDefinitions.ScannerContent,
                TroopDeploymentAnnouncementDefinitions.ScannerContent
            };

            for (int i = 0; i < groups.Length; i++)
            {
                Assert.AreEqual(
                    "name,owner,status",
                    groups[i].DefaultOrderCsv,
                    groups[i].Key);
                Assert.AreEqual(2, groups[i].Version, groups[i].Key);
            }
        }

        private static string Describe(ScannerResult result)
        {
            return ScannerResultContentFormatter.Describe(
                AdventureMapAnnouncementDefinitions.ScannerContent,
                result,
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
