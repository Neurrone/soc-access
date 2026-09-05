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
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class AdventureLobbyPlayerSettingsAdapter
    {
        private static readonly FieldInfo SettingsContainerField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_settingsContainer");
        private static readonly FieldInfo FactoryField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_factory");
        private static readonly FieldInfo CancelButtonField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_cancelButton");
        private static readonly FieldInfo ConfirmButtonField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_confirmButton");
        private static readonly FieldInfo LocalizationField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_localizationHandler");
        private static readonly MethodInfo SliderGetTextMeshMethod =
            AccessTools.Method(typeof(UISlider), "GetTextMesh");

        private readonly LobbyPlayerSettingsMenu _menu;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyPlayerSettingsAdapter(LobbyPlayerSettingsMenu menu)
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
                    GameText.Get(_localization, "Lobby/PlayerSettingsMenu/Header", string.Empty));
            }
        }

        public bool IsPresent()
        {
            UITransform container = GetField<UITransform>(SettingsContainerField);
            GameObject gameObject = _menu != null ? ((Component)_menu).gameObject : null;
            return _menu != null
                && gameObject != null
                && gameObject.activeInHierarchy
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
            AddToggleItems(items, factory);
            AddSliderItems(items, factory);
            AddButtonItems(items, factory);
            items.Sort(CompareControlItems);
            return items;
        }

        public ButtonItem GetCancelButton()
        {
            UIButton button = GetField<UIButton>(CancelButtonField);
            return button != null
                ? BuildButton("player-settings-cancel", button)
                : null;
        }

        public ButtonItem GetConfirmButton()
        {
            UIButton button = GetField<UIButton>(ConfirmButtonField);
            return button != null
                ? BuildButton("player-settings-confirm", button)
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
                        "player-settings-text-" + index,
                        () => SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text)),
                        () => IsActive(component))));
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
                        "player-settings-toggle-" + index,
                        () => GetToggleLabel(toggle),
                        () => toggle.ToggleValue = !toggle.ToggleValue,
                        () => toggle.ToggleValue,
                        () => NativeSelectionUtility.Select(toggle.GetSelectable()),
                        () => toggle.Active && toggle.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(GetToggleTooltipComponent(toggle) ?? component, _localization))));
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
                        "player-settings-slider-" + index,
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
                        () => Tooltip.ForComponent(GetSliderTooltipComponent(slider) ?? component, _localization))));
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
                        "player-settings-content-button-" + index,
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
            if (slider == null || Math.Abs(slider.ValueMultiplier) < 0.0001f)
            {
                return 1f;
            }

            return slider.ValueMultiplier;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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

        public sealed class SliderItem
        {
            public SliderItem(string id, Func<string> getLabel, Func<string> getValueText, Func<float> getValue, Func<float> getMinimumValue, Func<float> getMaximumValue, Func<float> getStep, Func<float, bool> setValue, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
            {
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
    }
}
