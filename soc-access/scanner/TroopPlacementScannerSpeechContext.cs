using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class TroopPlacementScannerSpeechContext : IScannerSpeechContext
    {
        private readonly ScannerResult _result;
        private readonly TroopPlacementTile _tile;
        private readonly TroopPlacementSnapshot _snapshot;
        private readonly IReadOnlyList<ScannerDirectionStep> _directions;
        private readonly int _resultIndex;
        private readonly int _resultCount;
        private readonly bool _includeItemName;

        public TroopPlacementScannerSpeechContext(
            ScannerResult result,
            TroopPlacementTile tile,
            TroopPlacementSnapshot snapshot,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount,
            bool includeItemName)
        {
            _result = result;
            _tile = tile;
            _snapshot = snapshot;
            _directions = directions;
            _resultIndex = resultIndex;
            _resultCount = resultCount;
            _includeItemName = includeItemName;
        }

        public SpeechRequest ToSpeechRequest()
        {
            TroopPlacementTileSpeechFormatter formatter = new TroopPlacementTileSpeechFormatter(_snapshot);
            string text = ScannerResultSpeechFormatter.Compose(
                ScannerResultSpeechFormatter.ItemName(_result, _includeItemName),
                ScannerResultContentFormatter.Describe(
                    TroopDeploymentAnnouncementDefinitions.ScannerContent,
                    _result,
                    BuildTileParts()),
                ScannerSpeechUtility.FormatDirections(_directions),
                formatter.DescribeCoordinates(_tile),
                ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(text, interrupt: false);
        }

        /// <summary>
        /// How high the ground is decides where the player wants a troop, so the
        /// scanner keeps it. A terrain result already names it as its subject,
        /// so it does not get it twice.
        /// </summary>
        private IEnumerable<AnnouncementPart> BuildTileParts()
        {
            if (_tile == null || _tile.Elevation <= 0 || _result == null || _result.Kind == ScannerResultKind.TerrainPoint)
            {
                yield break;
            }

            yield return new AnnouncementPart(
                TroopDeploymentAnnouncementDefinitions.TileKeys.Elevation,
                ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, _tile.Elevation));
        }
    }
}
