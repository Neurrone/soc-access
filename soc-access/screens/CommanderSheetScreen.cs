using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The wielder sheet, made navigable as a graph. Six places to be, in the order the sheet draws
    /// them: the tutorial button, the overview, the equipment, the backpack, the skills and powers,
    /// and the close cross.
    ///
    /// THE OVERVIEW IS THE STATS, THE SPECIALIZATION AND THE MODIFIERS, in that order, and it names
    /// the first stat as its landing (<c>GraphBuilder.LandStopOn</c>): the modifier bar in its tail
    /// reads as selected, and a stop that names no landing opens on whichever alternative is in
    /// force, so naming one is what keeps Tab off the bar. The bar and the showing tab's lines are a
    /// region named Modifiers after the specialization, which is what Alt+Down reaches them by.
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
    /// The equipment and backpack rows come from the shared contributor
    /// (<c>ui/ArtifactSlotNodes.cs</c>), which every screen drawing an <c>InventoryHUD</c> uses; only
    /// the three hint sentences below belong to this sheet.
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
    public sealed class CommanderSheetScreen : LiveScreen<CommanderSheetAdapter>
    {
        private const string TutorialStop = "commander-sheet-tutorial";
        private const string OverviewStop = "commander-sheet-overview";
        private const string EquipmentStop = "commander-sheet-equipment";
        private const string InventoryStop = "commander-sheet-inventory";
        private const string SkillsStop = "commander-sheet-skills";
        private const string CloseStop = "commander-sheet-close";
        private const string KeyPrefix = "commander-sheet";

        // A subject of its own per synthesized node, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the auto-arrange button and the read-only lines are not drawn as
        // controls of their own.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        /// <summary>The commander HUD's settings hold the sheet (<see cref="HudSources"/>).</summary>
        private readonly ScreenSource<CommanderSheet> _source =
            ScreenSource<CommanderSheet>.FromOwner(HudSources.Commander, HudSources.Sheet);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override CommanderSheetAdapter Adapt(object menu)
        {
            return new CommanderSheetAdapter((CommanderSheet)menu);
        }

        public override string Key
        {
            get { return "commander-sheet"; }
        }

        /// <summary>Layer 26: over every panel it can be opened from.</summary>
        public override int Layer
        {
            get { return 26; }
        }

        /// <summary>The wielder the sheet is about, as it draws them at the top: the name and the race
        /// and title under it.</summary>
        public override string ScreenName
        {
            get
            {
                if (Live == null)
                {
                    return null;
                }

                string name = Live.CommanderName;
                string title = Live.CommanderClass;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return string.IsNullOrWhiteSpace(title) ? null : title;
                }

                return string.IsNullOrWhiteSpace(title)
                    ? name
                    : ModText.Get(ModStrings.Common.ListSeparator, name, title);
            }
        }

        public override bool IsActive()
        {
            SyncLive();
            return Live != null && Live.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            ArtifactSlotNodes.RegisterSounds();

            if (Live.IsTutorialButtonVisible())
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
            Component button = Live.TutorialButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.GetTutorialButtonLabel(),
                () => Live.ActivateTutorial());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            builder.AddItem(new DrawnNode(
                ControlId.For(button, "commander-sheet:tutorial"),
                vtable,
                button));
        }

        // ---- the overview: stats, specialization, the modifier tabs and their rows ----

        private void BuildOverview(GraphBuilder builder)
        {
            ControlId firstStat = BuildBand(
                builder,
                "stats",
                GameText.Get("Common/CommanderInventory/Stats", string.Empty),
                Items("Stats", Live.GetStats));
            BuildBand(
                builder,
                "specialization",
                GameText.Get("Commanders/Tooltip/Specializations", string.Empty),
                Items("Specializations", Live.GetSpecializations));
            BuildModifiers(builder);

            if (firstStat != null)
            {
                // Exactly there: the bar in the stop's tail reads as selected, and a stop otherwise
                // lands on the alternative in force.
                builder.LandStopOn(firstStat);
            }
        }

        /// <summary>The modifier tab bar and the rows of the showing tab, one region of the overview
        /// (<see cref="CommanderBands.Modifiers"/>) - read-only after the specialization, reached by
        /// the region jump rather than by Tab.</summary>
        private void BuildModifiers(GraphBuilder builder)
        {
            IReadOnlyList<CommanderSheetAdapter.ModifierCategory> categories = Live.GetModifierCategories();
            List<CommanderBands.TabItem> tabs = new List<CommanderBands.TabItem>();
            for (int i = 0; i < categories.Count; i++)
            {
                CommanderSheetAdapter.ModifierCategory it = categories[i];
                tabs.Add(new CommanderBands.TabItem(it.Label, it.Index, it.Button, it.Tooltip));
            }

            CommanderBands.Modifiers(
                builder,
                KeyPrefix,
                tabs,
                Live.GetActiveModifierCategoryIndex,
                index => Live.ActivateModifierCategory(index),
                index => Live.SelectModifierCategory(index),
                Live.GetActiveModifierListLabel(),
                Lines(Items("Modifiers", Live.GetActiveModifiers)),
                Marker);
        }

        // ---- the equipment and the backpack ----

        private void BuildEquipment(GraphBuilder builder)
        {
            ArtifactSlotNodes.Equipment(builder, Live, "commander-sheet", AddSlotHints);
        }

        private void BuildInventory(GraphBuilder builder)
        {
            ArtifactSlotNodes.Inventory(builder, Live, "commander-sheet", AddSlotHints, Marker("auto-arrange"));
        }

        /// <summary>The three gestures an occupied slot has, in the order they are said: what the right
        /// click does with the artifact, what Ctrl and the right click do, and what Ctrl and the left
        /// click do. Each names its action and the binding the chord belongs to, so re-binding either
        /// click re-words all three.</summary>
        private void AddSlotHints(NodeVtable vtable, InventorySlotInfo slot)
        {
            ArtifactDetails.EquipInstruction instruction = Live.GetArtifactInstruction(slot);
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

        // ---- the skills and the powers ----

        private void BuildSkills(GraphBuilder builder)
        {
            BuildBand(
                builder,
                "skills",
                GameText.Get("Commanders/Tooltip/Skills", string.Empty),
                Items("Skills", () => Live.GetSkills(powers: false)));
            BuildBand(
                builder,
                "powers",
                GameText.Get("Commanders/Tooltip/Powers", string.Empty),
                Items("Powers", () => Live.GetSkills(powers: true)));
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            Component close = Live.CloseButton;
            if (close == null || !Live.IsCloseVisible())
            {
                return;
            }

            // An icon with no text of its own, so the mod names it.
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => Live.ActivateClose());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
            builder.AddItem(new DrawnNode(ControlId.For(close, "commander-sheet:close"), vtable, close));
        }

        // ---- shared ----

        /// <summary>One of the bands the sheet draws under a caption - the stats, the specialization,
        /// the modifiers of the showing tab, the skills, the powers
        /// (<see cref="CommanderBands.Band"/>). Answers with the band's first line, which is what the
        /// overview lands its stop on.</summary>
        private ControlId BuildBand(
            GraphBuilder builder,
            string key,
            string caption,
            IReadOnlyList<CommanderSheetAdapter.LabeledItem> items)
        {
            return CommanderBands.Band(builder, KeyPrefix, key, caption, Lines(items), Marker);
        }

        private static IReadOnlyList<CommanderBands.Line> Lines(
            IReadOnlyList<CommanderSheetAdapter.LabeledItem> items)
        {
            List<CommanderBands.Line> lines = new List<CommanderBands.Line>();
            for (int i = 0; i < items.Count; i++)
            {
                CommanderSheetAdapter.LabeledItem it = items[i];
                lines.Add(new CommanderBands.Line(it.Label, it.Value, it.Tooltip, it.OnFocus));
            }

            return lines;
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
