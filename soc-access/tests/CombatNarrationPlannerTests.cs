using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquest;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Events.Combat;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatNarrationPlannerTests
    {
        [TestMethod]
        public void FlushMovesSpellBeforeImmediatelyPrecedingSpellEffect()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            TestEvent damage = new TestEvent("damage");
            TestEvent spell = new TestEvent("spell");

            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreSame(spell, result[0].Event);
            Assert.AreSame(damage, result[1].Event);
        }

        [TestMethod]
        public void FlushDoesNotMoveSpellAcrossNonEffectEvent()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            TestEvent attack = new TestEvent("attack");
            TestEvent damage = new TestEvent("damage");
            TestEvent spell = new TestEvent("spell");

            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, attack));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreSame(attack, result[0].Event);
            Assert.AreSame(spell, result[1].Event);
            Assert.AreSame(damage, result[2].Event);
        }

        [TestMethod]
        public void FlushMovesSpellBeforeImmediatelyPrecedingTeleportEffects()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            TestEvent firstTeleport = new TestEvent("first teleport");
            TestEvent secondTeleport = new TestEvent("second teleport");
            TestEvent spell = new TestEvent("spell");

            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Teleport, firstTeleport));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Teleport, secondTeleport));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreSame(spell, result[0].Event);
            Assert.AreSame(firstTeleport, result[1].Event);
            Assert.AreSame(secondTeleport, result[2].Event);
        }

        [TestMethod]
        public void FlushMovesSpellBeforeImmediatelyPrecedingPushEffect()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            TestEvent push = new TestEvent("push");
            TestEvent spell = new TestEvent("spell");

            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Push, push));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreSame(spell, result[0].Event);
            Assert.AreSame(push, result[1].Event);
        }

        [TestMethod]
        public void BacteriaRemovalSummariesMergeAndUseYourTroopsShortcut()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10, 20 }, new[] { 30 });
            BacteriaRef guarded = Bacteria("Guarded");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(guarded, Troop(10, 1, "Footmen"), 10), snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(guarded, Troop(20, 1, "Rangers"), 20), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Guarded removed from your troops", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaRemovalSummaryUsesExplicitTargetsForPartialSide()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10, 20 }, new[] { 30 });

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(Bacteria("Guarded"), Troop(10, 1, "Footmen"), 10), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual("Guarded removed from 10 Footmen at 0, 0", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummariesMergeAndCombineSameModifier()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new[] { 30 });
            BacteriaRef momentum = Bacteria("Momentum");
            TroopRef footmen = Troop(10, 1, "Footmen");

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    momentum,
                    footmen,
                    new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 5) },
                    10),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    momentum,
                    footmen,
                    new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 5) },
                    10),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Momentum affects your troops, melee offense +10", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaSummariesDoNotMergeAcrossNonSummaryEvent()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10, 20 }, new int[0]);
            BacteriaRef guarded = Bacteria("Guarded");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(guarded, Troop(10, 1, "Footmen"), 10), snapshot);
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, new TestEvent("attack")));
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(guarded, Troop(20, 1, "Rangers"), 20), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("Guarded removed from 10 Footmen at 0, 0", result[0].Event.GetSpeechText());
            Assert.AreEqual("attack", result[1].Event.GetSpeechText());
            Assert.AreEqual("Guarded removed from 10 Rangers at 0, 0", result[2].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaRemovalSummaryUsesAllTroopsShortcutForBothSides()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new[] { 30 });
            BacteriaRef tempest = Bacteria("Tempest");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(tempest, Troop(10, 1, "Footmen"), 10), snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(tempest, Troop(30, 2, "Militia"), 30), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Tempest removed from all troops", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummaryUsesAllRangedTroopsShortcut()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(
                your: new[] { 10, 20 },
                enemy: new[] { 30 },
                yourMelee: new[] { 10 },
                enemyMelee: new int[0],
                yourRanged: new[] { 20 },
                enemyRanged: new[] { 30 });
            BacteriaRef tempest = Bacteria("Tempest");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(tempest, Troop(20, 1, "Rangers"), new[] { Modifier(BacteriaModifierType.TroopRangedOffense, -25) }, 20), snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(tempest, Troop(30, 2, "Militia"), new[] { Modifier(BacteriaModifierType.TroopRangedOffense, -25) }, 30), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Tempest affects all ranged troops, ranged offense -25", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummaryPrefersYourTroopsWhenAllYourTroopsAreMelee()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(
                your: new[] { 10, 20 },
                enemy: new[] { 30 },
                yourMelee: new[] { 10, 20 },
                enemyMelee: new int[0],
                yourRanged: new int[0],
                enemyRanged: new[] { 30 });
            BacteriaRef momentum = Bacteria("Momentum");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(momentum, Troop(10, 1, "Footmen"), new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 10) }, 10), snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(momentum, Troop(20, 1, "Knights"), new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 10) }, 20), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Momentum affects your troops, melee offense +10", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummaryPrefersEnemyTroopsWhenAllEnemyTroopsAreRanged()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(
                your: new[] { 10 },
                enemy: new[] { 30, 40 },
                yourMelee: new[] { 10 },
                enemyMelee: new int[0],
                yourRanged: new int[0],
                enemyRanged: new[] { 30, 40 });
            BacteriaRef tempest = Bacteria("Tempest");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(tempest, Troop(30, 2, "Militia"), new[] { Modifier(BacteriaModifierType.TroopRangedOffense, -25) }, 30), snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(tempest, Troop(40, 2, "Rangers"), new[] { Modifier(BacteriaModifierType.TroopRangedOffense, -25) }, 40), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Tempest affects enemy troops, ranged offense -25", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummaryUsesGroupSpecificMeleeAndRangedShortcuts()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(
                your: new[] { 10, 20 },
                enemy: new int[0],
                yourMelee: new[] { 10 },
                enemyMelee: new int[0],
                yourRanged: new[] { 20 },
                enemyRanged: new int[0]);
            BacteriaRef momentum = Bacteria("Momentum");

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    momentum,
                    Troop(10, 1, "Footmen"),
                    new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 10), Modifier(BacteriaModifierType.TroopDefense, 10) },
                    10),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    momentum,
                    Troop(20, 1, "Rangers"),
                    new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 10), Modifier(BacteriaModifierType.TroopRangedOffense, 10), Modifier(BacteriaModifierType.TroopDefense, 10) },
                    20),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Momentum affects your melee troops, melee offense +10 and defense +10 and your ranged troops, melee offense +10, ranged offense +10, and defense +10", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaSummaryDropsZeroCountTargets()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(Bacteria("Guarded"), Troop(10, 1, "Plague Rats", 0), 10), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void FlushSuppressesSameEffectRemovalWhenEffectIsAppliedToSameTarget()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            BacteriaRef highGround = Bacteria("High Ground");

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    highGround,
                    Troop(10, 1, "Militia"),
                    new[] { Modifier(BacteriaModifierType.TroopRangedRange, 1) },
                    10),
                snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(highGround, Troop(10, 1, "Militia"), 10), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("High Ground affects your troops, ranged range +1", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void FlushSuppressesSameNamedEffectRemovalWhenEffectTypeChangesForSameTarget()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            BacteriaRef newHighGround = Bacteria("High Ground", 1);
            BacteriaRef oldHighGround = Bacteria("High Ground", 2);

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    newHighGround,
                    Troop(10, 1, "Militia"),
                    new[] { Modifier(BacteriaModifierType.TroopRangedRange, 1) },
                    10),
                snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(oldHighGround, Troop(10, 1, "Militia"), 10), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("High Ground affects your troops, ranged range +1", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void SpellCastEventDeduplicatesSelectedTargetPoints()
        {
            SpellCastEvent spell = new SpellCastEvent(
                new CommanderRef(1, 1, 1, "Cecilia"),
                new SpellRef((SpellTypes)24, "Advance", 1),
                new[] { new Vector2Int(9, 10), new Vector2Int(9, 10), new Vector2Int(9, 10) },
                null);

            Assert.AreEqual("Cecilia casts Advance at 9, 10", spell.GetSpeechText());
        }

        [TestMethod]
        public void FlushSuppressesAcidCloudCreation()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            EntityRef acidCloud = new EntityRef(100, 6, "Acid Cloud", new Vector2Int(8, 8));

            planner.Enqueue(CombatNarrationItem.Create(
                CombatNarrationItemKind.MapEntityCreated,
                new MapEntityCreatedEvent(acidCloud),
                entityId: acidCloud.EntityId));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void FlushKeepsOtherMapEntityCreation()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            EntityRef barrier = new EntityRef(100, 10, "Barrier", new Vector2Int(8, 8));

            planner.Enqueue(CombatNarrationItem.Create(
                CombatNarrationItemKind.MapEntityCreated,
                new MapEntityCreatedEvent(barrier),
                entityId: barrier.EntityId));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Barrier at 8, 8 appears", result[0].Event.GetSpeechText());
        }

        private static BacteriaRef Bacteria(string name)
        {
            return Bacteria(name, 1);
        }

        private static BacteriaRef Bacteria(string name, int bacteriaType)
        {
            return new BacteriaRef(1, (BacteriaTypes)bacteriaType, name);
        }

        private static TroopRef Troop(int troopId, int teamId, string name)
        {
            return Troop(troopId, teamId, name, 10);
        }

        private static TroopRef Troop(int troopId, int teamId, string name, int count)
        {
            return new TroopRef(troopId, teamId, 1, name, count, Vector2Int.zero);
        }

        private static CombatNarrationSnapshot Snapshot(IEnumerable<int> your, IEnumerable<int> enemy)
        {
            return Snapshot(your, enemy, null, null, null, null);
        }

        private static CombatNarrationSnapshot Snapshot(
            IEnumerable<int> your,
            IEnumerable<int> enemy,
            IEnumerable<int> yourMelee,
            IEnumerable<int> enemyMelee,
            IEnumerable<int> yourRanged,
            IEnumerable<int> enemyRanged)
        {
            return new CombatNarrationSnapshot(1, your, enemy, yourMelee, enemyMelee, yourRanged, enemyRanged);
        }

        private static ModifierChange Modifier(BacteriaModifierType type, int amount)
        {
            return new ModifierChange(type, BacteriaModifierApplicationType.Value, amount);
        }

        private sealed class TestEvent : IAccessibilityEvent
        {
            private readonly string _text;

            public TestEvent(string text)
            {
                _text = text;
            }

            public string Kind
            {
                get { return "test"; }
            }

            public string GetSpeechText()
            {
                return _text;
            }
        }
    }
}
