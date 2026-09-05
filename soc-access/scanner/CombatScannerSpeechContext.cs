using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    public sealed class CombatScannerSpeechContext : IScannerSpeechContext
    {
        private readonly ScannerResult _result;
        private readonly CombatTile _tile;
        private readonly CombatAdapter _adapter;
        private readonly IReadOnlyList<ScannerDirectionStep> _directions;
        private readonly int _resultIndex;
        private readonly int _resultCount;
        private readonly bool _includeItemName;

        public CombatScannerSpeechContext(
            ScannerResult result,
            CombatTile tile,
            CombatAdapter adapter,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount,
            bool includeItemName)
        {
            _result = result;
            _tile = tile;
            _adapter = adapter;
            _directions = directions;
            _resultIndex = resultIndex;
            _resultCount = resultCount;
            _includeItemName = includeItemName;
        }

        public SpeechRequest ToSpeechRequest()
        {
            CombatTileSpeechFormatter formatter = new CombatTileSpeechFormatter(_adapter, null, includeEnemyInfluence: false);
            string text = ScannerResultSpeechFormatter.Compose(
                ScannerResultSpeechFormatter.ItemName(_result, _includeItemName),
                ScannerResultContentFormatter.Describe(
                    CombatAnnouncementDefinitions.ScannerContent,
                    _result,
                    BuildTileParts()),
                ScannerSpeechUtility.FormatDirections(_directions),
                formatter.DescribeCoordinates(_tile),
                ScannerSpeechUtility.FormatResultCount(_resultIndex, _resultCount));
            return new SpeechRequest(text, interrupt: false);
        }

        /// <summary>
        /// Whether the acting troop can get there, and how high the ground is,
        /// both decide what the player would do with a result, so the scanner
        /// keeps them even though the rest of the tile belongs to the cursor.
        /// A terrain result already names one of these as its subject, so it
        /// does not get it twice.
        /// </summary>
        private IEnumerable<AnnouncementPart> BuildTileParts()
        {
            if (_tile == null)
            {
                yield break;
            }

            if (_tile.IsReachable)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Reachable,
                    ModText.Get(ModStrings.Spatial.Reachable));
            }

            if (_tile.Elevation > 0 && _result != null && _result.Kind != ScannerResultKind.TerrainPoint)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Elevation,
                    ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, _tile.Elevation));
            }
        }
    }
}
