using System;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Ranked substring matching shared by scanner search and custom category
    /// keywords. Lower tiers are better matches; <see cref="NoMatch"/> means the
    /// query does not match at all.
    /// </summary>
    internal static class ScannerTextMatch
    {
        public const int NoMatch = -1;

        /// <summary>
        /// Normalizes a user-entered query for matching. Returns null when the
        /// query cannot match anything.
        /// </summary>
        public static string NormalizeQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            string normalized = query.Trim().ToLowerInvariant();
            return normalized.Length == 0 ? null : normalized;
        }

        public static string NormalizeLabel(string label)
        {
            return (label ?? string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Ranks how well an already-lowercased label matches an already
        /// normalized query.
        ///
        /// 0 whole-word match at the start, 1 prefix of the first word,
        /// 2 whole-word match at a later word, 3 prefix of a later word,
        /// 4 substring anywhere, 5 every query token prefixes some word in
        /// order.
        /// </summary>
        public static int Tier(string label, string query)
        {
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(query) || query.Length > label.Length)
            {
                return NoMatch;
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

            return query.IndexOf(' ') >= 0 && MatchesWordPrefixTokens(label, query) ? 5 : NoMatch;
        }

        /// <summary>
        /// Ranks a raw label against a normalized query, lowercasing the label
        /// for the caller.
        /// </summary>
        public static int TierForLabel(string label, string normalizedQuery)
        {
            return Tier(NormalizeLabel(label), normalizedQuery);
        }

        public static bool Matches(string label, string normalizedQuery)
        {
            return TierForLabel(label, normalizedQuery) != NoMatch;
        }

        /// <summary>
        /// The same question as <see cref="Matches"/> for a label that has
        /// already been through <see cref="NormalizeLabel"/>, so a caller
        /// asking about one label many times over does not lowercase it again
        /// for every question.
        /// </summary>
        public static bool MatchesNormalized(string normalizedLabel, string normalizedQuery)
        {
            return Tier(normalizedLabel, normalizedQuery) != NoMatch;
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
    }
}
