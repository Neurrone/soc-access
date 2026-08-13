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

        /// <summary>
        /// Leaves the results where the adapter put them rather than sorting
        /// them by distance. Lives here rather than on the category because a
        /// category can hold one scope whose order matters beside others whose
        /// order does not.
        /// </summary>
        public bool PreserveResultOrder { get; set; }

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
        /// Items group on everything the announcement says about the thing
        /// itself: what it is, whose it is, and what state it is in. Instances
        /// under one item are then interchangeable, and a scope that mixes
        /// sides or states spends a stop on each rather than making the player
        /// walk the enemy's spawn points to reach their own, or the chests they
        /// have already emptied to reach the ones still worth a visit. Each
        /// fact is spoken with the instance, so the items say which is which
        /// without needing names of their own.
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
