using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class TroopPlacementTileSpeechFormatter : ISpatialTileSpeechFormatter<TroopPlacementTile>
    {
        private readonly TroopPlacementSnapshot _snapshot;

        public TroopPlacementTileSpeechFormatter(TroopPlacementSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public string DescribeTile(TroopPlacementTile tile)
        {
            if (tile == null)
            {
                return ModText.Get(ModStrings.Screens.TroopPlacement);
            }

            List<string> parts = new List<string>();
            AddIfPresent(parts, DescribePrimaryContent(tile));
            AddIfPresent(parts, DescribeTileContext(tile));
            AddIfPresent(parts, DescribeCoordinates(tile));
            return string.Join(", ", parts.ToArray());
        }

        public string DescribePrimaryContent(TroopPlacementTile tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(tile.TroopLabel))
            {
                parts.Add(tile.TroopLabel);
            }

            if (tile.SpawnSide.HasValue)
            {
                bool enemySpawn = _snapshot != null
                    && _snapshot.OwnSide.HasValue
                    && tile.SpawnSide.Value != _snapshot.OwnSide.Value;
                parts.Add(enemySpawn
                    ? ModText.Get(ModStrings.Spatial.EnemySpawnPoint)
                    : ModText.Get(ModStrings.Spatial.SpawnPoint));
            }

            if (!string.IsNullOrWhiteSpace(tile.EntityLabel))
            {
                parts.Add(tile.EntityLabel);
            }

            return string.Join(", ", parts.ToArray());
        }

        public string DescribeTileContext(TroopPlacementTile tile)
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

            if (tile.Elevation > 0)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation));
            }

            return string.Join(", ", parts.ToArray());
        }

        public string DescribeCoordinates(TroopPlacementTile tile)
        {
            return tile == null ? string.Empty : HexCoordinateFormatter.Format(tile.Point);
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
