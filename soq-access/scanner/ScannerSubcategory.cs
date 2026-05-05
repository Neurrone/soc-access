using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerSubcategory
    {
        private readonly List<ScannerResult> _results = new List<ScannerResult>();

        public ScannerSubcategory(string label)
        {
            Label = label;
        }

        public string Label { get; private set; }

        public List<ScannerResult> Results
        {
            get { return _results; }
        }
    }
}
