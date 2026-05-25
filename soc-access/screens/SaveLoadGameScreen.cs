using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SaveLoadGameScreen : Screen
    {
        private const string EntriesMenuId = "save-load-entries";
        private const string TabsMenuId = "save-load-tabs";
        private const string CancelButtonId = "save-load-cancel";

        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(SaveLoadGameMenuInstaller), "Container");

        private readonly SaveLoadGameMenuAdapter _adapter;

        public SaveLoadGameScreen(SaveLoadGameMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public object SourceKey
        {
            get { return _adapter != null ? _adapter.SourceKey : null; }
        }

        public static Screen TryBuildActiveScreen()
        {
            SaveLoadGameMenu menu = FindActiveSaveLoadGameMenu();
            if (menu == null)
            {
                return null;
            }

            SaveLoadGameMenuAdapter adapter = new SaveLoadGameMenuAdapter(menu);
            return adapter.IsPresent() ? new SaveLoadGameScreen(adapter) : null;
        }

        public bool Matches(SaveLoadGameMenu menu)
        {
            return ReferenceEquals(SourceKey, menu);
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

        private static ContainerWidget BuildRoot(SaveLoadGameMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget(
                "save-load-game-screen",
                adapter != null ? adapter.Title : string.Empty);

            root.AddChild(BuildTabs(adapter));
            root.AddChild(new TextWidget(
                "save-load-description",
                () => adapter != null ? adapter.GetSaveDescriptionText() : string.Empty,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter != null
                    && adapter.IsSaveDescriptionVisible()
                    && !string.IsNullOrWhiteSpace(adapter.GetSaveDescriptionText())));
            root.AddChild(new TextInputWidget(
                "save-load-name",
                string.Empty,
                () => adapter != null ? adapter.InputField : null,
                null,
                () => adapter?.FocusInput(),
                () => adapter != null && adapter.IsInputEnabled(),
                () => adapter != null && adapter.IsInputVisible()));
            root.AddChild(BuildEntries(adapter));
            root.AddChild(new TextWidget(
                "save-load-details",
                () => adapter != null ? adapter.GetDetailsText() : string.Empty,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter != null && adapter.HasDetailsText()));

            AddButton(root, adapter != null ? adapter.SaveButton : null);
            AddButton(root, adapter != null ? adapter.LoadButton : null);
            AddButton(root, adapter != null ? adapter.LoadAsHotseatButton : null);
            AddButton(root, adapter != null ? adapter.LoadAsOnlineButton : null);
            AddButton(root, adapter != null ? adapter.DeleteButton : null);
            AddButton(root, adapter != null ? adapter.CancelButton : null);
            return root;
        }

        private static MenuWidget BuildTabs(SaveLoadGameMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                TabsMenuId,
                ModText.Get(ModStrings.Screens.Categories),
                () => adapter != null
                    && adapter.Mode == SaveLoadGameMenu.Mode.Load
                    && adapter.GetTabs().Count > 0);

            if (adapter == null)
            {
                return menu;
            }

            IReadOnlyList<SaveLoadGameMenuAdapter.TabItem> tabs = adapter.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                SaveLoadGameMenuAdapter.TabItem tab = tabs[i];
                menu.AddItem(new MenuItemWidget(
                    tab.Id,
                    tab.GetLabel,
                    () => tab.IsSelected() ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    tab.Activate,
                    tab.Focus,
                    tab.IsVisible,
                    (Tooltip)null,
                    null,
                    tab.IsEnabled));
                if (tab.IsSelected())
                {
                    menu.SetFocusedItemById(tab.Id);
                }
            }

            return menu;
        }

        private static MenuWidget BuildEntries(SaveLoadGameMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget(
                EntriesMenuId,
                string.Empty,
                () => adapter != null && adapter.GetEntries().Count > 0);

            if (adapter == null)
            {
                return menu;
            }

            IReadOnlyList<SaveLoadGameMenuAdapter.SaveEntry> entries = adapter.GetEntries();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                SaveLoadGameMenuAdapter.SaveEntry entry = entries[i];
                menu.AddItem(new MenuItemWidget(
                    entry.Id,
                    () => BuildEntryLabel(entry),
                    () => BuildEntryStatus(entry),
                    entry.Select,
                    entry.Focus,
                    entry.IsVisible));
                if (entry.IsSelected)
                {
                    menu.SetFocusedItemById(entry.Id);
                }
            }

            return menu;
        }

        private static string BuildEntryLabel(SaveLoadGameMenuAdapter.SaveEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return MenuButtonTextUtility.JoinParts(entry.SaveName, entry.DateText);
        }

        private static string BuildEntryStatus(SaveLoadGameMenuAdapter.SaveEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (entry.IsSelected)
            {
                parts.Add(ModText.Get(ModStrings.UI.Selected));
            }

            if (entry.IsCorrupt)
            {
                parts.Add(ModText.Get(ModStrings.UI.StatusCorrupt));
            }

            return parts.Count == 0 ? string.Empty : ModText.JoinList(parts);
        }

        private static void AddButton(ContainerWidget root, SaveLoadGameMenuAdapter.ButtonItem button)
        {
            if (root == null || button == null)
            {
                return;
            }

            Func<string> getLabel = button.Id == CancelButtonId
                ? (Func<string>)(() => ModText.Get(ModStrings.Actions.Cancel))
                : button.GetLabel;

            root.AddChild(new ButtonWidget(
                button.Id,
                getLabel,
                button.Activate,
                button.Focus,
                button.IsEnabled,
                button.IsVisible));
        }

        private static SaveLoadGameMenu FindActiveSaveLoadGameMenu()
        {
            SaveLoadGameMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<SaveLoadGameMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                SaveLoadGameMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                SaveLoadGameMenu menu = TryResolve<SaveLoadGameMenu>(installer);
                if (menu == null)
                {
                    continue;
                }

                SaveLoadGameMenuAdapter adapter = new SaveLoadGameMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return menu;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(SaveLoadGameMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static T TryResolve<T>(SaveLoadGameMenuInstaller installer) where T : class
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
