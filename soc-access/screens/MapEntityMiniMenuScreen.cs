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
    /// navigable as a graph. Four places to be, in the order the menu draws them: the heading, the
    /// details, the strip of action buttons, and the close.
    ///
    /// Measured 2026-09-07: the entity's own name ("Crowpoint") is drawn ABOVE its type name ("Small
    /// Settlement"), then the description rows, then the actions in a row of icon buttons. The
    /// heading is ONE node reading both names, in that drawn order, because they are one title split
    /// across two labels; the screen's name is the custom name where the entity has one and the type
    /// name where it does not.
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
    public sealed class MapEntityMiniMenuScreen : GraphScreen
    {
        private const string HeadingStop = "map-entity-heading";
        private const string DetailsStop = "map-entity-details";
        private const string ActionsStop = "map-entity-actions";
        private const string CloseStop = "map-entity-close";

        private readonly MapEntityMiniMenuAdapter _adapter;

        // The lines the game gives no component of their own for, and the close, keyed by subjects
        // held across rebuilds so the reconciler seats the cursor back on the same node.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public MapEntityMiniMenuScreen(MapEntityMiniMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            MapEntityMiniMenu[] menus = Resources.FindObjectsOfTypeAll<MapEntityMiniMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new MapEntityMiniMenuScreen(adapter);
                }
            }

            return null;
        }

        public MapEntityMiniMenuAdapter Adapter
        {
            get { return _adapter; }
        }

        public override string Key
        {
            get { return "map-entity-mini-menu"; }
        }

        /// <summary>The entity's own name where it has one, and its type name where it does not.
        /// </summary>
        /// <summary>None: the heading node is the start node and already says the entity's name, so a
        /// screen name would read it twice on arrival.</summary>
        public override string ScreenName
        {
            get { return null; }
        }

        /// <summary>The heading, so arrival reads what the menu is about before its details.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(HeadingStop);
            BuildHeading(builder);

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(ActionsStop);
            BuildActions(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the heading ----

        private void BuildHeading(GraphBuilder builder)
        {
            NodeVtable vtable = GraphNodes.Text(HeadingText);
            Component drawn = _adapter.EntityNameComponent;
            if (drawn != null)
            {
                builder.AddItem(new DrawnNode(ControlId.For(drawn, "map-entity:heading"), vtable, drawn));
                return;
            }

            builder.AddItem(new SyntheticNode(ControlId.For(Marker("heading"), "map-entity:heading"), vtable));
        }

        /// <summary>Both drawn names in their drawn order, the entity's own first.</summary>
        private string HeadingText()
        {
            string type = _adapter.EntityName;
            if (!_adapter.IsCustomNameVisible)
            {
                return type;
            }

            string custom = _adapter.CustomName;
            return string.IsNullOrWhiteSpace(type)
                ? custom
                : ModText.Get(ModStrings.Common.ListSeparator, custom, type);
        }

        // ---- the details, in drawn order ----

        private void BuildDetails(GraphBuilder builder)
        {
            if (_adapter.IsBlueprintDescriptionVisible)
            {
                AddText(builder, "blueprint-description", _adapter.BlueprintDescriptionComponent,
                    () => _adapter.BlueprintDescription);
            }

            AddStoredWielder(builder);
            AddDescriptionRows(builder);

            if (_adapter.IsUpgradeSummaryVisible)
            {
                AddText(builder, "upgrades", _adapter.UpgradesComponent, () => _adapter.UpgradeSummary);
            }

            if (_adapter.IsSiegeStateVisible)
            {
                AddText(builder, "siege-state", _adapter.SiegeStateComponent, () => _adapter.SiegeState);
            }

            if (_adapter.IsTownStatusVisible)
            {
                AddText(builder, "town-status", _adapter.TownStatusComponent, TownStatusText);
            }
        }

        /// <summary>The stored wielder as the one control the game draws for it: the wielder's name,
        /// and the click that ejects them.</summary>
        private void AddStoredWielder(GraphBuilder builder)
        {
            if (!_adapter.IsStoredWielderVisible)
            {
                return;
            }

            string name = _adapter.StoredWielderName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.StoredWielderName,
                () => _adapter.ActivateEjectWielder(),
                _adapter.IsEjectWielderEnabled,
                _adapter.StoredWielderTooltip);
            Component drawn = _adapter.StoredWielderButton;
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(drawn);
            builder.AddItem(new DrawnNode(ControlId.For(drawn, "map-entity:stored-wielder"), vtable, drawn));
        }

        private void AddDescriptionRows(GraphBuilder builder)
        {
            IReadOnlyList<MapEntityMiniMenuAdapter.DescriptionRow> rows = _adapter.GetDescriptionRows();
            for (int i = 0; i < rows.Count; i++)
            {
                MapEntityMiniMenuAdapter.DescriptionRow row = rows[i];
                if (row == null || row.Component == null)
                {
                    continue;
                }

                MapEntityMiniMenuAdapter.DescriptionRow it = row;
                NodeVtable vtable = GraphNodes.Text(() => it.Label, null, it.GetTooltip());
                builder.AddItem(new DrawnNode(ControlId.For(it.Component, it.Id), vtable, it.Component));
            }
        }

        /// <summary>How far a raze or a conversion has got, counted off the round dots the game draws
        /// and named by the siege state it belongs to where there is one.</summary>
        private string TownStatusText()
        {
            string siege = _adapter.SiegeState;
            string prefix = string.IsNullOrWhiteSpace(siege) ? ModText.Get(ModStrings.Screens.TownStatus) : siege;
            return ModText.Get(
                ModStrings.Screens.TownStatusRounds,
                prefix,
                _adapter.TownStatusRoundsComplete,
                _adapter.TownStatusRoundsRemaining);
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

        // ---- the action strip ----

        private void BuildActions(GraphBuilder builder)
        {
            IReadOnlyList<MapEntityMiniMenuAdapter.ActionButton> actions = _adapter.GetActions();
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
                    () => _adapter.Close())));
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
