using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
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

        public TroopPlacementScannerSpeechContext(
            ScannerResult result,
            TroopPlacementTile tile,
            TroopPlacementSnapshot snapshot,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount)
        {
            _result = result;
            _tile = tile;
            _snapshot = snapshot;
            _directions = directions;
            _resultIndex = resultIndex;
            _resultCount = resultCount;
        }

        public SpeechRequest ToSpeechRequest()
        {
            TroopPlacementTileSpeechFormatter formatter = new TroopPlacementTileSpeechFormatter(_snapshot);
            string text = ScannerResultSpeechFormatter.Compose(
                ScannerResultContentFormatter.Describe(
                    TroopDeploymentAnnouncementDefinitions.ScannerContent,
                    _result),
                ScannerSpeechUtility.FormatDirections(_directions),
                formatter.DescribeCoordinates(_tile),
                ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(text, interrupt: false);
        }
    }
}
