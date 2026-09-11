using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// What one scanner result sounds like: the item it belongs to, what tells it apart, the way
    /// there, where it is and which of how many it is. Only three things differ between the
    /// adventure map, the battle board and troop placement - the announcement group whose order and
    /// suffixes the player owns, the tile facts that change what they would do with the result, and
    /// how that surface writes a coordinate - so each surface passes those three and inherits the
    /// rest. The tile parts and the coordinates are read when the line is composed, not when the
    /// context is built.
    /// </summary>
    public abstract class ScannerSpeechContext : IScannerSpeechContext
    {
        private readonly ScannerResult _result;
        private readonly AnnouncementGroupDefinition _contentGroup;
        private readonly IEnumerable<AnnouncementPart> _tileParts;
        private readonly Func<string> _coordinates;
        private readonly IReadOnlyList<ScannerDirectionStep> _directions;
        private readonly int _resultIndex;
        private readonly int _resultCount;
        private readonly bool _includeItemName;

        protected ScannerSpeechContext(
            ScannerResult result,
            AnnouncementGroupDefinition contentGroup,
            IEnumerable<AnnouncementPart> tileParts,
            Func<string> coordinates,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount,
            bool includeItemName)
        {
            _result = result;
            _contentGroup = contentGroup;
            _tileParts = tileParts;
            _coordinates = coordinates;
            _directions = directions;
            _resultIndex = resultIndex;
            _resultCount = resultCount;
            _includeItemName = includeItemName;
        }

        public SpeechRequest ToSpeechRequest()
        {
            string text = ScannerResultSpeechFormatter.Compose(
                ScannerResultSpeechFormatter.ItemName(_result, _includeItemName),
                ScannerResultContentFormatter.Describe(_contentGroup, _result, _tileParts),
                ScannerSpeechUtility.FormatDirections(_directions),
                _coordinates == null ? string.Empty : _coordinates(),
                ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(text, interrupt: false);
        }
    }
}
