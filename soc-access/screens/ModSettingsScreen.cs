using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ModSettingsScreen : Screen
    {
        private const string TabChangedSoundKey = "Common_DefaultClick";

        private readonly Func<bool> _close;
        private ModSettingsTab _selectedTab = ModSettingsTab.General;

        public ModSettingsScreen(Func<bool> close)
            : base(new ContainerWidget("mod-settings-screen", ModText.Get(ModStrings.Screens.ModSettings)))
        {
            _close = close;
            RootWidget = BuildRoot();
        }

        public override bool IsPresent()
        {
            return true;
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _close != null && _close();
            }

            return base.OnActionJustPressed(action);
        }

        private ContainerWidget BuildRoot()
        {
            ContainerWidget root = new ContainerWidget("mod-settings-screen", ModText.Get(ModStrings.Screens.ModSettings));
            root.AddChild(BuildTabs());
            root.AddChild(new CheckboxWidget(
                "mod-settings-scanner-plays-directional-beep",
                ModText.Get(ModStrings.Screens.ScannerPlaysDirectionalBeep),
                ToggleScannerPlaysDirectionalBeep,
                () => ModSettings.ScannerPlaysDirectionalBeep,
                IsGeneralTabSelected));
            root.AddChild(new CheckboxWidget(
                "mod-settings-read-story-camera-focus-changes",
                ModText.Get(ModStrings.Screens.ReadStoryCameraFocusChanges),
                ToggleReadStoryCameraFocusChanges,
                () => ModSettings.ReadStoryCameraFocusChanges,
                IsGeneralTabSelected));
            root.AddChild(new CheckboxWidget(
                "mod-settings-read-enemy-influence",
                ModText.Get(ModStrings.Screens.ReadEnemyInfluence),
                ToggleReadEnemyInfluence,
                () => ModSettings.ReadEnemyInfluence,
                IsCombatTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-close",
                ModText.Get(ModStrings.Screens.Close),
                () => _close != null && _close(),
                null,
                () => true));
            return root;
        }

        private MenuWidget BuildTabs()
        {
            MenuWidget menu = new MenuWidget("mod-settings-tabs", ModText.Get(ModStrings.Screens.Tabs));
            AddTab(menu, ModSettingsTab.General, "mod-settings-tab-general", ModStrings.Screens.General);
            AddTab(menu, ModSettingsTab.Combat, "mod-settings-tab-combat", ModStrings.Screens.Combat);
            menu.SetFocusedItemById(_selectedTab == ModSettingsTab.Combat
                ? "mod-settings-tab-combat"
                : "mod-settings-tab-general");
            return menu;
        }

        private void AddTab(MenuWidget menu, ModSettingsTab tab, string id, ModString label)
        {
            menu.AddItem(new MenuItemWidget(
                id,
                () => ModText.Get(label),
                null,
                () => SelectTab(tab),
                () =>
                {
                    if (_selectedTab != tab)
                    {
                        SelectTab(tab);
                    }
                },
                () => true));
        }

        private bool SelectTab(ModSettingsTab tab)
        {
            if (_selectedTab == tab)
            {
                return true;
            }

            _selectedTab = tab;
            NativeSoundUtility.PostEvent(TabChangedSoundKey);
            return true;
        }

        private bool IsGeneralTabSelected()
        {
            return _selectedTab == ModSettingsTab.General;
        }

        private bool IsCombatTabSelected()
        {
            return _selectedTab == ModSettingsTab.Combat;
        }

        private static void ToggleReadEnemyInfluence()
        {
            ModSettings.SetReadEnemyInfluence(!ModSettings.ReadEnemyInfluence);
        }

        private static void ToggleReadStoryCameraFocusChanges()
        {
            ModSettings.SetReadStoryCameraFocusChanges(!ModSettings.ReadStoryCameraFocusChanges);
        }

        private static void ToggleScannerPlaysDirectionalBeep()
        {
            ModSettings.SetScannerPlaysDirectionalBeep(!ModSettings.ScannerPlaysDirectionalBeep);
        }

        private enum ModSettingsTab
        {
            General,
            Combat
        }
    }
}
