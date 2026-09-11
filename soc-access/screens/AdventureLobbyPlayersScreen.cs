using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's player page, made navigable as a graph. Three places to be, and Tab moves between
    /// them: the table of player slots, the panel of everything else the page draws, and the header
    /// band's Back and Options.
    ///
    /// The slots are a TABLE (owner ruling), which is what retires the widget screen's "selected
    /// player" indirection: the widget drew ONE set of faction, colour, wielder, team and AI controls
    /// and pointed them at whichever slot the cursor had last been on, and the sheet reads each row's
    /// own controls in place. The player's NAME is the primary column, so Up and Down read the slots
    /// and a vertical crossing in a metadata column says which slot it landed on; Left and Right walk
    /// one slot's controls, keeping their column on the way down.
    ///
    /// The cells ARE the drawn controls, not read-only copies of them: a faction, colour, starting
    /// wielder, team or AI-difficulty cell is a combo box whose value is what the button draws and
    /// whose Enter is the button's own click, which opens the game's icon dropdown
    /// (<see cref="AdventureLobbyIconDropdownScreen"/>); an action cell is the drawn button. A control
    /// the row does not draw is skipped - an empty slot draws only Join and Add AI, an AI slot draws
    /// its difficulty and Remove AI - and the sheet matches columns by identity, so a ragged row never
    /// lands the cursor in a neighbouring column.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: the rows as
    /// <c>LobbyPlayerEntry</c>s 52 px tall at y 97 and y 151, each drawing its slot number (x 83),
    /// the name (x 147) over a <c>NameButton</c> (x 141), then FactionButton (334), ColorButton (380),
    /// StartingWielderButton (425), Partnership (471) and, on an AI row, AiModeButton (517); the
    /// row's actions on the right, JoinButton (719), PlayerSettingsButton (782) and Leave, Kick,
    /// Remove AI or Add AI (813). Online the page adds a band ACROSS THE TOP at y 43 to 82 - the game
    /// name (x 266), the Invites Only toggle (645), the game code (782) and Invite Friend (915) - and
    /// a chat button at [77,693]. The right-hand panel draws the map preview (title at y 307, its
    /// description under it), Mixed Factions (y 582), Game settings (y 616), Set Ready (y 678) and
    /// Start Game (y 676). Back is at [21,20] and Options at [1233,11].
    ///
    /// The NAME IS A BUTTON where the game draws one: <c>NameButton</c> is
    /// <c>LobbyPlayerEntry._userActionsButton</c>, drawn over the name with the tooltip "Show Player
    /// Actions", and clicking it opens the platform user menu. So the primary carries the click where
    /// the row draws that button (the offline lobby draws it on both slots) and is a plain line where
    /// it does not (the online lobby's own slot and its empty slot). The row's ready state and its
    /// DLC requirement are parts of the primary, both being drawn beside the name.
    ///
    /// Escape is CLAIMED and presses the drawn Back button: <c>LobbyMenu</c>, <c>LobbyPlayerMenu</c>
    /// and <c>LobbyNavigation</c> register no input callback at all (decompiled; the navigation only
    /// UNregisters), so the key would otherwise do nothing here.
    /// </summary>
    public sealed class AdventureLobbyPlayersScreen : LiveScreen<AdventureLobbyPlayersAdapter>
    {
        private const string PlayersStop = "lobby-players";
        private const string PanelStop = "lobby-panel";
        private const string HeaderStop = "lobby-header";
        private const string SheetKey = "lobby:";

        // The logical columns of a slot row. The primary (0) is the name; then the settings the row
        // draws left to right; then one column per ACTION, so that Down from Leave on a row that has
        // one reaches Leave on the next row that has one and falls to the row's name where it does
        // not, rather than landing on a different command with the same rectangle.
        private const int FactionColumn = 1;
        private const int ColorColumn = 2;
        private const int StartingWielderColumn = 3;
        private const int PartnershipColumn = 4;
        private const int AiDifficultyColumn = 5;
        private const int JoinColumn = 6;
        private const int PlayerSettingsColumn = 7;
        private const int LeaveColumn = 8;
        private const int KickColumn = 9;
        private const int ToggleAiColumn = 10;
        private const int ColumnCount = 11;

        // The identity of each slot row, one string instance per row kept across rebuilds. The sheet
        // keys every cell of a row off this and hands the primary the same object as its subject, so
        // the cursor seats back on the column it left when the lobby redraws the row under it - which
        // is what a non-colour pick from the icon dropdown does, the dropdown hiding a frame before
        // the row is rebuilt. It is the row's POSITION in the drawn list, not anything read off the
        // game's row: LobbyPlayerEntry.TeamId is the team state's id and reads -1 while the entry
        // holds no state, which would move the whole row's keys mid-redraw and collapse two rows onto
        // one. The text is the one the rows have always had, the drawn list being the map's first N
        // slots in team order.
        private readonly Dictionary<int, string> _rowKeys = new Dictionary<int, string>();

        /// <summary>The lobby page, off the lobby navigator (<see cref="LobbySources"/>).</summary>
        protected override object ResolveMenu()
        {
            return LobbySources.Lobby.Current;
        }

        /// <summary>Everything the page reads that is not on the lobby menu itself: the player menu
        /// whose row list the slots come from, the map settings band, the ready and start buttons,
        /// and the online band. All of them are in the same scene as the lobby menu, so they are
        /// resolved once here rather than looked for again per frame.</summary>
        protected override AdventureLobbyPlayersAdapter Adapt(object menu)
        {
            return new AdventureLobbyPlayersAdapter(
                (LobbyMenu)menu,
                LobbySources.Navigation.Current,
                LobbySources.PlayerMenu.Current,
                LobbySources.MapSettings.Current,
                LobbySources.LobbyButtons.Current,
                LobbySources.MultiplayerPanel.Current);
        }

        public override string Key
        {
            get { return "lobby-players"; }
        }

        /// <summary>Layer 4: the lobby proper, over the map pages it was reached through.</summary>
        public override int Layer
        {
            get { return 4; }
        }

        /// <summary>The header band's drawn title ("Conquest", "Online Conquest").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        /// <summary>The slots, which are what the page is about.</summary>
        public override object InitialFocusStop
        {
            get { return PlayersStop; }
        }

        public override bool IsWorkable
        {
            get { return Live != null && Live.IsInteractive(); }
        }

        public override IMenuButtonAdapter BackButton
        {
            get { return Live != null ? Live.BackButton : null; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(PlayersStop);
            BuildPlayers(builder);

            builder.BeginStop(PanelStop);
            BuildPanel(builder);

            builder.BeginStop(HeaderStop);
            BuildHeader(builder);
        }

        // ---- the table of slots ----

        private void BuildPlayers(GraphBuilder builder)
        {
            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            // The columns are all caption-less: the page draws no heading band, and every cell of a
            // row is a control that says its own name, so a caption crossed into the column would be
            // the same word twice. The array's LENGTH is what makes the region read as a table.
            sheet.Region(Live.PlayersLabel, new string[ColumnCount]);

            IReadOnlyList<AdventureLobbyPlayersAdapter.PlayerSlotItem> slots = Live.GetPlayerSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                AdventureLobbyPlayersAdapter.PlayerSlotItem slot = slots[i];
                if (slot == null || slot.Entry == null)
                {
                    continue;
                }

                sheet.RowAt(Primary(slot), RowKey(i), Cells(slot), slot.Entry);
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                // Tab into the table lands on a SLOT; SetStart beside it because this is the first
                // stop, whose landing the reconciler would otherwise never look at.
                builder.LandStopOn(sheet.FirstRow);
                builder.SetStart(sheet.FirstRow);
            }
        }

        /// <summary>The slot's own cell: the name the row draws, its ready state and its DLC
        /// requirement, and - where the game draws the button under the name - the click that opens
        /// the platform user menu.</summary>
        private NodeVtable Primary(AdventureLobbyPlayersAdapter.PlayerSlotItem slot)
        {
            AdventureLobbyPlayersAdapter.PlayerSlotItem it = slot;
            AdventureLobbyPlayersAdapter.LobbyButtonItem actions = slot.PlayerActionsButton;
            bool drawn = actions != null && actions.IsVisible;
            // No availability part: the game only draws this button while there is an action to take,
            // so "unavailable" on it would be read as the slot being unavailable.
            NodeVtable vtable = drawn
                ? GraphNodes.Button(() => it.Name, () => actions.Activate(), null, actions.Tooltip)
                : GraphNodes.Text(() => it.Name, null, it.Tooltip);
            vtable.Announcements.Add(GraphNodes.ValuePart(() => ReadyText(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => it.DlcRequirementText));
            vtable.OnFocusVisual = it.FocusNative;
            return vtable;
        }

        /// <summary>The row's ready marker, in the mod's own words. Drawn online only.</summary>
        private static string ReadyText(AdventureLobbyPlayersAdapter.PlayerSlotItem slot)
        {
            if (!slot.IsReadyStateDrawn)
            {
                return null;
            }

            return ModText.Get(slot.IsReady ? ModStrings.Screens.Ready : ModStrings.Screens.NotReady);
        }

        /// <summary>The controls the row draws beside the name: the five settings in their drawn
        /// order, then the row's commands sorted by their drawn left edge.</summary>
        private List<GraphSheet.SheetCell> Cells(AdventureLobbyPlayersAdapter.PlayerSlotItem slot)
        {
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
            AddSetting(cells, slot, FactionColumn, Live.FactionLabel, slot.FactionButton);
            AddSetting(cells, slot, ColorColumn, Live.ColorLabel, slot.ColorButton);
            AddSetting(cells, slot, StartingWielderColumn, Live.StartingWielderLabel, slot.StartingWielderButton);
            AddSetting(cells, slot, PartnershipColumn, Live.PartnershipLabel, slot.PartnershipButton);
            AddSetting(cells, slot, AiDifficultyColumn, Live.AiDifficultyLabel, slot.AiDifficultyButton);

            List<GraphSheet.SheetCell> actions = new List<GraphSheet.SheetCell>();
            List<float> lefts = new List<float>();
            AddAction(actions, lefts, slot, JoinColumn, slot.JoinButton);
            AddAction(actions, lefts, slot, PlayerSettingsColumn, slot.PlayerSettingsButton);
            AddAction(actions, lefts, slot, LeaveColumn, slot.LeaveButton);
            AddAction(actions, lefts, slot, KickColumn, slot.KickButton);
            AddAction(actions, lefts, slot, ToggleAiColumn, slot.ToggleAiButton);
            DrawnOrder.SortAscending(actions, lefts);
            cells.AddRange(actions);
            return cells;
        }

        /// <summary>One of the row's value buttons, as the combo box it is: the caption is the game's
        /// own name for the setting, the value is what the button draws, and Enter is the button's own
        /// click, which opens the game's icon dropdown.</summary>
        private void AddSetting(
            List<GraphSheet.SheetCell> cells,
            AdventureLobbyPlayersAdapter.PlayerSlotItem slot,
            int column,
            string caption,
            AdventureLobbyPlayersAdapter.LobbyButtonItem button)
        {
            if (button == null || !button.IsVisible)
            {
                return;
            }

            AdventureLobbyPlayersAdapter.LobbyButtonItem it = button;
            NodeVtable vtable = GraphNodes.ComboBox(
                () => caption,
                () => it.Label,
                () => it.Activate(),
                () => it.IsEnabled,
                it.Tooltip);
            cells.Add(new GraphSheet.SheetCell(column, 0, Cell(vtable, slot, it)));
        }

        /// <summary>One of the row's commands (Join, Player settings, Leave, Kick, Remove AI, Add AI),
        /// as the drawn button it is.</summary>
        private void AddAction(
            List<GraphSheet.SheetCell> cells,
            List<float> lefts,
            AdventureLobbyPlayersAdapter.PlayerSlotItem slot,
            int column,
            AdventureLobbyPlayersAdapter.LobbyButtonItem button)
        {
            if (button == null || !button.IsVisible)
            {
                return;
            }

            AdventureLobbyPlayersAdapter.LobbyButtonItem it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => it.Label,
                () => it.Activate(),
                () => it.IsEnabled,
                it.Tooltip);
            cells.Add(new GraphSheet.SheetCell(column, 0, Cell(vtable, slot, it)));
            lefts.Add(DrawnOrder.LeftOf(it.Button));
        }

        /// <summary>What every cell of a row shares: the game's own focus visual, and one type-ahead
        /// result per slot whichever column the cursor is standing in.</summary>
        private static NodeVtable Cell(
            NodeVtable vtable,
            AdventureLobbyPlayersAdapter.PlayerSlotItem slot,
            AdventureLobbyPlayersAdapter.LobbyButtonItem button)
        {
            AdventureLobbyPlayersAdapter.PlayerSlotItem row = slot;
            AdventureLobbyPlayersAdapter.LobbyButtonItem it = button;
            vtable.OnFocusVisual = it.Focus;
            vtable.SearchText = () => row.Name;
            return vtable;
        }

        // ---- the panel of everything else the page draws ----

        private void BuildPanel(GraphBuilder builder)
        {
            AddMultiplayerBand(builder);
            AddMapPreview(builder);
            AddMixedFactions(builder);
            AddGameSettings(builder);
            AddLobbyButton(builder, "lobby:set-ready", Live.GetSetReadyButton());
            AddLobbyButton(builder, "lobby:set-not-ready", Live.GetSetNotReadyButton());
            AddLobbyButton(builder, "lobby:start-game", Live.GetStartGameButton());
            AddChatButton(builder);
        }

        /// <summary>The online band across the top of the page, sorted by drawn left edge: the game's
        /// name, the Invites Only toggle, the code, Invite Friend, and the crossplay pair where a
        /// platform draws them. It is declared FIRST because it is drawn first - measured at y 43 to
        /// 82, above the map preview at y 97 - though the widget screen read it after the name too.
        /// </summary>
        private void AddMultiplayerBand(GraphBuilder builder)
        {
            AdventureLobbyPlayersAdapter.MultiplayerPanelItem panel = Live.GetMultiplayerPanel();
            if (panel == null)
            {
                return;
            }

            List<NodeDeclaration> nodes = new List<NodeDeclaration>();
            List<float> lefts = new List<float>();

            Component name = panel.GameNameLabel;
            if (panel.IsGameNameVisible && name != null)
            {
                Add(nodes, lefts, new DrawnNode(
                    ControlId.For(name, "lobby:game-name"),
                    GraphNodes.Text(() => panel.GameName),
                    name), DrawnOrder.LeftOf(name));
            }

            Component gameCode = panel.GameCodeField;
            if (panel.IsGameCodeVisible && gameCode != null)
            {
                NodeVtable code = GraphNodes.Button(
                    () => panel.CopyGameCodeLabel,
                    () => CopyGameCode(panel),
                    null,
                    panel.GameCodeTooltip);
                // NEITHER SELECTED NOR AIMED AT: the game draws the code in one of its own text
                // fields, and selecting that field - which a focus visual does, and which drawing its
                // tooltip does too - hands it the keyboard and the mod stands down until something
                // takes the focus off it again. Measured 2026-09-06: landing here answered
                // "standing down" to every key. Its tooltip is still in the review buffer.
                GraphNodes.DoNotDrawTooltip(code);
                Add(nodes, lefts, new DrawnNode(
                    ControlId.For(gameCode, "lobby:game-code"), code, gameCode), DrawnOrder.LeftOf(gameCode));
            }

            AddToggle(nodes, lefts, "lobby:invites-only", panel.InvitesOnly);
            AddPanelButton(nodes, lefts, "lobby:invite-friend", panel.InviteFriendButton);
            AddToggle(nodes, lefts, "lobby:crossplay", panel.Crossplay);

            if (panel.IsXboxCrossplayInformationVisible)
            {
                Add(nodes, lefts, new SyntheticNode(
                    ControlId.For(Marker("lobby:crossplay-information"), "lobby:crossplay-information"),
                    GraphNodes.Text(() => panel.XboxCrossplayInformation)), float.MaxValue);
            }

            DrawnOrder.SortAscending(nodes, lefts);
            for (int i = 0; i < nodes.Count; i++)
            {
                builder.AddItem(nodes[i]);
            }
        }

        /// <summary>The map preview beside the table, as the one line it is: the name the panel draws,
        /// then what it draws under it as a section, so the review buffer holds the description one
        /// drawn line at a time.</summary>
        private void AddMapPreview(GraphBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(Live.MapTitle) && string.IsNullOrWhiteSpace(Live.MapDescription))
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => Live.MapTitle, live: true, kind: AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection>
                {
                    NodeSection.Composed(() => SpokenLines.Of(new[] { Live.MapDescription })),
                },
            };
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("map-preview"), "lobby:map-preview"),
                vtable));
        }

        private void AddMixedFactions(GraphBuilder builder)
        {
            AdventureLobbyPlayersAdapter.MixedFactionsItem item = Live.GetMixedFactionsItem();
            if (item == null || !item.IsVisible)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Checkbox(
                () => item.Label,
                () => item.IsChecked,
                item.Toggle,
                () => item.IsEnabled,
                item.Tooltip);
            vtable.OnFocusVisual = item.Focus;
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("mixed-factions"), "lobby:mixed-factions"),
                vtable));
        }

        private void AddGameSettings(GraphBuilder builder)
        {
            AdventureLobbyPlayersAdapter.LobbyPlayerSettingsItem item = Live.GetSettingsItem();
            if (item == null || !item.IsVisible)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => item.Label,
                () => item.Activate(),
                () => item.IsEnabled,
                item.Tooltip);
            vtable.OnFocusVisual = item.Focus;
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("game-settings"), "lobby:game-settings"),
                vtable));
        }

        /// <summary>The button the game draws to open the chat, which is the keyboard's only way in.
        /// Drawn in the online lobby only (measured at [77,693]).</summary>
        private void AddChatButton(GraphBuilder builder)
        {
            ChatAdapter chat = Chat();
            if (chat == null || !chat.IsButtonVisible())
            {
                return;
            }

            ChatAdapter it = chat;
            NodeVtable vtable = GraphNodes.Button(
                () => ChatButtonText.Label(it),
                () => it.Open(),
                it.IsButtonEnabled,
                it.ButtonTooltip);
            vtable.OnFocusVisual = it.FocusButton;
            builder.AddItem(new DrawnNode(ControlId.For(it.Button, "lobby:chat"), vtable, it.Button));
        }

        private ChatAdapter Chat()
        {
            return ChatSource.Current;
        }

        private void AddLobbyButton(GraphBuilder builder, string key, AdventureLobbyPlayersAdapter.LobbyButtonItem item)
        {
            if (item == null || !item.IsVisible || item.Button == null)
            {
                return;
            }

            AdventureLobbyPlayersAdapter.LobbyButtonItem it = item;
            NodeVtable vtable = GraphNodes.Button(
                () => it.Label,
                () => it.Activate(),
                () => it.IsEnabled,
                it.Tooltip);
            vtable.OnFocusVisual = it.Focus;
            builder.AddItem(new DrawnNode(ControlId.For(it.Button, key), vtable, it.Button));
        }

        private void AddPanelButton(
            List<NodeDeclaration> nodes,
            List<float> lefts,
            string key,
            AdventureLobbyPlayersAdapter.LobbyButtonItem item)
        {
            if (item == null || !item.IsVisible || item.Button == null)
            {
                return;
            }

            AdventureLobbyPlayersAdapter.LobbyButtonItem it = item;
            NodeVtable vtable = GraphNodes.Button(
                () => it.Label,
                () => it.Activate(),
                () => it.IsEnabled,
                it.Tooltip);
            vtable.OnFocusVisual = it.Focus;
            Add(nodes, lefts, new DrawnNode(ControlId.For(it.Button, key), vtable, it.Button), DrawnOrder.LeftOf(it.Button));
        }

        private void AddToggle(
            List<NodeDeclaration> nodes,
            List<float> lefts,
            string key,
            AdventureLobbyPlayersAdapter.ToggleItem item)
        {
            if (item == null || !item.IsVisible)
            {
                return;
            }

            AdventureLobbyPlayersAdapter.ToggleItem it = item;
            Component subject = it.Subject;
            if (subject == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Checkbox(
                () => it.Label,
                () => it.IsChecked,
                it.Toggle,
                () => it.IsEnabled,
                it.Tooltip);
            vtable.OnFocusVisual = it.Focus;
            Add(nodes, lefts, new DrawnNode(ControlId.For(subject, key), vtable, subject), DrawnOrder.LeftOf(subject));
        }

        // ---- the header band ----

        private void BuildHeader(GraphBuilder builder)
        {
            // Back (x 21) then Options (x 1233), left to right.
            AddHeaderButton(builder, "lobby:back", Live.BackButton);
            AddHeaderButton(builder, "lobby:options", Live.OptionsButton);
        }

        private void AddHeaderButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            IMenuButtonAdapter it = button;
            NodeVtable vtable = GraphNodes.Button(
                it.GetLabel,
                () => it.Activate(),
                it.IsEnabled,
                Live.GetButtonTooltip(it));
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(it.Button);
            builder.AddItem(new DrawnNode(ControlId.For(it.Button, key), vtable, it.Button));
        }

        // ---- shared helpers ----

        private static void Add<T>(List<T> items, List<float> lefts, T item, float left)
        {
            items.Add(item);
            lefts.Add(left);
        }

        /// <summary>The row's identity, minted once per position and handed back as the same string
        /// instance every frame.</summary>
        private string RowKey(int index)
        {
            string key;
            if (!_rowKeys.TryGetValue(index, out key))
            {
                key = "lobby-player-slot-" + (index + 1);
                _rowKeys.Add(index, key);
            }

            return key;
        }
        /// <summary>Put the game's code on the clipboard and say so, because nothing on the screen
        /// changes to show that it happened.</summary>
        private static bool CopyGameCode(AdventureLobbyPlayersAdapter.MultiplayerPanelItem panel)
        {
            if (!panel.CopyGameCodeToClipboard())
            {
                return false;
            }

            SpeechPipeline.Output(new SpeechRequest(
                ModText.Get(ModStrings.Screens.CopiedGameCodeToClipboard),
                interrupt: false));
            return true;
        }

    }
}
