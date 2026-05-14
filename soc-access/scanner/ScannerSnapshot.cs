using System;
using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
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
                subcategory = "All";
            }

            GetOrAddCategory(category).GetOrAddSubcategory(subcategory).Results.Add(result);
        }

        public void SortByDistance(Vector2Int origin)
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                ScannerCategory category = _categories[i];
                for (int j = 0; j < category.Subcategories.Count; j++)
                {
                    category.Subcategories[j].Results.Sort((left, right) =>
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
            }
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

        private static int DistanceSquared(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            return x * x + y * y;
        }
    }
}
