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

            string normalizedQuery = query.Trim().ToLowerInvariant();
            if (normalizedQuery.Length == 0)
            {
                return null;
            }

            ScannerSnapshot search = new ScannerSnapshot();
            search.MarkAsSearchSnapshot();

            ScannerCategory searchCategory = search.GetOrAddCategory(ModText.Get(ModStrings.Scanner.SearchResults));
            ScannerSubcategory all = searchCategory.GetOrAddSubcategory(ModText.Get(ModStrings.Scanner.All));
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

                        int tier = MatchTier((result.Label ?? string.Empty).ToLowerInvariant(), normalizedQuery);
                        if (tier < 0)
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
                        if (!addedToCategory.TryGetValue(sourceCategory.Label, out categoryKeys))
                        {
                            categoryKeys = new HashSet<string>();
                            addedToCategory[sourceCategory.Label] = categoryKeys;
                        }

                        if (categoryKeys.Add(result.Key))
                        {
                            if (targetSubcategory == null)
                            {
                                targetSubcategory = searchCategory.GetOrAddSubcategory(sourceCategory.Label);
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

        private static int MatchTier(string label, string query)
        {
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(query) || query.Length > label.Length)
            {
                return -1;
            }

            if (StartsAt(label, query, 0))
            {
                return IsWordEnd(label, query.Length) ? 0 : 1;
            }

            for (int i = 1; i < label.Length; i++)
            {
                if (!IsWordBoundary(label[i - 1]) || IsWordBoundary(label[i]))
                {
                    continue;
                }

                if (StartsAt(label, query, i))
                {
                    return IsWordEnd(label, i + query.Length) ? 2 : 3;
                }
            }

            if (label.IndexOf(query, StringComparison.Ordinal) >= 0)
            {
                return 4;
            }

            return query.IndexOf(' ') >= 0 && MatchesWordPrefixTokens(label, query) ? 5 : -1;
        }

        private static bool StartsAt(string label, string query, int index)
        {
            return index >= 0
                && index + query.Length <= label.Length
                && string.Compare(label, index, query, 0, query.Length, StringComparison.Ordinal) == 0;
        }

        private static bool IsWordBoundary(char value)
        {
            return value == ' ' || value == ',';
        }

        private static bool IsWordEnd(string label, int index)
        {
            return index >= label.Length || IsWordBoundary(label[index]);
        }

        private static bool MatchesWordPrefixTokens(string label, string query)
        {
            string[] tokens = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            int tokenIndex = 0;
            for (int i = 0; i < label.Length && tokenIndex < tokens.Length; i++)
            {
                if ((i == 0 || IsWordBoundary(label[i - 1])) && !IsWordBoundary(label[i]))
                {
                    string token = tokens[tokenIndex];
                    if (StartsAt(label, token, i))
                    {
                        tokenIndex++;
                    }
                }
            }

            return tokenIndex == tokens.Length;
        }

        private static int DistanceSquared(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            return x * x + y * y;
        }
    }
}
