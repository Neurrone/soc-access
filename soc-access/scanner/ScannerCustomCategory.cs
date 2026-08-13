using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// One taxonomy subcategory a custom category pulls in whole. Both keys are
    /// stable taxonomy keys, so a selector survives a language change and can be
    /// written to the config file as it stands.
    /// </summary>
    internal struct ScannerCustomCategorySelector : IEquatable<ScannerCustomCategorySelector>
    {
        public ScannerCustomCategorySelector(string categoryKey, string subcategoryKey)
        {
            CategoryKey = categoryKey ?? string.Empty;
            SubcategoryKey = subcategoryKey ?? string.Empty;
        }

        public string CategoryKey { get; private set; }

        public string SubcategoryKey { get; private set; }

        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(CategoryKey) && !string.IsNullOrWhiteSpace(SubcategoryKey); }
        }

        public bool Equals(ScannerCustomCategorySelector other)
        {
            return CategoryKey == other.CategoryKey && SubcategoryKey == other.SubcategoryKey;
        }

        public override bool Equals(object obj)
        {
            return obj is ScannerCustomCategorySelector && Equals((ScannerCustomCategorySelector)obj);
        }

        public override int GetHashCode()
        {
            return CategoryKey.GetHashCode() ^ SubcategoryKey.GetHashCode();
        }
    }

    /// <summary>
    /// A player-defined scanner category: a handful of taxonomy subcategories
    /// plus free-text keywords, gathered under one name so the scopes reached
    /// for every turn sit together instead of scattered across the full
    /// category cycle.
    /// </summary>
    internal sealed class ScannerCustomCategory
    {
        private readonly List<ScannerCustomCategorySelector> _selectors = new List<ScannerCustomCategorySelector>();
        private readonly List<string> _keywords = new List<string>();
        private string _name;

        public ScannerCustomCategory(int id, string name)
        {
            Id = id;
            _name = name ?? string.Empty;
        }

        public int Id { get; private set; }

        public string Name
        {
            get { return _name; }
        }

        public IReadOnlyList<ScannerCustomCategorySelector> Selectors
        {
            get { return _selectors; }
        }

        public IReadOnlyList<string> Keywords
        {
            get { return _keywords; }
        }

        /// <summary>
        /// The single key that walks this category on the adventure map. Only
        /// one category can hold a given key, which the list this belongs to is
        /// what enforces; a category on its own only knows what it was told.
        /// </summary>
        public ScannerQuickKey QuickKey { get; private set; }

        public bool SetQuickKey(ScannerQuickKey quickKey)
        {
            if (QuickKey == quickKey)
            {
                return false;
            }

            QuickKey = quickKey;
            return true;
        }

        /// <summary>
        /// A whitespace-only name would persist as something a screen reader
        /// speaks as silence, with no cue that anything went wrong, so a blank
        /// rename is refused rather than stored.
        /// </summary>
        public bool Rename(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0 || trimmed == _name)
            {
                return false;
            }

            _name = trimmed;
            return true;
        }

        public bool HasSelector(string categoryKey, string subcategoryKey)
        {
            return IndexOfSelector(new ScannerCustomCategorySelector(categoryKey, subcategoryKey)) >= 0;
        }

        public bool SetSelector(string categoryKey, string subcategoryKey, bool selected)
        {
            ScannerCustomCategorySelector selector = new ScannerCustomCategorySelector(categoryKey, subcategoryKey);
            if (!selector.IsValid)
            {
                return false;
            }

            int index = IndexOfSelector(selector);
            if (selected == (index >= 0))
            {
                return false;
            }

            if (selected)
            {
                _selectors.Add(selector);
            }
            else
            {
                _selectors.RemoveAt(index);
            }

            return true;
        }

        /// <summary>
        /// Refuses a blank keyword, which would match nothing, and a keyword
        /// that only differs by case from one already present, which would put
        /// the same scope in the cycle twice.
        /// </summary>
        public bool AddKeyword(string keyword)
        {
            string trimmed = (keyword ?? string.Empty).Trim();
            if (trimmed.Length == 0 || IndexOfKeyword(trimmed) >= 0)
            {
                return false;
            }

            _keywords.Add(trimmed);
            return true;
        }

        public bool RemoveKeyword(string keyword)
        {
            int index = IndexOfKeyword((keyword ?? string.Empty).Trim());
            if (index < 0)
            {
                return false;
            }

            _keywords.RemoveAt(index);
            return true;
        }

        private int IndexOfSelector(ScannerCustomCategorySelector selector)
        {
            for (int i = 0; i < _selectors.Count; i++)
            {
                if (_selectors[i].Equals(selector))
                {
                    return i;
                }
            }

            return -1;
        }

        private int IndexOfKeyword(string keyword)
        {
            for (int i = 0; i < _keywords.Count; i++)
            {
                if (string.Equals(_keywords[i], keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
