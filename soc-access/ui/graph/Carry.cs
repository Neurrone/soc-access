using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// Something the player has picked up and is holding: a ship being moved between fleets, a
    /// population unit being moved between planets.
    ///
    /// The NAME is captured here, at pick-up, and never re-derived. The whole point of carrying is
    /// that the thing leaves where it was: by the time it is dropped, the row it was read off may
    /// be gone, may have been recycled onto another item, or may be drawn on a screen the player
    /// has left. A name resolved at drop time is therefore a name for something else.
    /// </summary>
    public sealed class CarryItem
    {
        /// <summary>The game's own object - what a drop actually acts on, and the identity two
        /// carries are compared by.</summary>
        public readonly object Cargo;

        /// <summary>
        /// What to call it, in the game's words, as they were when it was picked up - and the ONE
        /// place the drag's wording is decided, since every drag phrase (the pick-up announcement,
        /// both derived hints, a drop's own report) says the thing by this name.
        ///
        /// A cargo measured in UNITS composes its count into this name and states it every time,
        /// including one ("Imperials x 1"), because the rows of a population ring hand over different
        /// numbers and the count is what tells them apart. Cargo that is a single thing - a module, a
        /// ship, a queue line, a tactic card - names itself plainly. Which it is is the source's
        /// answer, made here at pick-up: nothing downstream re-derives it, and no flag has to travel
        /// with the carry.
        /// </summary>
        public readonly string Name;

        /// <summary>Which sort of thing this is ("ship", "population"). A control takes cargo of one
        /// kind, so a ship cannot be dropped into a planet's population.</summary>
        public readonly string Kind;

        /// <summary>
        /// HOW MANY of the thing one press picked up, captured at pick-up like the name and for the
        /// same reason.
        ///
        /// One is the answer for everything that is picked up whole - a ship, a queue line - and the
        /// default, so a source that moves single things says nothing about it. Where the game's own
        /// drag carries a variable amount (a population marker carries itself and every marker of the
        /// same people after it round the ring), the source works out what THAT marker would carry and
        /// says so here, and every drop the item reaches moves that many rather than one.
        /// </summary>
        public readonly int Quantity;

        public CarryItem(object cargo, string name, string kind, int quantity = 1)
        {
            Cargo = cargo;
            Name = name;
            Kind = kind;
            Quantity = quantity < 1 ? 1 : quantity;
        }
    }

    /// <summary>
    /// What a drop target answered. A refusal is the GAME's refusal - the words its own check gives
    /// for why this cannot happen - never a rule the mod invented, and the carry survives it: the
    /// player is still holding the thing and can try somewhere else.
    /// </summary>
    public sealed class DropResult
    {
        public readonly bool Dropped;

        /// <summary>What to say. On a drop, the screen's own account of what happened (null falls
        /// back to "Dropped X"); on a refusal, the game's reason (null falls back to "X cannot go
        /// there").</summary>
        public readonly string Message;

        private DropResult(bool dropped, string message)
        {
            Dropped = dropped;
            Message = message;
        }

        /// <summary>It happened. <paramref name="message"/> is the screen's own words for it.</summary>
        public static DropResult Done(string message = null)
        {
            return new DropResult(true, message);
        }

        /// <summary>The game said no, in <paramref name="reason"/>'s words. The player keeps
        /// carrying.</summary>
        public static DropResult Refused(string reason = null)
        {
            return new DropResult(false, reason);
        }
    }

    /// <summary>
    /// What the player is carrying, if anything - the whole of the pick-up-and-drop mode.
    ///
    /// An instance rather than a static so it dies with the mod on a hot reload, and engine-free so
    /// the rules below are unit-tested off the game. The screens declare which controls can be
    /// picked up and which will take a drop (<see cref="NodeVtable.OnPickUp"/>,
    /// <see cref="NodeVtable.OnDrop"/>); everything about what a key press MEANS is decided in
    /// <see cref="CarryActions"/>, and what a control SAYS about dragging is derived from those same two
    /// declarations (<see cref="DraggablePart"/>, <see cref="DropTargetPart"/>, added to every control's
    /// readout by <c>GraphAnnouncer.EffectiveAnnouncements</c>) - no screen writes the words.
    ///
    /// The carry belongs to the screen it started on (<see cref="Owner"/>): its drop targets are
    /// there, so a player who walks off to another page is no longer carrying anything. A menu
    /// opened OVER that screen does not count as leaving it - see
    /// <see cref="ScreenChanged"/>.
    /// </summary>
    public sealed class CarryState
    {
        /// <summary>
        /// Which ACTIONS the three carry gestures are, as the host's input manager knows them.
        ///
        /// Core cannot see the input manager, so a sentence naming a chord names an action and a
        /// binding index and lets <see cref="NodeHints.Chord"/> spell it - the usage-hint contract,
        /// reused here so a re-bound gesture re-words the pick-up announcement and both derived hints
        /// with nothing to keep in step. The defaults are the shipped action keys; the host may set
        /// them, and a test may point them anywhere.
        /// </summary>
        public static string PickUpAction = "ui.carry";

        public static string DropAction = "ui.activate";

        public static string CancelAction = "ui.back";

        /// <summary>
        /// The carry's LIFECYCLE, for a host that wants to do something around it that Core cannot
        /// do itself - playing the noises the GAME's own mouse drag makes is what these exist for,
        /// and which noise that is (or whether there is one at all) is the host's answer, never a
        /// rule in here. Told WHAT was picked up or put down, so a host that cares about one sort of
        /// cargo only can read the item's kind and stay out of the way for the rest.
        ///
        /// <see cref="Started"/> is raised by every press that takes something up, the re-pick that
        /// swaps what is held included - each is a new carry beginning. <see cref="Ended"/> is
        /// raised by every ending the PLAYER performed: a drop that landed, a drop the game refused
        /// (the carry survives that one, and it is still an attempt ending), and a give-up. A carry
        /// that merely lapses because the player walked off the page
        /// (<see cref="ScreenChanged"/>) raises nothing, for the reason it says nothing: the player
        /// did not end it, they left.
        ///
        /// An observer that throws costs its own effect and never the carry.
        /// </summary>
        public Action<CarryItem> Started;

        public Action<CarryItem> Ended;

        /// <summary>What is being carried, or null.</summary>
        public CarryItem Held { get; private set; }

        /// <summary>The screen the carry started on. Opaque here - the adapter knows what a screen
        /// is.</summary>
        public object Owner { get; private set; }

        public bool IsCarrying
        {
            get { return Held != null; }
        }

        /// <summary>Whether what is being carried is of <paramref name="kind"/> - the question a
        /// drop target asks to know whether it is one right now.</summary>
        public bool Accepts(string kind)
        {
            return Held != null && Held.Kind == kind;
        }

        public void PickUp(CarryItem item, object owner)
        {
            Held = item;
            Owner = item == null ? null : owner;
        }

        public void Clear()
        {
            Held = null;
            Owner = null;
        }

        /// <summary>
        /// The focused screen changed. <paramref name="stillOnOwnersPage"/> is the adapter's answer
        /// to "is the screen the carry started on still the page the player is on" - true while a
        /// menu or a child screen is open over it. False drops the carry, silently: the player went
        /// somewhere the thing they were holding cannot be put down.
        /// </summary>
        public void ScreenChanged(bool stillOnOwnersPage)
        {
            if (IsCarrying && !stillOnOwnersPage)
            {
                Clear();
            }
        }

        /// <summary>
        /// Whether a control would TAKE what is being held right now: the right sort of place for this
        /// cargo, and - where the screen declared a test of its own for the targets among a family that
        /// will refuse - that test too (<see cref="NodeVtable.DropAccepts"/>).
        /// </summary>
        public bool Takes(NodeVtable vtable)
        {
            return vtable != null
                && vtable.OnDrop != null
                && Accepts(vtable.DropKind)
                && (vtable.DropAccepts == null || vtable.DropAccepts(Held));
        }

        /// <summary>
        /// What a control the player could pick something up from says while NOTHING is being carried -
        /// the only announcement of the pick-up key there is.
        ///
        /// It goes quiet the moment something is held: the player is then hunting for somewhere to put
        /// that thing down, and being told the control under the cursor could also be picked up is noise.
        /// A control that is both source and target therefore says "drop target" mid-drag and nothing
        /// else.
        ///
        /// The pick-up command answers for itself whether there is anything to give right now - an empty
        /// slot, a foreign ship, a population the game will not let leave - so the word cannot promise a
        /// gesture that would do nothing. That is why the command must be a pure query
        /// (<see cref="NodeVtable.OnPickUp"/>) and why this part is NOT live: it is asked when a readout
        /// is composed, never per frame, and a word appearing on its own after a drag was cancelled
        /// would be noise on top of the gesture that already said what happened.
        /// </summary>
        public NodeAnnouncement DraggablePart(NodeVtable vtable)
        {
            if (vtable == null || vtable.OnPickUp == null)
            {
                return null;
            }

            NodeVtable it = vtable;
            return new NodeAnnouncement(
                () =>
                    Held == null && it.OnPickUp() != null
                        ? ModText.Get(ModStrings.UI.StatusDraggable)
                        : null
            );
        }

        /// <summary>
        /// The two DERIVED usage hints, appended to every control's review buffer by
        /// <see cref="NodeBuffer"/> after the hints a screen declared by hand.
        ///
        /// They are derived here, from the same two declarations everything else about the carry is
        /// derived from, for the reason the "draggable" and "drop target" words are: which controls
        /// can be picked up and which will take a drop is already written down in the vtable, so no
        /// screen wires a sentence and EVERY screen with a drag has both - the fleet lists, the two
        /// queues, the ship designer's slots, the population rings.
        ///
        /// The pick-up hint is gated exactly as <see cref="DraggablePart"/> is (nothing held, and the
        /// control's own pure query really has something to give), and it names what would be picked
        /// up in the query's own words, quantity included - which is the only place a player can learn
        /// that this marker carries three people and the next one carries one. The drop hint is gated
        /// on <see cref="Takes"/>, so it inherits the target's own
        /// <see cref="NodeVtable.DropAccepts"/> and never promises a drop the game would refuse; it is
        /// live, appearing and disappearing as the player picks things up.
        ///
        /// Never both: holding something silences the pick-up half, exactly as the readout's words do.
        /// The chords are rendered through the injected <see cref="NodeHints.Chord"/>, so a re-bound
        /// gesture re-words both, and with no renderer neither line exists at all.
        /// </summary>
        public void HintLines(List<string> into, NodeVtable vtable)
        {
            Func<string, int, string> render = NodeHints.Chord;
            if (into == null || vtable == null || render == null)
            {
                return;
            }

            try
            {
                if (Held == null)
                {
                    if (vtable.OnPickUp == null)
                    {
                        return;
                    }

                    CarryItem offer = vtable.OnPickUp();
                    string pickUp = offer == null ? null : render(PickUpAction, 0);
                    if (!string.IsNullOrEmpty(pickUp))
                    {
                        into.Add(
                            ModText.Get(ModStrings.Graph.DragHint, pickUp, offer.Name)
                        );
                    }

                    return;
                }

                if (!Takes(vtable))
                {
                    return;
                }

                string drop = render(DropAction, 0);
                if (!string.IsNullOrEmpty(drop))
                {
                    into.Add(ModText.Get(ModStrings.Graph.DragDropHint, drop, Held.Name));
                }
            }
            catch (Exception)
            {
                // A hint is the least important thing in a buffer: a source query or an acceptance
                // test that throws costs the player one sentence, never the content it was appended to.
            }
        }

        /// <summary>
        /// The state word a control that would TAKE the carried thing says while focused. Live, so it
        /// appears and disappears under a cursor left standing on the target while the player picks
        /// something up or puts it down. Says nothing when nothing compatible is being carried, which is
        /// every other moment of the game.
        /// </summary>
        public NodeAnnouncement DropTargetPart(NodeVtable vtable)
        {
            if (vtable == null || vtable.OnDrop == null)
            {
                return null;
            }

            NodeVtable it = vtable;
            return new NodeAnnouncement(
                () => Takes(it) ? ModText.Get(ModStrings.Graph.DragDropTarget) : null,
                true
            );
        }
    }

    /// <summary>What a carry key press did, and what to say about it. Composed here rather than in
    /// the navigator so the whole decision - including its wording - is testable off the game.</summary>
    public sealed class CarryOutcome
    {
        /// <summary>Whether the key was ours. False means the mod has no business with it here and
        /// the game should get it.</summary>
        public readonly bool Handled;

        /// <summary>What to speak, interrupting, or null.</summary>
        public readonly string Speech;

        public CarryOutcome(bool handled, string speech)
        {
            Handled = handled;
            Speech = speech;
        }

        public static readonly CarryOutcome NotOurs = new CarryOutcome(false, null);
    }

    /// <summary>
    /// What the two carry gestures do on the control the player is standing on. One decision table,
    /// in one place, because each key means several things depending on what is being held.
    ///
    /// The carry key (<see cref="Press"/>) is the one that HOLDS things:
    ///
    ///   nothing held, control offers something  -> pick it up
    ///   something held, control offers another  -> carry that one instead
    ///   something held, control offers this one -> drag it again, and say so
    ///   something held, control offers neither  -> nothing, silently
    ///
    /// THERE IS NO PUT-BACK (owner ruling 2026-08-29). The key used to end the carry when it landed
    /// back on the control the thing came from, which read as a cancel the player had not asked for:
    /// once a source can hand over DIFFERENT amounts of the same thing - a population marker carries
    /// itself and every marker after it - pressing the key again on the same slot is how a player asks
    /// for that slot's amount, not how they give up. So every press on a source picks up, the same
    /// slot included, and the re-pick simply re-announces what is now held. The back key is the one
    /// cancel.
    ///
    /// The activation key (<see cref="Activate"/>) is the one that PUTS THEM DOWN: on a control that
    /// will take what is held it drops there, through the GAME's own check, and everywhere else it
    /// was never ours - the control does its own click and the carry simply survives it. That split
    /// is what keeps a carry from being a mode the player is trapped in: normal navigation and
    /// normal activation go on working, and only a drop, a put-back, the back key or leaving the
    /// page ends it.
    ///
    /// A refused drop keeps the carry: the player hears why, still holding the thing, and can try
    /// somewhere else. A control that is neither source nor target CONSUMES the carry key while a
    /// carry is up - the carry is the mode the player is in - and answers with silence, because
    /// looking for the target is done by pressing the key along a row of controls and a cue on each
    /// of them is noise. Where nothing is being carried the carry key was never ours and the game
    /// gets it.
    /// </summary>
    public static class CarryActions
    {
        /// <summary>
        /// Whether the carry key belongs to the mod on this control: the same question the dispatch
        /// below answers, asked BEFORE the press so the game can be told to stand down from it (the
        /// key is the game's own everywhere else). Never speaks and changes nothing.
        /// </summary>
        public static bool Claims(NodeVtable vtable, CarryState carry)
        {
            if (carry != null && carry.IsCarrying)
            {
                return true;
            }

            return vtable != null && vtable.OnPickUp != null;
        }

        /// <summary>The carry key, pressed on the control <paramref name="vtable"/> describes.</summary>
        public static CarryOutcome Press(NodeVtable vtable, CarryState carry, object owner)
        {
            if (carry == null)
            {
                return CarryOutcome.NotOurs;
            }

            if (!carry.IsCarrying)
            {
                return vtable != null && vtable.OnPickUp != null
                    ? PickUp(vtable, carry, owner)
                    : CarryOutcome.NotOurs;
            }

            if (vtable != null && vtable.OnPickUp != null)
            {
                return PickUp(vtable, carry, owner);
            }

            // Held something, and this control has nothing to give - including a control that would
            // TAKE it, which is the activation key's business and not this one's. Claimed - the carry
            // is a mode and the key belongs to it - but silent: while carrying, the key is pressed on
            // control after control looking for the one that will hand over something else, and a cue
            // on each of them is noise. The carry survives, which is what the player is listening for.
            return new CarryOutcome(true, null);
        }

        /// <summary>
        /// The activation key, pressed while something is held. Handled - and a drop - only on a
        /// control that will take THIS cargo; anywhere else the answer is
        /// <see cref="CarryOutcome.NotOurs"/> and the control's own click runs, with the carry still
        /// live. That is deliberate: the player has to be able to walk a page and use it while
        /// holding something, and the destination is confirmed with the same key that confirms
        /// everything else.
        ///
        /// "Will take this cargo" is DELIBERATELY WEAKER here than the question the readout asks: the
        /// right kind of drop target, without the screen's own <see cref="NodeVtable.DropAccepts"/>.
        /// A control that will not take the cargo says nothing about being a target, and still
        /// answers the key with the GAME's own reason for refusing - which is the useful half of a
        /// locked battle-tactics slot ("This tactic is locked") and the reason a screen writes such a
        /// test at all. The cost is that a target whose refusal has no words answers a press with the
        /// mod's generic sentence, so a screen whose gate can say no owes its drop a reason or owes
        /// its player a gate that cannot be reached by accident (the empire page's shipping row is
        /// the second kind: its gate now asks the same numbers the clamp does, so nothing advertises
        /// a ship that would carry nobody).
        /// </summary>
        public static CarryOutcome Activate(NodeVtable vtable, CarryState carry)
        {
            if (carry == null || !carry.IsCarrying)
            {
                return CarryOutcome.NotOurs;
            }

            CarryItem held = carry.Held;
            return vtable != null && vtable.OnDrop != null && vtable.DropKind == held.Kind
                ? Drop(vtable, carry, held)
                : CarryOutcome.NotOurs;
        }

        /// <summary>Give up the carry - what the back key does while something is held. Not handled
        /// when nothing is: the key then means whatever it always meant.</summary>
        public static CarryOutcome Cancel(CarryState carry)
        {
            if (carry == null || !carry.IsCarrying)
            {
                return CarryOutcome.NotOurs;
            }

            CarryItem held = carry.Held;
            carry.Clear();
            Raise(carry.Ended, held);
            return new CarryOutcome(true, ModText.Get(ModStrings.Graph.DragCancelled));
        }

        /// <summary>Tell a lifecycle observer, if there is one. Guarded because an observer is
        /// something hung on the SIDE of the carry: whatever it does, the press it is watching still
        /// has to do what it said it would.</summary>
        private static void Raise(Action<CarryItem> observer, CarryItem item)
        {
            if (observer == null)
            {
                return;
            }

            try
            {
                observer(item);
            }
            catch (Exception)
            {
            }
        }

        private static CarryOutcome PickUp(NodeVtable vtable, CarryState carry, object owner)
        {
            CarryItem item = vtable.OnPickUp();
            if (item == null)
            {
                // The control can be a source and still have nothing to give right now (an empty
                // slot, a ship the game will not release). Silent, like every other gesture key with
                // nothing to do: the key is pressed speculatively along a row, and a cue on each
                // press is noise rather than reassurance.
                return new CarryOutcome(true, null);
            }

            // No put-back branch: a press on the control the thing came from picks it up AGAIN (see the
            // decision table above). Where the source hands over the same amount that is a harmless
            // re-announce; where it hands over a different one - a population marker further round the
            // ring - it is how the player asks for that amount.
            carry.PickUp(item, owner);
            Raise(carry.Started, item);
            return new CarryOutcome(true, Carrying(item));
        }

        /// <summary>
        /// What a pick-up says: what is now held, and BOTH ways out of holding it (owner ruling
        /// 2026-08-29).
        ///
        /// The carry is the one mode the mod puts a player into without a surface to look at, and the
        /// gestures that end it are on two different keys - the drop is the activation key on a target,
        /// the cancel is the back key anywhere. A player who has just picked something up is exactly
        /// the player who needs both, so the announcement teaches them, every time, the override
        /// re-pick included. The chords are spelled by the injected renderer rather than written into
        /// the sentence, so re-binding either gesture re-words it; where there is no renderer at all
        /// (a test, boot, a host with no keyboard) the sentence falls back to naming what is held,
        /// which is the whole of what the mod knows then.
        /// </summary>
        private static string Carrying(CarryItem item)
        {
            Func<string, int, string> render = NodeHints.Chord;
            string drop = null;
            string cancel = null;
            if (render != null)
            {
                try
                {
                    drop = render(CarryState.DropAction, 0);
                    cancel = render(CarryState.CancelAction, 0);
                }
                catch (Exception)
                {
                    drop = null;
                    cancel = null;
                }
            }

            return string.IsNullOrEmpty(drop) || string.IsNullOrEmpty(cancel)
                ? ModText.Get(ModStrings.Graph.DragStartedPlain, item.Name)
                : ModText.Get(ModStrings.Graph.DragStarted, item.Name, drop, cancel);
        }

        private static CarryOutcome Drop(NodeVtable vtable, CarryState carry, CarryItem held)
        {
            DropResult result = vtable.OnDrop(held);
            if (result == null || !result.Dropped)
            {
                Raise(carry.Ended, held);
                string refusal = result == null ? null : result.Message;
                return new CarryOutcome(
                    true,
                    string.IsNullOrEmpty(refusal)
                        ? ModText.Get(ModStrings.Graph.DragDropRefused, held.Name)
                        : refusal
                );
            }

            carry.Clear();
            Raise(carry.Ended, held);
            return new CarryOutcome(
                true,
                string.IsNullOrEmpty(result.Message)
                    ? ModText.Get(ModStrings.Graph.DragDropped, held.Name)
                    : result.Message
            );
        }
    }
}
