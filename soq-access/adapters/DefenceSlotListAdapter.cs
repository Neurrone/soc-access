using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DefenceSlotListAdapter
    {
        private readonly IReadOnlyList<TroopHUDEntry> _entries;
        private readonly string _slotType;
        private readonly ILocalizationHandler _localization;

        public DefenceSlotListAdapter(IReadOnlyList<TroopHUDEntry> entries, string slotType, ILocalizationHandler localization)
        {
            _entries = entries ?? new TroopHUDEntry[0];
            _slotType = slotType ?? string.Empty;
            _localization = localization;
        }

        public IReadOnlyList<Slot> GetSlots()
        {
            List<Slot> slots = new List<Slot>();
            for (int i = 0; i < _entries.Count; i++)
            {
                TroopHUDEntry entry = _entries[i];
                slots.Add(new Slot(_slotType + "-slot-" + (i + 1), _slotType, i + 1, entry, _localization));
            }

            return slots;
        }

        internal sealed class Slot
        {
            private readonly TroopHUDEntry _entry;
            private readonly ILocalizationHandler _localization;
            private readonly string _slotType;
            private readonly int _slotNumber;

            public Slot(string id, string slotType, int slotNumber, TroopHUDEntry entry, ILocalizationHandler localization)
            {
                Id = id ?? string.Empty;
                _slotType = slotType ?? string.Empty;
                _slotNumber = slotNumber;
                _entry = entry;
                _localization = localization;
            }

            public string Id { get; private set; }

            public string Label
            {
                get
                {
                    Tooltip tooltip = Tooltip;
                    if (tooltip != null && tooltip.TextLines != null && tooltip.TextLines.Count > 0)
                    {
                        return SpeechTextSanitizer.Normalize(string.Join(". ", tooltip.TextLines));
                    }

                    return "Empty, slot " + _slotNumber;
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
        }
    }
}
