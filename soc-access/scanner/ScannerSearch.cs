using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    public static class ScannerSearch
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

            ScannerProjection.Project(source, searchCategory, all, result =>
            {
                int tier = ScannerTextMatch.TierForLabel(result.Label, normalizedQuery);
                if (tier == ScannerTextMatch.NoMatch)
                {
                    return false;
                }

                int distance = ScannerSnapshot.DistanceSquared(origin, result.Position);
                RecordBestMatch(matchInfoByKey, result.Key, tier, distance);
                return true;
            });

            if (!all.HasResults)
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
            return distanceCompare != 0 ? distanceCompare : ScannerSnapshot.CompareTieBreak(left, right);
        }
    }
}
