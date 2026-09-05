using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    public static class ScannerLookAround
    {
        public static ScannerSnapshot Build(ScannerSnapshot source, Vector2Int origin, int radius)
        {
            if (source == null || radius < 1)
            {
                return null;
            }

            ScannerSnapshot lookAround = new ScannerSnapshot();
            lookAround.MarkAsLookAroundSnapshot();

            ScannerCategory lookAroundCategory = lookAround.GetOrAddCategory(
                ScannerCategoryKeys.LookAround,
                () => ModText.Get(ModStrings.Scanner.LookAround));
            lookAroundCategory.FlatItems = true;
            ScannerSubcategory all = lookAroundCategory.GetOrAddSubcategory(
                ScannerSubcategoryKeys.All,
                () => ModText.Get(ModStrings.Scanner.All));
            Dictionary<string, HashSet<string>> addedToCategory = new Dictionary<string, HashSet<string>>();
            HashSet<string> addedToAll = new HashSet<string>();

            for (int categoryIndex = 0; categoryIndex < source.Categories.Count; categoryIndex++)
            {
                ScannerCategory sourceCategory = source.Categories[categoryIndex];
                if (sourceCategory == null || sourceCategory.IsCustom)
                {
                    continue;
                }

                ScannerSubcategory targetSubcategory = null;
                for (int subcategoryIndex = 0; subcategoryIndex < sourceCategory.Subcategories.Count; subcategoryIndex++)
                {
                    ScannerSubcategory sourceSubcategory = sourceCategory.Subcategories[subcategoryIndex];
                    if (sourceSubcategory == null)
                    {
                        continue;
                    }

                    foreach (ScannerResult result in sourceSubcategory.AllResults)
                    {
                        if (!IsWithinLookRadius(result, origin, radius))
                        {
                            continue;
                        }

                        if (addedToAll.Add(result.Key))
                        {
                            all.Add(result);
                        }

                        HashSet<string> categoryKeys;
                        if (!addedToCategory.TryGetValue(sourceCategory.Key, out categoryKeys))
                        {
                            categoryKeys = new HashSet<string>();
                            addedToCategory[sourceCategory.Key] = categoryKeys;
                        }

                        if (categoryKeys.Add(result.Key))
                        {
                            if (targetSubcategory == null)
                            {
                                ScannerCategory labelSource = sourceCategory;
                                targetSubcategory = lookAroundCategory.GetOrAddSubcategory(
                                    labelSource.Key,
                                    () => labelSource.Label);
                            }

                            targetSubcategory.Add(result);
                        }
                    }
                }
            }

            if (!all.HasResults)
            {
                return null;
            }

            lookAround.SortBy(origin, (left, right) => CompareDirectionally(origin, left, right));
            return lookAround;
        }

        private static bool IsWithinLookRadius(ScannerResult result, Vector2Int origin, int radius)
        {
            if (result == null || result.Position == origin || result.Kind != ScannerResultKind.Point)
            {
                return false;
            }

            int x = result.Position.x - origin.x;
            int y = result.Position.y - origin.y;
            return x * x + y * y <= radius * radius;
        }

        private static int CompareDirectionally(Vector2Int origin, ScannerResult left, ScannerResult right)
        {
            int angleCompare = AngleFromNorth(origin, left.Position).CompareTo(AngleFromNorth(origin, right.Position));
            if (angleCompare != 0)
            {
                return angleCompare;
            }

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

        private static double AngleFromNorth(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            double angle = Math.Atan2(x, y);
            return angle < 0 ? angle + Math.PI * 2 : angle;
        }

        private static int DistanceSquared(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            return x * x + y * y;
        }
    }
}
