using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ReachableAdventureEntitySummaryFormatterTests
    {
        [TestMethod]
        public void FormatSpeaksNoneWhenNoEntitiesAreReachable()
        {
            string text = ReachableAdventureEntitySummaryFormatter.Format(new ReachableAdventureEntity[0]);

            Assert.AreEqual("Reachable: none", text);
        }

        [TestMethod]
        public void FormatGroupsDuplicateNamesAndOrdersByNearestDistance()
        {
            ReachableAdventureEntity[] entities =
            {
                Entity(1, "pile of wood", 3f, 3, 0),
                Entity(2, "pile of stone", 5f, 5, 0),
                Entity(3, "pile of wood", 7f, 7, 0)
            };

            string text = ReachableAdventureEntitySummaryFormatter.Format(entities);

            Assert.AreEqual("Reachable: 2 pile of wood, pile of stone", text);
        }

        [TestMethod]
        public void FormatSortsGroupsByNearestMemberNotLastMember()
        {
            ReachableAdventureEntity[] entities =
            {
                Entity(1, "gold mine", 4f, 4, 0),
                Entity(2, "pile of wood", 3f, 3, 0),
                Entity(3, "pile of wood", 9f, 9, 0),
                Entity(4, "pile of stone", 5f, 5, 0)
            };

            string text = ReachableAdventureEntitySummaryFormatter.Format(entities);

            Assert.AreEqual("Reachable: 2 pile of wood, gold mine, pile of stone", text);
        }

        private static ReachableAdventureEntity Entity(int id, string name, float distance, int x, int y)
        {
            return new ReachableAdventureEntity(id, name, new Vector2Int(x, y), distance);
        }
    }
}
