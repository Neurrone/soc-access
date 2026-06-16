using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquest;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Battle.Bacterias;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Events.Combat;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatNarrationPlannerTests
    {
        [TestMethod]
        public void ModifierChangeFormatsLocalizedDescriptionWithLanguageSpecificAmountPosition()
        {
            ModifierChange change = new ModifierChange(
                BacteriaModifierType.TroopRootSpread,
                BacteriaModifierApplicationType.Value,
                1,
                "根茎蔓延 {0} 格",
                true,
                1);

            Assert.AreEqual("根茎蔓延 +1 格", change.Format());
        }

        [TestMethod]
        public void ModifierChangeDoesNotAppendAmountWhenLocalizedDescriptionHasNoAmount()
        {
            ModifierChange change = new ModifierChange(
                BacteriaModifierType.TroopIgnoreZoneOfControl,
                BacteriaModifierApplicationType.Value,
                1,
                "无视控制区域",
                false,
                1);

            Assert.AreEqual("无视控制区域", change.Format());
        }

        [TestMethod]
        public void ModifierChangeFormatsNegativeBlessedAsPositiveCursedAmount()
        {
            ModifierChange change = new ModifierChange(
                BacteriaModifierType.TroopBlessed,
                BacteriaModifierApplicationType.Value,
                -2,
                "{0} Misfortune",
                true,
                -1);

            Assert.AreEqual("+2 Misfortune", change.Format());
        }

        [TestMethod]
        public void ModifierChangeAddsPercentForPercentageBasedModifierTypes()
        {
            ModifierChange change = new ModifierChange(
                BacteriaModifierType.TroopSpellDamageResistance,
                BacteriaModifierApplicationType.Value,
                10,
                "{0} Spell Damage Resistance",
                true,
                1);

            Assert.AreEqual("+10% Spell Damage Resistance", change.Format());
        }

        [TestMethod]
        public void CombatRefsDoNotSynthesizeNamesFromEnums()
        {
            Assert.AreEqual(string.Empty, new SpellRef((SpellTypes)24, string.Empty, 1).Name);
            Assert.AreEqual(string.Empty, new AbilityRef(TroopAbilityType.Leap, string.Empty).Name);
            Assert.AreEqual(string.Empty, new BacteriaRef(1, (BacteriaTypes)248, string.Empty).Name);
        }

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
        public void FlushMovesSpellBeforeBlindFuryExchange()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            TestEvent removal = new TestEvent("removal");
            AttackEvent blindFuryAttack = Attack(AttackTrigger.BlindFury);
            TestEvent blindFuryDamage = new TestEvent("blind fury damage");
            AttackEvent retaliationAttack = Attack(AttackTrigger.Retaliation);
            TestEvent retaliationDamage = new TestEvent("retaliation damage");
            TestEvent spell = new TestEvent("spell");

            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.BacteriaRemoved, removal));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, blindFuryAttack));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, blindFuryDamage));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, retaliationAttack));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, retaliationDamage));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreSame(spell, result[0].Event);
            Assert.AreSame(removal, result[1].Event);
            Assert.AreSame(blindFuryAttack, result[2].Event);
            Assert.AreSame(blindFuryDamage, result[3].Event);
            Assert.AreSame(retaliationAttack, result[4].Event);
            Assert.AreSame(retaliationDamage, result[5].Event);
        }

        [TestMethod]
        public void FlushDoesNotMoveSpellBeforeOrdinaryRetaliationExchange()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            AttackEvent attack = Attack(AttackTrigger.Player);
            TestEvent damage = new TestEvent("damage");
            AttackEvent retaliationAttack = Attack(AttackTrigger.Retaliation);
            TestEvent retaliationDamage = new TestEvent("retaliation damage");
            TestEvent spell = new TestEvent("spell");

            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, attack));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, retaliationAttack));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, retaliationDamage));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreSame(attack, result[0].Event);
            Assert.AreSame(damage, result[1].Event);
            Assert.AreSame(retaliationAttack, result[2].Event);
            Assert.AreSame(spell, result[3].Event);
            Assert.AreSame(retaliationDamage, result[4].Event);
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
        public void FlushSuppressesRepelRemovalWithoutAssociatedPush()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            BacteriaRef repel = Bacteria("Repel", 259);

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(repel, Troop(10, 1, "Shield of Order"), 10), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(0, result.Count);
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
            Assert.AreEqual("Momentum affects 10 Footmen at 0, 0, melee offense +10", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummariesDoNotCombineBlessedChangesForSameTroop()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            BacteriaRef fortune = Bacteria("Fortune");
            TroopRef footmen = Troop(10, 1, "Footmen");

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    fortune,
                    footmen,
                    new[] { Modifier(BacteriaModifierType.TroopBlessed, -1) },
                    10),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    fortune,
                    footmen,
                    new[] { Modifier(BacteriaModifierType.TroopBlessed, 1) },
                    10),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Fortune affects 10 Footmen at 0, 0, blessed -1 and blessed +1", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummariesStillGroupSameBlessedChangeAcrossTroops()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10, 20 }, new int[0]);
            BacteriaRef fortune = Bacteria("Fortune");

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    fortune,
                    Troop(10, 1, "Footmen"),
                    new[] { Modifier(BacteriaModifierType.TroopBlessed, 1) },
                    10),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    fortune,
                    Troop(20, 1, "Rangers"),
                    new[] { Modifier(BacteriaModifierType.TroopBlessed, 1) },
                    20),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Fortune affects your troops, blessed +1", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void FlushMovesSpellBeforeBacteriaModifierSummary()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            TestEvent spell = new TestEvent("spell");
            BacteriaRef lethargy = Bacteria("Lethargy", 292);

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    lethargy,
                    Troop(10, 1, "Rangers"),
                    new[] { Modifier(BacteriaModifierType.TroopMovement, -1), Modifier(BacteriaModifierType.TroopInitiative, -10) },
                    10),
                snapshot);
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(2, result.Count);
            Assert.AreSame(spell, result[0].Event);
            Assert.AreEqual("Lethargy affects 10 Rangers at 0, 0, movement -1 and initiative -10", result[1].Event.GetSpeechText());
        }

        [TestMethod]
        public void FlushMovesSpellBeforeBacteriaModifierSummaryAcrossQueueChanged()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new int[0], new[] { 10 });
            TestEvent spell = new TestEvent("spell");
            BacteriaRef insectSwarm = Bacteria("Insect Swarm", 265);
            DamageEvent damage = Damage(Troop(10, 2, "Ravagers", 3), insectSwarm);

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    insectSwarm,
                    Troop(10, 2, "Ravagers", 3),
                    new[] { Modifier(BacteriaModifierType.TroopInitiative, -15) },
                    10),
                snapshot);
            planner.Enqueue(CombatNarrationItem.Direct(CombatNarrationItemKind.QueueChanged, new QueueChangedEvent()));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage, 10));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(4, result.Count);
            Assert.AreSame(spell, result[0].Event);
            Assert.AreEqual("Insect Swarm affects 3 enemy Ravagers at 0, 0, initiative -15", result[1].Event.GetSpeechText());
            Assert.IsInstanceOfType(result[2].Event, typeof(QueueChangedEvent));
            Assert.AreSame(damage, result[3].Event);
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
        public void BacteriaModifierSummaryUsesExplicitTargetForSingleEnemyRangedTroop()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(
                your: new[] { 10 },
                enemy: new[] { 30 },
                yourMelee: new[] { 10 },
                enemyMelee: new int[0],
                yourRanged: new int[0],
                enemyRanged: new[] { 30 });
            BacteriaRef highGround = Bacteria("High Ground");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(highGround, Troop(30, 2, "Militia"), new[] { Modifier(BacteriaModifierType.TroopRangedRange, 1) }, 30), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("High Ground affects 10 enemy Militia at 0, 0, ranged range +1", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaRemovalSummaryUsesExplicitTargetForSingleEnemyRangedTroop()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(
                your: new[] { 10 },
                enemy: new[] { 30 },
                yourMelee: new[] { 10 },
                enemyMelee: new int[0],
                yourRanged: new int[0],
                enemyRanged: new[] { 30 });
            BacteriaRef highGround = Bacteria("High Ground");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(highGround, Troop(30, 2, "Militia"), 30), snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("High Ground removed from 10 enemy Militia at 0, 0", result[0].Event.GetSpeechText());
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
                your: new[] { 10, 15, 20, 25 },
                enemy: new int[0],
                yourMelee: new[] { 10, 15 },
                enemyMelee: new int[0],
                yourRanged: new[] { 20, 25 },
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
                    Troop(15, 1, "Knights"),
                    new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 10), Modifier(BacteriaModifierType.TroopDefense, 10) },
                    15),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    momentum,
                    Troop(20, 1, "Rangers"),
                    new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 10), Modifier(BacteriaModifierType.TroopRangedOffense, 10), Modifier(BacteriaModifierType.TroopDefense, 10) },
                    20),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    momentum,
                    Troop(25, 1, "Sappers"),
                    new[] { Modifier(BacteriaModifierType.TroopMeleeOffense, 10), Modifier(BacteriaModifierType.TroopRangedOffense, 10), Modifier(BacteriaModifierType.TroopDefense, 10) },
                    25),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Momentum affects your melee troops, melee offense +10 and defense +10 and your ranged troops, melee offense +10, ranged offense +10, and defense +10", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummariesMergeAfterAbilityEvent()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10, 20 }, new int[0]);
            BacteriaRef protectedEffect = Bacteria("Protected", 410);

            planner.Enqueue(CombatNarrationItem.Create(
                CombatNarrationItemKind.Ability,
                new AbilityUsedEvent(
                    new ActorRef(Troop(5, 1, "Shield of Order"), true),
                    new AbilityRef((TroopAbilityType)8, "Protect"),
                    null,
                    null),
                5));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    protectedEffect,
                    Troop(10, 1, "Archers"),
                    new[] { Modifier(BacteriaModifierType.TroopDefense, 25) },
                    10),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    protectedEffect,
                    Troop(20, 1, "Footmen"),
                    new[] { Modifier(BacteriaModifierType.TroopDefense, 25) },
                    20),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Shield of Order uses Protect", result[0].Event.GetSpeechText());
            Assert.AreEqual("Protected affects your troops, defense +25", result[1].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaModifierSummariesMergeAcrossQueueChanged()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 12, 14, 16, 17, 20 }, new int[0]);
            BacteriaRef inspiredFirst = Bacteria("Inspired", 1465, 63);
            BacteriaRef inspiredSecond = Bacteria("Inspired", 1469, 63);

            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    inspiredFirst,
                    TroopAt(14, 1, "Sheng Grenadier", 30, new Vector2Int(1, 5)),
                    new[]
                    {
                        Modifier(BacteriaModifierType.TroopMeleeOffense, 5),
                        Modifier(BacteriaModifierType.TroopRangedOffense, 5),
                        Modifier(BacteriaModifierType.TroopInitiative, 5)
                    },
                    14),
                snapshot);
            planner.Enqueue(CombatNarrationItem.Direct(CombatNarrationItemKind.QueueChanged, new QueueChangedEvent()));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    inspiredSecond,
                    TroopAt(16, 1, "Yi", 6, new Vector2Int(1, 6)),
                    new[]
                    {
                        Modifier(BacteriaModifierType.TroopMeleeOffense, 5),
                        Modifier(BacteriaModifierType.TroopRangedOffense, 5),
                        Modifier(BacteriaModifierType.TroopInitiative, 5)
                    },
                    16),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Inspired affects 30 Sheng Grenadier at 1.5, 5 and 6 Yi at 1, 6, melee offense +5, ranged offense +5, and initiative +5", result[0].Event.GetSpeechText());
            Assert.IsInstanceOfType(result[1].Event, typeof(QueueChangedEvent));
        }

        [TestMethod]
        public void FlushCondensesObservedFengMovePassiveTraitLogSequence()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 12, 14, 16, 17, 20 }, new int[0]);
            TroopRef feng = TroopAt(20, 1, "Feng", 5, new Vector2Int(2, 6));
            TroopRef grenadier = TroopAt(14, 1, "Sheng Grenadier", 30, new Vector2Int(1, 5));
            TroopRef yi = TroopAt(16, 1, "Yi", 6, new Vector2Int(1, 6));
            BacteriaRef fortuneFirst = Bacteria("Fortune", 1461, 1721);
            BacteriaRef fortuneSecond = Bacteria("Fortune", 1463, 1721);
            BacteriaRef inspiredFirst = Bacteria("Inspired", 1465, 63);
            BacteriaRef inspiredSecond = Bacteria("Inspired", 1469, 63);

            planner.Enqueue(CombatNarrationItem.Create(
                CombatNarrationItemKind.Move,
                new TroopMovedEvent(new ActorRef(feng, true), new Vector2Int(3, 6), new Vector2Int(2, 6), null),
                20));
            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(fortuneFirst, 14));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    fortuneFirst,
                    grenadier,
                    new[] { Modifier(BacteriaModifierType.TroopBlessed, 1) },
                    14),
                snapshot);
            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(fortuneSecond, 16));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    fortuneSecond,
                    yi,
                    new[] { Modifier(BacteriaModifierType.TroopBlessed, 1) },
                    16),
                snapshot);
            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(inspiredFirst, 14));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    inspiredFirst,
                    grenadier,
                    new[]
                    {
                        Modifier(BacteriaModifierType.TroopMeleeOffense, 5),
                        Modifier(BacteriaModifierType.TroopRangedOffense, 5),
                        Modifier(BacteriaModifierType.TroopInitiative, 5)
                    },
                    14),
                snapshot);
            planner.Enqueue(CombatNarrationItem.Direct(CombatNarrationItemKind.QueueChanged, new QueueChangedEvent()));
            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(inspiredSecond, 16));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    inspiredSecond,
                    yi,
                    new[]
                    {
                        Modifier(BacteriaModifierType.TroopMeleeOffense, 5),
                        Modifier(BacteriaModifierType.TroopRangedOffense, 5),
                        Modifier(BacteriaModifierType.TroopInitiative, 5)
                    },
                    16),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("Feng moves to 2, 6", result[0].Event.GetSpeechText());
            Assert.AreEqual("Fortune affects 30 Sheng Grenadier at 1.5, 5 and 6 Yi at 1, 6, blessed +1", result[1].Event.GetSpeechText());
            Assert.AreEqual("Inspired affects 30 Sheng Grenadier at 1.5, 5 and 6 Yi at 1, 6, melee offense +5, ranged offense +5, and initiative +5", result[2].Event.GetSpeechText());
            Assert.IsInstanceOfType(result[3].Event, typeof(QueueChangedEvent));
        }

        [TestMethod]
        public void FlushCondensesObservedFengMovePassiveTraitRemovalSequence()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 12, 14, 16, 17, 20 }, new int[0]);
            TroopRef feng = TroopAt(20, 1, "Feng", 5, new Vector2Int(2, 6));
            TroopRef grenadier = TroopAt(14, 1, "Sheng Grenadier", 30, new Vector2Int(1, 5));
            TroopRef yi = TroopAt(16, 1, "Yi", 6, new Vector2Int(1, 6));
            BacteriaRef fortuneFirst = Bacteria("Fortune", 1461, 1721);
            BacteriaRef fortuneSecond = Bacteria("Fortune", 1463, 1721);
            BacteriaRef inspiredFirst = Bacteria("Inspired", 1465, 63);
            BacteriaRef inspiredSecond = Bacteria("Inspired", 1469, 63);

            planner.Enqueue(CombatNarrationItem.Create(
                CombatNarrationItemKind.Move,
                new TroopMovedEvent(new ActorRef(feng, true), new Vector2Int(3, 6), new Vector2Int(2, 6), null),
                20));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaRemovalSummary(fortuneFirst, grenadier, 14),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaRemovalSummary(fortuneSecond, yi, 16),
                snapshot);
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaRemovalSummary(inspiredFirst, grenadier, 14),
                snapshot);
            planner.Enqueue(CombatNarrationItem.Direct(CombatNarrationItemKind.QueueChanged, new QueueChangedEvent()));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaRemovalSummary(inspiredSecond, yi, 16),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("Feng moves to 2, 6", result[0].Event.GetSpeechText());
            Assert.AreEqual("Fortune removed from 30 Sheng Grenadier at 1.5, 5 and 6 Yi at 1, 6", result[1].Event.GetSpeechText());
            Assert.AreEqual("Inspired removed from 30 Sheng Grenadier at 1.5, 5 and 6 Yi at 1, 6", result[2].Event.GetSpeechText());
            Assert.IsInstanceOfType(result[3].Event, typeof(QueueChangedEvent));
        }

        [TestMethod]
        public void CombatEventNarratorBatchesBacteriaAddAndModifierResponsesForSummaries()
        {
            Assert.IsTrue(CombatEventNarrator.ShouldBufferResponseTypeForBacteriaSummary(typeof(AddBattleBacteriaCommand.Response)));
            Assert.IsTrue(CombatEventNarrator.ShouldBufferResponseTypeForBacteriaSummary(typeof(RemoveBattleBacteriaCommand.Response)));
            Assert.IsTrue(CombatEventNarrator.ShouldBufferResponseTypeForBacteriaSummary(typeof(ChangeBattleBacteriaModifierCommand.Response)));
            Assert.IsFalse(CombatEventNarrator.ShouldBufferResponseTypeForBacteriaSummary(typeof(QueueChangedEvent)));
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
        public void FlushSuppressesBacteriaRemovalWhenSameBacteriaWasAddedInBatch()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 25 }, new int[0]);
            BacteriaRef rejuvenation = Bacteria("Rejuvenation", 2354, 53);
            TestEvent spell = new TestEvent("Ravenfayre casts Rejuvenation at 3.5, 3");

            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(rejuvenation, 25));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaRemovalSummary(
                    rejuvenation,
                    Troop(25, 1, "Faey Ragers", 13),
                    25),
                snapshot);
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(spell, result[0].Event);
        }

        [TestMethod]
        public void FlushKeepsBacteriaRemovalWhenOnlyDifferentBacteriaInstanceWasAdded()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 25 }, new int[0]);
            BacteriaRef added = Bacteria("Rejuvenation", 2354, 53);
            BacteriaRef removed = Bacteria("Rejuvenation", 2355, 53);

            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(added, 25));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaRemovalSummary(
                    removed,
                    Troop(25, 1, "Faey Ragers", 13),
                    25),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Rejuvenation removed from 13 Faey Ragers at 0, 0", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void BacteriaAddedMarkerDoesNotBlockModifierSummaryMerging()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10, 20 }, new int[0]);
            BacteriaRef firstProtected = Bacteria("Protected", 2199, 410);
            BacteriaRef secondProtected = Bacteria("Protected", 2200, 410);

            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(firstProtected, 10));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    firstProtected,
                    Troop(10, 1, "Archers"),
                    new[] { Modifier(BacteriaModifierType.TroopDefense, 25) },
                    10),
                snapshot);
            planner.Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(secondProtected, 20));
            planner.EnqueueBacteriaSummary(
                CombatNarrationItem.CreateBacteriaModifierSummary(
                    secondProtected,
                    Troop(20, 1, "Footmen"),
                    new[] { Modifier(BacteriaModifierType.TroopDefense, 25) },
                    20),
                snapshot);

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Protected affects your troops, defense +25", result[0].Event.GetSpeechText());
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
            Assert.AreEqual("High Ground affects 10 Militia at 0, 0, ranged range +1", result[0].Event.GetSpeechText());
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
            Assert.AreEqual("High Ground affects 10 Militia at 0, 0, ranged range +1", result[0].Event.GetSpeechText());
        }

        [TestMethod]
        public void FlushSuppressesDamageBacteriaRemovalForSameBacteriaType()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            BacteriaRef rupture = Bacteria("Rupture", 306);
            DamageEvent damage = Damage(Troop(10, 1, "Faey Ragers", 3), rupture);

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(rupture, Troop(10, 1, "Faey Ragers", 3), 10), snapshot);
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage, 10));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(damage, result[0].Event);
        }

        [TestMethod]
        public void FlushSuppressesMultiTargetDamageBacteriaRemovalForSameBacteriaType()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new int[0], new[] { 10, 20 });
            BacteriaRef chainLightning = Bacteria("Chain Lightning", 275);
            DamageEvent damage = Damage(Troop(10, 2, "Banes", 19), chainLightning);

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(chainLightning, Troop(10, 2, "Banes", 19), 10), snapshot);
            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(chainLightning, Troop(20, 2, "Cultists", 20), 20), snapshot);
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage, 10));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(damage, result[0].Event);
        }

        [TestMethod]
        public void FlushKeepsUnrelatedBacteriaRemovalWhenDamageUsesDifferentBacteriaType()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            BacteriaRef highGround = Bacteria("High Ground", 566);
            BacteriaRef rupture = Bacteria("Rupture", 306);
            DamageEvent damage = Damage(Troop(10, 1, "Faey Ragers", 3), rupture);

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(highGround, Troop(10, 1, "Faey Ragers", 3), 10), snapshot);
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage, 10));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("High Ground removed from 3 Faey Ragers at 0, 0", result[0].Event.GetSpeechText());
            Assert.AreSame(damage, result[1].Event);
        }

        [TestMethod]
        public void FlushSuppressesSpellDamageBacteriaRemovalWhenDamageHasNoBacteriaType()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            CombatNarrationSnapshot snapshot = Snapshot(new[] { 10 }, new int[0]);
            BacteriaRef justice = Bacteria("Justice2", 248);
            TroopRef target = Troop(10, 1, "Faey Queens", 4);
            DamageEvent damage = new DamageEvent(
                null,
                TargetRef.FromTroop(target),
                204,
                2,
                4,
                2,
                DamageType.Spell,
                AttackTrigger.Damage,
                false,
                null);
            TestEvent spell = new TestEvent("spell");

            planner.EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(justice, target, 10), snapshot);
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Damage, damage, 10));
            planner.Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Spell, spell));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(2, result.Count);
            Assert.AreSame(spell, result[0].Event);
            Assert.AreSame(damage, result[1].Event);
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
        public void SpellCastEventShortensEnemyCommanderPrefix()
        {
            SpellCastEvent spell = new SpellCastEvent(
                new CommanderRef(1, 2, 1, "Merkoth"),
                new SpellRef((SpellTypes)24, "Fireball", 1),
                null,
                null);

            Assert.AreEqual("enemy Merkoth casts Fireball", spell.GetSpeechText());
        }

        [TestMethod]
        public void DirectionalAttackEventNarratesLeft()
        {
            DirectionalAttackEvent attack = new DirectionalAttackEvent(
                new ActorRef(Troop(10, 2, "Hearts", 3), false),
                BeamFacing.Left);

            Assert.AreEqual("3 enemy Hearts at 0, 0 attacks left", attack.GetSpeechText());
        }

        [TestMethod]
        public void DirectionalAttackEventNarratesRight()
        {
            DirectionalAttackEvent attack = new DirectionalAttackEvent(
                new ActorRef(Troop(10, 2, "Hearts", 3), false),
                BeamFacing.Right);

            Assert.AreEqual("3 enemy Hearts at 0, 0 attacks right", attack.GetSpeechText());
        }

        [TestMethod]
        public void BeamFacingFormatsForTileSpeech()
        {
            Assert.AreEqual("facing left", CombatText.FormatBeamFacing(BeamFacing.Left));
            Assert.AreEqual("facing right", CombatText.FormatBeamFacing(BeamFacing.Right));
        }

        [TestMethod]
        public void FlushSuppressesAcidCloudCreation()
        {
            for (int blueprintId = 4; blueprintId <= 6; blueprintId++)
            {
                CombatNarrationPlanner planner = new CombatNarrationPlanner();
                EntityRef acidCloud = new EntityRef(100 + blueprintId, blueprintId, "Acid Cloud", new Vector2Int(8, 8));

                planner.Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.MapEntityCreated,
                    new MapEntityCreatedEvent(acidCloud),
                    entityId: acidCloud.EntityId));

                IReadOnlyList<CombatNarrationItem> result = planner.Flush();

                Assert.AreEqual(0, result.Count);
            }
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

        [TestMethod]
        public void LeapTeleportAbilityKeepsLeapAbilitySpeech()
        {
            AbilityUsedEvent leap = CombatEventNarrator.CreateLeapAbilityUsedEvent(
                new ActorRef(Troop(10, 1, "Rider"), true),
                new AbilityRef(TroopAbilityType.Leap, "Leap"),
                new Vector2Int(3, 4),
                new Vector2Int(5, 4));

            Assert.AreEqual("Rider uses Leap", leap.GetSpeechText());
        }

        [TestMethod]
        public void LeapTeleportNarratesAbilityThenDestination()
        {
            CombatNarrationPlanner planner = new CombatNarrationPlanner();
            ActorRef piercers = new ActorRef(Troop(10, 2, "Piercers", 9), true);

            planner.Enqueue(CombatNarrationItem.Create(
                CombatNarrationItemKind.Ability,
                new AbilityUsedEvent(
                    piercers,
                    new AbilityRef(TroopAbilityType.Leap, "Leap"),
                    null,
                    null),
                10));
            planner.Enqueue(CombatNarrationItem.Create(
                CombatNarrationItemKind.Teleport,
                new TeleportEvent(
                    piercers,
                    new Vector2Int(6, 4),
                    new Vector2Int(1, 4),
                    TeleportBattleTroopCommand.Source.Leap),
                10));

            IReadOnlyList<CombatNarrationItem> result = planner.Flush();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Piercers uses Leap", result[0].Event.GetSpeechText());
            Assert.AreEqual("Piercers teleports to 1, 4", result[1].Event.GetSpeechText());
        }

        private static BacteriaRef Bacteria(string name)
        {
            return Bacteria(name, 1);
        }

        private static AttackEvent Attack(AttackTrigger trigger)
        {
            TroopRef attacker = Troop(10, 1, "Footmen");
            TroopRef target = Troop(20, 2, "Militia");
            return new AttackEvent(new ActorRef(attacker, false), TargetRef.FromTroop(target), trigger);
        }

        private static BacteriaRef Bacteria(string name, int bacteriaType)
        {
            return new BacteriaRef(1, (BacteriaTypes)bacteriaType, name);
        }

        private static BacteriaRef Bacteria(string name, int bacteriaId, int bacteriaType)
        {
            return new BacteriaRef(bacteriaId, (BacteriaTypes)bacteriaType, name);
        }

        private static TroopRef Troop(int troopId, int teamId, string name)
        {
            return Troop(troopId, teamId, name, 10);
        }

        private static TroopRef Troop(int troopId, int teamId, string name, int count)
        {
            return new TroopRef(troopId, teamId, 1, name, count, Vector2Int.zero);
        }

        private static TroopRef TroopAt(int troopId, int teamId, string name, int count, Vector2Int position)
        {
            return new TroopRef(troopId, teamId, 1, name, count, position);
        }

        private static DamageEvent Damage(TroopRef target, BacteriaRef bacteria)
        {
            return new DamageEvent(
                null,
                TargetRef.FromTroop(target),
                39,
                3,
                target.Count,
                0,
                DamageType.Spell,
                AttackTrigger.Damage,
                false,
                bacteria);
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
