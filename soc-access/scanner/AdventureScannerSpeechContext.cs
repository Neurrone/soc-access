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
                _includeItemName && _result != null ? _result.ItemLabel : null,
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
