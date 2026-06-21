using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class TroopPlacementTileSpeechFormatter
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

            return ConfigurableAnnouncementComposer.Compose(
                TroopDeploymentAnnouncementDefinitions.Tile,
                BuildTileParts(tile, includeCoordinates: true));
        }

        public string DescribeScannerContent(TroopPlacementTile tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            return ConfigurableAnnouncementComposer.Compose(
                TroopDeploymentAnnouncementDefinitions.ScannerContent,
                BuildTileParts(tile, includeCoordinates: false));
        }

        public string DescribeCoordinates(TroopPlacementTile tile)
        {
            return tile == null ? string.Empty : HexCoordinateFormatter.Format(tile.Point);
        }

        private IEnumerable<AnnouncementPart> BuildTileParts(TroopPlacementTile tile, bool includeCoordinates)
        {
            if (!string.IsNullOrWhiteSpace(tile.TroopLabel))
            {
                yield return new AnnouncementPart(
                    TroopDeploymentAnnouncementDefinitions.TileKeys.Troop,
                    tile.TroopLabel);
            }

            string spawnPoint = DescribeSpawnPoint(tile);
            if (!string.IsNullOrWhiteSpace(spawnPoint))
            {
                yield return new AnnouncementPart(
                    TroopDeploymentAnnouncementDefinitions.TileKeys.SpawnPoint,
                    spawnPoint);
            }

            if (tile.IsImpassable)
            {
                yield return new AnnouncementPart(
                    TroopDeploymentAnnouncementDefinitions.TileKeys.Impassable,
                    ModText.Get(ModStrings.Spatial.Impassable));
            }

            if (tile.Elevation > 0)
            {
                yield return new AnnouncementPart(
                    TroopDeploymentAnnouncementDefinitions.TileKeys.Elevation,
                    ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation));
            }

            if (includeCoordinates)
            {
                yield return new AnnouncementPart(
                    TroopDeploymentAnnouncementDefinitions.TileKeys.Coordinates,
                    DescribeCoordinates(tile));
            }
        }

        private string DescribeSpawnPoint(TroopPlacementTile tile)
        {
            if (tile == null || !tile.SpawnSide.HasValue)
            {
                return string.Empty;
            }

            bool enemySpawn = _snapshot != null
                && _snapshot.OwnSide.HasValue
                && tile.SpawnSide.Value != _snapshot.OwnSide.Value;
            return enemySpawn
                ? ModText.Get(ModStrings.Spatial.EnemySpawnPoint)
                : ModText.Get(ModStrings.Spatial.SpawnPoint);
        }
    }
}
