using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;
using DropResult = SongsOfConquestAccess.UI.Graph.DropResult;
using NativeDropResult = SongsOfConquestAccess.Adapters.DropResult;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// A wielder's artifacts as rows - the equipment stop and the backpack stop every screen that
    /// draws an <c>InventoryHUD</c> puts up, a CONTRIBUTOR rather than a screen, as
    /// <see cref="TroopHudRows"/> is for the army.
    ///
    /// EVERY GESTURE ON AN ARTIFACT IS THE GAME'S OWN CLICK, delivered into the button the game hangs
    /// its handlers on (<c>InventoryArtifactMovable</c>'s <c>UIButton</c>), so the game's own rules
    /// decide what happens. Enter is the left click and Backslash the right one; the Ctrl chords are
    /// further bindings of those same two actions, because the game's handlers read the physical Ctrl
    /// for themselves. WHAT the clicks then do is the screen's business to say, which is why the
    /// hints are the caller's: on the sheet the left click is inert and Ctrl and the right click
    /// destroy, and in the market the left click selects the artifact for sale and Ctrl and the right
    /// click sell it.
    ///
    /// A SLOT IS A BUTTON ONLY WHERE ENTER DOES SOMETHING (owner ruling 2026-09-08): the market's
    /// occupied slots, whose left click the game answers. Every other slot - the sheet's and the
    /// trade's, and an empty one anywhere - is a line, with the same carry, drop and right-click
    /// gestures on it; those never needed the role.
    ///
    /// TWO-HANDERS: when the artifact in the main hand takes both hands, the game draws a ghost of it
    /// in the off hand, and the Main Hand and Off Hand nodes become ONE node carrying the game's own
    /// name for that slot ("Both Hands"). It picks up from the main hand, and a drop on it goes to the
    /// main hand except for an artifact that fits the off hand ALONE, which is the game's own
    /// right-click resolution copied for this one merged node.
    /// </summary>
    public static class ArtifactSlotNodes
    {
        /// <summary>What is carried between artifact slots.</summary>
        public const string ArtifactCargo = "artifact";

        /// <summary>The noise the game itself makes when a drag of an artifact begins
        /// (<c>InventoryArtifactMovable.OnBeginDrag</c>).</summary>
        public const string PickUpSound = "Adventure_InventoryPickupArtifact";

        /// <summary>The sentences a screen says about an occupied slot, in the order they read. The
        /// screen writes them because only it knows what the game's two clicks mean on it.</summary>
        public delegate void SlotHints(NodeVtable vtable, InventorySlotInfo slot);

        /// <summary>The game's own drag noise, for the keyboard's carry. Called on every build: the
        /// registration is a delegate over this load and must not outlive it.</summary>
        public static void RegisterSounds()
        {
            CarrySounds.Register(ArtifactCargo, () => NativeSoundUtility.PostEvent(PickUpSound), null);
        }

        /// <summary>The equipment column, under the game's own caption or the one the caller composed
        /// (a page with two wielders on it names each column after its owner): one node per drawn
        /// slot, with the two hands merged into one where the main hand holds a two-hander.</summary>
        public static void Equipment(
            GraphBuilder builder,
            IArtifactSlots slots,
            string keyPrefix,
            SlotHints hints,
            string caption = null)
        {
            IReadOnlyList<InventorySlotInfo> drawn = slots == null ? null : slots.GetEquipmentSlots();
            if (builder == null || drawn == null || drawn.Count == 0)
            {
                return;
            }

            bool merged = slots.IsMainHandTwoHanded();
            InventorySlotInfo mainHand = Find(drawn, InventorySlot.MainHand);
            InventorySlotInfo offHand = Find(drawn, InventorySlot.OffHand);

            builder.PushContext(caption ?? slots.EquipmentLabel);
            for (int i = 0; i < drawn.Count; i++)
            {
                InventorySlotInfo slot = drawn[i];
                if (merged && slot.Slot == InventorySlot.OffHand)
                {
                    // The game draws a ghost of the two-hander here; the one node below stands for
                    // both hands.
                    continue;
                }

                if (merged && slot.Slot == InventorySlot.MainHand)
                {
                    AddSlot(builder, slots, mainHand, keyPrefix + ":equipment/both-hands", slots.BothHandsSlotName, offHand, hints);
                    continue;
                }

                AddSlot(builder, slots, slot, keyPrefix + ":equipment/" + slot.Slot, slot.SlotName, null, hints);
            }

            builder.PopContext();
        }

        /// <summary>The backpack, under the game's own caption or the one the caller composed:
        /// auto-arrange, then one node per drawn cell.</summary>
        public static void Inventory(
            GraphBuilder builder,
            IArtifactSlots slots,
            string keyPrefix,
            SlotHints hints,
            object autoArrangeMarker,
            string caption = null)
        {
            IReadOnlyList<InventorySlotInfo> cells = slots == null ? null : slots.GetBackpackSlots();
            if (builder == null || cells == null)
            {
                return;
            }

            builder.PushContext(caption ?? slots.InventoryLabel);
            AddAutoArrange(builder, slots, keyPrefix, autoArrangeMarker);
            for (int i = 0; i < cells.Count; i++)
            {
                AddSlot(builder, slots, cells[i], keyPrefix + ":inventory/" + i, null, null, hints);
            }

            builder.PopContext();
        }

        /// <summary>Auto-arrange, the game's own middle click, as a button at the top of the backpack.
        /// It sits in a row of its own that COUNTS NOTHING, so the positions the slots under it say are
        /// the positions of the backpack ("5 of 16") rather than places in a list that has a button at
        /// the top of it.</summary>
        private static void AddAutoArrange(
            GraphBuilder builder,
            IArtifactSlots slots,
            string keyPrefix,
            object marker)
        {
            string label = OneLine(slots.AutoArrangeText);
            if (marker == null || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(() => label, () => slots.AutoArrangeArtifacts());
            builder.StartRow(keyPrefix + ":auto-arrange", positions: false);
            builder.AddItem(new SyntheticNode(ControlId.For(marker, keyPrefix + ":auto-arrange"), vtable));
            builder.EndRow();
        }

        /// <summary>
        /// One slot: what is in it, where it is, and every gesture the game gives the artifact there.
        ///
        /// The name is what the slot HOLDS, watched live: both of the things that change it - a right
        /// click that equips it away, a drop that fills it - happen under a cursor standing right
        /// here. <paramref name="slotName"/> is the game's own name for an equipment slot, and null in
        /// the backpack, where the position the graph speaks is the whole of where the slot is.
        ///
        /// <paramref name="dropInstead"/> is the off hand of a merged two-hander node: the one slot
        /// whose drop may land somewhere other than the node the player is standing on.
        /// </summary>
        private static void AddSlot(
            GraphBuilder builder,
            IArtifactSlots slots,
            InventorySlotInfo slot,
            string key,
            string slotName,
            InventorySlotInfo dropInstead,
            SlotHints hints)
        {
            if (slot == null)
            {
                return;
            }

            InventorySlotInfo it = slot;
            InventorySlotInfo alternative = dropInstead;
            NodeVtable vtable = slots.AnswersLeftClick(it)
                ? GraphNodes.Button(() => SlotName(it), () => slots.LeftClickArtifact(it), null, it.Tooltip)
                : GraphNodes.Text(() => SlotName(it), null, it.Tooltip);
            vtable.Announcements[0].Live = true;
            if (!string.IsNullOrWhiteSpace(slotName))
            {
                string name = slotName;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => name, watch: false));
            }

            vtable.DropKind = ArtifactCargo;
            vtable.OnDrop = held => Drop(slots, held, Target(slots, it, alternative, held));
            vtable.DropAccepts = held => slots.CanRearrangeArtifactTo(Movable(held), Target(slots, it, alternative, held));
            vtable.OnPickUp = () => PickUp(it);
            // Selecting the game's own cell is what makes it draw the artifact's tooltip, and what
            // scrolls a backpack cell below the fold into view.
            vtable.OnFocusVisual = it.FocusNative;

            if (it.CanDrag)
            {
                vtable.OnContextual = () => slots.RightClickArtifact(it);
                if (hints != null)
                {
                    hints(vtable, it);
                }
            }

            object drawnBy = it.Movable != null ? (object)it.Movable : it.NativeSlot;
            ControlId id = ControlId.Structural(key);
            builder.AddItem(drawnBy == null
                ? (NodeDeclaration)new SyntheticNode(id, vtable)
                : new DrawnNode(id, vtable, drawnBy));
        }

        /// <summary>What a slot is called: the artifact in it, or the mod's word for an empty one.
        /// </summary>
        public static string SlotName(InventorySlotInfo slot)
        {
            return slot == null || string.IsNullOrWhiteSpace(slot.ArtifactName)
                ? ModText.Get(ModStrings.Screens.Empty)
                : slot.ArtifactName;
        }

        private static InventorySlotInfo Find(IReadOnlyList<InventorySlotInfo> slots, InventorySlot slot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Slot == slot)
                {
                    return slots[i];
                }
            }

            return null;
        }

        private static CarryItem PickUp(InventorySlotInfo slot)
        {
            return slot.Movable == null
                ? null
                : new CarryItem(slot.Movable, SlotName(slot), ArtifactCargo);
        }

        /// <summary>Where a drop on this node really goes. Every slot but one is itself; the merged
        /// two-hander node sends an artifact that fits the off hand ALONE to the off hand, which is
        /// what the game's own right click does with it.</summary>
        private static InventorySlotInfo Target(
            IArtifactSlots slots,
            InventorySlotInfo slot,
            InventorySlotInfo offHand,
            CarryItem held)
        {
            return offHand != null && slots.IsOffHandOnlyArtifact(Movable(held)) ? offHand : slot;
        }

        private static InventoryArtifactMovable Movable(CarryItem held)
        {
            return held == null ? null : held.Cargo as InventoryArtifactMovable;
        }

        /// <summary>The drop, through the game's own check and its own move. A refusal it has words
        /// for is spoken in them; one it has none for falls back to the engine's sentence, and the
        /// player keeps carrying either way.</summary>
        private static DropResult Drop(IArtifactSlots slots, CarryItem held, InventorySlotInfo target)
        {
            NativeDropResult result = slots.DropArtifact(Movable(held), target);
            switch (result)
            {
                case NativeDropResult.Dropped:
                    return DropResult.Done();
                case NativeDropResult.DeniedWithFeedback:
                    return DropResult.Refused(slots.RearrangeRefusalText);
                default:
                    return DropResult.Refused();
            }
        }

        /// <summary>Game text written for a renderer, read as one spoken line: its rich-text tags and
        /// its mouse-button icons are not words.</summary>
        // The auto-arrange caption is the same localized string on every build, and cleaning it ran
        // three regexes a frame. One remembered answer covers it: every backpack draws that one line.
        private static string _lastRaw;
        private static string _lastClean;

        private static string OneLine(string raw)
        {
            if (_lastClean != null && string.Equals(raw, _lastRaw, StringComparison.Ordinal))
            {
                return _lastClean;
            }

            IList<string> lines = SpokenLines.Of(new[] { raw });
            string clean = lines.Count > 0 ? lines[0] : string.Empty;
            _lastRaw = raw;
            _lastClean = clean;
            return clean;
        }
    }
}
