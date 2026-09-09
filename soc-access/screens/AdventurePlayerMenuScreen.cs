using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Economy;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The kingdom's player list, made navigable as a graph. Two places to be: the players and the
    /// close.
    ///
    /// The players are a LIST OF GROUPS (owner ruling 2026-09-07), not a table. Measured the same
    /// day: an "Allies" caption, then one row per ally - a name button (which the game leaves
    /// interactable only for a remote human, where clicking it opens the platform user menu), a
    /// partnership badge carrying the team number, and, for a living ally in your partnership, a
    /// Resources button and a Towns button at the row's right, with a band of that ally's treasury
    /// drawn under the row (gold with its income, then stone, wood, glimmerweave, ancient amber and
    /// celestial ore) - then an "Enemies" caption and one row per enemy. Both captions are drawn only
    /// while the local player HAS a partner, and the game places them by SIBLING INDEX as it sorts
    /// the rows (<c>AdventurePlayerMenu.Show</c> puts your own partnership first, then orders by
    /// partnership and team), so the captions and the rows are interleaved by their DRAWN TOP on
    /// every build rather than by the order the entries are enumerated in.
    ///
    /// WHY A GROUP. The band and the row's buttons belong to the row, and neither survived being read
    /// any other way: as cells of a table the band was not reachable at all (it is drawn under the
    /// row, not beside it) and the buttons were columns a vertical crossing had to be talked through,
    /// and as a separate Tab-stop the band was a place with no name that silently changed contents as
    /// the cursor moved. So a row that draws either of them is an expandable group: Right opens it and
    /// descends, Left closes it, and the group's own readout is the row. A row that draws NEITHER -
    /// your own, and every enemy - has nothing to hold and stays a plain line.
    ///
    /// The name is the group's or the line's label, and the team, the colour, the relation, the AI
    /// difficulty, the dead marker and the score are PARTS of it rather than lines of their own: none
    /// of them is a control. Where the game leaves the name button interactable the label takes that
    /// click; where it does not there is no click at all, as with the main menu's foldouts.
    ///
    /// THE BAND IS ONE LINE (owner ruling 2026-09-07): the group's first child, labelled with the
    /// gold figure and its income and carrying the other five resources as further parts, so it is
    /// spoken as one line and held in the review buffer as one line per resource. The six tooltips
    /// are not attached - they only repeat the figures. The buttons the row draws follow it, in the
    /// order the adapter always lists them.
    ///
    /// The REGIONS are the captions the menu draws ("Allies", "Enemies"), so Alt+Up and Alt+Down jump
    /// between the two sides; a game with no partner draws neither caption and gets no region.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control and is dismissed by clicking
    /// the blocker behind it, so the node runs the game's own <c>AdventurePlayerMenu.Hide</c>. Escape
    /// is still the game's (<c>ConsumesBack</c> false): the kingdom HUD registers <c>UI.ExitMenu</c>
    /// for this menu in <c>KingdomInformationHUD.ReregisterHotKeys</c>.
    /// </summary>
    public sealed class AdventurePlayerMenuScreen : LiveScreen<AdventurePlayerMenuAdapter>
    {
        private const string PlayersStop = "adventure-players";
        private const string CloseStop = "adventure-players-close";

        private static readonly ResourceType[] ResourceSummaryOrder =
        {
            ResourceType.Gold,
            ResourceType.Stone,
            ResourceType.Wood,
            ResourceType.Glimmerweave,
            ResourceType.AncientAmber,
            ResourceType.CelestialOre
        };

        // The close node has nothing on screen to key on, so it gets a subject of its own, kept across
        // rebuilds so the reconciler seats the cursor back on it.
        private readonly object _closeKey = new object();

        /// <summary>The kingdom HUD holds this menu in a field of its own
        /// (<see cref="HudSources"/>).</summary>
        protected override object ResolveMenu()
        {
            return HudSources.PlayerMenu.Current;
        }

        protected override AdventurePlayerMenuAdapter Adapt(object menu)
        {
            return new AdventurePlayerMenuAdapter((AdventurePlayerMenu)menu);
        }

        public override string Key
        {
            get { return "adventure-players"; }
        }

        /// <summary>Layer 20: an in-game panel over the map.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The menu's own drawn title ("Players").</summary>
        public override string ScreenName
        {
            get
            {
                string title = Live != null ? Live.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        /// <summary>The players, which are what the menu is about.</summary>
        public override object InitialFocusStop
        {
            get { return PlayersStop; }
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

            builder.BeginStop(PlayersStop);
            BuildPlayers(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the players ----

        private void BuildPlayers(GraphBuilder builder)
        {
            List<Band> bands = new List<Band>();
            AddCaption(bands, Live.AllyCaption, "allies");
            AddCaption(bands, Live.EnemyCaption, "enemies");

            IReadOnlyList<AdventurePlayerMenuAdapter.PlayerItem> players = Live.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                AdventurePlayerMenuAdapter.PlayerItem player = players[i];
                if (player != null && player.Entry != null)
                {
                    bands.Add(new Band(null, null, player, Top(player.Entry)));
                }
            }

            SortByTop(bands);

            ControlId first = null;
            bool captioned = false;
            for (int i = 0; i < bands.Count; i++)
            {
                Band band = bands[i];
                if (band.Player == null)
                {
                    // A caption opens a side of the list and closes the one before it; the rows under
                    // it are the region Alt+Up and Alt+Down jump to.
                    if (captioned)
                    {
                        builder.PopContext();
                    }

                    builder.PushContext(band.Caption.Text);
                    builder.SetRegion("adventure-players:side/" + band.RegionKey);
                    captioned = true;
                    continue;
                }

                ControlId id = BuildPlayer(builder, band.Player);
                if (first == null)
                {
                    first = id;
                }
            }

            if (captioned)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
            if (first != null)
            {
                // Tab into the list lands on a PLAYER; SetStart beside it because this is the first
                // stop, whose landing the reconciler would otherwise never look at.
                builder.LandStopOn(first);
                builder.SetStart(first);
            }
        }

        private static void AddCaption(
            List<Band> bands,
            AdventurePlayerMenuAdapter.CaptionItem caption,
            string regionKey)
        {
            if (caption != null && caption.IsVisible && !string.IsNullOrWhiteSpace(caption.Text))
            {
                bands.Add(new Band(caption, regionKey, null, Top(caption.Component)));
            }
        }

        /// <summary>One player: a plain line where the row holds nothing, and an expandable group
        /// where it draws a treasury band or a button of its own.</summary>
        /// <returns>The line's or the group header's id.</returns>
        private ControlId BuildPlayer(GraphBuilder builder, AdventurePlayerMenuAdapter.PlayerItem player)
        {
            ControlId id = ControlId.For(player.Entry, "adventure-players:player/" + player.TeamId);
            List<AdventurePlayerMenuAdapter.ActionItem> actions = VisibleActions(player);
            bool hasBand = player.HasResourceSummary;
            if (!hasBand && actions.Count == 0)
            {
                builder.AddItem(new DrawnNode(id, PlayerLabel(player, asGroup: false), player.Entry));
                return id;
            }

            // Expanded out of the builder's own persistent set, keyed by the team, so a group the
            // player opened stays open across the rebuild every navigation operation makes.
            builder.BeginGroup(new DrawnNode(id, PlayerLabel(player, asGroup: true), player.Entry));
            if (hasBand)
            {
                BuildResourceBand(builder, player);
            }

            for (int i = 0; i < actions.Count; i++)
            {
                AdventurePlayerMenuAdapter.ActionItem it = actions[i];
                NodeVtable vtable = GraphNodes.Button(
                    () => it.Label,
                    () => it.Activate(),
                    () => it.IsEnabled,
                    it.Tooltip);
                Follow(vtable, it.Focus);
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Component, "adventure-players:action/" + it.Id),
                    vtable,
                    it.Component));
            }

            builder.EndGroup();
            return id;
        }

        /// <summary>The buttons the row is really drawing, in the order the adapter always lists them
        /// so that a key made from a button's kind stays on that kind whichever buttons a row leaves
        /// out.</summary>
        private static List<AdventurePlayerMenuAdapter.ActionItem> VisibleActions(
            AdventurePlayerMenuAdapter.PlayerItem player)
        {
            List<AdventurePlayerMenuAdapter.ActionItem> visible =
                new List<AdventurePlayerMenuAdapter.ActionItem>();
            IReadOnlyList<AdventurePlayerMenuAdapter.ActionItem> actions = player.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                AdventurePlayerMenuAdapter.ActionItem action = actions[i];
                if (action != null && action.IsVisible && action.Component != null)
                {
                    visible.Add(action);
                }
            }

            return visible;
        }

        /// <summary>The row's own readout: the name the menu draws, everything the row says ABOUT the
        /// player, and - where the game leaves the name button interactable - the click that opens the
        /// platform user menu. A group with no such click has no activation at all, as the main menu's
        /// foldouts do not.</summary>
        private NodeVtable PlayerLabel(AdventurePlayerMenuAdapter.PlayerItem player, bool asGroup)
        {
            AdventurePlayerMenuAdapter.PlayerItem it = player;
            AdventurePlayerMenuAdapter.ActionItem actions = player.PlatformActions;
            bool clickable = actions != null && actions.IsEnabled;
            // No availability part on the name: the button is drawn under every row and refuses on most
            // of them, and "unavailable" there would be read as the player being unavailable.
            NodeVtable vtable;
            if (asGroup)
            {
                vtable = GraphNodes.Group(
                    () => it.Name,
                    clickable ? (Action)(() => actions.Activate()) : null,
                    null,
                    it.Tooltip);
            }
            else
            {
                vtable = clickable
                    ? GraphNodes.Button(() => it.Name, () => actions.Activate(), null, it.Tooltip)
                    : GraphNodes.Text(() => it.Name, null, it.Tooltip);
            }

            vtable.Announcements.Add(GraphNodes.ValuePart(() => DeadText(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.TeamLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.ColorLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.RelationLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.AiLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.ScoreText));
            Follow(vtable, player.FocusNative);
            return vtable;
        }

        /// <summary>The dead marker the row draws, in the mod's own word.</summary>
        private static string DeadText(AdventurePlayerMenuAdapter.PlayerItem player)
        {
            return player.IsDead ? ModText.Get(ModStrings.Screens.WielderDead) : null;
        }

        /// <summary>What the row and everything under it share: the game's own focus visual.</summary>
        private static void Follow(NodeVtable vtable, Action focus)
        {
            vtable.OnFocusVisual = focus;
        }

        /// <summary>The treasury band the menu draws under an ally, as ONE line: the gold figure and
        /// its income as the label, the other five resources as further parts in the game's own
        /// resource order. Spoken it is one line; in the review buffer it is one line per resource.
        /// Keyed by the team, so moving to another ally is a move to another line rather than the same
        /// line quietly changing under the cursor.</summary>
        private static void BuildResourceBand(
            GraphBuilder builder,
            AdventurePlayerMenuAdapter.PlayerItem player)
        {
            AdventurePlayerMenuAdapter.PlayerItem it = player;
            ResourceType lead = ResourceSummaryOrder[0];
            NodeVtable vtable = GraphNodes.Text(() => it.GetResourceLabel(lead));
            for (int i = 1; i < ResourceSummaryOrder.Length; i++)
            {
                ResourceType type = ResourceSummaryOrder[i];
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.GetResourceLabel(type), watch: false));
            }

            vtable.OnFocusVisual = () => it.FocusResource(lead);
            builder.AddItem(new SyntheticNode(
                ControlId.Structural("adventure-players:resources/" + it.TeamId),
                vtable));
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            builder.AddItem(new SyntheticNode(
                ControlId.For(_closeKey, "adventure-players:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => Live.Close())));
        }

        // ---- shared helpers ----

        // One line of the menu as it is drawn: either one of the two captions or one player's row.
        private struct Band
        {
            public readonly AdventurePlayerMenuAdapter.CaptionItem Caption;
            public readonly string RegionKey;
            public readonly AdventurePlayerMenuAdapter.PlayerItem Player;
            public readonly float Top;

            public Band(
                AdventurePlayerMenuAdapter.CaptionItem caption,
                string regionKey,
                AdventurePlayerMenuAdapter.PlayerItem player,
                float top)
            {
                Caption = caption;
                RegionKey = regionKey;
                Player = player;
                Top = top;
            }
        }

        /// <summary>Put the captions and the rows in the order the menu draws them, top to bottom. An
        /// insertion sort: there are a handful of lines and the order must be stable where two of them
        /// share a top edge.</summary>
        private static void SortByTop(List<Band> bands)
        {
            for (int i = 1; i < bands.Count; i++)
            {
                Band band = bands[i];
                int j = i - 1;
                while (j >= 0 && bands[j].Top < band.Top)
                {
                    bands[j + 1] = bands[j];
                    j--;
                }

                bands[j + 1] = band;
            }
        }

        private static float Top(Component component)
        {
            return component != null ? component.transform.position.y : 0f;
        }
    }
}
