using System.Collections.Generic;
using UnityEngine;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Events
{
    internal sealed class MapWielderSelectedEvent : IAccessibilityEvent
    {
        public MapWielderSelectedEvent(int wielderId, string wielderName, Vector2Int tile)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? ModText.Get(ModStrings.Events.Wielder) : wielderName;
            Tile = tile;
        }

        public string Kind { get { return AccessibilityEvents.Map.WielderSelected; } }
        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string GetSpeechText()
        {
            return ModText.Get(ModStrings.Events.SelectedWielder, WielderName);
        }
    }

    internal sealed class WielderRecruitedEvent : IAccessibilityEvent
    {
        public WielderRecruitedEvent(int wielderId, string wielderName)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? ModText.Get(ModStrings.Events.Wielder) : wielderName;
        }

        public string Kind { get { return AccessibilityEvents.Notification.WielderRecruited; } }

        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public string GetSpeechText()
        {
            return ModText.Get(ModStrings.Events.RecruitedWielder, WielderName);
        }
    }

    internal sealed class MapWielderMovedEvent : IAccessibilityEvent
    {
        public MapWielderMovedEvent(int wielderId, string wielderName, Vector2Int tile)
            : this(wielderId, wielderName, tile, isLocalWielder: false)
        {
        }

        public MapWielderMovedEvent(int wielderId, string wielderName, Vector2Int tile, bool isLocalWielder)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? ModText.Get(ModStrings.Events.Wielder) : wielderName;
            Tile = tile;
            IsLocalWielder = isLocalWielder;
        }

        public string Kind { get { return AccessibilityEvents.Map.WielderMoved; } }
        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public bool IsLocalWielder { get; private set; }

        public string GetSpeechText()
        {
            return ModText.Get(ModStrings.Events.WielderMoved, WielderName, FormatTile(Tile));
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + ", " + tile.y;
        }
    }

    internal sealed class MapWielderTeleportedEvent : IAccessibilityEvent
    {
        public MapWielderTeleportedEvent(int wielderId, string wielderName, Vector2Int tile, TeleportSource source)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? ModText.Get(ModStrings.Events.Wielder) : wielderName;
            Tile = tile;
            Source = source;
        }

        public string Kind { get { return AccessibilityEvents.Map.WielderTeleported; } }
        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Tile { get; private set; }

        public TeleportSource Source { get; private set; }

        public string GetSpeechText()
        {
            return ModText.Get(ModStrings.Events.WielderTeleported, WielderName, FormatTile(Tile));
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + ", " + tile.y;
        }
    }

    internal sealed class BuildSiteSelectedEvent : IAccessibilityEvent
    {
        public BuildSiteSelectedEvent(int entityId, BuildSiteSize size, Vector2Int tile)
        {
            EntityId = entityId;
            Size = size;
            Tile = tile;
        }

        public string Kind { get { return AccessibilityEvents.Map.BuildSiteSelected; } }
        public int EntityId { get; private set; }

        public BuildSiteSize Size { get; private set; }

        public Vector2Int Tile { get; private set; }

        public string GetSpeechText()
        {
            return ModText.Get(ModStrings.Events.SelectedBuildSite, FormatSize(Size), FormatTile(Tile));
        }

        private static string FormatSize(BuildSiteSize size)
        {
            switch (size)
            {
                case BuildSiteSize.Large:
                    return ModText.Get(ModStrings.Events.BuildSiteLarge);
                case BuildSiteSize.LargeSettlement:
                    return ModText.Get(ModStrings.Events.BuildSiteLargeSettlement);
                case BuildSiteSize.Medium:
                    return ModText.Get(ModStrings.Events.BuildSiteMedium);
                case BuildSiteSize.SmallSettlement:
                    return ModText.Get(ModStrings.Events.BuildSiteSmallSettlement);
                case BuildSiteSize.Town:
                    return ModText.Get(ModStrings.Events.BuildSiteTown);
                default:
                    return ModText.Get(ModStrings.Events.BuildSiteSmall);
            }
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + ", " + tile.y;
        }
    }

    internal sealed class MapDiscoveryRevealedEvent : IAccessibilityEvent
    {
        private readonly List<string> _items;

        public MapDiscoveryRevealedEvent(IReadOnlyList<string> items)
        {
            _items = new List<string>();
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(items[i]))
                {
                    _items.Add(items[i]);
                }
            }
        }

        public string Kind { get { return AccessibilityEvents.Map.DiscoveryRevealed; } }

        public string GetSpeechText()
        {
            if (_items.Count == 0)
            {
                return string.Empty;
            }

            return ModText.Get(ModStrings.Events.Revealed, ModText.JoinList(_items));
        }
    }

    internal sealed class MapWieldersNoLongerVisibleEvent : IAccessibilityEvent
    {
        private readonly List<string> _wielderNames;

        public MapWieldersNoLongerVisibleEvent(IReadOnlyList<string> wielderNames)
        {
            _wielderNames = new List<string>();
            if (wielderNames == null)
            {
                return;
            }

            for (int i = 0; i < wielderNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(wielderNames[i]))
                {
                    _wielderNames.Add(wielderNames[i]);
                }
            }
        }

        public string Kind { get { return AccessibilityEvents.Map.WieldersNoLongerVisible; } }

        public string GetSpeechText()
        {
            if (_wielderNames.Count == 0)
            {
                return string.Empty;
            }

            return ModText.Get(ModStrings.Events.WieldersNoLongerVisible, ModText.JoinList(_wielderNames));
        }
    }

    internal sealed class MapDestinationSetEvent : IAccessibilityEvent
    {
        public MapDestinationSetEvent(int wielderId, string wielderName, Vector2Int destination)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? ModText.Get(ModStrings.Events.Wielder) : wielderName;
            Destination = destination;
        }

        public string Kind { get { return AccessibilityEvents.Map.DestinationSet; } }
        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public Vector2Int Destination { get; private set; }

        public string GetSpeechText()
        {
            return ModText.Get(ModStrings.Events.DestinationSet, WielderName, FormatTile(Destination));
        }

        private static string FormatTile(Vector2Int tile)
        {
            return tile.x + ", " + tile.y;
        }
    }

    internal sealed class MapDestinationClearedEvent : IAccessibilityEvent
    {
        public MapDestinationClearedEvent(int wielderId, string wielderName)
        {
            WielderId = wielderId;
            WielderName = string.IsNullOrWhiteSpace(wielderName) ? ModText.Get(ModStrings.Events.Wielder) : wielderName;
        }

        public string Kind { get { return AccessibilityEvents.Map.DestinationCleared; } }
        public int WielderId { get; private set; }

        public string WielderName { get; private set; }

        public string GetSpeechText()
        {
            return ModText.Get(ModStrings.Events.DestinationCleared, WielderName);
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
            return ModText.Get(ModStrings.Events.ModError, Message);
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
            return IsVisible ? ModText.Get(ModStrings.Events.HudOpen) : ModText.Get(ModStrings.Events.HudClosed);
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
            return Announce
                ? ModText.Get(ModStrings.Events.MapCameraFocus, Tile.x + ", " + Tile.y)
                : string.Empty;
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
