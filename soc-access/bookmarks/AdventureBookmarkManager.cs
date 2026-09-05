using System;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Bookmarks
{
    internal sealed class AdventureBookmarkManager
    {
        private readonly AdventureBookmarkStore _store;
        private AdventureBookmarkGameIdentity _identity;
        private AdventureBookmarkSet _set = new AdventureBookmarkSet();

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

            if (_identity != null && _identity.SameStorageAs(identity))
            {
                return;
            }

            _identity = identity;
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
