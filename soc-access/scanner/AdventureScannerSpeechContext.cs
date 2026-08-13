using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class AdventureScannerSpeechContext : IScannerSpeechContext
    {
        private readonly ScannerResult _result;
        private readonly AdventureMapTile _tile;
        private readonly IReadOnlyList<ScannerDirectionStep> _directions;
        private readonly int _resultIndex;
        private readonly int _resultCount;

        public AdventureScannerSpeechContext(
            ScannerResult result,
            AdventureMapTile tile,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount)
        {
            _result = result;
            _tile = tile;
            _directions = directions;
            _resultIndex = resultIndex;
            _resultCount = resultCount;
        }

        public SpeechRequest ToSpeechRequest()
        {
            AdventureMapTileSpeechFormatter formatter = new AdventureMapTileSpeechFormatter();
            string text = ScannerResultSpeechFormatter.Compose(
                ScannerResultContentFormatter.Describe(
                    AdventureMapAnnouncementDefinitions.ScannerContent,
                    _result),
                ScannerSpeechUtility.FormatDirections(_directions),
                formatter.DescribeCoordinates(_tile),
                ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(text, interrupt: false);
        }
    }
}
