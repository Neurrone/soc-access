using System.Collections.Generic;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Speech.Spatial
{
    public sealed class TroopPlacementTileSpeechFormatter
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
                BuildTileParts(tile));
        }

        public string DescribeCoordinates(TroopPlacementTile tile)
        {
            return tile == null ? string.Empty : HexCoordinateFormatter.Format(tile.Point);
        }

        private IEnumerable<AnnouncementPart> BuildTileParts(TroopPlacementTile tile)
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

            if (!tile.IsImpassable && (tile.Elevation > 0 || tile.Kind == BattlefieldCellKind.Unreachable))
            {
                // A cliff, a wall, a tower or a flight of stairs is named by what it is; ordinary
                // raised ground is named by its height alone. Unreachable ground is named at any
                // height, since the pocket it belongs to is mostly at height 0. A cell nothing can
                // enter says no height at all: how high a blocked hex stands is no use to a player
                // who can never put a troop on it.
                string ground = BattlefieldText.CellGround(tile.Kind, tile.Elevation);
                yield return new AnnouncementPart(
                    TroopDeploymentAnnouncementDefinitions.TileKeys.Elevation,
                    string.IsNullOrEmpty(ground)
                        ? ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation)
                        : ground);
            }

            yield return new AnnouncementPart(
                TroopDeploymentAnnouncementDefinitions.TileKeys.Coordinates,
                DescribeCoordinates(tile));
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
