using System.Collections.Generic;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// One artifact list and the key it was built from - a run of numbers read off the game, one per
    /// fact the list froze. The caller writes this frame's numbers into <see cref="BeginKey"/> and
    /// asks <see cref="Unchanged"/> whether the list it built last time still describes the game; a
    /// key that differs by one number rebuilds the whole list, which is what makes a moved artifact
    /// appear without a hook.
    ///
    /// Every screen that draws an <c>InventoryHUD</c> pays the same price for a slot list - a
    /// localized inventory caption and slot name per drawn slot, a rarity-formatted artifact name per
    /// artifact, and the instruction lines the artifact tooltip strips per occupied slot - so the
    /// three adapters that build one (trading, the wielder sheet, the artifact market) share this.
    /// It lives on the adapter, which lives exactly as long as the menu instance it wraps.
    /// </summary>
    public sealed class SlotSnapshot
    {
        private readonly List<int> _key = new List<int>();

        private readonly List<int> _read = new List<int>();

        private IReadOnlyList<InventorySlotInfo> _slots;

        /// <summary>The list this frame's key is written into, emptied for the caller. Kept across
        /// frames so a key that has not changed costs no allocation at all.</summary>
        public List<int> BeginKey()
        {
            _read.Clear();
            return _read;
        }

        /// <summary>The slots built for the key just read, or null where the game has moved since -
        /// including the first read, which has built nothing yet.</summary>
        public IReadOnlyList<InventorySlotInfo> Unchanged()
        {
            if (_slots == null || _key.Count != _read.Count)
            {
                return null;
            }

            for (int i = 0; i < _key.Count; i++)
            {
                if (_key[i] != _read[i])
                {
                    return null;
                }
            }

            return _slots;
        }

        /// <summary>Hold these slots for the key just read, and answer with them.</summary>
        public IReadOnlyList<InventorySlotInfo> Keep(IReadOnlyList<InventorySlotInfo> slots)
        {
            _key.Clear();
            _key.AddRange(_read);
            _slots = slots;
            return slots;
        }
    }
}
