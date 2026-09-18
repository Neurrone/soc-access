using System.Collections.Generic;
using SongsOfConquest.Common.Localization;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// One list and the key it was built from - a run of numbers read off the game, one per fact the
    /// list froze. The caller writes this frame's numbers into <see cref="BeginKey"/> and asks
    /// <see cref="Unchanged"/> whether the list it built last time still describes the game; a key
    /// that differs by one number rebuilds the whole list, which is what makes a moved artifact
    /// appear without a hook.
    ///
    /// Every screen that draws an <c>InventoryHUD</c> pays the same price for a slot list - a
    /// localized inventory caption and slot name per drawn slot, a rarity-formatted artifact name per
    /// artifact, and the instruction lines the artifact tooltip strips per occupied slot - so the
    /// reader the three menus share (<see cref="InventorySlotReader"/>) holds two of these. The
    /// wielder sheet's composed bands and the artifact market's offers are the same bargain over
    /// other rows. It lives on the adapter, which lives exactly as long as the menu instance it
    /// wraps.
    ///
    /// Every one of those frozen strings is localized, and the game changes language in place - the
    /// options menu the pause menu, the lobby and a battle all open calls SetCurrentLanguage and the
    /// drawn text re-localizes where it stands, with no number in any key moving. So the language
    /// the list was built under is part of the key here rather than at each site: it is read off the
    /// game every frame like the rest of the key (the handler's own CurrentLanguage) and compared by
    /// reference. Where there is no handler at all, as in a test, the answer never changes and the
    /// key is the caller's numbers alone.
    /// </summary>
    public sealed class SlotSnapshot<T>
    {
        private readonly List<int> _key = new List<int>();

        private readonly List<int> _read = new List<int>();

        private ILanguageDefinition _keyLanguage;

        private ILanguageDefinition _readLanguage;

        private IReadOnlyList<T> _items;

        /// <summary>The list this frame's key is written into, emptied for the caller. Kept across
        /// frames so a key that has not changed costs no allocation at all. The language is read
        /// here too, so it is read once per key and not once per number.</summary>
        public List<int> BeginKey()
        {
            _read.Clear();
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            _readLanguage = localization != null ? localization.CurrentLanguage : null;
            return _read;
        }

        /// <summary>The list built for the key just read, or null where the game has moved since -
        /// including the first read, which has built nothing yet.</summary>
        public IReadOnlyList<T> Unchanged()
        {
            if (_items == null
                || !ReferenceEquals(_keyLanguage, _readLanguage)
                || _key.Count != _read.Count)
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

            return _items;
        }

        /// <summary>Hold this list for the key just read, and answer with it.</summary>
        public IReadOnlyList<T> Keep(IReadOnlyList<T> items)
        {
            _key.Clear();
            _key.AddRange(_read);
            _keyLanguage = _readLanguage;
            _items = items;
            return items;
        }
    }
}
