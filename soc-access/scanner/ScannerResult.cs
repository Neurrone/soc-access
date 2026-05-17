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
        public ScannerResult(string key, string label, Vector2Int position)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new System.ArgumentException("Scanner result key is required.", "key");
            }

            Key = key;
            Label = label;
            Position = position;
            Points = new List<Vector2Int>();
        }

        public string Label { get; private set; }

        public string Key { get; private set; }

        public Vector2Int Position { get; private set; }

        public bool NotVisible { get; set; }

        public ScannerResultKind Kind { get; set; }

        public object StableReference { get; set; }

        public List<Vector2Int> Points { get; private set; }
    }

    internal static class ScannerResultLabels
    {
        public static string ZoneOfControl(int tileCount, string name)
        {
            string tileWord = tileCount == 1 ? " tile" : " tiles";
            return tileCount + tileWord + " within " + FormatPossessive(name) + " zone of control";
        }

        private static string FormatPossessive(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "commander's";
            }

            return name.EndsWith("s") || name.EndsWith("S") ? name + "'" : name + "'s";
        }
    }
}
