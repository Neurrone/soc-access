using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DefenceSlotListAdapter
    {
        private readonly IReadOnlyList<TroopHUDEntry> _entries;
        private readonly ILocalizationHandler _localization;

        public DefenceSlotListAdapter(IReadOnlyList<TroopHUDEntry> entries, ILocalizationHandler localization)
        {
            _entries = entries ?? new TroopHUDEntry[0];
            _localization = localization;
        }

        public IReadOnlyList<Slot> GetSlots()
        {
            List<Slot> slots = new List<Slot>();
            for (int i = 0; i < _entries.Count; i++)
            {
                TroopHUDEntry entry = _entries[i];
                slots.Add(new Slot(i + 1, entry, _localization));
            }

            return slots;
        }

        internal sealed class Slot
        {
            private readonly TroopHUDEntry _entry;
            private readonly ILocalizationHandler _localization;

            public Slot(int slotNumber, TroopHUDEntry entry, ILocalizationHandler localization)
            {
                SlotNumber = slotNumber;
                _entry = entry;
                _localization = localization;
            }

            public int SlotNumber { get; private set; }

            public bool IsOccupied
            {
                get { return _entry != null && _entry.Troop != null; }
            }

            public Tooltip Tooltip
            {
                get
                {
                    if (!IsVisible(_entry as Component))
                    {
                        return null;
                    }

                    return Tooltip.ForComponent(_entry != null ? _entry.GetSelectable() : null, _localization);
                }
            }

            public void Focus()
            {
                if (IsVisible(_entry as Component))
                {
                    NativeSelectionUtility.Select(_entry.GetSelectable());
                }
            }

            private static bool IsVisible(Component component)
            {
                return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
            }
        }
    }
}
