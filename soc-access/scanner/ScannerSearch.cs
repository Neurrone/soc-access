using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal static class ScannerSearch
    {
        private sealed class MatchInfo
        {
            public int Tier;
            public int DistanceSquared;
        }

        public static ScannerSnapshot Build(ScannerSnapshot source, string query, Vector2Int origin)
        {
            if (source == null || string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            string normalizedQuery = ScannerTextMatch.NormalizeQuery(query);
            if (normalizedQuery == null)
            {
                return null;
            }

            ScannerSnapshot search = new ScannerSnapshot();
            search.MarkAsSearchSnapshot();

            ScannerCategory searchCategory = search.GetOrAddCategory(
                ScannerCategoryKeys.SearchResults,
                () => ModText.Get(ModStrings.Scanner.SearchResults));
            ScannerSubcategory all = searchCategory.GetOrAddSubcategory(
                ScannerSubcategoryKeys.All,
                () => ModText.Get(ModStrings.Scanner.All));
            Dictionary<string, MatchInfo> matchInfoByKey = new Dictionary<string, MatchInfo>();
            HashSet<string> addedToAll = new HashSet<string>();
            Dictionary<string, HashSet<string>> addedToCategory = new Dictionary<string, HashSet<string>>();

            for (int categoryIndex = 0; categoryIndex < source.Categories.Count; categoryIndex++)
            {
                ScannerCategory sourceCategory = source.Categories[categoryIndex];
                if (sourceCategory == null)
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

                    for (int resultIndex = 0; resultIndex < sourceSubcategory.Results.Count; resultIndex++)
                    {
                        ScannerResult result = sourceSubcategory.Results[resultIndex];
                        if (result == null)
                        {
                            continue;
                        }

                        int tier = ScannerTextMatch.TierForLabel(result.Label, normalizedQuery);
                        if (tier == ScannerTextMatch.NoMatch)
                        {
                            continue;
                        }

                        int distance = DistanceSquared(origin, result.Position);
                        RecordBestMatch(matchInfoByKey, result.Key, tier, distance);

                        if (addedToAll.Add(result.Key))
                        {
                            all.Results.Add(result);
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
                                targetSubcategory = searchCategory.GetOrAddSubcategory(
                                    labelSource.Key,
                                    () => labelSource.Label);
                            }

                            targetSubcategory.Results.Add(result);
                        }
                    }
                }
            }

            if (all.Results.Count == 0)
            {
                return null;
            }

            search.SortBy(origin, (left, right) => CompareMatches(matchInfoByKey, left, right));
            return search;
        }

        private static void RecordBestMatch(Dictionary<string, MatchInfo> matches, string key, int tier, int distanceSquared)
        {
            MatchInfo info;
            if (!matches.TryGetValue(key, out info))
            {
                matches[key] = new MatchInfo
                {
                    Tier = tier,
                    DistanceSquared = distanceSquared
                };
                return;
            }

            if (tier < info.Tier || (tier == info.Tier && distanceSquared < info.DistanceSquared))
            {
                info.Tier = tier;
                info.DistanceSquared = distanceSquared;
            }
        }

        private static int CompareMatches(Dictionary<string, MatchInfo> matches, ScannerResult left, ScannerResult right)
        {
            MatchInfo leftInfo = matches[left.Key];
            MatchInfo rightInfo = matches[right.Key];
            int tierCompare = leftInfo.Tier.CompareTo(rightInfo.Tier);
            if (tierCompare != 0)
            {
                return tierCompare;
            }

            int distanceCompare = leftInfo.DistanceSquared.CompareTo(rightInfo.DistanceSquared);
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

        private static int DistanceSquared(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            return x * x + y * y;
        }
    }
}
