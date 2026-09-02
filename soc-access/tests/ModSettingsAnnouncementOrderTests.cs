using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ModSettingsAnnouncementOrderTests
    {
        [TestMethod]
        public void MergeAnnouncementOrderAppendsMovementCostAfterCustomizedAdventureTileOrder()
        {
            string savedOrder = string.Join(
                ",",
                new[]
                {
                    AdventureMapAnnouncementDefinitions.TileKeys.ExplorationState,
                    AdventureMapAnnouncementDefinitions.TileKeys.Wielder,
                    AdventureMapAnnouncementDefinitions.TileKeys.MapEntity,
                    AdventureMapAnnouncementDefinitions.TileKeys.InteractionPoint,
                    AdventureMapAnnouncementDefinitions.TileKeys.ReachabilityOrRoutePreview,
                    AdventureMapAnnouncementDefinitions.TileKeys.ZoneOfControl,
                    AdventureMapAnnouncementDefinitions.TileKeys.Coordinates,
                    AdventureMapAnnouncementDefinitions.TileKeys.Terrain
                });

            IReadOnlyList<string> order = ModSettings.MergeAnnouncementOrder(
                AdventureMapAnnouncementDefinitions.Tile,
                savedOrder);

            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.Terrain, order[order.Count - 3]);
            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.RoadDirections, order[order.Count - 2]);
            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.MovementCost, order[order.Count - 1]);
        }

        [TestMethod]
        public void MergeAnnouncementOrderPutsRoadDirectionsRightAfterWhereverTerrainWasMovedTo()
        {
            // Road directions were added after an order had already been saved, so they have to
            // land next to the terrain they describe rather than at the end of whatever the
            // player arranged.
            string savedOrder = string.Join(
                ",",
                new[]
                {
                    AdventureMapAnnouncementDefinitions.TileKeys.Terrain,
                    AdventureMapAnnouncementDefinitions.TileKeys.ExplorationState,
                    AdventureMapAnnouncementDefinitions.TileKeys.Wielder,
                    AdventureMapAnnouncementDefinitions.TileKeys.MapEntity,
                    AdventureMapAnnouncementDefinitions.TileKeys.InteractionPoint,
                    AdventureMapAnnouncementDefinitions.TileKeys.ReachabilityOrRoutePreview,
                    AdventureMapAnnouncementDefinitions.TileKeys.ZoneOfControl,
                    AdventureMapAnnouncementDefinitions.TileKeys.Coordinates
                });

            IReadOnlyList<string> order = ModSettings.MergeAnnouncementOrder(
                AdventureMapAnnouncementDefinitions.Tile,
                savedOrder);

            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.Terrain, order[0]);
            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.RoadDirections, order[1]);
        }

        [TestMethod]
        public void MergeAnnouncementOrderAppendsMovementCostAfterCustomizedAdventureScannerContentOrder()
        {
            string savedOrder = string.Join(
                ",",
                new[]
                {
                    AdventureMapAnnouncementDefinitions.TileKeys.ReachabilityOrRoutePreview,
                    ScannerAnnouncementDefinitions.ContentKeys.Name,
                    ScannerAnnouncementDefinitions.ContentKeys.Owner,
                    ScannerAnnouncementDefinitions.ContentKeys.Status
                });

            IReadOnlyList<string> order = ModSettings.MergeAnnouncementOrder(
                AdventureMapAnnouncementDefinitions.ScannerContent,
                savedOrder);

            Assert.AreEqual(ScannerAnnouncementDefinitions.ContentKeys.Status, order[order.Count - 2]);
            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.MovementCost, order[order.Count - 1]);
        }
    }
}
