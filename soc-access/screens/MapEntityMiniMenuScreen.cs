using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The mini menu the map pops up over a settlement, a resource generator or a build site, made
    /// navigable as a graph. Three places to be, in the order the menu draws them: the details, the
    /// strip of action buttons, and the close.
    ///
    /// Measured 2026-09-07: the entity's own name ("Crowpoint") is drawn ABOVE its type name ("Small
    /// Settlement"), then the description rows, then the actions in a row of icon buttons. The two
    /// names are one title split across two labels, so together, in that drawn order, they are the
    /// SCREEN'S NAME, said once on arrival, rather than a stop of their own (owner ruling 2026-09-07).
    ///
    /// The stored wielder is ONE BUTTON, not a line plus a mod-labelled command: the game draws a
    /// single control there (<c>MapEntityMiniMenu._storedWielderButton</c>) whose click ejects the
    /// wielder, so the node is that button, labelled with the wielder's name and unavailable while
    /// the game refuses the ejection.
    ///
    /// Native tooltips are buffer-only, as everywhere: the description rows, the stored wielder and
    /// the action buttons all declare theirs as a section.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control and is dismissed by clicking
    /// outside it, so the node runs the game's own <c>MapEntityMiniMenu.Hide</c>. Escape is the
    /// game's (<c>ConsumesBack</c> false): <c>MapEntityMiniMenu.Show</c> registers <c>UI.ExitMenu</c>
    /// against <c>Hide</c> itself.
    /// </summary>
    public sealed class MapEntityMiniMenuScreen : LiveScreen<MapEntityMiniMenuAdapter>
    {
        private const string DetailsStop = "map-entity-details";
        private const string ActionsStop = "map-entity-actions";
        private const string CloseStop = "map-entity-close";

        // The lines the game gives no component of their own for, and the close, keyed by subjects
        // held across rebuilds so the reconciler seats the cursor back on the same node.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        /// <summary>After a hot reload: point the slot at the menu already showing.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<MapEntityMiniMenuScreen>(FindActive());
        }

        public static MapEntityMiniMenuAdapter FindActive()
        {
            MapEntityMiniMenu[] menus = Resources.FindObjectsOfTypeAll<MapEntityMiniMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return (adapter);
                }
            }

            return null;
        }

        public MapEntityMiniMenuAdapter Adapter
        {
            get { return Live; }
        }

        public override string Key
        {
            get { return "map-entity-mini-menu"; }
        }

        /// <summary>Layer 20: an in-game panel over the map.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>Both drawn names, the entity's own first: the heading is the screen's name, said
        /// once on arrival, and not a stop of its own (owner ruling 2026-09-07). Focus starts on the
        /// first detail line.</summary>
        public override string ScreenName
        {
            get
            {
                string heading = Live != null ? HeadingText() : null;
                return string.IsNullOrWhiteSpace(heading) ? null : heading;
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

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(ActionsStop);
            BuildActions(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>Both drawn names in their drawn order, the entity's own first.</summary>
        private string HeadingText()
        {
            string type = Live.EntityName;
            if (!Live.IsCustomNameVisible)
            {
                return type;
            }

            string custom = Live.CustomName;
            return string.IsNullOrWhiteSpace(type)
                ? custom
                : ModText.Get(ModStrings.Common.ListSeparator, custom, type);
        }

        // ---- the details, in drawn order ----

        private void BuildDetails(GraphBuilder builder)
        {
            if (Live.IsBlueprintDescriptionVisible)
            {
                AddParagraphs(builder, "blueprint-description", Live.BlueprintDescriptionComponent,
                    () => Live.BlueprintDescriptionLines);
            }

            AddStoredWielder(builder);
            AddDescriptionRows(builder);

            if (Live.IsUpgradeSummaryVisible)
            {
                AddText(builder, "upgrades", Live.UpgradesComponent, () => Live.UpgradeSummary);
            }

            if (Live.IsSiegeStateVisible)
            {
                AddText(builder, "siege-state", Live.SiegeStateComponent, () => Live.SiegeState);
            }

            if (Live.IsTownStatusVisible)
            {
                AddText(builder, "town-status", Live.TownStatusComponent, TownStatusText);
            }
        }

        /// <summary>The stored wielder as the one control the game draws for it: the wielder's name,
        /// and the click that ejects them.</summary>
        private void AddStoredWielder(GraphBuilder builder)
        {
            if (!Live.IsStoredWielderVisible)
            {
                return;
            }

            string name = Live.StoredWielderName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.StoredWielderName,
                () => Live.ActivateEjectWielder(),
                Live.IsEjectWielderEnabled,
                Live.StoredWielderTooltip);
            Component drawn = Live.StoredWielderButton;
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(drawn);
            builder.AddItem(new DrawnNode(ControlId.For(drawn, "map-entity:stored-wielder"), vtable, drawn));
        }

        private void AddDescriptionRows(GraphBuilder builder)
        {
            IReadOnlyList<MapEntityMiniMenuAdapter.DescriptionRow> rows = Live.GetDescriptionRows();
            for (int i = 0; i < rows.Count; i++)
            {
                MapEntityMiniMenuAdapter.DescriptionRow row = rows[i];
                if (row == null || row.Component == null)
                {
                    continue;
                }

                MapEntityMiniMenuAdapter.DescriptionRow it = row;
                NodeVtable vtable = GraphNodes.Paragraphs(() => it.Lines, it.GetTooltip());
                builder.AddItem(new DrawnNode(ControlId.For(it.Component, it.Id), vtable, it.Component));
            }
        }

        /// <summary>How far a raze or a conversion has got, counted off the round dots the game draws
        /// and named by the siege state it belongs to where there is one.</summary>
        private string TownStatusText()
        {
            string siege = Live.SiegeState;
            string prefix = string.IsNullOrWhiteSpace(siege) ? ModText.Get(ModStrings.Screens.TownStatus) : siege;
            return ModText.Get(
                ModStrings.Screens.TownStatusRounds,
                prefix,
                Live.TownStatusRoundsComplete,
                Live.TownStatusRoundsRemaining);
        }

        /// <summary>One line the menu draws and the player only reads, keyed by the component the game
        /// draws it as where there is one and by a subject of the screen's own where there is not.
        /// </summary>
        private void AddText(GraphBuilder builder, string key, Component drawn, Func<string> label)
        {
            NodeVtable vtable = GraphNodes.Text(label);
            if (drawn != null)
            {
                builder.AddItem(new DrawnNode(ControlId.For(drawn, "map-entity:" + key), vtable, drawn));
                return;
            }

            builder.AddItem(new SyntheticNode(ControlId.For(Marker(key), "map-entity:" + key), vtable));
        }

        /// <summary>The same, for a text the menu may have written in more than one paragraph: one
        /// spoken line, one review-buffer line per paragraph.</summary>
        private void AddParagraphs(GraphBuilder builder, string key, Component drawn, Func<IList<string>> lines)
        {
            NodeVtable vtable = GraphNodes.Paragraphs(lines);
            if (drawn != null)
            {
                builder.AddItem(new DrawnNode(ControlId.For(drawn, "map-entity:" + key), vtable, drawn));
                return;
            }

            builder.AddItem(new SyntheticNode(ControlId.For(Marker(key), "map-entity:" + key), vtable));
        }

        // ---- the action strip ----

        private void BuildActions(GraphBuilder builder)
        {
            IReadOnlyList<MapEntityMiniMenuAdapter.ActionButton> actions = Live.GetActions();
            for (int i = 0; i < actions.Count; i++)
            {
                MapEntityMiniMenuAdapter.ActionButton action = actions[i];
                if (action == null || action.Component == null)
                {
                    continue;
                }

                MapEntityMiniMenuAdapter.ActionButton it = action;
                NodeVtable vtable = GraphNodes.Button(
                    () => it.Label,
                    () => it.Activate(),
                    it.IsEnabled,
                    it.GetTooltip());
                vtable.OnFocusVisual = () => { if (it.Focus != null) it.Focus(); };
                builder.AddItem(new DrawnNode(ControlId.For(it.Component, it.Id), vtable, it.Component));
            }
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("close"), "map-entity:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => Live.Close())));
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
