using System;
using System.Collections.Generic;
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
        private const string KeybindsStop = "mod-options-keybinds";
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

            builder.BeginStop(RowsStop);
            _rows.BuildRows(builder, _dialog.Rows);

            // The Keybinds tab draws no game controls but its own reset-all button; its rows are the
            // mod's gestures, a table of their own after that button, the way the Options window's
            // Controls page puts its rebind table after its reset-all.
            if (IsTabSelected(KeybindsTab))
            {
                builder.BeginStop(KeybindsStop);
                BuildKeybinds(builder);
            }

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

        /// <summary>The Keybinds tab. The only game control it draws is the reset-all button; the
        /// gestures themselves are a table built in <see cref="BuildKeybinds"/>, since the mod's
        /// gestures are not game input actions the cloned factory could draw as key-binding widgets.
        /// </summary>
        private void DrawKeybinds()
        {
            _dialog.AddButton(
                ModText.Get(ModStrings.Screens.ResetAllToDefaults),
                ModSettings.ClearAllKeybindOverrides);
        }

        private bool IsTabSelected(int index)
        {
            IReadOnlyList<ModDialog.Tab> tabs = _dialog != null ? _dialog.Tabs : null;
            ModDialog.Tab tab = tabs != null && index >= 0 && index < tabs.Count ? tabs[index] : null;
            return tab != null && tab.IsSelected();
        }

        /// <summary>
        /// The mod's own gestures as the same three-column table the Options window's Controls page
        /// uses: one region per catalog group, one row per gesture with a name cell, a binding chip
        /// and a "+". The chip reads the current hotkey (or "unbound") and, where the gesture is
        /// overridden, is a button that restores the default; the "+" starts the mod's own capture.
        /// The reading and the cell shapes mirror <see cref="MenuFormNodes.BuildKeyBindingSheet"/> -
        /// only the capture and clear are the mod's rather than the game's.
        /// </summary>
        private void BuildKeybinds(GraphBuilder builder)
        {
            GraphSheet sheet = new GraphSheet(builder, "mod-options:keybind:");
            IReadOnlyList<ModGestureCatalog.Group> groups = ModGestureCatalog.Groups;
            for (int g = 0; g < groups.Count; g++)
            {
                ModGestureCatalog.Group group = groups[g];
                sheet.Region(ModText.Get(group.Caption), new string[3]);
                IReadOnlyList<InputAction> actions = group.Actions;
                for (int i = 0; i < actions.Count; i++)
                {
                    AddKeybindRow(sheet, actions[i]);
                }
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                builder.LandStopOn(sheet.FirstRow);
            }
        }

        private static void AddKeybindRow(GraphSheet sheet, InputAction action)
        {
            NodeVtable name = GraphNodes.Text(() => action.Label);
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>
            {
                new GraphSheet.SheetCell(1, 0, ChipCell(action)),
                new GraphSheet.SheetCell(2, 0, PlusCell(action)),
            };
            // Keyed on the action itself - a stable identity for the whole mod load - so the cursor
            // holds its row across rebuilds. No drawn widget: these rows are the mod's, not the game's.
            sheet.RowAt(name, action, cells);
        }

        /// <summary>The binding chip: the gesture's current chord, or "unbound". A BUTTON that clears
        /// the override where the gesture has one - a plain read-only cell otherwise - with its text
        /// watched live so a rebind or clear is spoken with no polling.</summary>
        private static NodeVtable ChipCell(InputAction action)
        {
            Func<string> text = () => KeyBindingText.Display(
                CurrentChord(action), ModText.Get(ModStrings.Screens.KeybindUnbound));

            NodeVtable vtable;
            if (action.HasOverride)
            {
                vtable = GraphNodes.Button(text, () => ModSettings.ClearKeybindOverride(action.Key));
                NodeHints.Add(vtable, ModStrings.Screens.KeyBindingClearHint, AccessibilityActions.UiLeftClick.Key, 0);
            }
            else
            {
                vtable = GraphNodes.Text(text);
            }

            if (vtable.Announcements != null && vtable.Announcements.Count > 0)
            {
                vtable.Announcements[0].Live = true;
            }

            vtable.SearchText = () => action.Label;
            return vtable;
        }

        /// <summary>The "+" cell: arms the mod's capture for this gesture.</summary>
        private static NodeVtable PlusCell(InputAction action)
        {
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.KeybindRebind),
                () => ModKeyCapture.Rebind(action));
            NodeHints.Add(vtable, ModStrings.Screens.KeyBindingSetHint, AccessibilityActions.UiLeftClick.Key, 0);
            vtable.SearchText = () => action.Label;
            return vtable;
        }

        /// <summary>The chord the gesture's first effective binding reads as, or null when it has
        /// none - the primary hotkey the chip shows.</summary>
        private static string CurrentChord(InputAction action)
        {
            IReadOnlyList<InputBinding> bindings = action.Bindings;
            return bindings != null && bindings.Count > 0 ? ChordNames.Of(bindings[0]) : null;
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
