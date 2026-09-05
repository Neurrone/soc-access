using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    public sealed class AdventureScannerSpeechContext : IScannerSpeechContext
    {
        private readonly ScannerResult _result;
        private readonly AdventureMapTile _tile;
        private readonly IReadOnlyList<ScannerDirectionStep> _directions;
        private readonly int _resultIndex;
        private readonly int _resultCount;
        private readonly bool _includeItemName;

        public AdventureScannerSpeechContext(
            ScannerResult result,
            AdventureMapTile tile,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount,
            bool includeItemName)
        {
            _result = result;
            _tile = tile;
            _directions = directions;
            _resultIndex = resultIndex;
            _resultCount = resultCount;
            _includeItemName = includeItemName;
        }

        public SpeechRequest ToSpeechRequest()
        {
            AdventureMapTileSpeechFormatter formatter = new AdventureMapTileSpeechFormatter();
            string text = ScannerResultSpeechFormatter.Compose(
                ScannerResultSpeechFormatter.ItemName(_result, _includeItemName),
                ScannerResultContentFormatter.Describe(
                    AdventureMapAnnouncementDefinitions.ScannerContent,
                    _result,
                    BuildTileParts(_tile)),
                ScannerSpeechUtility.FormatDirections(_directions),
                formatter.DescribeCoordinates(_tile),
                ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(text, interrupt: false);
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
