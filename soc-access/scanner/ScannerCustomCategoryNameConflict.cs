using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Decides whether a proposed custom category name is already taken inside
    /// one taxonomy. The category cycle is only ever read aloud, so two
    /// categories sharing a name are one name to the player no matter which ids
    /// sit behind them, and the name is what has to stay distinct.
    /// </summary>
    internal static class ScannerCustomCategoryNameConflict
    {
        /// <summary>
        /// Reports whether <paramref name="name"/> is already the name of a
        /// built-in category of <paramref name="taxonomy"/> or of a custom
        /// category other than <paramref name="renamedId"/>. A blank name never
        /// conflicts; it is refused further down for being blank.
        /// </summary>
        public static bool Exists(
            string name,
            ScannerTaxonomy taxonomy,
            IReadOnlyList<ScannerCustomCategory> customCategories,
            int renamedId)
        {
            return Exists(name, BuiltInNames(taxonomy), customCategories, renamedId);
        }

        public static bool Exists(
            string name,
            IReadOnlyList<string> builtInNames,
            IReadOnlyList<ScannerCustomCategory> customCategories,
            int renamedId)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (builtInNames != null)
            {
                for (int i = 0; i < builtInNames.Count; i++)
                {
                    if (IsSameName(builtInNames[i], trimmed))
                    {
                        return true;
                    }
                }
            }

            if (customCategories == null)
            {
                return false;
            }

            for (int i = 0; i < customCategories.Count; i++)
            {
                ScannerCustomCategory category = customCategories[i];
                if (category != null && category.Id != renamedId && IsSameName(category.Name, trimmed))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the display names of a taxonomy's built-in categories.
        /// A definition only becomes localized text once its label is called,
        /// so this has to run at the moment the name is being judged.
        /// </summary>
        public static IReadOnlyList<string> BuiltInNames(ScannerTaxonomy taxonomy)
        {
            List<string> names = new List<string>();
            if (taxonomy == null)
            {
                return names;
            }

            IReadOnlyList<ScannerCategoryDefinition> definitions = taxonomy.Categories;
            for (int i = 0; i < definitions.Count; i++)
            {
                ScannerCategoryDefinition definition = definitions[i];
                names.Add(definition.Label != null ? definition.Label() : definition.Key);
            }

            return names;
        }

        private static bool IsSameName(string existing, string trimmed)
        {
            return !string.IsNullOrWhiteSpace(existing)
                && string.Equals(existing.Trim(), trimmed, StringComparison.OrdinalIgnoreCase);
        }
    }
}
