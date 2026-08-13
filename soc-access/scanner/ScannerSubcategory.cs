using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerSubcategory
    {
        private readonly List<ScannerResult> _results = new List<ScannerResult>();
        private readonly Func<string> _label;

        public ScannerSubcategory(string key, Func<string> label)
        {
            Key = key ?? string.Empty;
            _label = label;
        }

        public string Key { get; private set; }

        /// <summary>
        /// Resolved at call time so a language change between snapshot
        /// construction and speech cannot leave a stale label behind.
        /// </summary>
        public string Label
        {
            get { return _label != null ? _label() : Key; }
        }

        public List<ScannerResult> Results
        {
            get { return _results; }
        }
    }
}
