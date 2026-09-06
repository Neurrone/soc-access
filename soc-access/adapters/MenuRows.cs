using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE ROWS A <see cref="MenuFactoryController"/> HAS DRAWN, read as facts.
    ///
    /// The game builds every settings form the same way: a controller draws captions, toggles,
    /// sliders, dropdowns, input fields and buttons into one content column, and the
    /// <see cref="IMenuFactoryCollection"/> behind it remembers what it made. So the reader is the
    /// collection, not the column: <c>GetCreatedToggles</c> and its kin answer with exactly the
    /// controls this form drew and nothing that belongs to a control's own insides, which walking
    /// the transforms could not tell apart (a toggle's label is a text mesh too).
    ///
    /// It was `OptionsMenuAdapter`'s until the mod grew a form of its own. The mod's options dialog
    /// draws its rows with the game's controller over a collection it builds from the game's own
    /// factory settings, so the same reader reads the mod's rows and the game's, and the mod's
    /// dialog is navigated by the code that navigates Options.
    ///
    /// Rows come back in the order they are DRAWN - sorted by the sibling path of each control's
    /// transform - because a collection lists each kind together and a form is read top to bottom.
    /// </summary>
    public static class MenuRows
    {
        /// <summary>Everything the collection has drawn, in drawn order.</summary>
        public static IReadOnlyList<MenuRow> Read(IMenuFactoryCollection factory)
        {
            if (factory == null)
            {
                return new MenuRow[0];
            }

            List<MenuRow> items = new List<MenuRow>();
            AddTexts(items, factory);
            AddInputs(items, factory);
            AddDropdowns(items, factory);
            AddToggles(items, factory);
            AddSliders(items, factory);
            AddButtons(items, factory);
            items.Sort(Compare);
            return items;
        }

        /// <summary>One button read on its own - the window's own control rather than a drawn row.
        /// </summary>
        public static MenuRowButton Button(string id, UIButton button)
        {
            if (button == null)
            {
                return null;
            }

            return new MenuRowButton(
                id,
                () => Label(button),
                () => NativeSelectionUtility.Click(button),
                () => NativeSelectionUtility.Select(button),
                () => button.Active && button.Interactable,
                () => IsActive(button),
                () => Tooltip.ForComponent(button, null));
        }

        private static void AddTexts(List<MenuRow> items, IMenuFactoryCollection factory)
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

                items.Add(new MenuRow(
                    component.transform,
                    new MenuRowText(
                        "options-text-" + i,
                        () => SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text)),
                        () => IsActive(component))));
            }
        }

        private static void AddInputs(List<MenuRow> items, IMenuFactoryCollection factory)
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

                items.Add(new MenuRow(
                    component.transform,
                    new MenuRowInput(
                        "options-input-" + i,
                        () => InputLabel(field),
                        () => field,
                        () => field.Active && field.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(InputTextMesh(field) ?? component, null))));
            }
        }

        private static void AddDropdowns(List<MenuRow> items, IMenuFactoryCollection factory)
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

                items.Add(new MenuRow(
                    component.transform,
                    new MenuRowDropdown(
                        "options-dropdown-" + i,
                        () => DropdownLabel(dropdown),
                        () => DropdownOptions(dropdown),
                        () => DropdownValue(dropdown),
                        value => SetDropdownValue(dropdown, value),
                        () => NativeSelectionUtility.Select(dropdown.GetSelectable()),
                        () => dropdown.Active && dropdown.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(DropdownTextMesh(dropdown) ?? component, null),
                        () => DropdownPopup.Show(dropdown),
                        () => DropdownPopup.Hide(dropdown),
                        () => DropdownPopup.IsOpen(dropdown),
                        optionIndex => DropdownPopup.FocusOption(dropdown, optionIndex))));
            }
        }

        private static void AddToggles(List<MenuRow> items, IMenuFactoryCollection factory)
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

                items.Add(new MenuRow(
                    component.transform,
                    new MenuRowToggle(
                        "options-toggle-" + i,
                        () => ToggleLabel(toggle),
                        () => toggle.ToggleValue = !toggle.ToggleValue,
                        () => toggle.ToggleValue,
                        () => NativeSelectionUtility.Select(toggle.GetSelectable()),
                        () => toggle.Active && toggle.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(ToggleTextMesh(toggle) ?? component, null))));
            }
        }

        private static void AddSliders(List<MenuRow> items, IMenuFactoryCollection factory)
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

                items.Add(new MenuRow(
                    component.transform,
                    new MenuRowSlider(
                        "options-slider-" + i,
                        () => SliderLabel(slider),
                        () => SliderValueText(slider),
                        () => slider.SliderValue,
                        () => SliderMinimum(slider),
                        () => SliderMaximum(slider),
                        () => SliderStep(slider),
                        value => SetSliderValue(slider, value),
                        () => NativeSelectionUtility.Select(slider.GetSelectable()),
                        () => slider.Active && slider.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(SliderTextMesh(slider) ?? component, null),
                        () => SliderValueEditor.Label(slider),
                        () => SliderValueEditor.Open(slider))));
            }
        }

        private static void AddButtons(List<MenuRow> items, IMenuFactoryCollection factory)
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

                items.Add(new MenuRow(
                    component.transform,
                    new MenuRowButton(
                        "options-button-" + i,
                        () => Label(button),
                        () => NativeSelectionUtility.Click(button),
                        () => NativeSelectionUtility.Select(component),
                        () => button.Active && button.Interactable,
                        () => IsActive(component),
                        () => Tooltip.ForComponent(component, null))));
            }
        }

        // ---- reading one control ----

        public static string Label(IUIButton button)
        {
            UIButton concrete = button as UIButton;
            return concrete != null
                ? MenuButtonTextUtility.GetAllVisibleText(concrete)
                : SpeechTextSanitizer.Normalize(button != null ? button.Text : null);
        }

        private static string ToggleLabel(IUIToggle toggle)
        {
            UIToggle concrete = toggle as UIToggle;
            if (concrete != null)
            {
                string text = SpeechTextSanitizer.Normalize(
                    UITextMeshTextUtility.GetEffectiveText(concrete.GetTextMesh()));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpeechTextSanitizer.Normalize(toggle != null ? toggle.Text : null);
        }

        private static Component ToggleTextMesh(IUIToggle toggle)
        {
            UIToggle concrete = toggle as UIToggle;
            return concrete != null ? concrete.GetTextMesh() as Component : null;
        }

        private static string SliderLabel(IUISlider slider)
        {
            IUITextMesh textMesh = SliderText.Of(slider);
            if (textMesh != null)
            {
                string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpeechTextSanitizer.Normalize(slider != null ? slider.Text : null);
        }

        private static Component SliderTextMesh(IUISlider slider)
        {
            return SliderText.Of(slider) as Component;
        }

        private static string InputLabel(IUITextMeshInputField field)
        {
            IUITextMesh textMesh = InputTextMesh(field) as IUITextMesh;
            if (textMesh != null)
            {
                string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpeechTextSanitizer.Normalize(field != null ? field.Text : null);
        }

        /// <summary>An input field's own label mesh - the field draws its caption beside the box, and
        /// <c>GetTextMeshPro</c> answers with that rather than with the box's text.</summary>
        private static Component InputTextMesh(IUITextMeshInputField field)
        {
            UITextMeshInputField concrete = field as UITextMeshInputField;
            return concrete != null ? concrete.GetTextMeshPro() : null;
        }

        private static string DropdownLabel(IUITextMeshDropdown dropdown)
        {
            IUITextMesh textMesh = DropdownText.Of(dropdown);
            if (textMesh != null)
            {
                string text = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpeechTextSanitizer.Normalize(dropdown != null ? dropdown.Text : null);
        }

        private static Component DropdownTextMesh(IUITextMeshDropdown dropdown)
        {
            return DropdownText.Of(dropdown) as Component;
        }

        private static bool IsActive(Component component)
        {
            return component != null && component.gameObject.activeInHierarchy;
        }

        private static IReadOnlyList<string> DropdownOptions(IUITextMeshDropdown dropdown)
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

        private static int DropdownValue(IUITextMeshDropdown dropdown)
        {
            if (dropdown == null)
            {
                return 0;
            }

            int value = dropdown.DropdownValue;
            int count = dropdown.DropdownValueCount;
            if (count <= 0 || value < 0)
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

        private static string SliderValueText(IUISlider slider)
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

            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static float SliderStep(IUISlider slider)
        {
            if (slider == null)
            {
                return 1f;
            }

            if (slider.UseWholeNumbers)
            {
                return SliderMultiplier(slider);
            }

            if (slider.NearestDecimal > 0f)
            {
                return slider.NearestDecimal;
            }

            float range = SliderMaximum(slider) - SliderMinimum(slider);
            return range > 0f ? range / 100f : 1f;
        }

        private static float SliderMinimum(IUISlider slider)
        {
            return slider == null ? 0f : slider.SliderMinValue * SliderMultiplier(slider);
        }

        private static float SliderMaximum(IUISlider slider)
        {
            return slider == null ? 0f : slider.SliderMaxValue * SliderMultiplier(slider);
        }

        private static bool SetSliderValue(IUISlider slider, float value)
        {
            if (slider == null || !slider.Active || !slider.Interactable)
            {
                return false;
            }

            float minimum = SliderMinimum(slider);
            float maximum = SliderMaximum(slider);
            if (value < minimum)
            {
                value = minimum;
            }
            else if (value > maximum)
            {
                value = maximum;
            }

            slider.SetSliderValue(value / SliderMultiplier(slider), sendNotify: true);
            return true;
        }

        private static float SliderMultiplier(IUISlider slider)
        {
            return slider == null || slider.ValueMultiplier == 0f ? 1f : slider.ValueMultiplier;
        }

        private static int Compare(MenuRow left, MenuRow right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            return string.CompareOrdinal(HierarchyKey(left.Transform), HierarchyKey(right.Transform));
        }

        private static string HierarchyKey(Transform transform)
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

        /// <summary>A slider's own label mesh, which the game keeps private.</summary>
        private static class SliderText
        {
            private static readonly System.Reflection.MethodInfo Method =
                AccessTools.Method(typeof(UISlider), "GetTextMesh");

            public static IUITextMesh Of(IUISlider slider)
            {
                UISlider concrete = slider as UISlider;
                return concrete != null && Method != null
                    ? Method.Invoke(concrete, new object[0]) as IUITextMesh
                    : null;
            }
        }

        /// <summary>A dropdown's own label mesh, likewise.</summary>
        private static class DropdownText
        {
            private static readonly System.Reflection.MethodInfo Method =
                AccessTools.Method(typeof(UITextMeshDropdown), "GetText");

            public static IUITextMesh Of(IUITextMeshDropdown dropdown)
            {
                UITextMeshDropdown concrete = dropdown as UITextMeshDropdown;
                return concrete != null && Method != null
                    ? Method.Invoke(concrete, new object[0]) as IUITextMesh
                    : null;
            }
        }
    }

    /// <summary>One drawn row: what it is, and where it is drawn.</summary>
    public sealed class MenuRow
    {
        public MenuRow(Transform transform, object item)
        {
            Transform = transform;
            Item = item;
        }

        public Transform Transform { get; private set; }
        public object Item { get; private set; }
    }

    /// <summary>A caption, or any text the form draws on a line of its own.</summary>
    public sealed class MenuRowText
    {
        public MenuRowText(string id, Func<string> getText, Func<bool> isVisible)
        {
            Id = id;
            GetText = getText;
            IsVisible = isVisible;
        }

        public string Id { get; private set; }
        public Func<string> GetText { get; private set; }
        public Func<bool> IsVisible { get; private set; }
    }

    public sealed class MenuRowButton
    {
        public MenuRowButton(string id, Func<string> getLabel, Func<bool> activate, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
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

    /// <summary>A text box the form draws. The field itself is handed over, because taking the
    /// keyboard is the game's own affair and the mod's editor drives it directly.</summary>
    public sealed class MenuRowInput
    {
        public MenuRowInput(string id, Func<string> getLabel, Func<IUITextMeshInputField> getField, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
        {
            Id = id;
            GetLabel = getLabel;
            GetField = getField;
            IsEnabled = isEnabled;
            IsVisible = isVisible;
            GetTooltip = getTooltip;
        }

        public string Id { get; private set; }
        public Func<string> GetLabel { get; private set; }
        public Func<IUITextMeshInputField> GetField { get; private set; }
        public Func<bool> IsEnabled { get; private set; }
        public Func<bool> IsVisible { get; private set; }
        public Func<Tooltip> GetTooltip { get; private set; }
    }

    public sealed class MenuRowToggle
    {
        public MenuRowToggle(string id, Func<string> getLabel, Action toggle, Func<bool> isChecked, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip)
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

    public sealed class MenuRowDropdown : IDropList
    {
        public MenuRowDropdown(string id, Func<string> getLabel, Func<IReadOnlyList<string>> getOptions, Func<int> getValue, Func<int, bool> setValue, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip, Func<bool> openPopup, Func<bool> closePopup, Func<bool> isPopupOpen, Func<int, bool> focusOption)
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

    public sealed class MenuRowSlider
    {
        public MenuRowSlider(string id, Func<string> getLabel, Func<string> getValueText, Func<float> getValue, Func<float> getMinimumValue, Func<float> getMaximumValue, Func<float> getStep, Func<float, bool> setValue, Action focus, Func<bool> isEnabled, Func<bool> isVisible, Func<Tooltip> getTooltip, Func<string> getValueEditorLabel, Func<bool> openValueEditor)
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

        /// <summary>What the game calls the popup the slider's own value box opens, empty where the
        /// slider draws no such box; and the native open itself.</summary>
        public Func<string> GetValueEditorLabel { get; private set; }
        public Func<bool> OpenValueEditor { get; private set; }
    }
}
