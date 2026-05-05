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
            List<string> parts = new List<string>();
            TroopPlacementTileSpeechFormatter formatter = new TroopPlacementTileSpeechFormatter(_snapshot);
            if (_result != null && _result.IsTerrainGroup)
            {
                ScannerSpeechUtility.AddIfPresent(parts, _result.Label);
            }
            else
            {
                ScannerSpeechUtility.AddIfPresent(parts, formatter.DescribePrimaryContent(_tile));
                ScannerSpeechUtility.AddIfPresent(parts, formatter.DescribeTileContext(_tile));
            }

            ScannerSpeechUtility.AddIfPresent(parts, ScannerSpeechUtility.FormatDirections(_directions));
            ScannerSpeechUtility.AddIfPresent(parts, formatter.DescribeCoordinates(_tile));
            ScannerSpeechUtility.AddIfPresent(parts, ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(string.Join(". ", parts.ToArray()), interrupt: false);
        }
    }
}
