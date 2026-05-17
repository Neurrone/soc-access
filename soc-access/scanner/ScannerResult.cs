using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal enum ScannerResultKind
    {
        Point,
        TerrainPoint,
        TerrainGroup,
        AreaGroup,
        CommanderZoneOfControl
    }

    internal sealed class ScannerResult
    {
        public ScannerResult(string label, Vector2Int position)
        {
            Label = label;
            Position = position;
            Points = new List<Vector2Int>();
        }

        public string Label { get; private set; }

        public Vector2Int Position { get; private set; }

        public bool NotVisible { get; set; }

        public ScannerResultKind Kind { get; set; }

        public object StableReference { get; set; }

        public List<Vector2Int> Points { get; private set; }
    }
}
