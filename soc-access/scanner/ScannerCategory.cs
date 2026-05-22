using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerCategory
    {
        private readonly List<ScannerSubcategory> _subcategories = new List<ScannerSubcategory>();

        public ScannerCategory(string label)
        {
            Label = label;
        }

        public string Label { get; private set; }

        public bool PreserveResultOrder { get; set; }

        public List<ScannerSubcategory> Subcategories
        {
            get { return _subcategories; }
        }

        public ScannerSubcategory GetOrAddSubcategory(string label)
        {
            for (int i = 0; i < _subcategories.Count; i++)
            {
                if (_subcategories[i].Label == label)
                {
                    return _subcategories[i];
                }
            }

            ScannerSubcategory subcategory = new ScannerSubcategory(label);
            _subcategories.Add(subcategory);
            return subcategory;
        }
    }
}
