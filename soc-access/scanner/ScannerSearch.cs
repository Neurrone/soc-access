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
            public ScannerResult Nearest;
        }

        public static ScannerSnapshot Build(ScannerSnapshot source, string query, Vector2Int origin)
        {
            return Build(source, query, ScannerDistanceOrder.StraightLine(origin));
        }

        /// <summary>The match tier decides first and the scanner's own distance order decides
        /// inside a tier, so a search agrees with the walk about which of two equally good matches
        /// is the nearer one.</summary>
        public static ScannerSnapshot Build(ScannerSnapshot source, string query, ScannerDistanceOrder order)
        {
            if (source == null || order == null || string.IsNullOrWhiteSpace(query))
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

                RecordBestMatch(matchInfoByKey, result, tier, order);
                return true;
            });

            if (!all.HasResults)
            {
                return null;
            }

            search.SortBy(order, (left, right) => CompareMatches(matchInfoByKey, order, left, right));
            return search;
        }

        private static void RecordBestMatch(
            Dictionary<string, MatchInfo> matches,
            ScannerResult result,
            int tier,
            ScannerDistanceOrder order)
        {
            MatchInfo info;
            if (!matches.TryGetValue(result.Key, out info))
            {
                matches[result.Key] = new MatchInfo
                {
                    Tier = tier,
                    Nearest = result
                };
                return;
            }

            if (tier < info.Tier || (tier == info.Tier && order.CompareDistance(result, info.Nearest) < 0))
            {
                info.Tier = tier;
                info.Nearest = result;
            }
        }

        private static int CompareMatches(
            Dictionary<string, MatchInfo> matches,
            ScannerDistanceOrder order,
            ScannerResult left,
            ScannerResult right)
        {
            MatchInfo leftInfo = matches[left.Key];
            MatchInfo rightInfo = matches[right.Key];
            int tierCompare = leftInfo.Tier.CompareTo(rightInfo.Tier);
            if (tierCompare != 0)
            {
                return tierCompare;
            }

            int distanceCompare = order.CompareDistance(leftInfo.Nearest, rightInfo.Nearest);
            return distanceCompare != 0 ? distanceCompare : ScannerSnapshot.CompareTieBreak(left, right);
        }
    }
}
