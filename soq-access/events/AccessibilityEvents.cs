namespace SongsOfConquestAccess.Events
{
    internal static class AccessibilityEvents
    {
        public static class Map
        {
            public const string WielderSelected = "map.wielder.selected";
            public const string WielderUnselected = "map.wielder.unselected";
            public const string WielderMoved = "map.wielder.moved";
            public const string EntitySelected = "map.entity.selected";
            public const string EntityUnselected = "map.entity.unselected";
            public const string DestinationSet = "map.destination.set";
            public const string DestinationCleared = "map.destination.cleared";
            public const string ActionFailed = "map.action.failed";
        }

        public static class Notification
        {
            public const string WorldReward = "notification.world.reward";
            public const string WorldMessage = "notification.world.message";
            public const string DeniedMove = "notification.denied_move";
            public const string DeniedEntityInteraction = "notification.denied_entity_interaction";
        }
    }
}
