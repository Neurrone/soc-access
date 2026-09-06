using System;
using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// Picking something up and putting it down: the two decision tables in
    /// <see cref="CarryActions"/>. <see cref="CarryActions.Press"/> is the carry key, which only ever
    /// HOLDS things - pick up, swap, put back; <see cref="CarryActions.Activate"/> is the activation
    /// key, which is the only thing that DROPS. Every phrase here is the shipped English one, because
    /// what the player HEARS is the behavior - a drop that refuses silently and one that refuses in
    /// the game's words are the same code path with different evidence.
    /// </summary>
    [TestClass]
    public class CarryTests
    {
        private const string Ship = "ship";

        private static NodeVtable Source(object cargo, string name, string kind = Ship)
        {
            NodeVtable vtable = Vt(name);
            vtable.OnPickUp = () => new CarryItem(cargo, name, kind);
            return vtable;
        }

        private static NodeVtable Target(DropResult answer, string kind = Ship)
        {
            NodeVtable vtable = Vt("Fleet");
            vtable.DropKind = kind;
            vtable.OnDrop = item => answer;
            return vtable;
        }

        [TestMethod]
        public void PickingSomethingUpAnnouncesItByTheNameItHadThen()
        {
            CarryState carry = new CarryState();
            object explorer = new object();
            CarryOutcome outcome = CarryActions.Press(
                Source(explorer, "Explorer"),
                carry,
                "galaxy"
            );

            Assert.IsTrue(outcome.Handled);
            Assert.AreEqual("Dragging Explorer", outcome.Speech);
            Assert.AreSame(explorer, carry.Held.Cargo);
            Assert.AreEqual("Explorer", carry.Held.Name);
            Assert.AreEqual("galaxy", carry.Owner);
        }

        [TestMethod]
        public void TheCarriedNameSurvivesTheControlItCameFrom()
        {
            CarryState carry = new CarryState();
            string drawn = "Explorer";
            NodeVtable vtable = Vt("row");
            vtable.OnPickUp = () => new CarryItem(new object(), drawn, Ship);
            CarryActions.Press(vtable, carry, "galaxy");

            // The row is recycled onto another ship, which is what actually happens to a list the
            // game re-sorts under the player.
            drawn = "Hunter";

            Assert.AreEqual("Explorer", carry.Held.Name);
            Assert.AreEqual("Cancelled drag", CarryActions.Cancel(carry).Speech);
        }

        [TestMethod]
        public void AControlWithNothingToGiveIsClaimedAndSilent()
        {
            CarryState carry = new CarryState();
            NodeVtable empty = Vt("Empty slot");
            empty.OnPickUp = () => null;

            CarryOutcome outcome = CarryActions.Press(empty, carry, "galaxy");

            // Claimed - the control IS a source - but silent, like every other gesture key with
            // nothing to do on the control it was pressed on.
            Assert.IsTrue(outcome.Handled);
            Assert.IsNull(outcome.Speech);
            Assert.IsFalse(carry.IsCarrying);
        }

        [TestMethod]
        public void TheKeyIsNotOursOnAControlWithNothingToCarry()
        {
            CarryState carry = new CarryState();
            Assert.IsFalse(CarryActions.Claims(Vt("Button"), carry));
            Assert.IsFalse(CarryActions.Press(Vt("Button"), carry, "galaxy").Handled);
        }

        [TestMethod]
        public void EverythingIsOursWhileSomethingIsBeingCarriedAndSaysNothing()
        {
            CarryState carry = new CarryState();
            carry.PickUp(new CarryItem(new object(), "Explorer", Ship), "galaxy");

            Assert.IsTrue(CarryActions.Claims(Vt("Button"), carry));
            CarryOutcome outcome = CarryActions.Press(Vt("Button"), carry, "galaxy");

            // Consumed - the carry is the mode the player is in - but silent: looking for the target
            // means pressing the key along a row of controls, and a cue on each of them is noise.
            Assert.IsTrue(outcome.Handled);
            Assert.IsNull(outcome.Speech);
            Assert.IsTrue(carry.IsCarrying);
        }

        [TestMethod]
        public void AnotherSourceSwapsWhatIsBeingCarried()
        {
            CarryState carry = new CarryState();
            object hunter = new object();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Press(Source(hunter, "Hunter"), carry, "galaxy");

            Assert.AreEqual("Dragging Hunter", outcome.Speech);
            Assert.AreSame(hunter, carry.Held.Cargo);
        }

        /// <summary>There is NO put-back (owner ruling 2026-08-29): the key pressed again on the
        /// control the thing came from picks it up again rather than ending the carry. It has to be, as
        /// soon as one source can hand over different AMOUNTS of the same thing - a population marker
        /// carries itself and every marker after it - because pressing the key on a slot is then how a
        /// player asks for that slot's amount, and a cancel there would throw the carry away instead.
        /// The back key is the only cancel.</summary>
        [TestMethod]
        public void TheSourceItCameFromHandsItOverAgainInsteadOfPuttingItBack()
        {
            CarryState carry = new CarryState();
            object explorer = new object();
            NodeVtable row = Source(explorer, "Explorer");
            CarryActions.Press(row, carry, "galaxy");

            CarryOutcome outcome = CarryActions.Press(row, carry, "galaxy");

            Assert.IsTrue(outcome.Handled);
            Assert.AreEqual("Dragging Explorer", outcome.Speech);
            Assert.IsTrue(carry.IsCarrying);
            Assert.AreSame(explorer, carry.Held.Cargo);
        }

        /// <summary>A re-pick on the SAME cargo can still change how much of it is held: that is the
        /// whole reason the put-back had to go.</summary>
        [TestMethod]
        public void ARePickOnTheSameCargoTakesTheNewAmount()
        {
            CarryState carry = new CarryState();
            object imperials = new object();
            NodeVtable first = Vt("First");
            first.OnPickUp = () => new CarryItem(imperials, "Imperials x 3", Ship, 3);
            NodeVtable last = Vt("Last");
            last.OnPickUp = () => new CarryItem(imperials, "Imperials", Ship);

            CarryActions.Press(first, carry, "system");
            Assert.AreEqual(3, carry.Held.Quantity);

            CarryActions.Press(last, carry, "system");
            Assert.AreEqual(1, carry.Held.Quantity);
            Assert.AreSame(imperials, carry.Held.Cargo);
        }

        /// <summary>What one press picked up is what the drop is told about, captured at pick-up like
        /// the name and for the same reason.</summary>
        [TestMethod]
        public void TheQuantityTravelsFromThePickUpToTheDrop()
        {
            CarryState carry = new CarryState();
            NodeVtable source = Vt("Slot");
            source.OnPickUp = () => new CarryItem(new object(), "Imperials x 3", Ship, 3);
            int dropped = 0;
            NodeVtable target = Vt("Port");
            target.DropKind = Ship;
            target.OnDrop = item =>
            {
                dropped = item.Quantity;
                return DropResult.Done();
            };

            CarryActions.Press(source, carry, "system");
            CarryActions.Activate(target, carry);

            Assert.AreEqual(3, dropped);
        }

        /// <summary>The pick-up announcement teaches the way out - both ways - with the chords spelled
        /// by the injected renderer rather than written into the sentence, so re-binding either gesture
        /// re-words it.</summary>
        [TestMethod]
        public void ThePickUpAnnouncementNamesBothWaysOutOfTheCarry()
        {
            CarryState carry = new CarryState();
            try
            {
                NodeHints.Chord = (action, index) =>
                    action == CarryState.DropAction ? "Enter" : "Backspace";

                CarryOutcome outcome = CarryActions.Press(
                    Source(new object(), "Explorer"),
                    carry,
                    "galaxy"
                );

                Assert.AreEqual(
                    "Dragging Explorer. Enter to drop, Backspace to cancel.",
                    outcome.Speech
                );
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>The two DERIVED hints: what this control would hand over while nothing is held, and
        /// where what IS held can go. Never both, and neither without a renderer.</summary>
        [TestMethod]
        public void TheDerivedHintsFollowWhatIsHeld()
        {
            CarryState carry = new CarryState();
            NodeVtable source = Vt("Slot");
            source.OnPickUp = () => new CarryItem(new object(), "Imperials x 3", Ship, 3);
            NodeVtable target = Target(DropResult.Done());
            try
            {
                NodeHints.Chord = (action, index) =>
                    action == CarryState.DropAction ? "Enter" : "Space";

                List<string> lines = new List<string>();
                carry.HintLines(lines, source);
                CollectionAssert.AreEqual(new[] { "Space to drag Imperials x 3." }, lines);

                lines.Clear();
                carry.HintLines(lines, target);
                Assert.AreEqual(0, lines.Count);

                CarryActions.Press(source, carry, "system");

                lines.Clear();
                carry.HintLines(lines, source);
                Assert.AreEqual(0, lines.Count);

                lines.Clear();
                carry.HintLines(lines, target);
                CollectionAssert.AreEqual(new[] { "Enter to drop Imperials x 3." }, lines);
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>A target whose own test says no offers no drop hint either - the hint is gated on
        /// the same <see cref="CarryState.Takes"/> the "drop target" word is, so it inherits every
        /// screen's <see cref="NodeVtable.DropAccepts"/> for free.</summary>
        [TestMethod]
        public void ATargetThatRefusesThisCargoOffersNoDropHint()
        {
            CarryState carry = new CarryState();
            NodeVtable target = Target(DropResult.Done());
            target.DropAccepts = held => false;
            try
            {
                NodeHints.Chord = (action, index) => "Enter";
                CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

                List<string> lines = new List<string>();
                carry.HintLines(lines, target);

                Assert.AreEqual(0, lines.Count);
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>
        /// Whether a drag says a COUNT is a fact about the cargo, decided by the source when it
        /// composes the name (owner ruling 2026-08-29). Population is measured in units, so it states
        /// the count every time, one included; a module, a ship, a queue line and a tactic card are
        /// single things and must never read "x 1". Both shapes go through the very same phrases,
        /// which is what this holds: the phrases interpolate the name and add nothing of their own.
        /// </summary>
        [TestMethod]
        public void OnlyCargoMeasuredInUnitsStatesACountInTheDragPhrases()
        {
            try
            {
                NodeHints.Chord = (action, index) =>
                    action == CarryState.DropAction
                        ? "Enter"
                        : (action == CarryState.CancelAction ? "Escape" : "Space");

                // What PopulationMoves.Name composes for a single unit: the count, always.
                CarryState units = new CarryState();
                NodeVtable people = Vt("Slot");
                people.OnPickUp = () =>
                    new CarryItem(new object(), "Imperials x 1", Ship, 1);

                Assert.AreEqual(
                    "Dragging Imperials x 1. Enter to drop, Escape to cancel.",
                    CarryActions.Press(people, units, "system").Speech
                );

                List<string> lines = new List<string>();
                new CarryState().HintLines(lines, people);
                CollectionAssert.AreEqual(new[] { "Space to drag Imperials x 1." }, lines);

                // And a single thing keeps its plain name through the identical phrases.
                CarryState single = new CarryState();
                NodeVtable module = Source(new object(), "Basic Warp Drive");

                Assert.AreEqual(
                    "Dragging Basic Warp Drive. Enter to drop, Escape to cancel.",
                    CarryActions.Press(module, single, "shipdesign").Speech
                );

                lines.Clear();
                new CarryState().HintLines(lines, module);
                CollectionAssert.AreEqual(new[] { "Space to drag Basic Warp Drive." }, lines);
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>With no renderer at all neither hint exists, and the pick-up falls back to naming
        /// what is held: a sentence promising a chord nobody can spell says nothing.</summary>
        [TestMethod]
        public void WithNoChordRendererThereAreNoHintsAndNoPromisedKeys()
        {
            CarryState carry = new CarryState();
            NodeVtable source = Source(new object(), "Explorer");

            List<string> lines = new List<string>();
            carry.HintLines(lines, source);
            Assert.AreEqual(0, lines.Count);

            Assert.AreEqual("Dragging Explorer", CarryActions.Press(source, carry, "galaxy").Speech);
        }

        [TestMethod]
        public void ADropGoesThroughTheTargetAndEndsTheCarry()
        {
            CarryState carry = new CarryState();
            object explorer = new object();
            CarryActions.Press(Source(explorer, "Explorer"), carry, "galaxy");

            CarryItem dropped = null;
            NodeVtable fleet = Vt("Second fleet");
            fleet.DropKind = Ship;
            fleet.OnDrop = item =>
            {
                dropped = item;
                return DropResult.Done("Explorer joined Second fleet");
            };

            CarryOutcome outcome = CarryActions.Activate(fleet, carry);

            Assert.AreSame(explorer, dropped.Cargo);
            Assert.AreEqual("Explorer joined Second fleet", outcome.Speech);
            Assert.IsFalse(carry.IsCarrying);
        }

        [TestMethod]
        public void ADropTheTargetSaysNothingAboutStillReportsItself()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(Target(DropResult.Done()), carry);

            Assert.AreEqual("Dropped Explorer", outcome.Speech);
            Assert.IsFalse(carry.IsCarrying);
        }

        [TestMethod]
        public void ARefusedDropSpeaksTheGamesReasonAndKeepsCarrying()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(
                Target(DropResult.Refused("The fleet is full")),
                carry
            );

            Assert.IsTrue(outcome.Handled);
            Assert.AreEqual("The fleet is full", outcome.Speech);
            Assert.IsTrue(carry.IsCarrying);
            Assert.AreEqual("Explorer", carry.Held.Name);
        }

        [TestMethod]
        public void AWordlessRefusalStillSaysTheDropDidNotHappen()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(Target(DropResult.Refused()), carry);

            Assert.AreEqual("Explorer cannot go there", outcome.Speech);
            Assert.IsTrue(carry.IsCarrying);
        }

        [TestMethod]
        public void ATargetOnlyTakesItsOwnKindOfCargo()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            bool asked = false;
            NodeVtable planet = Vt("Homeworld");
            planet.DropKind = "population";
            planet.OnDrop = item =>
            {
                asked = true;
                return DropResult.Done();
            };

            CarryOutcome outcome = CarryActions.Activate(planet, carry);

            // Not ours: the control does its own click, exactly as it would with nothing held.
            Assert.IsFalse(asked);
            Assert.IsFalse(outcome.Handled);
            Assert.IsTrue(carry.IsCarrying);
        }

        [TestMethod]
        public void TheCarryKeyNeverDropsEvenOnATargetItCouldDropOn()
        {
            CarryState carry = new CarryState();
            bool asked = false;
            NodeVtable fleet = Vt("Second fleet");
            fleet.DropKind = Ship;
            fleet.OnDrop = item =>
            {
                asked = true;
                return DropResult.Done();
            };
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Press(fleet, carry, "galaxy");

            // Claimed - the carry is the mode - and silent, with the thing still held: dropping is
            // Enter's job, and Space on a target that offers nothing to pick up does nothing at all.
            Assert.IsFalse(asked);
            Assert.IsTrue(outcome.Handled);
            Assert.IsNull(outcome.Speech);
            Assert.IsTrue(carry.IsCarrying);
        }

        [TestMethod]
        public void ATargetThatIsAlsoASourceHandsOverItsOwnOnTheCarryKeyAndTakesTheDropOnEnter()
        {
            CarryState carry = new CarryState();
            object hunter = new object();
            NodeVtable both = Target(DropResult.Done());
            both.OnPickUp = () => new CarryItem(hunter, "Hunter", Ship);
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            Assert.AreEqual("Dragging Hunter", CarryActions.Press(both, carry, "galaxy").Speech);
            Assert.AreSame(hunter, carry.Held.Cargo);
            Assert.AreEqual("Dropped Hunter", CarryActions.Activate(both, carry).Speech);
        }

        [TestMethod]
        public void AQueueLineSaysWhichItemMovedAndWhereItLanded()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Applied Casimir Effect"), carry, "research");

            CarryOutcome outcome = CarryActions.Activate(
                Target(
                    DropResult.Done("Moved Applied Casimir Effect to position 2")
                ),
                carry
            );

            // The position the player will hear the line read back with, not a zero-based index.
            Assert.AreEqual("Moved Applied Casimir Effect to position 2", outcome.Speech);
            Assert.IsFalse(carry.IsCarrying);
        }

        [TestMethod]
        public void ActivationIsNeverOursWhileNothingIsHeld()
        {
            CarryState carry = new CarryState();

            Assert.IsFalse(CarryActions.Activate(Target(DropResult.Done()), carry).Handled);
            Assert.IsFalse(CarryActions.Activate(Vt("Button"), null).Handled);
        }

        [TestMethod]
        public void ActivatingSomethingElseLeavesTheCarryAlone()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(Vt("Button"), carry);

            // Not ours, so the button does its own click - and the carry survives it, which is what
            // lets the player walk and use a page while holding something.
            Assert.IsFalse(outcome.Handled);
            Assert.IsNull(outcome.Speech);
            Assert.IsTrue(carry.IsCarrying);
        }

        [TestMethod]
        public void TheBackKeyIsOnlyOursWhileSomethingIsHeld()
        {
            CarryState carry = new CarryState();
            Assert.IsFalse(CarryActions.Cancel(carry).Handled);

            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");
            CarryOutcome outcome = CarryActions.Cancel(carry);

            Assert.IsTrue(outcome.Handled);
            Assert.AreEqual("Cancelled drag", outcome.Speech);
            Assert.IsFalse(carry.IsCarrying);
        }

        [TestMethod]
        public void TheLifecycleIsToldOfEveryPickUpAndOfEveryEndingThePlayerPerformed()
        {
            CarryState carry = new CarryState();
            List<string> heard = new List<string>();
            carry.Started = item => heard.Add("started " + item.Name);
            carry.Ended = item => heard.Add("ended " + item.Name);

            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");
            CarryActions.Activate(Target(DropResult.Refused("Full")), carry);
            CarryActions.Press(Source(new object(), "Hunter"), carry, "galaxy");
            CarryActions.Cancel(carry);
            CarryActions.Press(Source(new object(), "Scout"), carry, "galaxy");
            CarryActions.Activate(Target(DropResult.Done()), carry);

            CollectionAssert.AreEqual(
                new[]
                {
                    "started Explorer",
                    "ended Explorer",
                    "started Hunter",
                    "ended Hunter",
                    "started Scout",
                    "ended Scout",
                },
                heard
            );

            // A carry the player never ended - they walked off the page - is not one of its endings,
            // and an observer that throws costs its own effect and nothing else.
            carry.Ended = item =>
            {
                throw new InvalidOperationException("deaf");
            };
            CarryActions.Press(Source(new object(), "Ranger"), carry, "galaxy");
            carry.ScreenChanged(false);
            Assert.IsFalse(carry.IsCarrying);

            CarryActions.Press(Source(new object(), "Ranger"), carry, "galaxy");
            Assert.IsTrue(CarryActions.Cancel(carry).Handled);
        }

        [TestMethod]
        public void ASourceThatHandsOverNothingStartsNoCarryAndTellsNobody()
        {
            CarryState carry = new CarryState();
            int started = 0;
            carry.Started = item => started++;
            NodeVtable empty = Vt("Empty slot");
            empty.OnPickUp = () => null;

            Assert.IsTrue(CarryActions.Press(empty, carry, "galaxy").Handled);
            Assert.AreEqual(0, started);
        }

        [TestMethod]
        public void LeavingThePageDropsTheCarryButAMenuOverItDoesNot()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            carry.ScreenChanged(true);
            Assert.IsTrue(carry.IsCarrying);

            carry.ScreenChanged(false);
            Assert.IsFalse(carry.IsCarrying);
            Assert.IsNull(carry.Owner);
        }

        [TestMethod]
        public void ATargetSaysSoOnlyWhileSomethingItTakesIsBeingCarried()
        {
            CarryState carry = new CarryState();
            NodeAnnouncement part = carry.DropTargetPart(Target(DropResult.Done()));

            Assert.IsTrue(part.Live);
            Assert.IsNull(part.Text());

            carry.PickUp(new CarryItem(new object(), "Explorer", "population"), "galaxy");
            Assert.IsNull(part.Text());

            carry.PickUp(new CarryItem(new object(), "Explorer", Ship), "galaxy");
            Assert.AreEqual("drop target", part.Text());
        }

        [TestMethod]
        public void ATargetThatWouldRefuseThisCargoSaysNothingButStillRefusesInTheGamesWords()
        {
            CarryState carry = new CarryState();
            NodeVtable locked = Target(DropResult.Refused("This tactic is locked"));
            locked.DropAccepts = held => false;
            NodeAnnouncement part = carry.DropTargetPart(locked);
            carry.PickUp(new CarryItem(new object(), "Explorer", Ship), "tactics");

            Assert.IsNull(part.Text());

            // The drop is still the target's, so a player who presses anyway hears the game's reason
            // rather than the control's own click.
            CarryOutcome outcome = CarryActions.Activate(locked, carry);
            Assert.IsTrue(outcome.Handled);
            Assert.AreEqual("This tactic is locked", outcome.Speech);
        }

        [TestMethod]
        public void ASourceSaysDraggableOnlyWhileNothingIsCarried()
        {
            CarryState carry = new CarryState();
            NodeAnnouncement part = carry.DraggablePart(Source(new object(), "Explorer"));

            // Not live: the word is composed with the readout, and one appearing on its own after a
            // cancelled drag would be noise on top of the gesture that already said what happened.
            Assert.IsFalse(part.Live);
            Assert.AreEqual("draggable", part.Text());

            carry.PickUp(new CarryItem(new object(), "Hunter", Ship), "fleets");
            Assert.IsNull(part.Text());
        }

        [TestMethod]
        public void AControlWithNothingToGiveDoesNotSayDraggable()
        {
            CarryState carry = new CarryState();
            NodeVtable empty = Vt("Empty slot");
            empty.OnPickUp = () => null;

            Assert.IsNull(carry.DraggablePart(empty).Text());
        }

        [TestMethod]
        public void AControlThatIsNeitherSourceNorTargetSaysNothingAboutDragging()
        {
            CarryState carry = new CarryState();

            Assert.IsNull(carry.DraggablePart(Vt("Button")));
            Assert.IsNull(carry.DropTargetPart(Vt("Button")));
        }

        [TestMethod]
        public void NothingIsCarriedWithoutAState()
        {
            Assert.IsFalse(CarryActions.Claims(Vt("Button"), null));
            Assert.IsFalse(CarryActions.Press(Vt("Button"), null, "galaxy").Handled);
            Assert.IsFalse(CarryActions.Cancel(null).Handled);
        }
    }
}
