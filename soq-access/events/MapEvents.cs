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
        public Vector2Int Tile { get; private set; }

        public string Message { get; private set; }

        public string GetSpeechText()
        {
            return "Mod error: " + Message;
        }
    }

    internal sealed class MapHudVisibilityChangedEvent : IAccessibilityEvent
    {
        public MapHudVisibilityChangedEvent(bool isVisible)
        {
            IsVisible = isVisible;
        }

        public string Kind { get { return AccessibilityEvents.Map.HudVisibilityChanged; } }

        public bool IsVisible { get; private set; }

        public string GetSpeechText()
        {
            return IsVisible ? "HUD open" : "HUD closed";
        }
    }

    internal sealed class MapCameraFocusEvent : IAccessibilityEvent
    {
        public MapCameraFocusEvent(Vector2Int tile)
            : this(tile, announce: false)
        {
        }

        public MapCameraFocusEvent(Vector2Int tile, bool announce)
        {
            Tile = tile;
            Announce = announce;
        }

        public string Kind { get { return AccessibilityEvents.Map.CameraFocus; } }

        public Vector2Int Tile { get; private set; }

        public bool Announce { get; private set; }

        public string GetSpeechText()
        {
            return Announce ? "Map camera focuses on (" + Tile.x + ", " + Tile.y + ")" : string.Empty;
        }
    }

    internal sealed class MapRoundChangedEvent : IAccessibilityEvent
    {
        public MapRoundChangedEvent(string roundLabel)
        {
            RoundLabel = roundLabel;
        }

        public string Kind { get { return AccessibilityEvents.Map.RoundChanged; } }

        public string RoundLabel { get; private set; }

        public string GetSpeechText()
        {
            return RoundLabel ?? string.Empty;
        }
    }
}
