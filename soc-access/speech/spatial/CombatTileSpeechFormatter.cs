using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;

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
                return ModText.Get(ModStrings.UI.CombatGrid);
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
                parts.Add(ModText.Get(ModStrings.Spatial.Reachable));
            }

            if (tile.Troop != null)
            {
                parts.Add(FormatAttackablePrefix(_adapter.DescribeTroopForSpeech(tile.Troop), tile.IsTroopAttackable));
                BeamFacing? facing = _adapter != null && _adapter.PerformsBeamAttacks(tile.Troop)
                    ? _adapter.GetBeamFacing(tile.Troop)
                    : null;
                if (facing.HasValue)
                {
                    parts.Add(CombatText.FormatBeamFacing(facing.Value));
                }
            }
            else if (tile.Entity != null)
            {
                parts.Add(FormatAttackablePrefix(_adapter.DescribeEntityForSpeech(tile.Entity), tile.IsEntityAttackable));
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
                if (ModSettings.ReadEnemyInfluence)
                {
                    _adapter.AddEnemyInfluenceForSpeech(tile.Point, tile.Troop, parts);
                }
                else if (_adapter.IsThreatenedByEnemy(tile.Point, tile.Troop))
                {
                    parts.Add(ModText.Get(ModStrings.Spatial.Threatened));
                }
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
            if (tile.IsImpassable)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.Impassable));
            }
            else if (tile.IsBlocked)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.Blocked));
            }

            foreach (string mapEffect in tile.MapEffects)
            {
                if (!string.IsNullOrWhiteSpace(mapEffect))
                {
                    parts.Add(mapEffect);
                }
            }

            if (tile.Elevation > 0)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation));
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

        internal static string FormatAttackablePrefix(string label, bool isAttackable)
        {
            if (!isAttackable || string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            return ModText.Get(ModStrings.Common.ListSeparator, ModText.Get(ModStrings.Scanner.Attackable), label);
        }
    }
}
