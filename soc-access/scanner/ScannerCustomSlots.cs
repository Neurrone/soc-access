using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The three slots one taxonomy's player-defined categories live in: fixed,
    /// numbered, and each either empty or holding one category.
    ///
    /// Fixed slots rather than a list is the whole design. On the adventure map
    /// the three keys address a SLOT, so "custom category 2" means the same
    /// thing on every press of the same key whatever the player has done to the
    /// other two. There are therefore no ids, no ordering and no delete:
    /// clearing a slot is the delete, and the slot is still there afterwards.
    /// </summary>
    public sealed class ScannerCustomSlots
    {
        /// <summary>How many there are, and the number the player hears: slot 0
        /// is spoken as custom category 1.</summary>
        public const int Count = 3;

        private readonly ScannerCustomCategory[] _slots = new ScannerCustomCategory[Count];

        /// <summary>
        /// What is in a slot, or null where it is empty. A number outside the
        /// three answers null rather than throwing, because the callers are a
        /// key handler and a settings row.
        /// </summary>
        public ScannerCustomCategory Slot(int slot)
        {
            return slot < 0 || slot >= Count ? null : _slots[slot];
        }

        /// <summary>
        /// Put a category in a slot, or null to empty it. A nameless category is
        /// refused: the name is what the category cycle says, so a slot holding
        /// one would be a category the player cannot hear.
        /// </summary>
        public bool Set(int slot, ScannerCustomCategory category)
        {
            if (slot < 0 || slot >= Count)
            {
                return false;
            }

            if (category != null && string.IsNullOrWhiteSpace(category.Name))
            {
                return false;
            }

            _slots[slot] = category;
            return true;
        }

        public bool Clear(int slot)
        {
            return Set(slot, null);
        }

        /// <summary>The first slot nobody has filled, or -1 once all three are
        /// spoken for. What the migration of an older saved list walks.</summary>
        public int FirstEmpty()
        {
            for (int i = 0; i < Count; i++)
            {
                if (_slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Whether a name is already the name of another slot, trimmed and
        /// ignoring case, because the conflict a player hits is a spoken one.
        /// </summary>
        public bool NameTaken(string name, int slot)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                if (i != slot
                    && _slots[i] != null
                    && string.Equals(_slots[i].Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The three in order, empties included, which is what the
        /// synthesizer walks so a category keeps the number it is spoken under.
        /// </summary>
        public IReadOnlyList<ScannerCustomCategory> All
        {
            get { return _slots; }
        }
    }
}
