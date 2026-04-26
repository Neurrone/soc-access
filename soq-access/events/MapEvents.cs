using UnityEngine;

namespace SongsOfConquestAccess.Events
{
    internal sealed class MapWielderSelectedEvent : IAccessibilityEvent
    {
        public MapWielderSelectedEvent(int wielderId, string wielderName, Vector2Int tile)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? "wielder" : wielderName;
            Tile = tile;
        }

        public string Kind { get { return AccessibilityEvents.Map.WielderSelected; } }

        public bool Interrupt { get { return false; } }

        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string GetSpeechText()
        {
            return "Selected wielder " + WielderName;
        }
    }

    internal sealed class MapWielderUnselectedEvent : IAccessibilityEvent
    {
        public MapWielderUnselectedEvent(int wielderId, string wielderName, Vector2Int tile)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? "wielder" : wielderName;
            Tile = tile;
        }

        public string Kind { get { return AccessibilityEvents.Map.WielderUnselected; } }

        public bool Interrupt { get { return false; } }

        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string GetSpeechText()
        {
            return "Unselected wielder " + WielderName;
        }
    }

    internal sealed class MapWielderMovedEvent : IAccessibilityEvent
    {
        public MapWielderMovedEvent(int wielderId, string wielderName, Vector2Int tile)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? "wielder" : wielderName;
            Tile = tile;
        }

        public string Kind { get { return AccessibilityEvents.Map.WielderMoved; } }

        public bool Interrupt { get { return false; } }

        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string GetSpeechText()
        {
            return WielderName + " moved to " + FormatTile(Tile);
        }

        private static string FormatTile(Vector2Int tile)
        {
            return "(" + tile.x + ", " + tile.y + ")";
        }
    }

    internal sealed class MapEntitySelectedEvent : IAccessibilityEvent
    {
        public MapEntitySelectedEvent(int entityId, string entityName, Vector2Int tile)
        {
            EntityId = entityId;
            EntityName = string.IsNullOrWhiteSpace(entityName) ? "entity" : entityName;
            Tile = tile;
        }

        public string Kind { get { return AccessibilityEvents.Map.EntitySelected; } }

        public bool Interrupt { get { return false; } }

        public int EntityId { get; private set; }

        public string EntityName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string GetSpeechText()
        {
            return "Selected " + EntityName;
        }
    }

    internal sealed class MapEntityUnselectedEvent : IAccessibilityEvent
    {
        public MapEntityUnselectedEvent(int entityId, string entityName, Vector2Int tile)
        {
            EntityId = entityId;
            EntityName = string.IsNullOrWhiteSpace(entityName) ? "entity" : entityName;
            Tile = tile;
        }

        public string Kind { get { return AccessibilityEvents.Map.EntityUnselected; } }

        public bool Interrupt { get { return false; } }

        public int EntityId { get; private set; }

        public string EntityName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string GetSpeechText()
        {
            return "Unselected " + EntityName;
        }
    }

    internal sealed class MapDestinationSetEvent : IAccessibilityEvent
    {
        public MapDestinationSetEvent(int wielderId, string wielderName, Vector2Int destination)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? "wielder" : wielderName;
            Destination = destination;
        }

        public string Kind { get { return AccessibilityEvents.Map.DestinationSet; } }

        public bool Interrupt { get { return false; } }

        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Destination { get; private set; }

        public string GetSpeechText()
        {
            return WielderName + "'s destination set to " + FormatTile(Destination);
        }

        private static string FormatTile(Vector2Int tile)
        {
            return "(" + tile.x + ", " + tile.y + ")";
        }
    }

    internal sealed class MapDestinationClearedEvent : IAccessibilityEvent
    {
        public MapDestinationClearedEvent(int wielderId, string wielderName)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? "wielder" : wielderName;
        }

        public string Kind { get { return AccessibilityEvents.Map.DestinationCleared; } }

        public bool Interrupt { get { return false; } }

        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public string GetSpeechText()
        {
            return WielderName + "'s destination cleared.";
        }
    }

    internal sealed class MapActionFailedEvent : IAccessibilityEvent
    {
        // This is an accessibility mod failure, not a native game denial.
        // Native movement/action denials are surfaced from AdventureMenuSystem
        // notification hooks and should keep using those game-authored messages.
        public MapActionFailedEvent(Vector2Int tile, string message)
        {
            Tile = tile;
            Message = message ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Map.ActionFailed; } }

        public bool Interrupt { get { return false; } }

        public Vector2Int Tile { get; private set; }

        public string Message { get; private set; }

        public string GetSpeechText()
        {
            return "Mod error: " + Message;
        }
    }
}
