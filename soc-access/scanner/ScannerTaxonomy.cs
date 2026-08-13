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
    internal sealed class ScannerTaxonomy
    {
        private readonly List<ScannerCategoryDefinition> _categories;

        public ScannerTaxonomy(params ScannerCategoryDefinition[] categories)
        {
            _categories = new List<ScannerCategoryDefinition>(categories ?? new ScannerCategoryDefinition[0]);
        }

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

    internal sealed class ScannerCategoryDefinition
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

        public bool PreserveResultOrder { get; set; }

        public bool FlatItems { get; set; }

        public IReadOnlyList<ScannerSubcategoryDefinition> Subcategories
        {
            get { return _subcategories; }
        }

        public ScannerCategoryDefinition WithPreservedResultOrder()
        {
            PreserveResultOrder = true;
            return this;
        }

        /// <summary>
        /// Keeps every result its own item. For a category whose order carries
        /// meaning, grouping by name would shuffle that order away.
        /// </summary>
        public ScannerCategoryDefinition WithFlatItems()
        {
            FlatItems = true;
            return this;
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

    internal sealed class ScannerSubcategoryDefinition
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
    }
}
