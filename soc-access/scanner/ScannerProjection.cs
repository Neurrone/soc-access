using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// The walk a temporary snapshot is built by. Search and look-around both take one source
    /// snapshot, keep the results a filter accepts, and lay them out the same way: every kept result
    /// once in the All subcategory, and once more under a subcategory named after the source
    /// category it came from, which is only created when something lands in it. The two differ in
    /// the filter and in the order they sort by afterwards, nothing else.
    /// </summary>
    public static class ScannerProjection
    {
        public static void Project(
            ScannerSnapshot source,
            ScannerCategory target,
            ScannerSubcategory all,
            Func<ScannerResult, bool> include)
        {
            if (source == null || target == null || all == null || include == null)
            {
                return;
            }

            HashSet<string> addedToAll = new HashSet<string>();
            Dictionary<string, HashSet<string>> addedToCategory = new Dictionary<string, HashSet<string>>();

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
                        if (result == null || !include(result))
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
                                targetSubcategory = target.GetOrAddSubcategory(
                                    labelSource.Key,
                                    () => labelSource.Label);
                            }

                            targetSubcategory.Add(result);
                        }
                    }
                }
            }
        }
    }
}
