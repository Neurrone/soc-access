using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    public sealed class OnlineGameListScreen : Screen
    {
        private const string TableId = "online-games-table";
        private readonly OnlineGameListAdapter _adapter;

        public OnlineGameListScreen(OnlineGameListAdapter adapter)
            : base(BuildRoot(adapter, null))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            OnlineGameListAdapter adapter = OnlineGameListAdapter.TryCreateActive();
            return adapter != null ? new OnlineGameListScreen(adapter) : null;
        }

        public bool Matches(GameListMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null
                    && _adapter.BackButton != null
                    && _adapter.BackButton.Activate();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            Refresh(announceFocus: true);
        }

        public void Refresh(bool announceFocus)
        {
            if (!IsPresent())
            {
                return;
            }

            FocusState focusState = CaptureFocusState();
            RootWidget = BuildRoot(_adapter, focusState);
            if (announceFocus)
            {
                UIManager.RequestFocus(RootWidget);
            }
            else
            {
                UIManager.RequestFocusSilently(RootWidget);
            }
        }

        private FocusState CaptureFocusState()
        {
            Widget focusedChild = RootWidget != null ? RootWidget.FocusedChild : null;
            TableWidget table = focusedChild as TableWidget;
            MenuWidget menu = focusedChild as MenuWidget;
            MenuItemWidget menuItem = menu != null ? menu.FocusedItem : null;
            return new FocusState(
                focusedChild != null ? focusedChild.Id : null,
                menuItem != null ? menuItem.Id : null,
                table != null,
                table != null ? table.FocusedRowIndex : 0,
                table != null ? table.FocusedColumnIndex : 1);
        }

        private static ContainerWidget BuildRoot(OnlineGameListAdapter adapter, FocusState focusState)
        {
            ContainerWidget root = new ContainerWidget("online-game-list-screen", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            AddRegion(root, adapter, focusState);
            root.AddChild(new TextWidget(
                "online-game-list-status",
                () => adapter.StatusText,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsStatusVisible));
            AddGameTable(root, adapter, focusState);
            root.AddChild(new TextWidget(
                "online-game-list-selected",
                () => adapter.SelectedEntryText,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsSelectedEntryTextVisible));
            AddButton(root, "host-game", adapter.HostGameButton, adapter);
            AddButton(root, "load-and-host-game", adapter.HostSavedGameButton, adapter);
            AddButton(root, "join-with-game-code", adapter.JoinWithCodeButton, adapter);
            AddButton(root, "join-selected-game", adapter.JoinSelectedButton, adapter);
            AddButton(root, "options", adapter.OptionsButton, adapter);
            AddButton(root, "back", adapter.BackButton, adapter);

            if (focusState != null && !string.IsNullOrWhiteSpace(focusState.RootChildId))
            {
                root.SetFocusedChildById(focusState.RootChildId);
            }

            return root;
        }

        private static void AddRegion(ContainerWidget root, OnlineGameListAdapter adapter, FocusState focusState)
        {
            MenuWidget menu = new MenuWidget(
                "online-game-list-region",
                adapter.RegionLabel,
                null,
                null,
                null);
            IReadOnlyList<OnlineGameListAdapter.DropdownOption> options = adapter.GetRegionOptions();
            for (int i = 0; i < options.Count; i++)
            {
                OnlineGameListAdapter.DropdownOption option = options[i];
                menu.AddItem(new MenuItemWidget(
                    "online-game-list-region-" + option.Index,
                    () => option.Label,
                    () => option.IsSelected ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    option.Activate,
                    adapter.FocusRegion,
                    () => true));
            }

            menu.SetFocusedItemById("online-game-list-region-" + adapter.GetRegionValue());
            if (focusState != null
                && focusState.RootChildId == menu.Id
                && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
            {
                menu.SetFocusedItemById(focusState.MenuItemId);
            }

            root.AddChild(menu);
        }

        private static void AddGameTable(ContainerWidget root, OnlineGameListAdapter adapter, FocusState focusState)
        {
            TableWidget table = new TableWidget(
                TableId,
                adapter.GamesLabel,
                BuildColumns(),
                BuildRows(adapter));

            if (focusState != null && focusState.HasTableFocus)
            {
                table.SetFocusedCell(focusState.TableRowIndex, focusState.TableColumnIndex);
            }
            else
            {
                table.SetFocusedColumn(1);
            }

            root.AddChild(table);
        }

        private static IEnumerable<TableWidget.Column> BuildColumns()
        {
            yield return new TableWidget.Column("status", ModText.Get(ModStrings.UI.Status), () => TableWidget.SortDirection.None, null);
            yield return new TableWidget.Column("game-name", GameText.Get("Lobby/GameList/GameName", "Game Name"), () => TableWidget.SortDirection.None, null);
            yield return new TableWidget.Column("players", GameText.Get("Common/Players", "Players"), () => TableWidget.SortDirection.None, null);
        }

        private static IReadOnlyList<TableWidget.Row> BuildRows(OnlineGameListAdapter adapter)
        {
            List<TableWidget.Row> rows = new List<TableWidget.Row>();
            IReadOnlyList<OnlineGameListAdapter.GameRow> games = adapter.GetRows();
            for (int i = 0; i < games.Count; i++)
            {
                OnlineGameListAdapter.GameRow row = games[i];
                rows.Add(new TableWidget.Row(
                    row.Id,
                    row.Label,
                    columnId => GetCellValue(row, columnId),
                    row.GetCellTooltip,
                    row.FocusNative,
                    row.Activate));
            }

            return rows;
        }

        private static string GetCellValue(OnlineGameListAdapter.GameRow row, string columnId)
        {
            if (row == null)
            {
                return string.Empty;
            }

            switch (columnId)
            {
                case "status":
                    return row.Status;
                case "game-name":
                    return row.Name;
                case "players":
                    return row.Players;
                default:
                    return string.Empty;
            }
        }

        private static void AddButton(ContainerWidget root, string id, IMenuButtonAdapter button, OnlineGameListAdapter adapter)
        {
            root.AddChild(new ButtonWidget(
                id,
                () => button != null ? button.GetLabel() : string.Empty,
                () => button != null && button.Activate(),
                () => NativeSelectionUtility.Select(button != null ? button.Button as UnityEngine.Component : null),
                () => button != null && button.IsEnabled(),
                () => button != null && button.IsVisible(),
                () => adapter.GetButtonTooltip(button)));
        }

        private sealed class FocusState
        {
            public FocusState(string rootChildId, string menuItemId, bool hasTableFocus, int tableRowIndex, int tableColumnIndex)
            {
                RootChildId = rootChildId;
                MenuItemId = menuItemId;
                HasTableFocus = hasTableFocus;
                TableRowIndex = tableRowIndex;
                TableColumnIndex = tableColumnIndex;
            }

            public string RootChildId { get; private set; }
            public string MenuItemId { get; private set; }
            public bool HasTableFocus { get; private set; }
            public int TableRowIndex { get; private set; }
            public int TableColumnIndex { get; private set; }
        }
    }
}
