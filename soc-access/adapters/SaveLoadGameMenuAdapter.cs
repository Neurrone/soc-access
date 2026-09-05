using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class SaveLoadGameMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(SaveLoadGameMenu), "_settings");
        private static readonly FieldInfo CurrentModeField = AccessTools.Field(typeof(SaveLoadGameMenu), "_currentMode");
        private static readonly FieldInfo ActiveEntriesField = AccessTools.Field(typeof(SaveLoadGameMenu), "_activeEntries");
        private static readonly FieldInfo SelectedEntryField = AccessTools.Field(typeof(SaveLoadGameMenu), "_selectedSaveEntry");
        private static readonly MethodInfo TryCloseMethod = AccessTools.Method(typeof(SaveLoadGameMenu), "TryClose");
        private static readonly MethodInfo SetupSelectedSaveMethod = AccessTools.Method(typeof(SaveLoadGameMenu), "SetupSelectedSave");

        private static readonly FieldInfo EntryButtonField = AccessTools.Field(typeof(SaveLoadGameMenuEntry), "_button");
        private static readonly FieldInfo EntrySelectedFrameField = AccessTools.Field(typeof(SaveLoadGameMenuEntry), "_selectedFrame");
        private static readonly FieldInfo EntryDateTextField = AccessTools.Field(typeof(SaveLoadGameMenuEntry), "_dateText");
        private static readonly FieldInfo EntryDefinitionField = AccessTools.Field(typeof(SaveLoadGameMenuEntry), "_loadGameDefinition");

        private static readonly FieldInfo TabGroupTabsField = AccessTools.Field(typeof(UITabGroup), "_tabs");
        private static readonly Type TabType = typeof(UITabGroup).GetNestedType("Tab");
        private static readonly FieldInfo TabButtonField = TabType != null ? AccessTools.Field(TabType, "Button") : null;

        private readonly SaveLoadGameMenu _menu;

        public SaveLoadGameMenuAdapter(SaveLoadGameMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public SaveLoadGameMenu.Mode Mode
        {
            get
            {
                object value = CurrentModeField != null && _menu != null ? CurrentModeField.GetValue(_menu) : null;
                return value is SaveLoadGameMenu.Mode ? (SaveLoadGameMenu.Mode)value : SaveLoadGameMenu.Mode.Load;
            }
        }

        public string Title
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                string title = SpeechTextSanitizer.Normalize(
                    UITextMeshTextUtility.GetEffectiveText(settings != null ? settings.TitleText : null));
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }

                return string.Empty;
            }
        }

        public IUITextMeshInputField InputField
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                return settings != null ? settings.InputField : null;
            }
        }

        public bool IsPresent()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return _menu != null
                && settings != null
                && IsActive(settings.Parent as Component);
        }

        public bool Close()
        {
            if (_menu == null || TryCloseMethod == null)
            {
                return false;
            }

            TryCloseMethod.Invoke(_menu, new object[0]);
            return true;
        }

        public bool IsInputVisible()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return settings != null && IsActive(settings.InputField as Component);
        }

        public bool IsInputEnabled()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return settings != null
                && settings.InputField != null
                && settings.InputField.Interactable
                && IsInputVisible();
        }

        public void FocusInput()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            if (settings != null && settings.InputField != null)
            {
                NativeSelectionUtility.Select(settings.InputField.GetSelectable());
            }
        }

        public string GetSaveDescriptionText()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return SpeechTextSanitizer.Normalize(
                UITextMeshTextUtility.GetEffectiveText(settings != null ? settings.SaveDescriptionText : null));
        }

        public bool IsSaveDescriptionVisible()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return settings != null && settings.SaveDescriptionContainer != null && settings.SaveDescriptionContainer.activeInHierarchy;
        }

        public string GetSelectedSaveText()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return SpeechTextSanitizer.Normalize(
                UITextMeshTextUtility.GetEffectiveText(settings != null ? settings.SelectedSaveText : null));
        }

        // The menu's own verdict on the selected save: SetupSelectedSave ends on InvalidEntry when
        // the save fails validation (version, missing content) and leaves the load button off.
        // The field starts out at InvalidEntry, so read it only after a selection has settled.
        public bool IsSelectedSaveRefused()
        {
            object state = _menu != null ? SelectionStateField?.GetValue(_menu) : null;
            return state != null && state.ToString() == "InvalidEntry";
        }

        private static readonly FieldInfo SelectionStateField =
            AccessTools.Field(typeof(SaveLoadGameMenu), "_selectionState");

        public string GetInformationText()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return SpeechTextSanitizer.Normalize(
                UITextMeshTextUtility.GetEffectiveText(settings != null ? settings.InformationText : null));
        }

        public bool HasDetailsText()
        {
            if (!IsDetailsVisible())
            {
                return false;
            }

            string information = GetInformationText();
            return !string.IsNullOrWhiteSpace(information);
        }

        public bool IsDetailsVisible()
        {
            SaveLoadGameMenu.Settings settings = Settings;
            return settings != null
                && settings.SelectedSaveContainer != null
                && settings.SelectedSaveContainer.Active;
        }

        public string GetDetailsText()
        {
            return GetInformationText();
        }

        public IReadOnlyList<TabItem> GetTabs()
        {
            List<TabItem> result = new List<TabItem>();
            SaveLoadGameMenu.Settings settings = Settings;
            UITabGroup tabGroup = settings != null ? settings.CategoryTabGroup : null;
            IList tabs = TabGroupTabsField != null && tabGroup != null ? TabGroupTabsField.GetValue(tabGroup) as IList : null;
            if (tabs == null)
            {
                return result;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                object tab = tabs[i];
                UIButton button = TabButtonField != null && tab != null ? TabButtonField.GetValue(tab) as UIButton : null;
                int index = i;
                result.Add(new TabItem(
                    "save-load-tab-" + index,
                    index,
                    button,
                    () => GetTabLabel(index, button),
                    () => ActivateTab(index, button),
                    () => FocusButton(button),
                    () => IsActive(tabGroup as Component) && MenuButtonAdapterBase.IsButtonVisible(button),
                    () => button != null && button.Active && button.Interactable,
                    () => tabGroup != null && tabGroup.CurrentTab == index));
            }

            return result;
        }

        public IReadOnlyList<SaveEntry> GetEntries()
        {
            List<SaveEntry> result = new List<SaveEntry>();
            IList entries = ActiveEntriesField != null && _menu != null ? ActiveEntriesField.GetValue(_menu) as IList : null;
            if (entries == null)
            {
                return result;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SaveLoadGameMenuEntry entry = entries[i] as SaveLoadGameMenuEntry;
                if (entry == null)
                {
                    continue;
                }

                int index = i;
                result.Add(new SaveEntry(this, entry, index));
            }

            return result;
        }

        public ButtonItem SaveButton
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                return BuildButton("save-load-save", settings != null ? settings.SaveGameButton : null);
            }
        }

        public ButtonItem LoadButton
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                return BuildEnabledOnlyButton("save-load-load", settings != null ? settings.LoadGameButton : null);
            }
        }

        public ButtonItem LoadAsHotseatButton
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                return BuildEnabledOnlyButton("save-load-load-hotseat", settings != null ? settings.LoadAsHotseatGameButton : null);
            }
        }

        public ButtonItem LoadAsOnlineButton
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                return BuildEnabledOnlyButton("save-load-load-online", settings != null ? settings.LoadAsOnlineGameButton : null);
            }
        }

        public ButtonItem DeleteButton
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                return BuildButton("save-load-delete", settings != null ? settings.DeleteSaveButton : null);
            }
        }

        public ButtonItem CancelButton
        {
            get
            {
                SaveLoadGameMenu.Settings settings = Settings;
                UIButton button = settings != null ? settings.ExitButton : null;
                return new ButtonItem(
                    "save-load-cancel",
                    () => GetButtonLabel(button),
                    Close,
                    () => FocusButton(button),
                    () => true,
                    () => true);
            }
        }

        private SaveLoadGameMenu.Settings Settings
        {
            get { return SettingsField != null && _menu != null ? SettingsField.GetValue(_menu) as SaveLoadGameMenu.Settings : null; }
        }

        private ButtonItem BuildButton(string id, UIButton button)
        {
            return new ButtonItem(
                id,
                () => GetButtonLabel(button),
                () => ActivateButton(button),
                () => FocusButton(button),
                () => button != null && button.Active && button.Interactable,
                () => MenuButtonAdapterBase.IsButtonVisible(button));
        }

        private ButtonItem BuildEnabledOnlyButton(string id, UIButton button)
        {
            return new ButtonItem(
                id,
                () => GetButtonLabel(button),
                () => ActivateButton(button),
                () => FocusButton(button),
                () => button != null && button.Active && button.Interactable,
                () => button != null
                    && button.Active
                    && button.Interactable
                    && MenuButtonAdapterBase.IsButtonVisible(button));
        }

        private static string GetButtonLabel(UIButton button)
        {
            return MenuButtonTextUtility.GetAllVisibleText(button);
        }

        private static string GetTabLabel(int index, UIButton button)
        {
            return GetButtonLabel(button);
        }

        private static bool ActivateButton(UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable || !MenuButtonAdapterBase.IsButtonVisible(button))
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static bool FocusButton(UIButton button)
        {
            if (button == null)
            {
                return false;
            }

            return NativeSelectionUtility.Select(button);
        }

        private static bool ActivateTab(int index, UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable || !MenuButtonAdapterBase.IsButtonVisible(button))
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private bool IsEntrySelected(SaveLoadGameMenuEntry entry)
        {
            SaveLoadGameMenuEntry selected = SelectedEntryField != null && _menu != null
                ? SelectedEntryField.GetValue(_menu) as SaveLoadGameMenuEntry
                : null;
            if (ReferenceEquals(selected, entry))
            {
                return true;
            }

            UIImage selectedFrame = EntrySelectedFrameField != null && entry != null
                ? EntrySelectedFrameField.GetValue(entry) as UIImage
                : null;
            return IsActive(selectedFrame as Component);
        }

        private static bool IsActive(Component component)
        {
            return component != null && component.gameObject.activeInHierarchy;
        }

        public sealed class TabItem
        {
            private readonly Func<string> _getLabel;
            private readonly Func<bool> _activate;
            private readonly Func<bool> _focus;
            private readonly Func<bool> _isVisible;
            private readonly Func<bool> _isEnabled;
            private readonly Func<bool> _isSelected;

            public TabItem(
                string id,
                int index,
                UIButton button,
                Func<string> getLabel,
                Func<bool> activate,
                Func<bool> focus,
                Func<bool> isVisible,
                Func<bool> isEnabled,
                Func<bool> isSelected)
            {
                Id = id;
                Index = index;
                Button = button;
                _getLabel = getLabel;
                _activate = activate;
                _focus = focus;
                _isVisible = isVisible;
                _isEnabled = isEnabled;
                _isSelected = isSelected;
            }

            public string Id { get; private set; }
            public int Index { get; private set; }
            public UIButton Button { get; private set; }
            public string GetLabel() { return _getLabel != null ? _getLabel() : string.Empty; }
            public bool Activate() { return _activate != null && _activate(); }
            public void Focus() { if (_focus != null) { _focus(); } }
            public bool IsVisible() { return _isVisible == null || _isVisible(); }
            public bool IsEnabled() { return _isEnabled == null || _isEnabled(); }
            public bool IsSelected() { return _isSelected != null && _isSelected(); }
        }

        public sealed class SaveEntry
        {
            private readonly SaveLoadGameMenuAdapter _adapter;
            private readonly SaveLoadGameMenuEntry _entry;

            public SaveEntry(SaveLoadGameMenuAdapter adapter, SaveLoadGameMenuEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                Index = index;
            }

            public int Index { get; private set; }

            public string Id
            {
                get { return "save-load-entry-" + Index; }
            }

            public string SaveName
            {
                get
                {
                    LoadGameDefinition definition = Definition;
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.SaveName))
                    {
                        return SpeechTextSanitizer.Normalize(definition.SaveName);
                    }

                    UIButton button = EntryButtonField != null ? EntryButtonField.GetValue(_entry) as UIButton : null;
                    return SpeechTextSanitizer.Normalize(button != null ? button.Text : null);
                }
            }

            public string DateText
            {
                get
                {
                    UITextMesh text = EntryDateTextField != null ? EntryDateTextField.GetValue(_entry) as UITextMesh : null;
                    return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
                }
            }

            public bool IsCorrupt
            {
                get
                {
                    LoadGameDefinition definition = Definition;
                    return definition != null && definition.Corrupt;
                }
            }

            public DateTime LastWriteTime
            {
                get
                {
                    LoadGameDefinition definition = Definition;
                    return definition != null ? definition.LastWriteTime : DateTime.MinValue;
                }
            }

            public bool IsSelected
            {
                get { return _adapter != null && _adapter.IsEntrySelected(_entry); }
            }

            public bool IsVisible()
            {
                return IsActive(_entry as Component);
            }

            public bool Select()
            {
                UIButton button = EntryButtonField != null ? EntryButtonField.GetValue(_entry) as UIButton : null;
                if (button == null || !button.Active || !button.Interactable || !IsVisible())
                {
                    return false;
                }

                return NativeSelectionUtility.Click(button);
            }

            public void Focus()
            {
                // Accessibility focus must not select a save. The game reuses
                // native selection to load details and, in save mode, to copy
                // the selected save name into the edit field.
            }

            private LoadGameDefinition Definition
            {
                get { return EntryDefinitionField != null ? EntryDefinitionField.GetValue(_entry) as LoadGameDefinition : null; }
            }
        }

        public sealed class ButtonItem
        {
            private readonly Func<string> _getLabel;
            private readonly Func<bool> _activate;
            private readonly Action _focus;
            private readonly Func<bool> _isEnabled;
            private readonly Func<bool> _isVisible;

            public ButtonItem(
                string id,
                Func<string> getLabel,
                Func<bool> activate,
                Action focus,
                Func<bool> isEnabled,
                Func<bool> isVisible)
            {
                Id = id;
                _getLabel = getLabel;
                _activate = activate;
                _focus = focus;
                _isEnabled = isEnabled;
                _isVisible = isVisible;
            }

            public string Id { get; private set; }
            public string GetLabel() { return _getLabel != null ? _getLabel() : string.Empty; }
            public bool Activate() { return _activate != null && _activate(); }
            public void Focus() { _focus?.Invoke(); }
            public bool IsEnabled() { return _isEnabled == null || _isEnabled(); }
            public bool IsVisible() { return _isVisible == null || _isVisible(); }
        }
    }
}
