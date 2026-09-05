using System;
using System.Collections.Generic;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class AdventureMapTile
    {
        private static readonly Scanner.ScannerDirection[] NoRoadDirections = new Scanner.ScannerDirection[0];

        private IReadOnlyList<Scanner.ScannerDirection> _roadDirections;
        private Func<IReadOnlyList<Scanner.ScannerDirection>> _roadDirectionsSource;

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
            public int Id { get; set; }

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

        public float? ReachableMovementCost { get; set; }

        public bool IsImpassable { get; set; }

        public bool IsBlocked { get; set; }

        public bool IsInteractionPoint { get; set; }

        public PathIndicatorInfo PathIndicator { get; set; }

        public AdventureTerrainKind Terrain { get; set; }

        /// <summary>
        /// The neighbouring tiles this road carries on into, empty for anything that is not a
        /// road. Worked out on first read rather than up front, because finding them looks at
        /// the surrounding tiles and most callers of GetTile never ask: the skip navigator walks
        /// a whole row through GetTile, and turning road directions off means nothing reads this
        /// at all.
        /// </summary>
        public IReadOnlyList<Scanner.ScannerDirection> RoadDirections
        {
            get
            {
                if (_roadDirections == null)
                {
                    _roadDirections = _roadDirectionsSource != null
                        ? _roadDirectionsSource() ?? NoRoadDirections
                        : NoRoadDirections;
                    _roadDirectionsSource = null;
                }

                return _roadDirections;
            }
        }

        /// <summary>
        /// Whether the road branches here rather than merely passing through. Three neighbours
        /// carrying road is the fork: one is a dead end and two is a road going somewhere.
        /// </summary>
        public bool IsRoadFork
        {
            get { return RoadDirections.Count >= 3; }
        }

        /// <summary>Defers working out the road directions until something asks for them.</summary>
        public void SetRoadDirectionsSource(Func<IReadOnlyList<Scanner.ScannerDirection>> source)
        {
            _roadDirections = null;
            _roadDirectionsSource = source;
        }

        public CommanderInfo Commander { get; set; }

        public List<string> ZoneOfControlNames { get; private set; } = new List<string>();

        public IMapEntity MapEntity { get; set; }

        public int? MapEntityId { get; set; }

        public string MapEntityName { get; set; }

        public string MapEntityHint { get; set; }

        public List<string> MapEntityDetails { get; private set; } = new List<string>();

        public bool MapEntityVisited { get; set; }

        public string MapEntityRelationship { get; set; }

        /// <summary>The commander or map entity on this tile classified the way the scanner
        /// classifies it; <see cref="AdventureEntityCategory.None"/> when nothing here qualifies.</summary>
        public AdventureEntityCategory EntityCategory { get; set; }
    }
}
