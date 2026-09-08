using System;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The menu for hiring a wielder at a claimed settlement, made navigable as a graph. Three places
    /// to be, in the order the menu draws them: the list of candidates down the left, the details of
    /// the one selected on the right, and the close cross.
    ///
    /// Measured 2026-09-07: entries left, details right; the skill levels are drawn as bare numbers
    /// the mod does not read. The entries are declared in the order the menu keeps them, which is the
    /// drawn order - the pool puts each entry it takes at the end of the container
    /// (<c>SetAsLastSibling</c>).
    ///
    /// ARRIVING ON A CANDIDATE SELECTS IT. The click only refills the details pane on the right -
    /// nothing is bought and no page is replaced (<c>PurchaseWielderMenu.HandleEntrySelected</c>) -
    /// so arriving at a candidate and seeing what they are worth is one event, which is also what the
    /// mouse does. Enter selects the same way; the purchase is its own button, and the game's own
    /// double click.
    ///
    /// The details are ONE stop rather than one per band: they are a description of the candidate the
    /// player just arrived at, read top to bottom. EVERY BAND OF IT IS A REGION (owner ruling
    /// 2026-09-08): the quote, the stats, the troops, the skills, the specialization and the
    /// purchase, so the region jump walks the pane band by band from wherever the cursor stands. The
    /// troops and the skills are named by the captions the pane draws over them ("Starting Troops",
    /// "Skills"), so a troop or a skill is heard with what it is, and the stats by the word the game
    /// uses for the same band on the wielder sheet (owner ruling 2026-09-08; the pane itself draws no
    /// text over its StatsSection, measured the same day). The quote, the specialization (whose
    /// line already opens with the game's caption) and the purchase are bare regions.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the menu is an
    /// <c>AdventureMenuBackground</c> with <c>_canClose</c> true, so it draws the close cross and
    /// registers <c>UI.ExitMenu</c> on its own close in <c>AnimateEntry</c>.
    /// </summary>
    public sealed class PurchaseWielderScreen : LiveScreen<PurchaseWielderMenuAdapter>
    {
        private const string EntriesStop = "purchase-wielder-list";
        private const string DetailsStop = "purchase-wielder-details";
        private const string CloseStop = "purchase-wielder-close";

        // A subject of its own per synthesized line, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the quote, the four stats, the specialization and the purchase
        // status are read off text meshes the details pane rebinds rather than off rows of their own.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        /// <summary>After a hot reload: point the slot at the menu already showing.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<PurchaseWielderScreen>(FindActive());
        }

        public static PurchaseWielderMenuAdapter FindActive()
        {
            PurchaseWielderMenu[] menus = Resources.FindObjectsOfTypeAll<PurchaseWielderMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PurchaseWielderMenuAdapter adapter = new PurchaseWielderMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return (adapter);
                }
            }

            return null;
        }

        public PurchaseWielderMenuAdapter Adapter
        {
            get { return Live; }
        }

        public override string Key
        {
            get { return "purchase-wielder"; }
        }

        /// <summary>Layer 24: over the settlement page that opens it.</summary>
        public override int Layer
        {
            get { return 24; }
        }

        /// <summary>The title the menu draws over the list ("Arleon Wielders 2/2").</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool IsActive()
        {
            return Live != null && Live.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(EntriesStop);
            BuildEntries(builder);

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the candidates ----

        private void BuildEntries(GraphBuilder builder)
        {
            IReadOnlyList<PurchaseWielderMenuAdapter.EntryItem> entries = Live.GetEntries();
            ControlId selected = null;
            for (int i = 0; i < entries.Count; i++)
            {
                PurchaseWielderMenuAdapter.EntryItem entry = entries[i];
                Component button = entry.Button;
                if (button == null)
                {
                    continue;
                }

                PurchaseWielderMenuAdapter.EntryItem it = entry;
                NodeVtable vtable = GraphNodes.Button(() => it.Name, () => it.Select());
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.ClassText));
                vtable.Announcements.Add(GraphNodes.ValuePart(() => StatusText(it)));
                vtable.Announcements.Add(GraphNodes.SelectedPart(() => it.IsSelected));
                // Arrival IS the selection: the click refills the details pane and nothing else.
                vtable.OnFocusVisual = () => it.Focus();
                ControlId id = ControlId.For(button, "purchase-wielder:entry/" + entry.Id);
                builder.AddItem(new DrawnNode(id, vtable, button));
                if (entry.IsSelected)
                {
                    selected = id;
                }
            }

            // The candidate the menu is showing, so arriving reads the details the player can see.
            builder.LandStopOn(selected);
            builder.SetStart(selected);
        }

        /// <summary>What the entry's own overlays say about the wielder: the game draws a frame around
        /// one the team already has and a cross over one that has died, and neither carries words.
        /// </summary>
        private static string StatusText(PurchaseWielderMenuAdapter.EntryItem entry)
        {
            if (entry.IsDead)
            {
                return ModText.Get(ModStrings.Screens.WielderDead);
            }

            return entry.IsOwned ? ModText.Get(ModStrings.Screens.WielderOwned) : null;
        }

        // ---- the details ----

        private void BuildDetails(GraphBuilder builder)
        {
            builder.SetRegion("purchase-wielder:quote");
            AddParagraphs(builder, "quote", SelectedQuoteLines);
            string stats = GameText.Get("Common/CommanderInventory/Stats", string.Empty);
            BeginRegion(builder, stats, "purchase-wielder:stats");
            AddStat(builder, "offence", () => Live.OffenceHeader, () => Live.Offence);
            AddStat(builder, "defence", () => Live.DefenceHeader, () => Live.Defence);
            AddStat(builder, "movement", () => Live.MovementHeader, () => Live.Movement);
            AddStat(builder, "view-radius", () => Live.ViewRadiusHeader, () => Live.ViewRadius);
            EndRegion(builder, stats);
            BuildTroops(builder);
            BuildSkills(builder);
            if (Live.HasSpecialization())
            {
                builder.SetRegion("purchase-wielder:specialization");
                AddParagraphs(builder, "specialization", () => Live.SpecializationLines);
            }

            builder.SetRegion("purchase-wielder:purchase");
            if (Live.HasPurchaseStatus())
            {
                AddLine(builder, "status", () => Live.PurchaseStatus);
            }

            BuildPurchase(builder);
            builder.SetRegion(null);
        }

        /// <summary>The candidate the pane is describing, read as the player sees it: the name, the
        /// level and the description's opening paragraph as one line, then a line per further
        /// paragraph.</summary>
        private IList<string> SelectedQuoteLines()
        {
            IList<string> description = Live.SelectedDescriptionLines;
            List<string> parts = new List<string> { Live.SelectedName };
            string level = Live.SelectedLevel;
            if (!string.IsNullOrWhiteSpace(level))
            {
                parts.Add(ModText.Get(ModStrings.Screens.LevelValue, level));
            }

            if (description.Count > 0)
            {
                parts.Add(description[0]);
            }

            List<string> lines = new List<string>();
            string first = JoinSentences(parts);
            if (!string.IsNullOrWhiteSpace(first))
            {
                lines.Add(first);
            }

            for (int i = 1; i < description.Count; i++)
            {
                lines.Add(description[i]);
            }

            return lines;
        }

        private static string JoinSentences(List<string> parts)
        {
            List<string> filtered = new List<string>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    filtered.Add(parts[i]);
                }
            }

            return string.Join(". ", filtered.ToArray());
        }

        private void BuildTroops(GraphBuilder builder)
        {
            if (!Live.HasTroops())
            {
                return;
            }

            string header = Live.TroopsHeader;
            BeginRegion(builder, header, "purchase-wielder:troops");
            for (int i = 0; i < Live.TroopSlotCount; i++)
            {
                Component slot = Live.GetTroopComponent(i);
                if (slot == null || !Live.IsTroopVisible(i))
                {
                    continue;
                }

                int index = i;
                NodeVtable vtable = GraphNodes.Text(
                    () => TroopLabel(index),
                    null,
                    Live.GetTroopTooltip(index));
                vtable.OnFocusVisual = () => Live.FocusTroop(index);
                builder.AddItem(new DrawnNode(
                    ControlId.For(slot, "purchase-wielder:troop/" + index),
                    vtable,
                    slot));
            }

            EndRegion(builder, header);
        }

        private string TroopLabel(int index)
        {
            string name = Live.GetTroopName(index);
            int amount = Live.GetTroopAmount(index);
            return amount > 0 ? ModText.Get(ModStrings.Combat.TroopQuantity, amount, name) : name;
        }

        private void BuildSkills(GraphBuilder builder)
        {
            string header = Live.SkillsHeader;
            BeginRegion(builder, header, "purchase-wielder:skills");
            for (int i = 0; i < Live.SkillSlotCount; i++)
            {
                Component slot = Live.GetSkillComponent(i);
                if (slot == null || !Live.IsSkillVisible(i))
                {
                    continue;
                }

                int index = i;
                NodeVtable vtable = GraphNodes.Text(
                    () => SkillLabel(index),
                    null,
                    Live.GetSkillTooltip(index));
                vtable.OnFocusVisual = () => Live.FocusSkill(index);
                builder.AddItem(new DrawnNode(
                    ControlId.For(slot, "purchase-wielder:skill/" + index),
                    vtable,
                    slot));
            }

            EndRegion(builder, header);
        }

        /// <summary>A band as a region of the details stop, under the caption the pane draws over it
        /// where it draws one.</summary>
        private static void BeginRegion(GraphBuilder builder, string caption, string key)
        {
            builder.SetRegion(key);
            if (!string.IsNullOrWhiteSpace(caption))
            {
                builder.PushContext(caption);
            }
        }

        private static void EndRegion(GraphBuilder builder, string caption)
        {
            if (!string.IsNullOrWhiteSpace(caption))
            {
                builder.PopContext();
            }
        }

        private string SkillLabel(int index)
        {
            string name = Live.GetSkillName(index);
            return string.IsNullOrWhiteSpace(name) ? ModText.Get(ModStrings.Screens.Skill, index + 1) : name;
        }

        private void BuildPurchase(GraphBuilder builder)
        {
            Component purchase = Live.PurchaseButton;
            if (purchase == null || !Live.IsPurchaseVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.PurchaseLabel,
                () => Live.ActivatePurchase(),
                Live.IsPurchaseEnabled,
                Live.PurchaseTooltip);
            vtable.OnFocusVisual = () => Live.FocusPurchase();
            builder.AddItem(new DrawnNode(
                ControlId.For(purchase, "purchase-wielder:purchase"),
                vtable,
                purchase));
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
            builder.AddItem(new DrawnNode(ControlId.For(close, "purchase-wielder:close"), vtable, close));
        }

        // ---- shared ----

        private void AddStat(GraphBuilder builder, string key, Func<string> header, Func<string> value)
        {
            if (string.IsNullOrWhiteSpace(value()))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Text(header);
            vtable.Announcements.Add(GraphNodes.ValuePart(value));
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(key), "purchase-wielder:" + key),
                vtable));
        }

        private void AddLine(GraphBuilder builder, string key, Func<string> text)
        {
            if (string.IsNullOrWhiteSpace(text()))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(key), "purchase-wielder:" + key),
                GraphNodes.Text(text)));
        }

        /// <summary>The same, for a text the pane may have written in more than one paragraph: one
        /// spoken line, one review-buffer line per paragraph.</summary>
        private void AddParagraphs(GraphBuilder builder, string key, Func<IList<string>> lines)
        {
            IList<string> paragraphs = lines();
            if (paragraphs == null || paragraphs.Count == 0)
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(key), "purchase-wielder:" + key),
                GraphNodes.Paragraphs(() => paragraphs)));
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
