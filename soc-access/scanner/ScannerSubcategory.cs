using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerSubcategory
    {
        private readonly List<ScannerItem> _items = new List<ScannerItem>();

        /// <summary>
        /// The same items again, by key, purely so adding one does not have to
        /// walk the ones already there. The list is what defines their order;
        /// this only answers whether a key has an item yet. A flat scope gives
        /// every result a key of its own, so a large map put thousands of
        /// results through that walk inside a single frame.
        /// </summary>
        private readonly Dictionary<string, ScannerItem> _itemsByKey =
            new Dictionary<string, ScannerItem>(StringComparer.Ordinal);

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

        /// <summary>
        /// Leaves the results where the adapter put them rather than sorting
        /// them by distance. Lives here rather than on the category because a
        /// category can hold one scope whose order matters beside others whose
        /// order does not.
        /// </summary>
        public bool PreserveResultOrder { get; set; }

        /// <summary>
        /// The items in the order the cycle walks them, free to be reordered in
        /// place. Taking one out is what has to go through
        /// <see cref="RemoveItem"/> instead, so the key lookup loses it too.
        /// </summary>
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

            GetOrAddItem(FlatItems ? result.Key : ItemKeyFor(result)).Instances.Add(result);
        }

        /// <summary>
        /// Items group on what the thing is, whose it is, and the state it is
        /// in, so a scope that mixes sides or states spends a stop on each
        /// rather than making the player walk the enemy's spawn points to
        /// reach their own, or the chests they have already emptied to reach
        /// the ones still worth a visit. Each fact is spoken with the
        /// instance, so the items say which is which without needing names of
        /// their own.
        ///
        /// What the current turn happens to allow is not identity and does not
        /// split anything: every enemy stack of one unit stays one item, and
        /// whether the acting troop can hit this one is what the player hears
        /// walking the instances.
        /// </summary>
        private static string ItemKeyFor(ScannerResult result)
        {
            string key = result.ItemKey + RelationshipSuffix(result.Relationship);
            if (result.Unvisited)
            {
                key += ":unvisited";
            }

            return result.NotVisible ? key + ":unseen" : key;
        }

        private static string RelationshipSuffix(ScannerResultRelationship relationship)
        {
            switch (relationship)
            {
                case ScannerResultRelationship.Neutral:
                    return ":neutral";
                case ScannerResultRelationship.Friendly:
                    return ":friendly";
                case ScannerResultRelationship.Enemy:
                    return ":enemy";
                default:
                    return string.Empty;
            }
        }

        public ScannerItem GetOrAddItem(string key)
        {
            // The same normalization the item does to its own key, done before
            // the lookup so a hit costs nothing but the hash.
            string lookup = key ?? string.Empty;
            ScannerItem existing;
            if (_itemsByKey.TryGetValue(lookup, out existing))
            {
                return existing;
            }

            ScannerItem item = new ScannerItem(lookup);
            _items.Add(item);
            _itemsByKey.Add(lookup, item);
            return item;
        }

        /// <summary>
        /// An item that has lost its last instance leaves both the order and
        /// the key lookup. Leaving it in the lookup would hand a later result
        /// with that key an item nothing holds any more, and the result would
        /// vanish from the scope without a trace.
        /// </summary>
        public void RemoveItem(ScannerItem item)
        {
            if (item == null)
            {
                return;
            }

            _items.Remove(item);
            ScannerItem held;
            if (_itemsByKey.TryGetValue(item.Key, out held) && held == item)
            {
                _itemsByKey.Remove(item.Key);
            }
        }

        public void RemoveItemAt(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return;
            }

            RemoveItem(_items[index]);
        }
    }
}
