using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    public sealed class ScannerCategory
    {
        private readonly List<ScannerSubcategory> _subcategories = new List<ScannerSubcategory>();
        private readonly Func<string> _label;
        private bool _flatItems;

        public ScannerCategory(string key, Func<string> label)
            : this(key, label, null)
        {
        }

        public ScannerCategory(string key, Func<string> label, ScannerCategoryDefinition definition)
        {
            Key = key ?? string.Empty;
            _label = label;
            Definition = definition;
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

        public ScannerCategoryDefinition Definition { get; private set; }

        /// <summary>
        /// Marks a category the player defined, which holds copies of results
        /// that already live in a real category. Anything that sweeps the whole
        /// snapshot skips these, so a result is not counted twice.
        /// </summary>
        public bool IsCustom { get; set; }

        /// <summary>
        /// Applies to every subcategory under this category, including ones
        /// added later, so a category that wants a flat list cannot end up half
        /// grouped.
        /// </summary>
        public bool FlatItems
        {
            get { return _flatItems; }

            set
            {
                _flatItems = value;
                for (int i = 0; i < _subcategories.Count; i++)
                {
                    _subcategories[i].FlatItems = value;
                }
            }
        }

        public List<ScannerSubcategory> Subcategories
        {
            get { return _subcategories; }
        }

        public ScannerSubcategory GetOrAddSubcategory(string key)
        {
            ScannerSubcategoryDefinition definition = Definition != null ? Definition.GetSubcategory(key) : null;
            ScannerSubcategory subcategory = GetOrAddSubcategory(key, definition != null ? definition.Label : null);
            if (definition != null)
            {
                // Both are one-way: a definition can turn them on, and a
                // caller that already turned one on for its own reasons, as
                // Look Around does, does not get it turned back off.
                subcategory.FlatItems = subcategory.FlatItems || definition.FlatItems;
                subcategory.PreserveResultOrder = subcategory.PreserveResultOrder || definition.PreserveResultOrder;
            }

            return subcategory;
        }

        public ScannerSubcategory GetOrAddSubcategory(string key, Func<string> label)
        {
            for (int i = 0; i < _subcategories.Count; i++)
            {
                if (_subcategories[i].Key == key)
                {
                    return _subcategories[i];
                }
            }

            ScannerSubcategory subcategory = new ScannerSubcategory(key, label)
            {
                FlatItems = _flatItems
            };
            _subcategories.Add(subcategory);
            return subcategory;
        }
    }
}
