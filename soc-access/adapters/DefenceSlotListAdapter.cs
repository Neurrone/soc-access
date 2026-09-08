using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class DefenceSlotListAdapter
    {
        private readonly IReadOnlyList<TroopHUDEntry> _source;
        private readonly IReadOnlyList<TroopHUDEntry> _entries;
        private readonly ILocalizationHandler _localization;
        private List<Slot> _slots;

        public DefenceSlotListAdapter(IReadOnlyList<TroopHUDEntry> entries, ILocalizationHandler localization)
        {
            _source = entries;
            _entries = entries ?? new TroopHUDEntry[0];
            _localization = localization;
        }

        /// <summary>The game's own list this reads, so an owner keeping one of these can tell whether
        /// the menu has swapped the list out from under it.</summary>
        public IReadOnlyList<TroopHUDEntry> Entries
        {
            get { return _source; }
        }

        /// <summary>The slots over the game's list. A slot is a view onto its entry rather than a
        /// snapshot of it, so the list is rebuilt only when the game's own has changed: both pages
        /// that draw these ask for them on every build.</summary>
        public IReadOnlyList<Slot> GetSlots()
        {
            if (IsSlotListCurrent())
            {
                return _slots;
            }

            List<Slot> slots = new List<Slot>();
            for (int i = 0; i < _entries.Count; i++)
            {
                TroopHUDEntry entry = _entries[i];
                slots.Add(new Slot(i + 1, entry, _localization));
            }

            _slots = slots;
            return slots;
        }

        private bool IsSlotListCurrent()
        {
            if (_slots == null || _slots.Count != _entries.Count)
            {
                return false;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (!ReferenceEquals(_slots[i].Entry, _entries[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public sealed class Slot
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

            /// <summary>The game's entry this slot is a view onto.</summary>
            public TroopHUDEntry Entry
            {
                get { return _entry; }
            }

            public bool IsOccupied
            {
                get { return IsVisible(_entry as Component) && GetDetails() != null; }
            }

            public string TroopName
            {
                get
                {
                    AdventureTroopDetails details = GetDetails();
                    string nameKey = details != null ? details.TroopDetails.Description.NameKey : string.Empty;
                    return !string.IsNullOrWhiteSpace(nameKey) && _localization != null
                        ? SpokenLines.Clean(_localization.GetPluralTextGeneric(nameKey, CurrentSize))
                        : string.Empty;
                }
            }

            public int CurrentSize
            {
                get
                {
                    AdventureTroopDetails details = GetDetails();
                    if (details != null && details.TroopDetails.Description.Amount > 0)
                    {
                        return details.TroopDetails.Description.Amount;
                    }

                    return ParseAmount(_entry != null ? _entry.AmountText : null);
                }
            }

            public int MaxSize
            {
                get
                {
                    AdventureTroopDetails details = GetDetails();
                    return details != null
                        && details.TroopDetails.Stats != null
                        && details.TroopDetails.Stats.MaxTroopSize != null
                        ? details.TroopDetails.Stats.MaxTroopSize.GetValue()
                        : 0;
                }
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

            private AdventureTroopDetails GetDetails()
            {
                IDetails nativeDetails;
                return _entry != null
                    && NativeTooltipUtility.TryGetUiDetails(_entry.GetSelectable(), out nativeDetails)
                    ? nativeDetails as AdventureTroopDetails
                    : null;
            }

            private static int ParseAmount(string amountText)
            {
                string normalized = SpokenLines.Clean(amountText);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    return 0;
                }

                int separatorIndex = normalized.IndexOf('/');
                if (separatorIndex >= 0)
                {
                    normalized = normalized.Substring(0, separatorIndex);
                }

                int amount;
                return int.TryParse(normalized.Trim(), out amount) ? amount : 0;
            }
        }
    }
}
