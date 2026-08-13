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
    }
}
