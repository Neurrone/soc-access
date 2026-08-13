using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
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
                "mod-settings-scanner-uses-long-directions",
                ModText.Get(ModStrings.Screens.ScannerUsesLongDirections),
                ToggleScannerUsesLongDirections,
                () => ModSettings.ScannerUsesLongDirections,
                IsScannerTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-scanner-result-announcements",
                ModText.Get(ModStrings.Screens.ScannerResultAnnouncements),
                () => OpenAnnouncementOrderScreen(ScannerAnnouncementDefinitions.Result),
                null,
                () => true,
                IsScannerTabSelected));
            root.AddChild(new CheckboxWidget(
                "mod-settings-read-story-camera-focus-changes",
                ModText.Get(ModStrings.Screens.ReadStoryCameraFocusChanges),
                ToggleReadStoryCameraFocusChanges,
                () => ModSettings.ReadStoryCameraFocusChanges,
                IsGeneralTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-adventure-map-tile-announcements",
                ModText.Get(ModStrings.Screens.TileAnnouncements),
                () => OpenAnnouncementOrderScreen(AdventureMapAnnouncementDefinitions.Tile),
                null,
                () => true,
                IsAdventureMapTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-adventure-map-scanner-content-announcements",
                ModText.Get(ModStrings.Screens.ScannerContentAnnouncements),
                () => OpenAnnouncementOrderScreen(AdventureMapAnnouncementDefinitions.ScannerContent),
                null,
                () => true,
                IsAdventureMapTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-adventure-map-wielder-announcements",
                ModText.Get(ModStrings.Screens.WielderAnnouncements),
                () => OpenAnnouncementOrderScreen(AdventureMapAnnouncementDefinitions.Wielder),
                null,
                () => true,
                IsAdventureMapTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-adventure-map-entity-announcements",
                ModText.Get(ModStrings.Screens.MapEntityAnnouncements),
                () => OpenAnnouncementOrderScreen(AdventureMapAnnouncementDefinitions.MapEntity),
                null,
                () => true,
                IsAdventureMapTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-troop-deployment-tile-announcements",
                ModText.Get(ModStrings.Screens.TileAnnouncements),
                () => OpenAnnouncementOrderScreen(TroopDeploymentAnnouncementDefinitions.Tile),
                null,
                () => true,
                IsTroopDeploymentTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-troop-deployment-scanner-content-announcements",
                ModText.Get(ModStrings.Screens.ScannerContentAnnouncements),
                () => OpenAnnouncementOrderScreen(TroopDeploymentAnnouncementDefinitions.ScannerContent),
                null,
                () => true,
                IsTroopDeploymentTabSelected));
            root.AddChild(new CheckboxWidget(
                "mod-settings-read-enemy-influence",
                ModText.Get(ModStrings.Screens.ReadEnemyInfluence),
                ToggleReadEnemyInfluence,
                () => ModSettings.ReadEnemyInfluence,
                IsCombatTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-tile-announcements",
                ModText.Get(ModStrings.Screens.TileAnnouncements),
                () => OpenAnnouncementOrderScreen(CombatAnnouncementDefinitions.Tile),
                null,
                () => true,
                IsCombatTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-combat-scanner-content-announcements",
                ModText.Get(ModStrings.Screens.ScannerContentAnnouncements),
                () => OpenAnnouncementOrderScreen(CombatAnnouncementDefinitions.ScannerContent),
                null,
                () => true,
                IsCombatTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-troop-announcements",
                ModText.Get(ModStrings.Screens.TroopAnnouncements),
                () => OpenAnnouncementOrderScreen(CombatAnnouncementDefinitions.Troop),
                null,
                () => true,
                IsCombatTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-entity-announcements",
                ModText.Get(ModStrings.Screens.EntityAnnouncements),
                () => OpenAnnouncementOrderScreen(CombatAnnouncementDefinitions.Entity),
                null,
                () => true,
                IsCombatTabSelected));
            root.AddChild(new CheckboxWidget(
                "mod-settings-tile-cues-enabled",
                ModText.Get(ModStrings.Screens.PlayTileSoundCues),
                ToggleTileCuesEnabled,
                () => ModSettings.TileCuesEnabled,
                IsAudioTabSelected));
            root.AddChild(new ButtonWidget(
                "mod-settings-audio-glossary",
                ModText.Get(ModStrings.Screens.AudioGlossary),
                OpenAudioGlossaryScreen,
                null,
                () => true,
                IsAudioTabSelected));
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
            AddTab(menu, ModSettingsTab.Scanner, "mod-settings-tab-scanner", ModStrings.Screens.Scanner);
            AddTab(menu, ModSettingsTab.AdventureMap, "mod-settings-tab-adventure-map", ModStrings.Screens.AdventureMap);
            AddTab(menu, ModSettingsTab.TroopDeployment, "mod-settings-tab-troop-deployment", ModStrings.Screens.TroopDeployment);
            AddTab(menu, ModSettingsTab.Combat, "mod-settings-tab-combat", ModStrings.Screens.Combat);
            AddTab(menu, ModSettingsTab.Audio, "mod-settings-tab-audio", ModStrings.Screens.Audio);
            menu.SetFocusedItemById(GetSelectedTabId());
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

        private bool IsScannerTabSelected()
        {
            return _selectedTab == ModSettingsTab.Scanner;
        }

        private bool IsAdventureMapTabSelected()
        {
            return _selectedTab == ModSettingsTab.AdventureMap;
        }

        private bool IsTroopDeploymentTabSelected()
        {
            return _selectedTab == ModSettingsTab.TroopDeployment;
        }

        private bool IsCombatTabSelected()
        {
            return _selectedTab == ModSettingsTab.Combat;
        }

        private bool IsAudioTabSelected()
        {
            return _selectedTab == ModSettingsTab.Audio;
        }

        private string GetSelectedTabId()
        {
            switch (_selectedTab)
            {
                case ModSettingsTab.Scanner:
                    return "mod-settings-tab-scanner";
                case ModSettingsTab.AdventureMap:
                    return "mod-settings-tab-adventure-map";
                case ModSettingsTab.TroopDeployment:
                    return "mod-settings-tab-troop-deployment";
                case ModSettingsTab.Combat:
                    return "mod-settings-tab-combat";
                case ModSettingsTab.Audio:
                    return "mod-settings-tab-audio";
                default:
                    return "mod-settings-tab-general";
            }
        }

        private static void ToggleReadEnemyInfluence()
        {
            ModSettings.SetReadEnemyInfluence(!ModSettings.ReadEnemyInfluence);
        }

        private static void ToggleReadStoryCameraFocusChanges()
        {
            ModSettings.SetReadStoryCameraFocusChanges(!ModSettings.ReadStoryCameraFocusChanges);
        }

        private static void ToggleTileCuesEnabled()
        {
            ModSettings.SetTileCuesEnabled(!ModSettings.TileCuesEnabled);
        }

        private static bool OpenAudioGlossaryScreen()
        {
            if (SocAccessPlugin.Instance == null || SocAccessPlugin.Instance.ScreenManager == null)
            {
                return false;
            }

            SocAccessPlugin.Instance.ScreenManager.Push(
                new AudioGlossaryScreen(),
                "audio glossary screen opened");
            return true;
        }

        private static void ToggleScannerUsesLongDirections()
        {
            ModSettings.SetScannerUsesLongDirections(!ModSettings.ScannerUsesLongDirections);
        }

        private static bool OpenAnnouncementOrderScreen(AnnouncementGroupDefinition group)
        {
            if (group == null || SocAccessPlugin.Instance == null || SocAccessPlugin.Instance.ScreenManager == null)
            {
                return false;
            }

            SocAccessPlugin.Instance.ScreenManager.Push(
                new AnnouncementOrderScreen(group),
                "announcement order screen opened");
            return true;
        }

        private enum ModSettingsTab
        {
            General,
            Scanner,
            AdventureMap,
            TroopDeployment,
            Combat,
            Audio
        }
    }
}
