using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class AdventureLobbyMapSelectScreen : Screen
    {
        private const string TableId = "map-select-table";
        private static readonly string[] FilterIds =
        {
            "filter-map-type",
            "filter-tag",
            "filter-win-condition",
            "filter-players",
            "filter-size",
            "filter-completed",
            "filter-content-profile"
        };

        private readonly AdventureLobbyMapSelectAdapter _adapter;

        public AdventureLobbyMapSelectScreen(AdventureLobbyMapSelectAdapter adapter)
            : base(BuildRootWidget(adapter, null))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyMapSelectAdapter adapter = FindActiveMapSelectMenu(null);
            return adapter != null ? new AdventureLobbyMapSelectScreen(adapter) : null;
        }

        public bool Matches(MapSelectMenu menu)
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

            RootWidget = BuildRootWidget(_adapter, CaptureFocusState());
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

        private static ContainerWidget BuildRootWidget(AdventureLobbyMapSelectAdapter adapter, FocusState focusState)
        {
            ContainerWidget root = new ContainerWidget(
                "adventure-lobby-map-select-screen",
                adapter != null ? adapter.Title : string.Empty);

            AddFilterMenus(root, adapter, focusState);
            AddOptionalButton(root, "clear-filters", adapter != null ? adapter.GetClearFiltersButton() : null);
            AddMapTable(root, adapter, focusState);
            AddDetails(root, adapter);
            AddOptionalButton(root, "confirm", adapter != null ? adapter.SelectButton : null);
            AddOptionalButton(root, "options", adapter != null ? adapter.OptionsButton : null);
            AddOptionalButton(root, "back", adapter != null ? adapter.BackButton : null);

            if (focusState != null && !string.IsNullOrWhiteSpace(focusState.RootChildId))
            {
                root.SetFocusedChildById(focusState.RootChildId);
            }

            return root;
        }

        private static void AddFilterMenus(ContainerWidget root, AdventureLobbyMapSelectAdapter adapter, FocusState focusState)
        {
            if (root == null || adapter == null)
            {
                return;
            }

            IReadOnlyList<MapSelectFilterAdapter> filters = adapter.GetFilters();
            for (int i = 0; i < filters.Count; i++)
            {
                MapSelectFilterAdapter filter = filters[i];
                if (filter == null)
                {
                    continue;
                }

                string filterId = i < FilterIds.Length ? FilterIds[i] : "filter-" + i;
                MenuWidget menu = new MenuWidget(filterId, filter.Label, () => filter.IsVisible, filter.OpenNative, filter.CloseNative);
                AddFilterOptions(menu, filter);
                if (focusState != null
                    && focusState.RootChildId == filterId
                    && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
                {
                    menu.SetFocusedItemById(focusState.MenuItemId);
                }

                root.AddChild(menu);
            }
        }

        private static void AddFilterOptions(MenuWidget menu, MapSelectFilterAdapter filter)
        {
            if (menu == null || filter == null)
            {
                return;
            }

            IReadOnlyList<MapSelectFilterAdapter.Option> options = filter.GetOptions();
            for (int i = 0; i < options.Count; i++)
            {
                MapSelectFilterAdapter.Option option = options[i];
                if (option == null)
                {
                    continue;
                }

                string optionId = menu.Id + "-" + option.Index;
                menu.AddItem(new MenuItemWidget(
                    optionId,
                    () => option.Label,
                    () => option.IsChecked ? ModText.Get(ModStrings.UI.StatusChecked) : ModText.Get(ModStrings.UI.StatusUnchecked),
                    () => ToggleFilterOption(option),
                    option.FocusNative,
                    () => option.IsVisible,
                    option.GetTooltip,
                    onUnfocus: null,
                    isEnabled: () => option.IsEnabled));
            }
        }

        private static bool ToggleFilterOption(MapSelectFilterAdapter.Option option)
        {
            if (option == null || !option.IsEnabled)
            {
                return false;
            }

            option.Toggle();
            return true;
        }

        private static void AddMapTable(ContainerWidget root, AdventureLobbyMapSelectAdapter adapter, FocusState focusState)
        {
            if (root == null)
            {
                return;
            }

            TableWidget table = new TableWidget(
                TableId,
                adapter != null ? adapter.MapsLabel : string.Empty,
                BuildColumns(adapter),
                BuildRows(adapter));

            if (focusState != null && focusState.HasTableFocus)
            {
                table.SetFocusedCell(focusState.TableRowIndex, focusState.TableColumnIndex);
            }
            else
            {
                AdventureLobbyMapSelectRowAdapter selected = adapter != null ? adapter.SelectedRow : null;
                if (selected != null)
                {
                    table.SetFocusedRowById(BuildRowId(adapter, selected));
                }

                table.SetFocusedColumn(1);
            }

            root.AddChild(table);
        }

        private static IEnumerable<TableWidget.Column> BuildColumns(AdventureLobbyMapSelectAdapter adapter)
        {
            IReadOnlyList<MapSelectSortButtonAdapter> sortButtons = adapter != null
                ? adapter.GetSortButtons()
                : new MapSelectSortButtonAdapter[0];
            IReadOnlyList<string> labels = adapter != null
                ? adapter.GetColumnLabels()
                : new string[0];

            yield return BuildColumn(labels, sortButtons, "type", 0);
            yield return BuildColumn(labels, sortButtons, "name", 1);
            yield return BuildColumn(labels, sortButtons, "tag", 2);
            yield return BuildColumn(labels, sortButtons, "win-condition", 3);
            yield return BuildColumn(labels, sortButtons, "players", 4);
            yield return BuildColumn(labels, sortButtons, "size", 5);
            yield return BuildColumn(labels, sortButtons, "completed", 6);
        }

        private static TableWidget.Column BuildColumn(
            IReadOnlyList<string> labels,
            IReadOnlyList<MapSelectSortButtonAdapter> sortButtons,
            string id,
            int sortButtonIndex)
        {
            MapSelectSortButtonAdapter sortButton = GetSortButton(sortButtons, sortButtonIndex);
            return new TableWidget.Column(
                id,
                GetColumnLabel(labels, sortButtonIndex, id),
                () => ConvertSortDirection(sortButton != null ? sortButton.Direction : MapSelectSortDirection.None),
                () => sortButton != null && sortButton.Activate());
        }

        private static string GetColumnLabel(IReadOnlyList<string> labels, int index, string fallback)
        {
            if (labels != null && index >= 0 && index < labels.Count && !string.IsNullOrWhiteSpace(labels[index]))
            {
                return labels[index];
            }

            return fallback ?? string.Empty;
        }

        private static IReadOnlyList<TableWidget.Row> BuildRows(AdventureLobbyMapSelectAdapter adapter)
        {
            List<TableWidget.Row> rows = new List<TableWidget.Row>();
            if (adapter == null)
            {
                return rows;
            }

            IReadOnlyList<AdventureLobbyMapSelectRowAdapter> visibleRows = adapter.GetVisibleRows();
            for (int i = 0; i < visibleRows.Count; i++)
            {
                AdventureLobbyMapSelectRowAdapter row = visibleRows[i];
                if (row == null)
                {
                    continue;
                }

                rows.Add(new TableWidget.Row(
                    BuildRowId(adapter, row),
                    row.Name,
                    columnId => GetCellValue(row, columnId),
                    row.GetCellTooltip,
                    row.FocusNative,
                    row.Activate));
            }

            return rows;
        }

        private static string BuildRowId(AdventureLobbyMapSelectAdapter adapter, AdventureLobbyMapSelectRowAdapter row)
        {
            int index = 0;
            IReadOnlyList<AdventureLobbyMapSelectRowAdapter> rows = adapter != null
                ? adapter.GetVisibleRows()
                : new AdventureLobbyMapSelectRowAdapter[0];
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null && row != null && rows[i].NativeKey == row.NativeKey)
                {
                    index = i;
                    break;
                }
            }

            string id = "map-select-row-" + index;
            string nativeKey = row != null ? row.NativeKey : null;
            if (!string.IsNullOrWhiteSpace(nativeKey))
            {
                id = id + "-" + SanitizeId(nativeKey);
            }

            return id;
        }

        private static string GetCellValue(AdventureLobbyMapSelectRowAdapter row, string columnId)
        {
            if (row == null)
            {
                return string.Empty;
            }

            switch (columnId)
            {
                case "type":
                    return row.TypeLabel;
                case "name":
                    return row.Name;
                case "tag":
                    return ModText.JoinList(row.TagLabels);
                case "win-condition":
                    return ModText.JoinList(row.WinConditionLabels);
                case "players":
                    return row.Players > 0 ? row.Players.ToString() : string.Empty;
                case "size":
                    return row.SizeLabel;
                case "completed":
                    return row.IsCompleted ? row.CompletedLabel : row.NotCompletedLabel;
                default:
                    return string.Empty;
            }
        }

        private static MapSelectSortButtonAdapter GetSortButton(IReadOnlyList<MapSelectSortButtonAdapter> buttons, int index)
        {
            if (buttons == null || index < 0 || index >= buttons.Count)
            {
                return null;
            }

            return buttons[index];
        }

        private static TableWidget.SortDirection ConvertSortDirection(MapSelectSortDirection direction)
        {
            if (direction == MapSelectSortDirection.Ascending)
            {
                return TableWidget.SortDirection.Ascending;
            }

            if (direction == MapSelectSortDirection.Descending)
            {
                return TableWidget.SortDirection.Descending;
            }

            return TableWidget.SortDirection.None;
        }

        private static void AddOptionalButton(ContainerWidget root, string id, IMenuButtonAdapter button)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                id,
                button.GetLabel,
                button.Activate,
                () => FocusNativeButton(button.Button),
                button.IsEnabled,
                button.IsVisible));
        }

        private static void AddDetails(ContainerWidget root, AdventureLobbyMapSelectAdapter adapter)
        {
            if (root == null || adapter == null)
            {
                return;
            }

            root.AddChild(new TextWidget(
                "selected-map-details",
                () => BuildSelectedDetails(adapter),
                null,
                includeParentLabelInAnnouncement: false,
                tooltip: null,
                isVisible: () => !string.IsNullOrWhiteSpace(BuildSelectedDetails(adapter))));
        }

        private static string BuildSelectedDetails(AdventureLobbyMapSelectAdapter adapter)
        {
            AdventureLobbyMapSelectRowAdapter row = adapter != null ? adapter.SelectedRow : null;
            if (row == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddIfNotEmpty(parts, row.Name);
            AddIfNotEmpty(parts, row.Description);
            return string.Join(System.Environment.NewLine, parts.ToArray());
        }

        private static void FocusNativeButton(UIButton button)
        {
            if (button == null)
            {
                return;
            }

            NativeSelectionUtility.Select(button);
        }

        public static AdventureLobbyMapSelectAdapter FindActiveMapSelectMenu(MapSelectMenu targetMenu)
        {
            MapSelectMenu[] menus = Resources.FindObjectsOfTypeAll<MapSelectMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapSelectMenu menu = menus[i];
                if (!IsLiveSceneMapSelectMenu(menu))
                {
                    continue;
                }

                if (targetMenu != null && !ReferenceEquals(targetMenu, menu))
                {
                    continue;
                }

                AdventureLobbyMapSelectAdapter adapter = new AdventureLobbyMapSelectAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneMapSelectMenu(MapSelectMenu menu)
        {
            if (menu == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)menu).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] chars = value.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c))
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
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
