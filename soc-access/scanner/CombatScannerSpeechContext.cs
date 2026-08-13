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
            CombatTileSpeechFormatter formatter = new CombatTileSpeechFormatter(_adapter, null, includeEnemyInfluence: false);
            string text = ScannerResultSpeechFormatter.Compose(
                ScannerResultContentFormatter.Describe(
                    CombatAnnouncementDefinitions.ScannerContent,
                    _result),
                ScannerSpeechUtility.FormatDirections(_directions),
                formatter.DescribeCoordinates(_tile),
                ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(text, interrupt: false);
        }
    }
}
