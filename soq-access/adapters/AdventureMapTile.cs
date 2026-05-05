using System.Collections.Generic;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Map;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureMapTile
    {
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

        public MapGroundType? Terrain { get; set; }

        public List<string> Environment { get; private set; } = new List<string>();

        public ICommanderState Commander { get; set; }

        public string CommanderName { get; set; }

        public bool IsSelectedCommander { get; set; }

        public string CommanderRelationship { get; set; }

        public IMapEntity MapEntity { get; set; }

        public string MapEntityName { get; set; }

        public string MapEntityHint { get; set; }

        public List<string> MapEntityDetails { get; private set; } = new List<string>();

        public bool MapEntityVisited { get; set; }

        public string MapEntityRelationship { get; set; }
    }
}
