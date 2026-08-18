using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Builds the player's custom categories out of a finished snapshot and
    /// puts them in front of it. Each selector becomes a subcategory holding
    /// the results of the taxonomy subcategory it names; each keyword becomes a
    /// subcategory holding every result whose name matches it; an All
    /// subcategory gathers the lot.
    ///
    /// Results are shared with the categories they came from rather than
    /// copied. The scanner prunes and re-queries results by key across the
    /// whole snapshot, so a shared result stays in step with its original
    /// instead of drifting from it.
    /// </summary>
    internal static class ScannerCustomCategorySynthesizer
    {
        private const string CategoryKeyPrefix = "custom:";
        private const string SelectorKeyPrefix = "sel:";
        private const string KeywordKeyPrefix = "kw:";

        /// <summary>
        /// The key a synthesized category carries in the snapshot, which is how
        /// anything that has to find one definition again names it.
        /// </summary>
        public static string CategoryKeyFor(int id)
        {
            return CategoryKeyPrefix + id;
        }

        /// <summary>
        /// Applies the categories the player defined for whichever taxonomy the
        /// snapshot was built from, and hands the snapshot back so a snapshot
        /// builder can wrap its own result in one call.
        /// </summary>
        public static ScannerSnapshot ApplyFromSettings(ScannerSnapshot snapshot)
        {
            if (snapshot != null && snapshot.Taxonomy != null)
            {
                // Isolated the way an adapter's contribution is: a snapshot
                // without the player's categories still scans the map.
                ScannerContribution.Run(
                    "custom categories",
                    () => Apply(snapshot, ModSettings.GetScannerCustomCategories(snapshot.Taxonomy.Key)));
            }

            return snapshot;
        }

        public static void Apply(ScannerSnapshot snapshot, IReadOnlyList<ScannerCustomCategory> customCategories)
        {
            if (snapshot == null || customCategories == null || customCategories.Count == 0)
            {
                return;
            }

            List<ScannerCategory> synthesized = new List<ScannerCategory>();
            KeywordCandidates candidates = new KeywordCandidates(snapshot);
            for (int i = 0; i < customCategories.Count; i++)
            {
                ScannerCategory category = Build(snapshot, customCategories[i], candidates);
                if (category != null)
                {
                    synthesized.Add(category);
                }
            }

            snapshot.PrependCategories(synthesized);
        }

        private static ScannerCategory Build(
            ScannerSnapshot snapshot,
            ScannerCustomCategory definition,
            KeywordCandidates candidates)
        {
            if (definition == null)
            {
                return null;
            }

            ScannerCategory category = new ScannerCategory(
                CategoryKeyFor(definition.Id),
                () => definition.Name)
            {
                IsCustom = true
            };
            ScannerSubcategory all = category.GetOrAddSubcategory(
                ScannerSubcategoryKeys.All,
                () => ModText.Get(ModStrings.Scanner.All));
            HashSet<string> addedToAll = new HashSet<string>();

            for (int i = 0; i < definition.Selectors.Count; i++)
            {
                AddSelector(snapshot, category, all, addedToAll, definition.Selectors[i]);
            }

            for (int i = 0; i < definition.Keywords.Count; i++)
            {
                AddKeyword(category, all, addedToAll, candidates, definition.Keywords[i]);
            }

            return category;
        }

        private static void AddSelector(
            ScannerSnapshot snapshot,
            ScannerCategory category,
            ScannerSubcategory all,
            HashSet<string> addedToAll,
            ScannerCustomCategorySelector selector)
        {
            ScannerSubcategory source = FindSubcategory(snapshot, selector.CategoryKey, selector.SubcategoryKey);
            if (source == null)
            {
                return;
            }

            ScannerSubcategory target = category.GetOrAddSubcategory(
                SelectorKeyPrefix + selector.CategoryKey + ":" + selector.SubcategoryKey,
                () => DescribeSelector(snapshot, selector));

            // A scope that is deliberately unsorted or ungrouped is that way
            // for a reason the player did not stop caring about by pulling it
            // into a category of their own.
            target.FlatItems = source.FlatItems;
            target.PreserveResultOrder = source.PreserveResultOrder;
            foreach (ScannerResult result in source.AllResults)
            {
                target.Add(result);
                AddToAll(all, addedToAll, result);
            }
        }

        private static void AddKeyword(
            ScannerCategory category,
            ScannerSubcategory all,
            HashSet<string> addedToAll,
            KeywordCandidates candidates,
            string keyword)
        {
            string query = ScannerTextMatch.NormalizeQuery(keyword);
            if (query == null)
            {
                return;
            }

            ScannerSubcategory target = category.GetOrAddSubcategory(KeywordKeyPrefix + keyword, () => keyword);
            HashSet<string> added = new HashSet<string>();
            List<KeywordCandidate> searchable = candidates.All;
            for (int i = 0; i < searchable.Count; i++)
            {
                KeywordCandidate candidate = searchable[i];
                if (!candidate.Matches(query) || !added.Add(candidate.Result.Key))
                {
                    continue;
                }

                target.Add(candidate.Result);
                AddToAll(all, addedToAll, candidate.Result);
            }
        }

        private static void AddToAll(ScannerSubcategory all, HashSet<string> addedToAll, ScannerResult result)
        {
            if (result != null && addedToAll.Add(result.Key))
            {
                all.Add(result);
            }
        }

        private static ScannerSubcategory FindSubcategory(ScannerSnapshot snapshot, string categoryKey, string subcategoryKey)
        {
            for (int i = 0; i < snapshot.Categories.Count; i++)
            {
                ScannerCategory category = snapshot.Categories[i];
                if (category == null || category.IsCustom || category.Key != categoryKey)
                {
                    continue;
                }

                for (int j = 0; j < category.Subcategories.Count; j++)
                {
                    if (category.Subcategories[j].Key == subcategoryKey)
                    {
                        return category.Subcategories[j];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Every result the keywords are asked about, in the order the
        /// snapshot holds them, with the names they can be caught by already
        /// lowercased. Matching lowercases both sides, and the same results are
        /// asked about every keyword of every custom category, so lowercasing
        /// where the question is put threw away results times keywords times
        /// three strings inside one frame. Gathered on the first keyword and
        /// not before, since a category built only from selectors never asks,
        /// and thrown away with the pass: the names are read live and this must
        /// not be the copy that outlives them.
        /// </summary>
        private sealed class KeywordCandidates
        {
            private readonly ScannerSnapshot _snapshot;
            private List<KeywordCandidate> _gathered;

            public KeywordCandidates(ScannerSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public List<KeywordCandidate> All
            {
                get
                {
                    if (_gathered == null)
                    {
                        _gathered = Gather();
                    }

                    return _gathered;
                }
            }

            private List<KeywordCandidate> Gather()
            {
                List<KeywordCandidate> candidates = new List<KeywordCandidate>();
                for (int i = 0; i < _snapshot.Categories.Count; i++)
                {
                    ScannerCategory source = _snapshot.Categories[i];
                    if (source == null || source.IsCustom)
                    {
                        continue;
                    }

                    for (int j = 0; j < source.Subcategories.Count; j++)
                    {
                        foreach (ScannerResult result in source.Subcategories[j].AllResults)
                        {
                            if (result != null)
                            {
                                candidates.Add(new KeywordCandidate(result));
                            }
                        }
                    }
                }

                return candidates;
            }
        }

        private struct KeywordCandidate
        {
            public KeywordCandidate(ScannerResult result)
            {
                Result = result;
                Label = ScannerTextMatch.NormalizeLabel(result.Label);
                ItemLabel = ScannerTextMatch.NormalizeLabel(result.ItemLabel);
                InstanceLabel = ScannerTextMatch.NormalizeLabel(result.InstanceLabel);
            }

            public ScannerResult Result { get; private set; }

            private string Label { get; set; }

            private string ItemLabel { get; set; }

            private string InstanceLabel { get; set; }

            /// <summary>
            /// A keyword catches a result by any of the three names it can be
            /// spoken under, so "grass" finds terrain named by its item and
            /// "12 tiles" is not the only handle on a cluster.
            /// </summary>
            public bool Matches(string query)
            {
                return ScannerTextMatch.MatchesNormalized(Label, query)
                    || ScannerTextMatch.MatchesNormalized(ItemLabel, query)
                    || ScannerTextMatch.MatchesNormalized(InstanceLabel, query);
            }
        }

        /// <summary>
        /// Names a selector by where it came from, because inside a custom
        /// category "Enemy" alone does not say enemy what.
        /// </summary>
        private static string DescribeSelector(ScannerSnapshot snapshot, ScannerCustomCategorySelector selector)
        {
            ScannerTaxonomy taxonomy = snapshot.Taxonomy;
            ScannerCategoryDefinition category = taxonomy != null ? taxonomy.GetCategory(selector.CategoryKey) : null;
            ScannerSubcategoryDefinition subcategory = category != null ? category.GetSubcategory(selector.SubcategoryKey) : null;
            if (category == null || category.Label == null)
            {
                return selector.CategoryKey;
            }

            return subcategory != null && subcategory.Label != null
                ? ModText.Get(ModStrings.Common.ListSeparator, category.Label(), subcategory.Label())
                : category.Label();
        }
    }
}
