using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>Shorthand for the adventure-map tiles the speech and cue tests read.</summary>
    internal static class TileFixtures
    {
        /// <summary>An explored, visible tile of one terrain — where nearly every tile fixture starts.</summary>
        public static AdventureMapTile Tile(int x, int y, AdventureTerrainKind terrain)
        {
            return Tile(new Vector2Int(x, y), terrain);
        }

        public static AdventureMapTile Tile(Vector2Int position, AdventureTerrainKind terrain)
        {
            return new AdventureMapTile(position)
            {
                IsExplored = true,
                IsVisible = true,
                Terrain = terrain
            };
        }

        /// <summary>A tile with nothing set on it: neither explored nor visible, no terrain.</summary>
        public static AdventureMapTile Bare(int x, int y)
        {
            return new AdventureMapTile(new Vector2Int(x, y));
        }
    }
}
