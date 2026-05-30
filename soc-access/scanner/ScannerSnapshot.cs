using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal struct ScannerSnapshotLocation
    {
        public ScannerSnapshotLocation(int categoryIndex, int subcategoryIndex, int resultIndex)
        {
            CategoryIndex = categoryIndex;
            SubcategoryIndex = subcategoryIndex;
            ResultIndex = resultIndex;
        }

        public int CategoryIndex { get; private set; }

        public int SubcategoryIndex { get; private set; }

        public int ResultIndex { get; private set; }

        public static ScannerSnapshotLocation NotFound
        {
            get { return new ScannerSnapshotLocation(-1, -1, -1); }
        }
    }

    internal sealed class ScannerSnapshot
    {
        private readonly List<ScannerCategory> _categories = new List<ScannerCategory>();

        public List<ScannerCategory> Categories
        {
            get { return _categories; }
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _categories.Count; i++)
                {
                    for (int j = 0; j < _categories[i].Subcategories.Count; j++)
                    {
                        if (_categories[i].Subcategories[j].Results.Count > 0)
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

        public ScannerCategory GetOrAddCategory(string label)
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                if (_categories[i].Label == label)
                {
                    return _categories[i];
                }
            }

            ScannerCategory category = new ScannerCategory(label);
            _categories.Add(category);
            return category;
        }

        public void Add(string category, string subcategory, ScannerResult result)
        {
            if (result == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(subcategory))
            {
                subcategory = ModText.Get(ModStrings.Scanner.All);
            }

            GetOrAddCategory(category).GetOrAddSubcategory(subcategory).Results.Add(result);
        }

        public void SortByDistance(Vector2Int origin)
        {
            SortBy(origin, (left, right) =>
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
            });
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
                if (category.PreserveResultOrder)
                {
                    continue;
                }

                for (int j = 0; j < category.Subcategories.Count; j++)
                {
                    category.Subcategories[j].Results.Sort(comparison);
                }
            }
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
            for (int resultIndex = 0; resultIndex < subcategory.Results.Count; resultIndex++)
            {
                ScannerResult result = subcategory.Results[resultIndex];
                if (result != null && result.Key == key)
                {
                    location = new ScannerSnapshotLocation(categoryIndex, subcategoryIndex, resultIndex);
                    return true;
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
                    if (category.Subcategories[j].Results.Count == 0)
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
                    for (int k = subcategory.Results.Count - 1; k >= 0; k--)
                    {
                        ScannerResult result = subcategory.Results[k];
                        if (result != null && result.Key == key)
                        {
                            subcategory.Results.RemoveAt(k);
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
