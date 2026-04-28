using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Input
{
    // If a new widget uses the same key (such as the arrow keys) as an existing widget, do not reuse the previous widget's action in the new widget
    // Instead, create new semantic actions for the new widget.
    // For example, the map navigation and menu widgets both use the arrow keys, but different actions
    internal static class AccessibilityActions
    {
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

        public static readonly InputAction[] All =
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
