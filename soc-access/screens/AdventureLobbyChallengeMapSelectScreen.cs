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
    internal sealed class AdventureLobbyChallengeMapSelectScreen : Screen
    {
        private const string TableId = "challenge-map-select-table";

        private readonly AdventureLobbyChallengeMapSelectAdapter _adapter;

        public AdventureLobbyChallengeMapSelectScreen(AdventureLobbyChallengeMapSelectAdapter adapter)
            : base(BuildRootWidget(adapter, null))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyChallengeMapSelectAdapter adapter = FindActiveChallengeMapSelectMenu(null);
            return adapter != null ? new AdventureLobbyChallengeMapSelectScreen(adapter) : null;
        }

        public bool Matches(ChallengeMapsMenu menu)
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
            return new FocusState(
                focusedChild != null ? focusedChild.Id : null,
                table != null,
                table != null ? table.FocusedRowIndex : 0,
                table != null ? table.FocusedColumnIndex : 0);
        }

        private static ContainerWidget BuildRootWidget(AdventureLobbyChallengeMapSelectAdapter adapter, FocusState focusState)
        {
            ContainerWidget root = new ContainerWidget(
                "adventure-lobby-challenge-map-select-screen",
                adapter != null ? adapter.Title : string.Empty);

            AddMapTable(root, adapter, focusState);
            AddDetails(root, adapter);
            AddOptionalButton(root, "confirm", adapter != null ? adapter.ConfirmButton : null);
            AddOptionalButton(root, "options", adapter != null ? adapter.OptionsButton : null);
            AddOptionalButton(root, "back", adapter != null ? adapter.BackButton : null);

            if (focusState != null && !string.IsNullOrWhiteSpace(focusState.RootChildId))
            {
                root.SetFocusedChildById(focusState.RootChildId);
            }

            return root;
        }

        private static void AddMapTable(ContainerWidget root, AdventureLobbyChallengeMapSelectAdapter adapter, FocusState focusState)
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
                AdventureLobbyChallengeMapRowAdapter selected = adapter != null ? adapter.SelectedRow : null;
                if (selected != null)
                {
                    table.SetFocusedRowById(BuildRowId(adapter, selected));
                }

                table.SetFocusedColumn(0);
            }

            root.AddChild(table);
        }

        private static IEnumerable<TableWidget.Column> BuildColumns(AdventureLobbyChallengeMapSelectAdapter adapter)
        {
            yield return new TableWidget.Column(
                "name",
                adapter != null ? adapter.NameColumnLabel : string.Empty,
                () => TableWidget.SortDirection.None,
                null);
            yield return new TableWidget.Column(
                "win-condition",
                adapter != null ? adapter.WinConditionColumnLabel : string.Empty,
                () => TableWidget.SortDirection.None,
                null);
            yield return new TableWidget.Column(
                "completed",
                adapter != null ? adapter.CompletedColumnLabel : string.Empty,
                () => TableWidget.SortDirection.None,
                null);
        }

        private static IReadOnlyList<TableWidget.Row> BuildRows(AdventureLobbyChallengeMapSelectAdapter adapter)
        {
            List<TableWidget.Row> rows = new List<TableWidget.Row>();
            if (adapter == null)
            {
                return rows;
            }

            IReadOnlyList<AdventureLobbyChallengeMapRowAdapter> visibleRows = adapter.GetVisibleRows();
            for (int i = 0; i < visibleRows.Count; i++)
            {
                AdventureLobbyChallengeMapRowAdapter row = visibleRows[i];
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
                    row.Select));
            }

            return rows;
        }

        private static string GetCellValue(AdventureLobbyChallengeMapRowAdapter row, string columnId)
        {
            if (row == null)
            {
                return string.Empty;
            }

            switch (columnId)
            {
                case "name":
                    return row.Name;
                case "win-condition":
                    return ModText.JoinList(row.WinConditionLabels);
                case "completed":
                    return row.IsCompleted ? row.CompletedLabel : row.NotCompletedLabel;
                default:
                    return string.Empty;
            }
        }

        private static void AddDetails(ContainerWidget root, AdventureLobbyChallengeMapSelectAdapter adapter)
        {
            if (root == null || adapter == null)
            {
                return;
            }

            root.AddChild(new TextWidget(
                "selected-challenge-map-details",
                () => BuildSelectedDetails(adapter),
                null,
                includeParentLabelInAnnouncement: false,
                tooltip: null,
                isVisible: () => !string.IsNullOrWhiteSpace(BuildSelectedDetails(adapter))));
        }

        private static string BuildSelectedDetails(AdventureLobbyChallengeMapSelectAdapter adapter)
        {
            AdventureLobbyChallengeMapRowAdapter row = adapter != null ? adapter.SelectedRow : null;
            if (row == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddIfNotEmpty(parts, row.Name);
            AddIfNotEmpty(parts, row.Description);
            return string.Join(System.Environment.NewLine, parts.ToArray());
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

        private static void FocusNativeButton(UIButton button)
        {
            if (button == null)
            {
                return;
            }

            NativeSelectionUtility.Select(button);
        }

        private static string BuildRowId(AdventureLobbyChallengeMapSelectAdapter adapter, AdventureLobbyChallengeMapRowAdapter row)
        {
            int index = 0;
            IReadOnlyList<AdventureLobbyChallengeMapRowAdapter> rows = adapter != null
                ? adapter.GetVisibleRows()
                : new AdventureLobbyChallengeMapRowAdapter[0];
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null && row != null && rows[i].NativeKey == row.NativeKey)
                {
                    index = i;
                    break;
                }
            }

            string id = "challenge-map-row-" + index;
            string nativeKey = row != null ? row.NativeKey : null;
            if (!string.IsNullOrWhiteSpace(nativeKey))
            {
                id = id + "-" + SanitizeId(nativeKey);
            }

            return id;
        }

        internal static AdventureLobbyChallengeMapSelectAdapter FindActiveChallengeMapSelectMenu(ChallengeMapsMenu targetMenu)
        {
            ChallengeMapsMenu[] menus = Resources.FindObjectsOfTypeAll<ChallengeMapsMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                ChallengeMapsMenu menu = menus[i];
                if (!IsLiveSceneChallengeMapSelectMenu(menu))
                {
                    continue;
                }

                if (targetMenu != null && !ReferenceEquals(targetMenu, menu))
                {
                    continue;
                }

                AdventureLobbyChallengeMapSelectAdapter adapter = new AdventureLobbyChallengeMapSelectAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneChallengeMapSelectMenu(ChallengeMapsMenu menu)
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
            public FocusState(string rootChildId, bool hasTableFocus, int tableRowIndex, int tableColumnIndex)
            {
                RootChildId = rootChildId;
                HasTableFocus = hasTableFocus;
                TableRowIndex = tableRowIndex;
                TableColumnIndex = tableColumnIndex;
            }

            public string RootChildId { get; private set; }

            public bool HasTableFocus { get; private set; }

            public int TableRowIndex { get; private set; }

            public int TableColumnIndex { get; private set; }
        }
    }
}
