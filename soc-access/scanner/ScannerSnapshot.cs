using System;
using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal struct ScannerSnapshotLocation
    {
        public ScannerSnapshotLocation(int categoryIndex, int subcategoryIndex, int itemIndex, int resultIndex)
        {
            CategoryIndex = categoryIndex;
            SubcategoryIndex = subcategoryIndex;
            ItemIndex = itemIndex;
            ResultIndex = resultIndex;
        }

        public int CategoryIndex { get; private set; }

        public int SubcategoryIndex { get; private set; }

        public int ItemIndex { get; private set; }

        public int ResultIndex { get; private set; }

        public static ScannerSnapshotLocation NotFound
        {
            get { return new ScannerSnapshotLocation(-1, -1, -1, -1); }
        }
    }

    internal sealed class ScannerSnapshot
    {
        private readonly List<ScannerCategory> _categories = new List<ScannerCategory>();
        private ScannerTaxonomy _taxonomy;

        public ScannerSnapshot()
        {
        }

        public ScannerSnapshot(ScannerTaxonomy taxonomy)
        {
            Initialize(taxonomy);
        }

        public List<ScannerCategory> Categories
        {
            get { return _categories; }
        }

        public ScannerTaxonomy Taxonomy
        {
            get { return _taxonomy; }
        }

        /// <summary>
        /// Puts categories in front of everything the taxonomy declared, so the
        /// player's own categories are the first ones the category cycle
        /// reaches.
        /// </summary>
        public void PrependCategories(IReadOnlyList<ScannerCategory> categories)
        {
            if (categories == null)
            {
                return;
            }

            for (int i = categories.Count - 1; i >= 0; i--)
            {
                if (categories[i] != null)
                {
                    _categories.Insert(0, categories[i]);
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _categories.Count; i++)
                {
                    for (int j = 0; j < _categories[i].Subcategories.Count; j++)
                    {
                        if (_categories[i].Subcategories[j].HasResults)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public bool HasSortOrigin { get; private set; }

        public Vector2Int SortOrigin { get; private set; }

        public bool IsSearchSnapshot { get; private set; }

        public bool IsLookAroundSnapshot { get; private set; }

        public bool IsTemporarySnapshot
        {
            get { return IsSearchSnapshot || IsLookAroundSnapshot; }
        }

        public bool UseSortOriginForDirections { get; private set; }

        public void MarkAsSearchSnapshot()
        {
            IsSearchSnapshot = true;
        }

        public void MarkAsLookAroundSnapshot()
        {
            IsLookAroundSnapshot = true;
            UseSortOriginForDirections = true;
        }

        /// <summary>
        /// Creates every category and subcategory the taxonomy declares, in
        /// declaration order, so cycling order is fixed regardless of which
        /// contributions actually produce results.
        /// </summary>
        public void Initialize(ScannerTaxonomy taxonomy)
        {
            _taxonomy = taxonomy;
            if (taxonomy == null)
            {
                return;
            }

            for (int i = 0; i < taxonomy.Categories.Count; i++)
            {
                ScannerCategoryDefinition definition = taxonomy.Categories[i];
                ScannerCategory category = GetOrAddCategory(definition.Key);
                for (int j = 0; j < definition.Subcategories.Count; j++)
                {
                    category.GetOrAddSubcategory(definition.Subcategories[j].Key);
                }
            }
        }

        public ScannerCategory GetOrAddCategory(string key)
        {
            ScannerCategoryDefinition definition = _taxonomy != null ? _taxonomy.GetCategory(key) : null;
            return GetOrAddCategory(key, definition != null ? definition.Label : null, definition);
        }

        public ScannerCategory GetOrAddCategory(string key, Func<string> label)
        {
            return GetOrAddCategory(key, label, null);
        }

        private ScannerCategory GetOrAddCategory(string key, Func<string> label, ScannerCategoryDefinition definition)
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                if (_categories[i].Key == key)
                {
                    return _categories[i];
                }
            }

            ScannerCategory category = new ScannerCategory(key, label, definition);
            _categories.Add(category);
            return category;
        }

        public void Add(string categoryKey, string subcategoryKey, ScannerResult result)
        {
            if (result == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(subcategoryKey))
            {
                subcategoryKey = ScannerSubcategoryKeys.All;
            }

            GetOrAddCategory(categoryKey).GetOrAddSubcategory(subcategoryKey).Add(result);
        }

        public void SortByDistance(Vector2Int origin)
        {
            SortBy(origin, (left, right) => CompareByDistance(origin, left, right));
        }

        /// <summary>
        /// Nearest first, and then by name and position so no two results are
        /// ever tied. A walk that flattens a subcategory back into one list has
        /// to reproduce this order exactly, which it can only do if the order is
        /// total.
        /// </summary>
        public static int CompareByDistance(Vector2Int origin, ScannerResult left, ScannerResult right)
        {
            int distanceCompare = DistanceSquared(origin, left.Position).CompareTo(DistanceSquared(origin, right.Position));
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            int labelCompare = string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
            if (labelCompare != 0)
            {
                return labelCompare;
            }

            int xCompare = left.Position.x.CompareTo(right.Position.x);
            return xCompare != 0 ? xCompare : left.Position.y.CompareTo(right.Position.y);
        }

        public void SortBy(Vector2Int origin, Comparison<ScannerResult> comparison)
        {
            if (comparison == null)
            {
                return;
            }

            SortOrigin = origin;
            HasSortOrigin = true;
            for (int i = 0; i < _categories.Count; i++)
            {
                ScannerCategory category = _categories[i];
                for (int j = 0; j < category.Subcategories.Count; j++)
                {
                    ScannerSubcategory subcategory = category.Subcategories[j];
                    if (!subcategory.PreserveResultOrder)
                    {
                        SortSubcategory(subcategory, comparison);
                    }
                }
            }
        }

        /// <summary>
        /// Instances sort inside their item, then items sort by their leading
        /// instance, so the item cycle runs nearest first for the same reason
        /// the flat list used to.
        /// </summary>
        private static void SortSubcategory(ScannerSubcategory subcategory, Comparison<ScannerResult> comparison)
        {
            List<ScannerItem> items = subcategory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                items[i].Instances.Sort(comparison);
            }

            items.Sort((left, right) => comparison(left.Instances[0], right.Instances[0]));
        }

        public bool TryLocateByKey(
            string key,
            int categoryHint,
            int subcategoryHint,
            bool allowFallback,
            out ScannerSnapshotLocation location)
        {
            location = ScannerSnapshotLocation.NotFound;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (TryLocateInSubcategory(key, categoryHint, subcategoryHint, out location))
            {
                return true;
            }

            if (!allowFallback)
            {
                location = ScannerSnapshotLocation.NotFound;
                return false;
            }

            for (int categoryIndex = 0; categoryIndex < _categories.Count; categoryIndex++)
            {
                ScannerCategory category = _categories[categoryIndex];
                for (int subcategoryIndex = 0; subcategoryIndex < category.Subcategories.Count; subcategoryIndex++)
                {
                    if (categoryIndex == categoryHint && subcategoryIndex == subcategoryHint)
                    {
                        continue;
                    }

                    if (TryLocateInSubcategory(key, categoryIndex, subcategoryIndex, out location))
                    {
                        return true;
                    }
                }
            }

            location = ScannerSnapshotLocation.NotFound;
            return false;
        }

        private bool TryLocateInSubcategory(
            string key,
            int categoryIndex,
            int subcategoryIndex,
            out ScannerSnapshotLocation location)
        {
            location = ScannerSnapshotLocation.NotFound;
            if (categoryIndex < 0 || categoryIndex >= _categories.Count)
            {
                return false;
            }

            ScannerCategory category = _categories[categoryIndex];
            if (subcategoryIndex < 0 || subcategoryIndex >= category.Subcategories.Count)
            {
                return false;
            }

            ScannerSubcategory subcategory = category.Subcategories[subcategoryIndex];
            for (int itemIndex = 0; itemIndex < subcategory.Items.Count; itemIndex++)
            {
                List<ScannerResult> instances = subcategory.Items[itemIndex].Instances;
                for (int resultIndex = 0; resultIndex < instances.Count; resultIndex++)
                {
                    ScannerResult result = instances[resultIndex];
                    if (result != null && result.Key == key)
                    {
                        location = new ScannerSnapshotLocation(categoryIndex, subcategoryIndex, itemIndex, resultIndex);
                        return true;
                    }
                }
            }

            return false;
        }

        public void PruneEmpty()
        {
            for (int i = _categories.Count - 1; i >= 0; i--)
            {
                ScannerCategory category = _categories[i];
                for (int j = category.Subcategories.Count - 1; j >= 0; j--)
                {
                    if (!category.Subcategories[j].HasResults)
                    {
                        category.Subcategories.RemoveAt(j);
                    }
                }

                if (category.Subcategories.Count == 0)
                {
                    _categories.RemoveAt(i);
                }
            }
        }

        public void PruneByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            for (int i = 0; i < _categories.Count; i++)
            {
                ScannerCategory category = _categories[i];
                for (int j = 0; j < category.Subcategories.Count; j++)
                {
                    ScannerSubcategory subcategory = category.Subcategories[j];
                    for (int itemIndex = subcategory.Items.Count - 1; itemIndex >= 0; itemIndex--)
                    {
                        List<ScannerResult> instances = subcategory.Items[itemIndex].Instances;
                        for (int k = instances.Count - 1; k >= 0; k--)
                        {
                            ScannerResult result = instances[k];
                            if (result != null && result.Key == key)
                            {
                                instances.RemoveAt(k);
                            }
                        }

                        if (instances.Count == 0)
                        {
                            subcategory.Items.RemoveAt(itemIndex);
                        }
                    }
                }
            }
        }

        private static int DistanceSquared(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            return x * x + y * y;
        }
    }
}
