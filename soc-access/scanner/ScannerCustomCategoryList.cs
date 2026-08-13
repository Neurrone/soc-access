using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The custom categories defined for one scanner taxonomy, in the order the
    /// player created them. Ids are handed out from a counter that only ever
    /// climbs, so deleting a category can never let a later one inherit its
    /// stored selectors.
    /// </summary>
    internal sealed class ScannerCustomCategoryList
    {
        private readonly List<ScannerCustomCategory> _categories = new List<ScannerCustomCategory>();
        private int _nextId = 1;

        public IReadOnlyList<ScannerCustomCategory> Categories
        {
            get { return _categories; }
        }

        public int NextId
        {
            get { return _nextId; }
        }

        public ScannerCustomCategory Add(Func<int, string> nameForPosition)
        {
            int id = _nextId;
            _nextId = id + 1;
            string name = nameForPosition != null ? nameForPosition(_categories.Count + 1) : string.Empty;
            ScannerCustomCategory category = new ScannerCustomCategory(id, name);
            _categories.Add(category);
            return category;
        }

        public bool Remove(int id)
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                if (_categories[i].Id == id)
                {
                    _categories.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public ScannerCustomCategory Get(int id)
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                if (_categories[i].Id == id)
                {
                    return _categories[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Restores a category read back from storage. The counter is dragged
        /// past every restored id so a list saved by a newer build, or hand
        /// edited, still cannot hand out an id twice.
        /// </summary>
        public void Restore(ScannerCustomCategory category)
        {
            if (category == null || Get(category.Id) != null)
            {
                return;
            }

            _categories.Add(category);
            if (category.Id >= _nextId)
            {
                _nextId = category.Id + 1;
            }
        }

        public void SetNextId(int nextId)
        {
            if (nextId > _nextId)
            {
                _nextId = nextId;
            }
        }

        public ScannerCustomCategory GetByQuickKey(ScannerQuickKey quickKey)
        {
            if (quickKey == ScannerQuickKey.None)
            {
                return null;
            }

            for (int i = 0; i < _categories.Count; i++)
            {
                if (_categories[i].QuickKey == quickKey)
                {
                    return _categories[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The first key nobody holds, or None once every key is spoken for.
        /// A key freed by a deletion is left alone until somebody asks for it,
        /// so a category the player never touched cannot change key underneath
        /// them.
        /// </summary>
        public ScannerQuickKey FirstFreeQuickKey()
        {
            for (int i = 0; i < ScannerQuickKeys.Assignable.Length; i++)
            {
                ScannerQuickKey quickKey = ScannerQuickKeys.Assignable[i];
                if (GetByQuickKey(quickKey) == null)
                {
                    return quickKey;
                }
            }

            return ScannerQuickKey.None;
        }

        /// <summary>
        /// Hands the key to one category and takes it off whoever held it
        /// before, because a key that walked two categories would step through
        /// whichever the storage order happened to reach first.
        /// </summary>
        public bool SetQuickKey(int id, ScannerQuickKey quickKey)
        {
            ScannerCustomCategory category = Get(id);
            if (category == null)
            {
                return false;
            }

            bool changed = false;
            if (quickKey != ScannerQuickKey.None)
            {
                ScannerCustomCategory holder = GetByQuickKey(quickKey);
                if (holder != null && holder.Id != id)
                {
                    changed |= holder.SetQuickKey(ScannerQuickKey.None);
                }
            }

            return category.SetQuickKey(quickKey) || changed;
        }
    }
}
