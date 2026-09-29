using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatAttackPreviewTextTests
    {
        [TestMethod]
        public void ComposeReadsASingleTargetPreviewWithoutAName()
        {
            var lines = CombatAttackPreviewText.Compose(new[]
            {
                Preview("94–117", "9–11", Troop(1, 2, "Spawns", 177, new Vector2Int(2, 0)), onTarget: true)
            });

            CollectionAssert.AreEqual(new[] { "damage 94–117, kills 9–11." }, (System.Collections.ICollection)lines);
        }

        [TestMethod]
        public void ComposePutsTheTargetFirstAndNamesEveryOtherStack()
        {
            var lines = CombatAttackPreviewText.Compose(new[]
            {
                Preview("119–149", "1", Troop(2, 2, "Roots of the Mother", 5, new Vector2Int(8, 6)), onTarget: false),
                Preview("173–216", "6", Troop(3, 1, "Grenadier", 6, new Vector2Int(4, 2)), onTarget: false),
                Preview("94–117", "9–11", Troop(1, 2, "Spawns", 177, new Vector2Int(2, 0)), onTarget: true)
            });

            CollectionAssert.AreEqual(new[]
            {
                "damage 94–117, kills 9–11.",
                "5 Roots of the Mother at 8, 6: damage 119–149, kills 1.",
                "6 friendly Grenadier at 4, 2: damage 173–216, kills 6."
            }, (System.Collections.ICollection)lines);
        }

        [TestMethod]
        public void ComposeNamesEveryStackWhenNoTroopIsAimedAt()
        {
            var lines = CombatAttackPreviewText.Compose(new[]
            {
                Preview("119–149", "1", Troop(2, 2, "Roots of the Mother", 5, new Vector2Int(8, 6)), onTarget: false),
                Preview(null, "6", Troop(3, 1, "Grenadier", 6, new Vector2Int(4, 2)), onTarget: false)
            });

            CollectionAssert.AreEqual(new[]
            {
                "5 Roots of the Mother at 8, 6: damage 119–149, kills 1.",
                "6 friendly Grenadier at 4, 2: kills 6."
            }, (System.Collections.ICollection)lines);
        }

        private static CombatAttackPreviewFacts Preview(string damage, string kills, TroopRef troop, bool onTarget)
        {
            return new CombatAttackPreviewFacts(damage, kills, null, false, troop, onTarget);
        }

        private static TroopRef Troop(int id, int teamId, string name, int count, Vector2Int position)
        {
            return new TroopRef(id, teamId, 1, name, count, position);
        }
    }
}
