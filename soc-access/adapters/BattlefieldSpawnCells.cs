using System.Collections.Generic;
using SongsOfConquest.Common.Map;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// WHERE A LAYOUT SETS TROOPS DOWN, read straight off the map: every spawn-point entity the
    /// layout carries, both sides together. This is the ground the terrain reading walks out from
    /// (<see cref="Battlefields.BattlefieldTerrain"/>), so it has to answer the same cells in a
    /// fight, on the placement page and in the dump - which is why all three read it here, off the
    /// <c>MapFormat</c> they all hold, rather than from whatever spawn list each of them happens to
    /// have.
    ///
    /// The two blueprint ids are the game's own <c>BattleMapEntities.UtilityAttackerSpawnpoint</c>
    /// and <c>UtilityDefenderSpawnpoint</c>, 2 and 3. The entity is enough: the type a spawn point
    /// carries - default, defence, fallback - says which troops the game prefers to put there, and
    /// a troop standing on it stands on it either way.
    /// </summary>
    public static class BattlefieldSpawnCells
    {
        private const ushort AttackerSpawnpointBlueprint = 2;
        private const ushort DefenderSpawnpointBlueprint = 3;

        public static List<Vector2Int> For(MapFormat map)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (map == null || map.Contents == null || map.Contents.MapEntities == null)
            {
                return cells;
            }

            foreach (MapEntityFormat entity in map.Contents.MapEntities)
            {
                if (entity != null
                    && (entity.Id == AttackerSpawnpointBlueprint || entity.Id == DefenderSpawnpointBlueprint))
                {
                    cells.Add(new Vector2Int(entity.X, entity.Y));
                }
            }

            return cells;
        }
    }
}
