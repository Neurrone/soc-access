using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
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
            return Read(factory, null);
        }

        /// <summary>Everything the collection has drawn, in drawn order.
        /// <paramref name="keyBindings"/> is the rebindable-action context (the Options window's
        /// Controls page); null for a form that draws none, such as the mod's own options dialog.
        /// </summary>
        public static IReadOnlyList<MenuRow> Read(IMenuFactoryCollection factory, KeyBindingSource keyBindings)
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
            AddKeyBindings(items, factory, keyBindings);
            SortByHierarchy(items);
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
                        () => SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text)),
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

        // ---- the rebindable-action rows ----

        // The controls page draws one <see cref="UIKeyBinding"/> per rebindable action through
        // AddKeyBinding, which the other GetCreated* readers never see. Each row has exactly one chip
        // (the current hotkey) plus its "+" button; the fields the game keeps private are resolved
        // once here, not per row.
        private static readonly FieldInfo ButtonsField = AccessTools.Field(typeof(UIKeyBinding), "_buttons");
        private static readonly FieldInfo PlusButtonField = AccessTools.Field(typeof(UIKeyBinding), "_plusButton");
        private static readonly FieldInfo LabelMeshField = AccessTools.Field(typeof(UIKeyBinding), "_textMesh");
        private static readonly FieldInfo EntryButtonField = AccessTools.Field(typeof(UIKeyBindingEntry), "_button");
        private static readonly FieldInfo EntryTextField = AccessTools.Field(typeof(UIKeyBindingEntry), "_text");

        private static void AddKeyBindings(List<MenuRow> items, IMenuFactoryCollection factory, KeyBindingSource source)
        {
            List<IUIKeyBinding> bindings = new List<IUIKeyBinding>();
            factory.GetCreatedKeyBindings(bindings);
            for (int i = 0; i < bindings.Count; i++)
            {
                IUIKeyBinding widget = bindings[i];
                Component component = widget as Component;
                if (component == null)
                {
                    continue;
                }

                items.Add(new MenuRow(component.transform, MakeKeyBinding("options-keybind-" + i, widget, source)));
            }
        }

        private static MenuRowKeyBinding MakeKeyBinding(string id, IUIKeyBinding widget, KeyBindingSource source)
        {
            Component component = widget as Component;
            // The game draws the override chip as an INTERACTABLE button and the default chip as a
            // dead one (AddDefaultBinding sets its Button non-interactable), so the chip itself is the
            // cheap, authoritative override signal - no per-row query into the input manager on the
            // build path.
            Func<bool> hasOverride = () =>
            {
                UIButton chip = ChipButton(widget);
                return chip != null && chip.Interactable;
            };

            // The label is set once when the row is drawn and the row record lives as long as the
            // drawn column, so it is read once: the sheet asks for it on every vertical edge it wires
            // (four times per row per build), and the tag strip behind it was 0.9 ms of the Controls
            // page's build (2026-09-11).
            string actionName = null;
            return new MenuRowKeyBinding(
                id,
                () => actionName ?? (actionName = ActionText(widget)),
                () =>
                {
                    // The chip carries the exact drawn text - modifiers included for an override; the
                    // container is the fallback where the chip cannot be read.
                    string chip = ChipText(widget);
                    if (!string.IsNullOrWhiteSpace(chip))
                    {
                        return chip;
                    }

                    BindingContainer container = source != null && source.Resolve != null ? source.Resolve(widget) : null;
                    return container != null ? (container.currentOverride ?? container.defaultBinding) : null;
                },
                hasOverride,
                () =>
                {
                    // Resolved BEFORE the click: starting the game's capture redraws the whole
                    // page inside the click, so afterwards this widget is no longer the drawn one
                    // and nothing maps it to its action any more.
                    BindingContainer container = source != null && source.Resolve != null ? source.Resolve(widget) : null;
                    bool clicked = NativeSelectionUtility.Click(PlusButton(widget));
                    if (clicked && source != null)
                    {
                        source.LastRebindWidget = widget;
                        source.LastRebindAction = container != null ? container.action : (ActionReference?)null;
                    }

                    return clicked;
                },
                () => hasOverride() && NativeSelectionUtility.Click(ChipButton(widget)),
                () => KeyCaptureFocus.IsCapturing() && source != null && ReferenceEquals(source.LastRebindWidget, widget),
                () => component != null && component.gameObject.activeInHierarchy,
                () => FocusRow(widget, component, source),
                () => PlusTooltipText(widget),
                () => Tooltip.ForComponent(PlusButton(widget) as Component, null),
                () => Tooltip.ForComponent(ChipButton(widget) as Component, null));
        }

        // The row is not a Selectable of its own, and the panel's AutoScrollToSelected only follows
        // the Selectable it registered for the row when it measured the column: the first under the
        // row, which is the chip (Buttons/EntryPrefab(Clone) is drawn before PlusButton, measured
        // 2026-09-11), given a UISelectionProxy as it was registered. Selecting the row object
        // itself fires no ISelectHandler the scroller listens to, so the row never scrolled into
        // view. A chip drawn since the column was measured (a rebind replaces it) carries no proxy,
        // so the column is measured again first, or that row would not scroll either.
        private static bool FocusRow(IUIKeyBinding widget, Component row, KeyBindingSource source)
        {
            UIButton chip = ChipButton(widget);
            if (chip == null)
            {
                return NativeSelectionUtility.Select(row);
            }

            if (chip.GetComponent<UISelectionProxy>() == null && source != null && source.RefreshScroll != null)
            {
                source.RefreshScroll();
            }

            return NativeSelectionUtility.Select(chip);
        }

        // The action label the game draws. It is NOT on IUIKeyBinding.Text - the game leaves that
        // empty and sets the row's own label mesh through a localization component - so it is read
        // the same way every other native label is, off that mesh with GetEffectiveText. The mesh is
        // the widget's cached field, so this is a field read and no subtree walk.
        public static string ActionText(IUIKeyBinding widget)
        {
            IUITextMesh mesh = widget != null && LabelMeshField != null ? LabelMeshField.GetValue(widget) as IUITextMesh : null;
            return mesh != null
                ? SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(mesh))
                : SpokenLines.Clean(widget != null ? widget.Text : null);
        }

        private static UIKeyBindingEntry Chip(IUIKeyBinding widget)
        {
            List<UIKeyBindingEntry> buttons = widget != null && ButtonsField != null
                ? ButtonsField.GetValue(widget) as List<UIKeyBindingEntry>
                : null;
            return buttons != null && buttons.Count > 0 ? buttons[0] : null;
        }

        private static UIButton PlusButton(IUIKeyBinding widget)
        {
            return widget != null && PlusButtonField != null ? PlusButtonField.GetValue(widget) as UIButton : null;
        }

        private static UIButton ChipButton(IUIKeyBinding widget)
        {
            UIKeyBindingEntry chip = Chip(widget);
            return chip != null && EntryButtonField != null ? EntryButtonField.GetValue(chip) as UIButton : null;
        }

        public static string ChipText(IUIKeyBinding widget)
        {
            UIKeyBindingEntry chip = Chip(widget);
            IUITextMesh mesh = chip != null && EntryTextField != null ? EntryTextField.GetValue(chip) as IUITextMesh : null;
            return mesh != null ? SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(mesh)) : null;
        }

        // The "+" button carries no visible text of its own; the game's own tooltip ("Create new
        // binding") is the label a reader wants, read the same way the cell's tooltip is.
        private static string PlusTooltipText(IUIKeyBinding widget)
        {
            Tooltip tooltip = Tooltip.ForComponent(PlusButton(widget) as Component, null);
            IReadOnlyList<string> lines = tooltip != null ? tooltip.TextLines : null;
            return lines != null && lines.Count > 0 ? SpokenLines.Clean(string.Join(" ", new List<string>(lines).ToArray())) : null;
        }

        // ---- reading one control ----

        public static string Label(IUIButton button)
        {
            UIButton concrete = button as UIButton;
            return concrete != null
                ? MenuButtonTextUtility.GetAllVisibleText(concrete)
                : SpokenLines.Clean(button != null ? button.Text : null);
        }

        private static string ToggleLabel(IUIToggle toggle)
        {
            UIToggle concrete = toggle as UIToggle;
            if (concrete != null)
            {
                string text = SpokenLines.Clean(
                    UITextMeshTextUtility.GetEffectiveText(concrete.GetTextMesh()));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpokenLines.Clean(toggle != null ? toggle.Text : null);
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
                string text = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpokenLines.Clean(slider != null ? slider.Text : null);
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
                string text = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpokenLines.Clean(field != null ? field.Text : null);
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
                string text = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return SpokenLines.Clean(dropdown != null ? dropdown.Text : null);
        }

        private static Component DropdownTextMesh(IUITextMeshDropdown dropdown)
        {
            return DropdownText.Of(dropdown) as Component;
        }

        private static bool IsActive(Component component)
        {
            return component != null && component.gameObject.activeInHierarchy;
        }

        // LAZY, never on a build path: the options are reached only through a Func the node holds,
        // so the walk is paid when the player opens the dropdown.
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
                options.Add(SpokenLines.Clean(tmpDropdown.options[i].text));
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

        /// <summary>Order the rows the way the game drew them, computing each row's hierarchy key
        /// ONCE. Comparing two rows by walking both their parent chains and building two strings
        /// there and then costs a walk per comparison - n log n of them for a tree that has not
        /// moved - so the keys are taken first and only the keys are compared. Rows that landed on
        /// the same transform keep the order they were read in.</summary>
        private static void SortByHierarchy(List<MenuRow> items)
        {
            int count = items.Count;
            if (count < 2)
            {
                return;
            }

            MenuRow[] rows = items.ToArray();
            string[] keys = new string[count];
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
            {
                keys[i] = HierarchyKey(rows[i].Transform);
                order[i] = i;
            }

            Array.Sort(
                order,
                (left, right) =>
                {
                    int byKey = string.CompareOrdinal(keys[left], keys[right]);
                    return byKey != 0 ? byKey : left - right;
                });

            for (int i = 0; i < count; i++)
            {
                items[i] = rows[order[i]];
            }
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

    /// <summary>
    /// The context the Options window's Controls page hands the reader so a key-binding row can name
    /// its action and fall back to the input manager for its text: the game keeps the row's
    /// <c>ActionReference</c> off the widget, on <c>OptionsMenuKeyBindContent._keyBinders</c>.
    ///
    /// <see cref="Resolve"/> maps a drawn row widget to the binding it holds (or null when the widget
    /// is unknown); <see cref="LastRebindWidget"/> is the row whose "+" the mod last activated, which
    /// is how a row tells its own capture from another's while the game is listening. It lives on the
    /// adapter that builds it, so it outlives the per-frame row records.
    /// </summary>
    public sealed class KeyBindingSource
    {
        public Func<IUIKeyBinding, BindingContainer> Resolve;
        public IUIKeyBinding LastRebindWidget;

        /// <summary>Have the panel's scroller measure the content column again - asked from a row's
        /// focus when its chip was redrawn since the last measure.</summary>
        public Action RefreshScroll;

        /// <summary>The action whose capture the mod last started from a "+", resolved before the
        /// click because the click redraws the page; null where the row could not be resolved.
        /// </summary>
        public ActionReference? LastRebindAction;
    }

    /// <summary>
    /// One rebindable-action row, read as facts: the game draws a gesture name, a chip holding the
    /// current hotkey (or nothing), and a "+" that starts a capture. Native facts only - the
    /// accessibility wording (the "not bound" chip, the column shape) is the screen's.
    ///
    /// The chip is a BUTTON only when the action is overridden, where activating it runs the game's
    /// own remove; the "+" always starts the game's interactive rebind. Both are the game's own
    /// clicks, not rules the mod reconstructs.
    /// </summary>
    public sealed class MenuRowKeyBinding
    {
        public MenuRowKeyBinding(
            string id,
            Func<string> getActionName,
            Func<string> getBindingText,
            Func<bool> hasOverride,
            Func<bool> rebind,
            Func<bool> clearOverride,
            Func<bool> isCapturing,
            Func<bool> isVisible,
            Action focus,
            Func<string> getPlusLabel,
            Func<Tooltip> getPlusTooltip,
            Func<Tooltip> getClearTooltip)
        {
            Id = id;
            GetActionName = getActionName;
            GetBindingText = getBindingText;
            HasOverride = hasOverride;
            Rebind = rebind;
            ClearOverride = clearOverride;
            IsCapturing = isCapturing;
            IsVisible = isVisible;
            Focus = focus;
            GetPlusLabel = getPlusLabel;
            GetPlusTooltip = getPlusTooltip;
            GetClearTooltip = getClearTooltip;
        }

        public string Id { get; private set; }

        /// <summary>The gesture name the game draws (the row's primary cell).</summary>
        public Func<string> GetActionName { get; private set; }

        /// <summary>The current hotkey the chip draws; empty where the game draws no binding.</summary>
        public Func<string> GetBindingText { get; private set; }

        /// <summary>Whether the row's binding is a player override rather than the game's default -
        /// the game draws the chip as an interactable button only then.</summary>
        public Func<bool> HasOverride { get; private set; }

        /// <summary>Start the game's capture (the "+" click); true when the click landed.</summary>
        public Func<bool> Rebind { get; private set; }

        /// <summary>Clear the override back to the default (the chip's own click); false when there
        /// is no override chip to click.</summary>
        public Func<bool> ClearOverride { get; private set; }

        /// <summary>Whether the game is listening for THIS row's new key right now.</summary>
        public Func<bool> IsCapturing { get; private set; }

        public Func<bool> IsVisible { get; private set; }

        /// <summary>Put the game's own selection on the row, which is what scrolls it into view.</summary>
        public Action Focus { get; private set; }

        /// <summary>The game's own label for the "+" ("Create new binding"), which draws no text.</summary>
        public Func<string> GetPlusLabel { get; private set; }

        public Func<Tooltip> GetPlusTooltip { get; private set; }

        /// <summary>The chip's own tooltip ("Remove binding") where it is an override chip.</summary>
        public Func<Tooltip> GetClearTooltip { get; private set; }
    }
}
