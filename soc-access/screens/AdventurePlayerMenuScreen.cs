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
    /// The kingdom's player list, made navigable as a graph. Three places to be: the table of players,
    /// the resource summary of the ally the cursor is standing on, and the close.
    ///
    /// The players are a TABLE (owner ruling), which retires the widget screen's "selected player"
    /// indirection: the widget drew ONE resource-trade and ONE towns-trade button and pointed them at
    /// whichever row the cursor had last been on, and the sheet reads each row's own buttons in place.
    /// The player's NAME is the primary column, so Up and Down read the players and a vertical crossing
    /// in an action column says which player it landed on. The REGIONS are the captions the menu draws
    /// ("Allies", "Enemies"), so Alt+Up and Alt+Down jump between the two sides.
    ///
    /// Measured 2026-09-07: an "Allies" caption, then one row per ally - a NameButton (left
    /// non-interactable for the local player and for an AI), a partnership badge carrying the team
    /// number, and, for a living ally in your partnership, a resource-trade and a towns-trade button at
    /// the row's right - then an "Enemies" caption and one row per enemy. Both captions are drawn only
    /// while the local player HAS a partner, and the game places them by SIBLING INDEX as it sorts the
    /// rows (<c>AdventurePlayerMenu.Show</c> puts your own partnership first, then orders by
    /// partnership and team), so the captions and the rows are interleaved by their DRAWN TOP on every
    /// build rather than by the order the entries are enumerated in.
    ///
    /// The name is a BUTTON where the game leaves that button interactable - clicking it opens the
    /// platform user menu - and a plain line where it does not, which is every row of a single-player
    /// game. The team, the colour, the relation, the AI difficulty, the dead marker and the score are
    /// PARTS of that cell rather than cells of their own: none of them is a control, and reading them
    /// as the row is read is what a vertical crossing wants.
    ///
    /// The RESOURCE SUMMARY the game draws under an ally is its own stop, holding one line per resource
    /// with the amount and the income as the widget screen read them. It is declared only for a player
    /// the game draws it for, and it follows the CURSOR: every cell of a row makes its player the
    /// adapter's selected one as focus lands on it.
    ///
    /// THE CLOSE IS THE MOD'S OWN NODE: the menu draws no close control and is dismissed by clicking
    /// the blocker behind it, so the node runs the game's own <c>AdventurePlayerMenu.Hide</c>. Escape is
    /// still the game's (<c>ConsumesBack</c> false): the kingdom HUD registers <c>UI.ExitMenu</c> for
    /// this menu in <c>KingdomInformationHUD.ReregisterHotKeys</c>.
    /// </summary>
    public sealed class AdventurePlayerMenuScreen : GraphScreen
    {
        private const string PlayersStop = "adventure-players";
        private const string SummaryStop = "adventure-players-summary";
        private const string CloseStop = "adventure-players-close";
        private const string SheetKey = "adventure-players:";

        // The logical columns of a player row. The primary (0) is the name; then one column per ACTION
        // in the order the adapter always lists them, so that Down from a towns-trade button reaches
        // the next row's towns-trade button and falls to that row's name where it draws none, rather
        // than landing on a different command with the same rectangle.
        private const int FirstActionColumn = 1;
        private const int ColumnCount = 5;

        private static readonly ResourceType[] ResourceSummaryOrder =
        {
            ResourceType.Gold,
            ResourceType.Stone,
            ResourceType.Wood,
            ResourceType.Glimmerweave,
            ResourceType.AncientAmber,
            ResourceType.CelestialOre
        };

        private readonly AdventurePlayerMenuAdapter _adapter;

        // The close node has nothing on screen to key on, so it gets a subject of its own, kept across
        // rebuilds so the reconciler seats the cursor back on it.
        private readonly object _closeKey = new object();

        public AdventurePlayerMenuScreen(AdventurePlayerMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventurePlayerMenu[] menus = Resources.FindObjectsOfTypeAll<AdventurePlayerMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                AdventurePlayerMenuAdapter adapter = new AdventurePlayerMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new AdventurePlayerMenuScreen(adapter);
                }
            }

            return null;
        }

        public bool Matches(AdventurePlayerMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.Source, menu);
        }

        public override string Key
        {
            get { return "adventure-players"; }
        }

        /// <summary>The menu's own drawn title ("Players").</summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        /// <summary>The players, which are what the menu is about.</summary>
        public override object InitialFocusStop
        {
            get { return PlayersStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Called by the detector whenever the menu refreshes a row. The graph is declared
        /// afresh on every navigation operation and reads the rows off the game each time, so there is
        /// nothing here to invalidate.</summary>
        public void Refresh()
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(PlayersStop);
            BuildPlayers(builder);

            AdventurePlayerMenuAdapter.PlayerItem selected = _adapter.SelectedPlayer;
            if (selected != null && selected.HasResourceSummary)
            {
                builder.BeginStop(SummaryStop);
                BuildSummary(builder, selected);
            }

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the table of players ----

        private void BuildPlayers(GraphBuilder builder)
        {
            List<Band> bands = new List<Band>();
            AddCaption(bands, _adapter.AllyCaption);
            AddCaption(bands, _adapter.EnemyCaption);

            IReadOnlyList<AdventurePlayerMenuAdapter.PlayerItem> players = _adapter.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                AdventurePlayerMenuAdapter.PlayerItem player = players[i];
                if (player != null && player.Entry != null)
                {
                    bands.Add(new Band(null, player, Top(player.Entry)));
                }
            }

            SortByTop(bands);

            // The columns are all caption-less: the menu draws no heading band, and every cell past the
            // name is a control that says its own name, so a caption crossed into the column would be
            // the same words twice. The array's LENGTH is what makes each region read as a table.
            string[] columns = new string[ColumnCount];
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            bool opened = false;
            for (int i = 0; i < bands.Count; i++)
            {
                Band band = bands[i];
                if (band.Player == null)
                {
                    sheet.Region(band.Caption.Text, columns);
                    opened = true;
                    continue;
                }

                if (!opened)
                {
                    // No caption is drawn at all - a game with no partner - so the table is one
                    // unnamed region rather than a level of hierarchy that says nothing.
                    sheet.Region(null, columns);
                    opened = true;
                }

                sheet.RowAt(Primary(band.Player), band.Player.Entry, Cells(band.Player), band.Player.Entry);
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                // Tab into the table lands on a PLAYER; SetStart beside it because this is the first
                // stop, whose landing the reconciler would otherwise never look at.
                builder.LandStopOn(sheet.FirstRow);
                builder.SetStart(sheet.FirstRow);
            }
        }

        private static void AddCaption(List<Band> bands, AdventurePlayerMenuAdapter.CaptionItem caption)
        {
            if (caption != null && caption.IsVisible && !string.IsNullOrWhiteSpace(caption.Text))
            {
                bands.Add(new Band(caption, null, Top(caption.Component)));
            }
        }

        /// <summary>The player's own cell: the name the row draws, everything the row says ABOUT the
        /// player, and - where the game leaves that button interactable - the click that opens the
        /// platform user menu.</summary>
        private NodeVtable Primary(AdventurePlayerMenuAdapter.PlayerItem player)
        {
            AdventurePlayerMenuAdapter.PlayerItem it = player;
            AdventurePlayerMenuAdapter.ActionItem actions = player.PlatformActions;
            // No availability part on the name: the button is drawn under every row and refuses on most
            // of them, and "unavailable" there would be read as the player being unavailable.
            NodeVtable vtable = actions != null && actions.IsEnabled
                ? GraphNodes.Button(() => it.Name, () => actions.Activate(), null, it.Tooltip)
                : GraphNodes.Text(() => it.Name, null, it.Tooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(() => DeadText(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.TeamLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.ColorLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.RelationLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.AiLabel));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.ScoreText));
            return Cell(vtable, it, it.FocusNative);
        }

        /// <summary>The dead marker the row draws, in the mod's own word.</summary>
        private static string DeadText(AdventurePlayerMenuAdapter.PlayerItem player)
        {
            return player.IsDead ? ModText.Get(ModStrings.Screens.WielderDead) : null;
        }

        /// <summary>The buttons the row draws beside the name, in the order of their drawn left edges
        /// but each keeping the column its kind always has.</summary>
        private List<GraphSheet.SheetCell> Cells(AdventurePlayerMenuAdapter.PlayerItem player)
        {
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
            List<float> lefts = new List<float>();
            IReadOnlyList<AdventurePlayerMenuAdapter.ActionItem> actions = player.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                AdventurePlayerMenuAdapter.ActionItem action = actions[i];
                if (action == null || !action.IsVisible || action.Component == null)
                {
                    continue;
                }

                AdventurePlayerMenuAdapter.ActionItem it = action;
                NodeVtable vtable = GraphNodes.Button(
                    () => it.Label,
                    () => it.Activate(),
                    () => it.IsEnabled,
                    it.Tooltip);
                cells.Add(new GraphSheet.SheetCell(FirstActionColumn + i, 0, Cell(vtable, player, it.Focus)));
                lefts.Add(Left(it.Component));
            }

            SortByLeft(cells, lefts);
            return cells;
        }

        /// <summary>What every cell of a row shares: one type-ahead result per player whichever column
        /// the cursor is standing in, the game's own focus visual, and the row becoming the adapter's
        /// SELECTED player - which is what the resource summary stop reads.</summary>
        private NodeVtable Cell(NodeVtable vtable, AdventurePlayerMenuAdapter.PlayerItem player, Action focus)
        {
            AdventurePlayerMenuAdapter.PlayerItem row = player;
            Action it = focus;
            vtable.SearchText = () => row.Name;
            vtable.OnFocusVisual = () =>
            {
                _adapter.SelectedTeamId = row.TeamId;
                if (it != null)
                {
                    it();
                }
            };
            return vtable;
        }

        // ---- the selected player's resource summary ----

        /// <summary>The resource block the menu draws under an ally: one line per resource with what
        /// the ally holds and what it earns, in the game's own resource order. Keyed by the team as
        /// well as the resource, so moving the cursor to another ally is a move to other lines rather
        /// than the same lines quietly changing under it.</summary>
        private void BuildSummary(GraphBuilder builder, AdventurePlayerMenuAdapter.PlayerItem player)
        {
            AdventurePlayerMenuAdapter.PlayerItem it = player;
            for (int i = 0; i < ResourceSummaryOrder.Length; i++)
            {
                ResourceType type = ResourceSummaryOrder[i];
                NodeVtable vtable = GraphNodes.Text(
                    () => it.GetResourceLabel(type),
                    null,
                    it.GetResourceTooltip(type));
                vtable.OnFocusVisual = () => it.FocusResource(type);
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural("adventure-players:summary/" + it.TeamId + "/" + type),
                    vtable));
            }
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            builder.AddItem(new SyntheticNode(
                ControlId.For(_closeKey, "adventure-players:close"),
                GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => _adapter.Close())));
        }

        // ---- shared helpers ----

        // One line of the menu as it is drawn: either one of the two captions or one player's row.
        private struct Band
        {
            public readonly AdventurePlayerMenuAdapter.CaptionItem Caption;
            public readonly AdventurePlayerMenuAdapter.PlayerItem Player;
            public readonly float Top;

            public Band(
                AdventurePlayerMenuAdapter.CaptionItem caption,
                AdventurePlayerMenuAdapter.PlayerItem player,
                float top)
            {
                Caption = caption;
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

        /// <summary>Put a row's buttons in the order the game draws them, left to right.</summary>
        private static void SortByLeft(List<GraphSheet.SheetCell> cells, List<float> lefts)
        {
            for (int i = 1; i < cells.Count; i++)
            {
                GraphSheet.SheetCell cell = cells[i];
                float left = lefts[i];
                int j = i - 1;
                while (j >= 0 && lefts[j] > left)
                {
                    cells[j + 1] = cells[j];
                    lefts[j + 1] = lefts[j];
                    j--;
                }

                cells[j + 1] = cell;
                lefts[j + 1] = left;
            }
        }

        private static float Top(Component component)
        {
            return component != null ? component.transform.position.y : 0f;
        }

        private static float Left(Component component)
        {
            return component != null ? component.transform.position.x : 0f;
        }
    }
}
