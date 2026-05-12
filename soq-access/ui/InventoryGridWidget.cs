using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal sealed class InventoryGridWidget : Widget
    {
        private readonly List<Column> _columns = new List<Column>();
        private readonly Func<InventorySlotInfo, InventorySlotInfo, DropResult> _drop;
        private int _focusedColumn;
        private int _focusedRow;
        private CellWidget _dragSource;

        public InventoryGridWidget(string id, IEnumerable<Column> columns, Func<InventorySlotInfo, InventorySlotInfo, DropResult> drop = null)
            : base(id)
        {
            _drop = drop;
            if (columns == null)
            {
                return;
            }

            foreach (Column column in columns)
            {
                if (column == null)
                {
                    continue;
                }

                _columns.Add(column);
                for (int i = 0; i < column.Cells.Count; i++)
                {
                    Cell cell = column.Cells[i];
                    if (cell != null)
                    {
                        cell.Widget = new CellWidget(this, cell);
                        cell.Widget.Parent = this;
                    }
                }
            }
        }

        public int FocusedColumnIndex
        {
            get { return _focusedColumn; }
        }

        public int FocusedRowIndex
        {
            get { return _focusedRow; }
        }

        public override bool AnnounceName
        {
            get { return true; }
        }

        public override string GetLabel()
        {
            return "Inventory grid";
        }

        public override string GetRole()
        {
            return "grid";
        }

        public override Widget GetFocusedWidget()
        {
            CellWidget cell = FocusedCell;
            return cell != null ? (Widget)cell : this;
        }

        public bool SetFocusedCell(int columnIndex, int rowIndex)
        {
            if (_columns.Count == 0)
            {
                return false;
            }

            _focusedColumn = Clamp(columnIndex, 0, _columns.Count - 1);
            _focusedRow = Clamp(rowIndex, 0, Math.Max(0, _columns[_focusedColumn].Cells.Count - 1));
            ClampFocus();
            return FocusedCell != null;
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.PreviousRow.Key
                || actionKey == AccessibilityActions.NextRow.Key
                || actionKey == AccessibilityActions.PreviousColumn.Key
                || actionKey == AccessibilityActions.NextColumn.Key
                || actionKey == AccessibilityActions.FirstRow.Key
                || actionKey == AccessibilityActions.LastRow.Key
                || actionKey == AccessibilityActions.StartDrag.Key
                || actionKey == AccessibilityActions.Activate.Key
                || (_dragSource != null && actionKey == AccessibilityActions.Cancel.Key);
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
                return MoveToRowEdge(first: true);
            }

            if (action.Key == AccessibilityActions.LastRow.Key)
            {
                return MoveToRowEdge(first: false);
            }

            if (action.Key == AccessibilityActions.StartDrag.Key)
            {
                return StartDrag();
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                return CompleteDrag();
            }

            if (action.Key == AccessibilityActions.Cancel.Key && _dragSource != null)
            {
                return CancelDrag();
            }

            return false;
        }

        protected override void OnFocus()
        {
            if (FocusedCell == null)
            {
                _focusedColumn = 0;
                _focusedRow = 0;
                ClampFocus();
            }

            FocusedCell?.Focus();
            UIManager.SetFocusedWidget(GetFocusedWidget());
        }

        protected override void OnUnfocus()
        {
            ClearDrag();
            FocusedCell?.Unfocus();
        }

        private CellWidget FocusedCell
        {
            get
            {
                if (_focusedColumn < 0 || _focusedColumn >= _columns.Count)
                {
                    return null;
                }

                Column column = _columns[_focusedColumn];
                if (_focusedRow < 0 || _focusedRow >= column.Cells.Count)
                {
                    return null;
                }

                Cell cell = column.Cells[_focusedRow];
                return cell != null ? cell.Widget : null;
            }
        }

        private bool MoveVertical(int delta)
        {
            if (_focusedColumn < 0 || _focusedColumn >= _columns.Count)
            {
                return false;
            }

            Column column = _columns[_focusedColumn];
            if (column.Cells.Count == 0)
            {
                return false;
            }

            return SetFocus(_focusedColumn, Clamp(_focusedRow + delta, 0, column.Cells.Count - 1));
        }

        private bool MoveHorizontal(int delta)
        {
            if (_columns.Count == 0)
            {
                return false;
            }

            int nextColumn = Clamp(_focusedColumn + delta, 0, _columns.Count - 1);
            Column column = _columns[nextColumn];
            if (column.Cells.Count == 0)
            {
                return false;
            }

            return SetFocus(nextColumn, Clamp(_focusedRow, 0, column.Cells.Count - 1));
        }

        private bool MoveToRowEdge(bool first)
        {
            if (_focusedColumn < 0 || _focusedColumn >= _columns.Count)
            {
                return false;
            }

            Column column = _columns[_focusedColumn];
            if (column.Cells.Count == 0)
            {
                return false;
            }

            return SetFocus(_focusedColumn, first ? 0 : column.Cells.Count - 1);
        }

        private bool SetFocus(int column, int row)
        {
            CellWidget previous = FocusedCell;
            _focusedColumn = column;
            _focusedRow = row;
            ClampFocus();
            CellWidget next = FocusedCell;
            if (next == null)
            {
                return false;
            }

            if (previous != null && !ReferenceEquals(previous, next))
            {
                previous.Unfocus();
            }

            next.Focus();
            UIManager.SetFocusedWidget(next);
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
            Column column = _columns[_focusedColumn];
            if (column.Cells.Count == 0)
            {
                _focusedRow = -1;
                return;
            }

            _focusedRow = Clamp(_focusedRow, 0, column.Cells.Count - 1);
        }

        private bool StartDrag()
        {
            CellWidget cell = FocusedCell;
            if (cell == null || !cell.IsOccupied)
            {
                return false;
            }

            _dragSource = cell;
            Speak("Started drag. Move to destination and press enter to drop.");
            return true;
        }

        private bool CompleteDrag()
        {
            CellWidget target = FocusedCell;
            if (_dragSource == null)
            {
                Speak("Press space to drag.");
                return true;
            }

            if (target == null || ReferenceEquals(target, _dragSource))
            {
                Speak("Invalid destination.");
                return true;
            }

            CellWidget source = _dragSource;
            DropResult result = source.DropTo(target);
            if (result == DropResult.Dropped || result == DropResult.DeniedWithFeedback)
            {
                _dragSource = null;
            }
            else
            {
                Speak("Cannot drop there.");
            }

            return true;
        }

        private bool CancelDrag()
        {
            if (_dragSource == null)
            {
                return false;
            }

            ClearDrag();
            Speak("Drag cancelled.");
            return true;
        }

        private void ClearDrag()
        {
            _dragSource = null;
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
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
        }

        internal sealed class Column
        {
            public Column(string id, string label, IEnumerable<Cell> cells)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Cells = new List<Cell>();
                if (cells == null)
                {
                    return;
                }

                foreach (Cell cell in cells)
                {
                    if (cell != null)
                    {
                        Cells.Add(cell);
                    }
                }
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public List<Cell> Cells { get; private set; }
        }

        internal sealed class Cell
        {
            public Cell(
                string id,
                string label,
                InventorySlotInfo slot)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Slot = slot;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public InventorySlotInfo Slot { get; private set; }
            public CellWidget Widget { get; set; }
        }

        internal sealed class CellWidget : Widget
        {
            private readonly InventoryGridWidget _grid;
            private readonly Cell _cell;

            public CellWidget(InventoryGridWidget grid, Cell cell)
                : base(cell != null ? cell.Id : string.Empty)
            {
                _grid = grid;
                _cell = cell;
            }

            public bool IsOccupied
            {
                get { return _cell != null && _cell.Slot != null && !_cell.Slot.IsEmpty; }
            }

            public override string GetLabel()
            {
                return _cell != null ? _cell.Label : string.Empty;
            }

            public override string GetStatus()
            {
                if (!IsOccupied || _grid == null)
                {
                    return string.Empty;
                }

                if (_grid._dragSource != null)
                {
                    return ReferenceEquals(_grid._dragSource, this) ? "dragging" : string.Empty;
                }

                return "draggable";
            }

            public override bool ClaimsAction(string actionKey)
            {
                return _grid != null && _grid.ClaimsAction(actionKey);
            }

            public override bool HandleAction(InputAction action)
            {
                return _grid != null && _grid.HandleAction(action);
            }

            public override Tooltip GetTooltip()
            {
                return _cell != null && _cell.Slot != null ? _cell.Slot.Tooltip : null;
            }

            protected override void OnFocus()
            {
                if (_cell != null && _cell.Slot != null)
                {
                    _cell.Slot.FocusNative();
                }
            }

            public DropResult DropTo(CellWidget target)
            {
                if (_cell == null || _cell.Slot == null || target == null || target._cell == null || target._cell.Slot == null)
                {
                    return DropResult.Invalid;
                }

                if (_cell.Slot.Movable == null || target._cell.Slot.NativeSlot == null)
                {
                    return DropResult.Invalid;
                }

                if (_grid != null && _grid._drop != null)
                {
                    return _grid._drop(_cell.Slot, target._cell.Slot);
                }

                return DropResult.Invalid;
            }
        }
    }
}
