using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// One named thing inside a subcategory, holding every instance of it.
    /// Twelve chests are a single stop in the item cycle rather than twelve, and
    /// the instance cycle walks the chests themselves.
    /// </summary>
    internal sealed class ScannerItem
    {
        private readonly List<ScannerResult> _instances = new List<ScannerResult>();

        public ScannerItem(string key)
        {
            Key = key ?? string.Empty;
        }

        public string Key { get; private set; }

        public List<ScannerResult> Instances
        {
            get { return _instances; }
        }

        /// <summary>
        /// Read from the current membership rather than stored, so a live
        /// re-query that renames the thing renames the item with it.
        /// </summary>
        public string Label
        {
            get { return _instances.Count > 0 ? _instances[0].ItemLabel : Key; }
        }
    }
}
