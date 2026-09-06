using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class OptionsMenuAdapter
    {
        private static readonly FieldInfo FactoryField = AccessTools.Field(typeof(OptionsMenu), "_factory");
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(OptionsMenu), "_settings");
        private static readonly FieldInfo TabsField = AccessTools.Field(typeof(OptionsMenu), "_tabs");
        private static readonly FieldInfo ContentTabsField = AccessTools.Field(typeof(OptionsMenu), "_contentTabs");
        private static readonly FieldInfo CurrentContentField = AccessTools.Field(typeof(OptionsMenu), "_currentContent");
        private static readonly MethodInfo SliderGetTextMeshMethod = AccessTools.Method(typeof(UISlider), "GetTextMesh");
        private static readonly MethodInfo DropdownGetTextMethod = AccessTools.Method(typeof(UITextMeshDropdown), "GetText");
        private static readonly FieldInfo SliderEditButtonField = AccessTools.Field(typeof(UISlider), "_editValueButton");

        private readonly OptionsMenu _menu;

        public OptionsMenuAdapter(OptionsMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public bool IsPresent()
        {
            OptionsMenu.Settings settings = Settings;
            return _menu != null
                && settings != null
                && settings.parent != null
                && settings.parent.Active;
        }

        public IReadOnlyList<TabItem> GetTabs()
        {
            List<TabItem> result = new List<TabItem>();
            List<UIButton> tabs = GetField<List<UIButton>>(_menu, TabsField);
            if (tabs == null)
            {
                return result;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                UIButton button = tabs[i];
                if (button == null)
                {
                    continue;
                }

                int index = i;
                result.Add(new TabItem(
                    "options-tab-" + index,
                    () => GetButtonLabel(button),
                    () => SelectTab(index),
                    () => IsActive(button as Component)));
            }

            return result;
        }

        public int GetActiveTabIndex()
        {
            List<IOptionsContent> contentTabs = GetField<List<IOptionsContent>>(_menu, ContentTabsField);
            IOptionsContent current = GetField<IOptionsContent>(_menu, CurrentContentField);
            if (contentTabs == null || current == null)
            {
                return 0;
            }

            int index = contentTabs.IndexOf(current);
            return index >= 0 ? index : 0;
        }

        public bool SelectTab(int index)
        {
            List<UIButton> tabs = GetField<List<UIButton>>(_menu, TabsField);
            if (tabs == null || index < 0 || index >= tabs.Count)
            {
                return false;
            }

            UIButton button = tabs[index];
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        public IReadOnlyList<ControlItem> GetCurrentContentControls()
        {
            IMenuFactoryCollection factory = Factory;
            OptionsMenu.Settings settings = Settings;
            if (factory == null || settings == null || settings.contentParent == null)
            {
                return new ControlItem[0];
            }

            List<ControlItem> items = new List<ControlItem>();
            AddTextItems(items, factory);
            AddDropdownItems(items, factory);
            AddToggleItems(items, factory);
            AddSliderItems(items, factory);
            AddButtonItems(items, factory);
            items.Sort(CompareControlItems);
            return items;
        }

        public ButtonItem GetOkButton()
        {
            OptionsMenu.Settings settings = Settings;
            UIButton button = settings != null ? settings.okButton : null;
            if (button == null)
            {
                return null;
            }

            return new ButtonItem(
                "options-ok",
                () => GetButtonLabel(button),
                () => NativeSelectionUtility.Click(button),
                () => NativeSelectionUtility.Select(button as Component),
                () => button.Active && button.Interactable,
                () => IsActive(button as Component),
                () => Tooltip.ForComponent(button as Component, null));
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Close();
            return true;
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
                        "options-text-" + index,
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
                            "options-dropdown-" + index,
                        () => GetDropdownLabel(dropdown),
                        () => GetDropdownOptions(dropdown),
                        () => GetDropdownValue(dropdown),
                        value => SetDropdownValue(dropdown, value),
                        () => NativeSelectionUtility.Select(dropdown.GetSelectable()),
                        () => dropdown.Active && dropdown.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(GetDropdownTooltipComponent(dropdown) ?? component, null),
                        () => DropdownPopup.Show(dropdown),
                        () => DropdownPopup.Hide(dropdown),
                        () => DropdownPopup.IsOpen(dropdown),
                        optionIndex => DropdownPopup.FocusOption(dropdown, optionIndex))));
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
                            "options-toggle-" + index,
                        () => GetToggleLabel(toggle),
                        () => toggle.ToggleValue = !toggle.ToggleValue,
                        () => toggle.ToggleValue,
                        () => NativeSelectionUtility.Select(toggle.GetSelectable()),
                        () => toggle.Active && toggle.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(GetToggleTooltipComponent(toggle) ?? component, null))));
            }
        }

        private void AddSliderItems(List<ControlItem> items, IMenuFactoryCollection factory)
        {
            List<IUISlider> sliders = new List<IUISlider>();
            factory.GetCreatedSliders(sliders);
            for (int i = 0; i < sliders.Count; i++)
            {
                IUISlider slider = sliders[i];
                Component component = slider as Component;
                if (component == null)
                {
                    continue;
                }

                int index = i;
                items.Add(new ControlItem(
                        component.transform,
                        new SliderItem(
                            "options-slider-" + index,
                        () => GetSliderLabel(slider),
                        () => FormatSliderValue(slider),
                        () => slider.SliderValue,
                        () => GetEffectiveSliderMinimum(slider),
                        () => GetEffectiveSliderMaximum(slider),
                        () => GetSliderStep(slider),
                        value => SetSliderValue(slider, value),
                        () => NativeSelectionUtility.Select(slider.GetSelectable()),
                        () => slider.Active && slider.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(GetSliderTooltipComponent(slider) ?? component, null),
                        () => GetSliderValueEditorLabel(slider),
                        () => OpenSliderValueEditor(slider))));
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
                        "options-button-" + index,
                        () => GetButtonLabel(button),
                        () => NativeSelectionUtility.Click(button),
                        () => NativeSelectionUtility.Select(component),
                        () => button.Active && button.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(component, null))));
            }
        }

        private IMenuFactoryCollection Factory
        {
            get { return GetField<IMenuFactoryCollection>(_menu, FactoryField); }
        }

        private OptionsMenu.Settings Settings
        {
            get { return GetField<OptionsMenu.Settings>(_menu, SettingsField); }
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        private static string GetButtonLabel(IUIButton button)
        {
            UIButton concrete = button as UIButton;
            string label = concrete != null
                ? MenuButtonTextUtility.GetAllVisibleText(concrete)
                : SpeechTextSanitizer.Normalize(button != null ? button.Text : null);
            return label;
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

        private static string GetSliderLabel(IUISlider slider)
        {
            UISlider concrete = slider as UISlider;
            if (concrete != null && SliderGetTextMeshMethod != null)
            {
                IUITextMesh textMesh = SliderGetTextMeshMethod.Invoke(concrete, new object[0]) as IUITextMesh;
                string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpeechTextSanitizer.Normalize(slider != null ? slider.Text : null);
        }

        private static Component GetSliderTooltipComponent(IUISlider slider)
        {
            UISlider concrete = slider as UISlider;
            if (concrete == null || SliderGetTextMeshMethod == null)
            {
                return null;
            }

            return SliderGetTextMeshMethod.Invoke(concrete, new object[0]) as Component;
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

        private static bool IsActive(Component component)
        {
            return component != null && component.gameObject.activeInHierarchy;
        }

        private static UIButton GetSliderEditButton(IUISlider slider)
        {
            UISlider concrete = slider as UISlider;
            return concrete != null && SliderEditButtonField != null
                ? SliderEditButtonField.GetValue(concrete) as UIButton
                : null;
        }

        /// <summary>What the game calls the popup its own value box opens ("Provide a number",
        /// <c>UISlider.HandleTextClicked</c>), or empty where this slider draws no such box.</summary>
        private static string GetSliderValueEditorLabel(IUISlider slider)
        {
            return GetSliderEditButton(slider) == null
                ? string.Empty
                : GameText.Get("Common/ProvideNumber", string.Empty);
        }

        /// <summary>
        /// Open the game's own "Provide a number" popup for this slider.
        ///
        /// The native path is the delegate the slider itself installed:
        /// <c>UISlider.OnEnable</c> adds <c>HandleTextClicked</c> to the value box's
        /// <c>OnClickedInside</c>, which <c>UITransform.Update</c> raises from a real mouse press
        /// landing inside the box - NOT from <c>OnPointerClick</c>, so a synthesized pointer click
        /// reaches the button's empty <c>OnClicked</c> and nothing happens. Running the installed
        /// delegate is that same handler minus the mouse; the handler's own guard on the slider being
        /// interactable, and the popup it raises, are the game's.
        /// </summary>
        private static bool OpenSliderValueEditor(IUISlider slider)
        {
            UIButton button = GetSliderEditButton(slider);
            Action<Vector2> clicked = button != null ? button.OnClickedInside : null;
            if (clicked == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            clicked(Vector2.zero);
            return true;
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

            int value = dropdown.DropdownValue;
            int count = dropdown.DropdownValueCount;
            if (count <= 0)
            {
                return 0;
            }

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

        private static string FormatSliderValue(IUISlider slider)
        {
            if (slider == null)
            {
                return string.Empty;
            }

            float value = slider.SliderValue;
            if (slider.DrawAsPercent)
            {
                return Math.Round(value * 100f) + "%";
            }

            return FormatFloat(value);
        }

        private static float GetSliderStep(IUISlider slider)
        {
            if (slider == null)
            {
                return 1f;
            }

            if (slider.UseWholeNumbers)
            {
                return GetSliderValueMultiplier(slider);
            }

            if (slider.NearestDecimal > 0f)
            {
                return slider.NearestDecimal;
            }

            float range = GetEffectiveSliderMaximum(slider) - GetEffectiveSliderMinimum(slider);
            return range > 0f ? range / 100f : 1f;
        }

        private static float GetEffectiveSliderMinimum(IUISlider slider)
        {
            if (slider == null)
            {
                return 0f;
            }

            return slider.SliderMinValue * GetSliderValueMultiplier(slider);
        }

        private static float GetEffectiveSliderMaximum(IUISlider slider)
        {
            if (slider == null)
            {
                return 0f;
            }

            return slider.SliderMaxValue * GetSliderValueMultiplier(slider);
        }

        private static bool SetSliderValue(IUISlider slider, float value)
        {
            if (slider == null || !slider.Active || !slider.Interactable)
            {
                return false;
            }

            float minimum = GetEffectiveSliderMinimum(slider);
            float maximum = GetEffectiveSliderMaximum(slider);
            if (value < minimum)
            {
                value = minimum;
            }
            else if (value > maximum)
            {
                value = maximum;
            }

            slider.SetSliderValue(value / GetSliderValueMultiplier(slider), sendNotify: true);
            return true;
        }

        private static float GetSliderValueMultiplier(IUISlider slider)
        {
            if (slider == null || slider.ValueMultiplier == 0f)
            {
                return 1f;
            }

            return slider.ValueMultiplier;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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

        public sealed class ControlItem
        {
            public ControlItem(Transform transform, object item)
            {
                Transform = transform;
                Item = item;
            }

            public Transform Transform { get; private set; }
            public object Item { get; private set; }
        }

        public sealed class TabItem
        {
            public TabItem(string id, Func<string> getLabel, Func<bool> select, Func<bool> isVisible)
            {
                Id = id;
                GetLabel = getLabel;
                Select = select;
                IsVisible = isVisible;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<bool> Select { get; private set; }
            public Func<bool> IsVisible { get; private set; }
        }

        public sealed class TextItem
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

        public sealed class ButtonItem
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

        public sealed class ToggleItem
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

        public sealed class DropdownItem : IDropList
        {
            public DropdownItem(string id, Func<string> getLabel, Func<IReadOnlyList<string>> getOptions, Func<int> getValue, Func<int, bool> setValue, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip, Func<bool> openPopup, Func<bool> closePopup, Func<bool> isPopupOpen, Func<int, bool> focusOption)
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
                OpenPopup = openPopup;
                ClosePopup = closePopup;
                IsPopupOpen = isPopupOpen;
                FocusOption = focusOption;
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

            /// <summary>Open the game's own list popup, close it, ask whether it is open, and put the
            /// game's highlight on one of its entries.</summary>
            public Func<bool> OpenPopup { get; private set; }
            public Func<bool> ClosePopup { get; private set; }
            public Func<bool> IsPopupOpen { get; private set; }
            public Func<int, bool> FocusOption { get; private set; }
        }

        public sealed class SliderItem
        {
            public SliderItem(string id, Func<string> getLabel, Func<string> getValueText, Func<float> getValue, Func<float> getMinimumValue, Func<float> getMaximumValue, Func<float> getStep, Func<float, bool> setValue, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip, Func<string> getValueEditorLabel, Func<bool> openValueEditor)
            {
                GetValueEditorLabel = getValueEditorLabel;
                OpenValueEditor = openValueEditor;
                Id = id;
                GetLabel = getLabel;
                GetValueText = getValueText;
                GetValue = getValue;
                GetMinimumValue = getMinimumValue;
                GetMaximumValue = getMaximumValue;
                GetStep = getStep;
                SetValue = setValue;
                Focus = focus;
                IsEnabled = isEnabled;
                IsVisible = isVisible;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<string> GetValueText { get; private set; }
            public Func<float> GetValue { get; private set; }
            public Func<float> GetMinimumValue { get; private set; }
            public Func<float> GetMaximumValue { get; private set; }
            public Func<float> GetStep { get; private set; }
            public Func<float, bool> SetValue { get; private set; }
            public Action Focus { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Func<Tooltip> GetTooltip { get; private set; }

            /// <summary>What the game calls the popup the slider's own value box opens, empty where
            /// the slider draws no such box; and the native open itself.</summary>
            public Func<string> GetValueEditorLabel { get; private set; }
            public Func<bool> OpenValueEditor { get; private set; }
        }
    }
}
