using UnityEngine.InputSystem;
using System.Collections.Generic;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Input
{
    // If a new widget uses the same key (such as the arrow keys) as an existing widget, do not reuse the previous widget's action in the new widget
    // Instead, create new semantic actions for the new widget.
    // For example, the map navigation and menu widgets both use the arrow keys, but different actions
    internal static class AccessibilityActions
    {
        // Global actions are available on every accessibility screen. The input
        // router checks screen-claimed actions first, then global actions, so
        // screens can own keys before global fallbacks see them.
        public static readonly InputAction TooltipActionsMenu = OneShot("tooltip_actions_menu", ModStrings.Actions.TooltipActionsMenu, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Backquote));

        public static readonly InputAction SummarizeResources = OneShot("summarize_resources", ModStrings.Actions.SummarizeResources, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.R, ctrl: true));

        public static readonly InputAction SummarizeEnemyResources = OneShot("summarize_enemy_resources", ModStrings.Actions.SummarizeEnemyResources, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.R, alt: true));

        public static readonly InputAction OpenModSettings = OneShot("open_mod_settings", ModStrings.Actions.OpenModSettings, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.M, ctrl: true));

        public static readonly InputAction PreviousBuffer = OneShot("previous_buffer", ModStrings.Actions.PreviousBuffer, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.LeftArrow, ctrl: true));

        public static readonly InputAction NextBuffer = OneShot("next_buffer", ModStrings.Actions.NextBuffer, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.RightArrow, ctrl: true));

        public static readonly InputAction PreviousBufferLine = OneShot("previous_buffer_line", ModStrings.Actions.PreviousBufferLine, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.UpArrow, ctrl: true));

        public static readonly InputAction NextBufferLine = OneShot("next_buffer_line", ModStrings.Actions.NextBufferLine, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.DownArrow, ctrl: true));

        public static readonly InputAction FirstBufferLine = OneShot("first_buffer_line", ModStrings.Actions.FirstBufferLine, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Home, ctrl: true));

        public static readonly InputAction LastBufferLine = OneShot("last_buffer_line", ModStrings.Actions.LastBufferLine, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.End, ctrl: true));

        public static readonly InputAction[] GLOBAL_ACTIONS =
        {
            TooltipActionsMenu,
            SummarizeResources,
            SummarizeEnemyResources,
            OpenModSettings,
            PreviousBuffer,
            NextBuffer,
            PreviousBufferLine,
            NextBufferLine,
            FirstBufferLine,
            LastBufferLine
        };

        public static bool IsGlobalAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            for (int i = 0; i < GLOBAL_ACTIONS.Length; i++)
            {
                InputAction globalAction = GLOBAL_ACTIONS[i];
                if (globalAction != null && globalAction.Key == action.Key)
                {
                    return true;
                }
            }

            return false;
        }

        public static readonly InputAction NextWidget = OneShot("next_widget", ModStrings.Actions.NextWidget, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Tab));

        public static readonly InputAction PreviousWidget = OneShot("previous_widget", ModStrings.Actions.PreviousWidget, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Tab, shift: true));

        public static readonly InputAction NextMenuItem = OneShot("next_menu_item", ModStrings.Actions.NextMenuItem, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction PreviousMenuItem = OneShot("previous_menu_item", ModStrings.Actions.PreviousMenuItem, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction FirstMenuItem = OneShot("first_menu_item", ModStrings.Actions.FirstMenuItem, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction LastMenuItem = OneShot("last_menu_item", ModStrings.Actions.LastMenuItem, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction NextHeading = OneShot("next_heading", ModStrings.Actions.NextHeading, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.H));

        public static readonly InputAction PreviousHeading = OneShot("previous_heading", ModStrings.Actions.PreviousHeading, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.H, shift: true));

        public static readonly InputAction MapMoveNorth = OneShot("map_move_north", ModStrings.Actions.MapMoveNorth, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction MapMoveSouth = OneShot("map_move_south", ModStrings.Actions.MapMoveSouth, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction MapMoveWest = OneShot("map_move_west", ModStrings.Actions.MapMoveWest, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction MapMoveEast = OneShot("map_move_east", ModStrings.Actions.MapMoveEast, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction MapSkipNorth = OneShot("map_skip_north", ModStrings.Actions.MapMoveNorth, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.UpArrow, shift: true));

        public static readonly InputAction MapSkipSouth = OneShot("map_skip_south", ModStrings.Actions.MapMoveSouth, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.DownArrow, shift: true));

        public static readonly InputAction MapSkipWest = OneShot("map_skip_west", ModStrings.Actions.MapMoveWest, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.LeftArrow, shift: true));

        public static readonly InputAction MapSkipEast = OneShot("map_skip_east", ModStrings.Actions.MapMoveEast, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.RightArrow, shift: true));

        public static readonly InputAction MapSecondaryAction = OneShot("map_secondary_action", ModStrings.Actions.MapSecondaryAction, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Backslash))
            // A reported keyboard produced a visible backslash but Unity exposed the key as OEM1:
            // key=OEM1 displayName=\ ctrl=False shift=False alt=False. Match only this literal
            // display name here instead of treating OEM1 as a universal backslash key.
            .AddBinding(new KeyboardDisplayNameBinding("\\"));

        public static readonly InputAction NextWielder = OneShot("next_wielder", ModStrings.Actions.NextWielder, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.W));

        public static readonly InputAction NextSettlement = OneShot("next_settlement", ModStrings.Actions.NextSettlement, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.S));

        public static readonly InputAction SummarizeReachableEntities = OneShot("summarize_reachable_entities", ModStrings.Actions.SummarizeReachableEntities, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.A));

        public static readonly InputAction DescribePosition = OneShot("describe_position", ModStrings.Actions.DescribePosition, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.D));

        public static readonly InputAction SonarSweep = OneShot("sonar_sweep", ModStrings.Actions.SonarSweep, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.P));

        public static readonly InputAction FocusHudTroops = OneShot("focus_hud_troops", ModStrings.Actions.FocusHudTroops, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.T));

        public static readonly InputAction FocusHudResources = OneShot("focus_hud_resources", ModStrings.Actions.FocusHudResources, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.R));

        public static readonly InputAction FocusHudObjectives = OneShot("focus_hud_objectives", ModStrings.Actions.FocusHudObjectives, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.B));

        public static readonly InputAction FocusHudNotifications = OneShot("focus_hud_notifications", ModStrings.Actions.FocusHudNotifications, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.N));

        public static readonly InputAction ScannerSearch = OneShot("scanner_search", ModStrings.Scanner.Search, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.F, ctrl: true));

        public static readonly InputAction ScannerPreviousCategory = OneShot("scanner_previous_category", ModStrings.Actions.ScannerPreviousCategory, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageUp, ctrl: true));

        public static readonly InputAction ScannerNextCategory = OneShot("scanner_next_category", ModStrings.Actions.ScannerNextCategory, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageDown, ctrl: true));

        public static readonly InputAction ScannerPreviousSubcategory = OneShot("scanner_previous_subcategory", ModStrings.Actions.ScannerPreviousSubcategory, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageUp, shift: true));

        public static readonly InputAction ScannerNextSubcategory = OneShot("scanner_next_subcategory", ModStrings.Actions.ScannerNextSubcategory, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageDown, shift: true));

        public static readonly InputAction ScannerPreviousItem = OneShot("scanner_previous_item", ModStrings.Actions.ScannerPreviousItem, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageUp));

        public static readonly InputAction ScannerNextItem = OneShot("scanner_next_item", ModStrings.Actions.ScannerNextItem, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageDown));

        public static readonly InputAction ScannerPreviousInstance = OneShot("scanner_previous_instance", ModStrings.Actions.ScannerPreviousInstance, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageUp, alt: true));

        public static readonly InputAction ScannerNextInstance = OneShot("scanner_next_instance", ModStrings.Actions.ScannerNextInstance, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageDown, alt: true));

        public static readonly InputAction ScannerJumpToResult = OneShot("scanner_jump_to_result", ModStrings.Actions.ScannerJumpToResult, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Home))
            // J mirrors Home so the jump sits next to the comma, period, and
            // slash category keys and the pair can be worked with one hand.
            .AddBinding(new KeyboardBinding(Key.J));

        public static readonly InputAction ScannerSpeakDistanceAndDirection = OneShot("scanner_speak_distance_and_direction", ModStrings.Actions.ScannerSpeakDistanceAndDirection, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction ScannerReturnFromJump = OneShot("scanner_return_from_jump", ModStrings.Actions.ScannerReturnFromJump, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Backspace));

        public static readonly InputAction ScannerLookAround = OneShot("scanner_look_around", ModStrings.Actions.ScannerLookAround, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.L));

        public static readonly InputAction ScannerIncreaseLookAroundRadius = OneShot("scanner_increase_look_around_radius", ModStrings.Actions.ScannerIncreaseLookAroundRadius, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.K));

        public static readonly InputAction ScannerDecreaseLookAroundRadius = OneShot("scanner_decrease_look_around_radius", ModStrings.Actions.ScannerDecreaseLookAroundRadius, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.K, shift: true));

        // One key per custom category on the adventure map, with shift walking
        // it backwards. Which category answers to which key is the player's to
        // set, so the actions are named after the keys rather than after any
        // category. Combat binds comma and period to its own troop cycles, and
        // both are claimed by the focused widget, so the two never meet.
        public static readonly InputAction ScannerNextCustomEntryComma =
            CustomEntry("scanner_next_custom_entry_comma", ModStrings.Actions.ScannerNextCustomEntry, ModStrings.Scanner.QuickKeyComma, Key.Comma, shift: false);

        public static readonly InputAction ScannerPreviousCustomEntryComma =
            CustomEntry("scanner_previous_custom_entry_comma", ModStrings.Actions.ScannerPreviousCustomEntry, ModStrings.Scanner.QuickKeyComma, Key.Comma, shift: true);

        public static readonly InputAction ScannerNextCustomEntryPeriod =
            CustomEntry("scanner_next_custom_entry_period", ModStrings.Actions.ScannerNextCustomEntry, ModStrings.Scanner.QuickKeyPeriod, Key.Period, shift: false);

        public static readonly InputAction ScannerPreviousCustomEntryPeriod =
            CustomEntry("scanner_previous_custom_entry_period", ModStrings.Actions.ScannerPreviousCustomEntry, ModStrings.Scanner.QuickKeyPeriod, Key.Period, shift: true);

        public static readonly InputAction ScannerNextCustomEntrySlash =
            CustomEntry("scanner_next_custom_entry_slash", ModStrings.Actions.ScannerNextCustomEntry, ModStrings.Scanner.QuickKeySlash, Key.Slash, shift: false);

        public static readonly InputAction ScannerPreviousCustomEntrySlash =
            CustomEntry("scanner_previous_custom_entry_slash", ModStrings.Actions.ScannerPreviousCustomEntry, ModStrings.Scanner.QuickKeySlash, Key.Slash, shift: true);

        public static readonly InputAction SliderDecrease = OneShot("slider_decrease", ModStrings.Actions.SliderDecrease, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction SliderIncrease = OneShot("slider_increase", ModStrings.Actions.SliderIncrease, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction SliderMinimum = OneShot("slider_minimum", ModStrings.Actions.SliderMinimum, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction SliderMaximum = OneShot("slider_maximum", ModStrings.Actions.SliderMaximum, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction PreviousRow = OneShot("previous_row", ModStrings.Actions.PreviousRow, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction NextRow = OneShot("next_row", ModStrings.Actions.NextRow, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction PreviousColumn = OneShot("previous_column", ModStrings.Actions.PreviousColumn, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction NextColumn = OneShot("next_column", ModStrings.Actions.NextColumn, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction FirstRow = OneShot("first_row", ModStrings.Actions.FirstRow, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction LastRow = OneShot("last_row", ModStrings.Actions.LastRow, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction Activate = OneShot("activate", ModStrings.Actions.Activate, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Enter))
            .AddBinding(new KeyboardBinding(Key.NumpadEnter));

        public static readonly InputAction StartDrag = OneShot("start_drag", ModStrings.Actions.StartDrag, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Space));

        public static readonly InputAction Cancel = OneShot("cancel", ModStrings.Actions.Cancel, InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Escape));

        public static readonly InputAction HexGridWest = OneShot("hex_grid_west", ModStrings.Actions.HexGridWest, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.A));

        public static readonly InputAction HexGridEast = OneShot("hex_grid_east", ModStrings.Actions.HexGridEast, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.D));

        public static readonly InputAction HexGridNorthWest = OneShot("hex_grid_north_west", ModStrings.Actions.HexGridNorthWest, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Q));

        public static readonly InputAction HexGridNorthEast = OneShot("hex_grid_north_east", ModStrings.Actions.HexGridNorthEast, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.E));

        public static readonly InputAction HexGridSouthWest = OneShot("hex_grid_south_west", ModStrings.Actions.HexGridSouthWest, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Z));

        public static readonly InputAction HexGridSouthEast = OneShot("hex_grid_south_east", ModStrings.Actions.HexGridSouthEast, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.C));

        public static readonly InputAction HexGridFocusCenterTile = OneShot("hex_grid_focus_center_tile", ModStrings.Actions.HexGridFocusCenterTile, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Space, ctrl: true));

        public static readonly InputAction HexGridSkipWest = OneShot("hex_grid_skip_west", ModStrings.Actions.HexGridWest, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.A, shift: true));

        public static readonly InputAction HexGridSkipEast = OneShot("hex_grid_skip_east", ModStrings.Actions.HexGridEast, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.D, shift: true));

        public static readonly InputAction HexGridSkipNorthWest = OneShot("hex_grid_skip_north_west", ModStrings.Actions.HexGridNorthWest, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Q, shift: true));

        public static readonly InputAction HexGridSkipNorthEast = OneShot("hex_grid_skip_north_east", ModStrings.Actions.HexGridNorthEast, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.E, shift: true));

        public static readonly InputAction HexGridSkipSouthWest = OneShot("hex_grid_skip_south_west", ModStrings.Actions.HexGridSouthWest, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Z, shift: true));

        public static readonly InputAction HexGridSkipSouthEast = OneShot("hex_grid_skip_south_east", ModStrings.Actions.HexGridSouthEast, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.C, shift: true));

        public static readonly InputAction CombatInspect = OneShot("combat_inspect", ModStrings.Actions.CombatInspect, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.I));

        public static readonly InputAction CombatNextActingTroop = OneShot("combat_next_acting_troop", ModStrings.Actions.CombatNextActingTroop, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Comma));

        public static readonly InputAction CombatPreviousActingTroop = OneShot("combat_previous_acting_troop", ModStrings.Actions.CombatPreviousActingTroop, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Comma, shift: true));

        public static readonly InputAction CombatFocusActingTroop = OneShot("combat_focus_acting_troop", ModStrings.Actions.CombatFocusActingTroop, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Space));

        public static readonly InputAction CombatNextEnemyTroop = OneShot("combat_next_enemy_troop", ModStrings.Actions.CombatNextEnemyTroop, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Period));

        public static readonly InputAction CombatPreviousEnemyTroop = OneShot("combat_previous_enemy_troop", ModStrings.Actions.CombatPreviousEnemyTroop, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Period, shift: true));

        public static readonly InputAction CombatNextRelevantTile = OneShot("combat_next_relevant_tile", ModStrings.Actions.CombatNextRelevantTile, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.W));

        public static readonly InputAction CombatPreviousRelevantTile = OneShot("combat_previous_relevant_tile", ModStrings.Actions.CombatPreviousRelevantTile, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.W, shift: true));

        public static readonly InputAction CombatFocusTimeline = OneShot("combat_focus_timeline", ModStrings.Actions.CombatFocusTimeline, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.T));

        public static readonly InputAction ReadThreat = OneShot("read_threat", ModStrings.Actions.ReadThreat, InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.S));

        public static readonly InputAction[] SaveBookmarks =
            CreateBookmarkActions("save_bookmark", "SaveBookmark", ctrl: true, shift: false, alt: false);

        public static readonly InputAction[] JumpToBookmarks =
            CreateBookmarkActions("jump_to_bookmark", "JumpToBookmark", ctrl: false, shift: true, alt: false);

        public static readonly InputAction[] SpeakBookmarkDirections =
            CreateBookmarkActions("speak_bookmark_direction", "SpeakBookmarkDirection", ctrl: false, shift: false, alt: true);

        public static readonly InputAction[] ToggleBookmarkBeacons =
            CreateBookmarkActions("toggle_bookmark_beacon", "ToggleBookmarkBeacon", ctrl: true, shift: true, alt: false);

        public static readonly InputAction[] NON_GLOBAL_ACTIONS = BuildNonGlobalActions();

        private static InputAction OneShot(string key, ModString label, InputClaimScope claimScope)
        {
            return new InputAction(key, () => ModText.Get(label), claimScope, InputRepeatPolicy.OneShotUntilRelease());
        }

        private static InputAction OneShot(string key, string label, InputClaimScope claimScope)
        {
            return new InputAction(key, label, claimScope, InputRepeatPolicy.OneShotUntilRelease());
        }

        /// <summary>
        /// The label names the key the action is bound to, because the category
        /// it walks is whichever one the player put on that key and can change
        /// between one reading of the label and the next.
        /// </summary>
        private static InputAction CustomEntry(string key, ModString label, ModString keyName, Key boundKey, bool shift)
        {
            return new InputAction(
                    key,
                    () => ModText.Get(label, ModText.Get(keyName)),
                    InputClaimScope.FocusedWidget,
                    InputRepeatPolicy.OneShotUntilRelease())
                .AddBinding(new KeyboardBinding(boundKey, shift: shift));
        }

        private static InputAction[] CreateBookmarkActions(string keyPrefix, string labelPrefix, bool ctrl, bool shift, bool alt)
        {
            Key[] digitKeys =
            {
                Key.Digit1,
                Key.Digit2,
                Key.Digit3,
                Key.Digit4,
                Key.Digit5,
                Key.Digit6,
                Key.Digit7,
                Key.Digit8,
                Key.Digit9,
                Key.Digit0
            };
            InputAction[] actions = new InputAction[AdventureBookmarkSlots.All.Length];
            for (int i = 0; i < AdventureBookmarkSlots.All.Length; i++)
            {
                string slot = AdventureBookmarkSlots.All[i];
                actions[i] = OneShot(keyPrefix + "_" + slot, labelPrefix + slot, InputClaimScope.FocusedWidget)
                    .AddBinding(new KeyboardBinding(digitKeys[i], ctrl, shift, alt));
            }

            return actions;
        }

        /// <summary>The action a dev-server caller named, or null. NON_GLOBAL_ACTIONS already
        /// carries the bookmark arrays, so these two arrays are the whole vocabulary.</summary>
        public static InputAction FindByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            InputAction found = FindByKey(GLOBAL_ACTIONS, key);
            return found ?? FindByKey(NON_GLOBAL_ACTIONS, key);
        }

        /// <summary>Every action key, comma separated - what a caller who named an action we do not
        /// have is told it could have said instead.</summary>
        public static string AllKeys()
        {
            List<string> keys = new List<string>(GLOBAL_ACTIONS.Length + NON_GLOBAL_ACTIONS.Length);
            AddKeys(keys, GLOBAL_ACTIONS);
            AddKeys(keys, NON_GLOBAL_ACTIONS);
            return string.Join(", ", keys.ToArray());
        }

        private static InputAction FindByKey(InputAction[] actions, string key)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                InputAction action = actions[i];
                if (action != null && action.Key == key)
                {
                    return action;
                }
            }

            return null;
        }

        private static void AddKeys(List<string> keys, InputAction[] actions)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] != null)
                {
                    keys.Add(actions[i].Key);
                }
            }
        }

        private static InputAction[] BuildNonGlobalActions()
        {
            List<InputAction> actions = new List<InputAction>
            {
                NextWidget,
                PreviousWidget,
                NextMenuItem,
                PreviousMenuItem,
                FirstMenuItem,
                LastMenuItem,
                NextHeading,
                PreviousHeading,
                MapMoveNorth,
                MapMoveSouth,
                MapMoveWest,
                MapMoveEast,
                MapSkipNorth,
                MapSkipSouth,
                MapSkipWest,
                MapSkipEast,
                MapSecondaryAction,
                NextWielder,
                NextSettlement,
                SummarizeReachableEntities,
                DescribePosition,
                SonarSweep,
                FocusHudTroops,
                FocusHudResources,
                FocusHudObjectives,
                FocusHudNotifications,
                ScannerSearch,
                ScannerPreviousCategory,
                ScannerNextCategory,
                ScannerPreviousSubcategory,
                ScannerNextSubcategory,
                ScannerPreviousItem,
                ScannerNextItem,
                ScannerPreviousInstance,
                ScannerNextInstance,
                ScannerJumpToResult,
                ScannerSpeakDistanceAndDirection,
                ScannerReturnFromJump,
                ScannerLookAround,
                ScannerIncreaseLookAroundRadius,
                ScannerDecreaseLookAroundRadius,
                ScannerNextCustomEntryComma,
                ScannerPreviousCustomEntryComma,
                ScannerNextCustomEntryPeriod,
                ScannerPreviousCustomEntryPeriod,
                ScannerNextCustomEntrySlash,
                ScannerPreviousCustomEntrySlash,
                SliderDecrease,
                SliderIncrease,
                SliderMinimum,
                SliderMaximum,
                PreviousRow,
                NextRow,
                PreviousColumn,
                NextColumn,
                FirstRow,
                LastRow,
                HexGridWest,
                HexGridEast,
                HexGridNorthWest,
                HexGridNorthEast,
                HexGridSouthWest,
                HexGridSouthEast,
                HexGridFocusCenterTile,
                HexGridSkipWest,
                HexGridSkipEast,
                HexGridSkipNorthWest,
                HexGridSkipNorthEast,
                HexGridSkipSouthWest,
                HexGridSkipSouthEast,
                CombatInspect,
                CombatNextActingTroop,
                CombatPreviousActingTroop,
                CombatFocusActingTroop,
                CombatNextEnemyTroop,
                CombatPreviousEnemyTroop,
                CombatNextRelevantTile,
                CombatPreviousRelevantTile,
                CombatFocusTimeline,
                ReadThreat,
                StartDrag,
                Activate,
                Cancel
            };

            actions.AddRange(SaveBookmarks);
            actions.AddRange(JumpToBookmarks);
            actions.AddRange(SpeakBookmarkDirections);
            actions.AddRange(ToggleBookmarkBeacons);
            return actions.ToArray();
        }
    }
}
