using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Declarative description of one scanner context's category and subcategory
    /// structure. Categories and subcategories are identified by a stable key so
    /// snapshot construction never depends on localized text; the label resolver
    /// runs at speech time.
    /// </summary>
    public sealed class ScannerTaxonomy
    {
        private readonly List<ScannerCategoryDefinition> _categories;

        public ScannerTaxonomy(string key, params ScannerCategoryDefinition[] categories)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Scanner taxonomy key is required.", "key");
            }

            Key = key;
            _categories = new List<ScannerCategoryDefinition>(categories ?? new ScannerCategoryDefinition[0]);
        }

        /// <summary>
        /// Identifies the context this taxonomy describes, so player settings
        /// scoped to a context know which one they belong to.
        /// </summary>
        public string Key { get; private set; }

        public IReadOnlyList<ScannerCategoryDefinition> Categories
        {
            get { return _categories; }
        }

        public ScannerCategoryDefinition GetCategory(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            for (int i = 0; i < _categories.Count; i++)
            {
                if (_categories[i].Key == key)
                {
                    return _categories[i];
                }
            }

            return null;
        }
    }

    public sealed class ScannerCategoryDefinition
    {
        private readonly List<ScannerSubcategoryDefinition> _subcategories;

        public ScannerCategoryDefinition(string key, Func<string> label, params ScannerSubcategoryDefinition[] subcategories)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Scanner category key is required.", "key");
            }

            Key = key;
            Label = label;
            _subcategories = new List<ScannerSubcategoryDefinition>(subcategories ?? new ScannerSubcategoryDefinition[0]);
        }

        public string Key { get; private set; }

        public Func<string> Label { get; private set; }

        public IReadOnlyList<ScannerSubcategoryDefinition> Subcategories
        {
            get { return _subcategories; }
        }

        public ScannerSubcategoryDefinition GetSubcategory(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            for (int i = 0; i < _subcategories.Count; i++)
            {
                if (_subcategories[i].Key == key)
                {
                    return _subcategories[i];
                }
            }

            return null;
        }
    }

    public sealed class ScannerSubcategoryDefinition
    {
        public ScannerSubcategoryDefinition(string key, Func<string> label)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Scanner subcategory key is required.", "key");
            }

            Key = key;
            Label = label;
        }

        public string Key { get; private set; }

        public Func<string> Label { get; private set; }

        /// <summary>
        /// Leaves the results in the order the adapter added them instead of
        /// sorting by distance, for a scope whose order is the information.
        /// </summary>
        public bool PreserveResultOrder { get; set; }

        /// <summary>
        /// Keeps every result its own item. For a scope whose order carries
        /// meaning, grouping by name would shuffle that order away.
        /// </summary>
        public bool FlatItems { get; set; }

        public ScannerSubcategoryDefinition WithPreservedResultOrder()
        {
            PreserveResultOrder = true;
            return this;
        }

        public ScannerSubcategoryDefinition WithFlatItems()
        {
            FlatItems = true;
            return this;
        }
    }
}
