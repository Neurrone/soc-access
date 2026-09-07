using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Details;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using DropResult = SongsOfConquestAccess.UI.Graph.DropResult;
using NativeDropResult = SongsOfConquestAccess.Adapters.DropResult;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The wielder sheet, made navigable as a graph. Six places to be, in the order the sheet draws
    /// them: the tutorial button, the overview (stats, specialization, the modifier tabs and the
    /// modifiers under them), the equipment, the backpack, the skills and powers, and the close cross.
    ///
    /// EVERY GESTURE ON AN ARTIFACT IS THE GAME'S OWN CLICK, delivered into the button the game hangs
    /// its handlers on (<c>InventoryArtifactMovable</c>'s <c>UIButton</c>), so the game's own rules
    /// decide what happens - the mod never calls <c>EquipArtifact</c> or <c>DestroyArtifact</c>
    /// itself. Enter is the left click (inert here, and with Ctrl physically held the game drops the
    /// artifact on the ground); Backslash is the right click (equip, unequip or use, and with Ctrl
    /// held destroy, its confirm dialog included). Both Ctrl chords are further bindings of the same
    /// two actions because the game's handlers read the physical Ctrl for themselves.
    ///
    /// THE MODIFIER TABS SWITCH ON ENTER, not on arrival: Up from the first modifier row lands on the
    /// bar, and a switch on arrival would take the list the player is reading away. Their focus visual
    /// is the game's own button selection, as the build menu's tier tabs are.
    ///
    /// TWO-HANDERS: when the artifact in the main hand takes both hands
    /// (<c>IArtifactLookup.GetSlot</c> answers <c>ArtifactSlot.BothHands</c>), the game draws a ghost
    /// of it in the off hand, and the Main Hand and Off Hand nodes become ONE node carrying the game's
    /// own name for that slot ("Both Hands"), so the stop has eight. It picks up from the main hand,
    /// and a drop on it goes to the main hand except for an artifact that fits the off hand ALONE,
    /// which is the game's own right-click resolution (<c>InventoryHUD.GetSlot(ArtifactSlot)</c>)
    /// copied for this one merged node; the game then applies its own swap and eviction rules.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the sheet IS an
    /// <c>AdventureMenuBackground</c> with a close cross, and <c>AnimateEntry</c> registers
    /// <c>UI.ExitMenu</c> on its own close (measured 2026-09-07 in the decompiled source). The
    /// navigator claims the key only while something is being carried.
    ///
    /// One deviation from the game's words, recorded here: the right-click hint names the action in
    /// the MOD's words ("Backslash equips") rather than the game's, because a usage hint renders one
    /// substitution - the chord - and the game's own instruction table has no entry for Use at all
    /// (<c>ArtifactDetails</c> draws no instruction row for a usable artifact). Which of the three
    /// sentences is used is still the game's answer, read off the same state its own tooltip reads.
    /// </summary>
    public sealed class CommanderSheetScreen : GraphScreen
    {
        private const string TutorialStop = "commander-sheet-tutorial";
        private const string OverviewStop = "commander-sheet-overview";
        private const string EquipmentStop = "commander-sheet-equipment";
        private const string InventoryStop = "commander-sheet-inventory";
        private const string SkillsStop = "commander-sheet-skills";
        private const string CloseStop = "commander-sheet-close";

        /// <summary>What is carried between the slots of this sheet.</summary>
        public const string ArtifactCargo = "artifact";

        /// <summary>The noise the game itself makes when a drag of an artifact begins
        /// (<c>InventoryArtifactMovable.OnBeginDrag</c>).</summary>
        public const string PickUpSound = "Adventure_InventoryPickupArtifact";

        private readonly CommanderSheetAdapter _adapter;

        // A subject of its own per synthesized node, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the auto-arrange button and the read-only lines are not drawn as
        // controls of their own.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public CommanderSheetScreen(CommanderSheetAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommanderSheet[] sheets = Resources.FindObjectsOfTypeAll<CommanderSheet>();
            for (int i = 0; i < sheets.Length; i++)
            {
                CommanderSheetAdapter adapter = new CommanderSheetAdapter(sheets[i]);
                if (adapter.IsPresent())
                {
                    return new CommanderSheetScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "commander-sheet"; }
        }

        /// <summary>The wielder the sheet is about, as it draws them at the top: the name and the race
        /// and title under it.</summary>
        public override string ScreenName
        {
            get
            {
                if (_adapter == null)
                {
                    return null;
                }

                string name = _adapter.CommanderName;
                string title = _adapter.CommanderClass;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return string.IsNullOrWhiteSpace(title) ? null : title;
                }

                return string.IsNullOrWhiteSpace(title)
                    ? name
                    : ModText.Get(ModStrings.Common.ListSeparator, name, title);
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Kept for the detector, which calls it whenever an artifact, a statistic or a skill
        /// changes. The graph is declared afresh on every operation, so there is nothing to rebuild.
        /// </summary>
        public void Refresh()
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            // The game's own drag noise, for the keyboard's carry. Registered on every build: the
            // registration is a delegate over this load and must not outlive it.
            CarrySounds.Register(ArtifactCargo, () => NativeSoundUtility.PostEvent(PickUpSound), null);

            if (_adapter.IsTutorialButtonVisible())
            {
                builder.BeginStop(TutorialStop);
                BuildTutorial(builder);
            }

            builder.BeginStop(OverviewStop);
            BuildOverview(builder);

            builder.BeginStop(EquipmentStop);
            BuildEquipment(builder);

            builder.BeginStop(InventoryStop);
            BuildInventory(builder);

            builder.BeginStop(SkillsStop);
            BuildSkills(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the tutorial button ----

        private void BuildTutorial(GraphBuilder builder)
        {
            Component button = _adapter.TutorialButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.GetTutorialButtonLabel(),
                () => _adapter.ActivateTutorial());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            builder.AddItem(new DrawnNode(
                ControlId.For(button, "commander-sheet:tutorial"),
                vtable,
                button));
        }

        // ---- the overview: stats, specialization, the modifier tabs and their rows ----

        private void BuildOverview(GraphBuilder builder)
        {
            BuildBand(
                builder,
                "stats",
                GameText.Get("Common/CommanderInventory/Stats", string.Empty),
                Items("Stats", _adapter.GetStats));
            BuildBand(
                builder,
                "specialization",
                GameText.Get("Commanders/Tooltip/Specializations", string.Empty),
                Items("Specializations", _adapter.GetSpecializations));
            BuildModifierTabs(builder);
            BuildBand(
                builder,
                "modifiers",
                _adapter.GetActiveModifierListLabel(),
                Items("Modifiers", _adapter.GetActiveModifiers));
        }

        /// <summary>The three modifier tabs as the ONE BAR the sheet draws: Left and Right walk it,
        /// Enter switches. Arriving must not switch - Up from the first modifier row lands here.
        /// </summary>
        private void BuildModifierTabs(GraphBuilder builder)
        {
            IReadOnlyList<CommanderSheetAdapter.ModifierCategory> categories = _adapter.GetModifierCategories();
            List<CommanderSheetAdapter.ModifierCategory> drawn = new List<CommanderSheetAdapter.ModifierCategory>();
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].Button != null)
                {
                    drawn.Add(categories[i]);
                }
            }

            if (drawn.Count == 0)
            {
                return;
            }

            builder.StartRow("commander-sheet:modifier-tabs");
            for (int i = 0; i < drawn.Count; i++)
            {
                CommanderSheetAdapter.ModifierCategory it = drawn[i];
                NodeVtable vtable = GraphNodes.Tab(
                    () => it.Label,
                    () => _adapter.GetActiveModifierCategoryIndex() == it.Index,
                    null,
                    it.Tooltip);
                vtable.OnActivate = () => _adapter.ActivateModifierCategory(it.Index);
                vtable.OnFocusVisual = () => _adapter.SelectModifierCategory(it.Index);
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Button, "commander-sheet:modifier-tab/" + it.Index),
                    vtable,
                    it.Button));
            }

            builder.EndRow();
        }

        // ---- the equipment ----

        private void BuildEquipment(GraphBuilder builder)
        {
            IReadOnlyList<InventorySlotInfo> slots = _adapter.GetEquipmentSlots();
            if (slots.Count == 0)
            {
                return;
            }

            bool merged = _adapter.IsMainHandTwoHanded();
            InventorySlotInfo mainHand = Find(slots, InventorySlot.MainHand);
            InventorySlotInfo offHand = Find(slots, InventorySlot.OffHand);

            builder.PushContext(_adapter.EquipmentLabel);
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotInfo slot = slots[i];
                if (merged && slot.Slot == InventorySlot.OffHand)
                {
                    // The game draws a ghost of the two-hander here; the one node below stands for
                    // both hands.
                    continue;
                }

                if (merged && slot.Slot == InventorySlot.MainHand)
                {
                    AddSlot(builder, mainHand, "equipment/both-hands", _adapter.BothHandsSlotName, offHand);
                    continue;
                }

                AddSlot(builder, slot, "equipment/" + slot.Slot, slot.SlotName, null);
            }

            builder.PopContext();
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

        // ---- the backpack ----

        private void BuildInventory(GraphBuilder builder)
        {
            IReadOnlyList<InventorySlotInfo> slots = _adapter.GetBackpackSlots();
            builder.PushContext(_adapter.InventoryLabel);
            BuildAutoArrange(builder);
            for (int i = 0; i < slots.Count; i++)
            {
                AddSlot(builder, slots[i], "inventory/" + i, null, null);
            }

            builder.PopContext();
        }

        /// <summary>Auto-arrange, the game's own middle click, as a button at the top of the backpack.
        /// It sits in a row of its own that COUNTS NOTHING, so the positions the slots under it say are
        /// the positions of the backpack ("5 of 16") rather than places in a list that has a button at
        /// the top of it.</summary>
        private void BuildAutoArrange(GraphBuilder builder)
        {
            string label = OneLine(_adapter.AutoArrangeText);
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(() => label, () => _adapter.AutoArrangeArtifacts());
            builder.StartRow("commander-sheet:auto-arrange", positions: false);
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("auto-arrange"), "commander-sheet:auto-arrange"),
                vtable));
            builder.EndRow();
        }

        // ---- a slot, equipment or backpack ----

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
        private void AddSlot(
            GraphBuilder builder,
            InventorySlotInfo slot,
            string key,
            string slotName,
            InventorySlotInfo dropInstead)
        {
            if (slot == null)
            {
                return;
            }

            InventorySlotInfo it = slot;
            InventorySlotInfo alternative = dropInstead;
            NodeVtable vtable = GraphNodes.Button(
                () => SlotName(it),
                () => _adapter.LeftClickArtifact(it),
                null,
                it.Tooltip);
            vtable.Announcements[0].Live = true;
            if (!string.IsNullOrWhiteSpace(slotName))
            {
                string name = slotName;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => name, watch: false));
            }

            vtable.DropKind = ArtifactCargo;
            vtable.OnDrop = held => Drop(held, Target(it, alternative, held));
            vtable.DropAccepts = held => _adapter.CanRearrangeArtifactTo(Movable(held), Target(it, alternative, held));
            vtable.OnPickUp = () => PickUp(it);
            // Selecting the game's own cell is what makes it draw the artifact's tooltip, and what
            // scrolls a backpack cell below the fold into view.
            vtable.OnFocusVisual = it.FocusNative;

            if (it.CanDrag)
            {
                vtable.OnContextual = () => _adapter.RightClickArtifact(it);
                AddSlotHints(vtable, it);
            }

            object drawnBy = it.Movable != null ? (object)it.Movable : it.NativeSlot;
            ControlId id = ControlId.Structural("commander-sheet:" + key);
            builder.AddItem(drawnBy == null
                ? (NodeDeclaration)new SyntheticNode(id, vtable)
                : new DrawnNode(id, vtable, drawnBy));
        }

        /// <summary>The three gestures an occupied slot has, in the order they are said: what the right
        /// click does with the artifact, what Ctrl and the right click do, and what Ctrl and the left
        /// click do. Each names its action and the binding the chord belongs to, so re-binding either
        /// click re-words all three.</summary>
        private void AddSlotHints(NodeVtable vtable, InventorySlotInfo slot)
        {
            ArtifactDetails.EquipInstruction instruction = _adapter.GetArtifactInstruction(slot);
            ModString contextual = instruction == ArtifactDetails.EquipInstruction.Use
                ? ModStrings.Screens.ArtifactUseHint
                : instruction == ArtifactDetails.EquipInstruction.Unequip
                    ? ModStrings.Screens.ArtifactUnequipHint
                    : ModStrings.Screens.ArtifactEquipHint;
            NodeHints.Add(vtable, contextual, AccessibilityActions.UiRightClick.Key);
            NodeHints.Add(
                vtable,
                ModStrings.Screens.ArtifactDestroyHint,
                AccessibilityActions.UiRightClick.Key,
                AccessibilityActions.UiRightClickCtrlBindingIndex);
            NodeHints.Add(
                vtable,
                ModStrings.Screens.ArtifactDropHint,
                AccessibilityActions.UiLeftClick.Key,
                AccessibilityActions.UiLeftClickCtrlBindingIndex);
        }

        /// <summary>What a slot is called: the artifact in it, or the mod's word for an empty one.
        /// </summary>
        private static string SlotName(InventorySlotInfo slot)
        {
            return string.IsNullOrWhiteSpace(slot.ArtifactName)
                ? ModText.Get(ModStrings.Screens.Empty)
                : slot.ArtifactName;
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
        private InventorySlotInfo Target(InventorySlotInfo slot, InventorySlotInfo offHand, CarryItem held)
        {
            return offHand != null && _adapter.IsOffHandOnlyArtifact(Movable(held)) ? offHand : slot;
        }

        private static InventoryArtifactMovable Movable(CarryItem held)
        {
            return held == null ? null : held.Cargo as InventoryArtifactMovable;
        }

        /// <summary>The drop, through the game's own check and its own move. A refusal it has words
        /// for is spoken in them; one it has none for falls back to the engine's sentence, and the
        /// player keeps carrying either way.</summary>
        private DropResult Drop(CarryItem held, InventorySlotInfo target)
        {
            NativeDropResult result = _adapter.DropArtifact(Movable(held), target);
            switch (result)
            {
                case NativeDropResult.Dropped:
                    return DropResult.Done();
                case NativeDropResult.DeniedWithFeedback:
                    return DropResult.Refused(_adapter.RearrangeRefusalText);
                default:
                    return DropResult.Refused();
            }
        }

        // ---- the skills and the powers ----

        private void BuildSkills(GraphBuilder builder)
        {
            BuildBand(
                builder,
                "skills",
                GameText.Get("Commanders/Tooltip/Skills", string.Empty),
                Items("Skills", () => _adapter.GetSkills(powers: false)));
            BuildBand(
                builder,
                "powers",
                GameText.Get("Commanders/Tooltip/Powers", string.Empty),
                Items("Powers", () => _adapter.GetSkills(powers: true)));
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            Component close = _adapter.CloseButton;
            if (close == null || !_adapter.IsCloseVisible())
            {
                return;
            }

            // An icon with no text of its own, so the mod names it.
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => _adapter.ActivateClose());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
            builder.AddItem(new DrawnNode(ControlId.For(close, "commander-sheet:close"), vtable, close));
        }

        // ---- shared ----

        /// <summary>One of the bands the sheet draws under a caption - the stats, the specialization,
        /// the modifiers of the showing tab, the skills, the powers. The caption is the REGION its
        /// rows belong to rather than a row of its own, because there is nothing there to operate.
        /// </summary>
        private void BuildBand(
            GraphBuilder builder,
            string key,
            string caption,
            IReadOnlyList<CommanderSheetAdapter.LabeledItem> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion("commander-sheet:" + key);
            }

            for (int i = 0; i < items.Count; i++)
            {
                CommanderSheetAdapter.LabeledItem it = items[i];
                NodeVtable vtable = GraphNodes.Text(() => it.Label, null, it.Tooltip);
                if (!string.IsNullOrWhiteSpace(it.Value))
                {
                    vtable.Announcements.Add(GraphNodes.ValuePart(() => it.Value));
                }

                if (it.OnFocus != null)
                {
                    vtable.OnFocusVisual = () => it.OnFocus();
                }

                builder.AddItem(new SyntheticNode(
                    ControlId.For(Marker(key + "/" + i), "commander-sheet:" + key + "/" + i),
                    vtable));
            }

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>One band's items, or an empty band where reading them threw: a section the game
        /// has stopped answering for costs its own rows and never the rest of the sheet.</summary>
        private static IReadOnlyList<CommanderSheetAdapter.LabeledItem> Items(
            string section,
            Func<IReadOnlyList<CommanderSheetAdapter.LabeledItem>> getter)
        {
            try
            {
                IReadOnlyList<CommanderSheetAdapter.LabeledItem> items = getter();
                return items ?? new CommanderSheetAdapter.LabeledItem[0];
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("CommanderSheetScreen section " + section + " failed to build: " + ex);
                return new CommanderSheetAdapter.LabeledItem[0];
            }
        }

        /// <summary>Game text written for a renderer, read as one spoken line: its rich-text tags and
        /// its mouse-button icons are not words.</summary>
        private static string OneLine(string raw)
        {
            IList<string> lines = SpokenLines.Of(new[] { raw });
            return lines.Count > 0 ? lines[0] : string.Empty;
        }

        private object Marker(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers.Add(key, marker);
            }

            return marker;
        }
    }
}
