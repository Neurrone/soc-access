using System;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using DropResult = SongsOfConquestAccess.UI.Graph.DropResult;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The adventure spellbook, made navigable as a graph. Four places to be: the tutorial button
    /// while the game still draws it, the quick bar, the spells, and the close cross.
    ///
    /// EVERY GESTURE IS THE GAME'S OWN. Enter on a spell is its <c>UIButton</c>'s left click, which
    /// casts in battle and is inert on the map; Backslash is the same button's RIGHT click, which the
    /// game answers by putting the spell in the first free slot under its own guards
    /// (<c>SpellBook.AddEntryToFirstAvailableQuickbarSlot</c>: nothing while auto-fill is on or the
    /// spell is already on the bar), and the spoken hint is gated on exactly those two conditions.
    /// Enter on a filled slot is its <c>_mainButton</c>'s click, delivered as a pointer click so the
    /// game's own dispatch decides what happens.
    ///
    /// A DROP IS THE MOUSE'S WHOLE DRAG, replayed in one call
    /// (<c>SpellbookAdapter.DragQuickbarSpell</c> / <c>DragSpellToQuickbar</c>): the source's own
    /// <c>OnBeginDrag</c> - which empties a source SLOT up front, as the pointer's press does - then
    /// the movable's hovered entry and its <c>EndDrag</c>, which moves onto an empty slot, swaps onto
    /// an occupied one and overwrites from a column. It has to be one call because the movable's
    /// <c>LateUpdate</c> re-reads the hovered slot from the real pointer and ends the drag as soon as
    /// the mouse button is up.
    ///
    /// THE AUTO-FILL BOX IS READ FIRST, before the slots, although the game draws it BENEATH them
    /// (owner ruling): it decides whether any of the slots can be worked at all.
    ///
    /// THE REMOVAL TARGET after the slots is the mod's line for a gesture the mouse has and the
    /// keyboard otherwise could not reach: the game removes a spell from the bar when a drag of it is
    /// released over nothing (<c>SpellbookMovableSpell.EndDrag</c> with no hovered entry, which plays
    /// its cancel sound and empties the origin). It is a plain line while nothing is carried and a
    /// drop target only while a spell picked up FROM A SLOT is held. The delete button the game
    /// reveals on hover is the other way out, declared as the one child of a filled slot's group.
    ///
    /// POSITIONS COUNT THE LIST, not the extras: the auto-fill box and the removal target sit in rows
    /// that count nothing, so a slot says "3 of 8"; the essence and tier headings of a column do the
    /// same, so its spells say "1 of 4".
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): <c>SpellBook.Show</c> registers
    /// <c>InputActions.UI.ExitMenu</c> and <c>UI.Cancel</c> at <c>InputLevel.Popup</c> with no gamepad
    /// gate (measured 2026-09-07 in the decompiled source), and <c>Common.ToggleSpellBook</c> - V -
    /// closes the window too. The navigator claims the key only while something is being carried.
    /// </summary>
    public sealed class SpellbookScreen : LiveScreen<SpellbookAdapter>
    {
        private const string TutorialStop = "spellbook-tutorial";
        private const string QuickbarStop = "spellbook-quickbar";
        private const string SpellsStop = "spellbook-spells";
        private const string CloseStop = "spellbook-close";

        /// <summary>What is carried between the quick bar's slots and the spell columns.</summary>
        public const string SpellCargo = "spell";

        /// <summary>The noise the game itself makes when a drag of a spell begins
        /// (<c>SpellbookMovableSpell.BeginDrag</c>).</summary>
        public const string PickUpSound = "Common_SpellbookBeginDrag";

        /// <summary>The noise the game makes when a drag ends on nothing
        /// (<c>SpellbookMovableSpell.EndDrag</c>), which is what a given-up carry is.</summary>
        public const string CancelSound = "Common_SpellbookEndDragCancel";

        // A subject of its own per synthesized node, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the removal target is a line the mod invented.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        // Whether the carry that is ending ran the game's own drag, which plays the game's own noise
        // for how it ended. Only a carry that did NOT get that far - one the player gave up, or one
        // the game would not take - is the mod's to make a noise about.
        private bool _nativeDragRan;

        /// <summary>The adventure spellbook: the commander HUD's settings hold the opener, and the
        /// opener holds the book (<see cref="HudSources"/>). The battle's spellbook is a different
        /// object and a different screen.</summary>
        private readonly ScreenSource<SpellBook> _source =
            ScreenSource<SpellBook>.FromOwner(HudSources.Commander, HudSources.Spellbook);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override SpellbookAdapter Adapt(object menu)
        {
            return new SpellbookAdapter((SpellBook)menu);
        }

        public override string Key
        {
            get { return "spellbook"; }
        }

        /// <summary>Layer 26: over the map and the battlefield alike.</summary>
        public override int Layer
        {
            get { return 26; }
        }

        /// <summary>The window draws no title of its own, so it is named after the HUD button that
        /// opens it.</summary>
        public override string ScreenName
        {
            get { return GameText.Get("Common/HUD/SpellbookButton", string.Empty); }
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

            // The game's own drag noises, for the keyboard's carry. Registered on every build: the
            // registration is a delegate over this load and must not outlive it.
            CarrySounds.Register(SpellCargo, () => NativeSoundUtility.PostEvent(PickUpSound), EndedCarry);

            if (Live.IsTutorialButtonVisible())
            {
                builder.BeginStop(TutorialStop);
                BuildTutorial(builder);
            }

            builder.BeginStop(QuickbarStop);
            BuildQuickbar(builder);

            builder.BeginStop(SpellsStop);
            BuildSpells(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>A carry has ended. The game made its own noise for every ending it performed
        /// itself; the ones it never heard about - a give-up, a drop it would not take - are the
        /// cancel, which is what the game calls a drag released over nothing.</summary>
        private void EndedCarry()
        {
            if (_nativeDragRan)
            {
                _nativeDragRan = false;
                return;
            }

            NativeSoundUtility.PostEvent(CancelSound);
        }

        // ---- the tutorial button ----

        private void BuildTutorial(GraphBuilder builder)
        {
            UIButton button = Live.TutorialButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.GetTutorialButtonLabel(),
                () => Live.ActivateTutorial());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select((Component)button);
            builder.AddItem(new DrawnNode(
                ControlId.For(button, "spellbook:tutorial"),
                vtable,
                button));
        }

        // ---- the quick bar ----

        private void BuildQuickbar(GraphBuilder builder)
        {
            string header = OneLine(Live.GetQuickbarHeaderText());
            bool named = !string.IsNullOrWhiteSpace(header);
            if (named)
            {
                builder.PushContext(header);
                builder.SetRegion("spellbook:quickbar");
            }

            BuildAutoPopulate(builder);

            IReadOnlyList<SpellbookAdapter.QuickbarItem> items = Items("quickbar", Live.GetQuickbarItems);
            for (int i = 0; i < items.Count; i++)
            {
                AddSlot(builder, items[i], i);
            }

            if (items.Count > 0)
            {
                BuildRemovalTarget(builder);
            }

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>The auto-fill box, at the TOP of the bar although the game draws it under the
        /// slots. Its row counts nothing, so the slots below it say where they sit in the bar rather
        /// than in a list with a box at the top of it.</summary>
        private void BuildAutoPopulate(GraphBuilder builder)
        {
            Component toggle = Live.AutoPopulateToggle;
            if (toggle == null || !Live.IsAutoPopulateVisible())
            {
                return;
            }

            // The box's name IS its tooltip, so the tooltip is not also declared: it would read twice.
            NodeVtable vtable = GraphNodes.Checkbox(
                () => Live.GetAutoPopulateLabel(),
                () => Live.IsAutoPopulateChecked(),
                () => Live.ToggleAutoPopulate());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(toggle);
            builder.StartRow("spellbook:auto-populate", positions: false);
            builder.AddItem(new DrawnNode(
                ControlId.For(toggle, "spellbook:auto-populate"),
                vtable,
                toggle));
            builder.EndRow();
        }

        /// <summary>
        /// One slot: the spell in it or the mod's word for an empty one, where it sits in the bar, and
        /// every gesture the game gives a spell there.
        ///
        /// A FILLED slot is a group, because the game hides a command under it: the delete button it
        /// draws when the pointer rests on the slot. Right opens the group and lands on it.
        ///
        /// An EMPTY one is no control at all (owner ruling): it is a place a spell can be dropped,
        /// so it says the mod's word for empty and nothing about a role, and it takes no click.
        /// </summary>
        private void AddSlot(GraphBuilder builder, SpellbookAdapter.QuickbarItem item, int index)
        {
            if (item == null || item.Entry == null)
            {
                return;
            }

            SpellbookAdapter.QuickbarItem it = item;
            ControlId id = ControlId.For(it.Entry, "spellbook:slot/" + index);
            Func<string> label = () => it.HasSpell ? it.SpellName : ModText.Get(ModStrings.Screens.Empty);
            // The slot's click is the game's own, which casts in battle and does not exist on the
            // map: there the node declares no click, and Enter is consumed silently.
            Action activate = it.CanActivate ? () => it.Activate() : (Action)null;
            NodeVtable vtable = it.HasSpell
                ? GraphNodes.Group(label, activate, null, it.Tooltip)
                : GraphNodes.Text(label, null, it.Tooltip);
            // What the slot holds changes under a cursor standing right here: a drop lands on it, the
            // game's own animation fills it a fifth of a second after the drop was reported.
            vtable.Announcements[0].Live = true;
            vtable.DropKind = SpellCargo;
            vtable.DropAccepts = held => it.AcceptsDrop;
            vtable.OnDrop = held => DropOnSlot(held, it);
            vtable.OnPickUp = () => PickUp(it);
            // Resting on the slot is what makes the game draw the spell's details and reveal the
            // delete button; leaving takes both away again.
            vtable.OnFocusVisual = () => it.Focus();
            vtable.OnBlurVisual = () => it.Unfocus();

            if (!it.HasSpell)
            {
                builder.AddItem(new DrawnNode(id, vtable, it.Entry));
                return;
            }

            builder.BeginGroup(new DrawnNode(id, vtable, it.Entry));
            AddRemove(builder, it, index);
            builder.EndGroup();
        }

        /// <summary>The delete button the game reveals on hover, as the one thing inside a filled
        /// slot. The game draws no text on it (its <c>RemoveButton</c> is an icon), so the mod names
        /// it.</summary>
        private void AddRemove(GraphBuilder builder, SpellbookAdapter.QuickbarItem item, int index)
        {
            SpellbookAdapter.QuickbarItem it = item;
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Remove),
                () => it.Delete());
            vtable.OnFocusVisual = () => it.Focus();
            vtable.OnBlurVisual = () => it.Unfocus();
            builder.AddItem(new DrawnNode(
                ControlId.Structural("spellbook:slot/" + index + "/remove"),
                vtable,
                it.Entry));
        }

        /// <summary>The game's drag-out-to-nothing removal, as a line the keyboard can reach. It says
        /// what it is for in the owner's words and no more; its row counts nothing, so the slots
        /// above it still say where they sit in the bar.</summary>
        private void BuildRemovalTarget(GraphBuilder builder)
        {
            NodeVtable vtable = GraphNodes.Text(() => ModText.Get(ModStrings.Screens.SpellbookDropToRemove));
            vtable.DropKind = SpellCargo;
            vtable.DropAccepts = held => held != null
                && held.Cargo is SpellbookQuickbarEntry
                && !Live.IsAutoPopulateChecked();
            vtable.OnDrop = DropToRemove;
            builder.StartRow("spellbook:remove-target", positions: false);
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("remove-target"), "spellbook:remove-target"),
                vtable));
            builder.EndRow();
        }

        private CarryItem PickUp(SpellbookAdapter.QuickbarItem item)
        {
            return item.CanDrag ? new CarryItem(item.Entry, item.SpellName, SpellCargo) : null;
        }

        /// <summary>A drop on a slot, through the game's own drag: a spell from another slot moves or
        /// swaps, a spell from a column overwrites.</summary>
        private DropResult DropOnSlot(CarryItem held, SpellbookAdapter.QuickbarItem target)
        {
            SpellbookQuickbarEntry fromSlot = held == null ? null : held.Cargo as SpellbookQuickbarEntry;
            bool ran = fromSlot != null
                ? Live.DragQuickbarSpell(fromSlot, target.Entry)
                : Live.DragSpellToQuickbar(held == null ? null : held.Cargo as SpellbookSpellEntry, target.Entry);
            if (!ran)
            {
                return DropResult.Refused();
            }

            _nativeDragRan = true;
            return DropResult.Done();
        }

        /// <summary>A drop on the removal line: the same drag, released over nothing, which the game
        /// answers with its cancel sound and an emptied slot.</summary>
        private DropResult DropToRemove(CarryItem held)
        {
            SpellbookQuickbarEntry fromSlot = held == null ? null : held.Cargo as SpellbookQuickbarEntry;
            if (fromSlot == null || !Live.DragQuickbarSpell(fromSlot, null))
            {
                return DropResult.Refused();
            }

            _nativeDragRan = true;
            return DropResult.Done();
        }

        // ---- the spell columns ----

        private void BuildSpells(GraphBuilder builder)
        {
            IReadOnlyList<SpellbookAdapter.SchoolItem> schools = Items("schools", Live.GetSchools);

            // Every column's spells in one pass over the entries: asking column by column walked the
            // whole list six times a frame.
            Dictionary<SpellbookSpellGroup, List<SpellbookAdapter.SpellItem>> spells = Grouped();
            for (int i = 0; i < schools.Count; i++)
            {
                SpellbookAdapter.SchoolItem school = schools[i];
                if (school == null)
                {
                    continue;
                }

                BuildColumn(builder, school.Group, school.Title, school, spells);
            }

            BuildColumn(builder, SpellbookSpellGroup.Multi, ModText.Get(ModStrings.Screens.MultiEssenceSpells), null, spells);
            builder.SetRegion(null);
        }

        /// <summary>One drawn column, named by the game's own name for its spells. A single-essence
        /// column opens with the two things in it that own words of their own: the essence income and
        /// the tier the column grants, both of which say everything they have to say in a tooltip and
        /// so must be reachable.</summary>
        private void BuildColumn(
            GraphBuilder builder,
            SpellbookSpellGroup group,
            string title,
            SpellbookAdapter.SchoolItem school,
            Dictionary<SpellbookSpellGroup, List<SpellbookAdapter.SpellItem>> grouped)
        {
            List<SpellbookAdapter.SpellItem> spells;
            if (grouped == null || !grouped.TryGetValue(group, out spells) || spells == null)
            {
                spells = EmptySpells;
            }

            if (spells.Count == 0 && school == null)
            {
                return;
            }

            string key = group.ToString().ToLowerInvariant();
            bool named = !string.IsNullOrWhiteSpace(title);
            if (named)
            {
                builder.PushContext(title);
            }

            builder.SetRegion("spellbook:column/" + key);
            if (school != null)
            {
                AddColumnHeadings(builder, school, key);
            }

            for (int i = 0; i < spells.Count; i++)
            {
                AddSpell(builder, spells[i]);
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>The essence income and the tier heading, in a row that counts nothing so the
        /// spells under them say where they sit in the column.</summary>
        private void AddColumnHeadings(GraphBuilder builder, SpellbookAdapter.SchoolItem school, string key)
        {
            SpellbookAdapter.SchoolItem it = school;
            if (it.EssenceComponent != null)
            {
                NodeVtable essence = GraphNodes.Text(() => it.EssenceName, null, it.EssenceTooltip);
                essence.Announcements.Add(GraphNodes.ValuePart(() => OneLine(it.EssenceAmountText)));
                builder.StartRow("spellbook:essence/" + key, positions: false);
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.EssenceComponent, "spellbook:essence/" + key),
                    essence,
                    it.EssenceComponent));
                builder.EndRow();
            }

            if (it.TierComponent != null)
            {
                NodeVtable tier = GraphNodes.Text(() => OneLine(it.TierTitle), null, it.TierTooltip);
                builder.StartRow("spellbook:tier/" + key, positions: false);
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.TierComponent, "spellbook:tier/" + key),
                    tier,
                    it.TierComponent));
                builder.EndRow();
            }
        }

        private void AddSpell(GraphBuilder builder, SpellbookAdapter.SpellItem item)
        {
            if (item == null || item.Entry == null)
            {
                return;
            }

            SpellbookAdapter.SpellItem it = item;
            NodeVtable vtable = GraphNodes.Button(
                () => it.Label,
                () => it.Activate(),
                () => it.CanCast,
                it.Tooltip);
            vtable.OnContextual = () => it.RightClick();
            vtable.OnPickUp = () => PickUpSpell(it);
            // Focusing the entry selects it natively and is what makes the game draw the spell's
            // detail panel for it.
            vtable.OnFocusVisual = () => it.Focus();
            vtable.OnBlurVisual = () => it.Unfocus();
            NodeHints.Add(
                vtable,
                ModStrings.Screens.SpellbookAddToQuickbarHint,
                AccessibilityActions.UiRightClick.Key,
                0,
                () => it.CanAddToQuickbar);
            builder.AddItem(new DrawnNode(
                ControlId.For(it.Entry, "spellbook:spell/" + it.Id),
                vtable,
                it.Entry));
        }

        private CarryItem PickUpSpell(SpellbookAdapter.SpellItem item)
        {
            return Live.IsAutoPopulateChecked()
                ? null
                : new CarryItem(item.Entry, item.Label, SpellCargo);
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            UIButton close = Live.CloseButton;
            if (close == null || !Live.IsCloseVisible())
            {
                return;
            }

            // An icon with no text of its own, so the mod names it.
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => Live.ActivateClose());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select((Component)close);
            builder.AddItem(new DrawnNode(ControlId.For(close, "spellbook:close"), vtable, close));
        }

        // ---- shared ----

        /// <summary>One section's items, or none where reading them threw: a part of the window the
        /// game has stopped answering for costs its own rows and never the rest of the page.</summary>
        private static readonly List<SpellbookAdapter.SpellItem> EmptySpells =
            new List<SpellbookAdapter.SpellItem>();

        /// <summary>Every column's spells, or none where reading them threw.</summary>
        private Dictionary<SpellbookSpellGroup, List<SpellbookAdapter.SpellItem>> Grouped()
        {
            try
            {
                return Live.GetSpellsByGroup();
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("SpellbookScreen section spells failed to build: " + exception);
                return null;
            }
        }

        private static IReadOnlyList<T> Items<T>(string section, Func<IReadOnlyList<T>> getter)
        {
            try
            {
                IReadOnlyList<T> items = getter != null ? getter() : null;
                return items ?? new T[0];
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("SpellbookScreen section " + section + " failed to build: " + exception);
                return new T[0];
            }
        }

        /// <summary>Game text written for a renderer, read as one spoken line: its rich-text tags are
        /// not words.</summary>
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
