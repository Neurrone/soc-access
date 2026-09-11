using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    public sealed class AdventureScannerSpeechContext : ScannerSpeechContext
    {
        public AdventureScannerSpeechContext(
            ScannerResult result,
            AdventureMapTile tile,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount,
            bool includeItemName)
            : base(
                result,
                AdventureMapAnnouncementDefinitions.ScannerContent,
                BuildTileParts(tile),
                () => new AdventureMapTileSpeechFormatter().DescribeCoordinates(tile),
                directions,
                resultIndex,
                resultCount,
                includeItemName)
        {
        }

        /// <summary>
        /// Whether the selected wielder can get there, and what the route
        /// costs, decide what the player would do with a result, so the scanner
        /// keeps them even though the rest of the tile belongs to the cursor.
        /// This is the adventure map's counterpart to combat reachability.
        /// </summary>
        public static IEnumerable<AnnouncementPart> BuildTileParts(AdventureMapTile tile)
        {
            string route = AdventureMapTileSpeechFormatter.DescribeReachabilityOrRoutePreview(tile);
            if (!string.IsNullOrWhiteSpace(route))
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.TileKeys.ReachabilityOrRoutePreview,
                    route);
            }

            string movementCost = AdventureMapTileSpeechFormatter.DescribeMovementCost(tile);
            if (!string.IsNullOrWhiteSpace(movementCost))
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.TileKeys.MovementCost,
                    movementCost);
            }
        }
    }
}
