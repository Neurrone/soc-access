using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal sealed class TableWidget : Widget
    {
        public enum SortDirection
        {
            None,
            Ascending,
            Descending
        }

        private readonly List<Column> _columns = new List<Column>();
        private readonly List<Row> _rows = new List<Row>();
        private int _focusedRow;
        private int _focusedColumn;
        private int _lastAnnouncedRow = -1;
        private int _lastAnnouncedColumn = -1;
        private int _currentAnnouncementRow = -1;
        private int _currentAnnouncementColumn = -1;
        private string _currentAnnouncement = string.Empty;

        public TableWidget(string id, string label, IEnumerable<Column> columns, IEnumerable<Row> rows)
            : base(id)
        {
            Label = label ?? string.Empty;
            AddColumns(columns);
            AddRows(rows);
        }

        public string Label { get; private set; }

        public int FocusedRowIndex
        {
            get { return _focusedRow; }
        }

        public int FocusedColumnIndex
        {
            get { return _focusedColumn; }
        }

        public override bool AnnounceName
        {
            get { return true; }
        }

        public override string GetLabel()
        {
            return Label;
        }

        public override string GetRole()
        {
            return ModText.Get(ModStrings.UI.RoleGrid);
        }

        public override Widget GetFocusedWidget()
        {
            Widget focused = GetFocusedCell();
            return focused ?? this;
        }

        public bool SetFocusedRowById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row != null && row.Id == id)
                {
                    _focusedRow = i + 1;
                    ClampFocus();
                    return true;
                }
            }

            return false;
        }

        public void SetFocusedColumn(int columnIndex)
        {
            _focusedColumn = columnIndex;
            ClampFocus();
        }

        public void SetFocusedCell(int rowIndex, int columnIndex)
        {
            _focusedRow = rowIndex;
            _focusedColumn = columnIndex;
            ClampFocus();
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.PreviousRow.Key
                || actionKey == AccessibilityActions.NextRow.Key
                || actionKey == AccessibilityActions.PreviousColumn.Key
                || actionKey == AccessibilityActions.NextColumn.Key
                || actionKey == AccessibilityActions.FirstRow.Key
                || actionKey == AccessibilityActions.LastRow.Key
                || actionKey == AccessibilityActions.Activate.Key;
        }

        public override bool HasClaimInTree(string actionKey)
        {
            return ClaimsAction(actionKey);
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (action.Key == AccessibilityActions.PreviousRow.Key)
            {
                return MoveVertical(-1);
            }

            if (action.Key == AccessibilityActions.NextRow.Key)
            {
                return MoveVertical(1);
            }

            if (action.Key == AccessibilityActions.PreviousColumn.Key)
            {
                return MoveHorizontal(-1);
            }

            if (action.Key == AccessibilityActions.NextColumn.Key)
            {
                return MoveHorizontal(1);
            }

            if (action.Key == AccessibilityActions.FirstRow.Key)
            {
                return SetFocus(0, _focusedColumn);
            }

            if (action.Key == AccessibilityActions.LastRow.Key)
            {
                return SetFocus(_rows.Count, _focusedColumn);
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                return ActivateFocusedCell();
            }

            return false;
        }

        protected override void OnFocus()
        {
            EnsureFocus();
        }

        public override bool EnsureFocus()
        {
            ClampFocus();
            return GetFocusedCell() != null;
        }

        private void AddColumns(IEnumerable<Column> columns)
        {
            if (columns == null)
            {
                return;
            }

            foreach (Column column in columns)
            {
                if (column != null)
                {
                    _columns.Add(column);
                }
            }
        }

        private void AddRows(IEnumerable<Row> rows)
        {
            if (rows == null)
            {
                return;
            }

            foreach (Row row in rows)
            {
                if (row != null)
                {
                    _rows.Add(row);
                }
            }
        }

        private bool MoveVertical(int delta)
        {
            int nextRow = Clamp(_focusedRow + delta, 0, _rows.Count);
            if (nextRow == _focusedRow)
            {
                return false;
            }

            return SetFocus(nextRow, _focusedColumn);
        }

        private bool MoveHorizontal(int delta)
        {
            int nextColumn = Clamp(_focusedColumn + delta, 0, _columns.Count - 1);
            if (nextColumn == _focusedColumn)
            {
                return false;
            }

            return SetFocus(_focusedRow, nextColumn);
        }

        private bool SetFocus(int row, int column)
        {
            if (_columns.Count == 0)
            {
                return false;
            }

            _focusedRow = Clamp(row, 0, _rows.Count);
            _focusedColumn = Clamp(column, 0, _columns.Count - 1);
            CellWidget focused = GetFocusedCell();
            if (focused == null)
            {
                return false;
            }

            UIManager.RequestFocus(focused);
            return true;
        }

        private void ClampFocus()
        {
            if (_columns.Count == 0)
            {
                _focusedColumn = -1;
                _focusedRow = -1;
                return;
            }

            _focusedColumn = Clamp(_focusedColumn, 0, _columns.Count - 1);
            _focusedRow = Clamp(_focusedRow, 0, _rows.Count);
        }

        private bool ActivateFocusedCell()
        {
            if (_focusedColumn < 0 || _focusedColumn >= _columns.Count)
            {
                return false;
            }

            if (_focusedRow == 0)
            {
                Column column = _columns[_focusedColumn];
                if (column == null || column.ActivateSort == null || !column.ActivateSort())
                {
                    return false;
                }

                Speak(GetHeaderAnnouncement(column));
                return true;
            }

            Row row = GetFocusedRow();
            return row != null && row.Activate != null && row.Activate();
        }

        private CellWidget GetFocusedCell()
        {
            if (_focusedColumn < 0 || _focusedColumn >= _columns.Count)
            {
                return null;
            }

            CellWidget cell = new CellWidget(this, _focusedRow, _focusedColumn);
            cell.Parent = this;
            return cell;
        }

        private Row GetFocusedRow()
        {
            return GetRow(_focusedRow);
        }

        private Row GetRow(int visualRowIndex)
        {
            int rowIndex = visualRowIndex - 1;
            if (rowIndex < 0 || rowIndex >= _rows.Count)
            {
                return null;
            }

            return _rows[rowIndex];
        }

        private Column GetColumn(int index)
        {
            return index >= 0 && index < _columns.Count ? _columns[index] : null;
        }

        private string GetCellAnnouncement(int rowIndex, int columnIndex)
        {
            if (_currentAnnouncementRow == rowIndex
                && _currentAnnouncementColumn == columnIndex
                && !string.IsNullOrWhiteSpace(_currentAnnouncement))
            {
                return _currentAnnouncement;
            }

            return BuildCellAnnouncement(rowIndex, columnIndex, forceFullContext: true);
        }

        private void SetCurrentCellAnnouncement(int rowIndex, int columnIndex)
        {
            _currentAnnouncement = BuildCellAnnouncement(rowIndex, columnIndex, forceFullContext: false);
            _currentAnnouncementRow = rowIndex;
            _currentAnnouncementColumn = columnIndex;
            _lastAnnouncedRow = rowIndex;
            _lastAnnouncedColumn = columnIndex;
        }

        private string BuildCellAnnouncement(int rowIndex, int columnIndex, bool forceFullContext)
        {
            Column column = GetColumn(columnIndex);
            if (column == null)
            {
                return string.Empty;
            }

            if (rowIndex == 0)
            {
                return GetHeaderAnnouncement(column);
            }

            Row row = GetRow(rowIndex);
            if (row == null)
            {
                return string.Empty;
            }

            bool includeRow = forceFullContext || rowIndex != _lastAnnouncedRow;
            bool includeColumn = forceFullContext || columnIndex != _lastAnnouncedColumn;
            string value = row.GetCellValue != null ? row.GetCellValue(column.Id) : string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                value = ModText.Get(ModStrings.UI.Blank);
            }

            return JoinParts(includeRow ? row.Label : string.Empty, includeColumn ? column.Label : string.Empty, value);
        }

        private static string GetHeaderAnnouncement(Column column)
        {
            if (column == null)
            {
                return string.Empty;
            }

            string direction = GetSortDirectionLabel(column.GetSortDirection != null
                ? column.GetSortDirection()
                : SortDirection.None);

            return JoinParts(column.Label, ModText.Get(ModStrings.UI.RoleColumnHeader), direction);
        }

        private static string GetSortDirectionLabel(SortDirection direction)
        {
            if (direction == SortDirection.Ascending)
            {
                return ModText.Get(ModStrings.UI.SortAscending);
            }

            if (direction == SortDirection.Descending)
            {
                return ModText.Get(ModStrings.UI.SortDescending);
            }

            return string.Empty;
        }

        private static string JoinParts(params string[] parts)
        {
            List<string> result = new List<string>();
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(parts[i]) && !ContainsEquivalent(result, parts[i]))
                    {
                        result.Add(parts[i]);
                    }
                }
            }

            return ModText.JoinListWithCommas(result);
        }

        private static bool ContainsEquivalent(List<string> parts, string value)
        {
            if (parts == null || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = value.Trim();
            for (int i = 0; i < parts.Count; i++)
            {
                if (string.Equals(parts[i]?.Trim(), normalizedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static void Speak(string text)
        {
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: true));
        }

        internal sealed class Column
        {
            public Column(string id, string label, Func<SortDirection> getSortDirection, Func<bool> activateSort)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                GetSortDirection = getSortDirection;
                ActivateSort = activateSort;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public Func<SortDirection> GetSortDirection { get; private set; }
            public Func<bool> ActivateSort { get; private set; }
        }

        internal sealed class Row
        {
            public Row(
                string id,
                string label,
                Func<string, string> getCellValue,
                Func<string, Tooltip> getCellTooltip,
                Action focusNative,
                Func<bool> activate)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                GetCellValue = getCellValue;
                GetCellTooltip = getCellTooltip;
                FocusNative = focusNative;
                Activate = activate;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public Func<string, string> GetCellValue { get; private set; }
            public Func<string, Tooltip> GetCellTooltip { get; private set; }
            public Action FocusNative { get; private set; }
            public Func<bool> Activate { get; private set; }
        }

        private sealed class CellWidget : Widget
        {
            private readonly TableWidget _table;
            private readonly int _rowIndex;
            private readonly int _columnIndex;

            public CellWidget(TableWidget table, int rowIndex, int columnIndex)
                : base(BuildId(table, rowIndex, columnIndex))
            {
                _table = table;
                _rowIndex = rowIndex;
                _columnIndex = columnIndex;
            }

            public override string GetLabel()
            {
                return _table != null ? _table.GetCellAnnouncement(_rowIndex, _columnIndex) : string.Empty;
            }

            public override Tooltip GetTooltip()
            {
                if (_table == null || _rowIndex == 0)
                {
                    return null;
                }

                Row row = _table.GetRow(_rowIndex);
                Column column = _table.GetColumn(_columnIndex);
                return row != null && column != null && row.GetCellTooltip != null
                    ? row.GetCellTooltip(column.Id)
                    : null;
            }

            public override bool ClaimsAction(string actionKey)
            {
                return _table != null && _table.ClaimsAction(actionKey);
            }

            public override bool HandleAction(InputAction action)
            {
                return _table != null && _table.HandleAction(action);
            }

            protected override void OnFocus()
            {
                if (_table == null || _rowIndex == 0)
                {
                    _table?.SetCurrentCellAnnouncement(_rowIndex, _columnIndex);
                    return;
                }

                _table.SetCurrentCellAnnouncement(_rowIndex, _columnIndex);
                Row row = _table.GetRow(_rowIndex);
                if (row != null)
                {
                    row.FocusNative?.Invoke();
                }
            }

            private static string BuildId(TableWidget table, int rowIndex, int columnIndex)
            {
                if (table == null)
                {
                    return string.Empty;
                }

                Column column = table.GetColumn(columnIndex);
                string columnId = column != null ? column.Id : columnIndex.ToString();
                if (rowIndex == 0)
                {
                    return table.Id + "-header-" + columnId;
                }

                Row row = table.GetRow(rowIndex);
                string rowId = row != null ? row.Id : rowIndex.ToString();
                return table.Id + "-" + rowId + "-" + columnId;
            }
        }
    }
}
