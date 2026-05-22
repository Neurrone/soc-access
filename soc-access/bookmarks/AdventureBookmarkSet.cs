using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Bookmarks
{
    internal sealed class AdventureBookmarkSet
    {
        private static readonly HashSet<string> ValidSlots = new HashSet<string>(AdventureBookmarkSlots.All);
        private readonly Dictionary<string, Vector2Int> _positions = new Dictionary<string, Vector2Int>();

        public bool TryGet(string slot, out Vector2Int position)
        {
            if (slot == null)
            {
                position = Vector2Int.zero;
                return false;
            }

            return _positions.TryGetValue(slot, out position);
        }

        public void Set(string slot, Vector2Int position)
        {
            if (!IsValidSlot(slot))
            {
                return;
            }

            _positions[slot] = position;
        }

        public IReadOnlyDictionary<string, Vector2Int> Positions
        {
            get { return _positions; }
        }

        public static bool IsValidSlot(string slot)
        {
            return slot != null && ValidSlots.Contains(slot);
        }
    }
}
