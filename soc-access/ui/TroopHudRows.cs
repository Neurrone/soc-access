using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common.Localization;
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
                it.PortraitTooltip,
                () => it.FocusPortrait(),
                it.Troops);
        }

        /// <summary>
        /// The same stop built from the PARTS, for a band the game draws without a
        /// <c>WielderInteractHeader</c> over it - the defence menu's stored wielder, whose portrait and
        /// army hang off <c>DefencePanelWielder</c> instead.
        /// </summary>
        public static void WielderStop(
            GraphBuilder builder,
            object stopKey,
            string keyPrefix,
            Component portrait,
            Func<string> wielderName,
            Tooltip portraitTooltip,
            Action focusPortrait,
            TroopHudAdapter troops)
        {
            if (builder == null || troops == null)
            {
                return;
            }

            builder.BeginStop(stopKey);
            AddPortrait(builder, keyPrefix, portrait, wielderName, portraitTooltip, focusPortrait);

            string caption = TroopsCaption();
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

        /// <summary>The page's name where its wielder band draws the banner naming the place the
        /// wielder has walked into: the page's own title and that name, since the portrait row says
        /// the wielder alone. The banner is dropped where the title is already the same words.
        /// </summary>
        public static string NameWithPlace(string title, WielderInteract wielder)
        {
            return NameWithPlace(title, wielder == null || !wielder.IsPresent ? null : wielder.CustomName);
        }

        /// <summary>A page's name and the second name the page draws with it, as one: the settlement's
        /// building and the name the player gave it, the defence menu's title and its subtitle, a
        /// wielder's page and the place it has walked into. The second is dropped where it is already
        /// the same words, and where either is empty the other stands alone.</summary>
        public static string NameWithPlace(string title, string place)
        {
            if (string.IsNullOrWhiteSpace(place) || SameText(title, place))
            {
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }

            return string.IsNullOrWhiteSpace(title)
                ? place
                : ModText.Get(ModStrings.Common.ListSeparator, title, place);
        }

        private static bool SameText(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.CurrentCultureIgnoreCase);
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

            // The game's own drag noise, for the keyboard's carry. Made once per load - the
            // delegates are over this assembly and must not outlive it, and CarrySounds.Reset on
            // Stop is what ends them; the next build after one asks again.
            if (!CarrySounds.Has(TroopCargo))
            {
                CarrySounds.Register(TroopCargo, () => NativeSoundUtility.PostEvent(PickUpSound), null);
            }

            IReadOnlyList<TroopHudAdapter.SlotItem> slots = troops.GetSlots();
            bool open = available == null || available();
            List<NodeDeclaration> nodes = Kept(slots, rowPrefix, available != null, open);
            if (nodes == null)
            {
                nodes = new List<NodeDeclaration>(slots.Count);
                for (int i = 0; i < slots.Count; i++)
                {
                    AddRow(nodes, troops, slots[i], rowPrefix + i, available, open);
                }

                Keep(slots, rowPrefix, available != null, open, nodes);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                builder.AddItem(nodes[i]);
            }
        }

        /// <summary>
        /// ONE BAR'S ROWS, KEPT BETWEEN BUILDS, keyed on the list the adapter answered with.
        ///
        /// A row is a vtable, ten closures and up to two usage hints, every one of which reads the
        /// game when it is READ, so rebuilding a bar whose slots have not moved bought nothing but
        /// the allocation - and a fresh troop tooltip per occupied row per frame with it. The
        /// adapter hands back the same list while the bar has not moved
        /// (<see cref="TroopHudAdapter.GetSlots"/>), which is the whole key to the slots' half of it.
        ///
        /// The rest of the key is the CALLER'S, because two things it passes decide what a row is
        /// built as: the key prefix the rows are named under, and whether the bar can be worked at
        /// all - the hostile join offer draws one that is locked until the offer is answered, and
        /// its rows carry neither drop nor pick-up while it is.
        ///
        /// Held against the slot list rather than by the screen, because nine screens call this and
        /// a contributor has no field of theirs to sit in. The table holds the list WEAKLY, so a
        /// bar's rows die with the adapter that answered for it and nothing has to say when a menu
        /// closed.
        /// </summary>
        private sealed class Bar
        {
            public string Prefix;

            public bool Gated;

            public bool Open;

            public List<NodeDeclaration> Nodes;
        }

        private static readonly ConditionalWeakTable<object, Bar> Bars = new ConditionalWeakTable<object, Bar>();

        private static List<NodeDeclaration> Kept(object slots, string prefix, bool gated, bool open)
        {
            Bar bar;
            return Bars.TryGetValue(slots, out bar)
                && bar.Gated == gated
                && bar.Open == open
                && string.Equals(bar.Prefix, prefix, StringComparison.Ordinal)
                ? bar.Nodes
                : null;
        }

        private static void Keep(object slots, string prefix, bool gated, bool open, List<NodeDeclaration> nodes)
        {
            Bars.Remove(slots);
            Bars.Add(slots, new Bar { Prefix = prefix, Gated = gated, Open = open, Nodes = nodes });
        }

        // The game's own word for an army, asked once per language instead of once per band per
        // frame: WielderStop runs on every build of eight screens and the lookup is I2's, which is
        // a key probe, a table read and a format, not a dictionary hit.
        private static ILanguageDefinition _captionLanguage;

        private static string _caption;

        private static string TroopsCaption()
        {
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            ILanguageDefinition language = localization != null ? localization.CurrentLanguage : null;
            if (_caption == null || !ReferenceEquals(language, _captionLanguage))
            {
                _captionLanguage = language;
                _caption = GameText.Get("Commanders/Tooltip/Troops", string.Empty) ?? string.Empty;
            }

            return _caption;
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

        /// <summary>The wielder the band is about: their name ALONE (owner ruling 2026-09-08), with
        /// the stats the game draws on their portrait behind it in the buffer. The banner over the
        /// portrait names the place, not the wielder, and belongs to the page's own name
        /// (<see cref="NameWithPlace"/>). Focusing the row selects the portrait, which is what makes
        /// the game draw those stats.</summary>
        private static void AddPortrait(
            GraphBuilder builder,
            string keyPrefix,
            Component portrait,
            Func<string> wielderName,
            Tooltip portraitTooltip,
            Action focusPortrait)
        {
            if (portrait == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Text(wielderName, null, portraitTooltip);
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
        /// right here. An EMPTY slot is a line and not a button (owner ruling 2026-09-08): the game
        /// wires the entry's click to the troop in it, so there is nothing to press, only somewhere a
        /// carried troop can be dropped.
        ///
        /// THE ROW IS BUILT ONCE PER SLOT LIST, not once per frame (<see cref="Bar"/>), so everything
        /// written here is either a closure that reads the game when it is read - which the label,
        /// the click, the drop, the pick-up and the two gates all are - or a fact that cannot change
        /// while the slot list and the caller's gate do not. <c>NodeHints.Add</c> APPENDS, so a
        /// vtable must never be passed through here twice.</summary>
        private static void AddRow(
            List<NodeDeclaration> into,
            TroopHudAdapter troops,
            TroopHudAdapter.SlotItem slot,
            string key,
            Func<bool> available,
            bool open)
        {
            TroopHudAdapter.SlotItem it = slot;
            Func<bool> workable = available;
            Func<bool> enabled = () => it.IsUnlocked && (workable == null || workable());
            NodeVtable vtable = it.IsOccupied
                ? GraphNodes.Button(() => Label(it), () => it.Click(), enabled, it.Details)
                : GraphNodes.Text(() => Label(it));
            if (!it.IsOccupied)
            {
                vtable.Announcements.Add(GraphNodes.DisabledPart(enabled));
            }

            vtable.Announcements[0].Live = true;
            // Selecting the entry is what makes the game draw the troop's details for it.
            vtable.OnFocusVisual = () => it.Focus();

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

            into.Add(new DrawnNode(ControlId.For(it.Entry, key), vtable, it.Entry));
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
