using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatInfluenceFormatterTests
    {
        [TestMethod]
        public void FormatZoneOfControlSuppressesOtherIndicatorsForSameEnemy()
        {
            string text = CombatInfluenceFormatter.Format(new[]
            {
                Source(Troop(10, "Oathbound", 15, new Vector2Int(8, 6)),
                    CombatRangeIndicator.ZoneOfControl,
                    CombatRangeIndicator.Deadly,
                    CombatRangeIndicator.Attack,
                    CombatRangeIndicator.Movement)
            });

            Assert.AreEqual("Zone of control: 15 Oathbound at 8, 6.", text);
        }

        [TestMethod]
        public void FormatDeadlySuppressesAttackButKeepsMovement()
        {
            string text = CombatInfluenceFormatter.Format(new[]
            {
                Source(Troop(10, "Spectres", 5, new Vector2Int(2, 0)),
                    CombatRangeIndicator.Deadly,
                    CombatRangeIndicator.Attack,
                    CombatRangeIndicator.Movement)
            });

            Assert.AreEqual("Deadly range: 5 Spectres at 2, 0. Movement range: 5 Spectres at 2, 0.", text);
        }

        [TestMethod]
        public void FormatGroupsAttackAndMovementSeparately()
        {
            string text = CombatInfluenceFormatter.Format(new[]
            {
                Source(Troop(10, "Rats", 5, new Vector2Int(0, 0)), CombatRangeIndicator.Attack),
                Source(Troop(20, "Spectres", 5, new Vector2Int(2, 0)), CombatRangeIndicator.Attack, CombatRangeIndicator.Movement)
            });

            Assert.AreEqual("Attack range: 5 Rats at 0, 0, 5 Spectres at 2, 0. Movement range: 5 Spectres at 2, 0.", text);
        }

        [TestMethod]
        public void FormatOrdersGroupsByDanger()
        {
            string text = CombatInfluenceFormatter.Format(new[]
            {
                Source(Troop(10, "Militia", 20, new Vector2Int(0, 0)), CombatRangeIndicator.Movement),
                Source(Troop(20, "Rangers", 6, new Vector2Int(2, 0)), CombatRangeIndicator.Attack),
                Source(Troop(30, "Spectres", 4, new Vector2Int(4, 0)), CombatRangeIndicator.Deadly),
                Source(Troop(40, "Oathbound", 9, new Vector2Int(6, 0)), CombatRangeIndicator.ZoneOfControl)
            });

            Assert.AreEqual("Zone of control: 9 Oathbound at 6, 0. Deadly range: 4 Spectres at 4, 0. Attack range: 6 Rangers at 2, 0. Movement range: 20 Militia at 0, 0.", text);
        }

        [TestMethod]
        public void FormatDoesNotUseLeadingInOrFinalAnd()
        {
            string text = CombatInfluenceFormatter.Format(new[]
            {
                Source(Troop(10, "Plague Rats", 59, new Vector2Int(6, 8)), CombatRangeIndicator.Movement),
                Source(Troop(20, "Oathbound", 19, new Vector2Int(6, 4)), CombatRangeIndicator.Movement),
                Source(Troop(30, "Spectres", 5, new Vector2Int(10, 4)), CombatRangeIndicator.Movement)
            });

            Assert.AreEqual("Movement range: 59 Plague Rats at 6, 8, 19 Oathbound at 6, 4, 5 Spectres at 10, 4.", text);
            Assert.IsFalse(text.StartsWith("in "));
            Assert.IsFalse(text.Contains("enemy"));
            Assert.IsFalse(text.Contains(" and "));
        }

        private static CombatInfluenceSource Source(TroopRef troop, params CombatRangeIndicator[] indicators)
        {
            return new CombatInfluenceSource(troop, indicators);
        }

        private static TroopRef Troop(int id, string name, int count, Vector2Int position)
        {
            return new TroopRef(id, 2, 1, name, count, position);
        }
    }
}
