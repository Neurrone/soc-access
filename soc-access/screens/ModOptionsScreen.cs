using System;
using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
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

        /// <summary>The index of the Bookmarks tab in <see cref="TabLabels"/> - the one page that
        /// holds no setting, only what is on disk now and the three things that can be done with it.
        /// </summary>
        private const int BookmarksTab = 7;

        /// <summary>The index of the Help tab in <see cref="TabLabels"/> - three links out of the
        /// game, and last for that reason.</summary>
        private const int HelpTab = 8;

        public const string HomepageUrl = "https://neurrone.github.io/soc-access/intro.html";
        public const string DiscordUrl = "https://discord.gg/4wgAFFyPCH";
        public const string PatreonUrl = "https://patreon.com/NeurronesMods";

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

        /// <summary>Whatever took the window away - Escape, the close button, the page underneath
        /// closing, a scene taking the canvas with it - disarms a capture armed on the Keybinds tab.
        /// The player has no cancel, so the window going is the only way out of an armed-but-unwanted
        /// capture, and a capture left armed has the router eat the next key pressed anywhere.
        /// </summary>
        public override void OnPop()
        {
            ModKeyCapture.Cancel();
            base.OnPop();
        }

        public bool Close()
        {
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
            ModStrings.Screens.Keybinds,
            ModStrings.Screens.Bookmarks,
            ModStrings.Screens.Help
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
                case BookmarksTab:
                    DrawBookmarks();
                    break;
                case HelpTab:
                    DrawHelp();
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
            // First in the tab because it changes the order every scanner readout on the map comes
            // in. It is a map row and not a scanner one because the walk it measures is the
            // adventure map's; the battle scanner has no pathfinder behind it.
            _dialog.AddDropdown(
                ModText.Get(ModStrings.Screens.ScannerResultOrder),
                new List<UITextMeshDropdown.Option>
                {
                    new UITextMeshDropdown.Option(ModText.Get(ModStrings.Screens.ScannerResultOrderStraightLine)),
                    new UITextMeshDropdown.Option(ModText.Get(ModStrings.Screens.ScannerResultOrderWalkablePath))
                },
                ModSettings.ScannerSortsByWalkablePath ? 1 : 0,
                index => ModSettings.SetScannerResultOrder(
                    index == 1 ? ScannerResultOrders.WalkablePath : ScannerResultOrders.StraightLine));
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

        /// <summary>The Help tab: where to read about the mod, where to ask about it, and where to
        /// support it. Each row is an ordinary button, so a mouse works them too, and each hands its
        /// address to whatever the system opens links with.</summary>
        private void DrawHelp()
        {
            _dialog.AddButton(ModText.Get(ModStrings.Screens.ModHomepage), () => OpenUrl(HomepageUrl));
            _dialog.AddButton(ModText.Get(ModStrings.Screens.JoinDiscordServer), () => OpenUrl(DiscordUrl));
            _dialog.AddButton(ModText.Get(ModStrings.Screens.SupportOnPatreon), () => OpenUrl(PatreonUrl));
        }

        private static void OpenUrl(string url)
        {
            try
            {
                UnityEngine.Application.OpenURL(url);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to open " + url + ": " + exception.Message);
            }
        }

        /// <summary>
        /// The Bookmarks tab: where this game's bookmarks are kept, and the three things that can be
        /// done with the file. Nothing on the page is a setting, so it is what the disk and the game
        /// said when it was drawn - once per tab switch and once per opening of the window, never
        /// per frame.
        /// </summary>
        private void DrawBookmarks()
        {
            AdventureBookmarkStore store = new AdventureBookmarkStore();
            AdventureBookmarkGameIdentity identity = GameBeingPlayed();
            bool hasFile = store.Exists(identity);
            // Off a game there is no file to name: the import and the folder are all this page is.
            if (identity != null)
            {
                // A line of its own: where the file is, or that there is none, is not the name of
                // the buttons under it.
                _dialog.AddText(
                    hasFile
                        ? ModText.Get(ModStrings.Screens.BookmarksSavedTo, store.GetPath(identity))
                        : ModText.Get(ModStrings.Screens.NoBookmarksForThisGame),
                    standalone: true);
            }

            if (hasFile)
            {
                _dialog.AddButton(
                    ModText.Get(ModStrings.Screens.CopyBookmarksToClipboard),
                    () => CopyBookmarks(store, identity));
            }

            _dialog.AddButton(
                ModText.Get(ModStrings.Screens.ImportBookmarksFromClipboard),
                () => ImportBookmarks(store));

            if (store.FolderHasFiles())
            {
                _dialog.AddButton(
                    ModText.Get(ModStrings.Screens.OpenBookmarksFolder),
                    () => OpenBookmarksFolder(store));
            }
        }

        /// <summary>The game being played, or null on the main menu and anywhere else the adventure
        /// map is not up. Read from the map screen's own live adapter each time it is asked for; the
        /// window outlives games, so nothing about one is kept here.</summary>
        private static AdventureBookmarkGameIdentity GameBeingPlayed()
        {
            ScreenManager manager = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            AdventureMapScreen map = manager == null ? null : manager.Registered<AdventureMapScreen>();
            return map != null && map.IsActive() ? map.Live.GetBookmarkGameIdentity() : null;
        }

        /// <summary>The file's own text on the clipboard, unchanged and with nothing added, so that
        /// what is copied out is exactly what can be pasted back in.</summary>
        private static void CopyBookmarks(AdventureBookmarkStore store, AdventureBookmarkGameIdentity identity)
        {
            string text;
            bool read = store.TryReadText(identity, out text);
            if (read)
            {
                UnityEngine.GUIUtility.systemCopyBuffer = text;
            }

            Speak(ModText.Get(read ? ModStrings.Screens.BookmarksCopied : ModStrings.Screens.BookmarksNotRead));
        }

        /// <summary>Paste a bookmarks file in. The text says which game it belongs to, so this works
        /// on the main menu and for a game other than the one open, and what it landed on is read in
        /// a dialog rather than spoken past. A file written for the game being played is picked up by
        /// the map itself: the store counts its writes and the map's bookmarks reload on the next
        /// gesture.</summary>
        private static void ImportBookmarks(AdventureBookmarkStore store)
        {
            AdventureBookmarkStore.ImportResult result = store.Import(UnityEngine.GUIUtility.systemCopyBuffer);
            string message;
            switch (result.Outcome)
            {
                case AdventureBookmarkStore.ImportOutcome.Empty:
                    message = ModText.Get(ModStrings.Screens.ClipboardEmpty);
                    break;
                case AdventureBookmarkStore.ImportOutcome.NotBookmarks:
                    message = ModText.Get(ModStrings.Screens.ClipboardNotBookmarks);
                    break;
                case AdventureBookmarkStore.ImportOutcome.WriteFailed:
                    message = ModText.Get(ModStrings.Screens.BookmarksNotWritten);
                    break;
                default:
                    // Which game the file belongs to is the store's business; whether that game is
                    // the one on the screen, and so which of the three answers this is, is the
                    // tab's.
                    AdventureBookmarkGameIdentity playing = GameBeingPlayed();
                    ModPluralString imported;
                    if (playing == null)
                    {
                        imported = ModStrings.Screens.BookmarksImportedForLaterGame;
                    }
                    else if (result.Identity.SameStorageAs(playing))
                    {
                        imported = ModStrings.Screens.BookmarksImported;
                    }
                    else
                    {
                        imported = ModStrings.Screens.BookmarksImportedForOtherGame;
                    }

                    message = ModText.Plural(imported, result.Count, result.Count);
                    break;
            }

            ModOptionsDialogs.OpenMessage(
                "mod-bookmarks-import",
                ModText.Get(ModStrings.Screens.ImportBookmarksFromClipboard),
                message,
                standaloneMessage: true);
        }

        /// <summary>Hand the folder to the desktop. Verified on Windows, where the file manager opens
        /// on it; the other platforms the game ships for are unverified.</summary>
        private static void OpenBookmarksFolder(AdventureBookmarkStore store)
        {
            try
            {
                System.Diagnostics.Process.Start(store.Folder);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to open the bookmarks folder: " + exception.Message);
            }
        }

        private static void Speak(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }
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
