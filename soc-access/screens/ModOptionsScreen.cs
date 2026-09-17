using System;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
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

        /// <summary>The index of the Keybinds tab in <see cref="TabLabels"/> - the one page whose rows
        /// are the mod's own gestures rather than game controls.</summary>
        private const int KeybindsTab = 6;

        /// <summary>The window this screen reads, or null when it is not open. Written by
        /// <see cref="Open"/>: the screen is registered once and lives for the whole mod load.
        /// </summary>
        private ModDialog _dialog;

        private readonly MenuFormNodes _rows = new MenuFormNodes("mod-options");

        /// <summary>Draw the window and put its screen on the stack. Answers false when the options
        /// panel it copies cannot be found, which is the only way it can fail.</summary>
        public static bool Open()
        {
            ScreenManager manager = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            ModOptionsScreen screen = manager == null ? null : manager.Registered<ModOptionsScreen>();
            Screen owner = manager == null ? null : manager.Current;
            if (screen == null || owner == null || screen.IsActive() || ReferenceEquals(owner, screen))
            {
                return false;
            }

            ModDialog dialog = ModDialog.Open(ModText.Get(ModStrings.Screens.ModOptions), withTabs: true);
            if (dialog == null)
            {
                return false;
            }

            screen._dialog = dialog;
            dialog.DrawContent = screen.Draw;
            dialog.OnClose = () => screen.Close();
            for (int i = 0; i < TabLabels.Length; i++)
            {
                dialog.AddTab(ModText.Get(TabLabels[i]));
            }

            dialog.Select(0);
            // A CHILD of the page it was opened from: nothing in the game says the window is up, and
            // the page underneath keeps its own cursor while it is.
            owner.PushChild(screen);
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

        public override bool IsActive()
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
            // A capture armed on the Keybinds tab must not outlive the window; the player has no
            // cancel, so the window closing is the only way out of an armed-but-unwanted capture.
            ModKeyCapture.Cancel();
            ModDialog dialog = _dialog;
            _dialog = null;
            if (dialog != null)
            {
                dialog.Close();
            }

            CloseSelf();
            return dialog != null;
        }

        /// <summary>Above every page it can be opened from and below the drop list its own combo boxes
        /// open. Read only by the dev server: a child screen is not polled.</summary>
        public override int Layer
        {
            get { return 50; }
        }

        /// <summary>The game took the window away underneath (the page it was drawn over closed), so
        /// the screen goes with it. A child is not polled; it asks for itself.</summary>
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!IsActive())
            {
                CloseSelf();
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(TabsStop);
            BuildTabs(builder);

            IReadOnlyList<MenuRow> rows = _dialog.Rows;
            builder.BeginStop(RowsStop);
            _rows.BuildRows(builder, rows, _dialog.Facts);
            // The Keybinds tab's gesture rows are the game's own key-binding widget, so they are read
            // as the Options window's Controls page is: a table after the reset-all button.
            _rows.BuildKeyBindingSheet(builder, rows);

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

                NodeVtable vtable = GraphNodes.SwitchingTab(
                    tab.GetLabel,
                    tab.IsSelected,
                    () => tab.Select(),
                    tab.IsVisible);
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
            ModStrings.Screens.Audio,
            ModStrings.Screens.Keybinds
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
                case KeybindsTab:
                    DrawKeybinds();
                    break;
            }
        }

        private void DrawGeneral()
        {
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.ReadStoryCameraFocusChanges),
                ModSettings.ReadStoryCameraFocusChanges,
                ModSettings.SetReadStoryCameraFocusChanges);
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.ReadLongTooltips),
                ModSettings.ReadLongTooltips,
                ModSettings.SetReadLongTooltips,
                tooltip: ModText.Get(ModStrings.Screens.ReadLongTooltipsTooltip));
            // A checkbox over a setting that is a string: on is "always", off is "never", and the
            // day a third value lands this row becomes a drop list without the stored value moving.
            _dialog.AddToggle(
                ModText.Get(ModStrings.Screens.ReadUsageHints),
                ModSettings.ReadUsageHints != UsageHintReading.Never,
                on => ModSettings.SetReadUsageHints(on ? UsageHintReading.Always : UsageHintReading.Never),
                tooltip: ModText.Get(ModStrings.Screens.ReadUsageHintsTooltip));
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
            _dialog.AddButton(ModText.Get(ModStrings.Screens.AudioGlossary), ModOptionsDialogs.OpenAudioGlossary);
        }

        /// <summary>The Keybinds tab: the reset-all button, then one of the game's own key-binding
        /// rows per mod gesture under its group's caption - the shape the Options window's Controls
        /// page draws, so a sighted player works it the same way and the sheet reads it the same
        /// way. The widget takes plain text and callbacks, so a mod gesture needs no game input
        /// action behind it; only the capture and the clear are the mod's.</summary>
        private void DrawKeybinds()
        {
            ModDialog dialog = _dialog;
            dialog.AddButton(
                ModText.Get(ModStrings.Screens.ResetAllToDefaults),
                () =>
                {
                    ModSettings.ClearAllKeybindOverrides();
                    // Every chip changed; the game's page re-sets each one, a redraw here is the same.
                    dialog.Redraw();
                });

            IReadOnlyList<ModGestureCatalog.Group> groups = ModGestureCatalog.Groups;
            for (int g = 0; g < groups.Count; g++)
            {
                ModGestureCatalog.Group group = groups[g];
                dialog.AddText(ModText.Get(group.Caption));
                IReadOnlyList<InputAction> actions = group.Actions;
                for (int i = 0; i < actions.Count; i++)
                {
                    DrawKeybind(dialog, actions[i]);
                }
            }
        }

        /// <summary>One gesture's row. The "+" arms the mod's capture and the chip is redrawn once
        /// the new chord is in force; the chip of an overridden gesture is the game's "remove"
        /// button and clears the override. The tooltips are the game's own Controls-page words
        /// (<c>Hotkeys/Add</c> and <c>Hotkeys/Remove</c>, as <c>OptionsMenuKeyBindContent.Draw</c>
        /// uses them).</summary>
        private static void DrawKeybind(ModDialog dialog, InputAction action)
        {
            IUIKeyBinding widget = null;
            widget = dialog.AddKeyBinding(
                action.Label,
                GameText.Get("Hotkeys/Add", null),
                () => ModKeyCapture.Rebind(action, () => ShowBinding(dialog, widget, action)),
                action.GetDescription != null ? action.GetDescription() : null);
            ShowBinding(dialog, widget, action);
        }

        /// <summary>Draw the chip for the gesture's first effective binding, or an empty chip (read
        /// as "not bound") where it has none.</summary>
        private static void ShowBinding(ModDialog dialog, IUIKeyBinding widget, InputAction action)
        {
            IReadOnlyList<InputBinding> bindings = action.Bindings;
            string chord = bindings != null && bindings.Count > 0 ? ChordNames.Of(bindings[0]) : null;
            dialog.ShowBinding(
                widget,
                action.Key,
                chord ?? string.Empty,
                action.HasOverride,
                GameText.Get("Hotkeys/Remove", null),
                () =>
                {
                    ModSettings.ClearKeybindOverride(action.Key);
                    ShowBinding(dialog, widget, action);
                });
        }

        private void AddAnnouncementOrder(ModString label, AnnouncementGroupDefinition group)
        {
            _dialog.AddButton(ModText.Get(label), () => ModOptionsDialogs.OpenAnnouncementOrder(group));
        }

        private void AddCustomCategories(ScannerTaxonomy taxonomy, ModString contextLabel)
        {
            _dialog.AddButton(
                ModText.Get(ModStrings.Screens.CustomCategories, ModText.Get(contextLabel)),
                () => ModOptionsDialogs.OpenCustomCategories(taxonomy, contextLabel));
        }

    }
}
