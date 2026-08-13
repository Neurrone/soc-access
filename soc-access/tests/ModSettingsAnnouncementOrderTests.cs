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

            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.Terrain, order[order.Count - 2]);
            Assert.AreEqual(AdventureMapAnnouncementDefinitions.TileKeys.MovementCost, order[order.Count - 1]);
        }
    }
}
