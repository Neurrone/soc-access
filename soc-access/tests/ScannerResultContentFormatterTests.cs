using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerResultContentFormatterTests
    {
        [TestMethod]
        public void ReadsTheInstanceAloneWhenThereIsNoOwnerOrStatus()
        {
            ScannerResult result = new ScannerResult("terrain:road", "Road", new Vector2Int(3, 4))
            {
                InstanceLabel = "12 tiles"
            };

            Assert.AreEqual("12 tiles", Describe(result));
        }

        [TestMethod]
        public void FallsBackToTheItemNameWhenTheInstanceHasNoNameOfItsOwn()
        {
            ScannerResult result = new ScannerResult("entity:7", "Chest", new Vector2Int(3, 4))
            {
                Relationship = ScannerResultRelationship.Neutral
            };

            Assert.AreEqual("Chest, Neutral", Describe(result));
        }

        [TestMethod]
        public void ReadsTheOwnerAfterTheName()
        {
            ScannerResult result = new ScannerResult("entity:7", "Gold mine", new Vector2Int(3, 4))
            {
                InstanceLabel = "Gold mine",
                Relationship = ScannerResultRelationship.Enemy
            };

            Assert.AreEqual("Gold mine, Enemy", Describe(result));
        }

        [TestMethod]
        public void ReadsBothStatusesAfterTheOwner()
        {
            ScannerResult result = new ScannerResult("entity:7", "Ancient amber", new Vector2Int(3, 4))
            {
                InstanceLabel = "Ancient amber",
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
                InstanceLabel = "Impassable",
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
                InstanceLabel = "Gold mine",
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
                InstanceLabel = "Gold mine",
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

        /// <summary>
        /// Everything about the result itself comes before anything about the
        /// ground it stands on. In combat that starts with whether the acting
        /// troop can hit it, since a player sweeping the enemy line is asking
        /// that and nothing else, and hearing it first means not sitting
        /// through the name and health of everything out of reach.
        /// </summary>
        [TestMethod]
        public void EveryScannerContentGroupLeadsWithTheResultBeforeTheTile()
        {
            Assert.AreEqual(
                "name,owner,status,reachability_or_route_preview,movement_cost",
                AdventureMapAnnouncementDefinitions.ScannerContent.DefaultOrderCsv);
            Assert.AreEqual(
                "attackable,name,owner,status,reachable,elevation",
                CombatAnnouncementDefinitions.ScannerContent.DefaultOrderCsv);
            Assert.AreEqual(
                "name,owner,status,elevation",
                TroopDeploymentAnnouncementDefinitions.ScannerContent.DefaultOrderCsv);
        }

        [TestMethod]
        public void ReadsAttackableBeforeTheTroopItNames()
        {
            ScannerResult result = new ScannerResult("troop:enemy:2:3", "20 Militia", new Vector2Int(2, 3))
            {
                InstanceLabel = "20 Militia",
                Relationship = ScannerResultRelationship.Enemy,
                Attackable = true
            };

            string text = ScannerResultContentFormatter.Describe(
                CombatAnnouncementDefinitions.ScannerContent,
                result,
                null,
                DefaultOrder,
                (group, element) => element.DefaultEnabled,
                (group, element) => element.DefaultSuffix);

            Assert.AreEqual("Attackable, 20 Militia, Enemy", text);
        }

        /// <summary>
        /// Only combat has an acting troop to answer for, so the screens that
        /// do not declare the element drop it rather than showing a fact they
        /// cannot compute.
        /// </summary>
        [TestMethod]
        public void OmitsAttackableWhereTheGroupDoesNotDeclareIt()
        {
            ScannerResult result = new ScannerResult("entity:7", "Gold mine", new Vector2Int(3, 4))
            {
                InstanceLabel = "Gold mine",
                Attackable = true
            };

            Assert.AreEqual("Gold mine", Describe(result));
        }

        [TestMethod]
        public void ReadsTileFactsAfterTheThing()
        {
            ScannerResult result = new ScannerResult("troop:friendly:2:3", "20 Militia", new Vector2Int(2, 3))
            {
                InstanceLabel = "20 Militia",
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
            ScannerResult result = new ScannerResult("entity:7", "Gold mine", new Vector2Int(3, 4))
            {
                InstanceLabel = "Gold mine"
            };

            string text = Describe(
                result,
                new[] { new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.Reachable, "reachable") });

            Assert.AreEqual("Gold mine", text);
        }

        [TestMethod]
        public void DoesNotReadTheAdventureMovementCostByDefault()
        {
            AdventureMapTile tile = CreateReachableRoadTile();
            ScannerResult result = new ScannerResult("terrain:road:4:2", "Dirt road", tile.Position)
            {
                InstanceLabel = "Dirt road"
            };

            string text = Describe(result, AdventureScannerSpeechContext.BuildTileParts(tile));

            Assert.AreEqual("Dirt road, reachable", text);
        }

        [TestMethod]
        public void ReadsAnEnabledAdventureMovementCostAtTheEnd()
        {
            AdventureMapTile tile = CreateReachableRoadTile();
            ScannerResult result = new ScannerResult("terrain:road:4:2", "Dirt road", tile.Position)
            {
                InstanceLabel = "Dirt road"
            };

            string text = ScannerResultContentFormatter.Describe(
                AdventureMapAnnouncementDefinitions.ScannerContent,
                result,
                AdventureScannerSpeechContext.BuildTileParts(tile),
                DefaultOrder,
                (group, element) => element.Key == AdventureMapAnnouncementDefinitions.TileKeys.MovementCost
                    || element.DefaultEnabled,
                (group, element) => element.DefaultSuffix);

            Assert.AreEqual("Dirt road, reachable, Movement cost: 3", text);
        }

        [TestMethod]
        public void OmitsTheAdventureMovementCostForAnUnexploredTile()
        {
            AdventureMapTile tile = new AdventureMapTile(new Vector2Int(4, 2))
            {
                ReachableMovementCost = 3f
            };
            ScannerResult result = new ScannerResult("terrain:road:4:2", "Dirt road", tile.Position)
            {
                InstanceLabel = "Dirt road"
            };

            string text = ScannerResultContentFormatter.Describe(
                AdventureMapAnnouncementDefinitions.ScannerContent,
                result,
                AdventureScannerSpeechContext.BuildTileParts(tile),
                DefaultOrder,
                (group, element) => element.Key == AdventureMapAnnouncementDefinitions.TileKeys.MovementCost
                    || element.DefaultEnabled,
                (group, element) => element.DefaultSuffix);

            Assert.AreEqual("Dirt road", text);
        }

        private static AdventureMapTile CreateReachableRoadTile()
        {
            return new AdventureMapTile(new Vector2Int(4, 2))
            {
                IsExplored = true,
                IsVisible = true,
                IsReachable = true,
                ReachableMovementCost = 3f,
                Terrain = AdventureTerrainKind.DirtRoad
            };
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
