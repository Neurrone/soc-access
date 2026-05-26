using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureLobbyGameSettingsAdapter
    {
        private static readonly FieldInfo ContainerField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_container");
        private static readonly FieldInfo FactoryField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_factory");
        private static readonly FieldInfo ApplyButtonField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_applyButton");
        private static readonly FieldInfo CancelButtonField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_cancelButton");
        private static readonly FieldInfo LocalizationField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_localizationHandler");
        private static readonly MethodInfo DropdownGetTextMethod =
            AccessTools.Method(typeof(UITextMeshDropdown), "GetText");
        private static readonly FieldInfo TimeInputMinutesField =
            AccessTools.Field(typeof(UITimeInputField), "_minutesInputfield");
        private static readonly FieldInfo TimeInputSecondsField =
            AccessTools.Field(typeof(UITimeInputField), "_secondsInputfield");

        private readonly LobbyMapSettingsMenu _menu;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyGameSettingsAdapter(LobbyMapSettingsMenu menu)
        {
            _menu = menu;
            _localization = menu != null && LocalizationField != null
                ? LocalizationField.GetValue(menu) as ILocalizationHandler
                : GlobalLocalizationVariables.LocalizationHandler;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get
            {
                return SpeechTextSanitizer.Normalize(
                    GameText.Get(_localization, "Lobby/CreateLobby/SetMapSettings", string.Empty));
            }
        }

        public bool IsPresent()
        {
            UITransform container = GetField<UITransform>(ContainerField);
            return _menu != null
                && container != null
                && container.Active
                && ((Component)container).gameObject.activeInHierarchy;
        }

        public IReadOnlyList<ControlItem> GetContentControls()
        {
            IMenuFactoryCollection factory = GetField<IMenuFactoryCollection>(FactoryField);
            if (factory == null)
            {
                return new ControlItem[0];
            }

            List<ControlItem> items = new List<ControlItem>();
            AddTextItems(items, factory);
            AddDropdownItems(items, factory);
            AddToggleItems(items, factory);
            AddTextInputItems(items, factory);
            AddTimeInputItems(items, factory);
            AddButtonItems(items, factory);
            items.Sort(CompareControlItems);
            return items;
        }

        public ButtonItem GetCancelButton()
        {
            UIButton button = GetField<UIButton>(CancelButtonField);
            return button != null
                ? BuildButton("game-settings-cancel", button)
                : null;
        }

        public ButtonItem GetApplyButton()
        {
            UIButton button = GetField<UIButton>(ApplyButtonField);
            return button != null
                ? BuildButton("game-settings-confirm", button)
                : null;
        }

        private void AddTextItems(List<ControlItem> items, IMenuFactoryCollection factory)
        {
            List<IUITextMesh> texts = new List<IUITextMesh>();
            factory.GetCreatedTextMeshes(texts);
            for (int i = 0; i < texts.Count; i++)
            {
                IUITextMesh text = texts[i];
                Component component = text as Component;
                if (component == null)
                {
                    continue;
                }

                int index = i;
                items.Add(new ControlItem(
                    component.transform,
                    new TextItem(
                        "game-settings-text-" + index,
                        () => SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text)),
                        () => IsActive(component))));
            }
        }

        private void AddDropdownItems(List<ControlItem> items, IMenuFactoryCollection factory)
        {
            List<IUITextMeshDropdown> dropdowns = new List<IUITextMeshDropdown>();
            factory.GetCreatedTextMeshDropdowns(dropdowns);
            for (int i = 0; i < dropdowns.Count; i++)
            {
                IUITextMeshDropdown dropdown = dropdowns[i];
                Component component = dropdown as Component;
                if (component == null)
                {
                    continue;
                }

                int index = i;
                items.Add(new ControlItem(
                    component.transform,
                    new DropdownItem(
                        "game-settings-dropdown-" + index,
                        () => GetDropdownLabel(dropdown),
                        () => GetDropdownOptions(dropdown),
                        () => GetDropdownValue(dropdown),
                        value => SetDropdownValue(dropdown, value),
                        () => NativeSelectionUtility.Select(dropdown.GetSelectable()),
                        () => dropdown.Active && dropdown.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(GetDropdownTooltipComponent(dropdown) ?? component, _localization))));
            }
        }

        private void AddToggleItems(List<ControlItem> items, IMenuFactoryCollection factory)
        {
            List<IUIToggle> toggles = new List<IUIToggle>();
            factory.GetCreatedToggles(toggles);
            List<IUIToggle> tinyToggles = new List<IUIToggle>();
            factory.GetCreatedTinyToggles(tinyToggles);
            toggles.AddRange(tinyToggles);

            for (int i = 0; i < toggles.Count; i++)
            {
                IUIToggle toggle = toggles[i];
                Component component = toggle as Component;
                if (component == null)
                {
                    continue;
                }

                int index = i;
                items.Add(new ControlItem(
                    component.transform,
                    new ToggleItem(
                        "game-settings-toggle-" + index,
                        () => GetToggleLabel(toggle),
                        () => toggle.ToggleValue = !toggle.ToggleValue,
                        () => toggle.ToggleValue,
                        () => NativeSelectionUtility.Select(toggle.GetSelectable()),
                        () => toggle.Active && toggle.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(GetToggleTooltipComponent(toggle) ?? component, _localization))));
            }
        }

        private void AddTextInputItems(List<ControlItem> items, IMenuFactoryCollection factory)
        {
            List<IUITextMeshInputField> fields = new List<IUITextMeshInputField>();
            factory.GetCreatedTextMeshInputFields(fields);
            for (int i = 0; i < fields.Count; i++)
            {
                IUITextMeshInputField field = fields[i];
                Component component = field as Component;
                if (component == null)
                {
                    continue;
                }

                int index = i;
                items.Add(new ControlItem(
                    component.transform,
                    new TextInputItem(
                        "game-settings-input-" + index,
                        () => GetInputLabel(field),
                        () => field,
                        () => NativeSelectionUtility.Select(field.GetSelectable()),
                        () => field.Active && field.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(GetInputTooltipComponent(field) ?? component, _localization))));
            }
        }

        private void AddTimeInputItems(List<ControlItem> items, IMenuFactoryCollection factory)
        {
            List<IUITimeInputField> fields = new List<IUITimeInputField>();
            factory.GetCreatedTimeInputFields(fields);
            for (int i = 0; i < fields.Count; i++)
            {
                IUITimeInputField field = fields[i];
                Component component = field as Component;
                if (component == null)
                {
                    continue;
                }

                int index = i;
                items.Add(new ControlItem(
                    component.transform,
                    new TimeInputItem(
                        "game-settings-time-input-" + index,
                        () => GetTextLabel(field),
                        () => field,
                        () => GetTimeInputChildField(field, TimeInputMinutesField),
                        () => GetTimeInputChildField(field, TimeInputSecondsField),
                        () => NativeSelectionUtility.Select(field.GetSelectable()),
                        () => field.Active && field.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(component, _localization))));
            }
        }

        private void AddButtonItems(List<ControlItem> items, IMenuFactoryCollection factory)
        {
            List<IUIButton> buttons = new List<IUIButton>();
            factory.GetCreatedButtons(buttons);
            for (int i = 0; i < buttons.Count; i++)
            {
                IUIButton button = buttons[i];
                Component component = button as Component;
                if (component == null)
                {
                    continue;
                }

                int index = i;
                items.Add(new ControlItem(
                    component.transform,
                    new ButtonItem(
                        "game-settings-content-button-" + index,
                        () => GetButtonLabel(button),
                        () => NativeSelectionUtility.Click(button),
                        () => NativeSelectionUtility.Select(component),
                        () => button.Active && button.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(component, _localization))));
            }
        }

        private ButtonItem BuildButton(string id, IUIButton button)
        {
            Component component = button as Component;
            return new ButtonItem(
                id,
                () => GetButtonLabel(button),
                () => NativeSelectionUtility.Click(button),
                () => NativeSelectionUtility.Select(component),
                () => button.Active && button.Interactable,
                () => IsActive(component),
                () => Tooltip.ForComponent(component, _localization));
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return _menu != null && field != null ? field.GetValue(_menu) as T : null;
        }

        private static string GetButtonLabel(IUIButton button)
        {
            UIButton concrete = button as UIButton;
            return concrete != null
                ? MenuButtonTextUtility.GetAllVisibleText(concrete)
                : SpeechTextSanitizer.Normalize(button != null ? button.Text : null);
        }

        private static string GetTextLabel(IUIText text)
        {
            return SpeechTextSanitizer.Normalize(text != null ? text.Text : null);
        }

        private static IUITextMeshInputField GetTimeInputChildField(IUITimeInputField field, FieldInfo childField)
        {
            UITimeInputField concrete = field as UITimeInputField;
            return concrete != null && childField != null
                ? childField.GetValue(concrete) as IUITextMeshInputField
                : null;
        }

        private static string GetInputLabel(IUITextMeshInputField field)
        {
            UITextMeshInputField concrete = field as UITextMeshInputField;
            if (concrete != null)
            {
                string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(concrete.GetTextMeshPro()));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }
            }

            return GetTextLabel(field);
        }

        private static Component GetInputTooltipComponent(IUITextMeshInputField field)
        {
            UITextMeshInputField concrete = field as UITextMeshInputField;
            return concrete != null ? concrete.GetTextMeshPro() as Component : null;
        }

        private static string GetToggleLabel(IUIToggle toggle)
        {
            UIToggle concrete = toggle as UIToggle;
            if (concrete != null)
            {
                string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(concrete.GetTextMesh()));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpeechTextSanitizer.Normalize(toggle != null ? toggle.Text : null);
        }

        private static Component GetToggleTooltipComponent(IUIToggle toggle)
        {
            UIToggle concrete = toggle as UIToggle;
            return concrete != null ? concrete.GetTextMesh() as Component : null;
        }

        private static string GetDropdownLabel(IUITextMeshDropdown dropdown)
        {
            UITextMeshDropdown concrete = dropdown as UITextMeshDropdown;
            if (concrete != null && DropdownGetTextMethod != null)
            {
                IUITextMesh textMesh = DropdownGetTextMethod.Invoke(concrete, new object[0]) as IUITextMesh;
                string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpeechTextSanitizer.Normalize(dropdown != null ? dropdown.Text : null);
        }

        private static Component GetDropdownTooltipComponent(IUITextMeshDropdown dropdown)
        {
            UITextMeshDropdown concrete = dropdown as UITextMeshDropdown;
            if (concrete == null || DropdownGetTextMethod == null)
            {
                return null;
            }

            return DropdownGetTextMethod.Invoke(concrete, new object[0]) as Component;
        }

        private static IReadOnlyList<string> GetDropdownOptions(IUITextMeshDropdown dropdown)
        {
            Component component = dropdown as Component;
            TMP_Dropdown tmpDropdown = component != null ? component.GetComponentInChildren<TMP_Dropdown>(true) : null;
            if (tmpDropdown == null || tmpDropdown.options == null)
            {
                return new string[0];
            }

            List<string> options = new List<string>();
            for (int i = 0; i < tmpDropdown.options.Count; i++)
            {
                options.Add(SpeechTextSanitizer.Normalize(tmpDropdown.options[i].text));
            }

            return options;
        }

        private static int GetDropdownValue(IUITextMeshDropdown dropdown)
        {
            if (dropdown == null)
            {
                return 0;
            }

            int count = dropdown.DropdownValueCount;
            if (count <= 0)
            {
                return 0;
            }

            int value = dropdown.DropdownValue;
            if (value < 0)
            {
                return 0;
            }

            return value >= count ? count - 1 : value;
        }

        private static bool SetDropdownValue(IUITextMeshDropdown dropdown, int value)
        {
            if (dropdown == null || !dropdown.Active || !dropdown.Interactable)
            {
                return false;
            }

            int count = dropdown.DropdownValueCount;
            if (count <= 0)
            {
                return false;
            }

            if (value < 0)
            {
                value = 0;
            }
            else if (value >= count)
            {
                value = count - 1;
            }

            dropdown.DropdownValue = value;
            return true;
        }

        private static bool IsActive(Component component)
        {
            return component != null && component.gameObject.activeInHierarchy;
        }

        private static int CompareControlItems(ControlItem left, ControlItem right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            return string.CompareOrdinal(BuildHierarchyKey(left.Transform), BuildHierarchyKey(right.Transform));
        }

        private static string BuildHierarchyKey(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<int> indices = new List<int>();
            Transform current = transform;
            while (current != null)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            indices.Reverse();
            string[] parts = new string[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                parts[i] = indices[i].ToString("D4");
            }

            return string.Join(".", parts);
        }

        internal sealed class ControlItem
        {
            public ControlItem(Transform transform, object item)
            {
                Transform = transform;
                Item = item;
            }

            public Transform Transform { get; private set; }
            public object Item { get; private set; }
        }

        internal sealed class TextItem
        {
            public TextItem(string id, Func<string> getText, Func<bool> isVisible)
            {
                Id = id;
                GetText = getText;
                IsVisible = isVisible;
            }

            public string Id { get; private set; }
            public Func<string> GetText { get; private set; }
            public Func<bool> IsVisible { get; private set; }
        }

        internal sealed class DropdownItem
        {
            public DropdownItem(string id, Func<string> getLabel, Func<IReadOnlyList<string>> getOptions, Func<int> getValue, Func<int, bool> setValue, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
            {
                Id = id;
                GetLabel = getLabel;
                GetOptions = getOptions;
                GetValue = getValue;
                SetValue = setValue;
                Focus = focus;
                IsEnabled = isEnabled;
                IsVisible = isVisible;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<IReadOnlyList<string>> GetOptions { get; private set; }
            public Func<int> GetValue { get; private set; }
            public Func<int, bool> SetValue { get; private set; }
            public Action Focus { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<Tooltip> GetTooltip { get; private set; }
        }

        internal sealed class ToggleItem
        {
            public ToggleItem(string id, Func<string> getLabel, Action toggle, Func<bool> isChecked, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
            {
                Id = id;
                GetLabel = getLabel;
                Toggle = toggle;
                IsChecked = isChecked;
                Focus = focus;
                IsEnabled = isEnabled;
                IsVisible = isVisible;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Action Toggle { get; private set; }
            public Func<bool> IsChecked { get; private set; }
            public Action Focus { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<Tooltip> GetTooltip { get; private set; }
        }

        internal sealed class TextInputItem
        {
            public TextInputItem(string id, Func<string> getLabel, Func<IUITextMeshInputField> getField, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
            {
                Id = id;
                GetLabel = getLabel;
                GetField = getField;
                Focus = focus;
                IsEnabled = isEnabled;
                IsVisible = isVisible;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<IUITextMeshInputField> GetField { get; private set; }
            public Action Focus { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<Tooltip> GetTooltip { get; private set; }
        }

        internal sealed class TimeInputItem
        {
            public TimeInputItem(
                string id,
                Func<string> getLabel,
                Func<IUITimeInputField> getField,
                Func<IUITextMeshInputField> getMinutesField,
                Func<IUITextMeshInputField> getSecondsField,
                Action focus,
                Func<bool> isEnabled,
                Func<bool> isVisible,
                Func<Tooltip> getTooltip)
            {
                Id = id;
                GetLabel = getLabel;
                GetField = getField;
                GetMinutesField = getMinutesField;
                GetSecondsField = getSecondsField;
                Focus = focus;
                IsEnabled = isEnabled;
                IsVisible = isVisible;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<IUITimeInputField> GetField { get; private set; }
            public Func<IUITextMeshInputField> GetMinutesField { get; private set; }
            public Func<IUITextMeshInputField> GetSecondsField { get; private set; }
            public Action Focus { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<Tooltip> GetTooltip { get; private set; }
        }

        internal sealed class ButtonItem
        {
            public ButtonItem(string id, Func<string> getLabel, Func<bool> activate, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
            {
                Id = id;
                GetLabel = getLabel;
                Activate = activate;
                Focus = focus;
                IsEnabled = isEnabled;
                IsVisible = isVisible;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Action Focus { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<Tooltip> GetTooltip { get; private set; }
        }
    }
}
