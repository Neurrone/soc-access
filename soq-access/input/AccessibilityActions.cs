using UnityEngine.InputSystem;

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
        public static readonly InputAction TooltipActionsMenu = OneShot("tooltip_actions_menu", "Tooltip Actions Menu", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.F10, shift: true));

        public static readonly InputAction[] GLOBAL_ACTIONS =
        {
            TooltipActionsMenu
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

        public static readonly InputAction NextWidget = OneShot("next_widget", "Next Widget", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Tab));

        public static readonly InputAction PreviousWidget = OneShot("previous_widget", "Previous Widget", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Tab, shift: true));

        public static readonly InputAction NextMenuItem = OneShot("next_menu_item", "Next Menu Item", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction PreviousMenuItem = OneShot("previous_menu_item", "Previous Menu Item", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction FirstMenuItem = OneShot("first_menu_item", "First Menu Item", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction LastMenuItem = OneShot("last_menu_item", "Last Menu Item", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction MapMoveNorth = OneShot("map_move_north", "Map Move North", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction MapMoveSouth = OneShot("map_move_south", "Map Move South", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction MapMoveWest = OneShot("map_move_west", "Map Move West", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction MapMoveEast = OneShot("map_move_east", "Map Move East", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction MapSecondaryAction = OneShot("map_secondary_action", "Map Secondary Action", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Backslash));

        public static readonly InputAction NextWielder = OneShot("next_wielder", "Next Wielder", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.W));

        public static readonly InputAction NextSettlement = OneShot("next_settlement", "Next Settlement", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.S));

        public static readonly InputAction ScannerRefresh = OneShot("scanner_refresh", "Refresh Scanner", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction ScannerPreviousCategory = OneShot("scanner_previous_category", "Previous Scanner Category", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageUp, ctrl: true));

        public static readonly InputAction ScannerNextCategory = OneShot("scanner_next_category", "Next Scanner Category", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageDown, ctrl: true));

        public static readonly InputAction ScannerPreviousSubcategory = OneShot("scanner_previous_subcategory", "Previous Scanner Subcategory", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageUp, shift: true));

        public static readonly InputAction ScannerNextSubcategory = OneShot("scanner_next_subcategory", "Next Scanner Subcategory", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageDown, shift: true));

        public static readonly InputAction ScannerPreviousResult = OneShot("scanner_previous_result", "Previous Scanner Result", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageUp));

        public static readonly InputAction ScannerNextResult = OneShot("scanner_next_result", "Next Scanner Result", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.PageDown));

        public static readonly InputAction ScannerJumpToResult = OneShot("scanner_jump_to_result", "Jump To Scanner Result", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction ScannerSpeakOrientation = OneShot("scanner_speak_orientation", "Scanner Result Orientation", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Home, shift: true));

        public static readonly InputAction SliderDecrease = OneShot("slider_decrease", "Slider Decrease", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction SliderIncrease = OneShot("slider_increase", "Slider Increase", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction SliderMinimum = OneShot("slider_minimum", "Slider Minimum", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction SliderMaximum = OneShot("slider_maximum", "Slider Maximum", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction PreviousArmySlot = OneShot("previous_army_slot", "Previous Army Slot", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction NextArmySlot = OneShot("next_army_slot", "Next Army Slot", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction PreviousArmy = OneShot("previous_army", "Previous Army", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction NextArmy = OneShot("next_army", "Next Army", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction Activate = OneShot("activate", "Activate", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Enter))
            .AddBinding(new KeyboardBinding(Key.NumpadEnter));

        public static readonly InputAction StartDrag = OneShot("start_drag", "Start Drag", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Space));

        public static readonly InputAction Cancel = OneShot("cancel", "Cancel", InputClaimScope.Screen)
            .AddBinding(new KeyboardBinding(Key.Escape));

        public static readonly InputAction HexGridWest = OneShot("hex_grid_west", "Hex Grid West", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.A));

        public static readonly InputAction HexGridEast = OneShot("hex_grid_east", "Hex Grid East", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.D));

        public static readonly InputAction HexGridNorthWest = OneShot("hex_grid_north_west", "Hex Grid Northwest", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Q));

        public static readonly InputAction HexGridNorthEast = OneShot("hex_grid_north_east", "Hex Grid Northeast", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.E));

        public static readonly InputAction HexGridSouthWest = OneShot("hex_grid_south_west", "Hex Grid Southwest", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.Z));

        public static readonly InputAction HexGridSouthEast = OneShot("hex_grid_south_east", "Hex Grid Southeast", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.C));

        public static readonly InputAction CombatInspect = OneShot("combat_inspect", "Inspect Combat Hex", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.I));

        public static readonly InputAction CombatNextRelevantTile = OneShot("combat_next_relevant_tile", "Next Relevant Combat Tile", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.W));

        public static readonly InputAction CombatPreviousRelevantTile = OneShot("combat_previous_relevant_tile", "Previous Relevant Combat Tile", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.W, shift: true));

        public static readonly InputAction CombatSummarizeEssence = OneShot("combat_summarize_essence", "Summarize Essence", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.S));

        public static readonly InputAction CombatSummarizeEnemyEssence = OneShot("combat_summarize_enemy_essence", "Summarize Enemy Essence", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.S, alt: true));

        public static readonly InputAction CombatFocusTimeline = OneShot("combat_focus_timeline", "Focus Timeline", InputClaimScope.FocusedWidget)
            .AddBinding(new KeyboardBinding(Key.T));

        public static readonly InputAction[] NON_GLOBAL_ACTIONS =
        {
            NextWidget,
            PreviousWidget,
            NextMenuItem,
            PreviousMenuItem,
            FirstMenuItem,
            LastMenuItem,
            MapMoveNorth,
            MapMoveSouth,
            MapMoveWest,
            MapMoveEast,
            MapSecondaryAction,
            NextWielder,
            NextSettlement,
            ScannerRefresh,
            ScannerPreviousCategory,
            ScannerNextCategory,
            ScannerPreviousSubcategory,
            ScannerNextSubcategory,
            ScannerPreviousResult,
            ScannerNextResult,
            ScannerJumpToResult,
            ScannerSpeakOrientation,
            SliderDecrease,
            SliderIncrease,
            SliderMinimum,
            SliderMaximum,
            PreviousArmySlot,
            NextArmySlot,
            PreviousArmy,
            NextArmy,
            HexGridWest,
            HexGridEast,
            HexGridNorthWest,
            HexGridNorthEast,
            HexGridSouthWest,
            HexGridSouthEast,
            CombatInspect,
            CombatNextRelevantTile,
            CombatPreviousRelevantTile,
            CombatSummarizeEssence,
            CombatSummarizeEnemyEssence,
            CombatFocusTimeline,
            StartDrag,
            Activate,
            Cancel
        };

        private static InputAction OneShot(string key, string label, InputClaimScope claimScope)
        {
            return new InputAction(key, label, claimScope, InputRepeatPolicy.OneShotUntilRelease());
        }
    }
}
