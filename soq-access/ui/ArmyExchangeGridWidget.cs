using System;
using System.Collections.Generic;
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
        private readonly Func<SlotWidget, SlotWidget, bool> _drop;
        private int _focusedColumn;
        private int _focusedRow;
        private SlotWidget _dragSource;

        public ArmyExchangeGridWidget(
            string id,
            string wielderArmyLabel,
            IEnumerable<SlotData> wielderSlots,
            IEnumerable<SlotData> joiningSlots,
            Func<SlotWidget, SlotWidget, bool> drop)
            : base(id)
        {
            WielderArmyLabel = string.IsNullOrWhiteSpace(wielderArmyLabel) ? "wielder's army" : wielderArmyLabel;
            JoiningArmyLabel = "joining army";
            _drop = drop;
            AddSlots(_wielderSlots, wielderSlots);
            AddSlots(_joiningSlots, joiningSlots);
        }

        public string WielderArmyLabel { get; private set; }

        public string JoiningArmyLabel { get; private set; }

        public string FocusedSlotId
        {
            get
            {
                SlotWidget slot = FocusedSlot;
                return slot != null ? slot.Id : string.Empty;
            }
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

        public bool SetFocusedSlotById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (SetFocusedSlotById(_wielderSlots, 0, id))
            {
                return true;
            }

            return SetFocusedSlotById(_joiningSlots, 1, id);
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.PreviousArmySlot.Key
                || actionKey == AccessibilityActions.NextArmySlot.Key
                || actionKey == AccessibilityActions.PreviousArmy.Key
                || actionKey == AccessibilityActions.NextArmy.Key
                || actionKey == AccessibilityActions.SelectArmyStack.Key
                || actionKey == AccessibilityActions.Activate.Key
                || actionKey == AccessibilityActions.Cancel.Key;
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

            if (action.Key == AccessibilityActions.PreviousArmySlot.Key)
            {
                return MoveVertical(-1);
            }

            if (action.Key == AccessibilityActions.NextArmySlot.Key)
            {
                return MoveVertical(1);
            }

            if (action.Key == AccessibilityActions.PreviousArmy.Key)
            {
                return MoveHorizontal(-1);
            }

            if (action.Key == AccessibilityActions.NextArmy.Key)
            {
                return MoveHorizontal(1);
            }

            if (action.Key == AccessibilityActions.SelectArmyStack.Key)
            {
                return StartDrag();
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                return CompleteDrag();
            }

            if (action.Key == AccessibilityActions.Cancel.Key)
            {
                return CancelDrag();
            }

            return false;
        }

        protected override void OnFocus()
        {
            if (FocusedSlot == null)
            {
                _focusedColumn = 0;
                _focusedRow = 0;
                ClampFocus();
            }

            FocusedSlot?.Focus();
            UIManager.SetFocusedWidget(GetFocusedWidget());
        }

        protected override void OnUnfocus()
        {
            FocusedSlot?.Unfocus();
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

        private void AddSlots(List<SlotWidget> target, IEnumerable<SlotData> slots)
        {
            if (slots == null)
            {
                return;
            }

            foreach (SlotData slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                SlotWidget widget = new SlotWidget(this, slot);
                widget.Parent = this;
                target.Add(widget);
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

        private bool SetFocus(int column, int row)
        {
            SlotWidget previous = FocusedSlot;
            _focusedColumn = column;
            _focusedRow = row;
            ClampFocus();
            SlotWidget next = FocusedSlot;
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
                Speak("Press space to select a stack to drag.");
                return true;
            }

            if (target == null || ReferenceEquals(target, _dragSource))
            {
                return CancelDrag();
            }

            SlotWidget source = _dragSource;
            _dragSource = null;
            if (_drop != null)
            {
                return _drop(source, target);
            }

            return true;
        }

        private bool CancelDrag()
        {
            if (_dragSource == null)
            {
                return false;
            }

            _dragSource = null;
            Speak("Drag cancelled.");
            return true;
        }

        private List<SlotWidget> GetColumn(int column)
        {
            return column == 0 ? _wielderSlots : _joiningSlots;
        }

        private bool SetFocusedSlotById(List<SlotWidget> slots, int column, string id)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                SlotWidget slot = slots[i];
                if (slot != null && slot.Id == id)
                {
                    _focusedColumn = column;
                    _focusedRow = i;
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

        internal sealed class SlotData
        {
            public SlotData(
                string id,
                string armyLabel,
                int slotNumber,
                string troopName,
                int currentSize,
                int maxSize,
                bool isOccupied,
                object nativeSource,
                Action onFocus)
            {
                Id = id ?? string.Empty;
                ArmyLabel = armyLabel ?? string.Empty;
                SlotNumber = slotNumber;
                TroopName = troopName ?? string.Empty;
                CurrentSize = currentSize;
                MaxSize = maxSize;
                IsOccupied = isOccupied;
                NativeSource = nativeSource;
                OnFocus = onFocus;
            }

            public string Id { get; private set; }

            public string ArmyLabel { get; private set; }

            public int SlotNumber { get; private set; }

            public string TroopName { get; private set; }

            public int CurrentSize { get; private set; }

            public int MaxSize { get; private set; }

            public bool IsOccupied { get; private set; }

            public object NativeSource { get; private set; }

            public Action OnFocus { get; private set; }
        }

        internal sealed class SlotWidget : Widget
        {
            private readonly ArmyExchangeGridWidget _grid;
            private readonly SlotData _data;

            public SlotWidget(ArmyExchangeGridWidget grid, SlotData data)
                : base(data != null ? data.Id : string.Empty)
            {
                _grid = grid;
                _data = data;
            }

            public string ArmyLabel
            {
                get { return _data != null ? _data.ArmyLabel : string.Empty; }
            }

            public int SlotNumber
            {
                get { return _data != null ? _data.SlotNumber : 0; }
            }

            public bool IsOccupied
            {
                get { return _data != null && _data.IsOccupied; }
            }

            public object NativeSource
            {
                get { return _data != null ? _data.NativeSource : null; }
            }

            public override string GetFocusMessage()
            {
                return GetLabel();
            }

            public override string GetLabel()
            {
                if (_data == null)
                {
                    return string.Empty;
                }

                string slotLabel = _data.ArmyLabel + " slot " + _data.SlotNumber;
                if (!_data.IsOccupied)
                {
                    return "Empty, " + slotLabel;
                }

                return _data.TroopName + ", " + _data.CurrentSize + " / " + _data.MaxSize + ", " + slotLabel;
            }

            public override bool ClaimsAction(string actionKey)
            {
                return _grid != null && _grid.ClaimsAction(actionKey);
            }

            public override bool HandleAction(InputAction action)
            {
                return _grid != null && _grid.HandleAction(action);
            }

            protected override void OnFocus()
            {
                _data?.OnFocus?.Invoke();
            }
        }
    }
}
