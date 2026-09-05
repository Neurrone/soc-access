using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.UI
{
    public sealed class AnnouncementOrderMenuWidget : Widget
    {
        private readonly AnnouncementGroupDefinition _group;
        private readonly Action<AnnouncementElementDefinition> _configure;
        private readonly Func<AnnouncementGroupDefinition, IReadOnlyList<string>> _getOrder;
        private readonly Func<AnnouncementGroupDefinition, string, int, bool> _moveElement;
        private readonly List<RowWidget> _rows = new List<RowWidget>();
        private string _focusedKey;

        public AnnouncementOrderMenuWidget(
            string id,
            AnnouncementGroupDefinition group,
            Action<AnnouncementElementDefinition> configure)
            : this(id, group, configure, ModSettings.GetAnnouncementOrder, ModSettings.MoveAnnouncementElement)
        {
        }

        public AnnouncementOrderMenuWidget(
            string id,
            AnnouncementGroupDefinition group,
            Action<AnnouncementElementDefinition> configure,
            Func<AnnouncementGroupDefinition, IReadOnlyList<string>> getOrder,
            Func<AnnouncementGroupDefinition, string, int, bool> moveElement)
            : base(id)
        {
            _group = group;
            _configure = configure;
            _getOrder = getOrder;
            _moveElement = moveElement;
            SyncRowsWithOrder();
        }

        public string FocusedElementKey
        {
            get
            {
                RowWidget row = FocusedRow;
                return row != null ? row.Key : string.Empty;
            }
        }

        public int FocusedButtonIndex
        {
            get
            {
                RowWidget row = FocusedRow;
                return row != null ? row.FocusedButtonIndex : -1;
            }
        }

        public override string GetLabel()
        {
            return _group != null ? ModText.Get(_group.Label) : string.Empty;
        }

        public override Widget GetFocusedWidget()
        {
            RowWidget row = FocusedRow;
            return row != null ? row.GetFocusedWidget() : this;
        }

        public override bool EnsureFocus()
        {
            SyncRowsWithOrder();
            RowWidget row = FocusedRow;
            return row != null && row.EnsureFocus();
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.NextMenuItem.Key
                || actionKey == AccessibilityActions.PreviousMenuItem.Key
                || actionKey == AccessibilityActions.FirstMenuItem.Key
                || actionKey == AccessibilityActions.LastMenuItem.Key;
        }

        public override bool HasClaimInTree(string actionKey)
        {
            if (ClaimsAction(actionKey))
            {
                return true;
            }

            RowWidget row = FocusedRow;
            return row != null && row.HasClaimInTree(actionKey);
        }

        public override bool HasFocusedClaimInTree(string actionKey)
        {
            return HasClaimInTree(actionKey);
        }

        public override bool HandleAction(InputAction action)
        {
            RowWidget row = FocusedRow;
            if (row != null && row.HandleAction(action))
            {
                return true;
            }

            if (action == null)
            {
                return false;
            }

            if (action.Key == AccessibilityActions.NextMenuItem.Key)
            {
                return MoveFocusedRow(1);
            }

            if (action.Key == AccessibilityActions.PreviousMenuItem.Key)
            {
                return MoveFocusedRow(-1);
            }

            if (action.Key == AccessibilityActions.FirstMenuItem.Key)
            {
                return SetFocusedRowIndex(0);
            }

            if (action.Key == AccessibilityActions.LastMenuItem.Key)
            {
                return SetFocusedRowIndex(_rows.Count - 1);
            }

            return false;
        }

        private RowWidget FocusedRow
        {
            get
            {
                SyncRowsWithOrder();
                if (_rows.Count == 0)
                {
                    return null;
                }

                int index = GetFocusedRowIndex();
                if (index < 0)
                {
                    index = 0;
                    _focusedKey = _rows[index].Key;
                }

                return _rows[index];
            }
        }

        private bool MoveFocusedRow(int delta)
        {
            if (_rows.Count == 0)
            {
                return false;
            }

            int index = GetFocusedRowIndex();
            int targetIndex = index + delta;
            if (targetIndex < 0 || targetIndex >= _rows.Count)
            {
                return true;
            }

            return SetFocusedRowIndex(targetIndex);
        }

        private bool SetFocusedRowIndex(int index)
        {
            if (_rows.Count == 0)
            {
                return false;
            }

            if (index < 0)
            {
                index = 0;
            }
            else if (index >= _rows.Count)
            {
                index = _rows.Count - 1;
            }

            if (_rows[index].Key == _focusedKey)
            {
                return true;
            }

            RowWidget previousRow = FocusedRow;
            int buttonIndex = previousRow != null ? previousRow.FocusedButtonIndex : 0;
            _rows[index].SetFocusedButtonIndex(buttonIndex);
            _focusedKey = _rows[index].Key;
            UIManager.RequestFocus(this);
            return true;
        }

        private bool MoveElement(RowWidget row, int delta)
        {
            if (row == null)
            {
                return false;
            }

            bool moved = _moveElement != null && _moveElement(_group, row.Key, delta);
            if (!moved)
            {
                return true;
            }

            _focusedKey = row.Key;
            SyncRowsWithOrder();
            SpeakMoveFeedback(row.Key);
            UIManager.RequestFocus(this);
            return true;
        }

        private void SpeakMoveFeedback(string key)
        {
            string text = BuildMoveFeedback(key);
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
        }

        public string BuildMoveFeedback(string key)
        {
            SyncRowsWithOrder();
            int index = GetRowIndex(key);
            if (index < 0 || _rows.Count <= 1)
            {
                return string.Empty;
            }

            string previous = index > 0 ? GetRowElementLabel(_rows[index - 1]) : string.Empty;
            string next = index + 1 < _rows.Count ? GetRowElementLabel(_rows[index + 1]) : string.Empty;
            if (!string.IsNullOrWhiteSpace(previous) && !string.IsNullOrWhiteSpace(next))
            {
                return ModText.Get(ModStrings.Screens.MovedBetween, previous, next);
            }

            if (!string.IsNullOrWhiteSpace(next))
            {
                return ModText.Get(ModStrings.Screens.MovedBefore, next);
            }

            return !string.IsNullOrWhiteSpace(previous)
                ? ModText.Get(ModStrings.Screens.MovedAfter, previous)
                : string.Empty;
        }

        private void Configure(RowWidget row)
        {
            if (row != null)
            {
                _configure?.Invoke(row.Element);
            }
        }

        private int GetFocusedRowIndex()
        {
            if (string.IsNullOrWhiteSpace(_focusedKey))
            {
                return _rows.Count > 0 ? 0 : -1;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Key == _focusedKey)
                {
                    return i;
                }
            }

            return _rows.Count > 0 ? 0 : -1;
        }

        private int GetRowIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return -1;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Key == key)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetRowElementLabel(RowWidget row)
        {
            return row != null && row.Element != null ? ModText.Get(row.Element.Label) : string.Empty;
        }

        private void SyncRowsWithOrder()
        {
            if (_group == null)
            {
                _rows.Clear();
                _focusedKey = string.Empty;
                return;
            }

            Dictionary<string, RowWidget> existing = new Dictionary<string, RowWidget>();
            for (int i = 0; i < _rows.Count; i++)
            {
                existing[_rows[i].Key] = _rows[i];
            }

            IReadOnlyList<string> order = _getOrder != null ? _getOrder(_group) : GetDefaultOrder();
            _rows.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                AnnouncementElementDefinition element = _group.GetElement(order[i]);
                if (element == null)
                {
                    continue;
                }

                RowWidget row;
                if (!existing.TryGetValue(element.Key, out row))
                {
                    row = new RowWidget(this, element);
                }

                row.Parent = this;
                _rows.Add(row);
            }

            if (_rows.Count == 0)
            {
                _focusedKey = string.Empty;
            }
            else if (GetFocusedRowIndex() < 0 || _group.GetElement(_focusedKey) == null)
            {
                _focusedKey = _rows[0].Key;
            }
        }

        private IReadOnlyList<string> GetDefaultOrder()
        {
            if (_group == null)
            {
                return new string[0];
            }

            List<string> keys = new List<string>();
            for (int i = 0; i < _group.Elements.Count; i++)
            {
                keys.Add(_group.Elements[i].Key);
            }

            return keys;
        }

        private sealed class RowWidget : Widget
        {
            private const int ConfigureButtonIndex = 0;
            private const int MoveUpButtonIndex = 1;
            private const int MoveDownButtonIndex = 2;
            private const int ButtonCount = 3;

            private readonly AnnouncementOrderMenuWidget _owner;
            private readonly RowButtonWidget[] _buttons;
            private int _focusedButtonIndex;

            public RowWidget(AnnouncementOrderMenuWidget owner, AnnouncementElementDefinition element)
                : base("announcement-order-row-" + (element != null ? element.Key : "unknown"))
            {
                _owner = owner;
                Element = element;
                _buttons = new[]
                {
                    new RowButtonWidget(this, ConfigureButtonIndex),
                    new RowButtonWidget(this, MoveUpButtonIndex),
                    new RowButtonWidget(this, MoveDownButtonIndex)
                };
                for (int i = 0; i < _buttons.Length; i++)
                {
                    _buttons[i].Parent = this;
                }
            }

            public AnnouncementElementDefinition Element { get; private set; }

            public string Key
            {
                get { return Element != null ? Element.Key : string.Empty; }
            }

            public int FocusedButtonIndex
            {
                get { return _focusedButtonIndex; }
            }

            public override bool AnnounceName
            {
                get { return true; }
            }

            public override string GetLabel()
            {
                string label = Element != null ? ModText.Get(Element.Label) : string.Empty;
                return ModText.Get(ModStrings.Screens.AnnouncementOrderRow, label);
            }

            public override Widget GetFocusedWidget()
            {
                return _buttons[_focusedButtonIndex];
            }

            public override bool EnsureFocus()
            {
                return _buttons.Length > 0;
            }

            public void SetFocusedButtonIndex(int index)
            {
                if (index < 0)
                {
                    index = 0;
                }
                else if (index >= _buttons.Length)
                {
                    index = _buttons.Length - 1;
                }

                _focusedButtonIndex = index;
            }

            public override bool ClaimsAction(string actionKey)
            {
                return actionKey == AccessibilityActions.PreviousColumn.Key
                    || actionKey == AccessibilityActions.NextColumn.Key;
            }

            public override bool HasClaimInTree(string actionKey)
            {
                return ClaimsAction(actionKey)
                    || _buttons[_focusedButtonIndex].ClaimsAction(actionKey);
            }

            public override bool HasFocusedClaimInTree(string actionKey)
            {
                return HasClaimInTree(actionKey);
            }

            public override bool HandleAction(InputAction action)
            {
                if (_buttons[_focusedButtonIndex].HandleAction(action))
                {
                    return true;
                }

                if (action == null)
                {
                    return false;
                }

                if (action.Key == AccessibilityActions.PreviousColumn.Key)
                {
                    return MoveButton(-1);
                }

                if (action.Key == AccessibilityActions.NextColumn.Key)
                {
                    return MoveButton(1);
                }

                return false;
            }

            private bool MoveButton(int delta)
            {
                int targetIndex = _focusedButtonIndex + delta;
                if (targetIndex < 0 || targetIndex >= _buttons.Length)
                {
                    return true;
                }

                _focusedButtonIndex = targetIndex;
                UIManager.RequestFocus(_owner);
                return true;
            }

            private bool ActivateButton(int buttonIndex)
            {
                if (buttonIndex == ConfigureButtonIndex)
                {
                    _owner.Configure(this);
                    return true;
                }

                return _owner.MoveElement(this, buttonIndex == MoveUpButtonIndex ? -1 : 1);
            }

            private string GetButtonLabel(int buttonIndex)
            {
                if (buttonIndex == MoveUpButtonIndex)
                {
                    return ModText.Get(ModStrings.Screens.MoveUp);
                }

                if (buttonIndex == MoveDownButtonIndex)
                {
                    return ModText.Get(ModStrings.Screens.MoveDown);
                }

                return ModText.Get(ModStrings.Screens.Configure);
            }

            private sealed class RowButtonWidget : Widget
            {
                private readonly RowWidget _row;
                private readonly int _buttonIndex;

                public RowButtonWidget(RowWidget row, int buttonIndex)
                    : base(row.Id + "-button-" + buttonIndex)
                {
                    _row = row;
                    _buttonIndex = buttonIndex;
                }

                public override string GetLabel()
                {
                    return _row.GetButtonLabel(_buttonIndex);
                }

                public override string GetRole()
                {
                    return ModText.Get(ModStrings.UI.RoleButton);
                }

                public override string GetStatus()
                {
                    return ModText.Get(ModStrings.Common.CountOf, _buttonIndex + 1, ButtonCount);
                }

                public override bool ClaimsAction(string actionKey)
                {
                    return actionKey == AccessibilityActions.Activate.Key;
                }

                public override bool HandleAction(InputAction action)
                {
                    if (action == null || action.Key != AccessibilityActions.Activate.Key)
                    {
                        return false;
                    }

                    return _row.ActivateButton(_buttonIndex);
                }
            }
        }
    }
}
