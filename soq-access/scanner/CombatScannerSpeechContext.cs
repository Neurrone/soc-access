using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class CombatScannerSpeechContext : IScannerSpeechContext
    {
        private readonly ScannerResult _result;
        private readonly CombatTile _tile;
        private readonly CombatAdapter _adapter;
        private readonly IReadOnlyList<ScannerDirectionStep> _directions;
        private readonly int _resultIndex;
        private readonly int _resultCount;

        public CombatScannerSpeechContext(
            ScannerResult result,
            CombatTile tile,
            CombatAdapter adapter,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount)
        {
            _result = result;
            _tile = tile;
            _adapter = adapter;
            _directions = directions;
            _resultIndex = resultIndex;
            _resultCount = resultCount;
        }

        public SpeechRequest ToSpeechRequest()
        {
            List<string> parts = new List<string>();
            CombatTileSpeechFormatter formatter = new CombatTileSpeechFormatter(_adapter, null, includeEnemyInfluence: false);
            if (_result != null && _result.Kind == ScannerResultKind.TerrainGroup)
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
