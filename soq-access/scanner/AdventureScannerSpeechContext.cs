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
            List<string> parts = new List<string>();
            AdventureMapTileSpeechFormatter formatter = new AdventureMapTileSpeechFormatter();

            if (_result != null && _result.IsTerrainGroup)
            {
                ScannerSpeechUtility.AddIfPresent(parts, _result.Label);
            }
            else
            {
                string primary = formatter.DescribePrimaryContent(_tile);
                if (_result != null && _result.NotVisible && !string.IsNullOrWhiteSpace(primary))
                {
                    primary += ", not visible";
                }

                ScannerSpeechUtility.AddIfPresent(parts, primary);
                ScannerSpeechUtility.AddIfPresent(parts, formatter.DescribeTileContext(_tile));
            }

            ScannerSpeechUtility.AddIfPresent(parts, ScannerSpeechUtility.FormatDirections(_directions));
            ScannerSpeechUtility.AddIfPresent(parts, formatter.DescribeCoordinates(_tile));
            ScannerSpeechUtility.AddIfPresent(parts, ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(string.Join(". ", parts.ToArray()), interrupt: false);
        }
    }
}
