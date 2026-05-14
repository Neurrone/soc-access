using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    // Most widgets are generic and can be used in other games too
    // However, this is only going to be applicable to Songs of Conquest, hence the imports of the game types
    internal sealed class ArmyExchangeGridWidget : Widget
    {
        private readonly List<SlotWidget> _wielderSlots = new List<SlotWidget>();
        private readonly List<SlotWidget> _joiningSlots = new List<SlotWidget>();
        private readonly Func<TroopHudAdapter.SlotItem, TroopHudAdapter.SlotItem, TroopHudAdapter.DropResult> _drop;
        private readonly Action _onCompletedDrop;
        private int _focusedColumn;
        private int _focusedRow;
        private SlotWidget _dragSource;

        public ArmyExchangeGridWidget(
            string id,
            string wielderArmyLabel,
            string joiningArmyLabel,
            IEnumerable<TroopHudAdapter.SlotItem> wielderSlots,
            IEnumerable<TroopHudAdapter.SlotItem> joiningSlots,
            Func<TroopHudAdapter.SlotItem, TroopHudAdapter.SlotItem, TroopHudAdapter.DropResult> drop,
            Action onCompletedDrop = null)
            : base(id)
        {
            WielderArmyLabel = string.IsNullOrWhiteSpace(wielderArmyLabel) ? "wielder's army" : wielderArmyLabel;
            JoiningArmyLabel = string.IsNullOrWhiteSpace(joiningArmyLabel) ? "joining army" : joiningArmyLabel;
            _drop = drop;
            _onCompletedDrop = onCompletedDrop;
            AddSlots(_wielderSlots, wielderSlots, WielderArmyLabel);
            AddSlots(_joiningSlots, joiningSlots, JoiningArmyLabel);
        }

        public string WielderArmyLabel { get; private set; }

        public string JoiningArmyLabel { get; private set; }

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
            return "Army exchange grid";
        }

        public override string GetRole()
        {
            return "grid";
        }

        public override Widget GetFocusedWidget()
        {
            SlotWidget focusedSlot = FocusedSlot;
            return focusedSlot != null ? (Widget)focusedSlot : this;
        }

        public bool SetFocusedCell(int columnIndex, int rowIndex)
        {
            _focusedColumn = Clamp(columnIndex, 0, 1);
            List<SlotWidget> column = GetColumn(_focusedColumn);
            if (column.Count == 0)
            {
                ClampFocus();
                return FocusedSlot != null;
            }

            _focusedRow = Clamp(rowIndex, 0, column.Count - 1);
            return FocusedSlot != null;
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
            EnsureFocus();
        }

        protected override void OnUnfocus()
        {
            ClearDrag();
        }

        public override bool EnsureFocus()
        {
            if (FocusedSlot == null)
            {
                _focusedColumn = 0;
                _focusedRow = 0;
                ClampFocus();
            }

            return FocusedSlot != null;
        }

        private SlotWidget FocusedSlot
        {
            get
            {
                List<SlotWidget> column = GetColumn(_focusedColumn);
                if (_focusedRow < 0 || _focusedRow >= column.Count)
                {
                    return null;
                }

                return column[_focusedRow];
            }
        }

        private void AddSlots(List<SlotWidget> target, IEnumerable<TroopHudAdapter.SlotItem> slots, string armyLabel)
        {
            if (slots == null)
            {
                return;
            }

            int index = 0;
            foreach (TroopHudAdapter.SlotItem slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                SlotWidget widget = new SlotWidget(this, BuildSlotId(target, slot, index), slot, armyLabel);
                widget.Parent = this;
                target.Add(widget);
                index++;
            }
        }

        private bool MoveVertical(int delta)
        {
            List<SlotWidget> column = GetColumn(_focusedColumn);
            if (column.Count == 0)
            {
                return false;
            }

            int nextRow = Clamp(_focusedRow + delta, 0, column.Count - 1);
            return SetFocus(_focusedColumn, nextRow);
        }

        private bool MoveHorizontal(int delta)
        {
            int nextColumn = Clamp(_focusedColumn + delta, 0, 1);
            List<SlotWidget> column = GetColumn(nextColumn);
            if (column.Count == 0)
            {
                return false;
            }

            int nextRow = Clamp(_focusedRow, 0, column.Count - 1);
            return SetFocus(nextColumn, nextRow);
        }

        private bool MoveToRowEdge(bool first)
        {
            List<SlotWidget> column = GetColumn(_focusedColumn);
            if (column.Count == 0)
            {
                return false;
            }

            return SetFocus(_focusedColumn, first ? 0 : column.Count - 1);
        }

        private bool SetFocus(int column, int row)
        {
            _focusedColumn = column;
            _focusedRow = row;
            ClampFocus();
            SlotWidget next = FocusedSlot;
            if (next == null)
            {
                return false;
            }

            UIManager.RequestFocus(next);
            return true;
        }

        private void ClampFocus()
        {
            _focusedColumn = Clamp(_focusedColumn, 0, 1);
            List<SlotWidget> column = GetColumn(_focusedColumn);
            if (column.Count == 0)
            {
                _focusedColumn = _focusedColumn == 0 ? 1 : 0;
                column = GetColumn(_focusedColumn);
            }

            _focusedRow = column.Count == 0 ? -1 : Clamp(_focusedRow, 0, column.Count - 1);
        }

        private bool StartDrag()
        {
            SlotWidget slot = FocusedSlot;
            if (slot == null || !slot.IsOccupied)
            {
                return false;
            }

            _dragSource = slot;
            Speak("Started drag. Move to destination and press enter to drop.");
            return true;
        }

        private bool CompleteDrag()
        {
            SlotWidget target = FocusedSlot;
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

            SlotWidget source = _dragSource;
            if (_drop != null)
            {
                TroopHudAdapter.DropResult result = _drop(source.Slot, target.Slot);
                if (result == TroopHudAdapter.DropResult.InvalidDestination)
                {
                    Speak("Cannot drop there.");
                    return true;
                }

                if (result == TroopHudAdapter.DropResult.Completed)
                {
                    _dragSource = null;
                    _onCompletedDrop?.Invoke();
                }
                else if (result == TroopHudAdapter.DropResult.MoveAmountPopupOpened)
                {
                    _dragSource = null;
                }

                return true;
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

        private List<SlotWidget> GetColumn(int column)
        {
            return column == 0 ? _wielderSlots : _joiningSlots;
        }

        private string BuildSlotId(List<SlotWidget> target, TroopHudAdapter.SlotItem slot, int index)
        {
            string side = ReferenceEquals(target, _wielderSlots) ? "left" : "right";
            int slotNumber = slot != null && slot.SlotNumber > 0 ? slot.SlotNumber : index + 1;
            return Id + "-" + side + "-slot-" + slotNumber;
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

        internal sealed class SlotWidget : Widget
        {
            private readonly ArmyExchangeGridWidget _grid;
            private readonly TroopHudAdapter.SlotItem _slot;
            private readonly string _armyLabel;

            public SlotWidget(ArmyExchangeGridWidget grid, string id, TroopHudAdapter.SlotItem slot, string armyLabel)
                : base(id)
            {
                _grid = grid;
                _slot = slot;
                _armyLabel = armyLabel ?? string.Empty;
            }

            public TroopHudAdapter.SlotItem Slot
            {
                get { return _slot; }
            }

            public string ArmyLabel
            {
                get { return _armyLabel; }
            }

            public int SlotNumber
            {
                get { return _slot != null ? _slot.SlotNumber : 0; }
            }

            public bool IsOccupied
            {
                get { return _slot != null && _slot.IsOccupied; }
            }

            public override string GetFocusMessage()
            {
                return base.GetFocusMessage();
            }

            public override string GetLabel()
            {
                if (_slot == null)
                {
                    return string.Empty;
                }

                string slotLabel = _armyLabel + " slot " + _slot.SlotNumber;
                if (!_slot.IsOccupied)
                {
                    return "Empty, " + slotLabel;
                }

                return _slot.TroopName + ", " + _slot.CurrentSize + " / " + _slot.MaxSize + ", " + slotLabel;
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
                return _slot != null && _slot.IsOccupied ? _slot.Tooltip : null;
            }

            protected override void OnFocus()
            {
                _slot?.Focus();
            }
        }
    }
}
