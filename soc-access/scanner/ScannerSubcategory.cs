using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerSubcategory
    {
        private readonly List<ScannerItem> _items = new List<ScannerItem>();
        private readonly Func<string> _label;

        public ScannerSubcategory(string key, Func<string> label)
        {
            Key = key ?? string.Empty;
            _label = label;
        }

        public string Key { get; private set; }

        /// <summary>
        /// Resolved at call time so a language change between snapshot
        /// construction and speech cannot leave a stale label behind.
        /// </summary>
        public string Label
        {
            get { return _label != null ? _label() : Key; }
        }

        /// <summary>
        /// Gives every result an item of its own instead of grouping by name.
        /// Used where the order of the flat list is the point, such as the
        /// chronological revealed list and the Look Around bearing sweep.
        /// </summary>
        public bool FlatItems { get; set; }

        public List<ScannerItem> Items
        {
            get { return _items; }
        }

        /// <summary>
        /// Items never survive losing their last instance, so an item count of
        /// zero is the same question as a result count of zero.
        /// </summary>
        public bool HasResults
        {
            get { return _items.Count > 0; }
        }

        public IEnumerable<ScannerResult> AllResults
        {
            get
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    List<ScannerResult> instances = _items[i].Instances;
                    for (int j = 0; j < instances.Count; j++)
                    {
                        yield return instances[j];
                    }
                }
            }
        }

        public void Add(ScannerResult result)
        {
            if (result == null)
            {
                return;
            }

            GetOrAddItem(FlatItems ? result.Key : result.ItemKey).Instances.Add(result);
        }

        public ScannerItem GetOrAddItem(string key)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Key == key)
                {
                    return _items[i];
                }
            }

            ScannerItem item = new ScannerItem(key);
            _items.Add(item);
            return item;
        }
    }
}
