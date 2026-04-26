namespace SongsOfConquestAccess.Input
{
    internal static class AccessibilityActions
    {
        public static readonly InputAction NextWidget = new InputAction("next_widget", "Next Widget");
        public static readonly InputAction PreviousWidget = new InputAction("previous_widget", "Previous Widget");
        public static readonly InputAction NextMenuItem = new InputAction("next_menu_item", "Next Menu Item");
        public static readonly InputAction PreviousMenuItem = new InputAction("previous_menu_item", "Previous Menu Item");
        public static readonly InputAction FirstMenuItem = new InputAction("first_menu_item", "First Menu Item");
        public static readonly InputAction LastMenuItem = new InputAction("last_menu_item", "Last Menu Item");
        public static readonly InputAction MapMoveNorth = new InputAction("map_move_north", "Map Move North");
        public static readonly InputAction MapMoveSouth = new InputAction("map_move_south", "Map Move South");
        public static readonly InputAction MapMoveWest = new InputAction("map_move_west", "Map Move West");
        public static readonly InputAction MapMoveEast = new InputAction("map_move_east", "Map Move East");
        public static readonly InputAction MapSecondaryAction = new InputAction("map_secondary_action", "Map Secondary Action");
        public static readonly InputAction Activate = new InputAction("activate", "Activate");
        public static readonly InputAction Cancel = new InputAction("cancel", "Cancel");
    }
}
