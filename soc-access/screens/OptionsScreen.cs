using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class OptionsScreen : Screen
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(OptionsMenuInstaller), "Container");

        private readonly OptionsMenuAdapter _adapter;

        public OptionsScreen(OptionsMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            OptionsMenu menu = FindActiveOptionsMenu();
            if (menu == null)
            {
                return null;
            }

            OptionsMenuAdapter adapter = new OptionsMenuAdapter(menu);
            return adapter.IsPresent() ? new OptionsScreen(adapter) : null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            RootWidget = BuildRoot(_adapter);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private static ContainerWidget BuildRoot(OptionsMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("options-screen", ModText.Get(ModStrings.Screens.Options));
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildTabs(adapter));
            AddContent(root, adapter);

            OptionsMenuAdapter.ButtonItem okButton = adapter.GetOkButton();
            if (okButton != null)
            {
                root.AddChild(BuildButton(okButton));
            }

            return root;
        }

        private static MenuWidget BuildTabs(OptionsMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("options-tabs", ModText.Get(ModStrings.Screens.Categories));
            IReadOnlyList<OptionsMenuAdapter.TabItem> tabs = adapter.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                OptionsMenuAdapter.TabItem tab = tabs[i];
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    tab.Id,
                    tab.GetLabel,
                    null,
                    tab.Select,
                    () =>
                    {
                        if (adapter.GetActiveTabIndex() != index)
                        {
                            tab.Select();
                        }
                    },
                    tab.IsVisible));
            }

            menu.SetFocusedItemById("options-tab-" + adapter.GetActiveTabIndex());
            return menu;
        }

        private static void AddContent(ContainerWidget root, OptionsMenuAdapter adapter)
        {
            IReadOnlyList<OptionsMenuAdapter.ControlItem> controls = adapter.GetCurrentContentControls();
            for (int i = 0; i < controls.Count; i++)
            {
                OptionsMenuAdapter.ControlItem control = controls[i];
                object item = control != null ? control.Item : null;
                OptionsMenuAdapter.TextItem text = item as OptionsMenuAdapter.TextItem;
                if (text != null)
                {
                    root.AddChild(new TextWidget(
                        text.Id,
                        text.GetText,
                        null,
                        includeParentLabelInAnnouncement: false,
                        isVisible: () => text.IsVisible() && !string.IsNullOrWhiteSpace(text.GetText())));
                    continue;
                }

                OptionsMenuAdapter.DropdownItem dropdown = item as OptionsMenuAdapter.DropdownItem;
                if (dropdown != null)
                {
                    root.AddChild(BuildDropdown(dropdown));
                    continue;
                }

                OptionsMenuAdapter.ToggleItem toggle = item as OptionsMenuAdapter.ToggleItem;
                if (toggle != null)
                {
                    root.AddChild(new CheckboxWidget(
                        toggle.Id,
                        toggle.GetLabel,
                        toggle.Toggle,
                        toggle.IsChecked,
                        toggle.IsVisible,
                        toggle.IsEnabled,
                        toggle.GetTooltip));
                    continue;
                }

                OptionsMenuAdapter.SliderItem slider = item as OptionsMenuAdapter.SliderItem;
                if (slider != null)
                {
                    root.AddChild(new SliderWidget(
                        slider.Id,
                        slider.GetLabel,
                        slider.GetValueText,
                        slider.GetValue,
                        slider.GetMinimumValue,
                        slider.GetMaximumValue,
                        slider.GetStep,
                        slider.SetValue,
                        slider.IsEnabled,
                        slider.IsVisible,
                        slider.GetTooltip));
                    continue;
                }

                OptionsMenuAdapter.ButtonItem button = item as OptionsMenuAdapter.ButtonItem;
                if (button != null)
                {
                    root.AddChild(BuildButton(button));
                }
            }
        }

        private static MenuWidget BuildDropdown(OptionsMenuAdapter.DropdownItem dropdown)
        {
            MenuWidget menu = new MenuWidget(dropdown.Id, dropdown.GetLabel(), dropdown.IsVisible);
            IReadOnlyList<string> options = dropdown.GetOptions();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                menu.AddItem(new MenuItemWidget(
                    dropdown.Id + "-option-" + index,
                    () => options[index],
                    () => dropdown.GetValue() == index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () => dropdown.SetValue(index),
                    () =>
                    {
                        dropdown.Focus?.Invoke();
                        if (dropdown.GetValue() != index)
                        {
                            dropdown.SetValue(index);
                        }
                    },
                    () => true,
                    dropdown.GetTooltip,
                    null,
                    dropdown.IsEnabled));
            }

            menu.SetFocusedItemById(dropdown.Id + "-option-" + dropdown.GetValue());
            return menu;
        }

        private static ButtonWidget BuildButton(OptionsMenuAdapter.ButtonItem button)
        {
            return new ButtonWidget(
                button.Id,
                button.GetLabel,
                button.Activate,
                button.Focus,
                button.IsEnabled,
                button.IsVisible,
                button.GetTooltip);
        }

        private static OptionsMenu FindActiveOptionsMenu()
        {
            OptionsMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<OptionsMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                OptionsMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                OptionsMenu menu = TryResolve<OptionsMenu>(installer);
                if (menu == null)
                {
                    continue;
                }

                OptionsMenuAdapter adapter = new OptionsMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return menu;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(OptionsMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static T TryResolve<T>(OptionsMenuInstaller installer) where T : class
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
