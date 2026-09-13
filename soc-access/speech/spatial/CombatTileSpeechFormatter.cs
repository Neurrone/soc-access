using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Speech.Spatial
{
    public sealed class CombatTileSpeechFormatter
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
                return ModText.Get(ModStrings.UI.Battlefield);
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

            return ModText.JoinListWithCommas(parts);
        }

        public string DescribeTroop(CombatTile tile)
        {
            if (tile == null || tile.Troop == null || _adapter == null)
            {
                return string.Empty;
            }

            BeamFacing? facing = _adapter.PerformsBeamAttacks(tile.Troop) ? _adapter.GetBeamFacing(tile.Troop) : null;
            return ComposeTroop(_adapter.GetTroopFacts(tile.Troop), tile.IsTroopAttackable, facing);
        }

        /// <summary>A stack as the cursor reads it out, from the facts alone: what it is, what it
        /// cannot do and what has been done to it, and the health it has left.</summary>
        public static string ComposeTroop(CombatTroopFacts troop, bool attackable, BeamFacing? facing)
        {
            List<AnnouncementPart> parts = new List<AnnouncementPart>();
            if (troop.IsActing)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.Acting,
                    ModText.Get(ModStrings.Spatial.Acting)));
            }

            if (attackable)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.Attackable,
                    ModText.Get(ModStrings.Scanner.Attackable)));
            }

            parts.Add(new AnnouncementPart(
                CombatAnnouncementDefinitions.TroopKeys.StackSize,
                troop.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            if (troop.IsEnemy)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.Affiliation,
                    ModText.Get(ModStrings.Spatial.Enemy)));
            }

            AnnouncementPart.AddIfPresent(parts, CombatAnnouncementDefinitions.TroopKeys.TroopName, CombatTroopText.Name(troop));
            AnnouncementPart.AddIfPresent(parts, CombatAnnouncementDefinitions.TroopKeys.Restrictions, DescribeRestrictions(troop));
            AnnouncementPart.AddIfPresent(parts, CombatAnnouncementDefinitions.TroopKeys.Effects, DescribeEffects(troop));
            AnnouncementPart.AddIfPresent(parts, CombatAnnouncementDefinitions.TroopKeys.Health, CombatTroopText.Health(troop.CurrentHealth, troop.MaxHealth));

            if (facing.HasValue)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.TroopKeys.FacingDirectionForBeamAttacks,
                    CombatText.FormatBeamFacing(facing.Value)));
            }

            return ConfigurableAnnouncementComposer.Compose(CombatAnnouncementDefinitions.Troop, parts);
        }

        /// <summary>What the stack cannot do or cannot be done to, in the game's own words except for
        /// Reloading: the game says that one as a sentence about ranged attacks, and a readout that
        /// names a stack wants a word. The mod's word leads for that reason - the game's list order
        /// among the three says nothing.</summary>
        public static string DescribeRestrictions(CombatTroopFacts troop)
        {
            List<string> parts = new List<string>();
            if (troop.IsReloading)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.Reloading));
            }

            for (int i = 0; troop.RestrictionNames != null && i < troop.RestrictionNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(troop.RestrictionNames[i]))
                {
                    parts.Add(troop.RestrictionNames[i]);
                }
            }

            return ModText.JoinListWithCommas(parts);
        }

        /// <summary>What the game's own buff and nerf indicators over the stack are called, buffs
        /// first. The names alone: what they DO is in the tile's review buffer.</summary>
        public static string DescribeEffects(CombatTroopFacts troop)
        {
            List<string> parts = new List<string>();
            for (int i = 0; troop.EffectNames != null && i < troop.EffectNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(troop.EffectNames[i]))
                {
                    parts.Add(troop.EffectNames[i]);
                }
            }

            return ModText.JoinListWithCommas(parts);
        }

        public string DescribeEntity(CombatTile tile)
        {
            if (tile == null || tile.Entity == null || _adapter == null)
            {
                return string.Empty;
            }

            CombatEntityFacts entity = _adapter.GetEntityFacts(tile.Entity);
            List<AnnouncementPart> parts = new List<AnnouncementPart>();
            if (tile.IsEntityAttackable)
            {
                parts.Add(new AnnouncementPart(
                    CombatAnnouncementDefinitions.EntityKeys.Attackable,
                    ModText.Get(ModStrings.Scanner.Attackable)));
            }

            AnnouncementPart.AddIfPresent(parts, CombatAnnouncementDefinitions.EntityKeys.EntityName, entity.Name);
            AnnouncementPart.AddIfPresent(
                parts,
                CombatAnnouncementDefinitions.EntityKeys.Health,
                entity.HasHealth ? CombatTroopText.Health(entity.HealthLeft, entity.MaxHealth) : string.Empty);
            return ConfigurableAnnouncementComposer.Compose(CombatAnnouncementDefinitions.Entity, parts);
        }

        public string DescribeCoordinates(CombatTile tile)
        {
            return tile == null ? string.Empty : CombatText.FormatPoint(tile.Point);
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

            if (tile.IsImpassable)
            {
                // What is standing there where the board has a name for it - "boulders,
                // impassable" - and the bare word where it has none.
                string impassable = BattlefieldText.CellImpassable(tile.Obstacle);
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Impassable,
                    string.IsNullOrEmpty(impassable) ? ModText.Get(ModStrings.Spatial.Impassable) : impassable);
            }

            string tileEffects = DescribeTileEffects(tile);
            if (!string.IsNullOrWhiteSpace(tileEffects))
            {
                yield return new AnnouncementPart(CombatAnnouncementDefinitions.TileKeys.TileEffects, tileEffects);
            }

            if (tile.Elevation > 0)
            {
                // A cliff, a wall, a tower or a flight of stairs is named by what it is; ordinary
                // raised ground is named by its height alone.
                string ground = BattlefieldText.CellGround(tile.Kind, tile.Elevation);
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Elevation,
                    string.IsNullOrEmpty(ground)
                        ? ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation)
                        : ground);
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

        private string DescribeOccupant(CombatTile tile)
        {
            if (tile.Troop != null)
            {
                return DescribeTroop(tile);
            }

            return tile.Entity != null ? DescribeEntity(tile) : string.Empty;
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

            return ModText.JoinListWithCommas(parts);
        }

        private static void AddTilePartIfPresent(string key, string text, out AnnouncementPart part)
        {
            part = string.IsNullOrWhiteSpace(text) ? null : new AnnouncementPart(key, text);
        }
    }
}
