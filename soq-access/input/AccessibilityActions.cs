using UnityEngine.InputSystem;

namespace SongsOfConquestAccess.Input
{
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

        public static readonly InputAction Activate = OneShot("activate", "Activate")
            .AddBinding(new KeyboardBinding(Key.Enter))
            .AddBinding(new KeyboardBinding(Key.NumpadEnter));

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
            Activate,
            Cancel
        };

        private static InputAction OneShot(string key, string label)
        {
            return new InputAction(key, label, InputRepeatPolicy.OneShotUntilRelease());
        }
    }
}
