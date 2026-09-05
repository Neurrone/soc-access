using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using CodexContentItem = SongsOfConquestAccess.Adapters.CodexMenuAdapter.CodexContentItem;
using CodexContentItemKind = SongsOfConquestAccess.Adapters.CodexMenuAdapter.CodexContentItemKind;

namespace SongsOfConquestAccess.UI
{
    public sealed class CodexContentWidget : Widget
    {
        private readonly Func<IReadOnlyList<CodexContentItem>> _getItems;
        private readonly Action _onFocus;
        private readonly Action<CodexContentItem> _onItemFocused;
        private int _focusedIndex;

        public CodexContentWidget(
            string id,
            Func<IReadOnlyList<CodexContentItem>> getItems,
            Action onFocus,
            Action<CodexContentItem> onItemFocused = null)
            : base(id)
        {
            _getItems = getItems;
            _onFocus = onFocus;
            _onItemFocused = onItemFocused;
        }

        public override string GetLabel()
        {
            IReadOnlyList<CodexContentItem> items = Items;
            if (items.Count == 0)
            {
                return string.Empty;
            }

            CodexContentItem item = CurrentItem;
            return FormatItem(item);
        }

        public override string GetRole()
        {
            return ModText.Get(ModStrings.UI.RoleDocument);
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.NextMenuItem.Key
                || actionKey == AccessibilityActions.PreviousMenuItem.Key
                || actionKey == AccessibilityActions.FirstMenuItem.Key
                || actionKey == AccessibilityActions.LastMenuItem.Key
                || actionKey == AccessibilityActions.NextHeading.Key
                || actionKey == AccessibilityActions.PreviousHeading.Key;
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (action.Key == AccessibilityActions.NextMenuItem.Key)
            {
                return MoveRelative(1);
            }

            if (action.Key == AccessibilityActions.PreviousMenuItem.Key)
            {
                return MoveRelative(-1);
            }

            if (action.Key == AccessibilityActions.FirstMenuItem.Key)
            {
                return MoveTo(0);
            }

            if (action.Key == AccessibilityActions.LastMenuItem.Key)
            {
                return MoveTo(Items.Count - 1);
            }

            if (action.Key == AccessibilityActions.NextHeading.Key)
            {
                return MoveToHeading(1);
            }

            if (action.Key == AccessibilityActions.PreviousHeading.Key)
            {
                return MoveToHeading(-1);
            }

            return false;
        }

        protected override void OnFocus()
        {
            _onFocus?.Invoke();
            ClampIndex();
        }

        private IReadOnlyList<CodexContentItem> Items
        {
            get
            {
                IReadOnlyList<CodexContentItem> items = _getItems != null ? _getItems() : null;
                return items ?? new CodexContentItem[0];
            }
        }

        private CodexContentItem CurrentItem
        {
            get
            {
                IReadOnlyList<CodexContentItem> items = Items;
                if (items.Count == 0)
                {
                    return null;
                }

                ClampIndex();
                return items[_focusedIndex];
            }
        }

        private bool MoveRelative(int delta)
        {
            return MoveTo(_focusedIndex + delta);
        }

        private bool MoveTo(int index)
        {
            IReadOnlyList<CodexContentItem> items = Items;
            if (items.Count == 0)
            {
                return false;
            }

            if (index < 0)
            {
                index = 0;
            }
            else if (index >= items.Count)
            {
                index = items.Count - 1;
            }

            if (index == _focusedIndex)
            {
                NotifyItemFocused();
                SpeakCurrent();
                return true;
            }

            _focusedIndex = index;
            NotifyItemFocused();
            SpeakCurrent();
            return true;
        }

        private bool MoveToHeading(int delta)
        {
            IReadOnlyList<CodexContentItem> items = Items;
            if (items.Count == 0)
            {
                return false;
            }

            int index = _focusedIndex + delta;
            while (index >= 0 && index < items.Count)
            {
                if (items[index] != null && items[index].Kind == CodexContentItemKind.Heading)
                {
                    _focusedIndex = index;
                    NotifyItemFocused();
                    SpeakCurrent();
                    return true;
                }

                index += delta;
            }

            return false;
        }

        private void ClampIndex()
        {
            int count = Items.Count;
            if (count == 0)
            {
                _focusedIndex = 0;
            }
            else if (_focusedIndex < 0)
            {
                _focusedIndex = 0;
            }
            else if (_focusedIndex >= count)
            {
                _focusedIndex = count - 1;
            }
        }

        private void SpeakCurrent()
        {
            string message = FormatItem(CurrentItem);
            if (!string.IsNullOrWhiteSpace(message))
            {
                SpeechPipeline.Output(new SpeechRequest(message, interrupt: true));
            }
        }

        private void NotifyItemFocused()
        {
            CodexContentItem item = CurrentItem;
            if (item != null)
            {
                _onItemFocused?.Invoke(item);
            }
        }

        private static string FormatItem(CodexContentItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.Kind == CodexContentItemKind.Essence)
            {
                return FormatEssenceItem(item);
            }

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                return string.Empty;
            }

            return item.Kind == CodexContentItemKind.Heading
                ? ModText.Get(ModStrings.UI.Heading, item.Text)
                : item.Text;
        }

        private static string FormatEssenceItem(CodexContentItem item)
        {
            if (item == null || item.Essences == null || item.Essences.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < item.Essences.Count; i++)
            {
                if (item.Essences[i] == null || string.IsNullOrWhiteSpace(item.Essences[i].Text))
                {
                    continue;
                }

                parts.Add(item.Essences[i].Text);
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }

            string values = ModText.JoinList(parts);
            return string.IsNullOrWhiteSpace(item.Text)
                ? values
                : ModText.Get(ModStrings.UI.LabelValue, item.Text, values);
        }
    }
}
