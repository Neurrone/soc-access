using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// THE MOD'S OPTIONS, AS A WINDOW.
    ///
    /// Ruling J: the settings that used to exist only as a mod-owned menu on Ctrl+M are drawn in a
    /// copy of the game's own options panel (<see cref="ModDialog"/>), so a sighted player can work
    /// them with the mouse and this screen reads them the way <see cref="OptionsScreen"/> reads the
    /// game's - through the same row reader and the same node declarations
    /// (<see cref="MenuFormNodes"/>).
    ///
    /// Three stops, as Options has: the category tabs down the left, the settings of the category
    /// showing, and the close button. The tabs switch ON FOCUS, for the same reason they do in
    /// Options: the switch is instant, so arriving at a tab and arriving at its page are one event.
    ///
    /// Escape is the mod's here. The window is the mod's own surface, so it claims Back and closes -
    /// and denies the game a key it would otherwise use on the menu underneath.
    /// </summary>
    public sealed class ModOptionsScreen : GraphScreen
    {
        private const string TabsStop = "mod-options-tabs";
        private const string RowsStop = "mod-options-rows";
        private const string ButtonsStop = "mod-options-buttons";

        private readonly ModDialog _dialog;
        private readonly MenuFormNodes _rows = new MenuFormNodes("mod-options");

        private ModOptionsScreen(ModDialog dialog)
        {
            _dialog = dialog;
        }

        /// <summary>Draw the window and put its screen on the stack. Answers false when the options
        /// panel it copies cannot be found, which is the only way it can fail.</summary>
        public static bool Open()
        {
            ScreenManager manager = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            if (manager == null || manager.Get<ModOptionsScreen>() != null)
            {
                return false;
            }

            ModDialog dialog = ModDialog.Open(ModText.Get(ModStrings.Screens.ModOptions), withTabs: true);
            if (dialog == null)
            {
                return false;
            }

            ModOptionsScreen screen = new ModOptionsScreen(dialog);
            dialog.DrawContent = screen.Draw;
            dialog.OnClose = () => screen.Close();
            for (int i = 0; i < TabLabels.Length; i++)
            {
                dialog.AddTab(ModText.Get(TabLabels[i]));
            }

            dialog.Select(0);
            manager.Push(screen, "mod options opened");
            return true;
        }

        public override string Key
        {
            get { return "mod-options"; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.ModOptions); }
        }

        /// <summary>The tab column, so arrival lands on the category showing rather than changing it
        /// by arriving - the same reason Options lands there.</summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override bool IsPresent()
        {
            return _dialog != null && _dialog.IsOpen;
        }

        /// <summary>The window is the mod's own, so the key that leaves it is the mod's too.</summary>
        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override bool Back()
        {
            return Close();
        }

        public bool Close()
        {
            _dialog.Close();
            ScreenManager manager = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            return manager != null && manager.Pop<ModOptionsScreen>("mod options closed");
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(TabsStop);
            BuildTabs(builder);

            builder.BeginStop(RowsStop);
            _rows.BuildRows(builder, _dialog.Rows);

            builder.BeginStop(ButtonsStop);
            _rows.AddWindowButton(
                builder,
                _dialog.CloseButton,
                () => ModText.Get(ModStrings.Screens.Close));
        }

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<ModDialog.Tab> tabs = _dialog.Tabs;
            for (int i = 0; i < tabs.Count; i++)
            {
                ModDialog.Tab tab = tabs[i];
                if (tab == null || !tab.IsVisible())
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Tab(tab.GetLabel, tab.IsSelected, tab.IsVisible);
                // Focusing the tab IS switching to it; the guard makes re-focusing the showing tab a
                // no-op, so re-entering the column does not redraw the page.
                vtable.OnFocusVisual = () =>
                {
                    if (!tab.IsSelected())
                    {
                        tab.Select();
                    }
                };
                vtable.OnActivate = () => tab.Select();
                builder.AddItem(new SyntheticNode(ControlId.Structural("mod-options:tab/" + i), vtable));
            }
        }

        // ---- what each tab holds ----

        private static readonly ModString[] TabLabels =
        {
            ModStrings.Screens.General,
            ModStrings.Screens.Scanner,
            ModStrings.Screens.AdventureMap,
            ModStrings.Screens.TroopDeployment,
            ModStrings.Screens.Combat,
            ModStrings.Screens.Audio
        };

        /// <summary>Draw one category. Every row is a real game control and every callback writes
        /// <c>ModSettings</c>, exactly as the menu this replaces did.</summary>
        private void Draw(int tab)
        {
            switch (tab)
            {
                case 0:
                    DrawGeneral();
                    break;
                case 1:
                    DrawScanner();
                    break;
                case 2:
                    DrawAdventureMap();
                    break;
                case 3:
                    DrawTroopDeployment();
                    break;
                case 4:
                    DrawCombat();
                    break;
                case 5:
                    DrawAudio();
                    break;
            }
        }

        private void DrawGeneral()
        {
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.ReadStoryCameraFocusChanges),
                ModSettings.ReadStoryCameraFocusChanges,
                ModSettings.SetReadStoryCameraFocusChanges);
        }

        private void DrawScanner()
        {
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.ScannerUsesLongDirections),
                ModSettings.ScannerUsesLongDirections,
                ModSettings.SetScannerUsesLongDirections);
            AddAnnouncementOrder(ModStrings.Screens.ScannerResultAnnouncements, ScannerAnnouncementDefinitions.Result);
            AddCustomCategories(AdventureScannerTaxonomy.Instance, ModStrings.Screens.AdventureMap);
            AddCustomCategories(BattleScannerTaxonomy.Instance, ModStrings.Screens.Battle);
        }

        private void DrawAdventureMap()
        {
            // Turning the road-directions element off leaves nothing for the long form to lengthen,
            // so this row goes with it rather than sitting there doing nothing.
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.AdventureMapUsesLongRoadDirections),
                ModSettings.AdventureMapUsesLongRoadDirections,
                ModSettings.SetAdventureMapUsesLongRoadDirections,
                ModSettings.GetAnnouncementElementEnabled(
                    AdventureMapAnnouncementDefinitions.Tile,
                    AdventureMapAnnouncementDefinitions.RoadDirectionsElement));
            AddAnnouncementOrder(ModStrings.Screens.TileAnnouncements, AdventureMapAnnouncementDefinitions.Tile);
            AddAnnouncementOrder(ModStrings.Screens.ScannerContentAnnouncements, AdventureMapAnnouncementDefinitions.ScannerContent);
            AddAnnouncementOrder(ModStrings.Screens.WielderAnnouncements, AdventureMapAnnouncementDefinitions.Wielder);
            AddAnnouncementOrder(ModStrings.Screens.MapEntityAnnouncements, AdventureMapAnnouncementDefinitions.MapEntity);
        }

        private void DrawTroopDeployment()
        {
            AddAnnouncementOrder(ModStrings.Screens.TileAnnouncements, TroopDeploymentAnnouncementDefinitions.Tile);
            AddAnnouncementOrder(ModStrings.Screens.ScannerContentAnnouncements, TroopDeploymentAnnouncementDefinitions.ScannerContent);
        }

        private void DrawCombat()
        {
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.ReadEnemyInfluence),
                ModSettings.ReadEnemyInfluence,
                ModSettings.SetReadEnemyInfluence);
            AddAnnouncementOrder(ModStrings.Screens.TileAnnouncements, CombatAnnouncementDefinitions.Tile);
            AddAnnouncementOrder(ModStrings.Screens.ScannerContentAnnouncements, CombatAnnouncementDefinitions.ScannerContent);
            AddAnnouncementOrder(ModStrings.Screens.TroopAnnouncements, CombatAnnouncementDefinitions.Troop);
            AddAnnouncementOrder(ModStrings.Screens.EntityAnnouncements, CombatAnnouncementDefinitions.Entity);
        }

        private void DrawAudio()
        {
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.PlayTileSoundCues),
                ModSettings.TileCuesEnabled,
                ModSettings.SetTileCuesEnabled);
            _dialog.AddButton(ModText.Get(ModStrings.Screens.AudioGlossary), OpenAudioGlossary);
        }

        private void AddAnnouncementOrder(ModString label, AnnouncementGroupDefinition group)
        {
            _dialog.AddButton(ModText.Get(label), () => Push(new AnnouncementOrderScreen(group), "announcement order screen opened"));
        }

        private void AddCustomCategories(ScannerTaxonomy taxonomy, ModString contextLabel)
        {
            _dialog.AddButton(
                ModText.Get(ModStrings.Screens.CustomCategories, ModText.Get(contextLabel)),
                () => Push(new ScannerCustomCategoriesScreen(taxonomy, contextLabel), "scanner custom categories screen opened"));
        }

        private static void OpenAudioGlossary()
        {
            Push(new AudioGlossaryScreen(), "audio glossary screen opened");
        }

        private static void Push(Screen screen, string reason)
        {
            ScreenManager manager = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            if (manager != null)
            {
                manager.Push(screen, reason);
            }
        }
    }
}
