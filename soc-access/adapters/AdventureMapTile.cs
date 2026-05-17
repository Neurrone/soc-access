using System.Collections.Generic;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Map;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureMapTile
    {
        public enum PathIndicatorKind
        {
            OnRoute,
            Destination
        }

        public sealed class PathIndicatorInfo
        {
            public PathIndicatorKind Kind { get; set; }

            public int TravelTurns { get; set; }

            public int? FurthestReachableTurns { get; set; }

            public bool IsInteractable { get; set; }

            public bool CanInteractThisTurn { get; set; }

            public int? CostMark { get; set; }

            public bool HasRoutePreview { get; set; }
        }

        public sealed class CommanderInfo
        {
            public ICommanderState Raw { get; set; }

            public string Name { get; set; }

            public bool IsSelected { get; set; }

            public string Relationship { get; set; }

            public bool IsOwnedByLocalTeam { get; set; }

            public string MovementLabel { get; set; }

            public float MovesLeft { get; set; }

            public float MaxMovement { get; set; }

            public bool HasDestination { get; set; }

            public Vector2Int Destination { get; set; }

            public bool HasThisTurnDestination { get; set; }

            public Vector2Int ThisTurnDestination { get; set; }
        }

        public AdventureMapTile(Vector2Int position)
        {
            Position = position;
        }

        public Vector2Int Position { get; private set; }

        public bool IsExplored { get; set; }

        public bool IsVisible { get; set; }

        public bool IsReachable { get; set; }

        public bool IsBlocked { get; set; }

        public bool IsInteractionPoint { get; set; }

        public PathIndicatorInfo PathIndicator { get; set; }

        public MapGroundType? Terrain { get; set; }

        public List<string> Environment { get; private set; } = new List<string>();

        public CommanderInfo Commander { get; set; }

        public List<string> ZoneOfControlNames { get; private set; } = new List<string>();

        public IMapEntity MapEntity { get; set; }

        public string MapEntityName { get; set; }

        public string MapEntityHint { get; set; }

        public List<string> MapEntityDetails { get; private set; } = new List<string>();

        public bool MapEntityVisited { get; set; }

        public string MapEntityRelationship { get; set; }
    }
}
