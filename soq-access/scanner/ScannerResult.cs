using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerResult
    {
        public ScannerResult(string label, Vector2Int position)
        {
            Label = label;
            Position = position;
        }

        public string Label { get; private set; }

        public Vector2Int Position { get; private set; }

        public bool NotVisible { get; set; }

        public bool IsTerrainGroup { get; set; }

        public object StableReference { get; set; }
    }
}
