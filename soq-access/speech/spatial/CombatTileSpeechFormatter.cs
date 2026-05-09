using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class CombatTileSpeechFormatter : ISpatialTileSpeechFormatter<CombatTile>
    {
        private readonly CombatAdapter _adapter;
        private readonly CombatInspectContext _context;
        private readonly bool _includeEnemyInfluence;

        public CombatTileSpeechFormatter(CombatAdapter adapter, CombatInspectContext context, bool includeEnemyInfluence = true)
        {
            _adapter = adapter;
            _context = context;
            _includeEnemyInfluence = includeEnemyInfluence;
        }

        public string DescribeTile(CombatTile tile)
        {
            if (tile == null)
            {
                return "Combat grid";
            }

            List<string> parts = new List<string>();
            AddIfPresent(parts, DescribePrimaryContent(tile));
            AddIfPresent(parts, DescribeTileContext(tile));
            AddIfPresent(parts, DescribeCoordinates(tile));
            AddIfPresent(parts, DescribeInfluence(tile));
            return string.Join(", ", parts.ToArray());
        }

        public string DescribePrimaryContent(CombatTile tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (_context == null && tile.IsReachable)
            {
                parts.Add("reachable");
            }

            if (tile.Troop != null)
            {
                parts.Add(_adapter.DescribeTroopForSpeech(tile.Troop));
            }
            else if (tile.Entity != null)
            {
                parts.Add(_adapter.DescribeEntityForSpeech(tile.Entity));
            }

            return string.Join(", ", parts.ToArray());
        }

        public string DescribeInfluence(CombatTile tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (_context != null)
            {
                _context.AddIndicators(tile.Point, parts);
            }
            else if (_includeEnemyInfluence)
            {
                _adapter.AddEnemyInfluenceForSpeech(tile.Point, tile.Troop, parts);
            }

            return string.Join(", ", parts.ToArray());
        }

        public string DescribeTileContext(CombatTile tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (!tile.IsWalkable)
            {
                parts.Add("blocked");
            }

            if (tile.Elevation > 0)
            {
                parts.Add("elevated ground, height " + tile.Elevation);
            }

            if (!string.IsNullOrWhiteSpace(tile.DecorativeFeature))
            {
                parts.Add(tile.DecorativeFeature);
            }

            return string.Join(", ", parts.ToArray());
        }

        public string DescribeCoordinates(CombatTile tile)
        {
            return tile == null ? string.Empty : CombatAdapter.FormatPoint(tile.Point);
        }

        private static void AddIfPresent(List<string> parts, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(text);
            }
        }
    }
}
