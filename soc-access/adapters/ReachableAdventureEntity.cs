using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class ReachableAdventureEntity
    {
        public ReachableAdventureEntity(int id, string name, Vector2Int position, float distance)
        {
            Id = id;
            Name = name;
            Position = position;
            Distance = distance;
        }

        public int Id { get; private set; }

        public string Name { get; private set; }

        public Vector2Int Position { get; private set; }

        public float Distance { get; private set; }
    }
}
