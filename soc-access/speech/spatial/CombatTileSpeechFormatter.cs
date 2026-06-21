using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class CombatTileSpeechFormatter
    {
        private readonly CombatAdapter _adapter;
        private readonly CombatInspectContext _context;
        private readonly bool _includeEnemyInfluence;
        private readonly bool _selectedForSpellcast;

        public CombatTileSpeechFormatter(
            CombatAdapter adapter,
            CombatInspectContext context,
            bool includeEnemyInfluence = true,
            bool selectedForSpellcast = false)
        {
            _adapter = adapter;
            _context = context;
            _includeEnemyInfluence = includeEnemyInfluence;
            _selectedForSpellcast = selectedForSpellcast;
        }

        public string DescribeTile(CombatTile tile)
        {
            if (tile == null)
            {
                return ModText.Get(ModStrings.UI.CombatGrid);
            }

            return ConfigurableAnnouncementComposer.Compose(
                CombatAnnouncementDefinitions.Tile,
                BuildTileParts(tile));
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

        public string DescribeScannerContent(CombatTile tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            return ConfigurableAnnouncementComposer.Compose(
                CombatAnnouncementDefinitions.ScannerContent,
                BuildScannerContentParts(tile));
        }

        public string DescribeTroop(CombatTile tile)
        {
            if (tile == null || tile.Troop == null || _adapter == null)
            {
                return string.Empty;
            }

            List<AnnouncementPart> parts = new List<AnnouncementPart>();
            if (_adapter.IsActingTroop(tile.Troop))
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.Acting,
                    ModText.Get(ModStrings.Spatial.Acting)));
            }

            if (tile.IsTroopAttackable)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.Attackable,
                    ModText.Get(ModStrings.Scanner.Attackable)));
            }

            parts.Add(new AnnouncementPart(
                CombatAnnouncementDefinitions.TroopKeys.StackSize,
                _adapter.GetTroopStackSize(tile.Troop).ToString(System.Globalization.CultureInfo.InvariantCulture)));

            if (_adapter.IsEnemyTroop(tile.Troop))
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.Affiliation,
                    ModText.Get(ModStrings.Spatial.Enemy)));
            }

            AddIfPresent(parts, CombatAnnouncementDefinitions.TroopKeys.TroopName, _adapter.GetTroopNameForSpeech(tile.Troop));
            AddIfPresent(parts, CombatAnnouncementDefinitions.TroopKeys.Health, _adapter.GetTroopHealthForSpeech(tile.Troop));

            BeamFacing? facing = _adapter.PerformsBeamAttacks(tile.Troop) ? _adapter.GetBeamFacing(tile.Troop) : null;
            if (facing.HasValue)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.FacingDirectionForBeamAttacks,
                    CombatText.FormatBeamFacing(facing.Value)));
            }

            return ConfigurableAnnouncementComposer.Compose(CombatAnnouncementDefinitions.Troop, parts);
        }

        public string DescribeEntity(CombatTile tile)
        {
            if (tile == null || tile.Entity == null || _adapter == null)
            {
                return string.Empty;
            }

            List<AnnouncementPart> parts = new List<AnnouncementPart>();
            if (tile.IsEntityAttackable)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.EntityKeys.Attackable,
                    ModText.Get(ModStrings.Scanner.Attackable)));
            }

            AddIfPresent(parts, CombatAnnouncementDefinitions.EntityKeys.EntityName, _adapter.GetEntityNameForSpeech(tile.Entity));
            AddIfPresent(parts, CombatAnnouncementDefinitions.EntityKeys.Health, _adapter.GetEntityHealthForSpeech(tile.Entity));
            return ConfigurableAnnouncementComposer.Compose(CombatAnnouncementDefinitions.Entity, parts);
        }

        public string DescribeCoordinates(CombatTile tile)
        {
            return tile == null ? string.Empty : CombatAdapter.FormatPoint(tile.Point);
        }

        private IEnumerable<AnnouncementPart> BuildTileParts(CombatTile tile)
        {
            if (_selectedForSpellcast)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.SelectedForSpellcast,
                    ModText.Get(ModStrings.UI.Selected));
            }

            if (_context == null && tile.IsReachable)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Reachable,
                    ModText.Get(ModStrings.Spatial.Reachable));
            }

            AddTilePartIfPresent(CombatAnnouncementDefinitions.TileKeys.Occupant, DescribeOccupant(tile), out AnnouncementPart occupant);
            if (occupant != null)
            {
                yield return occupant;
            }

            string impassableOrBlocked = DescribeImpassableOrBlocked(tile);
            if (!string.IsNullOrWhiteSpace(impassableOrBlocked))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.ImpassableOrBlocked, impassableOrBlocked);
            }

            string tileEffects = DescribeTileEffects(tile);
            if (!string.IsNullOrWhiteSpace(tileEffects))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.TileEffects, tileEffects);
            }

            if (tile.Elevation > 0)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Elevation,
                    ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation));
            }

            if (!string.IsNullOrWhiteSpace(tile.DecorativeFeature))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.DecorativeFeatures, tile.DecorativeFeature);
            }

            yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.Coordinates, DescribeCoordinates(tile));

            string influence = DescribeInfluence(tile);
            if (!string.IsNullOrWhiteSpace(influence))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.Influence, influence);
            }
        }

        private IEnumerable<AnnouncementPart> BuildScannerContentParts(CombatTile tile)
        {
            if (_context == null && tile.IsReachable)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Reachable,
                    ModText.Get(ModStrings.Spatial.Reachable));
            }

            AddTilePartIfPresent(CombatAnnouncementDefinitions.TileKeys.Occupant, DescribeOccupant(tile), out AnnouncementPart occupant);
            if (occupant != null)
            {
                yield return occupant;
            }

            string impassableOrBlocked = DescribeImpassableOrBlocked(tile);
            if (!string.IsNullOrWhiteSpace(impassableOrBlocked))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.ImpassableOrBlocked, impassableOrBlocked);
            }

            string tileEffects = DescribeTileEffects(tile);
            if (!string.IsNullOrWhiteSpace(tileEffects))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.TileEffects, tileEffects);
            }

            if (tile.Elevation > 0)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Elevation,
                    ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation));
            }

            if (!string.IsNullOrWhiteSpace(tile.DecorativeFeature))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.DecorativeFeatures, tile.DecorativeFeature);
            }
        }

        private string DescribeOccupant(CombatTile tile)
        {
            if (tile.Troop != null)
            {
                return DescribeTroop(tile);
            }

            return tile.Entity != null ? DescribeEntity(tile) : string.Empty;
        }

        private static string DescribeImpassableOrBlocked(CombatTile tile)
        {
            if (tile.IsImpassable)
            {
                return ModText.Get(ModStrings.Spatial.Impassable);
            }

            return tile.IsBlocked ? ModText.Get(ModStrings.Spatial.Blocked) : string.Empty;
        }

        private static string DescribeTileEffects(CombatTile tile)
        {
            List<string> parts = new List<string>();
            foreach (string mapEffect in tile.MapEffects)
            {
                if (!string.IsNullOrWhiteSpace(mapEffect))
                {
                    parts.Add(mapEffect);
                }
            }

            return string.Join(", ", parts.ToArray());
        }

        private static void AddIfPresent(List<AnnouncementPart> parts, string key, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(new AnnouncementPart(key, text));
            }
        }

        private static void AddTilePartIfPresent(string key, string text, out AnnouncementPart part)
        {
            part = string.IsNullOrWhiteSpace(text) ? null : new AnnouncementPart(key, text);
        }
    }
}
