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
    /// The wielder sheet, made navigable as a graph. Seven places to be, in the order the sheet draws
    /// them: the tutorial button, the overview (stats and specialization), the modifier tabs with the
    /// modifiers under them, the equipment, the backpack, the skills and powers, and the close cross.
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
    public sealed class CommanderSheetScreen : GraphScreen
    {
        private const string TutorialStop = "commander-sheet-tutorial";
        private const string OverviewStop = "commander-sheet-overview";
        private const string ModifiersStop = "commander-sheet-modifiers";
        private const string EquipmentStop = "commander-sheet-equipment";
        private const string InventoryStop = "commander-sheet-inventory";
        private const string SkillsStop = "commander-sheet-skills";
        private const string CloseStop = "commander-sheet-close";
        private const string KeyPrefix = "commander-sheet";

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

            ArtifactSlotNodes.RegisterSounds();

            if (_adapter.IsTutorialButtonVisible())
            {
                builder.BeginStop(TutorialStop);
                BuildTutorial(builder);
            }

            builder.BeginStop(OverviewStop);
            BuildOverview(builder);

            builder.BeginStop(ModifiersStop);
            BuildModifiers(builder);

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
        }

        /// <summary>The modifier tab bar and the rows of the showing tab, a stop of their own as the
        /// options screen's tabs are: a stop lands on whichever alternative is selected, and the bar
        /// under the stats would have made Tab into the overview land mid-stop on the selected tab.
        /// </summary>
        private void BuildModifiers(GraphBuilder builder)
        {
            BuildModifierTabs(builder);
            BuildBand(
                builder,
                "modifiers",
                _adapter.GetActiveModifierListLabel(),
                Items("Modifiers", _adapter.GetActiveModifiers));
        }

        /// <summary>The three modifier tabs as the ONE BAR the sheet draws
        /// (<see cref="CommanderBands.Tabs"/>).</summary>
        private void BuildModifierTabs(GraphBuilder builder)
        {
            IReadOnlyList<CommanderSheetAdapter.ModifierCategory> categories = _adapter.GetModifierCategories();
            List<CommanderBands.TabItem> tabs = new List<CommanderBands.TabItem>();
            for (int i = 0; i < categories.Count; i++)
            {
                CommanderSheetAdapter.ModifierCategory it = categories[i];
                tabs.Add(new CommanderBands.TabItem(it.Label, it.Index, it.Button, it.Tooltip));
            }

            CommanderBands.Tabs(
                builder,
                KeyPrefix,
                tabs,
                _adapter.GetActiveModifierCategoryIndex,
                index => _adapter.ActivateModifierCategory(index),
                index => _adapter.SelectModifierCategory(index));
        }

        // ---- the equipment and the backpack ----

        private void BuildEquipment(GraphBuilder builder)
        {
            ArtifactSlotNodes.Equipment(builder, _adapter, "commander-sheet", AddSlotHints);
        }

        private void BuildInventory(GraphBuilder builder)
        {
            ArtifactSlotNodes.Inventory(builder, _adapter, "commander-sheet", AddSlotHints, Marker("auto-arrange"));
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
        /// the modifiers of the showing tab, the skills, the powers
        /// (<see cref="CommanderBands.Band"/>).</summary>
        private void BuildBand(
            GraphBuilder builder,
            string key,
            string caption,
            IReadOnlyList<CommanderSheetAdapter.LabeledItem> items)
        {
            List<CommanderBands.Line> lines = new List<CommanderBands.Line>();
            for (int i = 0; i < items.Count; i++)
            {
                CommanderSheetAdapter.LabeledItem it = items[i];
                lines.Add(new CommanderBands.Line(it.Label, it.Value, it.Tooltip, it.OnFocus));
            }

            CommanderBands.Band(builder, KeyPrefix, key, caption, lines, Marker);
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
