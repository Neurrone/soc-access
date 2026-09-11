using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    public sealed class TroopPlacementScannerSpeechContext : ScannerSpeechContext
    {
        public TroopPlacementScannerSpeechContext(
            ScannerResult result,
            TroopPlacementTile tile,
            TroopPlacementSnapshot snapshot,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount,
            bool includeItemName)
            : base(
                result,
                TroopDeploymentAnnouncementDefinitions.ScannerContent,
                BuildTileParts(tile, result),
                () => new TroopPlacementTileSpeechFormatter(snapshot).DescribeCoordinates(tile),
                directions,
                resultIndex,
                resultCount,
                includeItemName)
        {
        }

        /// <summary>
        /// How high the ground is decides where the player wants a troop, so the
        /// scanner keeps it. A terrain result already names it as its subject,
        /// so it does not get it twice.
        /// </summary>
        private static IEnumerable<AnnouncementPart> BuildTileParts(TroopPlacementTile tile, ScannerResult result)
        {
            if (tile == null || tile.Elevation <= 0 || result == null || result.Kind == ScannerResultKind.TerrainPoint)
            {
                yield break;
            }

            yield return new AnnouncementPart(
                TroopDeploymentAnnouncementDefinitions.TileKeys.Elevation,
                ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation));
        }
    }
}
