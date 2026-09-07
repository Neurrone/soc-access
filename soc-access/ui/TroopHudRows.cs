using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using DropResult = SongsOfConquestAccess.UI.Graph.DropResult;
using NativeDropResult = SongsOfConquestAccess.Adapters.TroopHudAdapter.DropResult;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The wielder's army, as the rows every screen that draws a <c>TroopHUD</c> reads it - a
    /// CONTRIBUTOR rather than a screen: it owns one cluster of stops and each page calls it where its
    /// own layout puts the band (Endless Space 2 Access's "a persistent overlay is a contributor, not
    /// a screen"). The market, the trade, the settlement, the defence menu, the hostile join offer and
    /// the world choice menu all draw the same band; the adventure map draws the same rows with no
    /// portrait over them, which is why <see cref="Rows"/> stands on its own.
    ///
    /// EVERY GESTURE IS THE GAME'S OWN, delivered into the game's own handlers so its rules decide
    /// what happens:
    ///
    /// - Enter is the entry's left click, which does nothing: the game begins its drag on button DOWN
    ///   (<c>TroopHUDEntry.HandleButtonDown</c>), so a click on a troop is inert, and that is faithful.
    /// - Backslash is the entry's right click, which the game answers with its own disband confirm
    ///   dialog - and only where its own <c>CanDisband()</c> allows, which is where the hint is said.
    /// - Space picks a troop up. A drop is the game's whole drag replayed in one call
    ///   (<c>TroopHudAdapter.DropOn</c>): onto an empty slot or onto the same troop the game opens its
    ///   own split popup (<c>MoveTroopPopupScreen</c>), onto a different troop it swaps.
    /// - Ctrl+Enter is the same drop with the physical Ctrl the game's own mouse-up branch reads: the
    ///   whole stack onto an empty slot, everything that fits onto the same troop, and on the troop's
    ///   OWN slot the force split into the next unlocked slot.
    /// - Ctrl+1 to Ctrl+0 are the game's quick splits, which act on the troop the pointer is over; on
    ///   a troop row the mod claims the chord and hands the game the row as its hovered entry.
    ///
    /// A slot the bar draws with a lock on it is NOT a row (owner ruling 2026-09-08): a wielder who
    /// has not unlocked it cannot use it for anything, so the rows count only the slots the wielder
    /// has.
    /// </summary>
    public static class TroopHudRows
    {
        /// <summary>What is carried between troop slots.</summary>
        public const string TroopCargo = "troop";

        /// <summary>The noise the game itself makes when a drag of a troop begins
        /// (<c>TroopHUDEntryMovable.BeginDrag</c>). The end and move noises are the native drop's.
        /// </summary>
        public const string PickUpSound = "Adventure_HUDBeginDragTroop";

        /// <summary>
        /// The whole Wielder stop, the shape every wielder band takes: the portrait row naming the
        /// wielder, then the army under the game's own word for it.
        /// </summary>
        /// <param name="keyPrefix">The screen's own prefix for these controls
        /// ("world-choice:wielder"); the rows are keyed under it.</param>
        public static void WielderStop(
            GraphBuilder builder,
            object stopKey,
            string keyPrefix,
            WielderInteract wielder)
        {
            if (builder == null || wielder == null || !wielder.IsPresent)
            {
                return;
            }

            WielderInteract it = wielder;
            WielderStop(
                builder,
                stopKey,
                keyPrefix,
                it.Portrait,
                () => it.WielderName,
                () => it.CustomName,
                it.PortraitTooltip,
                () => it.FocusPortrait(),
                it.Troops);
        }

        /// <summary>
        /// The same stop built from the PARTS, for a band the game draws without a
        /// <c>WielderInteractHeader</c> over it - the defence menu's stored wielder, whose portrait and
        /// army hang off <c>DefencePanelWielder</c> instead.
        /// </summary>
        /// <param name="customName">The banner naming the place the wielder walked into, where the band
        /// draws one; null where it draws none.</param>
        public static void WielderStop(
            GraphBuilder builder,
            object stopKey,
            string keyPrefix,
            Component portrait,
            Func<string> wielderName,
            Func<string> customName,
            Tooltip portraitTooltip,
            Action focusPortrait,
            TroopHudAdapter troops)
        {
            if (builder == null || troops == null)
            {
                return;
            }

            builder.BeginStop(stopKey);
            AddPortrait(builder, keyPrefix, portrait, wielderName, customName, portraitTooltip, focusPortrait);

            string caption = GameText.Get("Commanders/Tooltip/Troops", string.Empty);
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(keyPrefix + ":troops");
            }

            Rows(builder, troops, RowPrefix(keyPrefix));

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>The rows alone, one per drawn slot in the order the bar draws them, declared into
        /// whatever stop and context the caller has opened.
        ///
        /// <paramref name="available"/> is a bar the game has drawn but LOCKED - the hostile join
        /// offer, whose overlay covers the army until the offer is accepted. Its rows still read as
        /// rows, and say "unavailable" as well; nothing may be picked up from them or dropped on them.
        /// Null, the usual answer, is a bar that can be worked.</summary>
        public static void Rows(
            GraphBuilder builder,
            TroopHudAdapter troops,
            string rowPrefix,
            Func<bool> available = null)
        {
            if (builder == null || troops == null)
            {
                return;
            }

            // The game's own drag noise, for the keyboard's carry. Registered on every build: the
            // registration is a delegate over this load and must not outlive it.
            CarrySounds.Register(TroopCargo, () => NativeSoundUtility.PostEvent(PickUpSound), null);

            IReadOnlyList<TroopHudAdapter.SlotItem> slots = troops.GetSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                AddRow(builder, troops, slots[i], rowPrefix + i, available);
            }
        }

        /// <summary>Where a screen's row keys start, so the same prefix names the rows and finds the
        /// focused one.</summary>
        public static string RowPrefix(string keyPrefix)
        {
            return keyPrefix + "/troop/";
        }

        /// <summary>
        /// Whether one of the game's quick-split chords belongs to the mod right now: only on an
        /// occupied troop row of this contributor's, where the mod can name the hovered entry the
        /// game's own handler needs. A screen answers <c>GraphScreen.ClaimsAction</c> with this.
        /// </summary>
        public static bool ClaimsAction(
            string actionKey,
            GraphNavigator navigator,
            TroopHudAdapter troops,
            string keyPrefix)
        {
            return AccessibilityActions.TroopSplitSize(actionKey) > 0
                && FocusedRow(navigator, troops, keyPrefix) != null;
        }

        /// <summary>Run the quick split the chord names, on the focused row. Nothing is spoken: what
        /// the game did is read off the rows' own live labels.</summary>
        public static bool OnAction(
            string actionKey,
            GraphNavigator navigator,
            TroopHudAdapter troops,
            string keyPrefix)
        {
            int size = AccessibilityActions.TroopSplitSize(actionKey);
            TroopHudAdapter.SlotItem row = size > 0 ? FocusedRow(navigator, troops, keyPrefix) : null;
            return row != null && troops.QuickSplit(row.Entry, size);
        }

        /// <summary>The occupied row the cursor is on, or null anywhere else - read off the focused
        /// node's key rather than off a remembered index, since the cursor moves for reasons a screen
        /// never hears about.</summary>
        public static TroopHudAdapter.SlotItem FocusedRow(
            GraphNavigator navigator,
            TroopHudAdapter troops,
            string keyPrefix)
        {
            int index = navigator == null ? -1 : navigator.FocusedIndex(RowPrefix(keyPrefix));
            IReadOnlyList<TroopHudAdapter.SlotItem> slots = index < 0 || troops == null
                ? null
                : troops.GetSlots();
            if (slots == null || index >= slots.Count)
            {
                return null;
            }

            TroopHudAdapter.SlotItem slot = slots[index];
            return slot.IsUnlocked && slot.IsOccupied ? slot : null;
        }

        /// <summary>The wielder the band is about: their name, then the banner naming the place they
        /// walked into where the band draws one, with the stats the game draws on their portrait behind
        /// both in the buffer. Focusing it selects the portrait, which is what makes the game draw
        /// those stats.</summary>
        private static void AddPortrait(
            GraphBuilder builder,
            string keyPrefix,
            Component portrait,
            Func<string> wielderName,
            Func<string> customName,
            Tooltip portraitTooltip,
            Action focusPortrait)
        {
            if (portrait == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Text(wielderName, null, portraitTooltip);
            if (customName != null)
            {
                // The banner the band draws over the portrait when the place the wielder walked into
                // has a name of its own; watched, since the game writes it as the band is set up.
                vtable.Announcements.Add(GraphNodes.ValuePart(customName));
            }

            if (focusPortrait != null)
            {
                vtable.OnFocusVisual = () => focusPortrait();
            }

            builder.AddItem(new DrawnNode(
                ControlId.For(portrait, keyPrefix + "/portrait"),
                vtable,
                portrait));
        }

        /// <summary>One drawn slot: what is in it, and every gesture the game gives the troop there.
        /// The name is watched live - a split, a merge and a disband all happen under a cursor standing
        /// right here.</summary>
        private static void AddRow(
            GraphBuilder builder,
            TroopHudAdapter troops,
            TroopHudAdapter.SlotItem slot,
            string key,
            Func<bool> available)
        {
            TroopHudAdapter.SlotItem it = slot;
            Func<bool> workable = available;
            NodeVtable vtable = GraphNodes.Button(
                () => Label(it),
                () => it.Click(),
                () => it.IsUnlocked && (workable == null || workable()),
                it.IsOccupied ? it.Details : null);
            vtable.Announcements[0].Live = true;
            // Selecting the entry is what makes the game draw the troop's details for it.
            vtable.OnFocusVisual = () => it.Focus();

            bool open = workable == null || workable();
            if (it.IsUnlocked && open)
            {
                vtable.DropKind = TroopCargo;
                vtable.DropAccepts = held => troops.CanDropOn(Cargo(held), it.Entry);
                vtable.OnDrop = held => Drop(troops, held, it);
                NodeHints.Add(
                    vtable,
                    ModStrings.Screens.TroopMoveWholeHint,
                    AccessibilityActions.UiLeftClick.Key,
                    AccessibilityActions.UiLeftClickCtrlBindingIndex,
                    Carrying);
            }

            if (it.IsUnlocked && it.IsOccupied && open)
            {
                vtable.OnPickUp = () => new CarryItem(it.Entry, Label(it), TroopCargo);
                if (it.CanDisband)
                {
                    vtable.OnContextual = () => it.RightClick();
                }

                NodeHints.Add(
                    vtable,
                    ModStrings.Screens.TroopDisbandHint,
                    AccessibilityActions.UiRightClick.Key,
                    0,
                    () => it.CanDisband);
            }

            builder.AddItem(new DrawnNode(ControlId.For(it.Entry, key), vtable, it.Entry));
        }

        /// <summary>What a slot is called: the troop in it and how many of them, or the mod's word for
        /// an empty one. Where it sits is the graph's to say.</summary>
        private static string Label(TroopHudAdapter.SlotItem slot)
        {
            if (slot == null || !slot.IsOccupied)
            {
                return ModText.Get(ModStrings.Screens.Empty);
            }

            return slot.CurrentSize > 0 && slot.MaxSize > 0
                ? ModText.Get(ModStrings.UI.TroopWithSize, slot.TroopName, slot.CurrentSize, slot.MaxSize)
                : slot.TroopName;
        }

        /// <summary>The drop, through the game's own drag. A merge onto an empty slot or onto the same
        /// troop opens the game's split popup, which the detector pushes as its own screen and which
        /// says what it is itself.</summary>
        private static DropResult Drop(
            TroopHudAdapter troops,
            CarryItem held,
            TroopHudAdapter.SlotItem target)
        {
            TroopHUDEntry source = Cargo(held);
            if (source == null)
            {
                return DropResult.Refused();
            }

            NativeDropResult result = troops.DropOn(source, target.Entry);
            return result == NativeDropResult.Completed || result == NativeDropResult.MoveAmountPopupOpened
                ? DropResult.Done()
                : DropResult.Refused();
        }

        private static TroopHUDEntry Cargo(CarryItem held)
        {
            return held == null ? null : held.Cargo as TroopHUDEntry;
        }

        /// <summary>Whether a troop is being carried right now - the gate on the hint for the gesture
        /// that only exists while one is.</summary>
        private static bool Carrying()
        {
            GraphNavigator navigator = SocAccessMod.Instance == null ? null : SocAccessMod.Instance.Navigator;
            return navigator != null && navigator.Carry != null && navigator.Carry.Accepts(TroopCargo);
        }
    }
}
