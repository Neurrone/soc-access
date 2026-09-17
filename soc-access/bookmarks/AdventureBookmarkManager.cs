using System;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Bookmarks
{
    public sealed class AdventureBookmarkManager
    {
        private readonly AdventureBookmarkStore _store;
        private AdventureBookmarkGameIdentity _identity;
        private AdventureBookmarkSet _set = new AdventureBookmarkSet();

        // The store's write counter as of the set now held. Any write the mod makes moves it, so a
        // file replaced from the Mod options window - a store of its own over the same folder - is
        // read again on the next bookmark gesture rather than staying as it was loaded.
        private int _generation = -1;

        public AdventureBookmarkManager(AdventureBookmarkStore store)
        {
            _store = store ?? new AdventureBookmarkStore();
        }

        public void EnsureLoaded(AdventureBookmarkGameIdentity identity)
        {
            if (identity == null)
            {
                _identity = null;
                _set = new AdventureBookmarkSet();
                return;
            }

            if (_identity != null && _identity.SameStorageAs(identity) && _generation == AdventureBookmarkStore.Generation)
            {
                return;
            }

            _identity = identity;
            _generation = AdventureBookmarkStore.Generation;
            try
            {
                _set = _store.Load(identity);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to load adventure bookmarks: " + exception.Message);
                _set = new AdventureBookmarkSet();
            }
        }

        public string Save(string slot, Vector2Int position)
        {
            if (!AdventureBookmarkSet.IsValidSlot(slot))
            {
                return string.Empty;
            }

            _set.Set(slot, position);
            try
            {
                _store.Save(_identity, _set);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to persist adventure bookmarks: " + exception.Message);
            }

            // This write is the mod's own, so it is not a reason to read the file back.
            _generation = AdventureBookmarkStore.Generation;

            return ModText.Get(ModStrings.Bookmarks.BookmarkSaved);
        }

        public bool TryGet(string slot, out Vector2Int position)
        {
            if (_set != null)
            {
                return _set.TryGet(slot, out position);
            }

            position = Vector2Int.zero;
            return false;
        }
    }
}
