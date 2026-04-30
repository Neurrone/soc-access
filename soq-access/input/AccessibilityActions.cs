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
        public static readonly InputAction TooltipActionsMenu = OneShot("tooltip_actions_menu", "Tooltip Actions Menu")
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

        public static readonly InputAction NextWidget = OneShot("next_widget", "Next Widget")
            .AddBinding(new KeyboardBinding(Key.Tab));

        public static readonly InputAction PreviousWidget = OneShot("previous_widget", "Previous Widget")
            .AddBinding(new KeyboardBinding(Key.Tab, shift: true));

        public static readonly InputAction NextMenuItem = OneShot("next_menu_item", "Next Menu Item")
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction PreviousMenuItem = OneShot("previous_menu_item", "Previous Menu Item")
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction FirstMenuItem = OneShot("first_menu_item", "First Menu Item")
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction LastMenuItem = OneShot("last_menu_item", "Last Menu Item")
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction MapMoveNorth = OneShot("map_move_north", "Map Move North")
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction MapMoveSouth = OneShot("map_move_south", "Map Move South")
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction MapMoveWest = OneShot("map_move_west", "Map Move West")
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction MapMoveEast = OneShot("map_move_east", "Map Move East")
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction MapSecondaryAction = OneShot("map_secondary_action", "Map Secondary Action")
            .AddBinding(new KeyboardBinding(Key.Backslash));

        public static readonly InputAction SliderDecrease = OneShot("slider_decrease", "Slider Decrease")
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction SliderIncrease = OneShot("slider_increase", "Slider Increase")
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction SliderMinimum = OneShot("slider_minimum", "Slider Minimum")
            .AddBinding(new KeyboardBinding(Key.Home));

        public static readonly InputAction SliderMaximum = OneShot("slider_maximum", "Slider Maximum")
            .AddBinding(new KeyboardBinding(Key.End));

        public static readonly InputAction PreviousArmySlot = OneShot("previous_army_slot", "Previous Army Slot")
            .AddBinding(new KeyboardBinding(Key.UpArrow));

        public static readonly InputAction NextArmySlot = OneShot("next_army_slot", "Next Army Slot")
            .AddBinding(new KeyboardBinding(Key.DownArrow));

        public static readonly InputAction PreviousArmy = OneShot("previous_army", "Previous Army")
            .AddBinding(new KeyboardBinding(Key.LeftArrow));

        public static readonly InputAction NextArmy = OneShot("next_army", "Next Army")
            .AddBinding(new KeyboardBinding(Key.RightArrow));

        public static readonly InputAction Activate = OneShot("activate", "Activate")
            .AddBinding(new KeyboardBinding(Key.Enter))
            .AddBinding(new KeyboardBinding(Key.NumpadEnter));

        public static readonly InputAction SelectArmyStack = OneShot("select_army_stack", "Select Army Stack")
            .AddBinding(new KeyboardBinding(Key.Space));

        public static readonly InputAction Cancel = OneShot("cancel", "Cancel")
            .AddBinding(new KeyboardBinding(Key.Escape));

        public static readonly InputAction TroopPlacementMoveWest = OneShot("troop_placement_move_west", "Troop Placement Move West")
            .AddBinding(new KeyboardBinding(Key.A));

        public static readonly InputAction TroopPlacementMoveEast = OneShot("troop_placement_move_east", "Troop Placement Move East")
            .AddBinding(new KeyboardBinding(Key.D));

        public static readonly InputAction TroopPlacementMoveNorthWest = OneShot("troop_placement_move_north_west", "Troop Placement Move Northwest")
            .AddBinding(new KeyboardBinding(Key.Q));

        public static readonly InputAction TroopPlacementMoveNorthEast = OneShot("troop_placement_move_north_east", "Troop Placement Move Northeast")
            .AddBinding(new KeyboardBinding(Key.E));

        public static readonly InputAction TroopPlacementMoveSouthWest = OneShot("troop_placement_move_south_west", "Troop Placement Move Southwest")
            .AddBinding(new KeyboardBinding(Key.Z));

        public static readonly InputAction TroopPlacementMoveSouthEast = OneShot("troop_placement_move_south_east", "Troop Placement Move Southeast")
            .AddBinding(new KeyboardBinding(Key.C));

        public static readonly InputAction TroopPlacementNextOwnSpawn = OneShot("troop_placement_next_own_spawn", "Next Own Spawn Point")
            .AddBinding(new KeyboardBinding(Key.W));

        public static readonly InputAction TroopPlacementPreviousOwnSpawn = OneShot("troop_placement_previous_own_spawn", "Previous Own Spawn Point")
            .AddBinding(new KeyboardBinding(Key.W, shift: true));

        public static readonly InputAction TroopPlacementNextEnemySpawn = OneShot("troop_placement_next_enemy_spawn", "Next Enemy Spawn Point")
            .AddBinding(new KeyboardBinding(Key.X));

        public static readonly InputAction TroopPlacementPreviousEnemySpawn = OneShot("troop_placement_previous_enemy_spawn", "Previous Enemy Spawn Point")
            .AddBinding(new KeyboardBinding(Key.X, shift: true));

        public static readonly InputAction TroopPlacementStartDrag = OneShot("troop_placement_start_drag", "Start Troop Placement Drag")
            .AddBinding(new KeyboardBinding(Key.Space));

        public static readonly InputAction TroopPlacementCancelDrag = OneShot("troop_placement_cancel_drag", "Cancel Troop Placement Drag")
            .AddBinding(new KeyboardBinding(Key.Escape));

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
            SliderDecrease,
            SliderIncrease,
            SliderMinimum,
            SliderMaximum,
            PreviousArmySlot,
            NextArmySlot,
            PreviousArmy,
            NextArmy,
            TroopPlacementMoveWest,
            TroopPlacementMoveEast,
            TroopPlacementMoveNorthWest,
            TroopPlacementMoveNorthEast,
            TroopPlacementMoveSouthWest,
            TroopPlacementMoveSouthEast,
            TroopPlacementNextOwnSpawn,
            TroopPlacementPreviousOwnSpawn,
            TroopPlacementNextEnemySpawn,
            TroopPlacementPreviousEnemySpawn,
            TroopPlacementStartDrag,
            TroopPlacementCancelDrag,
            Activate,
            SelectArmyStack,
            Cancel
        };

        private static InputAction OneShot(string key, string label)
        {
            return new InputAction(key, label, InputRepeatPolicy.OneShotUntilRelease());
        }
    }
}
